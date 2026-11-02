using UnityEngine;

public class LaneOffsetCalibrator : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private PlayerHeightProvider _heightProvider;
    [SerializeField] private RecalibrateHeightInput _recalibrateInput;

    [SerializeField, Range(0.0f, 1.0f)] private float _heightFraction = 0.5f;
    [SerializeField] private float _referenceHeight = 1.75f;
    [SerializeField] private float _armSpanToHeight = 1.0f;
    [SerializeField] private float _minEyeHeight = 0.8f;
    [SerializeField] private float _settleTolerance = 0.03f;
    [SerializeField] private float _settleTime = 0.5f;
    [SerializeField] private float _hitLineDistanceInFrontOfHead = 0.296f;

    private bool _pending = true;
    private float _settleStartHeight;
    private float _settledFor;
    private bool _hasLoggedLowEyes;

    private void OnEnable()
    {
        _trackPlayer.Started += OnTrackStarted;
        if (_recalibrateInput != null)
        {
            _recalibrateInput.RecalibrateRequested += Calibrate;
        }
    }

    private void OnDisable()
    {
        _trackPlayer.Started -= OnTrackStarted;
        if (_recalibrateInput != null)
        {
            _recalibrateInput.RecalibrateRequested -= Calibrate;
        }
    }

    private void OnTrackStarted()
    {
        Calibrate();
    }

    public void Calibrate()
    {
        _pending = true;
        _hasLoggedLowEyes = false;
        _settledFor = 0.0f;
        _settleStartHeight = float.NegativeInfinity;
    }

    private void Update()
    {
        if (!_pending)
        {
            return;
        }

        var eyeHeight = _heightProvider.EyeHeight;
        if (eyeHeight < _minEyeHeight && !_hasLoggedLowEyes)
        {
            _hasLoggedLowEyes = true;
            Debug.Log($"[Height] Waiting: eye height {eyeHeight:0.00} m is below the {_minEyeHeight:0.00} m minimum");
        }
        if (eyeHeight < _minEyeHeight || Mathf.Abs(eyeHeight - _settleStartHeight) > _settleTolerance)
        {
            _settleStartHeight = eyeHeight;
            _settledFor = 0.0f;
            return;
        }

        _settledFor += Time.unscaledDeltaTime;
        if (_settledFor < _settleTime)
        {
            return;
        }

        _pending = false;
        var hitLineHeight = _heightProvider.HeadTop * _heightFraction;
        _trackPlayer.SetHitLineHeight(_heightProvider.FloorY + hitLineHeight);
        _trackPlayer.HorizontalHeightFactor = GetExtraLaneSpacing(_heightProvider.HeadTop);
        var headDepth = Vector3.Dot(_heightProvider.Head.position, _trackPlayer.HorizontalTravelDirection);
        _trackPlayer.SetHitLineDepth(headDepth - _hitLineDistanceInFrontOfHead);
        Debug.Log($"[Height] Fitted to eye height {eyeHeight:0.00} m: playing surface at {hitLineHeight:0.00} m, " +
                  $"{_hitLineDistanceInFrontOfHead:0.00} m in front of the head");
    }

    private float GetExtraLaneSpacing(float height)
    {
        var extraReachPerSide = (height - _referenceHeight) * _armSpanToHeight * 0.5f;
        var outerLaneSpacings = Mathf.Max(1, _trackPlayer.Track.MaxNoteIndex) * 0.5f;
        return extraReachPerSide / outerLaneSpacings;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
        _heightProvider = FindFirstObjectByType<PlayerHeightProvider>();
        _recalibrateInput = FindFirstObjectByType<RecalibrateHeightInput>();
    }
#endif
}
