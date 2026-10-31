using System.Collections;
using UnityEngine;

// Runs the show: every song in the set list with applause in between, then the end. ConcertMenu is the UI on top.
public class ConcertSet : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdCheer _cheer;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private RecordingSessionWriter _recordingWriter;
    [SerializeField] private string _replaySceneName = "ReplayScene";
    [SerializeField] private Track[] _setList;

    [SerializeField] private float _applauseDuration = 5.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _engagementKept = 0.5f;

    [Header("Debug")]
    [SerializeField] private DebugSkipInput _debugSkip;
    [SerializeField] private Vector2 _skipEngagementRange = new Vector2(0.1f, 0.6f);

    private int _songIndex = -1;
    private bool _hasEnded;
    private int _startedFrame = -1;
    private Coroutine _applause;
    private bool _isLoadingReplay;

    public bool CanRecord => _recordingWriter != null;
    public bool HasStarted => _songIndex >= 0;

    public event System.Action SetStarted;
    public event System.Action SetEnded;

    private void OnEnable()
    {
        _trackPlayer.Finished += OnSongFinished;
        if (_debugSkip != null)
        {
            _debugSkip.SkipPressed += DebugSkipSong;
            _debugSkip.SkipToEndPressed += DebugSkipToEnd;
        }
    }

    private void OnDisable()
    {
        _trackPlayer.Finished -= OnSongFinished;
        if (_debugSkip != null)
        {
            _debugSkip.SkipPressed -= DebugSkipSong;
            _debugSkip.SkipToEndPressed -= DebugSkipToEnd;
        }
    }

    // A song that isn't loaded yet is loaded on the main thread the moment it plays, freezing the game for a beat.
    // Asking for all of them up front lets clips set to load in the background do so while the start screen is up
    private void Start()
    {
        foreach (var track in _setList)
        {
            LoadClip(track.BackingClip);
            LoadClip(track.InstrumentClip);
        }
    }

    private static void LoadClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private void DebugSkipSong()
    {
        // The same press may have just started the set from the start screen
        if (_songIndex < 0 || _hasEnded || Time.frameCount == _startedFrame)
        {
            return;
        }

        if (_applause != null)
        {
            StopCoroutine(_applause);
            Debug.Log($"[Debug] Skipped applause after song {_songIndex + 1}/{_setList.Length}");
            EndApplause();
            return;
        }

        var engagement = Random.Range(_skipEngagementRange.x, _skipEngagementRange.y);
        _engagement.Add(engagement);
        Debug.Log($"[Debug] Skipped song {_songIndex + 1}/{_setList.Length}, added {engagement:0.00} engagement " +
                  $"(now {_engagement.Engagement:0.00})");
        OnSongFinished();
    }

    public void StartSet()
    {
        if (_songIndex >= 0 || _setList.Length == 0)
        {
            return;
        }

        _engagement.ResetEngagement();
        _startedFrame = Time.frameCount;
        SetStarted?.Invoke();
        PlaySong(0);
    }

    private void PlaySong(int index)
    {
        _songIndex = index;
        _trackPlayer.PlayTrack(_setList[index]);
    }

    private void OnSongFinished()
    {
        _applause = StartCoroutine(Applause());
    }

    private IEnumerator Applause()
    {
        _trackPlayer.ResetPlayer();
        _trackPlayer.IsApplauding = true;
        if (_cheer != null)
        {
            _cheer.Cheer();
        }

        yield return new WaitForSeconds(_applauseDuration);
        EndApplause();
    }

    private void EndApplause()
    {
        _applause = null;
        _trackPlayer.IsApplauding = false;
        if (_songIndex + 1 < _setList.Length)
        {
            _engagement.Set(_engagement.Engagement * _engagementKept);
            PlaySong(_songIndex + 1);
        }
        else
        {
            EndSet();
        }
    }

    private void EndSet()
    {
        _hasEnded = true;
        SetEnded?.Invoke();
    }

    // Starts the set first if it hasn't, so everything that follows the show (recording, menus) sees a normal start and end
    private void DebugSkipToEnd()
    {
        // Paused: leave it be, the pause menu would end up on top of the end screen with the game still frozen
        if (_hasEnded || Time.timeScale == 0.0f)
        {
            Debug.Log($"[SkipDiag] Skip to end ignored: hasEnded {_hasEnded}, timeScale {Time.timeScale}");
            return;
        }

        if (_songIndex < 0)
        {
            StartSet();
        }
        if (_applause != null)
        {
            StopCoroutine(_applause);
            _applause = null;
        }
        _trackPlayer.ResetPlayer();
        _songIndex = _setList.Length - 1;
        Debug.Log("[Debug] Skipped to the end of the set");
        EndSet();
    }

    public void Restart()
    {
        _sceneTransition.ReloadScene();
    }

    // The recording may still be flushing to disk, so this waits for it before switching scenes
    public void WatchReplay()
    {
        if (_recordingWriter == null || _isLoadingReplay)
        {
            return;
        }
        _isLoadingReplay = true;
        StartCoroutine(LoadReplayOnceSaved());
    }

    private IEnumerator LoadReplayOnceSaved()
    {
        while (_recordingWriter.IsWriting)
        {
            yield return null;
        }
        ReplayEventDispatcher.RequestedRecordingPath = _recordingWriter.LastRecordingPath;
        _sceneTransition.LoadScene(_replaySceneName);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _cheer = FindFirstObjectByType<CrowdCheer>();
        _sceneTransition = FindFirstObjectByType<SceneTransition>();
        _recordingWriter = FindFirstObjectByType<RecordingSessionWriter>();
        _debugSkip = FindFirstObjectByType<DebugSkipInput>();
    }
#endif
}
