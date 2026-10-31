using System.Collections.Generic;
using UnityEngine;

// Whole-crowd cheering. Cheers on its own when engagement first reaches the threshold, then now and again while it
// stays up there, never over a cheer still playing. A requested cheer plays alongside one still playing instead of cutting it off.
public class CrowdCheer : MonoBehaviour
{
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    [SerializeField] private AudioClip[] _cheerClips;
    [SerializeField] private AudioSource _source;

    [SerializeField, Range(0.0f, 1.0f)] private float _cheerEngagement = 0.85f;
    [SerializeField] private bool _cheerOnReachingThreshold = true;
    [SerializeField] private float _cheerChancePerSecond = 0.1f;
    [SerializeField] private float _cooldown = 3.0f;
    [SerializeField] private Vector2 _volumeRange = new Vector2(0.15f, 1.0f);
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private bool _cheersOnItsOwn = true;

    private float _nextCheerAllowedTime;
    private int _lastClipIndex = -1;
    private bool _wasAboveThreshold;
    private readonly List<AudioSource> _sources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, float> _targetVolumes = new Dictionary<AudioSource, float>();

    public float CheerEngagement => _cheerEngagement;

    // clip index, pitch, volume
    public event System.Action<int, float, float> Cheered;

    private void Awake()
    {
        if (_source == null && !TryGetComponent(out _source))
        {
            _source = gameObject.AddComponent<AudioSource>();
        }
        _source.playOnAwake = false;
        _source.loop = false;
        _sources.Add(_source);
    }

    private bool IsAnyCheerPlaying()
    {
        foreach (var source in _sources)
        {
            if (source.isPlaying)
            {
                return true;
            }
        }
        return false;
    }

    private AudioSource GetFreeSource()
    {
        foreach (var source in _sources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        var extraSource = gameObject.AddComponent<AudioSource>();
        extraSource.playOnAwake = false;
        extraSource.loop = false;
        extraSource.outputAudioMixerGroup = _source.outputAudioMixerGroup;
        extraSource.spatialBlend = _source.spatialBlend;
        extraSource.rolloffMode = _source.rolloffMode;
        extraSource.minDistance = _source.minDistance;
        extraSource.maxDistance = _source.maxDistance;
        _sources.Add(extraSource);
        return extraSource;
    }

    public void CheerAtFullVolume()
    {
        if (_cheerClips == null || _cheerClips.Length == 0)
        {
            return;
        }

        Cheer(PickClipIndex(), Random.Range(_pitchRange.x, _pitchRange.y), 1.0f);
    }

    private void FadeInCheers()
    {
        foreach (var source in _sources)
        {
            if (_targetVolumes.TryGetValue(source, out var targetVolume))
            {
                var fadeStep = targetVolume * Time.deltaTime / Mathf.Max(1e-4f, _fadeInDuration);
                source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeStep);
            }
        }
    }

    private void Update()
    {
        FadeInCheers();

        if (!_cheersOnItsOwn)
        {
            return;
        }

        var isAbove = _engagement != null
            && _engagement.Engagement >= _cheerEngagement
            && _trackPlayer != null && _trackPlayer.IsPlaying;
        var justReached = isAbove && !_wasAboveThreshold;
        _wasAboveThreshold = isAbove;

        if (!isAbove || IsAnyCheerPlaying() || Time.time < _nextCheerAllowedTime)
        {
            return;
        }

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

        var engagement = _engagement != null ? _engagement.Engagement : 1.0f;
        Cheer(PickClipIndex(), Random.Range(_pitchRange.x, _pitchRange.y), Mathf.Lerp(_volumeRange.x, _volumeRange.y, engagement));
    }

    public void Cheer(int clipIndex, float pitch, float volume)
    {
        var clip = _cheerClips[clipIndex];
        var source = GetFreeSource();
        source.clip = clip;
        source.pitch = pitch;
        source.volume = 0.0f;
        _targetVolumes[source] = volume;
        source.Play();

        _lastClipIndex = clipIndex;
        Cheered?.Invoke(clipIndex, pitch, volume);

        _nextCheerAllowedTime = Time.time + clip.length / Mathf.Max(0.01f, pitch) + _cooldown;
    }

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
