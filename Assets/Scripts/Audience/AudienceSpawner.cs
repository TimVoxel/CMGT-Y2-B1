using System.Collections.Generic;
using UnityEngine;

// Spawns audience members standing on the bottom face of a box (in this object's local space).
public class AudienceSpawner : MonoBehaviour
{
    [SerializeField] private AudienceMember _prefab;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;
    // Optional. Members start clapping below this cheer's threshold.
    [SerializeField] private CrowdCheer _cheer;

    [SerializeField] private Vector3 _areaSize = new Vector3(10.0f, 2.0f, 6.0f);
    [SerializeField] private int _count = 40;
    // Members won't spawn closer than this to each other (if there's room)
    [SerializeField] private float _minSpacing = 0.6f;
    // Optional. Members turn to face it, e.g. the stage or the player.
    [SerializeField] private Transform _lookAt;

    // Each member gets a random threshold in this range. Low = easy to win over.
    [SerializeField] private Vector2 _engagementThresholdRange = new Vector2(0.05f, 0.8f);
    [SerializeField] private Vector2 _maxAmplitudeRange = new Vector2(0.15f, 0.3f);

    private readonly List<AudienceMember> _members = new List<AudienceMember>();

    public IReadOnlyList<AudienceMember> Members => _members;
    // Local height members stand at
    public float FloorHeight => -0.5f * _areaSize.y;

    private void Start()
    {
        Spawn();
    }

    public void Spawn()
    {
        foreach (var member in _members)
        {
            Destroy(member.gameObject);
        }
        _members.Clear();

        var placed = new List<Vector3>();
        const int attemptsPerMember = 30;
        for (var i = 0; i < _count; i++)
        {
            var position = RandomFloorPoint();
            for (var attempt = 1; attempt < attemptsPerMember && TooClose(position, placed); attempt++)
            {
                position = RandomFloorPoint();
            }
            placed.Add(position);

            var member = Instantiate(_prefab, transform);
            member.transform.localPosition = position;
            member.transform.localRotation = GetFacing(position);
            member.Init(
                _engagement,
                _trackPlayer,
                Random.Range(_engagementThresholdRange.x, _engagementThresholdRange.y),
                Random.Range(_maxAmplitudeRange.x, _maxAmplitudeRange.y));
            if (_cheer != null)
            {
                member.SetClapCeiling(_cheer.CheerEngagement);
            }
            _members.Add(member);
        }
    }

    private Vector3 RandomFloorPoint()
    {
        return new Vector3(
            Random.Range(-0.5f, 0.5f) * _areaSize.x,
            -0.5f * _areaSize.y,
            Random.Range(-0.5f, 0.5f) * _areaSize.z);
    }

    private bool TooClose(Vector3 position, List<Vector3> placed)
    {
        var minSqr = _minSpacing * _minSpacing;
        foreach (var other in placed)
        {
            if ((other - position).sqrMagnitude < minSqr)
            {
                return true;
            }
        }
        return false;
    }

    private Quaternion GetFacing(Vector3 localPosition)
    {
        if (_lookAt == null)
        {
            return Quaternion.identity;
        }

        var toTarget = transform.InverseTransformPoint(_lookAt.position) - localPosition;
        toTarget.y = 0.0f;
        return toTarget.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toTarget) : Quaternion.identity;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, _areaSize);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, _areaSize);

        // Floor the members stand on
        Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.4f);
        Gizmos.DrawCube(new Vector3(0.0f, -0.5f * _areaSize.y, 0.0f), new Vector3(_areaSize.x, 0.0f, _areaSize.z));

        // Which way they'll face
        if (_lookAt != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _lookAt.position);
        }
    }
}
