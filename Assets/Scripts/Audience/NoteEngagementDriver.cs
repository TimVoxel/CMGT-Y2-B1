using UnityEngine;

// Turns note hits and misses into engagement. Doesn't reset between songs (reset manually in ConcertSet)
public class NoteEngagementDriver : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private EngagementHolder _engagement;

    [SerializeField] private float _perfectHitGain = 0.03f;
    [SerializeField] private float _sloppyHitGain = 0.01f;
    [SerializeField] private float _missPenalty = 0.04f;
    [SerializeField] private float _referenceBpm = 120.0f;
    [SerializeField] private float _referenceNotesPerSecond = 1.5f;
    [SerializeField, Range(0.0f, 1.0f)] private float _tempoInfluence = 0.5f;
    [SerializeField, Range(0.0f, 1.0f)] private float _densityInfluence = 0.5f;

    private void OnEnable()
    {
        _trackPlayer.NoteHit += OnNoteHit;
        _trackPlayer.NoteMissed += OnNoteMissed;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteHit -= OnNoteHit;
        _trackPlayer.NoteMissed -= OnNoteMissed;
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        _engagement.Add(Mathf.Lerp(_sloppyHitGain, _perfectHitGain, accuracy) * TrackScale);
    }

    private void OnNoteMissed(TrackNote note)
    {
        _engagement.Add(-_missPenalty * TrackScale);
    }

    // Per-note multiplier. The engagement a song builds per second goes as
    // (bpm / ref)^tempoInfluence * (density / ref)^densityInfluence, spread over its notes.
    private float TrackScale
    {
        get
        {
            var track = _trackPlayer.Track;
            if (track == null || track.Bpm <= 0.0f || track.Duration <= 0.0f || track.NoteCount == 0)
            {
                return 1.0f;
            }
            var tempo = track.Bpm / _referenceBpm;
            var density = track.NoteCount / track.Duration / _referenceNotesPerSecond;
            return Mathf.Pow(tempo, _tempoInfluence) * Mathf.Pow(density, _densityInfluence - 1.0f);
        }
    }
}
