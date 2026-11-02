using UnityEngine;

// Shows the curved pause buttons while GamePause has the game paused. The pausing itself is GamePause's job.
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GamePause _pause;
    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private GameObject _startButtons;
    [SerializeField] private GameObject _pauseButtons;
    [SerializeField] private GameObject _settingsWindow;
    [SerializeField] private CurvedMenuButton _resumeButton;
    [SerializeField] private CurvedMenuButton _settingsButton;
    [SerializeField] private CurvedMenuButton _exitToMainMenuButton;

    private bool _wereStartButtonsShown;
    private bool _wasSettingsWindowShown;

    private void OnEnable()
    {
        _pause.Paused += OnPaused;
        _pause.Resumed += OnResumed;
        _resumeButton.Pressed += _pause.Resume;
        _settingsButton.Pressed += ToggleSettings;
        _exitToMainMenuButton.Pressed += _concertSet.Restart;
    }

    private void OnDisable()
    {
        _pause.Paused -= OnPaused;
        _pause.Resumed -= OnResumed;
        _resumeButton.Pressed -= _pause.Resume;
        _settingsButton.Pressed -= ToggleSettings;
        _exitToMainMenuButton.Pressed -= _concertSet.Restart;
    }

    private void OnPaused()
    {
        _wereStartButtonsShown = _startButtons.activeSelf;
        _wasSettingsWindowShown = _settingsWindow.activeSelf;
        _startButtons.SetActive(false);
        _settingsWindow.SetActive(false);
        _pauseButtons.SetActive(true);
    }

    private void OnResumed()
    {
        _pauseButtons.SetActive(false);
        _settingsWindow.SetActive(_wasSettingsWindowShown);
        _startButtons.SetActive(_wereStartButtonsShown);
    }

    private void ToggleSettings()
    {
        _settingsWindow.SetActive(!_settingsWindow.activeSelf);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _pause = FindFirstObjectByType<GamePause>();
        _concertSet = FindFirstObjectByType<ConcertSet>();
    }
#endif
}
