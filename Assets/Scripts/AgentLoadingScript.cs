using UnityEngine;

[CreateAssetMenu(menuName = "Game/Agent Loading Script", fileName = "New Agent Loading Script")]
public class AgentLoadingScript : ScriptableObject
{
    [SerializeField] private string _agentName = "spooteefy.ai";
    [SerializeField] private Color _agentNameColor = new Color(0.12f, 0.84f, 0.38f, 1.0f);
    [SerializeField] private string[] _steps =
    {
        "generating songs for your show...",
        "determining optimal order...",
        "performing the soundcheck for you...",
        "warming up the crowd...",
        "aiming the spotlights...",
    };
    [SerializeField] private string _audienceStep = "gathering you an audience...";
    [SerializeField] private float _audienceStepDuration = 6.0f;
    [SerializeField] private string _doneMessage = "all set. enjoy your show!";

    [Header("Progress")]
    [SerializeField] private Vector2 _burstSizeRange = new Vector2(0.03f, 0.35f);
    [SerializeField] private Vector2 _burstSpeedRange = new Vector2(0.2f, 1.5f);
    [SerializeField, Range(0.0f, 1.0f)] private float _stallChance = 0.35f;
    [SerializeField] private Vector2 _stallDurationRange = new Vector2(0.2f, 1.2f);
    [SerializeField] private float _width = 1200.0f;
    [SerializeField] private float _barHeight = 30.0f;
    [SerializeField] private Color _barBackgroundColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);

    public string AgentName => _agentName;
    public Color AgentNameColor => _agentNameColor;
    public string[] Steps => _steps;
    public string AudienceStep => _audienceStep;
    public float AudienceStepDuration => _audienceStepDuration;
    public string DoneMessage => _doneMessage;
    public Vector2 BurstSizeRange => _burstSizeRange;
    public Vector2 BurstSpeedRange => _burstSpeedRange;
    public float StallChance => _stallChance;
    public Vector2 StallDurationRange => _stallDurationRange;
    public float Width => _width;
    public float BarHeight => _barHeight;
    public Color BarBackgroundColor => _barBackgroundColor;
}
