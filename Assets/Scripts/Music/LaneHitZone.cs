using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// A bar that registers strikes from LaneHitters. Rather than waiting for a hitter to enter a collider (which misses
// small taps once the hand rests inside), it watches each hitter's height above the bar's top surface:
// - a strike is crossing down through the surface while over the bar, moving fast enough
// - the bar re-arms once the hitter comes back up a little above the surface
// - optionally, a downward stroke that bounces back up near the surface also counts, for rapid taps that never
//   come all the way back up
// Sideways the footprint is generous, since there's no physical bar to aim against; vertically it's precise.
[RequireComponent(typeof(Collider))]
public class LaneHitZone : MonoBehaviour
{
    private class HitterState
    {
        public bool HasPrevious;
        public Vector3 PreviousPosition;
        public float PreviousHeight;
        public bool IsArmed;
        public bool IsDescending;
        public float DescentStartHeight;
        public bool IsFinishingStroke;
        public float PeakDownwardSpeed;
    }

    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private int _laneIndex;
    [SerializeField] private Collider _collider;
    [SerializeField] private ParticleSystemRenderer[] _laneColoredParticles;

    [SerializeField] private float _minHitSpeed = 0.0f;
    [SerializeField] private float _retriggerDelay = 0.1f;
    [SerializeField] private float _perfectHitAccuracyThreshold = 0.9f;

    [Header("Strike detection (meters)")]
    [SerializeField] private float _footprintPadding = 0.03f;
    [SerializeField] private float _rearmHeight = 0.015f;
    [SerializeField] private bool _strikeOnBounce = true;
    [SerializeField] private float _minBounceTravel = 0.01f;
    [SerializeField] private float _upswingSpeed = 0.05f;
    [SerializeField] private float _maxStrikeDepth = 0.12f;
    [SerializeField] private float _minPredictedStrikeSpeed = 0.3f;

    [SerializeField] private UnityEvent _onPressed;
    [SerializeField] private UnityEvent _onNoteHit;
    [SerializeField] private UnityEvent _onPerfectHit;
    [SerializeField] private UnityEvent _onEmptyPress;
    [SerializeField] private UnityEvent _onMissed;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly Dictionary<LaneHitter, HitterState> _hitterStates = new Dictionary<LaneHitter, HitterState>();
    private float _lastPressTime = float.NegativeInfinity;
    private Bounds _barBounds;
    private bool _hasBarBounds;
    private int _upAxis;
    private float _upSign;
    private readonly List<Transform> _hitParticles = new List<Transform>();
    private readonly List<Vector3> _hitParticleRestPositions = new List<Vector3>();

    public int LaneIndex => _laneIndex;

    public event System.Action<LaneHitZone> Struck;

    public void Init(TrackPlayer trackPlayer, int laneIndex, Material laneMaterial)
    {
        _trackPlayer = trackPlayer;
        _laneIndex = laneIndex;
        foreach (var particles in _laneColoredParticles)
        {
            ApplyLaneColor(particles, laneMaterial.color);
        }
    }

    private static void ApplyLaneColor(ParticleSystemRenderer particles, Color laneColor)
    {
        laneColor.a = 1.0f;
        var main = particles.GetComponent<ParticleSystem>().main;
        main.startColor = laneColor;

        var material = particles.material;
        if (material.IsKeywordEnabled("_EMISSION"))
        {
            material.SetColor(EmissionColorId, laneColor);
        }
    }

    public bool IsStrikeExpectedWithin(float minSeconds, float maxSeconds)
    {
        if (!_hasBarBounds)
        {
            return false;
        }
        foreach (var hitter in LaneHitter.Active)
        {
            var height = Measure(hitter.transform.position, out var isOverBar);
            var downwardSpeed = -hitter.Velocity.y;
            if (!isOverBar || height <= 0.0f || downwardSpeed < _minPredictedStrikeSpeed)
            {
                continue;
            }
            var secondsToImpact = height / downwardSpeed;
            if (secondsToImpact >= minSeconds && secondsToImpact <= maxSeconds)
            {
                return true;
            }
        }
        return false;
    }

    public void NotifyMissed()
    {
        _onMissed?.Invoke();
    }

    private void Reset()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
    }

    private void Awake()
    {
        _collider.isTrigger = true;

        if (TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
        {
            _barBounds = meshFilter.sharedMesh.bounds;
            _hasBarBounds = true;
        }
        else if (_collider is BoxCollider box)
        {
            _barBounds = new Bounds(box.center, box.size);
            _hasBarBounds = true;
        }

        var localUp = transform.InverseTransformDirection(Vector3.up);
        _upAxis = 0;
        for (var axis = 1; axis < 3; axis++)
        {
            if (Mathf.Abs(localUp[axis]) > Mathf.Abs(localUp[_upAxis]))
            {
                _upAxis = axis;
            }
        }
        _upSign = Mathf.Sign(localUp[_upAxis]);

        CollectHitParticles();
    }

    private void CollectHitParticles()
    {
        foreach (var hitEvent in new[] { _onPressed, _onNoteHit, _onPerfectHit, _onEmptyPress })
        {
            for (var i = 0; i < hitEvent.GetPersistentEventCount(); i++)
            {
                if (hitEvent.GetPersistentTarget(i) is ParticleSystem particles && !_hitParticles.Contains(particles.transform))
                {
                    _hitParticles.Add(particles.transform);
                    _hitParticleRestPositions.Add(particles.transform.localPosition);
                }
            }
        }
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        if (!_hasBarBounds || deltaTime <= 0.0f)
        {
            return;
        }

        foreach (var hitter in LaneHitter.Active)
        {
            if (!_hitterStates.TryGetValue(hitter, out var state))
            {
                state = new HitterState();
                _hitterStates[hitter] = state;
            }
            Track(hitter, state, deltaTime);
        }
    }

    private void Track(LaneHitter hitter, HitterState state, float deltaTime)
    {
        var position = hitter.transform.position;
        var height = Measure(position, out var isOverBar);
        if (!state.HasPrevious)
        {
            state.HasPrevious = true;
            state.PreviousPosition = position;
            state.PreviousHeight = height;
            state.IsArmed = height > _rearmHeight;
            return;
        }

        var downwardSpeed = (state.PreviousHeight - height) / deltaTime;
        if (height > _rearmHeight)
        {
            state.IsArmed = true;
        }

        var struck = false;
        var strikePoint = position;
        var strikeSpeed = downwardSpeed;
        if (state.IsArmed && state.PreviousHeight > 0.0f && height <= 0.0f && downwardSpeed >= _minHitSpeed)
        {
            var crossing = state.PreviousHeight / (state.PreviousHeight - height);
            strikePoint = Vector3.Lerp(state.PreviousPosition, position, crossing);
            Measure(strikePoint, out var crossedOverBar);
            struck = crossedOverBar;
        }

        var isSwingingUp = -downwardSpeed >= _upswingSpeed;
        if (isSwingingUp)
        {
            state.IsFinishingStroke = false;
        }

        if (downwardSpeed >= _minHitSpeed && downwardSpeed > 0.0f)
        {
            if (!state.IsDescending && !state.IsFinishingStroke)
            {
                state.IsDescending = true;
                state.DescentStartHeight = state.PreviousHeight;
                state.PeakDownwardSpeed = 0.0f;
            }
            state.PeakDownwardSpeed = Mathf.Max(state.PeakDownwardSpeed, downwardSpeed);
        }
        else if (isSwingingUp && state.IsDescending)
        {
            state.IsDescending = false;
            var bounced = _strikeOnBounce
                && !struck
                && !state.IsArmed
                && isOverBar
                && height <= _rearmHeight
                && state.DescentStartHeight - height >= _minBounceTravel;
            if (bounced)
            {
                struck = true;
                strikeSpeed = state.PeakDownwardSpeed;
            }
        }

        state.PreviousPosition = position;
        state.PreviousHeight = height;

        if (struck)
        {
            state.IsArmed = false;
            state.IsDescending = false;
            state.IsFinishingStroke = true;
            hitter.NotifyStruck(state.DescentStartHeight);
            Strike(hitter, ProjectOntoSurface(strikePoint), strikeSpeed, state.DescentStartHeight);
        }
    }

    private Vector3 ProjectOntoSurface(Vector3 worldPoint)
    {
        var local = transform.InverseTransformPoint(worldPoint);
        local[_upAxis] = _barBounds.center[_upAxis] + _upSign * _barBounds.extents[_upAxis];
        return transform.TransformPoint(local);
    }

    private float Measure(Vector3 worldPoint, out bool isOverBar)
    {
        var scale = transform.lossyScale;
        var local = transform.InverseTransformPoint(worldPoint);

        var top = _barBounds.center[_upAxis] + _upSign * _barBounds.extents[_upAxis];
        var height = (local[_upAxis] - top) * _upSign * Mathf.Abs(scale[_upAxis]);

        isOverBar = height >= -_maxStrikeDepth;
        for (var axis = 0; axis < 3 && isOverBar; axis++)
        {
            if (axis == _upAxis)
            {
                continue;
            }
            var outside = (Mathf.Abs(local[axis] - _barBounds.center[axis]) - _barBounds.extents[axis]) * Mathf.Abs(scale[axis]);
            isOverBar = outside <= _footprintPadding;
        }
        return height;
    }

    private void Strike(LaneHitter hitter, Vector3 point, float speed, float windUpHeight)
    {
        if (Time.time - _lastPressTime < _retriggerDelay)
        {
            return;
        }
        _lastPressTime = Time.time;
        Struck?.Invoke(this);

        MoveHitParticlesTo(point);
        if (_trackPlayer == null)
        {
            _onPressed?.Invoke();
            hitter.PlayHaptics(0.0f, speed);
            return;
        }
        var hit = _trackPlayer.TryHit(_laneIndex, windUpHeight, out var accuracy);
        InvokePressEvents(hit, accuracy);
        hitter.PlayHaptics(hit ? accuracy : 0.0f, speed);
    }

    private void MoveHitParticlesTo(Vector3 point)
    {
        foreach (var particles in _hitParticles)
        {
            particles.position = point;
        }
    }

    public void ShowPress(bool hitNote, float accuracy)
    {
        for (var i = 0; i < _hitParticles.Count; i++)
        {
            _hitParticles[i].localPosition = _hitParticleRestPositions[i];
        }
        InvokePressEvents(hitNote, accuracy);
    }

    private void InvokePressEvents(bool hitNote, float accuracy)
    {
        _onPressed?.Invoke();
        if (hitNote)
        {
            if (accuracy >= _perfectHitAccuracyThreshold)
            {
                _onPerfectHit?.Invoke();
            }
            _onNoteHit?.Invoke();
        }
        else
        {
            _onEmptyPress?.Invoke();
        }
    }
}
