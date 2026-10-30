using System.Linq;
using UnityEngine;

// Plays the recorded songs on the TrackPlayer, following the replay clock, and applies the recorded hits and misses
public class ReplaySong : MonoBehaviour
{
    [SerializeField] private ReplayEventDispatcher _dispatcher;
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private Track[] _tracks;

    private double _songStartReplayTime;
    private double _songTimeAtStart;

    private void OnEnable()
    {
        _dispatcher.Subscribe<SongStartedEvent>(OnSongStarted);
        _dispatcher.Subscribe<SongFinishedEvent>(OnSongFinished);
        _dispatcher.Subscribe<NoteHitEvent>(OnNoteHit);
        _dispatcher.Subscribe<NoteMissedEvent>(OnNoteMissed);
        _dispatcher.Subscribe<EmptyPressEvent>(OnEmptyPress);
        _dispatcher.Subscribe<LaneLayoutChangedEvent>(OnLaneLayoutChanged);
        _dispatcher.Ticked += AdvanceSong;
        _dispatcher.Finished += OnReplayFinished;
    }

    private void OnDisable()
    {
        _dispatcher.Unsubscribe<SongStartedEvent>(OnSongStarted);
        _dispatcher.Unsubscribe<SongFinishedEvent>(OnSongFinished);
        _dispatcher.Unsubscribe<NoteHitEvent>(OnNoteHit);
        _dispatcher.Unsubscribe<NoteMissedEvent>(OnNoteMissed);
        _dispatcher.Unsubscribe<EmptyPressEvent>(OnEmptyPress);
        _dispatcher.Unsubscribe<LaneLayoutChangedEvent>(OnLaneLayoutChanged);
        _dispatcher.Ticked -= AdvanceSong;
        _dispatcher.Finished -= OnReplayFinished;
    }

    private void OnSongStarted(SongStartedEvent songStarted)
    {
        var track = _tracks.FirstOrDefault(candidate => candidate != null && candidate.name == songStarted.TrackName);
        if (track == null)
        {
            Debug.LogError($"Can't replay track '{songStarted.TrackName}', add it to {nameof(ReplaySong)}'s tracks");
            return;
        }

        _songStartReplayTime = songStarted.Time;
        _songTimeAtStart = songStarted.SongTime;
        _trackPlayer.PlayExternallyClocked(track, CurrentSongTime);
    }

    private void OnSongFinished(SongFinishedEvent songFinished)
    {
        _trackPlayer.ResetPlayer();
        _trackPlayer.IsApplauding = true;
    }

    private void OnReplayFinished()
    {
        _trackPlayer.IsApplauding = false;
    }

    private void AdvanceSong()
    {
        _trackPlayer.ApplauseSpeed = _dispatcher.IsPaused ? 0.0f : _dispatcher.Speed;
        if (_trackPlayer.IsPlaying)
        {
            _trackPlayer.SetExternalSongTime(CurrentSongTime, _dispatcher.IsPaused ? 0.0f : _dispatcher.Speed);
        }
    }

    private double CurrentSongTime => _songTimeAtStart + (_dispatcher.CurrentTime - _songStartReplayTime);

    private void OnNoteHit(NoteHitEvent noteHit)
    {
        _trackPlayer.ApplyRecordedHit(noteHit.NoteRecordIndex, noteHit.Accuracy);
    }

    private void OnNoteMissed(NoteMissedEvent noteMissed)
    {
        _trackPlayer.ApplyRecordedMiss(noteMissed.NoteRecordIndex);
    }

    private void OnEmptyPress(EmptyPressEvent emptyPress)
    {
        _trackPlayer.ApplyRecordedEmptyPress(emptyPress.Lane);
    }

    private void OnLaneLayoutChanged(LaneLayoutChangedEvent layout)
    {
        _trackPlayer.ApplyRecordedLayout(layout.HitLineHeight, layout.HorizontalHeightFactor);
    }
}
