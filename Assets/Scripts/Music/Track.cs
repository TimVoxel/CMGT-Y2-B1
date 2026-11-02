using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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
    [SerializeField, FormerlySerializedAs("_clip")] private AudioClip _backingClip;
    [SerializeField] private AudioClip _instrumentClip;
    [SerializeField, Range(0.0f, 1.0f)] private float _instrumentVolume = 1.0f;
    [SerializeField] private float _bpm;
    [SerializeField] private float _timingOffset;
    [SerializeField] private float _firstBeatTime;
    // Crowd claps every other beat instead of every beat
    [SerializeField] private bool _halfTimeClaps;
    // Metal tracks can start a mosh pit
    [SerializeField] private bool _isMetal;
    // Numbered like GarageBand, the first bar is 1
    [SerializeField] private int[] _smokeBars;
    [SerializeField] private List<TrackNoteRecord> _notes;
    [SerializeField] private float _duration;

    private const int BeatsPerBar = 4;

    private int? _maxNoteIndex;

    public AudioClip BackingClip => _backingClip;
    public AudioClip InstrumentClip => _instrumentClip;
    public float InstrumentVolume => _instrumentVolume;
    public float Bpm => _bpm;
    public float TimingOffset => _timingOffset;
    public IReadOnlyList<TrackNoteRecord> Notes => _notes;
    public int NoteCount => Notes.Count;
    public float Duration => _duration;

    public float GetNoteTime(int index) => _notes[index].Position + _timingOffset;

    public double GetBeat(double songTime) => (songTime - _firstBeatTime) * _bpm / 60.0;

    public double GetBeatTime(double beat) => _firstBeatTime + beat * 60.0 / _bpm;

    public double GetBarTime(int bar) => GetBeatTime((bar - 1) * BeatsPerBar);

    public int ClapInterval => _halfTimeClaps ? 2 : 1;
    public bool IsMetal => _isMetal;
    public IReadOnlyList<int> SmokeBars => _smokeBars ?? System.Array.Empty<int>();

    public int MaxNoteIndex
        => _maxNoteIndex ??= _notes.Max(t => t.NoteIndex);

    public void SortNotes()
    {
        _notes.Sort(0, NoteCount, TrackNoteRecord.ComparerInstance);
    }
}
