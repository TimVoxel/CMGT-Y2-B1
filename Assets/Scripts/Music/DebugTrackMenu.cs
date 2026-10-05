using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// World-space debug panel: a Restart button plus one button per track.
// Builds its own canvas at runtime, so just drop it on an empty GameObject and place that where you want the panel.
public class DebugTrackMenu : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private Track[] _tracks;

    // In canvas units; the canvas is scaled by _metersPerUnit
    [SerializeField] private Vector2 _buttonSize = new Vector2(400.0f, 70.0f);
    [SerializeField] private float _spacing = 10.0f;
    [SerializeField] private float _metersPerUnit = 0.001f;
    [SerializeField] private float _fontSize = 32.0f;

    [SerializeField] private Color _panelColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    [SerializeField] private Color _buttonColor = new Color(0.2f, 0.2f, 0.25f, 1.0f);
    [SerializeField] private Color _currentTrackColor = new Color(0.15f, 0.45f, 0.25f, 1.0f);

    private readonly List<(Track track, Image image)> _trackButtons = new List<(Track, Image)>();

    private void Start()
    {
        BuildUI();
        RefreshHighlight();
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("Debug Track Menu Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localScale = Vector3.one * _metersPerUnit;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        // Lets the XR ray / poke interactors click the buttons
        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        canvasObject.AddComponent<Image>().color = _panelColor;

        var layout = canvasObject.AddComponent<VerticalLayoutGroup>();
        var padding = Mathf.RoundToInt(_spacing);
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = _spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = canvasObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateButton(canvasObject.transform, "Restart", () => _trackPlayer.Play());

        foreach (var track in _tracks)
        {
            if (track == null)
            {
                continue;
            }

            var label = track.Clip != null ? track.Clip.name : track.name;
            var image = CreateButton(canvasObject.transform, label, () =>
            {
                _trackPlayer.PlayTrack(track);
                RefreshHighlight();
            });
            _trackButtons.Add((track, image));
        }
    }

    private Image CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.AddComponent<Image>();
        image.color = _buttonColor;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var element = buttonObject.AddComponent<LayoutElement>();
        element.preferredWidth = _buttonSize.x;
        element.preferredHeight = _buttonSize.y;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = _fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return image;
    }

    private void RefreshHighlight()
    {
        foreach (var (track, image) in _trackButtons)
        {
            image.color = track == _trackPlayer.Track ? _currentTrackColor : _buttonColor;
        }
    }

#if UNITY_EDITOR
    // Fills in the player and every Track asset in the project when the component is first added
    private void Reset()
    {
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();

        var tracks = new List<Track>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Track"))
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var track = UnityEditor.AssetDatabase.LoadAssetAtPath<Track>(path);
            if (track != null)
            {
                tracks.Add(track);
            }
        }
        _tracks = tracks.ToArray();
    }
#endif
}
