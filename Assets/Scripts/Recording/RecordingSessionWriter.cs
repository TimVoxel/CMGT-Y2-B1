using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RecordingSessionWriter : MonoBehaviour
{
    public const string Extension = "savrcr"; // short for sickass VR concert recording 
    private const int WriteAttemptRateMillis = 5;
    private const int FlushRateMillis = 2000;

    [SerializeField] private ConcertRecorder _recorder;
    [SerializeField] private string _outputSubPath;

    public string OutputDir => Path.Combine(Application.persistentDataPath, _outputSubPath);

    private Task _writeTask;

    public string LastRecordingPath { get; private set; }
    public bool IsWriting => _writeTask != null && !_writeTask.IsCompleted;

    public string FindLatestRecordingPath()
    {
        if (!Directory.Exists(OutputDir))
        {
            return null;
        }
        return new DirectoryInfo(OutputDir)
            .GetFiles($"*.{Extension}")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private void OnEnable()
    {
        _recorder.RecordingStarted += StartWrite;
    }

    private void OnDisable()
    {
        _recorder.RecordingStarted -= StartWrite;
    }

    private void StartWrite(RecordingSession session)
    {
        Directory.CreateDirectory(OutputDir);
        var outputFilePath = Path.Combine(OutputDir, $"{session.Header.StartedAtUtc.ToString("yyyy-MM-dd_HH-mm-ss")}.{Extension}");
        LastRecordingPath = outputFilePath;

        _writeTask = Task.Run(() =>
        {
            try
            {
                using var fileStream = File.Create(outputFilePath);
                WriteHeader(fileStream, session.Header);
                WriteQueue(fileStream, session);   
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        });
    }

    private void WriteQueue(FileStream file, RecordingSession session)
    {
        var nextFlushTime = DateTime.UtcNow.AddMilliseconds(FlushRateMillis); 
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        void FlushBlock()
        {
            writer.Flush();
            if (memoryStream.Length == 0)
            {
                return;
            }
            var compressed = Compress(memoryStream.GetBuffer(), (int) memoryStream.Length);
            memoryStream.SetLength(0);
            file.Write(BitConverter.GetBytes(compressed.Length));
            file.Write(compressed);
            file.Flush();
        }

        while (true)
        {
            while (session.Events.TryDequeue(out var e))
            {
                WriteEvent(writer, e);
            }
            if (session.IsFinished && session.Events.IsEmpty)
            {
                break;
            }
            if (DateTime.UtcNow >= nextFlushTime)
            {
                FlushBlock();
                nextFlushTime = DateTime.UtcNow.AddMilliseconds(FlushRateMillis);
            }
            Thread.Sleep(WriteAttemptRateMillis);
        }
        FlushBlock();
    }

    private void WriteEvent(BinaryWriter writer, RecordedEvent e)
    {
        var eventId = (byte) e.Type;
        writer.Write(eventId);
        writer.Write(e.Time);

        switch (e)
        {
            case PoseEvent pose:
                writer.Write((byte) pose.Device);
                writer.Write(pose.Pose.Position);
                writer.Write(pose.Pose.Rotation);
                break;

            case SongStartedEvent songStarted:
                writer.Write(songStarted.TrackName);
                writer.Write(songStarted.SongTime);
                break;

            case NoteHitEvent noteHit:
                writer.Write(noteHit.Lane);
                writer.Write(noteHit.NoteRecordIndex);
                writer.Write(noteHit.Offset);
                writer.Write(noteHit.Accuracy);
                break;
     
            case NoteMissedEvent noteMissed:
                writer.Write(noteMissed.Lane);
                writer.Write(noteMissed.NoteRecordIndex);
                break;

            case EmptyPressEvent emptyPress:
                writer.Write(emptyPress.Lane);
                break;

            case LaneLayoutChangedEvent laneLayoutChanged:
                writer.Write(laneLayoutChanged.HitLineHeight);
                writer.Write(laneLayoutChanged.HorizontalHeightFactor);
                break;

            case LaneDepthChangedEvent laneDepthChanged:
                writer.Write(laneDepthChanged.HitLineDepth);
                break;

            case AudienceMemberJoinedEvent memberJoined:
                writer.Write(memberJoined.MemberId);
                writer.Write(memberJoined.PrefabIndex);
                writer.Write(memberJoined.LocalPosition);
                writer.Write(memberJoined.LocalRotation);
                writer.Write(memberJoined.EngagementThreshold);
                writer.Write(memberJoined.MaxAmplitude);
                writer.Write(memberJoined.PoppedIn);
                break;

            case AudienceMemberLeftEvent memberLeft:
                writer.Write(memberLeft.MemberId);
                break;
            
            case EngagementChangedEvent engagementChanged:
                writer.Write(engagementChanged.Engagement);
                break;
                
            case CrowdCheeredEvent crowdCheered:
                writer.Write(crowdCheered.ClipIndex);
                writer.Write(crowdCheered.Pitch);
                writer.Write(crowdCheered.Volume);
                break;

            case SongFinishedEvent:
            case MoshPitStartedEvent:
            case MoshPitEndedEvent:
                break;

            default:
                throw new Exception($"Unexpected recorded event: {e.GetType()}");
        }
    }

    private void OnApplicationQuit()
    {
        _recorder.StopRecording();
        _writeTask?.Wait(TimeSpan.FromSeconds(2));
    }

    private void WriteHeader(FileStream fileStream, RecordingHeader header)
    {
        using var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true);
        writer.Write(header.FormatVersion);
        writer.Write(header.SceneName);
        writer.Write(header.StartedAtUtc.ToBinary());
        writer.Write(header.PoseSampleRate);
    }

    private static byte[] Compress(byte[] bytes, int count)
    {
        using var output = new MemoryStream();
        using (var deflater = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            deflater.Write(bytes, 0, count);
        }
        return output.ToArray();
    }
}