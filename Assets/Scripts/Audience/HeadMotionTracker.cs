using Unity.XR.CoreUtils;
using UnityEngine;

public class HeadMotionTracker : MonoBehaviour
{
    [SerializeField] private Vector2 _angularSpeedRange = new Vector2(60.0f, 200.0f);
    [SerializeField] private Vector2 _linearSpeedRange = new Vector2(0.3f, 1.0f);
    [SerializeField] private float _smoothTime = 0.75f;

    private Transform _head;
    private Quaternion _lastRotation;
    private Vector3 _lastPosition;
    private float _angularSpeed;
    private float _linearSpeed;

    public float Intensity => Mathf.Max(
        Mathf.InverseLerp(_angularSpeedRange.x, _angularSpeedRange.y, _angularSpeed),
        Mathf.InverseLerp(_linearSpeedRange.x, _linearSpeedRange.y, _linearSpeed));

    private void Awake()
    {
        _head = FindFirstObjectByType<XROrigin>().Camera.transform;
        _lastRotation = _head.localRotation;
        _lastPosition = _head.localPosition;
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0.0f)
        {
            return;
        }

        var rotation = _head.localRotation;
        var position = _head.localPosition;
        var blend = 1.0f - Mathf.Exp(-deltaTime / Mathf.Max(1e-4f, _smoothTime));

        _angularSpeed = Mathf.Lerp(_angularSpeed, Quaternion.Angle(_lastRotation, rotation) / deltaTime, blend);
        _linearSpeed = Mathf.Lerp(_linearSpeed, Vector3.Distance(_lastPosition, position) / deltaTime, blend);

        _lastRotation = rotation;
        _lastPosition = position;
    }
}
