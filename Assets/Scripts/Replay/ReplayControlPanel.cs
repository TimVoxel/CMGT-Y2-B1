using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// World-space play/pause, speed and restart controls. Builds its own panel like ConcertMenu, so place this object where the panel should appear.
public class ReplayControlPanel : MonoBehaviour
{
    [SerializeField] private ReplayEventDispatcher _dispatcher;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private string _mainMenuSceneName = "Concert";
    [SerializeField] private float[] _speeds = { 0.25f, 0.5f, 1.0f, 2.0f, 4.0f };
    // The left controller's menu button is the Quest convention for an app's own menu
    [SerializeField] private InputAction _toggleAction = CreateToggleAction();

    [Header("UI")]
    [SerializeField] private Vector2 _buttonSize = new Vector2(600.0f, 120.0f);
    [SerializeField] private float _spacing = 40.0f;
    [SerializeField] private float _metersPerUnit = 0.001f;
    [SerializeField] private float _fontSize = 56.0f;
    [SerializeField] private Color _panelColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    [SerializeField] private Color _buttonColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);

    [Header("Share")]
    [SerializeField] private float _shareButtonSize = 160.0f;
    [SerializeField] private Vector2 _sharePopupSize = new Vector2(600.0f, 180.0f);
    [SerializeField] private string _sharePopupText = "The share menu of the future would open here";
    [SerializeField] private float _sharePopupDuration = 3.0f;

    private GameObject _sharePopup;
    private float _sharePopupHideTime;
    private TextMeshProUGUI _timeLabel;
    private TextMeshProUGUI _playPauseLabel;
    private TextMeshProUGUI _speedLabel;
    private GameObject _panel;

    private static InputAction CreateToggleAction()
    {
        var action = new InputAction("Toggle Replay Menu", InputActionType.Button);
        action.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        action.AddBinding("<Keyboard>/m");
        return action;
    }

    private void OnEnable()
    {
        _toggleAction.Enable();
    }

    private void OnDisable()
    {
        _toggleAction.Disable();
    }

    private void Start()
    {
        _panel = WorldUI.CreatePanel(transform, "Replay Panel", _metersPerUnit, _spacing, _panelColor);
        _timeLabel = WorldUI.CreateLabel(_panel.transform, "", _buttonSize, _fontSize);
        _playPauseLabel = CreateButton(_panel.transform, "Pause", TogglePause);
        _speedLabel = CreateButton(_panel.transform, "", CycleSpeed);
        CreateButton(_panel.transform, "Restart", _sceneTransition.ReloadScene);
        CreateButton(_panel.transform, "Exit to menu", () => _sceneTransition.LoadScene(_mainMenuSceneName));
        CreateButton(_panel.transform, "Close", () => _panel.SetActive(false));
        BuildShare();
    }

    private void BuildShare()
    {
        var buttonSize = Vector2.one * _shareButtonSize;
        var shareButton = WorldUI.CreateIconButton(_panel.transform, "Share", WorldUI.CreateShareIcon(), ShowSharePopup, buttonSize, _buttonColor);
        WorldUI.AttachOutsideRight(shareButton.gameObject, buttonSize, _spacing, 0.0f);

        var popupText = WorldUI.CreateLabel(_panel.transform, _sharePopupText, _sharePopupSize, _fontSize * 0.7f);
        _sharePopup = popupText.transform.parent.gameObject;
        var popupBackground = _sharePopup.AddComponent<Image>();
        popupBackground.color = _panelColor;
        popupBackground.raycastTarget = false;
        WorldUI.AttachOutsideRight(_sharePopup, _sharePopupSize, _spacing, _shareButtonSize + _spacing);
        _sharePopup.SetActive(false);
    }

    // Placeholder until sharing exists
    private void ShowSharePopup()
    {
        _sharePopup.SetActive(true);
        _sharePopupHideTime = Time.unscaledTime + _sharePopupDuration;
    }

    private TextMeshProUGUI CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var button = WorldUI.CreateButton(parent, label, onClick, _buttonSize, _fontSize, _buttonColor);
        return button.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (_toggleAction.WasPressedThisFrame())
        {
            _panel.SetActive(!_panel.activeSelf);
        }

        if (_sharePopup.activeSelf && Time.unscaledTime >= _sharePopupHideTime)
        {
            _sharePopup.SetActive(false);
        }

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
