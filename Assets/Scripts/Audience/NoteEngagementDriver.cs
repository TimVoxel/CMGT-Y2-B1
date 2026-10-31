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

    [Header("Wind-up")]
    [SerializeField] private Vector2 _windUpHeightRange = new Vector2(0.15f, 0.6f);
    [SerializeField, Min(1.0f)] private float _maxWindUpGainMultiplier = 1.75f;
    [SerializeField] private float _charismaPerFullWindUp = 0.05f;

    [Header("Both hands after a wind-up")]
    [SerializeField] private float _bothHandsMinWindUpHeight = 0.3f;
    [SerializeField] private float _bothHandsWindow = 0.15f;
    [SerializeField] private float _bothHandsEngagementBonus = 0.1f;

    private float _lastNoteHitTime = float.NegativeInfinity;
    private float _lastBothHandsBonusTime = float.NegativeInfinity;

    private void OnEnable()
    {
        _trackPlayer.NoteHit += OnNoteHit;
        _trackPlayer.NoteMissed += OnNoteMissed;
        LaneHitter.Struck += TryGiveBothHandsBonus;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteHit -= OnNoteHit;
        _trackPlayer.NoteMissed -= OnNoteMissed;
        LaneHitter.Struck -= TryGiveBothHandsBonus;
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        var windUp = Mathf.InverseLerp(_windUpHeightRange.x, _windUpHeightRange.y, _trackPlayer.LastStrikeWindUpHeight);
        var windUpMultiplier = Mathf.Lerp(1.0f, _maxWindUpGainMultiplier, windUp);
        _engagement.Add(Mathf.Lerp(_sloppyHitGain, _perfectHitGain, accuracy) * TrackScale * _engagement.CharismaGainMultiplier * windUpMultiplier);
        _engagement.AddCharisma(windUp * _charismaPerFullWindUp);

        _lastNoteHitTime = Time.time;
        TryGiveBothHandsBonus();
    }

    private void TryGiveBothHandsBonus()
    {
        var now = Time.time;
        if (now - _lastNoteHitTime > _bothHandsWindow
            || now - _lastBothHandsBonusTime <= _bothHandsWindow
            || LaneHitter.Active.Count < 2)
        {
            return;
        }

        foreach (var hitter in LaneHitter.Active)
        {
            if (now - hitter.LastStrikeTime > _bothHandsWindow || hitter.LastStrikeWindUpHeight < _bothHandsMinWindUpHeight)
            {
                return;
            }
        }

        _lastBothHandsBonusTime = now;
        _engagement.Add(_bothHandsEngagementBonus * _engagement.CharismaGainMultiplier);
        Debug.Log("[BothHands] Bonus given");
    }

    private void OnNoteMissed(TrackNote note)
    {
        _engagement.Add(-_missPenalty * TrackScale * _engagement.CharismaPenaltyMultiplier);
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
