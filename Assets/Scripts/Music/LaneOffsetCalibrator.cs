using UnityEngine;

public class LaneOffsetCalibrator : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private PlayerHeightProvider _heightProvider;

    [SerializeField, Range(0.0f, 1.0f)] private float _heightFraction = 0.5f;
    [SerializeField] private float _referenceHeight = 1.75f;
    [SerializeField] private float _armSpanToHeight = 1.0f;
    [SerializeField] private float _minEyeHeight = 0.8f;
    [SerializeField] private float _settleTolerance = 0.03f;
    [SerializeField] private float _settleTime = 0.5f;

    private bool _pending = true;
    private float _settleStartHeight;
    private float _settledFor;

    private void OnEnable()
    {
        _trackPlayer.Started += OnTrackStarted;
    }

    private void OnDisable()
    {
        _trackPlayer.Started -= OnTrackStarted;
    }

    private void OnTrackStarted()
    {
        Calibrate();
    }

    public void Calibrate()
    {
        _pending = true;
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
        _trackPlayer.SetHitLineHeight(_heightProvider.FloorY + _heightProvider.HeadTop * _heightFraction);
        _trackPlayer.HorizontalHeightFactor = GetExtraLaneSpacing(_heightProvider.HeadTop);
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
    }
#endif
}
