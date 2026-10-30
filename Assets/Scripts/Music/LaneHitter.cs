using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// Put on a fingertip / controller tip. Anything with this component can strike a LaneHitZone, which tracks it by position.
[RequireComponent(typeof(Rigidbody))]
public class LaneHitter : MonoBehaviour
{
    [SerializeField] private float _radius = 0.015f;

    [SerializeField] private HapticImpulsePlayer _haptics;
    [SerializeField, Range(0.0f, 1.0f)] private float _baseAmplitude = 0.4f;
    [SerializeField, Range(0.0f, 1.0f)] private float _extraAmplitude = 0.6f;
    [SerializeField] private float _baseDuration = 0.05f;
    [SerializeField] private float _extraDuration = 0.07f;
    [SerializeField, Range(0.0f, 1.0f)] private float _strikeSpeedShare = 0.25f;
    [SerializeField] private Vector2 _strikeSpeedRange = new Vector2(0.2f, 1.5f);

    private static readonly List<LaneHitter> _active = new List<LaneHitter>();

    private Vector3 _lastPosition;

    public static IReadOnlyList<LaneHitter> Active => _active;
    public Vector3 Velocity { get; private set; }

    private void OnEnable()
    {
        _active.Add(this);
    }

    private void OnDisable()
    {
        _active.Remove(this);
    }

    private void Awake()
    {
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (!TryGetComponent<Collider>(out var col))
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = _radius;
            col = sphere;
        }
        col.isTrigger = true;

        if (_haptics == null)
        {
            _haptics = GetComponentInParent<HapticImpulsePlayer>();
        }

        _lastPosition = transform.position;
    }

    private void Update()
    {
        var position = transform.position;
        if (Time.deltaTime > 0.0f)
        {
            Velocity = (position - _lastPosition) / Time.deltaTime;
        }
        _lastPosition = position;
    }

    public void PlayHaptics(float accuracy, float strikeSpeed)
    {
        if (_haptics == null)
        {
            return;
        }

        accuracy = Mathf.Clamp01(accuracy);
        var accuracyAmplitude = _baseAmplitude + _extraAmplitude * accuracy;
        var speedAmplitude = (_baseAmplitude + _extraAmplitude) * Mathf.InverseLerp(_strikeSpeedRange.x, _strikeSpeedRange.y, strikeSpeed);
        var amplitude = Mathf.Clamp01(Mathf.Lerp(accuracyAmplitude, speedAmplitude, _strikeSpeedShare));
        _haptics.SendHapticImpulse(amplitude, _baseDuration + _extraDuration * accuracy);
    }
}
