using UnityEngine;

// Keeps the curved buttons centred on the player's head. Rotate this object in the scene to aim the menu.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private Transform _head;
    [SerializeField] private float _heightAboveHead = 0.2f;
    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private ConcertMenu _concertMenu;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private string _replaySceneName = "ReplayScene";
    [SerializeField] private GameObject _startButtons;
    [SerializeField] private CurvedMenuButton _playButton;
    [SerializeField] private CurvedMenuButton _replayButton;
    [SerializeField] private CurvedMenuButton _settingsButton;
    [SerializeField] private CurvedMenuButton _exitButton;
    [SerializeField] private GameObject _settingsWindow;

    [Header("Recordings browser")]
    [SerializeField] private float _browserRadius = 1.95f;
    [SerializeField] private float _browserAngle = 41.0f;
    [SerializeField] private float _browserVerticalOffset = -0.1f;
    [SerializeField] private float _browserMetersPerUnit = 0.0015f;
    [SerializeField] private float _browserWidth = 640.0f;
    [SerializeField] private Color _browserColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);
    [SerializeField] private Color _browserRowColor = new Color(0.32f, 0.32f, 0.4f, 1.0f);
    [SerializeField] private string _importPopupText = "A file dialog would be here";
    [SerializeField] private float _importPopupDuration = 3.0f;

    private RecordingsBrowserPanel _recordingsBrowser;

    private void Start()
    {
        _recordingsBrowser = new RecordingsBrowserPanel(_concertSet.RecordingsDirectory, PlayRecording, _browserWidth, _browserRowColor,
            _importPopupText, _importPopupDuration);
        _recordingsBrowser.Build(_startButtons.transform, _browserMetersPerUnit, _browserColor);

        var browserRotation = Quaternion.Euler(0.0f, _browserAngle, 0.0f);
        var browserPosition = browserRotation * Vector3.forward * _browserRadius + Vector3.up * _browserVerticalOffset;
        _recordingsBrowser.Transform.SetLocalPositionAndRotation(browserPosition, browserRotation);
    }

    private void Update()
    {
        _recordingsBrowser.Tick();
    }

    private void OnEnable()
    {
        _playButton.Pressed += Play;
        _replayButton.Pressed += ToggleRecordingsBrowser;
        _settingsButton.Pressed += OpenSettings;
        _exitButton.Pressed += Exit;
        _concertSet.SetStarted += OnSetStarted;
    }

    private void OnDisable()
    {
        _playButton.Pressed -= Play;
        _replayButton.Pressed -= ToggleRecordingsBrowser;
        _settingsButton.Pressed -= OpenSettings;
        _exitButton.Pressed -= Exit;
        _concertSet.SetStarted -= OnSetStarted;
    }

    private void LateUpdate()
    {
        transform.position = _head.position + Vector3.up * _heightAboveHead;
    }

    private void Play()
    {
        HideMenu();
        _concertMenu.BeginLoading();
    }

    private void OnSetStarted()
    {
        HideMenu();
    }

    private void HideMenu()
    {
        _startButtons.SetActive(false);
        _settingsWindow.SetActive(false);
    }

    private void ToggleRecordingsBrowser()
    {
        if (_recordingsBrowser.IsOpen)
        {
            _recordingsBrowser.Close();
            return;
        }
        _settingsWindow.SetActive(false);
        _recordingsBrowser.Open();
    }

    private void PlayRecording(string path)
    {
        ReplayEventDispatcher.RequestedRecordingPath = path;
        _sceneTransition.LoadScene(_replaySceneName);
    }

    private void OpenSettings()
    {
        _recordingsBrowser.Close();
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
        _concertMenu = FindFirstObjectByType<ConcertMenu>();
        _sceneTransition = FindFirstObjectByType<SceneTransition>();
    }
#endif
}
