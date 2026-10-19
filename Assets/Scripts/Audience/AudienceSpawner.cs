using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class AudienceSpawner : MonoBehaviour
{
    [SerializeField] private AudienceMember[] _prefabs;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private CrowdCheer _cheer;

    // Area for the starting crowd. Grows as more members join,
    // extending away from _lookAt so the front row stays where it is.
    [SerializeField] private Vector3 _areaSize = new Vector3(10.0f, 2.0f, 6.0f);

    // How fast width and depth grow with headcount: each side scales by (count / start count) ^ this.
    // 0 = never grows (crowd just gets denser), 0.5 = same density as the starting crowd, 1 = sides grow as fast as headcount.
    [SerializeField, Range(0.0f, 1.0f)] private float _areaGrowth = 0.25f;

    // Cap on how many times wider/deeper than the starting area it can get
    [SerializeField, Min(1.0f)] private float _maxAreaScale = 1.5f;
    [FormerlySerializedAs("_count")] [SerializeField] private int _initialSize = 40;
    [SerializeField] private float _minSpacing = 0.6f;
    [SerializeField] private Transform _lookAt;

    [SerializeField] private Vector2 _engagementThresholdRange = new Vector2(0.05f, 0.8f);
    [SerializeField] private Vector2 _maxAmplitudeRange = new Vector2(0.15f, 0.3f);

    private readonly List<AudienceMember> _members = new List<AudienceMember>();
    public event Action<AudienceMember, bool> MemberAdded;

    public IReadOnlyList<AudienceMember> Members => _members;
    public int StartCount => _initialSize;
    public float FloorHeight => -0.5f * _areaSize.y;

    private Vector3 GetAreaSize(int memberCount)
    {
        var growth = Mathf.Max(1.0f, (float)memberCount / Mathf.Max(1, _initialSize));
        var scale = Mathf.Min(_maxAreaScale, Mathf.Pow(growth, _areaGrowth));
        return new Vector3(_areaSize.x * scale, _areaSize.y, _areaSize.z * scale);
    }

    private Vector3 GetAreaCenter(Vector3 areaSize)
    {
        var extraDepth = areaSize.z - _areaSize.z;
        return new Vector3(0.0f, 0.0f, -GetFrontSign() * 0.5f * extraDepth);
    }

    private float GetFrontSign()
    {
        if (_lookAt == null)
        {
            return 0.0f;
        }
        var z = transform.InverseTransformPoint(_lookAt.position).z;
        return Mathf.Abs(z) < 1e-4f ? 0.0f : Mathf.Sign(z);
    }

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

        for (var i = 0; i < _initialSize; i++)
        {
            AddMember(false);
        }
    }

    public AudienceMember AddMember(bool popIn)
    {
        var position = FindFreeSpot(GetAreaSize(_members.Count + 1));

        var member = Instantiate(_prefabs[Random.Range(0, _prefabs.Length)], transform);
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
        if (popIn)
        {
            member.PopIn();
        }
        _members.Add(member);
        MemberAdded?.Invoke(member, popIn);
        return member;
    }

    public bool RemoveLeastEngagedMember()
    {
        var index = -1;
        for (var i = 0; i < _members.Count; i++)
        {
            if (_members[i].IsMoshing)
            {
                continue;
            }
            if (index < 0 || _members[i].EngagementThreshold > _members[index].EngagementThreshold)
            {
                index = i;
            }
        }
        if (index < 0)
        {
            return false;
        }

        var member = _members[index];
        _members.RemoveAt(index);
        member.Leave();
        return true;
    }

    private Vector3 FindFreeSpot(Vector3 areaSize)
    {
        const int attempts = 30;
        var center = GetAreaCenter(areaSize);
        var best = RandomFloorPoint(areaSize, center);
        var bestDistanceSqr = NearestMemberDistanceSqr(best);
        var minSqr = _minSpacing * _minSpacing;
        for (var attempt = 1; attempt < attempts && bestDistanceSqr < minSqr; attempt++)
        {
            var candidate = RandomFloorPoint(areaSize, center);
            var distanceSqr = NearestMemberDistanceSqr(candidate);
            if (distanceSqr > bestDistanceSqr)
            {
                best = candidate;
                bestDistanceSqr = distanceSqr;
            }
        }
        return best;
    }

    private Vector3 RandomFloorPoint(Vector3 areaSize, Vector3 center)
    {
        return new Vector3(
            center.x + Random.Range(-0.5f, 0.5f) * areaSize.x,
            FloorHeight,
            center.z + Random.Range(-0.5f, 0.5f) * areaSize.z);
    }

    private float NearestMemberDistanceSqr(Vector3 position)
    {
        var nearest = float.PositiveInfinity;
        foreach (var member in _members)
        {
            nearest = Mathf.Min(nearest, (member.BasePosition - position).sqrMagnitude);
        }
        return nearest;
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
        var size = GetAreaSize(Application.isPlaying ? _members.Count : _initialSize);
        Gizmos.DrawWireCube(GetAreaCenter(size), size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        var size = GetAreaSize(Application.isPlaying ? _members.Count : _initialSize);
        var center = GetAreaCenter(size);

        Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.15f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(1.0f, 0.5f, 0.1f, 0.4f);
        Gizmos.DrawCube(new Vector3(center.x, FloorHeight, center.z), new Vector3(size.x, 0.0f, size.z));

        if (_lookAt != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _lookAt.position);
        }
    }
}
