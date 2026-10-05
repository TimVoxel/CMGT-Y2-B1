using Unity.XR.CoreUtils;
using UnityEngine;

// Keeps the lane hit buttons at a fixed fraction of the player's height above their floor.
// Measures when a song starts (or restarts) rather than every frame, so ducking or leaning doesn't move the lanes.
public class LaneHeightCalibrator : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    // Needs Tracking Origin Mode = Floor, so the origin sits on the player's floor
    [SerializeField] private XROrigin _origin;

    // 0.5 = lane buttons at half the player's height
    [SerializeField, Range(0.0f, 1.0f)] private float _heightFraction = 0.5f;
    // Eyes to top of head, so the fraction is of the full body height
    [SerializeField] private float _eyeToHeadTop = 0.1f;
    // Below this the headset probably isn't tracking yet, so keep waiting
    [SerializeField] private float _minEyeHeight = 0.8f;

    private bool _pending = true;

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
        _pending = true;
    }

    // Can also be hooked to a button to recalibrate mid-song
    public void Calibrate()
    {
        _pending = true;
    }

    private void Update()
    {
        if (!_pending)
        {
            return;
        }

        var floorY = _origin.transform.position.y;
        var eyeHeight = _origin.Camera.transform.position.y - floorY;
        if (eyeHeight < _minEyeHeight)
        {
            return;
        }

        _pending = false;
        _trackPlayer.SetHitLineHeight(floorY + (eyeHeight + _eyeToHeadTop) * _heightFraction);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
        _origin = FindFirstObjectByType<XROrigin>();
    }
#endif
}
