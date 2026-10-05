using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class TrackNoteRecord
{
    public static readonly Comparer ComparerInstance = new Comparer();

    [SerializeField] private int _noteIndex;
    [SerializeField] private float _position;

    public int NoteIndex => _noteIndex;
    public float Position => _position;

    public class Comparer : IComparer<TrackNoteRecord>
    {
        public int Compare(TrackNoteRecord a, TrackNoteRecord b)
        {
            return a.Position.CompareTo(b.Position);
        }
    }
}

[CreateAssetMenu(menuName = "Game/Track", fileName = "New Track")]
public class Track : ScriptableObject
{
    [SerializeField] private AudioClip _clip;
    [SerializeField] private float _bpm;
    // Shifts every note in time, in seconds. Positive if notes feel early (rushing), negative if they drag.
    [SerializeField] private float _timingOffset;
    // Where beat 0 lands, in the same time as note positions (timing offset is applied on top)
    [SerializeField] private float _firstBeatTime;
    // Crowd claps every other beat instead of every beat
    [SerializeField] private bool _halfTimeClaps;
    // Metal tracks can start a mosh pit
    [SerializeField] private bool _isMetal;
    [SerializeField] private List<TrackNoteRecord> _notes;
    [SerializeField] private float _duration;

    private int? _maxNoteIndex;

    public AudioClip Clip => _clip;
    public float Bpm => _bpm;
    public float TimingOffset => _timingOffset;
    public IReadOnlyList<TrackNoteRecord> Notes => _notes;
    public int NoteCount => Notes.Count;
    public float Duration => _duration;

    // When a note should be hit, in song time
    public float GetNoteTime(int index) => _notes[index].Position + _timingOffset;

    // Beats since beat 0 (fractional). Assumes a constant tempo.
    public double GetBeat(double songTime) => (songTime - _firstBeatTime - _timingOffset) * _bpm / 60.0;

    // Inverse of GetBeat: the song time a (fractional) beat lands on
    public double GetBeatTime(double beat) => _firstBeatTime + _timingOffset + beat * 60.0 / _bpm;

    public int ClapInterval => _halfTimeClaps ? 2 : 1;
    public bool IsMetal => _isMetal;

    public int MaxNoteIndex
        => _maxNoteIndex ??= _notes.Max(t => t.NoteIndex);

    public void SortNotes()
    {
        _notes.Sort(0, NoteCount, TrackNoteRecord.ComparerInstance);
    }
}
