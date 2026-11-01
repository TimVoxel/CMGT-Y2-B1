using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A rectangular button bent onto a cylinder centred on this object's position.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class CurvedMenuButton : MonoBehaviour
{
    [SerializeField] private string _label = "Play";
    [SerializeField] private float _radius = 2.0f;
    [SerializeField] private float _centerAngle;
    [SerializeField] private float _arcAngle = 36.0f;
    [SerializeField] private float _height = 0.3f;
    [SerializeField] private float _verticalOffset = -0.05f;
    [SerializeField] private int _segments = 16;
    [SerializeField] private float _fontSize = 1.2f;
    [SerializeField] private Color _idleColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);
    [SerializeField] private Color _hoverColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private XRSimpleInteractable _interactable;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    public event Action Pressed;

    private void Awake()
    {
        var mesh = BuildArcMesh(_radius, _centerAngle, _arcAngle, _height, _verticalOffset, _segments);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        _renderer = GetComponent<MeshRenderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _interactable = GetComponent<XRSimpleInteractable>();

        if (!string.IsNullOrEmpty(_label))
        {
            var width = _radius * _arcAngle * Mathf.Deg2Rad;
            CreateLabel(transform, _label, _radius - 0.05f, _centerAngle, _verticalOffset, new Vector2(width, _height), _fontSize);
        }
        SetColor(_idleColor);
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        SetColor(_idleColor);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => SetColor(_hoverColor);

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (!_interactable.isHovered)
        {
            SetColor(_idleColor);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args) => Pressed?.Invoke();

    private void SetColor(Color color)
    {
        _propertyBlock.SetColor(BaseColorId, color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    private static Vector3 PointOnArc(float radius, float centerAngle, float arcAngle, float t)
    {
        var angle = Mathf.Lerp(centerAngle - arcAngle * 0.5f, centerAngle + arcAngle * 0.5f, t);
        return Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward * radius;
    }

    public static Mesh BuildArcMesh(float radius, float centerAngle, float arcAngle, float height, float verticalOffset, int segments)
    {
        var vertices = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        var bottom = Vector3.up * (verticalOffset - height * 0.5f);
        var top = Vector3.up * (verticalOffset + height * 0.5f);

        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var point = PointOnArc(radius, centerAngle, arcAngle, t);
            vertices[i * 2] = point + bottom;
            vertices[i * 2 + 1] = point + top;
            uvs[i * 2] = new Vector2(t, 0.0f);
            uvs[i * 2 + 1] = new Vector2(t, 1.0f);
        }

        for (var i = 0; i < segments; i++)
        {
            var vertex = i * 2;
            var triangle = i * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 3;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;
        }

        var mesh = new Mesh { name = "Curved Arc" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static TextMeshPro CreateLabel(Transform parent, string label, float radius, float angle, float verticalOffset, Vector2 size, float fontSize)
    {
        var direction = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
        var text = new GameObject("Label").AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = direction * radius + Vector3.up * verticalOffset;
        text.transform.localRotation = Quaternion.LookRotation(direction);
        text.rectTransform.sizeDelta = size;
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        var bottom = Vector3.up * (_verticalOffset - _height * 0.5f);
        var top = Vector3.up * (_verticalOffset + _height * 0.5f);

        for (var i = 0; i < _segments; i++)
        {
            var from = PointOnArc(_radius, _centerAngle, _arcAngle, (float)i / _segments);
            var to = PointOnArc(_radius, _centerAngle, _arcAngle, (float)(i + 1) / _segments);
            Gizmos.DrawLine(from + bottom, to + bottom);
            Gizmos.DrawLine(from + top, to + top);
        }
        Gizmos.DrawLine(PointOnArc(_radius, _centerAngle, _arcAngle, 0.0f) + bottom, PointOnArc(_radius, _centerAngle, _arcAngle, 0.0f) + top);
        Gizmos.DrawLine(PointOnArc(_radius, _centerAngle, _arcAngle, 1.0f) + bottom, PointOnArc(_radius, _centerAngle, _arcAngle, 1.0f) + top);
    }
}
