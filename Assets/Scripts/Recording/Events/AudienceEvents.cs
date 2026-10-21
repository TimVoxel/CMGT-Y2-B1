using UnityEngine;

public sealed class AudienceMemberJoinedEvent : RecordedEvent
{
    public AudienceMemberJoinedEvent(
        double time,
        int memberId,
        int prefabIndex,
        Vector3 localPosition,
        Quaternion localRotation,
        float engagementThreshold,
        float maxAmplitude,
        bool poppedIn) : base(time)
    {
        MemberId = memberId;
        PrefabIndex = prefabIndex;
        LocalPosition = localPosition;
        LocalRotation = localRotation;
        EngagementThreshold = engagementThreshold;
        MaxAmplitude = maxAmplitude;
        PoppedIn = poppedIn;
    }

    public override RecordedEventType Type => RecordedEventType.AudienceMemberJoined;
    public int MemberId { get; }
    public int PrefabIndex { get; }
    public Vector3 LocalPosition { get; }
    public Quaternion LocalRotation { get; }
    public float EngagementThreshold { get; }
    public float MaxAmplitude { get; }
    public bool PoppedIn { get; }
}

public sealed class AudienceMemberLeftEvent : RecordedEvent
{
    public AudienceMemberLeftEvent(double time, int memberId) : base(time)
    {
        MemberId = memberId;
    }

    public override RecordedEventType Type => RecordedEventType.AudienceMemberLeft;
    public int MemberId { get; }
}

public sealed class EngagementChangedEvent : RecordedEvent
{
    public EngagementChangedEvent(double time, float engagement) : base(time)
    {
        Engagement = engagement;
    }

    public override RecordedEventType Type => RecordedEventType.EngagementChanged;
    public float Engagement { get; }
}

public sealed class CrowdCheeredEvent : RecordedEvent
{
    public CrowdCheeredEvent(double time, int clipIndex, float pitch, float volume) : base(time)
    {
        ClipIndex = clipIndex;
        Pitch = pitch;
        Volume = volume;
    }

    public override RecordedEventType Type => RecordedEventType.CrowdCheered;
    public int ClipIndex { get; }
    public float Pitch { get; }
    public float Volume { get; }
}

public sealed class MoshPitStartedEvent : RecordedEvent
{
    public MoshPitStartedEvent(double time) : base(time)
    {
    }

    public override RecordedEventType Type => RecordedEventType.MoshPitStarted;
}

public sealed class MoshPitEndedEvent : RecordedEvent
{
    public MoshPitEndedEvent(double time) : base(time)
    {
    }

    public override RecordedEventType Type => RecordedEventType.MoshPitEnded;
}
