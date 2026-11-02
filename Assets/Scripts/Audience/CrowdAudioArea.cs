using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class CrowdAudioArea
{
    [SerializeField] private Vector3 _worldCenter = new Vector3(2.4f, 1.05f, 20.64f);
    [SerializeField] private Vector3 _extents = new Vector3(8.0f, 0.5f, 3.0f);

    public Vector3 PickPoint()
    {
        return _worldCenter + new Vector3(
            Random.Range(-_extents.x, _extents.x),
            Random.Range(-_extents.y, _extents.y),
            Random.Range(-_extents.z, _extents.z));
    }

    public AudioSource CreateSource(Transform parent, string name)
    {
        var sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(parent, false);
        sourceObject.transform.position = PickPoint();
        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        return source;
    }
}
