using UnityEngine;

public class MissSoundPlayer : MonoBehaviour
{
    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private KeySweepDetector _keySweep;
    [SerializeField] private AudioClip[] _sounds;
    [SerializeField] private Vector2 _volumeRange = new Vector2(0.4f, 0.6f);
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.94f, 1.06f);

    private readonly AudioSource[] _sources = new AudioSource[3];
    private int _nextSource;
    private int _lastSoundIndex = -1;

    private void Awake()
    {
        for (var i = 0; i < _sources.Length; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();
            _sources[i].playOnAwake = false;
            _sources[i].priority = 32;
        }
    }

    private void OnEnable()
    {
        _trackPlayer.EmptyPressed += OnEmptyPressed;
        if (_keySweep != null)
        {
            _keySweep.PokeDidNotAdvance += PlaySound;
        }
    }

    private void OnDisable()
    {
        _trackPlayer.EmptyPressed -= OnEmptyPressed;
        if (_keySweep != null)
        {
            _keySweep.PokeDidNotAdvance -= PlaySound;
        }
    }

    private void OnEmptyPressed(int laneIndex)
    {
        PlaySound();
    }

    private void PlaySound()
    {
        if (_sounds == null || _sounds.Length == 0)
        {
            return;
        }
        var soundIndex = Random.Range(0, _sounds.Length);
        if (soundIndex == _lastSoundIndex)
        {
            soundIndex = (soundIndex + 1) % _sounds.Length;
        }
        _lastSoundIndex = soundIndex;

        var source = _sources[_nextSource];
        _nextSource = (_nextSource + 1) % _sources.Length;
        source.clip = _sounds[soundIndex];
        source.volume = Random.Range(_volumeRange.x, _volumeRange.y);
        source.pitch = Random.Range(_pitchRange.x, _pitchRange.y);
        source.Play();
    }
}
