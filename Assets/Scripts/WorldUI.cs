using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Builds simple world-space panels at runtime: a backdrop that stacks its children vertically and sizes itself to fit.
public static class WorldUI
{
    public static GameObject CreatePanel(Transform parent, string name, float metersPerUnit, float spacing, Color color)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localScale = Vector3.one * metersPerUnit;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        canvasObject.AddComponent<Image>().color = color;

        var layout = canvasObject.AddComponent<VerticalLayoutGroup>();
        var padding = Mathf.RoundToInt(spacing);
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = canvasObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return canvasObject;
    }

    public static Button CreateButton(Transform parent, string label, UnityAction onClick, Vector2 size, float fontSize, Color color)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.AddComponent<Image>();
        image.color = color;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        AddLayoutSize(buttonObject, size);
        CreateText(buttonObject.transform, label, fontSize);
        return button;
    }

    public static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 size, float fontSize)
    {
        var labelObject = new GameObject(text, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        AddLayoutSize(labelObject, size);
        return CreateText(labelObject.transform, text, fontSize);
    }

    private static void AddLayoutSize(GameObject target, Vector2 size)
    {
        var element = target.AddComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize)
    {
        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
}
