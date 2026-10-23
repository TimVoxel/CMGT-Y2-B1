using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [SerializeField] private float _hitWindow = 0.15f;
    [SerializeField, FormerlySerializedAs("_laneButtonPrefab")] private GameObject _laneMarkerPrefab;
    [SerializeField] private LaneHitZone[] _laneHitZones;

    [SerializeField] private Material[] _noteMaterials;
    [SerializeField] private Material[] _noteButtonMaterials;

    private readonly List<GameObject> _laneMarkers = new List<GameObject>();

    public event Action<TrackNote, float> NoteHit;
    public event Action<TrackNote> NoteMissed;
    public event Action<int> EmptyPressed;
    public event Action Started;
    public event Action Finished;
    public event Action LayoutChanged;

    private const float MaxReplayAudioDrift = 0.1f;

    private double _startDspTime;
    private double _lastDspTime;
    private double _songTime;
    private bool _isExternallyClocked;
    private bool _isPlaying;
    private bool _finished;
    private int _processedNoteIndex;

    private List<TrackNote> _notes = new List<TrackNote>();
    public IReadOnlyList<TrackNote> Notes => _notes;
    public double SongTime => _songTime;
    public bool IsPlaying => _isPlaying;
    public float HitWindow => _hitWindow;
    public double CurrentBeat => _track.GetBeat(_songTime);
    public Track Track => _track;

    private float _horizontalHeightFactor;
    public float HorizontalHeightFactor
    {
        get => _horizontalHeightFactor;
        set
        {
            _horizontalHeightFactor = value;
            UpdateLaneMarkerPositions();
            UpdateNotePaths();
            LayoutChanged?.Invoke();
        }
    }
    private float HorizontalOffsetPerNote => _offsetPerNoteIndex + Mathf.Max(0f, HorizontalHeightFactor);

    public float LaneSpacing => HorizontalOffsetPerNote;
    public int LaneCount => _track.MaxNoteIndex + 1;
    public Quaternion LaneRotation => _spawnContainer.rotation;

    public Vector3 HitLineCenter => _spawnContainer.TransformPoint(Vector3.forward * _finalPosOffset);
    public double SongTimeToDspTime(double songTime) => _startDspTime + songTime;

    private void Awake()
    {
        if (_audioSource == null && !TryGetComponent(out _audioSource))
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;

        SpawnLaneMarkers();
        ResetPlayer();
    }

    private void SpawnLaneMarkers()
    {
        foreach (var marker in _laneMarkers)
        {
            Destroy(marker);
        }
        _laneMarkers.Clear();

        if (_track.NoteCount == 0)
        {
            return;
        }

        for (var i = 0; i <= _track.MaxNoteIndex; i++)
        {
            var laneMaterial = _noteButtonMaterials[i % (_track.MaxNoteIndex + 1)];
            if (i < _laneHitZones.Length)
            {
                _laneHitZones[i].Init(this, i, laneMaterial);
            }

            if (_laneMarkerPrefab == null)
            {
                continue;
            }
            var marker = Instantiate(_laneMarkerPrefab, _spawnContainer);
            marker.transform.localPosition = GetEndLocalPosition(i);
            marker.transform.localRotation = Quaternion.identity;
            foreach (var markerRenderer in marker.GetComponentsInChildren<MeshRenderer>())
            {
                markerRenderer.sharedMaterial = laneMaterial;
            }
            _laneMarkers.Add(marker);
        }
    }

    private void UpdateLaneMarkerPositions()
    {
        for (var i = 0; i < _laneMarkers.Count; i++)
        {
            _laneMarkers[i].transform.localPosition = GetEndLocalPosition(i);
        }
    }

    private void UpdateNotePaths()
    {
        foreach (var note in _notes)
        {
            note.SetPath(GetSpawnLocalPosition(note.NoteIndex), GetEndLocalPosition(note.NoteIndex));
            note.SetSongTime(_songTime);
        }
    }

    public bool TryHit(int laneIndex, out float accuracy)
    {
        accuracy = 0.0f;
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
            EmptyPressed?.Invoke(laneIndex);
            return false;
        }

        var offset = -closest.TimeLeft;
        accuracy = 1.0f - Mathf.Clamp01(Mathf.Abs(offset) / Mathf.Max(1e-6f, HitWindow));
        RemoveHitNote(closest, accuracy);
        return true;
    }

    private void RemoveHitNote(TrackNote note, float accuracy)
    {
        _notes.Remove(note);
        NoteHit?.Invoke(note, accuracy);
        note.Destroy();
    }

    private void RemoveMissedNote(TrackNote note)
    {
        _notes.Remove(note);
        NoteMissed?.Invoke(note);
        if (note.NoteIndex < _laneHitZones.Length)
        {
            _laneHitZones[note.NoteIndex].NotifyMissed();
        }
        note.Destroy();
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

        _isExternallyClocked = false;
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

    public void SetHitLineHeight(float worldY)
    {
        _spawnContainer.position += Vector3.up * (worldY - HitLineCenter.y);
        LayoutChanged?.Invoke();
    }
    
    public void PlayTrack(Track track)
    {
        ResetPlayer();
        _track = track;
        SpawnLaneMarkers();
        Play();
    }

    public void PlayExternallyClocked(Track track, double songTime)
    {
        ResetPlayer();
        _track = track;
        SpawnLaneMarkers();

        _isExternallyClocked = true;
        _audioSource.clip = _track.Clip;
        _isPlaying = true;
        SetExternalSongTime(songTime, 1.0f);
        Started?.Invoke();
    }

    public void SetExternalSongTime(double songTime, float playbackSpeed)
    {
        _songTime = songTime;
        _startDspTime = AudioSettings.dspTime - songTime;

        var clip = _audioSource.clip;
        var audible = clip != null && playbackSpeed > 0.0f && songTime >= 0.0 && songTime < clip.length;
        if (!audible)
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Pause();
            }
            return;
        }

        _audioSource.pitch = playbackSpeed;
        if (!_audioSource.isPlaying || Mathf.Abs(_audioSource.time - (float)songTime) > MaxReplayAudioDrift)
        {
            _audioSource.time = (float)songTime;
            if (!_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }
    }

    public void ApplyRecordedHit(int noteRecordIndex, float accuracy)
    {
        var note = _notes.Find(candidate => candidate.RecordIndex == noteRecordIndex);
        if (note == null)
        {
            return;
        }
        if (note.NoteIndex < _laneHitZones.Length)
        {
            _laneHitZones[note.NoteIndex].ShowPress(true, accuracy);
        }
        RemoveHitNote(note, accuracy);
    }

    public void ApplyRecordedMiss(int noteRecordIndex)
    {
        var note = _notes.Find(candidate => candidate.RecordIndex == noteRecordIndex);
        if (note != null)
        {
            RemoveMissedNote(note);
        }
    }

    public void ApplyRecordedEmptyPress(int laneIndex)
    {
        if (laneIndex < _laneHitZones.Length)
        {
            _laneHitZones[laneIndex].ShowPress(false, 0.0f);
        }
        EmptyPressed?.Invoke(laneIndex);
    }

    public void ApplyRecordedLayout(float hitLineHeight, float horizontalHeightFactor)
    {
        SetHitLineHeight(hitLineHeight);
        HorizontalHeightFactor = horizontalHeightFactor;
    }

    public void ResetPlayer()
    {
        _audioSource.Stop();
        _isPlaying = false;
        _finished = false;
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

        if (!_isExternallyClocked)
        {
            UpdateSongTime();
        }

        for (var i = _processedNoteIndex + 1; i < _track.NoteCount; i++)
        {
            if (_track.GetNoteTime(i) <= _songTime + _spawnTimeOffset)
            {
                _processedNoteIndex = i;
                _notes.Add(SpawnNote(i));
            }
        }

        foreach (var note in _notes)
        {
            note.SetSongTime(_songTime);
        }

        if (_isExternallyClocked)
        {
            return;
        }

        for (var i = _notes.Count - 1; i >= 0; i--)
        {
            var note = _notes[i];
            if (note.TimeLeft < -_hitWindow)
            {
                RemoveMissedNote(note);
            }
        }

        if (!_finished && _songTime >= _track.Duration
            && _processedNoteIndex >= _track.NoteCount - 1 && _notes.Count == 0
            && !_audioSource.isPlaying)
        {
            _finished = true;
            Finished?.Invoke();
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

    private TrackNote SpawnNote(int recordIndex)
    {
        var record = _track.Notes[recordIndex];
        var note = Instantiate(_prefab, _spawnContainer);
        note.transform.localPosition = GetSpawnLocalPosition(record.NoteIndex);
        note.Init(record.NoteIndex, recordIndex, GetEndLocalPosition(record.NoteIndex),
             _track.GetNoteTime(recordIndex),
             _spawnTimeOffset,
            _noteMaterials[record.NoteIndex % (_track.MaxNoteIndex + 1)]);
        return note;
    }

    private Vector3 GetSpawnLocalPosition(int noteIndex)
    {
        var center = _track.MaxNoteIndex * 0.5f;
        var rightOffset = (-center + (float)noteIndex) * HorizontalOffsetPerNote * Vector3.right;
        return Vector3.zero + rightOffset;
    }

    private Vector3 GetEndLocalPosition(int noteIndex)
    {
        var center = _track.MaxNoteIndex * 0.5f;
        var rightOffset = (-center + (float)noteIndex) * HorizontalOffsetPerNote * Vector3.right;
        return Vector3.zero + rightOffset + Vector3.forward * _finalPosOffset;
    }

    private void OnDrawGizmos()
    {
        if (_spawnContainer == null)
        {
            return;
        }

        var radius = Mathf.Max(0.02f, Mathf.Abs(HorizontalOffsetPerNote) * 0.2f);
      
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
            $"lanes 0-{_track.MaxNoteIndex}  spacing {HorizontalOffsetPerNote:0.##}  travel {_spawnTimeOffset:0.##}s");
#endif
    }
}
