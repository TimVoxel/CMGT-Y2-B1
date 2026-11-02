using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// A slider bar bent onto a cylinder centred on this object's position. Hold select on it and aim left or right.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class CurvedSlider : MonoBehaviour
{
    [SerializeField] private string _label = "Darkness";
    [SerializeField] private float _radius = 1.95f;
    [SerializeField] private float _centerAngle = 50.0f;
    [SerializeField] private float _arcAngle = 40.0f;
    [SerializeField] private float _verticalOffset;
    [SerializeField] private float _trackHeight = 0.02f;
    [SerializeField] private float _grabHeight = 0.12f;
    [SerializeField] private float _handleArcAngle = 1.5f;
    [SerializeField] private float _handleHeight = 0.08f;
    [SerializeField] private int _segments = 32;
    [SerializeField] private float _fontSize = 0.8f;
    [SerializeField] private float _labelSpacing = 0.08f;
    [SerializeField] private Color _trackColor = new Color(1.0f, 1.0f, 1.0f, 0.4f);
    [SerializeField] private Color _handleColor = new Color(1.0f, 1.0f, 1.0f, 0.9f);
    [SerializeField, Range(0.0f, 1.0f)] private float _value = 0.5f;

    private const int DrawOrderAbovePanel = 1;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private XRSimpleInteractable _interactable;
    private Transform _handle;
    private IXRSelectInteractor _dragger;

    public event Action<float> ValueChanged;
    public event Action Released;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _value))
            {
                return;
            }

            _value = clamped;
            UpdateHandle();
            ValueChanged?.Invoke(_value);
        }
    }

    private float MinAngle => _centerAngle - _arcAngle * 0.5f;
    private float MaxAngle => _centerAngle + _arcAngle * 0.5f;

    private void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = CurvedMenuButton.BuildArcMesh(_radius, _centerAngle, _arcAngle, _trackHeight, _verticalOffset, _segments);
        GetComponent<MeshCollider>().sharedMesh = CurvedMenuButton.BuildArcMesh(_radius, _centerAngle, _arcAngle, _grabHeight, _verticalOffset, _segments);
        var trackRenderer = GetComponent<MeshRenderer>();
        trackRenderer.sortingOrder = DrawOrderAbovePanel;
        SetColor(trackRenderer, _trackColor);

        _handle = new GameObject("Handle").transform;
        _handle.SetParent(transform, false);
        _handle.gameObject.AddComponent<MeshFilter>().sharedMesh = CurvedMenuButton.BuildArcMesh(_radius - 0.01f, 0.0f, _handleArcAngle, _handleHeight, _verticalOffset, 2);
        var handleRenderer = _handle.gameObject.AddComponent<MeshRenderer>();
        handleRenderer.sharedMaterial = trackRenderer.sharedMaterial;
        handleRenderer.sortingOrder = DrawOrderAbovePanel + 1;
        SetColor(handleRenderer, _handleColor);

        var width = _radius * _arcAngle * Mathf.Deg2Rad;
        var label = CurvedMenuButton.CreateLabel(transform, _label, _radius - 0.05f, _centerAngle, _verticalOffset + _labelSpacing, new Vector2(width, _labelSpacing), _fontSize);
        label.sortingOrder = DrawOrderAbovePanel;

        _interactable = GetComponent<XRSimpleInteractable>();
        UpdateHandle();
    }

    private void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _interactable.selectExited.RemoveListener(OnSelectExited);
        _dragger = null;
    }

    private void OnSelectEntered(SelectEnterEventArgs args) => _dragger = args.interactorObject;

    private void OnSelectExited(SelectExitEventArgs args)
    {
        _dragger = null;
        Released?.Invoke();
    }

    private void Update()
    {
        if (_dragger == null)
        {
            return;
        }

        var aim = _dragger.transform;
        if (TryGetAimAngle(aim.position, aim.forward, out var angle))
        {
            Value = Mathf.InverseLerp(MinAngle, MaxAngle, angle);
        }
    }

    private bool TryGetAimAngle(Vector3 worldOrigin, Vector3 worldDirection, out float angle)
    {
        angle = 0.0f;
        var origin3 = transform.InverseTransformPoint(worldOrigin);
        var direction3 = transform.InverseTransformDirection(worldDirection);
        var origin = new Vector2(origin3.x, origin3.z);
        var direction = new Vector2(direction3.x, direction3.z);

        var a = direction.sqrMagnitude;
        var b = 2.0f * Vector2.Dot(origin, direction);
        var c = origin.sqrMagnitude - _radius * _radius;
        var discriminant = b * b - 4.0f * a * c;
        if (a < 1e-6f || discriminant < 0.0f)
        {
            return false;
        }

        var hit = origin + direction * ((-b + Mathf.Sqrt(discriminant)) / (2.0f * a));
        angle = Mathf.Atan2(hit.x, hit.y) * Mathf.Rad2Deg;
        return true;
    }

    private void UpdateHandle()
    {
        if (_handle != null)
        {
            _handle.localRotation = Quaternion.Euler(0.0f, Mathf.Lerp(MinAngle, MaxAngle, _value), 0.0f);
        }
    }

    private static void SetColor(Renderer target, Color color)
    {
        var propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(BaseColorId, color);
        target.SetPropertyBlock(propertyBlock);
    }
}
