using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public readonly struct GigComment
{
    public GigComment(string username, string text, int likes)
    {
        Username = username;
        Text = text;
        Likes = likes;
    }

    public string Username { get; }
    public string Text { get; }
    public int Likes { get; }
}

// Made-up audience comments for the end screen. Usernames are glued together from parts, like default online names.
[CreateAssetMenu(menuName = "Game/Gig Comments", fileName = "New Gig Comments")]
public class GigComments : ScriptableObject
{
    [Serializable]
    private class Comment
    {
        [SerializeField, TextArea] private string _text;
        [SerializeField] private int _priority;

        public string Text => _text;
        public int Priority => _priority;
    }

    [SerializeField] private string[] _usernameAdjectives;
    [SerializeField] private string[] _usernameNouns;
    [SerializeField, Range(0.0f, 1.0f)] private float _usernameNumberChance = 0.7f;
    [SerializeField] private Vector2Int _usernameNumberRange = new Vector2Int(1, 9999);
    [SerializeField] private Comment[] _comments;
    [SerializeField, Min(0.0f)] private float _priorityShuffle = 3.0f;
    [FormerlySerializedAs("_commentsShown")] [SerializeField] private int _commentsPerPage = 4;

    [Header("Likes")]
    [SerializeField] private int _maxLikes = 5000;
    [SerializeField, Min(1.0f)] private float _likesSkew = 3.0f;

    public int CommentsPerPage => Mathf.Max(1, _commentsPerPage);

    public List<GigComment> GenerateComments()
    {
        var ordered = _comments
            .Select(comment => (comment, rank: comment.Priority + Random.Range(0.0f, _priorityShuffle)))
            .OrderByDescending(entry => entry.rank)
            .Select(entry => entry.comment)
            .ToList();

        var likes = ordered.Select(_ => GenerateLikes()).OrderByDescending(count => count).ToList();
        var comments = new List<GigComment>();
        for (var i = 0; i < ordered.Count; i++)
        {
            comments.Add(new GigComment(GenerateUsername(), ordered[i].Text, likes[i]));
        }
        return comments;
    }

    private string GenerateUsername()
    {
        var username = Pick(_usernameAdjectives) + Pick(_usernameNouns);
        if (Random.value < _usernameNumberChance)
        {
            username += Random.Range(_usernameNumberRange.x, _usernameNumberRange.y + 1);
        }
        return username;
    }

    private int GenerateLikes()
    {
        return Mathf.FloorToInt(_maxLikes * Mathf.Pow(Random.value, _likesSkew));
    }

    private static string Pick(string[] options)
    {
        return options == null || options.Length == 0 ? "" : options[Random.Range(0, options.Length)];
    }
}
