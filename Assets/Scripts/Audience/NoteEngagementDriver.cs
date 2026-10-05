using UnityEngine;

// Turns note hits and misses into engagement.
public class NoteEngagementDriver : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private EngagementHolder _engagement;

    // Gain for a perfectly timed hit
    [SerializeField] private float _perfectHitGain = 0.03f;
    // Gain for a hit right at the edge of the hit window
    [SerializeField] private float _sloppyHitGain = 0.01f;
    [SerializeField] private float _missPenalty = 0.04f;

    private void OnEnable()
    {
        _trackPlayer.NoteHit += OnNoteHit;
        _trackPlayer.NoteMissed += OnNoteMissed;
        _trackPlayer.Started += OnTrackStarted;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteHit -= OnNoteHit;
        _trackPlayer.NoteMissed -= OnNoteMissed;
        _trackPlayer.Started -= OnTrackStarted;
    }

    // Every song (and every restart) has to win the crowd over again
    private void OnTrackStarted()
    {
        _engagement.ResetEngagement();
    }

    private void OnNoteHit(TrackNote note, float offset)
    {
        var accuracy = 1.0f - Mathf.Clamp01(Mathf.Abs(offset) / Mathf.Max(1e-6f, _trackPlayer.HitWindow));
        _engagement.Add(Mathf.Lerp(_sloppyHitGain, _perfectHitGain, accuracy));
    }

    private void OnNoteMissed(TrackNote note)
    {
        _engagement.Add(-_missPenalty);
    }
}
