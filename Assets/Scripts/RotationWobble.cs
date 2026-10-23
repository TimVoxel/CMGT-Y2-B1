using UnityEngine;

// Quick decaying rock around an axis, like a struck bar ringing.
// Uses a damped spring like ScalePunch, so hits in quick succession stack instead of snapping.
public class RotationWobble : MonoBehaviour
{
    [SerializeField] private Vector3 _localAxis = Vector3.right;
    [SerializeField] private float _angle = 4.0f;
    [SerializeField] private float _frequency = 9.0f;
    [SerializeField, Range(0.05f, 0.95f)] private float _damping = 0.15f;

    private Quaternion _restRotation;
    private float _offset;
    private float _velocity;

    private void Awake()
    {
        _restRotation = transform.localRotation;
        enabled = false;
    }

    public void Wobble()
    {
        Kick(_angle);
    }

    private void Kick(float angle)
    {
        var omega = 2.0f * Mathf.PI * _frequency;
        // Alternate the swing direction so stacked hits don't keep pushing the same way
        _velocity += (_velocity > 0.0f ? -1.0f : 1.0f) * angle * omega;
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

        if (Mathf.Abs(_offset) < 0.01f && Mathf.Abs(_velocity) < 0.1f)
        {
            _offset = 0.0f;
            _velocity = 0.0f;
            enabled = false;
        }

        transform.localRotation = _restRotation * Quaternion.AngleAxis(_offset, _localAxis);
    }
}
