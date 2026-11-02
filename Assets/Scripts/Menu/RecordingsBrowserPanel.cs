using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecordingsBrowserPanel
{
    private const float TitleHeight = 80.0f;
    private const float ButtonHeight = 100.0f;
    private const float RowHeight = 90.0f;
    private const float ListHeight = 370.0f;
    private const float PopupHeight = 200.0f;
    private const float Spacing = 30.0f;
    private const float FontSize = 48.0f;

    private readonly string _recordingsDirectory;
    private readonly Action<string> _onRecordingChosen;
    private readonly float _width;
    private readonly Color _rowColor;
    private readonly string _importPopupText;
    private readonly float _importPopupDuration;

    private GameObject _panel;
    private RectTransform _list;
    private GameObject _importPopup;
    private float _importPopupHideTime;

    public bool IsOpen => _panel.activeSelf;
    public Transform Transform => _panel.transform;

    public RecordingsBrowserPanel(string recordingsDirectory, Action<string> onRecordingChosen, float width, Color rowColor,
        string importPopupText, float importPopupDuration)
    {
        _recordingsDirectory = recordingsDirectory;
        _onRecordingChosen = onRecordingChosen;
        _width = width;
        _rowColor = rowColor;
        _importPopupText = importPopupText;
        _importPopupDuration = importPopupDuration;
    }

    public void Build(Transform parent, float metersPerUnit, Color panelColor)
    {
        _panel = WorldUI.CreatePanel(parent, "Recordings Browser Panel", metersPerUnit, Spacing, panelColor);

        var title = WorldUI.CreateLabel(_panel.transform, "Replays", new Vector2(_width, TitleHeight), FontSize * 1.2f);
        title.fontStyle = FontStyles.Bold;

        WorldUI.CreateButton(_panel.transform, "Import", ShowImportPopup, new Vector2(_width, ButtonHeight), FontSize, _rowColor);
        _list = WorldUI.CreateScrollView(_panel.transform, new Vector2(_width, ListHeight), Spacing * 0.5f);

        var popupSize = new Vector2(_width, PopupHeight);
        var popupText = WorldUI.CreateLabel(_panel.transform, _importPopupText, popupSize, FontSize);
        _importPopup = popupText.transform.parent.gameObject;
        _importPopup.GetComponent<LayoutElement>().ignoreLayout = true;
        ((RectTransform)_importPopup.transform).sizeDelta = popupSize;
        var popupBackground = _importPopup.AddComponent<Image>();
        popupBackground.color = Color.black;
        popupBackground.raycastTarget = false;
        _importPopup.SetActive(false);

        _panel.SetActive(false);
    }

    public void Open()
    {
        _panel.SetActive(true);
        RefreshList();
    }

    public void Close()
    {
        _importPopup.SetActive(false);
        _panel.SetActive(false);
    }

    public void Tick()
    {
        if (_importPopup.activeSelf && Time.unscaledTime >= _importPopupHideTime)
        {
            _importPopup.SetActive(false);
        }
    }

    private void ShowImportPopup()
    {
        _importPopup.SetActive(true);
        _importPopupHideTime = Time.unscaledTime + _importPopupDuration;
    }

    private void RefreshList()
    {
        foreach (Transform row in _list)
        {
            UnityEngine.Object.Destroy(row.gameObject);
        }
        _list.anchoredPosition = Vector2.zero;

        var recordings = FindRecordings();
        if (recordings.Length == 0)
        {
            WorldUI.CreateLabel(_list, "No replays yet", new Vector2(_width, RowHeight), FontSize * 0.8f);
            return;
        }
        foreach (var recording in recordings)
        {
            var path = recording.FullName;
            var label = recording.LastWriteTime.ToString("yyyy-MM-dd  HH:mm");
            WorldUI.CreateButton(_list, label, () => _onRecordingChosen(path), new Vector2(_width, RowHeight), FontSize * 0.8f, _rowColor);
        }
    }

    private FileInfo[] FindRecordings()
    {
        if (!Directory.Exists(_recordingsDirectory))
        {
            return Array.Empty<FileInfo>();
        }
        return new DirectoryInfo(_recordingsDirectory)
            .GetFiles($"*.{RecordingSessionWriter.Extension}")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
    }
}
