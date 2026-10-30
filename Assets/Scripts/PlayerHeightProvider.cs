using System;
using Unity.XR.CoreUtils;
using UnityEngine;

public class PlayerHeightProvider : MonoBehaviour
{
    [SerializeField] private XROrigin _origin;

    [SerializeField] private float _eyeToHeadTop = 0.1f;
    [SerializeField] private float _maxHeight = 2.2f;

    public Transform Origin => _origin.transform;
    public Transform Head => _origin.Camera.transform;
    public float FloorY => _origin.transform.position.y;
    public float EyeHeight => _origin.Camera.transform.position.y - FloorY;
    public float HeadTop => EyeHeight + _eyeToHeadTop;
    public float NormalizedHeightFactor => Math.Max(0, EyeHeight) / Math.Max(1e-6f, _maxHeight);

#if UNITY_EDITOR
    private void Reset()
    {
        _origin = FindFirstObjectByType<XROrigin>();
    }
#endif
}
