public sealed class SongStartedEvent : RecordedEvent
{
    public SongStartedEvent(double time, string trackName, double songTime) : base(time)
    {
        TrackName = trackName;
        SongTime = songTime;
    }

    public override RecordedEventType Type => RecordedEventType.SongStarted;
    public string TrackName { get; }
    // Negative during the start delay, positive when recording began mid-song
    public double SongTime { get; }
}

public sealed class SongFinishedEvent : RecordedEvent
{
    public SongFinishedEvent(double time) : base(time)
    {
    }

    public override RecordedEventType Type => RecordedEventType.SongFinished;
}

public sealed class NoteHitEvent : RecordedEvent
{
    public NoteHitEvent(double time, int lane, int noteRecordIndex, float offset, float accuracy) : base(time)
    {
        Lane = lane;
        NoteRecordIndex = noteRecordIndex;
        Offset = offset;
        Accuracy = accuracy;
    }

    public override RecordedEventType Type => RecordedEventType.NoteHit;
    public int Lane { get; }
    public int NoteRecordIndex { get; }
    // Seconds, positive = late
    public float Offset { get; }
    public float Accuracy { get; }
}

public sealed class NoteMissedEvent : RecordedEvent
{
    public NoteMissedEvent(double time, int lane, int noteRecordIndex) : base(time)
    {
        Lane = lane;
        NoteRecordIndex = noteRecordIndex;
    }

    public override RecordedEventType Type => RecordedEventType.NoteMissed;
    public int Lane { get; }
    public int NoteRecordIndex { get; }
}

public sealed class EmptyPressEvent : RecordedEvent
{
    public EmptyPressEvent(double time, int lane) : base(time)
    {
        Lane = lane;
    }

    public override RecordedEventType Type => RecordedEventType.EmptyPress;
    public int Lane { get; }
}

// The lanes are resized and moved to fit the player's height after each song starts
public sealed class LaneLayoutChangedEvent : RecordedEvent
{
    public LaneLayoutChangedEvent(double time, float hitLineHeight, float horizontalHeightFactor) : base(time)
    {
        HitLineHeight = hitLineHeight;
        HorizontalHeightFactor = horizontalHeightFactor;
    }

    public override RecordedEventType Type => RecordedEventType.LaneLayoutChanged;
    public float HitLineHeight { get; }
    public float HorizontalHeightFactor { get; }
}

// Separate from the layout event so recordings made before lanes followed the player's depth still load
public sealed class LaneDepthChangedEvent : RecordedEvent
{
    public LaneDepthChangedEvent(double time, float hitLineDepth) : base(time)
    {
        HitLineDepth = hitLineDepth;
    }

    public override RecordedEventType Type => RecordedEventType.LaneDepthChanged;
    public float HitLineDepth { get; }
}
