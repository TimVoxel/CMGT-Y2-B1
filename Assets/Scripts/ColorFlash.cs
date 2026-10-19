using UnityEngine;

// Briefly tints a renderer, then fades back to its material's color. Used as "that was wrong" feedback.
// Uses a property block, so the material itself is never touched.
public class ColorFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _flashColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
    [SerializeField] private float _duration = 0.35f;

    private MaterialPropertyBlock _block;
    private float _elapsed;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        enabled = false;
    }

    public void Flash()
    {
        _elapsed = 0.0f;
        enabled = true;
        Apply();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration)
        {
            _renderer.SetPropertyBlock(null);
            enabled = false;
            return;
        }
        Apply();
    }

    private void Apply()
    {
        var baseColor = _renderer.sharedMaterial.GetColor(BaseColorId);
        var t = _elapsed / _duration;
        _block.SetColor(BaseColorId, Color.Lerp(_flashColor, baseColor, t * t));
        _renderer.SetPropertyBlock(_block);
    }
}
