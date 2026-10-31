using UnityEngine;

// Keeps a menu centered in front of the player at eye level, lazily following them so it never jitters with the head.
public class EyeLevelPlacement : MonoBehaviour
{
    [SerializeField] private PlayerHeightProvider _player;
    [SerializeField] private float _distance = 1.5f;
    [SerializeField] private float _smoothTime = 0.3f;
    [SerializeField] private float _fallbackEyeHeight = 1.6f;
    [SerializeField] private float _minEyeHeight = 0.8f;

    private Vector3 _velocity;
    private bool _placed;

    private void LateUpdate()
    {
        var target = TargetPosition();
        if (_placed)
        {
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, _smoothTime);
        }
        else
        {
            transform.position = target;
            _placed = true;
        }

        var awayFromHead = transform.position - _player.Head.position;
        awayFromHead.y = 0.0f;
        if (awayFromHead.sqrMagnitude > 1e-6f)
        {
            transform.rotation = Quaternion.LookRotation(awayFromHead);
        }
    }

    private Vector3 TargetPosition()
    {
        var forward = _player.Origin.forward;
        forward.y = 0.0f;
        forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;

        var eyeHeight = _player.EyeHeight >= _minEyeHeight ? _player.EyeHeight : _fallbackEyeHeight;
        var position = _player.Head.position + forward * _distance;
        position.y = _player.FloorY + eyeHeight;
        return position;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _player = FindFirstObjectByType<PlayerHeightProvider>();
    }
#endif
}
