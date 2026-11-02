using UnityEngine;

// Grows the crowd while the show is going well and loses people on mistakes.
// The crowd carries over between songs, so a good set builds a bigger audience.
public class AudienceGrowth : MonoBehaviour
{
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    // Engagement (0..1) -> new members per second
    [SerializeField] private AnimationCurve _joinsPerSecond = new AnimationCurve(
        new Keyframe(0.0f, 0.0f),
        new Keyframe(0.3f, 0.0f),
        new Keyframe(0.7f, 0.5f),
        new Keyframe(1.0f, 2.0f));
        
    [SerializeField] private int _maxMembers = 120;
    [SerializeField] private int _minMembers = 10;
    [SerializeField] private float _leavesPerMiss = 1.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _sloppyHitFraction = 0.6f;
    [SerializeField] private float _leavesPerSloppyHit = 0.5f;

    private float _pendingJoins;
    private float _pendingLeaves;

    private void OnEnable()
    {
        _trackPlayer.NoteMissed += OnNoteMissed;
        _trackPlayer.NoteHit += OnNoteHit;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteMissed -= OnNoteMissed;
        _trackPlayer.NoteHit -= OnNoteHit;
    }

    private void Update()
    {
        if (!_trackPlayer.IsPlaying)
        {
            return;
        }

        _pendingJoins += Mathf.Max(0.0f, _joinsPerSecond.Evaluate(_engagement.Engagement)) * Time.deltaTime;
        while (_pendingJoins >= 1.0f)
        {
            _pendingJoins -= 1.0f;
            if (_audience.StandingCount < _maxMembers)
            {
                _audience.AddMember(true);
            }
            else if (_audience.AddSeatedMember() == null)
            {
                _pendingJoins = 0.0f;
                break;
            }
        }
    }

    private void OnNoteMissed(TrackNote note)
    {
        Lose(_leavesPerMiss);
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        if (accuracy < 1.0f - _sloppyHitFraction)
        {
            Lose(_leavesPerSloppyHit);
        }
    }

    private void Lose(float members)
    {
        _pendingLeaves += members;
        while (_pendingLeaves >= 1.0f)
        {
            _pendingLeaves -= 1.0f;
            if (_audience.Members.Count <= _minMembers || !_audience.RemoveLeastEngagedMember())
            {
                _pendingLeaves = 0.0f;
                break;
            }
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _audience = FindFirstObjectByType<AudienceSpawner>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
    }
#endif
}
