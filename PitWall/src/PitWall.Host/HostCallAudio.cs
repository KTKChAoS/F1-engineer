using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PitWall.Host;

internal static class HostCallAudio
{
    public static async Task PlayAlertAsync(string? outputDeviceId)
    {
        MMDevice? outputDevice = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!string.IsNullOrWhiteSpace(outputDeviceId))
                outputDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(device => device.ID == outputDeviceId);

            var playerBuilder = new WasapiPlayerBuilder();
            if (outputDevice is not null)
                playerBuilder.WithDevice(outputDevice);
            await using var player = playerBuilder.Build();
            using var audioData = new MemoryStream(CreateAlertTone());
            using var audioSource = new RawSourceWaveStream(audioData, new WaveFormat(44_100, 16, 1));
            player.Init(audioSource);
            player.Play();
            await Task.Delay(480).ConfigureAwait(false);
            player.Stop();
        }
        catch { /* Audio output may be unavailable or disconnected. */ }
        finally { outputDevice?.Dispose(); }
    }

    public static async Task SpeakAsync(string text, string? outputDeviceId)
    {
        var speechPath = Path.Combine(ProjectPaths.LogsDirectory, $"call-{Guid.NewGuid():N}.wav");
        try
        {
            Directory.CreateDirectory(ProjectPaths.LogsDirectory);
            if (!SynthesizeToFile(text, speechPath))
                return;
            using var reader = new AudioFileReader(speechPath);
            using var enumerator = new MMDeviceEnumerator();
            if (!string.IsNullOrWhiteSpace(outputDeviceId))
            {
                using var devices = new DisposableDeviceCollection(enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active));
                var selected = devices.Items.FirstOrDefault(device => device.ID == outputDeviceId);
                if (selected is not null)
                {
                    await using var selectedPlayer = new WasapiPlayerBuilder().WithDevice(selected).Build();
                    await PlayToEndAsync(selectedPlayer, reader).ConfigureAwait(false);
                    return;
                }
            }

            await using var defaultPlayer = new WasapiPlayerBuilder().Build();
            await PlayToEndAsync(defaultPlayer, reader).ConfigureAwait(false);
        }
        catch { /* SAPI or audio output may be unavailable. */ }
        finally
        {
            try { File.Delete(speechPath); }
            catch { /* A temporary spoken-call file can be removed on a later run. */ }
        }
    }

    private static bool SynthesizeToFile(string text, string path)
    {
        object? voice = null;
        object? fileStream = null;
        try
        {
            var speechType = Type.GetTypeFromProgID("SAPI.SpVoice", throwOnError: false);
            var fileStreamType = Type.GetTypeFromProgID("SAPI.SpFileStream", throwOnError: false);
            if (speechType is null || fileStreamType is null)
                return false;
            voice = Activator.CreateInstance(speechType);
            fileStream = Activator.CreateInstance(fileStreamType);
            if (voice is null || fileStream is null)
                return false;
            fileStreamType.InvokeMember("Open", BindingFlags.InvokeMethod, null, fileStream, [path, 3, false]);
            speechType.InvokeMember("AudioOutputStream", BindingFlags.SetProperty, null, voice, [fileStream]);
            speechType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, [text, 0]);
            fileStreamType.InvokeMember("Close", BindingFlags.InvokeMethod, null, fileStream, null);
            return File.Exists(path) && new FileInfo(path).Length > 44;
        }
        catch { return false; }
        finally
        {
            if (fileStream is not null && Marshal.IsComObject(fileStream)) Marshal.ReleaseComObject(fileStream);
            if (voice is not null && Marshal.IsComObject(voice)) Marshal.ReleaseComObject(voice);
        }
    }

    private static async Task PlayToEndAsync(WasapiPlayer player, AudioFileReader reader)
    {
        player.Init(reader);
        player.Play();
        var timeout = DateTimeOffset.UtcNow.AddSeconds(30);
        while (player.PlaybackState == PlaybackState.Playing && DateTimeOffset.UtcNow < timeout)
            await Task.Delay(50).ConfigureAwait(false);
        player.Stop();
    }

    private sealed class DisposableDeviceCollection(IEnumerable<MMDevice> items) : IDisposable
    {
        public IEnumerable<MMDevice> Items { get; } = items.ToArray();
        public void Dispose()
        {
            foreach (var item in Items)
                item.Dispose();
        }
    }

    private static byte[] CreateAlertTone()
    {
        const int sampleRate = 44_100;
        const int toneSamples = sampleRate * 17 / 100;
        const int gapSamples = sampleRate / 30;
        var samples = toneSamples * 2 + gapSamples;
        var bytes = new byte[samples * sizeof(short)];
        for (var i = 0; i < toneSamples; i++)
        {
            WriteSample(bytes, i, 880, i);
            WriteSample(bytes, toneSamples + gapSamples + i, 660, i);
        }
        return bytes;

        static void WriteSample(byte[] target, int sampleIndex, int frequency, int toneIndex)
        {
            var envelope = Math.Min(1f, Math.Min(toneIndex / 300f, (toneSamples - toneIndex) / 300f));
            var value = (short)(Math.Sin(2 * Math.PI * frequency * toneIndex / sampleRate) * 7000 * Math.Max(0, envelope));
            var offset = sampleIndex * sizeof(short);
            target[offset] = (byte)(value & 0xff);
            target[offset + 1] = (byte)((value >> 8) & 0xff);
        }
    }
}
