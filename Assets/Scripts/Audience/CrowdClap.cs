using System.Collections.Generic;
using UnityEngine;

// Whole-crowd clapping on the beat from a small, fixed pool of AudioSources.
// Gets louder and thicker (more layered claps) as more members pass their clap threshold.
public class CrowdClap : MonoBehaviour
{
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private TrackPlayer _trackPlayer;

    [SerializeField] private AudioClip[] _clapClips;
    // One AudioSource per layer is created on this object
    [SerializeField, Range(1, 8)] private int _maxLayers = 3;
    // Random timing slop per layer in seconds, so layers smear into a crowd instead of phasing
    [SerializeField] private float _humanize = 0.015f;
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.95f, 1.08f);
    // Volume with one member clapping and with the whole crowd clapping
    [SerializeField] private Vector2 _volumeRange = new Vector2(0.2f, 0.8f);
    // Optional 0..1 spatial blend for the created sources (0 = 2D)
    [SerializeField, Range(0.0f, 1.0f)] private float _spatialBlend = 0.0f;
    [SerializeField] private CrowdAudioArea _audioArea = new CrowdAudioArea();

    // Claps are scheduled on the audio clock this far ahead of the beat
    private const double ScheduleAhead = 0.1;

    private readonly List<AudioSource> _sources = new List<AudioSource>();
    private readonly List<AudienceMember> _pendingClappers = new List<AudienceMember>();
    private long _lastScheduledBeat = long.MinValue;
    private double _pendingDspTime;
    private bool _clapPending;

    private void Awake()
    {
        for (var i = 0; i < _maxLayers; i++)
        {
            var source = _audioArea.CreateSource(transform, $"Clap Layer {i}");
            source.spatialBlend = _spatialBlend;
            _sources.Add(source);
        }
    }

    private void OnEnable()
    {
        if (_trackPlayer != null)
        {
            _trackPlayer.Started += OnTrackStarted;
        }
    }

    private void OnDisable()
    {
        if (_trackPlayer != null)
        {
            _trackPlayer.Started -= OnTrackStarted;
        }
    }

    // Song time jumps back on restart / track switch, so forget beats scheduled for the old playback
    private void OnTrackStarted()
    {
        _lastScheduledBeat = long.MinValue;
        _clapPending = false;
        foreach (var source in _sources)
        {
            source.Stop();
        }
    }

    private void Update()
    {
        if (_clapPending && AudioSettings.dspTime >= _pendingDspTime)
        {
            _clapPending = false;
            foreach (var member in _pendingClappers)
            {
                if (member != null)
                {
                    member.NotifyClapped();
                }
            }
        }

        if (_clapClips == null || _clapClips.Length == 0
            || _audience == null
            || _trackPlayer == null || !_trackPlayer.IsPlaying)
        {
            return;
        }

        var track = _trackPlayer.Track;
        var interval = track.ClapInterval;
        var nextBeat = ((long)System.Math.Floor(_trackPlayer.CurrentBeat / interval) + 1) * interval;
        if (nextBeat <= _lastScheduledBeat)
        {
            return;
        }

        var clapSongTime = track.GetBeatTime(nextBeat);
        if (clapSongTime - _trackPlayer.SongTime > ScheduleAhead)
        {
            return;
        }
        _lastScheduledBeat = nextBeat;

        var members = _audience.Members;
        _pendingClappers.Clear();
        foreach (var member in members)
        {
            if (member.IsClapping)
            {
                _pendingClappers.Add(member);
            }
        }
        if (_pendingClappers.Count == 0 || members.Count == 0)
        {
            return;
        }

        var fraction = (float)_pendingClappers.Count / members.Count;
        var layers = Mathf.Clamp(Mathf.CeilToInt(fraction * _sources.Count), 1, _sources.Count);
        // Spread the total volume over the layers so adding a layer doesn't jump the loudness
        var volumePerLayer = Mathf.Lerp(_volumeRange.x, _volumeRange.y, fraction) / Mathf.Sqrt(layers);

        // Scheduled on the audio clock so it lands on the beat regardless of frame timing
        var beatDspTime = _trackPlayer.SongTimeToDspTime(clapSongTime);
        for (var i = 0; i < layers; i++)
        {
            var source = _sources[i];
            source.clip = _clapClips[Random.Range(0, _clapClips.Length)];
            source.pitch = Random.Range(_pitchRange.x, _pitchRange.y);
            source.volume = volumePerLayer;
            source.PlayScheduled(beatDspTime + Random.Range(-_humanize, _humanize));
        }

        _pendingDspTime = beatDspTime;
        _clapPending = true;
    }
}
