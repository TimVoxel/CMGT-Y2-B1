using System;
using System.Collections.Concurrent;

public sealed class RecordingHeader
{
    public const ushort CurrentFormatVersion = 2;

    public RecordingHeader(DateTime startedAtUtc, string sceneName, float poseSampleRate)
    {
        StartedAtUtc = startedAtUtc;
        SceneName = sceneName;
        PoseSampleRate = poseSampleRate;
    }

    public ushort FormatVersion => CurrentFormatVersion;
    public DateTime StartedAtUtc { get; }
    public string SceneName { get; }
    public float PoseSampleRate { get; }
}

// Filled on the main thread, safe to drain from a writer thread.
// The writer is done once IsFinished is true and Events is empty.
public sealed class RecordingSession
{
    private volatile bool _isFinished;

    public RecordingSession(RecordingHeader header)
    {
        Header = header;
    }

    public RecordingHeader Header { get; }
    public ConcurrentQueue<RecordedEvent> Events { get; } = new ConcurrentQueue<RecordedEvent>();
    public bool IsFinished => _isFinished;

    public void Finish()
    {
        _isFinished = true;
    }

}
