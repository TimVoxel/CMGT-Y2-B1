using UnityEngine;

// Springy "pop" on scale: quick grow, small overshoot below normal, settles back.
// Uses a damped spring, so punches in quick succession stack smoothly instead of snapping.
public class ScalePunch : MonoBehaviour
{
    // How much bigger at the peak, e.g. 0.25 = 25% bigger
    [SerializeField] private float _amount = 0.25f;
    // Wobbles per second. Higher = snappier.
    [SerializeField] private float _frequency = 4.0f;
    // 0 = wobbles forever, 1 = no overshoot. ~0.35 gives one visible undershoot, then settles.
    [SerializeField, Range(0.05f, 0.95f)] private float _damping = 0.35f;

    private Vector3 _baseScale;
    private float _offset;
    private float _velocity;

    private void Awake()
    {
        _baseScale = transform.localScale;
        enabled = false;
    }

    public void Punch()
    {
        // Impulse sized so a punch from rest peaks at about _amount
        var omega = 2.0f * Mathf.PI * _frequency;
        var dampedOmega = omega * Mathf.Sqrt(1.0f - _damping * _damping);
        var peakTime = Mathf.Atan2(Mathf.Sqrt(1.0f - _damping * _damping), _damping) / dampedOmega;
        _velocity += _amount * omega * Mathf.Exp(_damping * omega * peakTime);
        enabled = true;
    }

    private void Update()
    {
        var omega = 2.0f * Mathf.PI * _frequency;
        var stiffness = omega * omega;
        var damping = 2.0f * _damping * omega;

        // Substep so low frame rates don't make the spring explode
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
