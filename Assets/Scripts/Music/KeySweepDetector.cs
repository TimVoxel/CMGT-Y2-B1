using System;
using System.Linq;
using UnityEngine;

// Watches for the player poking every xylophone key in turn, from their left to their right.
// A wrong key starts over (or counts as the first poke if it's the leftmost); poking the same key twice is ignored.
public class KeySweepDetector : MonoBehaviour
{
    private LaneHitZone[] _keys = Array.Empty<LaneHitZone>();
    private int _progress;
    private bool _isListening;

    public int KeyCount => _keys.Length;
    public event Action<int, int> ProgressChanged;
    public event Action PokeDidNotAdvance;
    public event Action Completed;

    private void Awake()
    {
        _keys = FindObjectsByType<LaneHitZone>(FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        foreach (var key in _keys)
        {
            key.Struck += OnKeyStruck;
        }
    }

    private void OnDisable()
    {
        foreach (var key in _keys)
        {
            key.Struck -= OnKeyStruck;
        }
    }

    public void StartListening()
    {
        var viewer = Camera.main;
        var right = viewer != null ? viewer.transform.right : Vector3.right;
        _keys = _keys.OrderBy(key => Vector3.Dot(key.transform.position, right)).ToArray();
        _isListening = true;
        SetProgress(0);
    }

    public void StopListening()
    {
        _isListening = false;
    }

    private void OnKeyStruck(LaneHitZone key)
    {
        if (!_isListening)
        {
            return;
        }

        var index = Array.IndexOf(_keys, key);
        if (index == _progress)
        {
            SetProgress(_progress + 1);
        }
        else if (index == _progress - 1)
        {
            PokeDidNotAdvance?.Invoke();
        }
        else if (index == 0)
        {
            SetProgress(1);
        }
        else
        {
            SetProgress(0);
            PokeDidNotAdvance?.Invoke();
        }

        if (_progress == _keys.Length)
        {
            _isListening = false;
            Completed?.Invoke();
        }
    }

    private void SetProgress(int progress)
    {
        _progress = progress;
        ProgressChanged?.Invoke(_progress, _keys.Length);
    }
}
