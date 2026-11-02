using System;
using System.Collections;
using TMPro;
using UnityEngine;

// A fake AI agent "preparing" the show: a chat-style message with a progress bar that stalls and bursts like a real download.
public class AgentLoadingScreen
{
    private readonly AgentLoadingScript _script;
    private GameObject _panel;
    private TextMeshProUGUI _message;
    private RectTransform _barFill;

    public GameObject Panel => _panel;

    public AgentLoadingScreen(AgentLoadingScript script)
    {
        _script = script;
    }

    public void Build(Transform parent, float metersPerUnit, float spacing, Color panelColor, float fontSize)
    {
        _panel = WorldUI.CreatePanel(parent, "Agent Loading Panel", metersPerUnit, spacing, panelColor);

        var width = _script.Width;
        var sender = WorldUI.CreateLabel(_panel.transform, _script.AgentName, new Vector2(width, fontSize), fontSize * 0.8f);
        sender.alignment = TextAlignmentOptions.Left;
        sender.fontStyle = FontStyles.Bold;
        sender.color = _script.AgentNameColor;

        _message = WorldUI.CreateLabel(_panel.transform, "", new Vector2(width, fontSize * 2.5f), fontSize);
        _message.alignment = TextAlignmentOptions.TopLeft;

        _barFill = WorldUI.CreateProgressBar(_panel.transform, new Vector2(width, _script.BarHeight),
            _script.BarBackgroundColor, _script.AgentNameColor);

        _panel.SetActive(false);
    }

    public IEnumerator Play(Action<float> onAudienceProgress)
    {
        _panel.SetActive(true);
        foreach (var step in _script.Steps)
        {
            yield return Load(step);
        }
        yield return LoadSteadily(_script.AudienceStep, _script.AudienceStepDuration, onAudienceProgress);
    }

    private IEnumerator LoadSteadily(string step, float duration, Action<float> onProgress)
    {
        var progress = 0.0f;
        while (progress < 1.0f)
        {
            progress = Mathf.MoveTowards(progress, 1.0f, Time.deltaTime / Mathf.Max(1e-4f, duration));
            Show(step, progress);
            onProgress(progress);
            yield return null;
        }
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }

    private IEnumerator Load(string step)
    {
        var progress = 0.0f;
        Show(step, progress);
        while (progress < 1.0f)
        {
            if (UnityEngine.Random.value < _script.StallChance)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(_script.StallDurationRange.x, _script.StallDurationRange.y));
            }

            var target = Mathf.Min(1.0f, progress + UnityEngine.Random.Range(_script.BurstSizeRange.x, _script.BurstSizeRange.y));
            var speed = UnityEngine.Random.Range(_script.BurstSpeedRange.x, _script.BurstSpeedRange.y);
            while (progress < target)
            {
                progress = Mathf.MoveTowards(progress, target, speed * Time.deltaTime);
                Show(step, progress);
                yield return null;
            }
        }
    }

    private void Show(string step, float progress)
    {
        _message.text = $"{step}\n{Mathf.FloorToInt(progress * 100.0f)}%";
        _barFill.anchorMax = new Vector2(progress, 1.0f);
    }
}
