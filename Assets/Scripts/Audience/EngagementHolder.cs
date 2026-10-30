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

    [Header("Charisma")]
    [SerializeField] private HeadMotionTracker _headMotion;
    [SerializeField, Min(1.0f)] private float _maxCharismaGainMultiplier = 2.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float _minCharismaPenaltyMultiplier = 0.5f;
    [SerializeField] private float _charismaBoostDecayPerSecond = 0.1f;

    private float _engagement;
    private float _charismaBoost;

    public float Engagement => _engagement;
    public float Charisma => Mathf.Clamp01((_headMotion != null ? _headMotion.Intensity : 0.0f) + _charismaBoost);
    public float CharismaGainMultiplier => Mathf.Lerp(1.0f, _maxCharismaGainMultiplier, Charisma);
    public float CharismaPenaltyMultiplier => Mathf.Lerp(1.0f, _minCharismaPenaltyMultiplier, Charisma);

    public event Action<float> Changed;

    private void Awake()
    {
        _engagement = _startEngagement;
    }

    private void Update()
    {
        _charismaBoost = Mathf.MoveTowards(_charismaBoost, 0.0f, _charismaBoostDecayPerSecond * Time.deltaTime);

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

    public void AddCharisma(float amount)
    {
        _charismaBoost = Mathf.Clamp01(_charismaBoost + amount);
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
