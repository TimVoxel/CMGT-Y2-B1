using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// The replay clock. Loads a recording and hands out each event once playback reaches its time.
// Runs first so listeners react to this frame's events before their own Update.
[DefaultExecutionOrder(-300)]
public class ReplayEventDispatcher : MonoBehaviour
{
    [SerializeField] private string _recordingsSubPath;
    [SerializeField] private string _recordingFileName;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private float _speed = 1.0f;

    private readonly Dictionary<Type, IHandlers> _handlers = new Dictionary<Type, IHandlers>();
    private int _nextEventIndex;

    public Recording Recording { get; private set; }
    public double CurrentTime { get; private set; }
    public double Duration => Recording.Events.Count > 0 ? Recording.Events[Recording.Events.Count - 1].Time : 0.0;
    public bool IsPaused { get; set; }
    public bool IsFinished => _nextEventIndex >= Recording.Events.Count;

    public float Speed
    {
        get => _speed;
        set => _speed = Mathf.Max(0.0f, value);
    }

    public event Action Ticked;
    public event Action Finished;

    public static string RequestedRecordingPath { get; set; }

    private string RecordingsDirectory => Path.Combine(Application.persistentDataPath, _recordingsSubPath);

    private void Awake()
    {
        var path = FindRecordingPath();
        if (path == null)
        {
            Debug.LogError($"No recording to replay in {RecordingsDirectory}");
            enabled = false;
            return;
        }

        Recording = Recording.Load(path);
        IsPaused = !_playOnStart;
        Debug.Log($"Replaying {path} ({Recording.Events.Count} events)");
    }

    private string FindRecordingPath()
    {
        if (File.Exists(RequestedRecordingPath))
        {
            return RequestedRecordingPath;
        }
        if (!Directory.Exists(RecordingsDirectory))
        {
            return null;
        }
        if (!string.IsNullOrEmpty(_recordingFileName))
        {
            var path = Path.Combine(RecordingsDirectory, _recordingFileName);
            return File.Exists(path) ? path : null;
        }
        return new DirectoryInfo(RecordingsDirectory)
            .GetFiles($"*.{RecordingSessionWriter.Extension}")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private void Update()
    {
        var wasFinished = IsFinished;
        if (!IsPaused)
        {
            // Capped so a hitch or the app being suspended doesn't skip a chunk of the replay
            var step = Math.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime) * _speed;
            CurrentTime = Math.Min(CurrentTime + step, Duration);
        }
        DispatchDueEvents();
        Ticked?.Invoke();

        if (IsFinished && !wasFinished)
        {
            IsPaused = true;
            Finished?.Invoke();
        }
    }

    private void DispatchDueEvents()
    {
        var events = Recording.Events;
        while (_nextEventIndex < events.Count && events[_nextEventIndex].Time <= CurrentTime)
        {
            var recordedEvent = events[_nextEventIndex];
            _nextEventIndex++;
            if (_handlers.TryGetValue(recordedEvent.GetType(), out var handlers))
            {
                handlers.Invoke(recordedEvent);
            }
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : RecordedEvent
    {
        GetHandlers<T>().Actions += handler;
    }

    public void Unsubscribe<T>(Action<T> handler) where T : RecordedEvent
    {
        GetHandlers<T>().Actions -= handler;
    }

    public bool TryFindNext<T>(Func<T, bool> match, out T found) where T : RecordedEvent
    {
        var events = Recording.Events;
        for (var i = _nextEventIndex; i < events.Count; i++)
        {
            if (events[i] is T candidate && match(candidate))
            {
                found = candidate;
                return true;
            }
        }
        found = null;
        return false;
    }

    private Handlers<T> GetHandlers<T>() where T : RecordedEvent
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers = new Handlers<T>();
            _handlers[typeof(T)] = handlers;
        }
        return (Handlers<T>)handlers;
    }

    private interface IHandlers
    {
        void Invoke(RecordedEvent recordedEvent);
    }

    private sealed class Handlers<T> : IHandlers where T : RecordedEvent
    {
        public Action<T> Actions;

        public void Invoke(RecordedEvent recordedEvent)
        {
            Actions?.Invoke((T)recordedEvent);
        }
    }
}
