using PitWall.Contracts;
using PitWall.Contracts.Enums;

namespace PitWall.Core.Recording;

public sealed class Recorder : IDisposable
{
    private readonly object _gate = new();
    private FileStream? _stream;
    private DateTimeOffset _started;
    private bool _disposed;

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _stream is not null;
            }
        }
    }

    public string? Path { get; private set; }

    public string Start(string directory, string track, SessionType sessionType, DateTimeOffset now)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            StopLocked();
            Directory.CreateDirectory(directory);
            var safeTrack = string.Join("_", track.Split(System.IO.Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeTrack))
            {
                safeTrack = "Unknown";
            }

            var fileName = $"{safeTrack}_{sessionType}_{now:yyyyMMdd_HHmmss}.pwrec";
            Path = System.IO.Path.Combine(directory, fileName);
            _started = now;
            _stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            PwrecFile.WriteHeader(_stream, now);
            return Path;
        }
    }

    public void Write(ReadOnlySpan<byte> datagram, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_stream is null)
            {
                return;
            }

            var ms = (uint)Math.Max(0, (now - _started).TotalMilliseconds);
            PwrecFile.WriteRecord(_stream, ms, datagram);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            StopLocked();
        }
    }

    private void StopLocked()
    {
        _stream?.Flush();
        _stream?.Dispose();
        _stream = null;
    }
}
