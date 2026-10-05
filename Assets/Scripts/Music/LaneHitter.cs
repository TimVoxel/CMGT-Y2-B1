using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// Put on a fingertip / controller tip. Anything with this component can press a LaneHitZone.
[RequireComponent(typeof(Rigidbody))]
public class LaneHitter : MonoBehaviour
{
    [SerializeField] private float _radius = 0.015f;

    // Optional, found in parents if left empty (controllers have one, tracked hands don't)
    [SerializeField] private HapticImpulsePlayer _haptics;
    [SerializeField] private float _hitAmplitude = 0.7f;
    [SerializeField] private float _hitDuration = 0.08f;
    [SerializeField] private float _missAmplitude = 0.2f;
    [SerializeField] private float _missDuration = 0.04f;

    private Vector3 _lastPosition;

    public Vector3 Velocity { get; private set; }

    private void Awake()
    {
        // Kinematic so tracking drives it, but it still lets trigger events fire
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

    public void PlayHaptics(bool hit)
    {
        if (_haptics == null)
        {
            return;
        }

        if (hit)
        {
            _haptics.SendHapticImpulse(_hitAmplitude, _hitDuration);
        }
        else
        {
            _haptics.SendHapticImpulse(_missAmplitude, _missDuration);
        }
    }
}
