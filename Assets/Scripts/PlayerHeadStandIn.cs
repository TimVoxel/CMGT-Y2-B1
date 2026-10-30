using Unity.XR.CoreUtils;
using UnityEngine;

public class PlayerHeadStandIn : MonoBehaviour
{
    private Transform _viewer;

    private void Awake()
    {
        var viewerCamera = FindFirstObjectByType<XROrigin>().Camera;
        viewerCamera.cullingMask &= ~(1 << gameObject.layer);
        _viewer = viewerCamera.transform;
    }

    private void LateUpdate()
    {
        transform.SetPositionAndRotation(_viewer.position, _viewer.rotation);
    }
}
