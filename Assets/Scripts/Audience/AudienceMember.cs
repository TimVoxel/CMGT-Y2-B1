using UnityEngine;

// Bobs up and down on the beat. Stays still until engagement passes its threshold,
// then bobs harder the more engaged the crowd gets.
public class AudienceMember : MonoBehaviour
{
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    // Engagement needed before this member starts moving
    [SerializeField, Range(0.0f, 1.0f)] private float _engagementThreshold = 0.3f;
    // Bob height at full engagement
    [SerializeField] private float _maxAmplitude = 0.25f;
    // How quickly the bob height follows engagement changes, in seconds
    [SerializeField] private float _amplitudeSmoothTime = 0.4f;
    // Each bob is scaled by a random amount in this range, so no two jumps look identical
    [SerializeField] private Vector2 _perBeatVariation = new Vector2(0.75f, 1.2f);

    [Header("Clapping")]
    // Engagement at which the crowd starts cheering. Set by the spawner from CrowdCheer if it has one.
    [SerializeField, Range(0.0f, 1.0f)] private float _clapCeiling = 0.85f;
    // Where this member's clap threshold sits between its bob threshold (0) and the clap ceiling (1)
    [SerializeField, Range(0.0f, 1.0f)] private float _clapThresholdBlend = 0.5f;
    // Fired by CrowdClap when a clap is heard and this member is clapping, e.g. for a ScalePunch
    [SerializeField] private UnityEngine.Events.UnityEvent _onClap;

    [Header("Moshing")]
    // Jumps are this much bigger while moshing
    [SerializeField] private float _moshJumpMultiplier = 1.8f;
    // How often a mosher changes direction, in seconds
    [SerializeField] private Vector2 _moshTurnInterval = new Vector2(0.3f, 1.0f);
    // How fast members walk back to their spot when the pit ends
    [SerializeField] private float _returnSpeed = 1.0f;
    [SerializeField] private float _turnSpeed = 540.0f;

    [Header("Joining / leaving")]
    // Time to scale in when joining and out when logging off
    [SerializeField] private float _popDuration = 0.35f;

    private Vector3 _baseScale;
    // 0 = invisible, 1 = full size. Moves toward 1 normally, toward 0 when leaving.
    private float _presence = 1.0f;
    private bool _isLeaving;

    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    // Where the member is standing (local, no bob). Equal to _basePosition unless moshing.
    private Vector3 _groundPosition;
    private bool _isMoshing;
    private Vector3 _moshCenter;
    private float _moshRadius;
    private float _moshSpeed;
    private Vector3 _moshVelocity;
    private float _nextMoshTurnTime;
    private float _amplitude;
    private float _amplitudeVelocity;
    private long _currentBeatIndex = long.MinValue;
    private float _beatVariation = 1.0f;

    public float EngagementThreshold => _engagementThreshold;
    public float ClapThreshold => Mathf.Lerp(_engagementThreshold, _clapCeiling, _clapThresholdBlend);
    public bool IsClapping => _engagement != null && _engagement.Engagement >= ClapThreshold;
    public bool IsMoshing => _isMoshing;
    public Vector3 BasePosition => _basePosition;
    public Vector3 GroundPosition => _groundPosition;
    public Vector3 MoshVelocity
    {
        get => _moshVelocity;
        set => _moshVelocity = value;
    }

    public void Init(EngagementHolder engagement, TrackPlayer trackPlayer, float engagementThreshold, float maxAmplitude)
    {
        _engagement = engagement;
        _trackPlayer = trackPlayer;
        _engagementThreshold = engagementThreshold;
        _maxAmplitude = maxAmplitude;
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
        _groundPosition = _basePosition;
    }

    public void SetClapCeiling(float clapCeiling)
    {
        _clapCeiling = clapCeiling;
    }

    // Grows in from nothing, for members joining mid-show
    public void PopIn()
    {
        _presence = 0.0f;
        transform.localScale = Vector3.zero;
    }

    // Shrinks away and destroys itself. The spawner has already dropped it from the crowd.
    public void Leave()
    {
        _isLeaving = true;
        _isMoshing = false;
    }

    private void UpdatePresence()
    {
        var target = _isLeaving ? 0.0f : 1.0f;
        if (_presence == target)
        {
            return;
        }

        _presence = Mathf.MoveTowards(_presence, target, Time.deltaTime / Mathf.Max(1e-4f, _popDuration));
        transform.localScale = _baseScale * Mathf.SmoothStep(0.0f, 1.0f, _presence);
        if (_isLeaving && _presence <= 0.0f)
        {
            Destroy(gameObject);
        }
    }

    private void Awake()
    {
        _baseScale = transform.localScale;
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
        _groundPosition = _basePosition;
    }

    // center and radius are in the same local space as the member's position
    public void StartMoshing(Vector3 center, float radius, float speed)
    {
        _isMoshing = true;
        _moshCenter = center;
        _moshRadius = radius;
        _moshSpeed = speed;
        _moshVelocity = RandomFlatDirection() * speed;
        _nextMoshTurnTime = Time.time + Random.Range(_moshTurnInterval.x, _moshTurnInterval.y);
    }

    public void StopMoshing()
    {
        _isMoshing = false;
    }

    // Used by the pit to push moshers apart when they bump
    public void Nudge(Vector3 offset)
    {
        _groundPosition += new Vector3(offset.x, 0.0f, offset.z);
    }

    private static Vector3 RandomFlatDirection()
    {
        var angle = Random.Range(0.0f, 2.0f * Mathf.PI);
        return new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
    }

    private void UpdateGroundPosition()
    {
        if (!_isMoshing)
        {
            _groundPosition = Vector3.MoveTowards(_groundPosition, _basePosition, _returnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _baseRotation, _turnSpeed * Time.deltaTime);
            return;
        }

        if (Time.time >= _nextMoshTurnTime)
        {
            // Swerve rather than pick a totally new heading, so paths look like charging around
            var swerve = Quaternion.Euler(0.0f, Random.Range(-100.0f, 100.0f), 0.0f);
            _moshVelocity = swerve * _moshVelocity.normalized * _moshSpeed;
            _nextMoshTurnTime = Time.time + Random.Range(_moshTurnInterval.x, _moshTurnInterval.y);
        }

        _groundPosition += _moshVelocity * Time.deltaTime;

        // Bounce off the edge of the pit
        var fromCenter = _groundPosition - _moshCenter;
        fromCenter.y = 0.0f;
        if (fromCenter.magnitude > _moshRadius)
        {
            var normal = fromCenter.normalized;
            _groundPosition = new Vector3(_moshCenter.x, _groundPosition.y, _moshCenter.z) + normal * _moshRadius;
            if (Vector3.Dot(_moshVelocity, normal) > 0.0f)
            {
                _moshVelocity = Vector3.Reflect(_moshVelocity, normal);
            }
        }

        if (_moshVelocity.sqrMagnitude > 1e-4f)
        {
            var facing = Quaternion.LookRotation(_moshVelocity);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, facing, _turnSpeed * Time.deltaTime);
        }
    }

    public void NotifyClapped()
    {
        _onClap.Invoke();
    }

    private void Update()
    {
        var targetAmplitude = 0.0f;
        if (_engagement != null && _trackPlayer != null && _trackPlayer.IsPlaying)
        {
            // Ramps from 0 at the threshold, so members ease in rather than popping into motion
            var excitement = Mathf.InverseLerp(_engagementThreshold, 1.0f, _engagement.Engagement);
            targetAmplitude = excitement * _maxAmplitude;
        }
        _amplitude = Mathf.SmoothDamp(_amplitude, targetAmplitude, ref _amplitudeVelocity, _amplitudeSmoothTime);

        var height = 0.0f;
        if (_trackPlayer != null)
        {
            var beat = _trackPlayer.CurrentBeat;

            // Re-roll on each beat, when the bob is at the bottom, so the change never pops
            var beatIndex = (long)System.Math.Floor(beat);
            if (beatIndex != _currentBeatIndex)
            {
                _currentBeatIndex = beatIndex;
                _beatVariation = Random.Range(_perBeatVariation.x, _perBeatVariation.y);
            }

            // |sin| bottoms out exactly on each beat, like landing a jump on the downbeat
            var beatPhase = (float)(beat - beatIndex);
            height = Mathf.Abs(Mathf.Sin(beatPhase * Mathf.PI)) * _amplitude * _beatVariation;
            if (_isMoshing)
            {
                height *= _moshJumpMultiplier;
            }
        }

        UpdatePresence();
        UpdateGroundPosition();
        transform.localPosition = _groundPosition + Vector3.up * height;
    }
}
