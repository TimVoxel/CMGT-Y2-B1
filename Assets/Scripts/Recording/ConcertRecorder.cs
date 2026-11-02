using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Observes the show and turns everything a replay needs into RecordedEvents.
// Doesn't write anything itself: a writer picks up the session from RecordingStarted and drains its queue.
public class ConcertRecorder : MonoBehaviour
{
    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdCheer _cheer;
    [SerializeField] private MoshPit _moshPit;
    [SerializeField] private GamePause _pause;

    [SerializeField] private Transform _head;
    [SerializeField] private Transform _leftHand;
    [SerializeField] private Transform _rightHand;

    [SerializeField] private float _poseSampleRate = 30.0f;
    [SerializeField] private float _minPositionDelta = 0.001f;
    [SerializeField] private float _minAngleDelta = 0.25f;
    [SerializeField] private float _minEngagementDelta = 0.005f;

    private readonly RecordedPose?[] _lastPoses = new RecordedPose?[3];
    private double _startTime;
    private double _nextPoseSampleTime;
    private float _lastEngagement;
    private bool _isPaused;
    private double _pausedAt;
    private double _pausedTotal;

    public RecordingSession Session { get; private set; }
    public bool IsRecording => Session != null;

    public event Action<RecordingSession> RecordingStarted;
    public event Action<RecordingSession> RecordingStopped;

    // Paused time is cut out, so the replay plays straight through a pause
    private double Now => (_isPaused ? _pausedAt : Time.unscaledTimeAsDouble) - _startTime - _pausedTotal;

    private void OnEnable()
    {
        _concertSet.SetStarted += StartRecording;
        _concertSet.SetEnded += StopRecording;
        if (_pause != null)
        {
            _pause.Paused += OnPaused;
            _pause.Resumed += OnResumed;
        }
    }

    private void OnDisable()
    {
        _concertSet.SetStarted -= StartRecording;
        _concertSet.SetEnded -= StopRecording;
        if (_pause != null)
        {
            _pause.Paused -= OnPaused;
            _pause.Resumed -= OnResumed;
        }
        StopRecording();
    }

    private void OnPaused()
    {
        _isPaused = true;
        _pausedAt = Time.unscaledTimeAsDouble;
    }

    private void OnResumed()
    {
        _isPaused = false;
        _pausedTotal += Time.unscaledTimeAsDouble - _pausedAt;
    }

    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        _startTime = Time.unscaledTimeAsDouble;
        _pausedTotal = 0.0;
        _nextPoseSampleTime = 0.0;
        Array.Clear(_lastPoses, 0, _lastPoses.Length);
        Session = new RecordingSession(new RecordingHeader(DateTime.UtcNow, SceneManager.GetActiveScene().name, _poseSampleRate));

        Subscribe();
        RecordSnapshot();
        RecordingStarted?.Invoke(Session);
    }

    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        Unsubscribe();
        var session = Session;
        Session = null;
        session.Finish();
        RecordingStopped?.Invoke(session);
    }

    private void Subscribe()
    {
        _trackPlayer.Started += OnSongStarted;
        _trackPlayer.Finished += OnSongFinished;
        _trackPlayer.NoteHit += OnNoteHit;
        _trackPlayer.NoteMissed += OnNoteMissed;
        _trackPlayer.EmptyPressed += OnEmptyPressed;
        _trackPlayer.LayoutChanged += OnLaneLayoutChanged;
        _audience.MemberAdded += OnMemberAdded;
        _audience.MemberRemoved += OnMemberRemoved;
        _engagement.Changed += OnEngagementChanged;
        if (_cheer != null)
        {
            _cheer.Cheered += OnCheered;
        }
        if (_moshPit != null)
        {
            _moshPit.PitStarted += OnMoshPitStarted;
            _moshPit.PitEnded += OnMoshPitEnded;
        }
    }

    private void Unsubscribe()
    {
        _trackPlayer.Started -= OnSongStarted;
        _trackPlayer.Finished -= OnSongFinished;
        _trackPlayer.NoteHit -= OnNoteHit;
        _trackPlayer.NoteMissed -= OnNoteMissed;
        _trackPlayer.EmptyPressed -= OnEmptyPressed;
        _trackPlayer.LayoutChanged -= OnLaneLayoutChanged;
        _audience.MemberAdded -= OnMemberAdded;
        _audience.MemberRemoved -= OnMemberRemoved;
        _engagement.Changed -= OnEngagementChanged;
        if (_cheer != null)
        {
            _cheer.Cheered -= OnCheered;
        }
        if (_moshPit != null)
        {
            _moshPit.PitStarted -= OnMoshPitStarted;
            _moshPit.PitEnded -= OnMoshPitEnded;
        }
    }

    // Like a keyframe: the state of the show at the moment recording starts, so later events have something to apply to
    private void RecordSnapshot()
    {
        foreach (var member in _audience.Members)
        {
            OnMemberAdded(member, false);
        }
        RecordEngagement(_engagement.Engagement);
        OnLaneLayoutChanged();
        if (_trackPlayer.IsPlaying)
        {
            OnSongStarted();
        }
        if (_moshPit != null && _moshPit.IsActive)
        {
            OnMoshPitStarted();
        }
        RecordPoses(true);
    }

    private void LateUpdate()
    {
        if (!IsRecording || Now < _nextPoseSampleTime)
        {
            return;
        }
        _nextPoseSampleTime = Now + 1.0 / Mathf.Max(1.0f, _poseSampleRate);
        RecordPoses(false);
    }

    private void RecordPoses(bool force)
    {
        RecordPose(TrackedDevice.Head, _head, force);
        RecordPose(TrackedDevice.LeftHand, _leftHand, force);
        RecordPose(TrackedDevice.RightHand, _rightHand, force);
    }

    private void RecordPose(TrackedDevice device, Transform target, bool force)
    {
        if (target == null)
        {
            return;
        }

        var pose = RecordedPose.FromWorld(target);
        var index = (int)device;
        if (!force && _lastPoses[index] is { } last && last.IsCloseTo(pose, _minPositionDelta, _minAngleDelta))
        {
            return;
        }
        _lastPoses[index] = pose;
        Record(new PoseEvent(Now, device, pose));
    }

    private void OnSongStarted()
    {
        Record(new SongStartedEvent(Now, _trackPlayer.Track.name, _trackPlayer.SongTime));
    }

    private void OnSongFinished()
    {
        Record(new SongFinishedEvent(Now));
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        Record(new NoteHitEvent(Now, note.NoteIndex, note.RecordIndex, -note.TimeLeft, accuracy));
    }

    private void OnNoteMissed(TrackNote note)
    {
        Record(new NoteMissedEvent(Now, note.NoteIndex, note.RecordIndex));
    }

    private void OnEmptyPressed(int lane)
    {
        Record(new EmptyPressEvent(Now, lane));
    }

    private void OnLaneLayoutChanged()
    {
        Record(new LaneLayoutChangedEvent(Now, _trackPlayer.HitLineCenter.y, _trackPlayer.HorizontalHeightFactor));
        Record(new LaneDepthChangedEvent(Now, _trackPlayer.HitLineDepth));
    }

    private void OnMemberAdded(AudienceMember member, bool poppedIn)
    {
        Record(new AudienceMemberJoinedEvent(
            Now,
            member.Id,
            member.PrefabIndex,
            member.BasePosition,
            member.BaseRotation,
            member.EngagementThreshold,
            member.MaxAmplitude,
            poppedIn));
    }

    private void OnMemberRemoved(AudienceMember member)
    {
        Record(new AudienceMemberLeftEvent(Now, member.Id));
    }

    private void OnEngagementChanged(float engagement)
    {
        if (Mathf.Abs(engagement - _lastEngagement) >= _minEngagementDelta || engagement <= 0.0f || engagement >= 1.0f)
        {
            RecordEngagement(engagement);
        }
    }

    private void RecordEngagement(float engagement)
    {
        _lastEngagement = engagement;
        Record(new EngagementChangedEvent(Now, engagement));
    }

    private void OnCheered(int clipIndex, float pitch, float volume)
    {
        Record(new CrowdCheeredEvent(Now, clipIndex, pitch, volume));
    }

    private void OnMoshPitStarted()
    {
        Record(new MoshPitStartedEvent(Now));
    }

    private void OnMoshPitEnded()
    {
        Record(new MoshPitEndedEvent(Now));
    }

    private void Record(RecordedEvent recordedEvent)
    {
        Session.Events.Enqueue(recordedEvent);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _concertSet = FindFirstObjectByType<ConcertSet>();
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
        _audience = FindFirstObjectByType<AudienceSpawner>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _cheer = FindFirstObjectByType<CrowdCheer>();
        _moshPit = FindFirstObjectByType<MoshPit>();
        _pause = FindFirstObjectByType<GamePause>();
        if (Camera.main != null)
        {
            _head = Camera.main.transform;
        }
    }
#endif
}
