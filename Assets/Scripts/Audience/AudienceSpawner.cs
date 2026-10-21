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
    [SerializeField] private Vector3 _areaSize = new Vector3(10.0f, 2.0f, 6.0f);
    [SerializeField, Range(0.0f, 1.0f)] private float _areaGrowth = 0.25f;

    [SerializeField, Min(1.0f)] private float _maxAreaScale = 1.5f;
    [FormerlySerializedAs("_count")] [SerializeField] private int _initialSize = 40;
    [SerializeField] private float _minSpacing = 0.6f;
    [SerializeField] private Transform _lookAt;

    [SerializeField] private Vector2 _engagementThresholdRange = new Vector2(0.05f, 0.8f);
    [SerializeField] private Vector2 _maxAmplitudeRange = new Vector2(0.15f, 0.3f);
    [SerializeField] private bool _spawnOnStart = true;

    private readonly List<AudienceMember> _members = new List<AudienceMember>();
    private int _nextMemberId;
    public event Action<AudienceMember, bool> MemberAdded;
    public event Action<AudienceMember> MemberRemoved;

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
        if (_spawnOnStart)
        {
            Spawn();
        }
    }

    public void Spawn()
    {
        foreach (var member in _members)
        {
            MemberRemoved?.Invoke(member);
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

        var prefabIndex = Random.Range(0, _prefabs.Length);
        var engagementThreshold = Random.Range(_engagementThresholdRange.x, _engagementThresholdRange.y);
        var maxAmplitude = Random.Range(_maxAmplitudeRange.x, _maxAmplitudeRange.y);
        return AddMember(_nextMemberId, prefabIndex, position, GetFacing(position), engagementThreshold, maxAmplitude, popIn);
    }

    public AudienceMember AddMember(
        int id,
        int prefabIndex,
        Vector3 localPosition,
        Quaternion localRotation,
        float engagementThreshold,
        float maxAmplitude,
        bool popIn)
    {
        _nextMemberId = Mathf.Max(_nextMemberId, id + 1);

        var member = Instantiate(_prefabs[prefabIndex], transform);
        member.transform.localPosition = localPosition;
        member.transform.localRotation = localRotation;
        member.Init(id, prefabIndex, _engagement, _trackPlayer, engagementThreshold, maxAmplitude);
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

        RemoveMemberAt(index);
        return true;
    }

    public bool RemoveMember(int id)
    {
        var index = _members.FindIndex(member => member.Id == id);
        if (index < 0)
        {
            return false;
        }

        RemoveMemberAt(index);
        return true;
    }

    private void RemoveMemberAt(int index)
    {
        var member = _members[index];
        _members.RemoveAt(index);
        member.Leave();
        MemberRemoved?.Invoke(member);
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
