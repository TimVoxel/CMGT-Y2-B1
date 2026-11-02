using UnityEngine;

// Keeps the curved buttons centred on the player's head. Rotate this object in the scene to aim the menu.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private Transform _head;
    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private string _replaySceneName = "ReplayScene";
    [SerializeField] private GameObject _startButtons;
    [SerializeField] private CurvedMenuButton _playButton;
    [SerializeField] private CurvedMenuButton _replayButton;
    [SerializeField] private CurvedMenuButton _settingsButton;
    [SerializeField] private CurvedMenuButton _exitButton;
    [SerializeField] private GameObject _settingsWindow;

    private void OnEnable()
    {
        _playButton.Pressed += Play;
        _replayButton.Pressed += Replay;
        _settingsButton.Pressed += OpenSettings;
        _exitButton.Pressed += Exit;
        _concertSet.SetStarted += OnSetStarted;
    }

    private void OnDisable()
    {
        _playButton.Pressed -= Play;
        _replayButton.Pressed -= Replay;
        _settingsButton.Pressed -= OpenSettings;
        _exitButton.Pressed -= Exit;
        _concertSet.SetStarted -= OnSetStarted;
    }

    private void LateUpdate()
    {
        transform.position = _head.position;
    }

    private void Play()
    {
        _concertSet.StartSet();
    }

    private void OnSetStarted()
    {
        _startButtons.SetActive(false);
        _settingsWindow.SetActive(false);
    }

    private void Replay()
    {
        _sceneTransition.LoadScene(_replaySceneName);
    }

    private void OpenSettings()
    {
        _settingsWindow.SetActive(!_settingsWindow.activeSelf);
    }

    private void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _head = Camera.main != null ? Camera.main.transform : null;
        _concertSet = FindFirstObjectByType<ConcertSet>();
        _sceneTransition = FindFirstObjectByType<SceneTransition>();
    }
#endif
}
