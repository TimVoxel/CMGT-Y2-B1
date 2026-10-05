using UnityEngine;

// Whole-crowd cheering from one AudioSource, so cheers never pile on top of each other.
// Cheers when engagement first reaches the threshold, then now and again while it stays up there.
public class CrowdCheer : MonoBehaviour
{
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    [SerializeField] private AudioClip[] _cheerClips;
    // Optional, one is added if left empty
    [SerializeField] private AudioSource _source;

    [SerializeField, Range(0.0f, 1.0f)] private float _cheerEngagement = 0.85f;
    // Cheer straight away when engagement climbs past the threshold
    [SerializeField] private bool _cheerOnReachingThreshold = true;
    // Chance per second of another cheer while engagement stays above the threshold
    [SerializeField] private float _cheerChancePerSecond = 0.1f;
    // Quiet time after a cheer finishes before the next one can start
    [SerializeField] private float _cooldown = 3.0f;
    // Volume at the threshold and at full engagement
    [SerializeField] private Vector2 _volumeRange = new Vector2(0.6f, 1.0f);
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.95f, 1.05f);

    private float _nextCheerAllowedTime;
    private int _lastClipIndex = -1;
    private bool _wasAboveThreshold;

    public float CheerEngagement => _cheerEngagement;

    private void Awake()
    {
        if (_source == null && !TryGetComponent(out _source))
        {
            _source = gameObject.AddComponent<AudioSource>();
        }
        _source.playOnAwake = false;
        _source.loop = false;
    }

    private void Update()
    {
        var isAbove = _engagement != null
            && _engagement.Engagement >= _cheerEngagement
            && _trackPlayer != null && _trackPlayer.IsPlaying;
        var justReached = isAbove && !_wasAboveThreshold;
        _wasAboveThreshold = isAbove;

        if (!isAbove || _source.isPlaying || Time.time < _nextCheerAllowedTime)
        {
            return;
        }

        // Frame-rate independent version of "_cheerChancePerSecond"
        var chance = 1.0f - Mathf.Exp(-_cheerChancePerSecond * Time.deltaTime);
        if ((justReached && _cheerOnReachingThreshold) || Random.value < chance)
        {
            Cheer();
        }
    }

    public void Cheer()
    {
        if (_cheerClips == null || _cheerClips.Length == 0)
        {
            return;
        }

        var clip = _cheerClips[PickClipIndex()];
        var excitement = _engagement != null
            ? Mathf.InverseLerp(_cheerEngagement, 1.0f, _engagement.Engagement)
            : 1.0f;

        _source.Stop();
        _source.clip = clip;
        _source.pitch = Random.Range(_pitchRange.x, _pitchRange.y);
        _source.volume = Mathf.Lerp(_volumeRange.x, _volumeRange.y, excitement);
        _source.Play();

        _nextCheerAllowedTime = Time.time + clip.length / Mathf.Max(0.01f, _source.pitch) + _cooldown;
    }

    // Random, but never the same clip twice in a row
    private int PickClipIndex()
    {
        if (_cheerClips.Length == 1)
        {
            return 0;
        }

        int index;
        if (_lastClipIndex < 0)
        {
            index = Random.Range(0, _cheerClips.Length);
        }
        else
        {
            // Pick from the other clips by skipping over the last one
            index = Random.Range(0, _cheerClips.Length - 1);
            if (index >= _lastClipIndex)
            {
                index++;
            }
        }
        _lastClipIndex = index;
        return index;
    }
}
