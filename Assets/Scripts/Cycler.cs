using UnityEngine;
using System;
using UnityEngine.UI;

public class Cycler : MonoBehaviour
{
    [SerializeField] private Transform _transform;
    [SerializeField] private float _maxSpeed;
    [SerializeField] private float _amplitude; 
    [SerializeField] private float _slowDownSpeed;

    [SerializeField] private RawImage[] _image;

    private float _startY;
    private float _scroll;
    private float _scrollSpeed;

    public event Action<Cycler> Revolved;

    private int _slotIndex;

    public int SlotIndex => _slotIndex;

    private void Awake()
    {
        _startY = _transform.position.y;
        _scroll = _startY;
        _slotIndex = 0;
    }

    private void Update()
    {
        _scroll += Time.deltaTime * _scrollSpeed;
        _transform.position = new Vector3(
            _transform.position.x, 
            _startY + _scroll * _amplitude,
            _transform.position.z);

        if (_scroll >= 1.0f)
        {
            _scroll = 0.0f;
            Revolved?.Invoke(this);
            _slotIndex = (_slotIndex + 1 + 2) % 2;
        }
        _scrollSpeed = Mathf.Max(0, _scrollSpeed - Time.deltaTime * _slowDownSpeed);
    }

    public void Spin()
    {
        _scrollSpeed = _maxSpeed;
    }

    public void ChangeSlot(Color color)
    {

        // Debug.Log($"SlotIndex: {_slotIndex}, {_image[_slotIndex].gameObject.name}: {color}");
        // Debug.Log($"Wrap: {(_slotIndex + 1 + 2) % 2}, {_image[(_slotIndex + 1 + 2) % 2].gameObject.name}: {_image[_slotIndex].color}");

        _image[0].color = _image[1].color;
        _image[1].color = color;
    }
}
