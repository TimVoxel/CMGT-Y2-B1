using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Runs the show: start screen, every song in the set list with applause in between, then the end screen.
// Builds its own world-space panels like DebugTrackMenu, so place this object where the panel should appear.
public class ConcertSet : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdCheer _cheer;
    [SerializeField] private ConcertRevenue _revenue;
    [SerializeField] private SceneTransition _sceneTransition;
    [SerializeField] private Track[] _setList;

    [SerializeField] private float _applauseDuration = 5.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _engagementKept = 0.5f;

    [Header("UI")]
    [SerializeField] private Vector2 _buttonSize = new Vector2(800.0f, 140.0f);
    [SerializeField] private float _spacing = 50.0f;
    [SerializeField] private float _metersPerUnit = 0.001f;
    [SerializeField] private float _fontSize = 64.0f;
    [SerializeField] private Color _panelColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    [SerializeField] private Color _buttonColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);

    [Header("Debug (editor and development builds only)")]
    [SerializeField] private Key _skipSongKey = Key.F2;
    [SerializeField] private Vector2 _skipEngagementRange = new Vector2(0.1f, 0.6f);

    private GameObject _startPanel;
    private GameObject _endPanel;
    private Button _revenueButton;
    private TextMeshProUGUI _revenueText;
    private int _songIndex = -1;
    private Coroutine _applause;

    private void OnEnable()
    {
        _trackPlayer.Finished += OnSongFinished;
    }

    private void OnDisable()
    {
        _trackPlayer.Finished -= OnSongFinished;
    }

    private void Start()
    {
        BuildStartPanel();
        BuildEndPanel();
        _endPanel.SetActive(false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && _skipSongKey != Key.None && keyboard[_skipSongKey].wasPressedThisFrame)
        {
            DebugSkipSong();
        }
    }

    private void DebugSkipSong()
    {
        if (_songIndex < 0 || _endPanel.activeSelf)
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
#endif

    private void BuildStartPanel()
    {
        _startPanel = WorldUI.CreatePanel(transform, "Start Panel", _metersPerUnit, _spacing, _panelColor);
        WorldUI.CreateLabel(_startPanel.transform, "Ready to play?", _buttonSize, _fontSize);
        WorldUI.CreateButton(_startPanel.transform, "Start", StartSet, _buttonSize, _fontSize, _buttonColor);
    }

    private void BuildEndPanel()
    {
        _endPanel = WorldUI.CreatePanel(transform, "End Panel", _metersPerUnit, _spacing, _panelColor);
        WorldUI.CreateLabel(_endPanel.transform, "That's the show!", _buttonSize, _fontSize);
        _revenueButton = WorldUI.CreateButton(_endPanel.transform, "Show revenue", ShowRevenue, _buttonSize, _fontSize, _buttonColor);
        _revenueText = _revenueButton.GetComponentInChildren<TextMeshProUGUI>();
        WorldUI.CreateButton(_endPanel.transform, "Restart", _sceneTransition.ReloadScene, _buttonSize, _fontSize, _buttonColor);
    }

    public void StartSet()
    {
        if (_songIndex >= 0 || _setList.Length == 0)
        {
            return;
        }

        _startPanel.SetActive(false);
        _engagement.ResetEngagement();
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
        if (_songIndex + 1 < _setList.Length)
        {
            _engagement.Set(_engagement.Engagement * _engagementKept);
            PlaySong(_songIndex + 1);
        }
        else
        {
            _endPanel.SetActive(true);
        }
    }

    private void ShowRevenue()
    {
        _revenueText.text = $"${_revenue.Revenue:N0} ({_revenue.TicketsSold} tickets)";
        _revenueButton.interactable = false;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _cheer = FindFirstObjectByType<CrowdCheer>();
        _revenue = FindFirstObjectByType<ConcertRevenue>();
        _sceneTransition = FindFirstObjectByType<SceneTransition>();
    }
#endif
}
