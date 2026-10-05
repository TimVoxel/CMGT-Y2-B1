using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Opens a mosh pit in the audience when a metal track gets the crowd hyped enough.
// Members standing inside the pit circle charge around and bounce off each other until engagement drops.
public class MoshPit : MonoBehaviour
{
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    [SerializeField, Range(0.0f, 1.0f)] private float _startEngagement = 0.95f;
    // Lower than the start so the pit doesn't flicker on and off around the threshold
    [SerializeField, Range(0.0f, 1.0f)] private float _stopEngagement = 0.85f;

    // Pit circle, in the spawner's local space (on its floor)
    [SerializeField] private Vector2 _center = Vector2.zero;
    [SerializeField] private float _radius = 2.0f;
    [SerializeField] private float _moshSpeed = 1.5f;
    // Moshers closer than this bump off each other
    [SerializeField] private float _bumpDistance = 0.5f;

    [SerializeField] private UnityEvent _onPitStarted;
    [SerializeField] private UnityEvent _onPitEnded;

    private readonly List<AudienceMember> _moshers = new List<AudienceMember>();
    private bool _isActive;

    public bool IsActive => _isActive;

    private Vector3 LocalCenter => new Vector3(_center.x, _audience != null ? _audience.FloorHeight : 0.0f, _center.y);

    private void Update()
    {
        var canMosh = _trackPlayer != null && _trackPlayer.IsPlaying && _trackPlayer.Track.IsMetal;
        var engagement = _engagement != null ? _engagement.Engagement : 0.0f;

        if (!_isActive && canMosh && engagement >= _startEngagement)
        {
            StartPit();
        }
        else if (_isActive && (!canMosh || engagement < _stopEngagement))
        {
            EndPit();
        }

        if (_isActive)
        {
            ResolveBumps();
        }
    }

    private void OnDisable()
    {
        if (_isActive)
        {
            EndPit();
        }
    }

    public void StartPit()
    {
        if (_audience == null)
        {
            return;
        }

        _isActive = true;
        _moshers.Clear();

        var center = LocalCenter;
        foreach (var member in _audience.Members)
        {
            var offset = member.BasePosition - center;
            offset.y = 0.0f;
            if (offset.magnitude <= _radius)
            {
                member.StartMoshing(center, _radius, _moshSpeed * Random.Range(0.8f, 1.2f));
                _moshers.Add(member);
            }
        }

        _onPitStarted.Invoke();
    }

    public void EndPit()
    {
        _isActive = false;
        foreach (var member in _moshers)
        {
            if (member != null)
            {
                member.StopMoshing();
            }
        }
        _moshers.Clear();

        _onPitEnded.Invoke();
    }

    // Push overlapping moshers apart and bounce their velocities, like bodies colliding
    private void ResolveBumps()
    {
        for (var i = 0; i < _moshers.Count; i++)
        {
            for (var j = i + 1; j < _moshers.Count; j++)
            {
                var a = _moshers[i];
                var b = _moshers[j];
                var delta = b.GroundPosition - a.GroundPosition;
                delta.y = 0.0f;
                var distance = delta.magnitude;
                if (distance >= _bumpDistance || distance < 1e-5f)
                {
                    continue;
                }

                var normal = delta / distance;
                var push = normal * ((_bumpDistance - distance) * 0.5f);
                a.Nudge(-push);
                b.Nudge(push);

                // Only bounce if they're moving into each other
                var closingSpeed = Vector3.Dot(a.MoshVelocity - b.MoshVelocity, normal);
                if (closingSpeed > 0.0f)
                {
                    a.MoshVelocity -= normal * closingSpeed;
                    b.MoshVelocity += normal * closingSpeed;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_audience == null)
        {
            return;
        }

        Gizmos.matrix = _audience.transform.localToWorldMatrix;
        Gizmos.color = new Color(1.0f, 0.1f, 0.1f, 0.9f);

        const int segments = 48;
        var center = LocalCenter;
        var previous = center + new Vector3(_radius, 0.0f, 0.0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * 2.0f * Mathf.PI / segments;
            var point = center + new Vector3(Mathf.Cos(angle) * _radius, 0.0f, Mathf.Sin(angle) * _radius);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
        Gizmos.DrawLine(center + Vector3.left * 0.2f, center + Vector3.right * 0.2f);
        Gizmos.DrawLine(center + Vector3.back * 0.2f, center + Vector3.forward * 0.2f);
    }
}
