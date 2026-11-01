using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Pretty prints a .savrcr recording to the console, like Tools/print_recording.py
public static class RecordingPrinter
{
    private const string LastFolderKey = "RecordingPrinter.LastFolder";
    // The console gets slow and cuts off very long entries, so events are split over several logs
    private const int EventsPerLog = 150;

    [MenuItem("Tools/Recording/Print Recording...")]
    public static void PrintWithoutPoses()
    {
        PickAndPrint(false);
    }

    [MenuItem("Tools/Recording/Print Recording With Poses...")]
    public static void PrintWithPoses()
    {
        PickAndPrint(true);
    }

    private static void PickAndPrint(bool includePoses)
    {
        var folder = EditorPrefs.GetString(LastFolderKey, Application.persistentDataPath);
        var path = EditorUtility.OpenFilePanel("Print recording", folder, "savrcr");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path));
        Print(path, includePoses);
    }

    public static void Print(string path, bool includePoses)
    {
        Recording recording;
        try
        {
            recording = Recording.Load(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"Couldn't read recording {path}\n{e}");
            return;
        }

        Debug.Log(DescribeSummary(path, recording));

        var shownEvents = recording.Events.Where(e => includePoses || e.Type != RecordedEventType.Pose).ToList();
        for (var start = 0; start < shownEvents.Count; start += EventsPerLog)
        {
            var end = Mathf.Min(start + EventsPerLog, shownEvents.Count);
            var log = new StringBuilder($"<b>{Path.GetFileName(path)}</b>  events {start + 1}-{end} of {shownEvents.Count}\n");
            for (var i = start; i < end; i++)
            {
                var e = shownEvents[i];
                log.Append(FormatTime(e.Time)).Append("  ").Append(FormatName(e.Type)).Append("  ").AppendLine(Describe(e));
            }
            Debug.Log(log.ToString());
        }

        if (recording.IsMalformed)
        {
            Debug.LogWarning($"{Path.GetFileName(path)} ends early or has a broken block, events after that point are missing");
        }
    }

    private static string DescribeSummary(string path, Recording recording)
    {
        var header = recording.Header;
        var events = recording.Events;
        var duration = events.Count > 0 ? events[events.Count - 1].Time : 0.0;

        var summary = new StringBuilder($"<b>Recording {Path.GetFileName(path)}</b>\n");
        summary.AppendLine($"Format {header.FormatVersion}, scene {header.SceneName}, started {header.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        summary.AppendLine($"Pose rate {header.PoseSampleRate:0.#} Hz, duration {FormatTime(duration)}, {events.Count} events, {new FileInfo(path).Length / 1024.0:0.0} KB");

        var counts = new Dictionary<RecordedEventType, int>();
        foreach (var e in events)
        {
            counts.TryGetValue(e.Type, out var count);
            counts[e.Type] = count + 1;
        }
        foreach (var pair in counts.OrderByDescending(pair => pair.Value))
        {
            summary.AppendLine($"    {pair.Key}: {pair.Value}");
        }
        return summary.ToString();
    }

    private static string Describe(RecordedEvent recordedEvent) => recordedEvent switch
    {
        PoseEvent pose => $"{pose.Device}  pos {pose.Pose.Position.ToString("F3")}  rot {pose.Pose.Rotation.ToString("F3")}",
        SongStartedEvent songStarted => $"track '{songStarted.TrackName}'  song time {songStarted.SongTime:0.000}s",
        SongFinishedEvent => "",
        NoteHitEvent noteHit => $"lane {noteHit.Lane}  note #{noteHit.NoteRecordIndex}  " +
                                $"{Mathf.Abs(noteHit.Offset) * 1000.0f:0.0}ms {(noteHit.Offset > 0.0f ? "late" : "early")}  accuracy {noteHit.Accuracy:0.00}",
        NoteMissedEvent noteMissed => $"lane {noteMissed.Lane}  note #{noteMissed.NoteRecordIndex}",
        EmptyPressEvent emptyPress => $"lane {emptyPress.Lane}",
        LaneLayoutChangedEvent layout => $"hit line height {layout.HitLineHeight:0.000}  horizontal factor {layout.HorizontalHeightFactor:0.000}",
        LaneDepthChangedEvent depth => $"hit line depth {depth.HitLineDepth:0.000}",
        AudienceMemberJoinedEvent joined => $"id {joined.MemberId}  prefab {joined.PrefabIndex}  pos {joined.LocalPosition.ToString("F2")}  " +
                                            $"threshold {joined.EngagementThreshold:0.00}  amplitude {joined.MaxAmplitude:0.00}{(joined.PoppedIn ? "  (popped in)" : "")}",
        AudienceMemberLeftEvent left => $"id {left.MemberId}",
        EngagementChangedEvent engagement => $"{engagement.Engagement:0.000}  [{new string('#', Mathf.RoundToInt(engagement.Engagement * 20.0f)).PadRight(20, '.')}]",
        CrowdCheeredEvent cheered => $"clip {cheered.ClipIndex}  pitch {cheered.Pitch:0.00}  volume {cheered.Volume:0.00}",
        MoshPitStartedEvent => "",
        MoshPitEndedEvent => "",
        _ => $"(no description for {recordedEvent.GetType().Name})"
    };

    private static string FormatName(RecordedEventType type) => type switch
    {
        RecordedEventType.NoteHit => $"<color=#4CAF50>{type}</color>",
        RecordedEventType.NoteMissed => $"<color=#E53935>{type}</color>",
        RecordedEventType.EmptyPress => $"<color=#FB8C00>{type}</color>",
        RecordedEventType.SongStarted or RecordedEventType.SongFinished => $"<b>{type}</b>",
        _ => type.ToString()
    };

    private static string FormatTime(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");
}
