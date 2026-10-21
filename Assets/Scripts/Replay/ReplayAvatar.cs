using System;
using UnityEngine;

// Moves stand-ins for the recorded player's head and hands, interpolating between recorded poses
public class ReplayAvatar : MonoBehaviour
{
    [SerializeField] private ReplayEventDispatcher _dispatcher;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _leftHand;
    [SerializeField] private Transform _rightHand;

    private readonly PoseEvent[] _fromPoses = new PoseEvent[3];
    private readonly PoseEvent[] _toPoses = new PoseEvent[3];

    private void OnEnable()
    {
        _dispatcher.Subscribe<PoseEvent>(OnPose);
        _dispatcher.Ticked += ApplyPoses;
    }

    private void OnDisable()
    {
        _dispatcher.Unsubscribe<PoseEvent>(OnPose);
        _dispatcher.Ticked -= ApplyPoses;
    }

    private void OnPose(PoseEvent pose)
    {
        var index = (int)pose.Device;
        _fromPoses[index] = pose;
        _dispatcher.TryFindNext<PoseEvent>(next => next.Device == pose.Device, out var nextPose);
        _toPoses[index] = nextPose;
    }

    private void ApplyPoses()
    {
        ApplyPose(TrackedDevice.Head, _head);
        ApplyPose(TrackedDevice.LeftHand, _leftHand);
        ApplyPose(TrackedDevice.RightHand, _rightHand);
    }

    private void ApplyPose(TrackedDevice device, Transform target)
    {
        var from = _fromPoses[(int)device];
        if (target == null || from == null)
        {
            return;
        }

        var to = _toPoses[(int)device];
        if (to == null)
        {
            target.SetPositionAndRotation(from.Pose.Position, from.Pose.Rotation);
            return;
        }

        var samplePeriod = 1.0 / Math.Max(1.0f, _dispatcher.Recording.Header.PoseSampleRate);
        var moveStart = Math.Max(from.Time, to.Time - samplePeriod);
        var t = Mathf.Clamp01((float)((_dispatcher.CurrentTime - moveStart) / Math.Max(1e-6, to.Time - moveStart)));
        target.SetPositionAndRotation(
            Vector3.Lerp(from.Pose.Position, to.Pose.Position, t),
            Quaternion.Slerp(from.Pose.Rotation, to.Pose.Rotation, t));
    }
}
