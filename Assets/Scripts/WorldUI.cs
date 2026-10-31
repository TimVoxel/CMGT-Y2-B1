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

    public static RectTransform CreateScrollView(Transform parent, Vector2 size, float spacing)
    {
        var viewObject = new GameObject("Scroll View", typeof(RectTransform));
        viewObject.transform.SetParent(parent, false);
        AddLayoutSize(viewObject, size);
        viewObject.AddComponent<Image>().color = Color.clear;
        viewObject.AddComponent<RectMask2D>();

        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewObject.transform, false);
        var content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0.0f, 1.0f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1.0f);
        content.sizeDelta = Vector2.zero;

        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = viewObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.viewport = (RectTransform)viewObject.transform;
        scrollRect.content = content;
        return content;
    }

    public static RectTransform CreateProgressBar(Transform parent, Vector2 size, Color backgroundColor, Color fillColor)
    {
        var barObject = new GameObject("Progress Bar", typeof(RectTransform));
        barObject.transform.SetParent(parent, false);
        var background = barObject.AddComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;
        AddLayoutSize(barObject, size);

        var fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(barObject.transform, false);
        var fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.0f, 1.0f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fill = fillObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        return fillRect;
    }

    public static Button CreateIconButton(Transform parent, string name, Sprite icon, UnityAction onClick, Vector2 size, Color color)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        var background = buttonObject.AddComponent<Image>();
        background.color = color;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);
        AddLayoutSize(buttonObject, size);

        var iconObject = new GameObject("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(buttonObject.transform, false);
        var iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = new Vector2(0.2f, 0.2f);
        iconRect.anchorMax = new Vector2(0.8f, 0.8f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var iconImage = iconObject.AddComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        return button;
    }

    public static void AttachOutsideRight(GameObject target, Vector2 size, float gap, float offsetFromTop)
    {
        var element = target.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = target.AddComponent<LayoutElement>();
        }
        element.ignoreLayout = true;

        var rect = (RectTransform)target.transform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.0f, 1.0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(gap, -offsetFromTop);
    }

    public static Sprite CreateShareIcon(int resolution = 128)
    {
        var top = new Vector2(0.72f, 0.80f);
        var middle = new Vector2(0.28f, 0.50f);
        var bottom = new Vector2(0.72f, 0.20f);
        const float dotRadius = 0.13f;
        const float lineHalfWidth = 0.04f;

        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        var pixels = new Color32[resolution * resolution];
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var point = new Vector2((x + 0.5f) / resolution, (y + 0.5f) / resolution);
                var distance = Mathf.Min(
                    Mathf.Min(Vector2.Distance(point, top), Vector2.Distance(point, middle), Vector2.Distance(point, bottom)) - dotRadius,
                    Mathf.Min(DistanceToSegment(point, middle, top), DistanceToSegment(point, middle, bottom)) - lineHalfWidth);
                var alpha = Mathf.Clamp01(0.5f - distance * resolution);
                pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255.0f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, resolution, resolution), new Vector2(0.5f, 0.5f));
        sprite.name = "Share Icon";
        return sprite;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
        return Vector2.Distance(point, start + segment * t);
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
