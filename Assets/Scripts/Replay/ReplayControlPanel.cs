using System;
using TMPro;
using UnityEngine;

// World-space play/pause, speed and restart controls. Builds its own panel like ConcertSet, so place this object where the panel should appear.
public class ReplayControlPanel : MonoBehaviour
{
    [SerializeField] private ReplayEventDispatcher _dispatcher;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private float[] _speeds = { 0.25f, 0.5f, 1.0f, 2.0f, 4.0f };

    [Header("UI")]
    [SerializeField] private Vector2 _buttonSize = new Vector2(600.0f, 120.0f);
    [SerializeField] private float _spacing = 40.0f;
    [SerializeField] private float _metersPerUnit = 0.001f;
    [SerializeField] private float _fontSize = 56.0f;
    [SerializeField] private Color _panelColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    [SerializeField] private Color _buttonColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);

    private TextMeshProUGUI _timeLabel;
    private TextMeshProUGUI _playPauseLabel;
    private TextMeshProUGUI _speedLabel;

    private void Start()
    {
        var panel = WorldUI.CreatePanel(transform, "Replay Panel", _metersPerUnit, _spacing, _panelColor);
        _timeLabel = WorldUI.CreateLabel(panel.transform, "", _buttonSize, _fontSize);
        _playPauseLabel = CreateButton(panel.transform, "Pause", TogglePause);
        _speedLabel = CreateButton(panel.transform, "", CycleSpeed);
        CreateButton(panel.transform, "Restart", _sceneTransition.ReloadScene);
    }

    private TextMeshProUGUI CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var button = WorldUI.CreateButton(parent, label, onClick, _buttonSize, _fontSize, _buttonColor);
        return button.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (_dispatcher.Recording == null)
        {
            _timeLabel.text = "No recording";
            return;
        }

        _timeLabel.text = $"{FormatTime(_dispatcher.CurrentTime)} / {FormatTime(_dispatcher.Duration)}";
        _playPauseLabel.text = _dispatcher.IsFinished ? "Finished" : _dispatcher.IsPaused ? "Play" : "Pause";
        _speedLabel.text = $"Speed {_dispatcher.Speed:0.##}x";
    }

    private void TogglePause()
    {
        if (_dispatcher.Recording != null && !_dispatcher.IsFinished)
        {
            _dispatcher.IsPaused = !_dispatcher.IsPaused;
        }
    }

    private void CycleSpeed()
    {
        var current = Array.IndexOf(_speeds, _dispatcher.Speed);
        _dispatcher.Speed = _speeds[(current + 1) % _speeds.Length];
    }

    private static string FormatTime(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");

#if UNITY_EDITOR
    private void Reset()
    {
        _dispatcher = FindFirstObjectByType<ReplayEventDispatcher>();
        _sceneTransition = FindFirstObjectByType<SceneTransition>();
    }
#endif
}
