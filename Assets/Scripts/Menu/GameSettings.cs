using UnityEngine;
using UnityEngine.Rendering;

public class GameSettings : MonoBehaviour
{
    [SerializeField] private CurvedSlider _darknessSlider;
    [SerializeField] private CurvedSlider _lightBrightnessSlider;
    [SerializeField] private CurvedSlider _NoteHitBoxCheckBox;
    [SerializeField] private CurvedSlider _volumeSlider;
    [SerializeField, Range(0.0f, 1.0f)] private float _maxDarkness = 0.9f;
    [SerializeField] private Vector2 _lightSpeedRange = new Vector2(1.0f, 8.0f);

    private const string DarknessKey = "Settings.Darkness";
    private const string LightBrightnessKey = "Settings.LightBrightness";
    private const string VolumeKey = "Settings.Volume";

    private SphericalHarmonicsL2 _originalAmbientProbe;
    private float _originalReflectionIntensity;

    private void Awake()
    {
        _originalAmbientProbe = RenderSettings.ambientProbe;
        _originalReflectionIntensity = RenderSettings.reflectionIntensity;

        _darknessSlider.Value = PlayerPrefs.GetFloat(DarknessKey, _darknessSlider.Value);
        _lightBrightnessSlider.Value = PlayerPrefs.GetFloat(LightBrightnessKey, _lightBrightnessSlider.Value);
        _volumeSlider.Value = PlayerPrefs.GetFloat(VolumeKey, _volumeSlider.Value);
    }

    private void Start()
    {
        ApplyToScene();
    }

    private void OnEnable()
    {
        _darknessSlider.ValueChanged += OnSliderChanged;
        _lightBrightnessSlider.ValueChanged += OnSliderChanged;
        _volumeSlider.ValueChanged += OnSliderChanged;
    }

    private void OnDisable()
    {
        _darknessSlider.ValueChanged -= OnSliderChanged;
        _lightBrightnessSlider.ValueChanged -= OnSliderChanged;
        _volumeSlider.ValueChanged -= OnSliderChanged;
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            PlayerPrefs.Save();
        }
    }

    private void OnSliderChanged(float value)
    {
        ApplyToScene();
        PlayerPrefs.SetFloat(DarknessKey, _darknessSlider.Value);
        PlayerPrefs.SetFloat(LightBrightnessKey, _lightBrightnessSlider.Value);
        PlayerPrefs.SetFloat(VolumeKey, _volumeSlider.Value);
    }

    private void ApplyToScene()
    {
        var brightness = _maxDarkness * _darknessSlider.Value;
        RenderSettings.ambientProbe = _originalAmbientProbe * brightness;
        RenderSettings.reflectionIntensity = _originalReflectionIntensity * brightness;
        AudioListener.volume = _volumeSlider.Value;

        var lightBrightness = 1.0f - _maxDarkness * _lightBrightnessSlider.Value;
        

        //var lightSpeed = Mathf.Lerp(_lightSpeedRange.x, _lightSpeedRange.y, _lightSpeedSlider.Value);
        //foreach (var lamp in FindObjectsByType<LampMove>(FindObjectsSortMode.None))
        //{
        //    lamp.speed = lightSpeed;
        //}
    }
}
