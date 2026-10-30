using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Fits the xylophone model to the lane layout. The top (bars, rails, end caps) stretches along its length so
// the lane bars line up with the lane buttons and rises with the hit line. The scissor legs pivot around their
// crossing point to reach the new height, like a real stand, so the feet stay on the floor.
// Works in the root's local space and expects the root to have a uniform scale with its length along local Z.
public class XylophoneStand : MonoBehaviour
{
    [Serializable]
    private struct Leg
    {
        public Transform Transform;
        [HideInInspector] public Vector3 UpwardAxis;
        [HideInInspector] public float HalfLength;
    }

    private struct PartPose
    {
        public Transform Transform;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public int LengthAxis;
        public ScalePunch Punch;
    }

    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private Transform[] _laneBars;
    [SerializeField] private Leg[] _legs;
    [SerializeField] private Transform[] _feet;
    [SerializeField, Range(1.0f, 89.0f)] private float _minLegAngle = 20.0f;
    [SerializeField, Range(1.0f, 89.0f)] private float _maxLegAngle = 80.0f;
    [SerializeField, Min(0.0f)] private float _legExtensionPerEnd = 0.05f;
    [SerializeField] private bool _alignBarEndsToPlayer = true;
    [SerializeField] private bool _levelBarTops = true;

    private readonly List<PartPose> _topParts = new List<PartPose>();
    private PartPose[] _legPoses;
    private Transform[] _legPivots;
    private PartPose[] _footPoses;
    private int[] _footLegIndices;
    private Vector3[] _footOffsetsFromLegBottom;
    private float _initialHitLineY;
    private float _initialLaneBarsCenter;
    private float _initialLaneBarSpacing;
    private bool _captured;

    private void Awake()
    {
        Capture();
    }

    private void OnEnable()
    {
        _trackPlayer.LayoutChanged += ApplyLayout;
    }

    private void OnDisable()
    {
        _trackPlayer.LayoutChanged -= ApplyLayout;
    }

    private void Start()
    {
        ApplyLayout();
    }

    private void Capture()
    {
        _captured = true;
        _initialHitLineY = _trackPlayer.HitLineCenter.y;

        var laneBarPositions = _laneBars.Select(bar => bar.localPosition.z).OrderBy(z => z).ToArray();
        _initialLaneBarsCenter = laneBarPositions.Average();
        _initialLaneBarSpacing = (laneBarPositions[^1] - laneBarPositions[0]) / Mathf.Max(1, laneBarPositions.Length - 1);

        var legTransforms = _legs.Select(leg => leg.Transform).ToArray();
        _topParts.Clear();
        foreach (Transform child in transform)
        {
            if (!legTransforms.Contains(child) && !_feet.Contains(child))
            {
                _topParts.Add(CapturePose(child));
            }
        }

        _legPoses = legTransforms.Select(CapturePose).ToArray();
        _legPivots = new Transform[_legs.Length];
        for (var i = 0; i < _legs.Length; i++)
        {
            var pivot = new GameObject(legTransforms[i].name + " Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.SetLocalPositionAndRotation(_legPoses[i].Position, Quaternion.LookRotation(_legs[i].UpwardAxis, Vector3.right));
            legTransforms[i].SetParent(pivot, true);
            _legPivots[i] = pivot;
        }
        _footPoses = _feet.Select(CapturePose).ToArray();
        _footLegIndices = new int[_feet.Length];
        _footOffsetsFromLegBottom = new Vector3[_feet.Length];
        for (var i = 0; i < _feet.Length; i++)
        {
            var footPosition = _footPoses[i].Position;
            var legIndex = Enumerable.Range(0, _legs.Length)
                .OrderBy(index => Vector3.Distance(GetInitialLegBottom(index), footPosition))
                .First();
            _footLegIndices[i] = legIndex;
            _footOffsetsFromLegBottom[i] = footPosition - GetInitialLegBottom(legIndex);
        }
    }

    private Vector3 GetInitialLegBottom(int legIndex)
    {
        return _legPoses[legIndex].Position - _legs[legIndex].UpwardAxis * _legs[legIndex].HalfLength;
    }

    private static PartPose CapturePose(Transform part)
    {
        var lengthAxis = Enumerable.Range(0, 3)
            .OrderByDescending(axis => Mathf.Abs((part.localRotation * AxisVector(axis)).z))
            .First();
        return new PartPose
        {
            Transform = part,
            Position = part.localPosition,
            Rotation = part.localRotation,
            Scale = part.localScale,
            LengthAxis = lengthAxis,
            Punch = part.GetComponent<ScalePunch>(),
        };
    }

    private static Vector3 AxisVector(int axis)
    {
        var vector = Vector3.zero;
        vector[axis] = 1.0f;
        return vector;
    }

    private void ApplyLayout()
    {
        if (!_captured)
        {
            Capture();
        }

        var rootScale = transform.lossyScale;
        var lengthStretch = _trackPlayer.LaneSpacing / Mathf.Max(1e-6f, _initialLaneBarSpacing * rootScale.z);
        var center = transform.InverseTransformPoint(_trackPlayer.HitLineCenter).z;
        var rise = (_trackPlayer.HitLineCenter.y - _initialHitLineY) / rootScale.y;

        foreach (var part in _topParts)
        {
            var position = part.Position;
            position.y += rise;
            position.z = center + (position.z - _initialLaneBarsCenter) * lengthStretch;
            var scale = part.Scale;
            scale[part.LengthAxis] *= lengthStretch;
            part.Transform.localPosition = position;
            part.Transform.localScale = scale;
            if (part.Punch != null)
            {
                part.Punch.RestScale = scale;
            }
        }

        var legBottoms = new Vector3[_legs.Length];
        for (var i = 0; i < _legs.Length; i++)
        {
            legBottoms[i] = ApplyLeg(_legs[i], _legPoses[i], _legPivots[i], rise, center - _initialLaneBarsCenter);
        }

        for (var i = 0; i < _feet.Length; i++)
        {
            _feet[i].localPosition = legBottoms[_footLegIndices[i]] + _footOffsetsFromLegBottom[i];
        }

        var towardPlayer = Vector3.ProjectOnPlane(_trackPlayer.NoteTravelDirection, Vector3.up).normalized;
        if (_alignBarEndsToPlayer && towardPlayer != Vector3.zero)
        {
            AlignBars(towardPlayer, null);
        }
        if (_levelBarTops)
        {
            AlignBars(Vector3.up, _trackPlayer.HitLineCenter.y);
        }
    }

    private void AlignBars(Vector3 direction, float? target)
    {
        var reaches = new float[_laneBars.Length];
        var farthest = float.NegativeInfinity;
        for (var i = 0; i < _laneBars.Length; i++)
        {
            reaches[i] = GetFarthestPoint(_laneBars[i], direction);
            farthest = Mathf.Max(farthest, reaches[i]);
        }

        var goal = target ?? farthest;
        for (var i = 0; i < _laneBars.Length; i++)
        {
            if (!float.IsNegativeInfinity(reaches[i]))
            {
                _laneBars[i].position += direction * (goal - reaches[i]);
            }
        }
    }

    private static float GetFarthestPoint(Transform bar, Vector3 direction)
    {
        if (!bar.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
        {
            return float.NegativeInfinity;
        }

        var bounds = meshFilter.sharedMesh.bounds;
        var reach = Vector3.Dot(bar.TransformPoint(bounds.center), direction);
        for (var axis = 0; axis < 3; axis++)
        {
            reach += Mathf.Abs(Vector3.Dot(bar.TransformVector(AxisVector(axis) * bounds.extents[axis]), direction));
        }
        return reach;
    }

    private Vector3 ApplyLeg(Leg leg, PartPose pose, Transform pivot, float rise, float shift)
    {
        var initialAxis = leg.UpwardAxis;
        var floorY = pose.Position.y - initialAxis.y * leg.HalfLength;

        var span = Mathf.Max(1e-4f, 2.0f * leg.HalfLength * initialAxis.y + rise);
        var sinAngle = Mathf.Clamp(span / (2.0f * leg.HalfLength),
            Mathf.Sin(_minLegAngle * Mathf.Deg2Rad), Mathf.Sin(_maxLegAngle * Mathf.Deg2Rad));
        var cosAngle = Mathf.Sqrt(1.0f - sinAngle * sinAngle);
        var axis = new Vector3(initialAxis.x, sinAngle, Mathf.Sign(initialAxis.z) * cosAngle).normalized;
        var halfLength = span / (2.0f * sinAngle);

        var crossing = new Vector3(pose.Position.x, floorY + span * 0.5f, pose.Position.z + shift);

        pivot.SetLocalPositionAndRotation(crossing, Quaternion.LookRotation(axis, Vector3.right));
        pivot.localScale = new Vector3(1.0f, 1.0f, (halfLength + _legExtensionPerEnd) / leg.HalfLength);
        return crossing - axis * halfLength;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            if (_captured)
            {
                ApplyLayout();
            }
            return;
        }
        if (_legs == null)
        {
            return;
        }
        for (var i = 0; i < _legs.Length; i++)
        {
            MeasureLeg(ref _legs[i]);
        }
    }

    private void MeasureLeg(ref Leg leg)
    {
        var mesh = leg.Transform != null && leg.Transform.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        if (mesh == null)
        {
            return;
        }

        var legTransform = leg.Transform;
        var points = mesh.vertices.Select(vertex => transform.InverseTransformPoint(legTransform.TransformPoint(vertex))).ToArray();
        var pivotY = legTransform.localPosition.y;
        var axis = Average(points.Where(point => point.y > pivotY)) - Average(points.Where(point => point.y <= pivotY));
        axis.x = 0.0f;
        leg.HalfLength = axis.magnitude * 0.5f;
        leg.UpwardAxis = axis.normalized;
    }

    private static Vector3 Average(IEnumerable<Vector3> points)
    {
        var list = points.ToList();
        return list.Aggregate(Vector3.zero, (sum, point) => sum + point) / Mathf.Max(1, list.Count);
    }
#endif
}
