using System;
using UnityEngine;

// How into the show the audience is, 0..1. Drifts down on its own so it has to be earned.
public class EngagementHolder : MonoBehaviour
{
    [SerializeField, Range(0.0f, 1.0f)] private float _startEngagement = 0.1f;
    // Per second, so a quiet stretch slowly loses the crowd
    [SerializeField] private float _decayPerSecond = 0.01f;
    // Never decays below this
    [SerializeField, Range(0.0f, 1.0f)] private float _floor = 0.0f;

    private float _engagement;

    public float Engagement => _engagement;

    public event Action<float> Changed;

    private void Awake()
    {
        _engagement = _startEngagement;
    }

    private void Update()
    {
        if (_engagement > _floor)
        {
            Set(Mathf.Max(_floor, _engagement - _decayPerSecond * Time.deltaTime));
        }
    }

    // Back to the starting mood, e.g. when a new song starts
    public void ResetEngagement()
    {
        Set(_startEngagement);
    }

    public void Add(float amount)
    {
        Set(_engagement + amount);
    }

    public void Set(float value)
    {
        var clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, _engagement))
        {
            return;
        }
        _engagement = clamped;
        Changed?.Invoke(_engagement);
    }
}
