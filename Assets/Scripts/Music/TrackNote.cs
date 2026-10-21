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
    public int RecordIndex { get; private set; }
    public float TimeLeft { get; private set; }

    public void Init(int noteIndex, int recordIndex, Vector3 endPos, double hitTime, float lifetime, Material material)
    {
        NoteIndex = noteIndex;
        RecordIndex = recordIndex;
        _lifetime = lifetime;
        _hitTime = hitTime;
        _startPos = transform.localPosition;
        _endPos = endPos;
        _renderer.material = material;
    }

    public void SetPath(Vector3 startPos, Vector3 endPos)
    {
        _startPos = startPos;
        _endPos = endPos;
    }

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
