using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class LaneHitZone : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private int _laneIndex;
    [SerializeField] private Collider _collider;
    [SerializeField] private ParticleSystemRenderer[] _laneColoredParticles;

    [SerializeField] private float _minHitSpeed = 0.0f;
    [SerializeField] private float _retriggerDelay = 0.1f;
    [SerializeField] private float _perfectHitAccuracyThreshold = 0.9f;

    [SerializeField] private UnityEvent _onPressed;
    [SerializeField] private UnityEvent _onNoteHit;
    [SerializeField] private UnityEvent _onPerfectHit;
    [SerializeField] private UnityEvent _onEmptyPress;
    [SerializeField] private UnityEvent _onMissed;

    private float _lastPressTime = float.NegativeInfinity;

    public int LaneIndex => _laneIndex;

    public void Init(TrackPlayer trackPlayer, int laneIndex, Material laneMaterial)
    {
        _trackPlayer = trackPlayer;
        _laneIndex = laneIndex;
        foreach (var particles in _laneColoredParticles)
        {
            particles.sharedMaterial = laneMaterial;
        }
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
    }

    private void OnTriggerEnter(Collider other)
    {
        var hitter = other.GetComponentInParent<LaneHitter>();
        if (hitter == null)
        {
            return;
        }

        if (Time.time - _lastPressTime < _retriggerDelay)
        {
            return;
        }

        if (hitter.Velocity.magnitude < _minHitSpeed)
        {
            return;
        }

        _lastPressTime = Time.time;

        if (_trackPlayer == null)
        {
            _onPressed?.Invoke();
            return;
        }
        var hit = _trackPlayer.TryHit(_laneIndex, out var accuracy);
        ShowPress(hit, accuracy);
        hitter.PlayHaptics(hit);
    }

    public void ShowPress(bool hitNote, float accuracy)
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
