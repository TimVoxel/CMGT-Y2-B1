using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public sealed class Recording
{
    public RecordingHeader Header { get; }
    public IReadOnlyList<RecordedEvent> Events { get; }   
    public bool IsMalformed { get; }    

    public Recording(RecordingHeader header, IReadOnlyList<RecordedEvent> events, bool isMalformed)
    {
        Header = header;
        Events = events;
        IsMalformed = isMalformed;
    }

    public static Recording Load(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        var formatVersion = reader.ReadUInt16();
        var sceneName = reader.ReadString();
        var startedAtUtc = DateTime.FromBinary(reader.ReadInt64());
        var poseSampleRate = reader.ReadSingle();
        
        if (formatVersion != RecordingHeader.CurrentFormatVersion)
        {
            throw new Exception($"The recording uses an unsupported format version: {formatVersion}");
        }

        var events = new List<RecordedEvent>();
        var isMalformed = false;

        try
        {
            while (!reader.IsAtEnd())
            {
                var size = reader.ReadInt32();
                var blockCompressed = reader.ReadBytes(size);

                isMalformed = blockCompressed.Length != size;
                if (isMalformed)
                {
                    break;
                }

                var decompressed = Decompress(blockCompressed);
                var decompressedReader = new BinaryReader(decompressed);

                while (!decompressed.IsAtEnd())
                {
                    events.Add(ReadEvent(decompressedReader));
                }
            }
        }
        catch (Exception e)
        {
            isMalformed = true;
            Debug.LogException(e);
        }

        return new Recording(
            new RecordingHeader(startedAtUtc, sceneName, poseSampleRate),
            events, 
            isMalformed);
    }

    private static RecordedEvent ReadEvent(BinaryReader reader)
    {
        var eventType = (RecordedEventType) reader.ReadByte();
        var time = reader.ReadDouble();

        return eventType switch
        {
            RecordedEventType.Pose => new PoseEvent(
                time,
                (TrackedDevice) reader.ReadByte(),
                new RecordedPose(reader.ReadVector3(), reader.ReadQuaternion())),

            RecordedEventType.SongStarted => new SongStartedEvent(time, reader.ReadString(), reader.ReadDouble()),
            RecordedEventType.SongFinished => new SongFinishedEvent(time),
            RecordedEventType.NoteHit => new NoteHitEvent(time, reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle()),
            RecordedEventType.NoteMissed => new NoteMissedEvent(time, reader.ReadInt32(), reader.ReadInt32()),
            RecordedEventType.EmptyPress => new EmptyPressEvent(time, reader.ReadInt32()),
            RecordedEventType.LaneLayoutChanged => new LaneLayoutChangedEvent(time, reader.ReadSingle(), reader.ReadSingle()),

            RecordedEventType.AudienceMemberJoined => new AudienceMemberJoinedEvent(
                time,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadVector3(),
                reader.ReadQuaternion(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadBoolean()),

            RecordedEventType.AudienceMemberLeft => new AudienceMemberLeftEvent(time, reader.ReadInt32()),
            RecordedEventType.EngagementChanged => new EngagementChangedEvent(time, reader.ReadSingle()),
            RecordedEventType.CrowdCheered => new CrowdCheeredEvent(time, reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle()),
            RecordedEventType.MoshPitStarted => new MoshPitStartedEvent(time),
            RecordedEventType.MoshPitEnded => new MoshPitEndedEvent(time),

            _ => throw new InvalidDataException($"Unknown recorded event type {(byte) eventType}")
        };
    }

    private static MemoryStream Decompress(byte[] bytes)
    {
        var output = new MemoryStream();
        using (var deflater = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress))
        {
            deflater.CopyTo(output);
        }
        output.Position = 0;
        return output;
    }
}