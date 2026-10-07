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
    // Every member runs its own Update and gets checked by CrowdClap on each beat, so keep this modest for Quest
    [SerializeField] private int _maxMembers = 120;

    // Members lost per missed note. Fractions accumulate, e.g. 0.5 = one leaves every second miss.
    [SerializeField] private float _leavesPerMiss = 1.0f;
    // A hit counts as sloppy when it's further off than this fraction of the hit window (0.6 = outer 40%)
    [SerializeField, Range(0.0f, 1.0f)] private float _sloppyHitFraction = 0.6f;
    // Members lost per sloppy hit. Less than a miss, since the note was still played.
    [SerializeField] private float _leavesPerSloppyHit = 0.5f;
    // Never drops below this, so the stage is never totally empty
    [SerializeField] private int _minMembers = 10;

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
            if (_audience.Members.Count >= _maxMembers)
            {
                // Don't bank joins while full, or a burst would pour in the moment anyone leaves
                _pendingJoins = 0.0f;
                break;
            }
            _audience.AddMember(true);
        }
    }

    private void OnNoteMissed(TrackNote note)
    {
        Lose(_leavesPerMiss);
    }

    private void OnNoteHit(TrackNote note, float offset)
    {
        if (Mathf.Abs(offset) > _sloppyHitFraction * _trackPlayer.HitWindow)
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
