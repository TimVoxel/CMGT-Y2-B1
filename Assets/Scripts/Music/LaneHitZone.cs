using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class LaneHitZone : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private int _laneIndex;
    [SerializeField] private Collider _collider;
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private ParticleSystemRenderer _psRenderer;

    [SerializeField] private float _minHitSpeed = 0.0f;
    [SerializeField] private float _retriggerDelay = 0.1f;

    [SerializeField] private UnityEvent _onPressed;
    [SerializeField] private UnityEvent _onNoteHit;
    [SerializeField] private UnityEvent _onEmptyPress;

    private float _lastPressTime = float.NegativeInfinity;

    public int LaneIndex => _laneIndex;

    public void Init(TrackPlayer trackPlayer, int laneIndex, Material material)
    {
        _trackPlayer = trackPlayer;
        _laneIndex = laneIndex;
        _renderer.material = material;
        _psRenderer.material = material;
    }

    private void Reset()
    {
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
        _onPressed.Invoke();

        var hit = _trackPlayer != null && _trackPlayer.TryHit(_laneIndex, out _);
        if (hit)
        {
            _onNoteHit.Invoke();
        }
        else
        {
            _onEmptyPress.Invoke();
        }
        hitter.PlayHaptics(hit);
    }
}
