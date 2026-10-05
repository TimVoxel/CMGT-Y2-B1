using UnityEngine;

public class TrackNote : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    private float _lifetime;
    private double _hitTime;
    private Vector3 _startPos;
    private Vector3 _endPos;

    public float Lifetime => _lifetime;
    public double HitTime => _hitTime;
    public int NoteIndex { get; private set; }
    public float TimeLeft { get; private set; }

    public void Init(int noteIndex, Vector3 endPos, double hitTime, float lifetime, Material material)
    {
        NoteIndex = noteIndex;
        _lifetime = lifetime;
        _hitTime = hitTime;
        _startPos = transform.localPosition;
        _endPos = endPos;
        _renderer.material = material;
    }

    // Driven by TrackPlayer so every note follows the same song clock
    public void SetSongTime(double songTime)
    {
        TimeLeft = (float)(_hitTime - songTime);
        transform.localPosition = Vector3.Lerp(_endPos, _startPos, TimeLeft / Mathf.Max(1e-6f, _lifetime));
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }
}
