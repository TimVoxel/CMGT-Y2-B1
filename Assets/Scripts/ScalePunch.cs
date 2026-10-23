using UnityEngine;

// Springy "pop" on scale: quick grow, small overshoot below normal, settles back.
// Uses a damped spring, so punches in quick succession stack smoothly instead of snapping.
public class ScalePunch : MonoBehaviour
{
    [SerializeField] private float _amount = 0.25f;
    [SerializeField] private float _frequency = 4.0f;
    [SerializeField, Range(0.05f, 0.95f)] private float _damping = 0.35f;
    [SerializeField] private float _shrinkAmount = 0.2f;

    private Vector3 _baseScale;
    private float _offset;
    private float _velocity;

    // For whatever else sizes this object, so a punch in progress doesn't restore a stale scale
    public Vector3 RestScale
    {
        get => _baseScale;
        set => _baseScale = value;
    }

    private void Awake()
    {
        _baseScale = transform.localScale;
        enabled = false;
    }

    public void Punch()
    {
        Kick(_amount);
    }

    public void Shrink()
    {
        Kick(-_shrinkAmount);
    }

    private void Kick(float amount)
    {
        var omega = 2.0f * Mathf.PI * _frequency;
        var dampedOmega = omega * Mathf.Sqrt(1.0f - _damping * _damping);
        var peakTime = Mathf.Atan2(Mathf.Sqrt(1.0f - _damping * _damping), _damping) / dampedOmega;
        _velocity += amount * omega * Mathf.Exp(_damping * omega * peakTime);
        enabled = true;
    }

    private void Update()
    {
        var omega = 2.0f * Mathf.PI * _frequency;
        var stiffness = omega * omega;
        var damping = 2.0f * _damping * omega;

        const float maxStep = 1.0f / 240.0f;
        var remaining = Time.deltaTime;
        while (remaining > 0.0f)
        {
            var dt = Mathf.Min(remaining, maxStep);
            _velocity += (-stiffness * _offset - damping * _velocity) * dt;
            _offset += _velocity * dt;
            remaining -= dt;
        }

        if (Mathf.Abs(_offset) < 0.0005f && Mathf.Abs(_velocity) < 0.005f)
        {
            _offset = 0.0f;
            _velocity = 0.0f;
            enabled = false;
        }

        transform.localScale = _baseScale * (1.0f + _offset);
    }
}
