using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class CoolPoseDetector : MonoBehaviour
{
    private enum Pose
    {
        None,
        OneHandUpLookingDown,
        BothHandsUp,
        HandsRaisedToTheSides,
    }

    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdCheer _cheer;
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _leftHand;
    [SerializeField] private Transform _rightHand;

    [Header("Reward")]
    [SerializeField] private float _engagementBoost = 0.5f;
    [SerializeField] private float _charismaBoost = 0.5f;
    [SerializeField] private float _holdDuration = 0.4f;
    [SerializeField] private float _cooldown = 20.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _hapticAmplitude = 0.8f;
    [SerializeField] private float _hapticDuration = 0.2f;

    [Header("Charisma while hitting notes")]
    [SerializeField] private float _lookingUpAngle = 30.0f;
    [SerializeField] private float _charismaPerHitLookingUp = 0.2f;
    [SerializeField] private float _charismaPerHitOneHandUp = 0.08f;

    [Header("Hands up")]
    [SerializeField] private float _handUpAboveHead = 0.1f;
    [SerializeField] private float _lookingDownAngle = 20.0f;

    [Header("Hands to the sides")]
    [SerializeField] private float _minSideDistance = 0.4f;
    [SerializeField] private float _maxForwardDistance = 0.35f;
    [SerializeField] private Vector2 _sideHeightRange = new Vector2(-0.45f, 0.25f);

    private HapticImpulsePlayer _leftHaptics;
    private HapticImpulsePlayer _rightHaptics;
    private Pose _heldPose;
    private float _heldFor;
    private bool _hasRewardedHeldPose;
    private float _nextRewardTime;

    private void Awake()
    {
        _leftHaptics = _leftHand.GetComponentInParent<HapticImpulsePlayer>();
        _rightHaptics = _rightHand.GetComponentInParent<HapticImpulsePlayer>();
    }

    private void OnEnable()
    {
        _trackPlayer.NoteHit += OnNoteHit;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteHit -= OnNoteHit;
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        if (HeadPitchDownward <= -_lookingUpAngle)
        {
            _engagement.AddCharisma(_charismaPerHitLookingUp);
        }
        if (IsHandUp(_leftHand) != IsHandUp(_rightHand))
        {
            _engagement.AddCharisma(_charismaPerHitOneHandUp);
        }
    }

    private float HeadPitchDownward => Mathf.Asin(Mathf.Clamp(-_head.forward.y, -1.0f, 1.0f)) * Mathf.Rad2Deg;

    private bool IsHandUp(Transform hand)
    {
        return hand.position.y >= _head.position.y + _handUpAboveHead;
    }

    private void Update()
    {
        if (!_concertSet.HasStarted)
        {
            return;
        }

        var pose = DetectPose();
        if (pose != _heldPose)
        {
            _heldPose = pose;
            _heldFor = 0.0f;
            _hasRewardedHeldPose = false;
        }

        if (pose == Pose.None)
        {
            return;
        }

        _heldFor += Time.deltaTime;
        if (_heldFor >= _holdDuration && !_hasRewardedHeldPose && Time.time >= _nextRewardTime)
        {
            _hasRewardedHeldPose = true;
            _nextRewardTime = Time.time + _cooldown;
            Reward(pose);
        }
    }

    private Pose DetectPose()
    {
        var isLeftUp = IsHandUp(_leftHand);
        var isRightUp = IsHandUp(_rightHand);

        if (isLeftUp && isRightUp)
        {
            return Pose.BothHandsUp;
        }

        if (isLeftUp || isRightUp)
        {
            return HeadPitchDownward >= _lookingDownAngle ? Pose.OneHandUpLookingDown : Pose.None;
        }

        return IsHandToTheSide(_leftHand, -1.0f) && IsHandToTheSide(_rightHand, 1.0f)
            ? Pose.HandsRaisedToTheSides
            : Pose.None;
    }

    private bool IsHandToTheSide(Transform hand, float sideSign)
    {
        var right = Vector3.ProjectOnPlane(_head.right, Vector3.up).normalized;
        var forward = Vector3.Cross(right, Vector3.up);
        var offset = hand.position - _head.position;

        return Vector3.Dot(offset, right) * sideSign >= _minSideDistance
            && Mathf.Abs(Vector3.Dot(offset, forward)) <= _maxForwardDistance
            && offset.y >= _sideHeightRange.x
            && offset.y <= _sideHeightRange.y;
    }

    private void Reward(Pose pose)
    {
        Debug.Log($"[Pose] {pose}");
        _engagement.Add(_engagementBoost);
        _engagement.AddCharisma(_charismaBoost);
        _cheer.CheerAtFullVolume();
        SendHaptics(_leftHaptics);
        SendHaptics(_rightHaptics);
    }

    private void SendHaptics(HapticImpulsePlayer haptics)
    {
        if (haptics != null)
        {
            haptics.SendHapticImpulse(_hapticAmplitude, _hapticDuration);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _concertSet = FindFirstObjectByType<ConcertSet>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _cheer = FindFirstObjectByType<CrowdCheer>();
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
    }
#endif
}
