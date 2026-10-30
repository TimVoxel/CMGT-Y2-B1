using System.Collections.Generic;
using UnityEngine;

public class AudioSourcePool : MonoBehaviour
{
    [SerializeField] private int _initialSize = 4;
    [SerializeField, Range(0.0f, 1.0f)] private float _spatialBlend = 1.0f;
    [SerializeField] private float _minDistance = 5.0f;
    [SerializeField] private float _maxDistance = 40.0f;

    private readonly List<AudioSource> _sources = new List<AudioSource>();

    private void Awake()
    {
        for (var i = 0; i < _initialSize; i++)
        {
            CreateSource();
        }
    }

    public void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        var source = GetFreeSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
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
        return CreateSource();
    }

    private AudioSource CreateSource()
    {
        var sourceObject = new GameObject($"Pooled Audio Source {_sources.Count}");
        sourceObject.transform.SetParent(transform, false);

        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = _spatialBlend;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;

        _sources.Add(source);
        return source;
    }
}
