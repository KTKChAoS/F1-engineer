using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using PitWall.Contracts;
using PitWall.Contracts.Enums;
using PitWall.Contracts.Messages;
using PitWall.Core.Recording;
using PitWall.Core.State;
using PitWall.Core.Telemetry;
using Serilog;

namespace PitWall.Host;

public partial class MainWindow : Window
{
    private readonly StateStore _store = new();
    private readonly Recorder _recorder = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private TelemetryPipeline? _pipeline;
    private LocalWebHost? _webHost;
    private CancellationTokenSource? _replayCts;
    private readonly string _recordingsDir;
    private readonly int _udpPort = Constants.DefaultUdpPort;
    private bool _loadingCallDevices;
    private string? _callOutputDeviceId;
    private string? _replayStatus;

    public MainWindow()
    {
        InitializeComponent();
        _recordingsDir = ProjectPaths.RecordingsDirectory;
        LoadCallOutputDevices();
        IpText.Text = "This PC: " + string.Join(", ", GetPrivateIpv4Addresses());
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.Tick += (_, _) => RefreshUi();
        _timer.Start();
        try
        {
            _pipeline = new TelemetryPipeline(_udpPort, _store, _recorder, Log.Logger);
            _pipeline.Start();
            _webHost = new LocalWebHost(_store, Constants.DefaultWebPort, OnCallReceived);
            await _webHost.StartAsync();
            Log.Information("Local dashboard listening on port {Port}", Constants.DefaultWebPort);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to bind UDP");
            StatusText.Text = ex.Message;
            StatusDot.Fill = (Brush)FindResource("AccentBrush");
        }

        await Task.CompletedTask;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _replayCts?.Cancel();
        _recorder.Dispose();
        if (_pipeline is not null)
        {
            await _pipeline.DisposeAsync();
        }
        if (_webHost is not null)
        {
            await _webHost.StopAsync();
            await _webHost.DisposeAsync();
        }
    }

    private void RefreshUi()
    {
        var snap = _store.Current;
        var age = snap.LastPacketUtc is { } last
            ? DateTimeOffset.UtcNow - last
            : Timeout.InfiniteTimeSpan;
        var connection = age == Timeout.InfiniteTimeSpan
            ? ConnectionState.Waiting
            : age.TotalMilliseconds <= Constants.ConnectionStaleThresholdMs
                ? ConnectionState.Receiving
                : ConnectionState.Stale;

        StatusText.Text = connection switch
        {
            ConnectionState.Receiving => "Receiving",
            ConnectionState.Stale => "Stale — no packet in 3s",
            _ => "Waiting for packets"
        };
        StatusDot.Fill = connection switch
        {
            ConnectionState.Receiving => (Brush)FindResource("OkBrush"),
            ConnectionState.Stale => (Brush)FindResource("WarnBrush"),
            _ => (Brush)FindResource("MutedBrush")
        };

        PpsText.Text = $"Packets/sec: {snap.Pps}";
        AgeText.Text = age == Timeout.InfiniteTimeSpan
            ? "Last packet: never"
            : $"Last packet: {age.TotalMilliseconds:0} ms ago";
        FormatText.Text = snap.PacketFormat == 0
            ? "Packet format: —"
            : $"Packet format: {snap.PacketFormat}";
        CountsText.Text = $"Received {snap.Received}  dropped {snap.Dropped}  malformed {snap.Malformed}";
        FormatWarning.Text = snap.WrongPacketFormat
            ? "This is not UDP Format 2025. In F1 25 set Telemetry → UDP Format to 2025."
            : "";

        var p = snap.Player;
        var d = snap.Driver;
        SessionText.Text = string.IsNullOrEmpty(snap.Track)
            ? "No session yet"
            : $"{snap.SessionType}  {snap.Track}  weather {snap.Weather}  {snap.TrackTempC:0}°C / {snap.AirTempC:0}°C";
        SpeedText.Text = $"{d.SpeedKph:0} km/h";
        GearText.Text = $"Gear {FormatGear(d.Gear)}   RPM {d.Rpm:0} / {d.MaxRpm:0}   DRS {d.Drs}";
        InputsText.Text = $"Throttle {d.Throttle:0%}   Brake {d.Brake:0%}   Steer {d.Steer:+0.00;-0.00;0}";
        LapText.Text = $"P{p.Position}  lap {p.Lap}  sector {p.Sector}  current {FormatMs(p.CurrentLapMs)}  last {FormatMs(p.LastLapMs)}";
        TyreText.Text = $"{snap.VisualCompound}  age {snap.TyreAgeLaps}  wear {FormatWheels(snap.Tyres.WearPct)}";
        EnergyText.Text = $"Fuel {snap.Energy.FuelKg:0.0} kg ({snap.Energy.FuelLaps:0.0} laps)   ERS {snap.Energy.ErsPct:0}% {snap.Energy.ErsMode}";
        RecordStatus.Text = _recorder.IsRecording ? $"Recording {_recorder.Path}" : _replayStatus ?? "";
    }

    private void OnCallReceived(CallMessage call)
    {
        var playSound = false;
        var speak = false;
        string? outputDeviceId = null;
        try
        {
            Dispatcher.Invoke(() =>
            {
                playSound = CallSoundCheck.IsChecked == true;
                speak = CallSpeechCheck.IsChecked == true;
                outputDeviceId = _callOutputDeviceId;
            });
        }
        catch (TaskCanceledException) { return; }
        catch (InvalidOperationException) { return; }

        if (playSound)
            _ = Task.Run(() => HostCallAudio.PlayAlertAsync(outputDeviceId));
        if (speak)
            _ = Task.Run(() => HostCallAudio.SpeakAsync(call.Text, outputDeviceId));
    }

    private void LoadCallOutputDevices()
    {
        _loadingCallDevices = true;
        CallOutputDeviceCombo.Items.Clear();
        CallOutputDeviceCombo.Items.Add(new ComboBoxItem { Content = "Default audio output", Tag = "" });
        string? savedDeviceId = null;
        try
        {
            if (File.Exists(ProjectPaths.LocalSettingsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(ProjectPaths.LocalSettingsPath));
                if (document.RootElement.TryGetProperty("callOutputDeviceId", out var deviceId))
                    savedDeviceId = deviceId.GetString();
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Could not read local audio settings"); }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                CallOutputDeviceCombo.Items.Add(new ComboBoxItem { Content = device.FriendlyName, Tag = device.ID });
                device.Dispose();
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Could not enumerate audio output devices"); }

        var savedItem = CallOutputDeviceCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, savedDeviceId));
        CallOutputDeviceCombo.SelectedItem = savedItem ?? CallOutputDeviceCombo.Items[0];
        _callOutputDeviceId = savedItem?.Tag as string;
        _loadingCallDevices = false;
    }

    private void OnCallOutputDeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingCallDevices || CallOutputDeviceCombo.SelectedItem is not ComboBoxItem selected)
            return;
        _callOutputDeviceId = string.IsNullOrWhiteSpace(selected.Tag as string) ? null : selected.Tag as string;
        try
        {
            File.WriteAllText(ProjectPaths.LocalSettingsPath, JsonSerializer.Serialize(new { callOutputDeviceId = _callOutputDeviceId }));
        }
        catch (Exception ex) { Log.Warning(ex, "Could not save local audio settings"); }
    }

    private void OnStartRecording(object sender, RoutedEventArgs e)
    {
        _replayStatus = null;
        var snap = _store.Current;
        var path = _recorder.Start(_recordingsDir, snap.Track, snap.SessionType, DateTimeOffset.UtcNow);
        RecordStatus.Text = $"Recording {path}";
    }

    private void OnStopRecording(object sender, RoutedEventArgs e)
    {
        _recorder.Stop();
        _replayStatus = "Recording stopped";
        RecordStatus.Text = _replayStatus;
    }

    private async void OnReplay(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PitWall recordings (*.pwrec)|*.pwrec",
            InitialDirectory = Directory.Exists(_recordingsDir) ? _recordingsDir : null
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _replayCts?.Cancel();
        _replayCts = new CancellationTokenSource();
        _replayStatus = $"Replaying {Path.GetFileName(dialog.FileName)}";
        RecordStatus.Text = _replayStatus;
        try
        {
            await Replayer.ReplayToUdpAsync(dialog.FileName, _udpPort, 1, false, _replayCts.Token);
            _replayStatus = "Replay finished";
        }
        catch (OperationCanceledException)
        {
            _replayStatus = "Replay cancelled";
        }
        catch (Exception ex)
        {
            _replayStatus = ex.Message;
        }
        RecordStatus.Text = _replayStatus;
    }

    private void OnFirewall(object sender, RoutedEventArgs e)
    {
        var udp = $"advfirewall firewall add rule name=\"PitWall UDP In\" dir=in action=allow protocol=UDP localport={_udpPort}";
        var tcp = $"advfirewall firewall add rule name=\"PitWall TCP In\" dir=in action=allow protocol=TCP localport={Constants.DefaultWebPort}";
        try
        {
            RunElevated("netsh", udp);
            RunElevated("netsh", tcp);
            MessageBox.Show("Firewall rules added (UDP 20777, TCP 5080).", "PitWall");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not add rules automatically. Run this in an elevated PowerShell:\n\n" +
                $"netsh {udp}\nnetsh {tcp}\n\n{ex.Message}",
                "PitWall");
        }
    }

    private static void RunElevated(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        });
        process?.WaitForExit();
    }

    private static IEnumerable<string> GetPrivateIpv4Addresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                var ip = addr.Address.ToString();
                if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || Is172Private(ip))
                {
                    yield return ip;
                }
            }
        }
    }

    private static bool Is172Private(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length == 4 && parts[0] == "172" && byte.TryParse(parts[1], out var b) && b is >= 16 and <= 31;
    }

    private static string FormatGear(int gear) => gear switch
    {
        -1 => "R",
        0 => "N",
        _ => gear.ToString()
    };

    private static string FormatMs(float ms)
    {
        if (ms <= 0)
        {
            return "—";
        }

        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{ts.Minutes}:{ts.Seconds:00}.{ts.Milliseconds:000}";
    }

    private static string FormatWheels(float[] wheels)
        => wheels is { Length: >= 4 }
            ? $"{wheels[0]:0}/{wheels[1]:0}/{wheels[2]:0}/{wheels[3]:0}%"
            : "—";
}
