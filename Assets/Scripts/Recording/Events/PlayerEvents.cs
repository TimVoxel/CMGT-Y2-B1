using UnityEngine;

public enum TrackedDevice : byte
{
    Head = 0,
    LeftHand = 1,
    RightHand = 2,
}

public readonly struct RecordedPose
{
    public RecordedPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public static RecordedPose FromWorld(Transform transform)
        => new RecordedPose(transform.position, transform.rotation);

    public bool IsCloseTo(RecordedPose other, float maxPositionDelta, float maxAngleDelta)
        => (Position - other.Position).sqrMagnitude <= maxPositionDelta * maxPositionDelta
           && Quaternion.Angle(Rotation, other.Rotation) <= maxAngleDelta;
}

// World space, so locomotion of the XR rig is captured too
public sealed class PoseEvent : RecordedEvent
{
    public PoseEvent(double time, TrackedDevice device, RecordedPose pose) : base(time)
    {
        Device = device;
        Pose = pose;
    }

    public override RecordedEventType Type => RecordedEventType.Pose;
    public TrackedDevice Device { get; }
    public RecordedPose Pose { get; }
}
