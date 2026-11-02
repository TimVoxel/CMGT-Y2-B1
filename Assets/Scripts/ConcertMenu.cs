using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The show's world-space menus: fake loading, start, and the end screen with comments. Follows ConcertSet's events.
// Builds its panels as children, so place this object where they should appear.
public class ConcertMenu : MonoBehaviour
{
    [SerializeField] private ConcertSet _concertSet;
    [SerializeField] private ConcertRevenue _revenue;
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private GamePause _pause;
    [SerializeField] private bool _showStartPanel = true;
    [SerializeField] private bool _showPausePanel = true;

    [SerializeField] private AgentLoadingScript _loadingScript;
    [SerializeField] private GigComments _gigComments;

    [Header("Layout")]
    [SerializeField] private Vector2 _buttonSize = new Vector2(800.0f, 140.0f);
    [SerializeField] private float _spacing = 50.0f;
    [SerializeField] private float _metersPerUnit = 0.001f;
    [SerializeField] private float _fontSize = 64.0f;
    [SerializeField] private Color _panelColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    [SerializeField] private Color _buttonColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);
    [SerializeField] private float _endPanelRaise = 0.3f;

    [Header("Comments")]
    [SerializeField] private Vector2 _commentSize = new Vector2(800.0f, 110.0f);
    [SerializeField] private float _commentFontScale = 0.6f;
    [SerializeField] private float _commentsViewHeight = 480.0f;
    [SerializeField] private float _commentsPanelGap = 50.0f;
    [SerializeField] private float _commentsPanelYaw = 15.0f;

    [Header("Demo lock")]
    [SerializeField] private string _commentsLockedPopupText = "For the purposes of the demo, please read all comments before proceeding with these options";
    [SerializeField] private Vector2 _commentsLockedPopupSize = new Vector2(800.0f, 320.0f);
    [SerializeField] private float _commentsLockedPopupDuration = 4.0f;
    [SerializeField] private Color _commentsLockedPopupColor = new Color(0.0f, 0.0f, 0.0f, 0.95f);
    [SerializeField] private Color _lockedButtonColor = new Color(0.1f, 0.1f, 0.12f, 1.0f);
    [SerializeField] private float _lockedButtonTextAlpha = 0.4f;
    [SerializeField] private float _scrolledToBottomTolerance = 5.0f;

    [Header("Start")]
    [SerializeField] private KeySweepDetector _keySweep;
    [SerializeField] private string _startTip = "Poke all keys left to right to start";
    [SerializeField] private string _heightHint = "Double-press [A] on right controller to adjust height";

    [Header("Debug")]
    [SerializeField] private DebugSkipInput _debugSkip;

    private GameObject _startPanel;
    private TextMeshProUGUI _startTipLabel;
    private GameObject _endPanel;
    private GameObject _pausePanel;
    private readonly List<GameObject> _hiddenWhilePaused = new List<GameObject>();
    private Button _revenueButton;
    private TextMeshProUGUI _revenueText;
    private Button _watchReplayButton;
    private Button _exitToMenuButton;
    private GameObject _commentsLockedPopup;
    private float _commentsLockedPopupHideTime;
    private bool _hasReadAllComments;
    private RectTransform _commentList;
    private Button _moreCommentsButton;
    private List<GigComment> _comments;
    private int _commentsLoaded;
    private AgentLoadingScreen _loadingScreen;
    private Coroutine _loading;

    private float CommentFontSize => _fontSize * _commentFontScale;

    private void OnEnable()
    {
        _concertSet.SetStarted += OnSetStarted;
        _concertSet.SetEnded += OnSetEnded;
        if (_pause != null)
        {
            _pause.Paused += OnPaused;
            _pause.Resumed += OnResumed;
        }
        if (_debugSkip != null)
        {
            _debugSkip.SkipPressed += OnDebugSkip;
        }
        if (_keySweep != null)
        {
            _keySweep.ProgressChanged += OnKeySweepProgress;
            _keySweep.Completed += _concertSet.StartSet;
        }
    }

    private void OnDisable()
    {
        _concertSet.SetStarted -= OnSetStarted;
        _concertSet.SetEnded -= OnSetEnded;
        if (_pause != null)
        {
            _pause.Paused -= OnPaused;
            _pause.Resumed -= OnResumed;
        }
        if (_debugSkip != null)
        {
            _debugSkip.SkipPressed -= OnDebugSkip;
        }
        if (_keySweep != null)
        {
            _keySweep.ProgressChanged -= OnKeySweepProgress;
            _keySweep.Completed -= _concertSet.StartSet;
        }
    }

    private void Start()
    {
        BuildStartPanel();
        BuildEndPanel();
        BuildPausePanel();
        _endPanel.SetActive(false);
        _pausePanel.SetActive(false);
        _startPanel.SetActive(false);
        if (_loadingScript != null)
        {
            _loadingScreen = new AgentLoadingScreen(_loadingScript);
            _loadingScreen.Build(transform, _metersPerUnit, _spacing, _panelColor, _fontSize);
        }
    }

    private void Update()
    {
        if (_commentsLockedPopup.activeSelf && Time.unscaledTime >= _commentsLockedPopupHideTime)
        {
            _commentsLockedPopup.SetActive(false);
        }

        if (!_hasReadAllComments && _endPanel.activeSelf && HasReachedLastComment())
        {
            _hasReadAllComments = true;
            _commentsLockedPopup.SetActive(false);
            SetButtonLocked(_watchReplayButton, false);
            SetButtonLocked(_exitToMenuButton, false);
        }
    }

    public void BeginLoading()
    {
        if (_loading != null || _concertSet.HasStarted)
        {
            return;
        }
        if (_loadingScreen != null)
        {
            _loading = StartCoroutine(LoadShow());
        }
        else
        {
            EndLoading();
        }
    }

    // Skips the fake loading, then on the start screen stands in for poking the keys
    private void OnDebugSkip()
    {
        if (_pause != null && _pause.IsPaused)
        {
            return;
        }
        if (_loading != null)
        {
            StopCoroutine(_loading);
            Debug.Log("[Debug] Skipped loading");
            EndLoading();
        }
        else if (!_concertSet.HasStarted)
        {
            Debug.Log("[Debug] Skipped the start screen");
            _concertSet.StartSet();
        }
    }

    private void ShowStartPanel()
    {
        _startPanel.SetActive(true);
        _startPanel.SetActive(_showStartPanel);
        if (_keySweep != null)
        {
            _keySweep.StartListening();
        }
    }

    private void OnKeySweepProgress(int poked, int total)
    {
        _startTipLabel.text = poked > 0 ? $"{_startTip}\n<size=70%>{poked} / {total}</size>" : _startTip;
    }

    private void OnSetStarted()
    {
        _audience.GatherStartingCrowd(1.0f);
        _startPanel.SetActive(false);
        if (_keySweep != null)
        {
            _keySweep.StopListening();
        }
    }

    private void OnSetEnded()
    {
        if (_loading != null)
        {
            StopCoroutine(_loading);
            _loading = null;
            _loadingScreen.Hide();
        }
        _endPanel.SetActive(true);
        if (_commentList != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_commentList);
        }
    }

    private void OnPaused()
    {
        _hiddenWhilePaused.Clear();
        foreach (var panel in new[] { _startPanel, _endPanel, _loadingScreen?.Panel })
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
                _hiddenWhilePaused.Add(panel);
            }
        }
        _pausePanel.SetActive(true);
        _pausePanel.SetActive(_showPausePanel);
    }

    private void OnResumed()
    {
        _pausePanel.SetActive(false);
        foreach (var panel in _hiddenWhilePaused)
        {
            panel.SetActive(true);
        }
        _hiddenWhilePaused.Clear();
    }

    private void BuildPausePanel()
    {
        _pausePanel = WorldUI.CreatePanel(transform, "Pause Panel", _metersPerUnit, _spacing, _panelColor);
        WorldUI.CreateLabel(_pausePanel.transform, "Paused", _buttonSize, _fontSize);
        if (_pause != null)
        {
            WorldUI.CreateButton(_pausePanel.transform, "Resume", _pause.Resume, _buttonSize, _fontSize, _buttonColor);
        }
        WorldUI.CreateButton(_pausePanel.transform, "Restart", _concertSet.Restart, _buttonSize, _fontSize, _buttonColor);
    }

    private IEnumerator LoadShow()
    {
        yield return _loadingScreen.Play(_audience.GatherStartingCrowd);
        EndLoading();
    }

    private void EndLoading()
    {
        _loading = null;
        _loadingScreen?.Hide();
        if (_concertSet.HasStarted)
        {
            return;
        }
        if (_showStartPanel)
        {
            ShowStartPanel();
        }
        else
        {
            _concertSet.StartSet();
        }
    }

    private void BuildStartPanel()
    {
        _startPanel = WorldUI.CreatePanel(transform, "Start Panel", _metersPerUnit, _spacing, _panelColor);
        if (_keySweep != null)
        {
            _startTipLabel = WorldUI.CreateLabel(_startPanel.transform, _startTip, new Vector2(_buttonSize.x, _fontSize * 2.8f), _fontSize);
            var hintFontSize = _fontSize * 0.6f;
            var hint = WorldUI.CreateLabel(_startPanel.transform, _heightHint, new Vector2(_buttonSize.x, hintFontSize * 2.6f), hintFontSize);
            hint.color = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        }
        if (_loadingScript != null)
        {
            var creditFontSize = _fontSize * 0.5f;
            var agentColor = ColorUtility.ToHtmlStringRGB(_loadingScript.AgentNameColor);
            WorldUI.CreateLabel(_startPanel.transform, $"brought to you by <b><color=#{agentColor}>{_loadingScript.AgentName}</color></b>",
                new Vector2(_buttonSize.x, creditFontSize * 1.6f), creditFontSize);
        }
        else
        {
            WorldUI.CreateButton(_startPanel.transform, "Start", _concertSet.StartSet, _buttonSize, _fontSize, _buttonColor);
        }
    }

    private void BuildEndPanel()
    {
        _endPanel = WorldUI.CreatePanel(transform, "End Panel", _metersPerUnit, _spacing, _panelColor);
        _endPanel.transform.localPosition = Vector3.up * _endPanelRaise;
        WorldUI.CreateLabel(_endPanel.transform, "That's the show!", _buttonSize, _fontSize);
        if (_concertSet.CanRecord)
        {
            _watchReplayButton = WorldUI.CreateButton(_endPanel.transform, "Watch replay", WatchReplay, _buttonSize, _fontSize, _buttonColor);
        }
        _revenueButton = WorldUI.CreateButton(_endPanel.transform, "What would I have earnt?", ShowRevenue, _buttonSize, _fontSize, _buttonColor);
        _revenueText = _revenueButton.GetComponentInChildren<TextMeshProUGUI>();
        _exitToMenuButton = WorldUI.CreateButton(_endPanel.transform, "Exit to menu", ExitToMenu, _buttonSize, _fontSize, _buttonColor);
        if (_gigComments != null)
        {
            BuildComments();
        }
        BuildCommentsLockedPopup();

        _hasReadAllComments = _gigComments == null;
        SetButtonLocked(_watchReplayButton, !_hasReadAllComments);
        SetButtonLocked(_exitToMenuButton, !_hasReadAllComments);
    }

    private void BuildCommentsLockedPopup()
    {
        var popupText = WorldUI.CreateLabel(_endPanel.transform, _commentsLockedPopupText, _commentsLockedPopupSize, _fontSize * 0.7f);
        _commentsLockedPopup = popupText.transform.parent.gameObject;
        _commentsLockedPopup.GetComponent<LayoutElement>().ignoreLayout = true;
        ((RectTransform)_commentsLockedPopup.transform).sizeDelta = _commentsLockedPopupSize;
        var background = _commentsLockedPopup.AddComponent<Image>();
        background.color = _commentsLockedPopupColor;
        background.raycastTarget = false;
        _commentsLockedPopup.SetActive(false);
    }

    private void ShowCommentsLockedPopup()
    {
        _commentsLockedPopup.SetActive(true);
        _commentsLockedPopupHideTime = Time.unscaledTime + _commentsLockedPopupDuration;
    }

    private void SetButtonLocked(Button button, bool isLocked)
    {
        if (button == null)
        {
            return;
        }
        button.image.color = isLocked ? _lockedButtonColor : _buttonColor;
        button.GetComponentInChildren<TextMeshProUGUI>(true).alpha = isLocked ? _lockedButtonTextAlpha : 1.0f;
    }

    private bool HasReachedLastComment()
    {
        if (_commentsLoaded < _comments.Count)
        {
            return false;
        }
        var hiddenBelowView = _commentList.rect.height - _commentList.anchoredPosition.y - _commentsViewHeight;
        return hiddenBelowView <= _scrolledToBottomTolerance;
    }

    private void BuildComments()
    {
        var panel = WorldUI.CreatePanel(_endPanel.transform, "Comments Panel", 1.0f, _spacing, _panelColor);
        panel.AddComponent<LayoutElement>().ignoreLayout = true;
        var offset = (_buttonSize.x + _commentSize.x) * 0.5f + 2.0f * _spacing + _commentsPanelGap;
        panel.transform.SetLocalPositionAndRotation(
            new Vector3(offset, 0.0f, 0.0f),
            Quaternion.Euler(0.0f, _commentsPanelYaw, 0.0f));

        var heading = WorldUI.CreateLabel(panel.transform, "Comments", new Vector2(_commentSize.x, _fontSize), _fontSize * 0.8f);
        heading.alignment = TextAlignmentOptions.Left;
        heading.fontStyle = FontStyles.Bold;

        _commentList = WorldUI.CreateScrollView(panel.transform, new Vector2(_commentSize.x, _commentsViewHeight), _spacing * 0.5f);
        var moreButtonSize = new Vector2(_commentSize.x, _commentSize.y * 0.6f);
        _moreCommentsButton = WorldUI.CreateButton(_commentList, "More", LoadMoreComments, moreButtonSize, CommentFontSize, _buttonColor);

        _comments = _gigComments.GenerateComments();
        _commentsLoaded = 0;
        LoadMoreComments();
    }

    private void LoadMoreComments()
    {
        var end = Mathf.Min(_commentsLoaded + _gigComments.CommentsPerPage, _comments.Count);
        for (; _commentsLoaded < end; _commentsLoaded++)
        {
            CreateComment(_comments[_commentsLoaded]);
        }

        _moreCommentsButton.transform.SetAsLastSibling();
        _moreCommentsButton.gameObject.SetActive(_commentsLoaded < _comments.Count);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_commentList);
    }

    private void CreateComment(GigComment comment)
    {
        var label = WorldUI.CreateLabel(_commentList, "Comment", _commentSize, CommentFontSize);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableAutoSizing = true;
        label.fontSizeMax = CommentFontSize;
        label.fontSizeMin = CommentFontSize * 0.6f;
        var usernameColor = ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(Random.value, 0.45f, 1.0f));
        label.text = $"<b><color=#{usernameColor}>{comment.Username}</color></b>  " +
                     $"<size=80%><color=#AAAAAA>{FormatLikes(comment.Likes)}</color></size>\n{comment.Text}";
    }

    private static string FormatLikes(int likes)
    {
        var count = likes >= 1000 ? $"{likes / 1000.0f:0.#}K" : likes.ToString();
        return likes == 1 ? "1 like" : $"{count} likes";
    }

    private void ExitToMenu()
    {
        if (!_hasReadAllComments)
        {
            ShowCommentsLockedPopup();
            return;
        }
        _concertSet.Restart();
    }

    private void WatchReplay()
    {
        if (!_hasReadAllComments)
        {
            ShowCommentsLockedPopup();
            return;
        }
        _watchReplayButton.interactable = false;
        _concertSet.WatchReplay();
    }

    private void ShowRevenue()
    {
        _revenueText.text = $"${_revenue.Revenue:N0} ({_revenue.TicketsSold} tickets)";
        _revenueButton.interactable = false;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _concertSet = FindFirstObjectByType<ConcertSet>();
        _revenue = FindFirstObjectByType<ConcertRevenue>();
        _audience = FindFirstObjectByType<AudienceSpawner>();
        _pause = FindFirstObjectByType<GamePause>();
        _debugSkip = FindFirstObjectByType<DebugSkipInput>();
        _keySweep = FindFirstObjectByType<KeySweepDetector>();
    }
#endif
}
