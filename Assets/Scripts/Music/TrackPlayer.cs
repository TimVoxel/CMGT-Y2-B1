using System;
using System.Collections.Generic;
using UnityEngine;

// Runs before anything reading SongTime / CurrentBeat so they never lag a frame
[DefaultExecutionOrder(-100)]
public class TrackPlayer : MonoBehaviour
{
    [SerializeField] private float _offsetPerNoteIndex;
    [SerializeField] private Transform _spawnContainer;
    [SerializeField] private float _finalPosOffset;

    [SerializeField] private TrackNote _prefab;

    [SerializeField] private Track _track;

    [SerializeField] private float _spawnTimeOffset;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private float _startDelay = 0.1f;

    // How far from the exact hit time (either side, in seconds) a press still counts
    [SerializeField] private float _hitWindow = 0.15f;
    // Optional. If set, one button is spawned at the end of each lane.
    [SerializeField] private LaneHitZone _laneButtonPrefab;

    [SerializeField] private Material[] _noteMaterials;
    [SerializeField] private Material[] _noteButtonMaterials;

    private readonly List<LaneHitZone> _laneButtons = new List<LaneHitZone>();

    // Offset is in seconds: negative = early, positive = late
    public event Action<TrackNote, float> NoteHit;
    public event Action<TrackNote> NoteMissed;
    // Fired whenever playback (re)starts, including after a track switch
    public event Action Started;

    private double _startDspTime;
    private double _lastDspTime;
    private double _songTime;
    private bool _isPlaying;
    private int _processedNoteIndex;

    private List<TrackNote> _notes = new List<TrackNote>();
    public IReadOnlyList<TrackNote> Notes => _notes;
    public double SongTime => _songTime;
    public bool IsPlaying => _isPlaying;
    public float HitWindow => _hitWindow;
    public double CurrentBeat => _track.GetBeat(_songTime);
    public Track Track => _track;

    // World position of the middle of the hit line, where the lane buttons sit
    public Vector3 HitLineCenter => _spawnContainer.TransformPoint(Vector3.forward * _finalPosOffset);

    // For scheduling sounds sample-accurately against the song
    public double SongTimeToDspTime(double songTime) => _startDspTime + songTime;

    private void Awake()
    {
        if (_audioSource == null && !TryGetComponent(out _audioSource))
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;

        SpawnLaneButtons();
        ResetPlayer();
    }

    private void SpawnLaneButtons()
    {
        foreach (var button in _laneButtons)
        {
            Destroy(button.gameObject);
        }
        _laneButtons.Clear();

        if (_laneButtonPrefab == null || _track.NoteCount == 0)
        {
            return;
        }

        for (var i = 0; i <= _track.MaxNoteIndex; i++)
        {
            var button = Instantiate(_laneButtonPrefab, _spawnContainer);
            button.transform.localPosition = GetEndLocalPosition(i);
            button.transform.localRotation = Quaternion.identity;
            button.Init(this, i, _noteButtonMaterials[i % (_track.MaxNoteIndex + 1)]);
            _laneButtons.Add(button);
        }
    }

    // Hits the closest note in the lane that's within the hit window. Returns false if there wasn't one.
    public bool TryHit(int laneIndex, out float offset)
    {
        offset = 0.0f;
        if (!_isPlaying)
        {
            return false;
        }

        TrackNote closest = null;
        foreach (var note in _notes)
        {
            if (note.NoteIndex != laneIndex || Mathf.Abs(note.TimeLeft) > _hitWindow)
            {
                continue;
            }
            if (closest == null || Mathf.Abs(note.TimeLeft) < Mathf.Abs(closest.TimeLeft))
            {
                closest = note;
            }
        }

        if (closest == null)
        {
            return false;
        }

        offset = -closest.TimeLeft;
        _notes.Remove(closest);
        NoteHit?.Invoke(closest, offset);
        closest.Destroy();
        return true;
    }

    private void Start()
    {
        if (_playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        ResetPlayer();

        _startDspTime = AudioSettings.dspTime + _startDelay;
        _lastDspTime = AudioSettings.dspTime;
        _songTime = -_startDelay;

        _audioSource.clip = _track.Clip;
        if (_track.Clip != null)
        {
            _audioSource.PlayScheduled(_startDspTime);
        }
        _isPlaying = true;
        Started?.Invoke();
    }

    // Moves the whole note highway (lane buttons and notes in flight) so the hit line sits at this world height
    public void SetHitLineHeight(float worldY)
    {
        _spawnContainer.position += Vector3.up * (worldY - HitLineCenter.y);
    }

    // Switches to another track and starts it from the beginning
    public void PlayTrack(Track track)
    {
        ResetPlayer();
        _track = track;
        SpawnLaneButtons();
        Play();
    }

    public void ResetPlayer()
    {
        _audioSource.Stop();
        _isPlaying = false;
        _songTime = 0.0;
        _processedNoteIndex = -1;
        _track.SortNotes();
        foreach (var note in _notes)
        {
            Destroy(note.gameObject);
        }
        _notes.Clear();
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        UpdateSongTime();

        for (var i = _processedNoteIndex + 1; i < _track.NoteCount; i++)
        {
            if (_track.GetNoteTime(i) <= _songTime + _spawnTimeOffset)
            {
                _processedNoteIndex = i;
                _notes.Add(SpawnNote(_track.Notes[i], _track.GetNoteTime(i)));
            }
        }

        foreach (var note in _notes)
        {
            note.SetSongTime(_songTime);
        }

        // Notes stay hittable until they're a full hit window late
        for (var i = _notes.Count - 1; i >= 0; i--)
        {
            var note = _notes[i];
            if (note.TimeLeft < -_hitWindow)
            {
                _notes.RemoveAt(i);
                NoteMissed?.Invoke(note);
                note.Destroy();
            }
        }
    }

    private void UpdateSongTime()
    {
        var dspTime = AudioSettings.dspTime;
        if (dspTime != _lastDspTime)
        {
            _lastDspTime = dspTime;
            _songTime = dspTime - _startDspTime;
        }
        else
        {
            _songTime += Time.unscaledDeltaTime;
        }
    }

    private TrackNote SpawnNote(TrackNoteRecord record, float hitTime)
    {
        var note = Instantiate(_prefab, _spawnContainer);
        note.transform.localPosition = GetSpawnLocalPosition(record.NoteIndex);
        note.Init(record.NoteIndex, GetEndLocalPosition(record.NoteIndex),
             hitTime,
             _spawnTimeOffset,
            _noteMaterials[record.NoteIndex % (_track.MaxNoteIndex + 1)]);
        return note;
    }

    private Vector3 GetSpawnLocalPosition(int noteIndex)
    {
        var center = _track.MaxNoteIndex * 0.5f;
        var rightOffset = (-center + (float)noteIndex) * _offsetPerNoteIndex * Vector3.right;
        return Vector3.zero + rightOffset;
    }

    private Vector3 GetEndLocalPosition(int noteIndex)
    {
        var center = _track.MaxNoteIndex * 0.5f;
        var rightOffset = (-center + (float)noteIndex) * _offsetPerNoteIndex * Vector3.right;
        return Vector3.zero + rightOffset + Vector3.forward * _finalPosOffset;
    }

    private void OnDrawGizmosSelected()
    {
        if (_spawnContainer == null)
        {
            return;
        }

        var radius = Mathf.Max(0.02f, Mathf.Abs(_offsetPerNoteIndex) * 0.2f);
      
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_spawnContainer.position, radius * 0.5f);
    
        if (_track == null || _track.NoteCount == 0)
        {
            return;
        }

        for (var i = 0; i <= _track.MaxNoteIndex; i++)
        {
            var spawnPos = _spawnContainer.TransformPoint(GetSpawnLocalPosition(i));
            var endPos = _spawnContainer.TransformPoint(GetEndLocalPosition(i));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPos, radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(spawnPos, endPos);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(endPos, radius);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(spawnPos, i.ToString());
#endif
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            _spawnContainer.position,
            $"lanes 0-{_track.MaxNoteIndex}  spacing {_offsetPerNoteIndex:0.##}  travel {_spawnTimeOffset:0.##}s");
#endif
    }
}
