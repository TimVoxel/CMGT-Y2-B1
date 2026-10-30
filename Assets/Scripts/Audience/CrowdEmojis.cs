using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// TikTok-live style emojis floating up out of the crowd. Only members into the show send them, more often and happier
// the more engaged the crowd is. A long miss streak makes a few members skeptical, but only a few.
public class CrowdEmojis : MonoBehaviour
{
    [Serializable]
    private class MoodTier
    {
        [SerializeField, Range(0.0f, 1.0f)] private float _minEngagement;
        [SerializeField] private bool _hasMaxEngagement;
        [SerializeField, Range(0.0f, 1.0f)] private float _maxEngagement = 1.0f;
        [SerializeField] private Sprite[] _sprites;

        public Sprite[] Sprites => _sprites;

        public bool Contains(float engagement)
        {
            return engagement >= _minEngagement && (!_hasMaxEngagement || engagement <= _maxEngagement);
        }
    }

    private class Emoji
    {
        public Transform Root;
        public SpriteRenderer Renderer;
        public Vector3 Origin;
        public float Age;
        public float Lifetime;
        public float RiseSpeed;
        public float SwayPhase;
        public float Size;
        public float Tilt;
    }

    private class Spam
    {
        public AudienceMember Member;
        public Sprite Sprite;
        public int Remaining;
        public float UntilNext;
    }

    [SerializeField] private AudienceSpawner _spawner;
    [SerializeField] private EngagementHolder _engagement;
    [SerializeField] private TrackPlayer _trackPlayer;

    [Header("Mood")]
    // Tiers may overlap: emojis are picked from every tier whose range holds the current engagement
    [SerializeField] private MoodTier[] _tiers;
    [SerializeField] private AnimationCurve _emojisPerSecond = new AnimationCurve(
        new Keyframe(0.0f, 0.0f), new Keyframe(0.2f, 0.0f), new Keyframe(1.0f, 6.0f));

    [Header("Spam")]
    // Chance per mood emoji that its sender keeps tapping the same one
    [SerializeField] private AnimationCurve _spamChance = new AnimationCurve(
        new Keyframe(0.0f, 0.0f), new Keyframe(0.4f, 0.02f), new Keyframe(1.0f, 0.15f));
    [SerializeField] private Vector2Int _spamCountRange = new Vector2Int(5, 15);
    [SerializeField] private Vector2 _spamIntervalRange = new Vector2(0.05f, 0.15f);
    [SerializeField] private float _spamSpread = 0.15f;
    [SerializeField] private int _maxSpams = 3;

    [Header("Excited by being pointed at")]
    [SerializeField] private Vector2Int _excitedBurstCountRange = new Vector2Int(3, 6);

    [Header("Skeptics")]
    [SerializeField] private Sprite[] _skepticalSprites;
    [SerializeField] private int _missStreakForSkeptics = 5;
    [SerializeField] private Vector2Int _skepticCountRange = new Vector2Int(1, 2);
    [SerializeField] private float _skepticCooldown = 8.0f;

    [Header("Look")]
    [SerializeField] private int _maxEmojis = 128;
    [SerializeField] private float _size = 0.35f;
    [SerializeField] private Vector2 _sizeVariation = new Vector2(0.8f, 1.2f);
    [SerializeField] private float _heightAboveHead = 0.15f;
    [SerializeField] private Vector2 _lifetimeRange = new Vector2(1.6f, 2.4f);
    [SerializeField] private Vector2 _riseSpeedRange = new Vector2(0.5f, 0.9f);
    [SerializeField] private float _swayAmplitude = 0.12f;
    [SerializeField] private float _swayFrequency = 1.2f;
    [SerializeField] private float _maxTilt = 20.0f;
    [SerializeField] private float _popDuration = 0.25f;
    [SerializeField] private float _popOvershoot = 1.7f;
    [SerializeField, Range(0.0f, 1.0f)] private float _fadeStart = 0.6f;

    private readonly List<Emoji> _active = new List<Emoji>();
    private readonly Stack<Emoji> _idle = new Stack<Emoji>();
    private readonly List<AudienceMember> _candidates = new List<AudienceMember>();
    private readonly List<Sprite> _moodSprites = new List<Sprite>();
    private readonly List<Spam> _spams = new List<Spam>();
    private float _untilNextEmoji;
    private int _missStreak;
    private float _skepticsAllowedTime;

    private void Awake()
    {
        for (var i = 0; i < _maxEmojis; i++)
        {
            _idle.Push(CreateEmoji(i));
        }
        _untilNextEmoji = NextInterval();
    }

    private void OnEnable()
    {
        _trackPlayer.NoteHit += OnNoteHit;
        _trackPlayer.NoteMissed += OnNoteMissed;
    }

    private void OnDisable()
    {
        _trackPlayer.NoteHit -= OnNoteHit;
        _trackPlayer.NoteMissed -= OnNoteMissed;
    }

    private Emoji CreateEmoji(int index)
    {
        var root = new GameObject($"Emoji {index}").transform;
        root.SetParent(transform, false);
        var spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(root, false);
        root.gameObject.SetActive(false);
        return new Emoji { Root = root, Renderer = spriteObject.AddComponent<SpriteRenderer>() };
    }

    private void Update()
    {
        EmitMood(Time.deltaTime);
        EmitSpams(Time.deltaTime);
        Animate(Time.deltaTime);
    }

    // Poisson process, so emojis come in natural clumps and gaps instead of a steady drip
    private static float NextInterval()
    {
        return -Mathf.Log(Mathf.Max(1e-6f, 1.0f - Random.value));
    }

    private void EmitMood(float deltaTime)
    {
        var rate = _emojisPerSecond.Evaluate(_engagement.Engagement);
        if (rate <= 0.0f)
        {
            return;
        }

        _untilNextEmoji -= rate * deltaTime;
        while (_untilNextEmoji <= 0.0f)
        {
            _untilNextEmoji += NextInterval();
            CollectMoodSprites();
            var member = PickEngagedMember();
            if (_moodSprites.Count > 0 && member != null)
            {
                var sprite = _moodSprites[Random.Range(0, _moodSprites.Count)];
                Spawn(member, sprite);
                if (_spams.Count < _maxSpams && Random.value < _spamChance.Evaluate(_engagement.Engagement))
                {
                    _spams.Add(new Spam
                    {
                        Member = member,
                        Sprite = sprite,
                        Remaining = Random.Range(_spamCountRange.x, _spamCountRange.y + 1),
                        UntilNext = Random.Range(_spamIntervalRange.x, _spamIntervalRange.y),
                    });
                }
            }
        }
    }

    private void EmitSpams(float deltaTime)
    {
        for (var i = _spams.Count - 1; i >= 0; i--)
        {
            var spam = _spams[i];
            spam.UntilNext -= deltaTime;
            while (spam.UntilNext <= 0.0f && spam.Remaining > 0 && spam.Member != null)
            {
                Spawn(spam.Member, spam.Sprite, _spamSpread);
                spam.Remaining--;
                spam.UntilNext += Random.Range(_spamIntervalRange.x, _spamIntervalRange.y);
            }

            if (spam.Remaining <= 0 || spam.Member == null)
            {
                _spams.RemoveAt(i);
            }
        }
    }

    public void SendExcitedBurst(AudienceMember member)
    {
        CollectMoodSprites(1.0f);
        if (_moodSprites.Count == 0)
        {
            return;
        }

        var sprite = _moodSprites[Random.Range(0, _moodSprites.Count)];
        Spawn(member, sprite);
        _spams.Add(new Spam
        {
            Member = member,
            Sprite = sprite,
            Remaining = Random.Range(_excitedBurstCountRange.x, _excitedBurstCountRange.y + 1),
            UntilNext = Random.Range(_spamIntervalRange.x, _spamIntervalRange.y),
        });
    }

    private void CollectMoodSprites()
    {
        CollectMoodSprites(_engagement.Engagement);
    }

    private void CollectMoodSprites(float engagement)
    {
        _moodSprites.Clear();
        foreach (var tier in _tiers)
        {
            if (tier.Contains(engagement) && tier.Sprites != null)
            {
                _moodSprites.AddRange(tier.Sprites);
            }
        }
    }

    private AudienceMember PickEngagedMember()
    {
        _candidates.Clear();
        foreach (var member in _spawner.Members)
        {
            if (_engagement.Engagement >= member.EngagementThreshold)
            {
                _candidates.Add(member);
            }
        }
        return _candidates.Count > 0 ? _candidates[Random.Range(0, _candidates.Count)] : null;
    }

    private void OnNoteHit(TrackNote note, float accuracy)
    {
        _missStreak = 0;
    }

    private void OnNoteMissed(TrackNote note)
    {
        _missStreak++;
        if (_missStreak < _missStreakForSkeptics || Time.time < _skepticsAllowedTime)
        {
            return;
        }

        _missStreak = 0;
        _skepticsAllowedTime = Time.time + _skepticCooldown;
        EmitSkeptics();
    }

    private void EmitSkeptics()
    {
        var members = _spawner.Members;
        if (_skepticalSprites == null || _skepticalSprites.Length == 0 || members.Count == 0)
        {
            return;
        }

        var count = Random.Range(_skepticCountRange.x, _skepticCountRange.y + 1);
        for (var i = 0; i < count; i++)
        {
            var member = members[Random.Range(0, members.Count)];
            Spawn(member, _skepticalSprites[Random.Range(0, _skepticalSprites.Length)]);
        }
    }

    private void Spawn(AudienceMember member, Sprite sprite, float spread = 0.0f)
    {
        if (_idle.Count == 0)
        {
            return;
        }

        var memberRenderer = member.GetComponentInChildren<Renderer>();
        var memberPosition = member.transform.position;
        var headHeight = memberRenderer != null ? memberRenderer.bounds.max.y : memberPosition.y;

        var emoji = _idle.Pop();
        var scatter = Random.insideUnitCircle * spread;
        emoji.Origin = new Vector3(memberPosition.x + scatter.x, headHeight + _heightAboveHead, memberPosition.z + scatter.y);
        emoji.Age = 0.0f;
        emoji.Lifetime = Random.Range(_lifetimeRange.x, _lifetimeRange.y);
        emoji.RiseSpeed = Random.Range(_riseSpeedRange.x, _riseSpeedRange.y);
        emoji.SwayPhase = Random.Range(0.0f, 2.0f * Mathf.PI);
        emoji.Size = _size * Random.Range(_sizeVariation.x, _sizeVariation.y);
        emoji.Tilt = Random.Range(-_maxTilt, _maxTilt);

        // Normalize to one unit tall and centered, whatever the sprite's pixels per unit and pivot
        var bounds = sprite.bounds;
        var normalize = 1.0f / Mathf.Max(1e-6f, bounds.size.y);
        emoji.Renderer.sprite = sprite;
        emoji.Renderer.color = Color.white;
        emoji.Renderer.transform.localScale = Vector3.one * normalize;
        emoji.Renderer.transform.localPosition = -bounds.center * normalize;

        emoji.Root.gameObject.SetActive(true);
        _active.Add(emoji);
        Place(emoji, Camera.main);
    }

    private void Animate(float deltaTime)
    {
        var viewer = Camera.main;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var emoji = _active[i];
            emoji.Age += deltaTime;
            if (emoji.Age >= emoji.Lifetime)
            {
                emoji.Root.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _idle.Push(emoji);
                continue;
            }
            Place(emoji, viewer);
        }
    }

    private void Place(Emoji emoji, Camera viewer)
    {
        var swayAngle = emoji.SwayPhase + emoji.Age * _swayFrequency * 2.0f * Mathf.PI;
        var sway = Mathf.Sin(swayAngle);
        var right = viewer != null ? viewer.transform.right : Vector3.right;
        var position = emoji.Origin + Vector3.up * (emoji.RiseSpeed * emoji.Age) + right * (sway * _swayAmplitude);

        var facing = viewer != null && position != viewer.transform.position
            ? Quaternion.LookRotation(position - viewer.transform.position)
            : Quaternion.identity;

        emoji.Root.SetPositionAndRotation(position, facing * Quaternion.Euler(0.0f, 0.0f, emoji.Tilt + sway * _maxTilt * 0.5f));
        emoji.Root.localScale = Vector3.one * (emoji.Size * PopScale(emoji.Age / Mathf.Max(1e-4f, _popDuration)));

        var color = emoji.Renderer.color;
        color.a = 1.0f - Mathf.InverseLerp(_fadeStart, 1.0f, emoji.Age / emoji.Lifetime);
        emoji.Renderer.color = color;
    }

    // Back-out easing: grows past full size, then settles
    private float PopScale(float progress)
    {
        if (progress >= 1.0f)
        {
            return 1.0f;
        }
        var x = progress - 1.0f;
        return 1.0f + (_popOvershoot + 1.0f) * x * x * x + _popOvershoot * x * x;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _spawner = FindFirstObjectByType<AudienceSpawner>();
        _engagement = FindFirstObjectByType<EngagementHolder>();
        _trackPlayer = FindFirstObjectByType<TrackPlayer>();
    }
#endif
}
