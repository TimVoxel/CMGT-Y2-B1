using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using Random = UnityEngine.Random;

public class LivestreamCameraDirector : MonoBehaviour
{
    [Serializable]
    private struct Shot
    {
        public Camera Camera;
        public float DollyScale;
    }

    [SerializeField] private Shot[] _shots;
    [SerializeField] private RectTransform _screen;
    [SerializeField, Range(1.0f, 179.0f)] private float _viewerFieldOfView = 125.0f;
    [SerializeField] private Vector2 _shotDurationRange = new Vector2(3.5f, 8.0f);

    [Header("Operator move during a shot")]
    [SerializeField] private float _dollyDistance = 0.25f;
    [SerializeField] private float _panAngle = 3.0f;
    [SerializeField] private float _zoomFieldOfView = 5.0f;

    [Header("Handheld sway")]
    [SerializeField] private float _swayDistance = 0.015f;
    [SerializeField] private float _swayAngle = 0.4f;
    [SerializeField] private float _swayFrequency = 0.35f;

    private readonly Plane[] _viewerFrustum = new Plane[6];
    private Camera _viewer;
    private Bounds _screenBounds;

    private Vector3[] _restPositions;
    private Quaternion[] _restRotations;
    private float[] _restFieldsOfView;

    private int _activeCameraIndex;
    private float _shotElapsed;
    private float _shotDuration;
    private Vector3 _dollyHalfOffset;
    private Vector3 _panHalfAngles;
    private float _zoomHalfDelta;

    private void Awake()
    {
        _viewer = FindFirstObjectByType<XROrigin>().Camera;
        _screenBounds = GetWorldBounds(_screen);

        _restPositions = new Vector3[_shots.Length];
        _restRotations = new Quaternion[_shots.Length];
        _restFieldsOfView = new float[_shots.Length];

        for (var i = 0; i < _shots.Length; i++)
        {
            var camera = _shots[i].Camera;
            _restPositions[i] = camera.transform.localPosition;
            _restRotations[i] = camera.transform.localRotation;
            _restFieldsOfView[i] = camera.fieldOfView;
            camera.enabled = false;
        }

        _activeCameraIndex = Random.Range(0, _shots.Length);
        StartShot();
    }

    private void Update()
    {
        _shotElapsed += Time.deltaTime;
        if (_shotElapsed >= _shotDuration)
        {
            CutToAnotherCamera();
        }

        var isScreenInView = IsScreenInView();
        _shots[_activeCameraIndex].Camera.enabled = isScreenInView;
        if (isScreenInView)
        {
            ApplyActiveCameraPose();
        }
    }

    private bool IsScreenInView()
    {
        var projection = Matrix4x4.Perspective(_viewerFieldOfView, 1.0f, _viewer.nearClipPlane, _viewer.farClipPlane);
        GeometryUtility.CalculateFrustumPlanes(projection * _viewer.worldToCameraMatrix, _viewerFrustum);
        return GeometryUtility.TestPlanesAABB(_viewerFrustum, _screenBounds);
    }

    private static Bounds GetWorldBounds(RectTransform rectTransform)
    {
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        var bounds = new Bounds(corners[0], Vector3.zero);
        for (var i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(corners[i]);
        }
        return bounds;
    }

    private void CutToAnotherCamera()
    {
        _shots[_activeCameraIndex].Camera.enabled = false;
        _activeCameraIndex = (_activeCameraIndex + Random.Range(1, _shots.Length)) % _shots.Length;
        StartShot();
    }

    private void StartShot()
    {
        _shotElapsed = 0.0f;
        _shotDuration = Random.Range(_shotDurationRange.x, _shotDurationRange.y);

        _dollyHalfOffset = Random.onUnitSphere * _dollyDistance * _shots[_activeCameraIndex].DollyScale * 0.5f;
        _panHalfAngles = new Vector3(
            Random.Range(-0.3f, 0.3f),
            Random.value < 0.5f ? -1.0f : 1.0f,
            0.0f) * _panAngle * 0.5f;
        _zoomHalfDelta = Random.Range(-_zoomFieldOfView, _zoomFieldOfView) * 0.5f;
    }

    private void ApplyActiveCameraPose()
    {
        var camera = _shots[_activeCameraIndex].Camera;
        var restRotation = _restRotations[_activeCameraIndex];

        var progress = Mathf.SmoothStep(-1.0f, 1.0f, _shotElapsed / _shotDuration);
        var swayTime = Time.time * _swayFrequency;

        var position = _dollyHalfOffset * progress + new Vector3(
            Sway(swayTime, 11.0f),
            Sway(swayTime, 23.0f),
            Sway(swayTime, 37.0f)) * _swayDistance;

        var angles = _panHalfAngles * progress + new Vector3(
            Sway(swayTime, 41.0f),
            Sway(swayTime, 53.0f),
            Sway(swayTime, 67.0f) * 0.5f) * _swayAngle;

        camera.transform.localPosition = _restPositions[_activeCameraIndex] + restRotation * position;
        camera.transform.localRotation = restRotation * Quaternion.Euler(angles);
        camera.fieldOfView = _restFieldsOfView[_activeCameraIndex] + _zoomHalfDelta * progress;
    }

    private static float Sway(float time, float seed)
    {
        return Mathf.PerlinNoise(time, seed) * 2.0f - 1.0f;
    }
}
