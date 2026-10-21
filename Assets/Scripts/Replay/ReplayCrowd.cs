using UnityEngine;

// Rebuilds the recorded audience: who's there, how engaged they are, cheers and mosh pits
public class ReplayCrowd : MonoBehaviour
{
    [SerializeField] private ReplayEventDispatcher _dispatcher;
    [SerializeField] private AudienceSpawner _audience;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private CrowdCheer _cheer;
    [SerializeField] private MoshPit _moshPit;

    private void OnEnable()
    {
        _dispatcher.Subscribe<AudienceMemberJoinedEvent>(OnMemberJoined);
        _dispatcher.Subscribe<AudienceMemberLeftEvent>(OnMemberLeft);
        _dispatcher.Subscribe<EngagementChangedEvent>(OnEngagementChanged);
        _dispatcher.Subscribe<CrowdCheeredEvent>(OnCheered);
        _dispatcher.Subscribe<MoshPitStartedEvent>(OnMoshPitStarted);
        _dispatcher.Subscribe<MoshPitEndedEvent>(OnMoshPitEnded);
    }

    private void OnDisable()
    {
        _dispatcher.Unsubscribe<AudienceMemberJoinedEvent>(OnMemberJoined);
        _dispatcher.Unsubscribe<AudienceMemberLeftEvent>(OnMemberLeft);
        _dispatcher.Unsubscribe<EngagementChangedEvent>(OnEngagementChanged);
        _dispatcher.Unsubscribe<CrowdCheeredEvent>(OnCheered);
        _dispatcher.Unsubscribe<MoshPitStartedEvent>(OnMoshPitStarted);
        _dispatcher.Unsubscribe<MoshPitEndedEvent>(OnMoshPitEnded);
    }

    private void OnMemberJoined(AudienceMemberJoinedEvent joined)
    {
        _audience.AddMember(
            joined.MemberId,
            joined.PrefabIndex,
            joined.LocalPosition,
            joined.LocalRotation,
            joined.EngagementThreshold,
            joined.MaxAmplitude,
            joined.PoppedIn);
    }

    private void OnMemberLeft(AudienceMemberLeftEvent left)
    {
        _audience.RemoveMember(left.MemberId);
    }

    private void OnEngagementChanged(EngagementChangedEvent engagementChanged)
    {
        _engagement.Set(engagementChanged.Engagement);
    }

    private void OnCheered(CrowdCheeredEvent cheered)
    {
        if (_cheer != null)
        {
            _cheer.Cheer(cheered.ClipIndex, cheered.Pitch, cheered.Volume);
        }
    }

    private void OnMoshPitStarted(MoshPitStartedEvent moshPitStarted)
    {
        if (_moshPit != null)
        {
            _moshPit.StartPit();
        }
    }

    private void OnMoshPitEnded(MoshPitEndedEvent moshPitEnded)
    {
        if (_moshPit != null && _moshPit.IsActive)
        {
            _moshPit.EndPit();
        }
    }
}
