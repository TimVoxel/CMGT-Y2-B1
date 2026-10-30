using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class AudiencePointing : MonoBehaviour
{
    private class PointingHand
    {
        public Transform Transform;
        public HapticImpulsePlayer Haptics;
        public AudienceMember Target;
        public float AimedFor;
        public bool HasReacted;
    }

    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdEmojis _emojis;
    [SerializeField] private Transform _leftHand;
    [SerializeField] private Transform _rightHand;

    [SerializeField, Range(0.0f, 45.0f)] private float _maxAimAngle = 10.0f;
    [SerializeField] private float _aimDuration = 0.2f;
    [SerializeField] private float _excitedDuration = 2.0f;

    [SerializeField] private bool _addsEngagement = true;
    [SerializeField] private float _engagementPerReaction = 0.01f;

    [SerializeField, Range(0.0f, 1.0f)] private float _hapticAmplitude = 0.5f;
    [SerializeField] private float _hapticDuration = 0.08f;

    [SerializeField] private AudioSourcePool _reactionSounds;
    [SerializeField] private AudioClip[] _reactionClips;
    [SerializeField] private Vector2 _reactionVolumeRange = new Vector2(0.2f, 0.4f);
    [SerializeField] private Vector2 _reactionPitchRange = new Vector2(0.95f, 1.05f);

    private PointingHand[] _hands;
    private int _lastReactionClipIndex;

    private void Awake()
    {
        _hands = new[] { CreateHand(_leftHand), CreateHand(_rightHand) };
        _lastReactionClipIndex = Random.Range(0, _reactionClips.Length);
    }

    private static PointingHand CreateHand(Transform hand)
    {
        return new PointingHand { Transform = hand, Haptics = hand.GetComponentInParent<HapticImpulsePlayer>() };
    }

    private void Update()
    {
        if (_concertSet != null && !_concertSet.HasStarted)
        {
            return;
        }

        foreach (var hand in _hands)
        {
            UpdateHand(hand);
        }
    }

    private void UpdateHand(PointingHand hand)
    {
        var target = FindAimedMember(hand.Transform);
        if (target != hand.Target)
        {
            hand.Target = target;
            hand.AimedFor = 0.0f;
            hand.HasReacted = false;
        }

        if (target == null)
        {
            return;
        }

        hand.AimedFor += Time.deltaTime;
        if (hand.AimedFor < _aimDuration)
        {
            return;
        }

        if (!hand.HasReacted)
        {
            hand.HasReacted = true;
            React(hand, target);
        }
        target.Excite(_excitedDuration);
    }

    private AudienceMember FindAimedMember(Transform hand)
    {
        AudienceMember closest = null;
        var closestDistanceToAimLine = float.PositiveInfinity;
        foreach (var member in _audience.Members)
        {
            var toMember = member.Center - hand.position;
            if (Vector3.Angle(hand.forward, toMember) > _maxAimAngle)
            {
                continue;
            }

            var distanceToAimLine = Vector3.Cross(hand.forward, toMember).magnitude;
            if (distanceToAimLine < closestDistanceToAimLine)
            {
                closest = member;
                closestDistanceToAimLine = distanceToAimLine;
            }
        }
        return closest;
    }

    private void React(PointingHand hand, AudienceMember member)
    {
        if (hand.Haptics != null)
        {
            hand.Haptics.SendHapticImpulse(_hapticAmplitude, _hapticDuration);
        }

        if (member.IsExcited)
        {
            return;
        }

        _emojis.SendExcitedBurst(member);
        _reactionSounds.PlayAt(
            PickReactionClip(),
            member.Center,
            Random.Range(_reactionVolumeRange.x, _reactionVolumeRange.y),
            Random.Range(_reactionPitchRange.x, _reactionPitchRange.y));
        if (_addsEngagement)
        {
            _engagement.Add(_engagementPerReaction);
        }
    }

    private AudioClip PickReactionClip()
    {
        var index = Random.Range(0, _reactionClips.Length - 1);
        if (index >= _lastReactionClipIndex)
        {
            index++;
        }
        _lastReactionClipIndex = index % _reactionClips.Length;
        return _reactionClips[_lastReactionClipIndex];
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _concertSet = FindFirstObjectByType<ConcertSet>();
        _audience =FindFirstObjectByType<AudienceSpawner>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _emojis = FindFirstObjectByType<CrowdEmojis>();
    }
#endif
}
