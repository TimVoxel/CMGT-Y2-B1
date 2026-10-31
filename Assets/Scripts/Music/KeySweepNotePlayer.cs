using UnityEngine;

public class KeySweepNotePlayer : MonoBehaviour
{
    [SerializeField] private KeySweepDetector _keySweep;
    [SerializeField] private AudioClip[] _notesLeftToRight;
    [SerializeField, Range(0.0f, 1.0f)] private float _volume = 0.65f;

    private AudioSource _source;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
    }

    private void OnEnable()
    {
        _keySweep.ProgressChanged += OnProgressChanged;
    }

    private void OnDisable()
    {
        _keySweep.ProgressChanged -= OnProgressChanged;
    }

    private void OnProgressChanged(int poked, int total)
    {
        var noteIndex = poked - 1;
        if (noteIndex >= 0 && noteIndex < _notesLeftToRight.Length)
        {
            _source.PlayOneShot(_notesLeftToRight[noteIndex], _volume);
        }
    }
}
