// Values are written to disk, never renumber
public enum RecordedEventType : byte
{
    Pose = 1,

    SongStarted = 10,
    SongFinished = 11,
    NoteHit = 12,
    NoteMissed = 13,
    EmptyPress = 14,
    LaneLayoutChanged = 15,

    AudienceMemberJoined = 20,
    AudienceMemberLeft = 21,
    EngagementChanged = 22,
    CrowdCheered = 23,
    MoshPitStarted = 24,
    MoshPitEnded = 25,
}

public abstract class RecordedEvent
{
    protected RecordedEvent(double time)
    {
        Time = time;
    }

    // Seconds since the recording started
    public double Time { get; }
    public abstract RecordedEventType Type { get; }
}
