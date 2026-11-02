using UnityEngine;

// Bobs up and down on the beat. Stays still until engagement passes its threshold,
// then bobs harder the more engaged the crowd gets.
public class AudienceMember : MonoBehaviour
{
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    [SerializeField, Range(0.0f, 1.0f)] private float _engagementThreshold = 0.3f;
    [SerializeField] private float _maxAmplitude = 0.25f;
    [SerializeField] private float _amplitudeSmoothTime = 0.4f;
    [SerializeField] private Vector2 _perBeatVariation = new Vector2(0.75f, 1.2f);

    [Header("Clapping")]
    [SerializeField, Range(0.0f, 1.0f)] private float _clapCeiling = 0.85f;
    [SerializeField, Range(0.0f, 1.0f)] private float _clapThresholdBlend = 0.5f;
    [SerializeField] private UnityEngine.Events.UnityEvent _onClap;

    [Header("Applause")]
    [SerializeField] private Vector2 _applauseJumpsPerSecond = new Vector2(1.5f, 2.3f);
    [SerializeField, Range(0.0f, 1.0f)] private float _applauseMinExcitement = 0.5f;
    [SerializeField] private float _applauseBlendTime = 0.3f;

    [Header("Excited by being pointed at")]
    [SerializeField] private float _excitedJumpHeight = 0.4f;
    [SerializeField] private float _excitedJumpsPerSecond = 3.0f;
    [SerializeField] private float _excitedBlendTime = 0.15f;

    [Header("Moshing")]
    [SerializeField] private float _moshJumpMultiplier = 1.8f;
    [SerializeField] private Vector2 _moshTurnInterval = new Vector2(0.3f, 1.0f);
    [SerializeField] private float _returnSpeed = 1.0f;
    [SerializeField] private float _turnSpeed = 540.0f;

    [Header("Joining / leaving")]
    [SerializeField] private float _popDuration = 0.35f;

    private Vector3 _baseScale;
    private float _presence = 1.0f;
    private bool _isLeaving;

    private Vector3 _basePosition;
    private Quaternion _baseRotation;
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
    private float _applauseJumpRate;
    private float _applauseJumpPhase;
    private float _applauseBlend;
    private Renderer _renderer;
    private float _excitedTimeLeft;
    private float _excitedJumpPhase;
    private float _excitedBlend;

    public int Id { get; private set; }
    public int PrefabIndex { get; private set; }
    public float EngagementThreshold => _engagementThreshold;
    public float MaxAmplitude => _maxAmplitude;
    public float ClapThreshold => Mathf.Lerp(_engagementThreshold, _clapCeiling, _clapThresholdBlend);
    public bool IsClapping => _engagement != null && _engagement.Engagement >= ClapThreshold;
    public bool IsMoshing => _isMoshing;
    public bool IsSeated => _maxAmplitude <= 0.0f;
    public bool IsExcited => _excitedTimeLeft > 0.0f;
    public Vector3 Center => _renderer != null ? _renderer.bounds.center : transform.position;
    public Vector3 BasePosition => _basePosition;
    public Quaternion BaseRotation => _baseRotation;
    public Vector3 GroundPosition => _groundPosition;
    public Vector3 MoshVelocity
    {
        get => _moshVelocity;
        set => _moshVelocity = value;
    }

    public void Init(int id, int prefabIndex, EngagementHolder engagement, TrackPlayer trackPlayer, float engagementThreshold, float maxAmplitude)
    {
        Id = id;
        PrefabIndex = prefabIndex;
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

    public void PopIn()
    {
        _presence = 0.0f;
        transform.localScale = Vector3.zero;
    }

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
        _applauseJumpRate = Random.Range(_applauseJumpsPerSecond.x, _applauseJumpsPerSecond.y);
        _applauseJumpPhase = Random.value;
        _renderer = GetComponentInChildren<Renderer>();
    }

    public void Excite(float duration)
    {
        if (IsSeated)
        {
            return;
        }

        _excitedTimeLeft = Mathf.Max(_excitedTimeLeft, duration);
    }

    private float UpdateExcitedHeight(float height)
    {
        _excitedTimeLeft = Mathf.Max(0.0f, _excitedTimeLeft - Time.deltaTime);
        _excitedBlend = Mathf.MoveTowards(_excitedBlend, IsExcited ? 1.0f : 0.0f, Time.deltaTime / Mathf.Max(1e-4f, _excitedBlendTime));
        if (_excitedBlend <= 0.0f)
        {
            return height;
        }

        _excitedJumpPhase = Mathf.Repeat(_excitedJumpPhase + Time.deltaTime * _excitedJumpsPerSecond, 1.0f);
        return Mathf.Lerp(height, Mathf.Sin(_excitedJumpPhase * Mathf.PI) * _excitedJumpHeight, _excitedBlend);
    }

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
            var swerve = Quaternion.Euler(0.0f, Random.Range(-100.0f, 100.0f), 0.0f);
            _moshVelocity = swerve * _moshVelocity.normalized * _moshSpeed;
            _nextMoshTurnTime = Time.time + Random.Range(_moshTurnInterval.x, _moshTurnInterval.y);
        }

        _groundPosition += _moshVelocity * Time.deltaTime;

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
        var isApplauding = _trackPlayer != null && _trackPlayer.IsApplauding;
        var targetAmplitude = 0.0f;
        if (_engagement != null && _trackPlayer != null && (_trackPlayer.IsPlaying || isApplauding))
        {
            var excitement = Mathf.InverseLerp(_engagementThreshold, 1.0f, _engagement.Engagement);
            if (isApplauding)
            {
                excitement = Mathf.Max(excitement, _applauseMinExcitement);
            }
            targetAmplitude = excitement * _maxAmplitude;
        }
        _amplitude = Mathf.SmoothDamp(_amplitude, targetAmplitude, ref _amplitudeVelocity, _amplitudeSmoothTime);

        var height = 0.0f;
        if (_trackPlayer != null)
        {
            var beat = _trackPlayer.CurrentBeat;

            var beatIndex = (long)System.Math.Floor(beat);
            if (beatIndex != _currentBeatIndex)
            {
                _currentBeatIndex = beatIndex;
                _beatVariation = Random.Range(_perBeatVariation.x, _perBeatVariation.y);
            }

            var beatPhase = (float)(beat - beatIndex);
            height = Mathf.Abs(Mathf.Sin(beatPhase * Mathf.PI)) * _amplitude * _beatVariation;

            if (isApplauding)
            {
                _applauseJumpPhase = Mathf.Repeat(_applauseJumpPhase + Time.deltaTime * _applauseJumpRate * _trackPlayer.ApplauseSpeed, 1.0f);
            }
            _applauseBlend = Mathf.MoveTowards(_applauseBlend, isApplauding ? 1.0f : 0.0f, Time.deltaTime / Mathf.Max(1e-4f, _applauseBlendTime));
            var applauseHeight = Mathf.Sin(_applauseJumpPhase * Mathf.PI) * _amplitude;
            height = Mathf.Lerp(height, applauseHeight, _applauseBlend);

            if (_isMoshing)
            {
                height *= _moshJumpMultiplier;
            }
        }

        height = UpdateExcitedHeight(height);

        UpdatePresence();
        UpdateGroundPosition();
        transform.localPosition = _groundPosition + Vector3.up * height;
    }
}
