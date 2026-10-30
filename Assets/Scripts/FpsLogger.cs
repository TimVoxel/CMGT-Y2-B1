using UnityEngine;

// Logs frame rate every few seconds: average, plus the worst frame since the last log so hitches don't hide in the mean.
public class FpsLogger : MonoBehaviour
{
    [SerializeField] private float _interval = 5.0f;

    private float _elapsed;
    private int _frames;
    private float _worstFrameTime;

    private void Update()
    {
        var deltaTime = Time.unscaledDeltaTime;
        _elapsed += deltaTime;
        _frames++;
        _worstFrameTime = Mathf.Max(_worstFrameTime, deltaTime);

        if (_elapsed < _interval)
        {
            return;
        }

        Debug.Log($"[FPS] avg {_frames / _elapsed:0.0}, worst frame {_worstFrameTime * 1000.0f:0.0} ms " +
                  $"({1.0f / Mathf.Max(1e-6f, _worstFrameTime):0.0} fps) over {_elapsed:0.0}s");
        _elapsed = 0.0f;
        _frames = 0;
        _worstFrameTime = 0.0f;
    }
}
