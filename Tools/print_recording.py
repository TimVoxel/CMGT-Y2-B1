"""Pretty prints a concert recording (.savrcr) written by RecordingSessionWriter.

Usage:
    python Tools/print_recording.py [file] [--hide-poses] [--summary]

Without a file, lists the recordings in the editor's persistentDataPath and lets you pick one.

Format v1: header, then events.
Format v2: header, then blocks of [int32 compressed length][raw deflate data], each holding whole events.
"""

import argparse
import os
import struct
import sys
import zlib
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path

EXTENSION = ".savrcr"
SUPPORTED_FORMAT_VERSIONS = (1, 2)
FIRST_COMPRESSED_VERSION = 2
DEFAULT_RECORDINGS_DIR = Path(os.environ.get("USERPROFILE", "~")).expanduser() / "AppData" / "LocalLow" / "Liars" / "The Guitar One"

DEVICES = {0: "Head", 1: "LeftHand", 2: "RightHand"}


class TruncatedError(Exception):
    pass


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    @property
    def at_end(self) -> bool:
        return self.offset >= len(self.data)

    def _unpack(self, fmt: str):
        size = struct.calcsize(fmt)
        if self.offset + size > len(self.data):
            raise TruncatedError()
        values = struct.unpack_from("<" + fmt, self.data, self.offset)
        self.offset += size
        return values[0] if len(values) == 1 else values

    def bytes(self, count: int) -> bytes:
        if self.offset + count > len(self.data):
            raise TruncatedError()
        value = self.data[self.offset:self.offset + count]
        self.offset += count
        return value

    def byte(self) -> int: return self._unpack("B")
    def bool(self) -> bool: return self._unpack("?")
    def ushort(self) -> int: return self._unpack("H")
    def int(self) -> int: return self._unpack("i")
    def long(self) -> int: return self._unpack("q")
    def float(self) -> float: return self._unpack("f")
    def double(self) -> float: return self._unpack("d")
    def vector3(self): return self._unpack("3f")
    def quaternion(self): return self._unpack("4f")

    # .NET BinaryWriter.Write(string): 7-bit encoded byte length, then UTF-8
    def string(self) -> str:
        length = 0
        shift = 0
        while True:
            part = self.byte()
            length |= (part & 0x7F) << shift
            shift += 7
            if not part & 0x80:
                break
        return self.bytes(length).decode("utf-8")


def from_dotnet_binary(value: int) -> datetime:
    ticks = value & 0x3FFF_FFFF_FFFF_FFFF
    return datetime(1, 1, 1, tzinfo=timezone.utc) + timedelta(microseconds=ticks // 10)


def fmt_vec(values) -> str:
    return "(" + ", ".join(f"{v:7.3f}" for v in values) + ")"


def read_pose(r: Reader):
    device = DEVICES.get(r.byte(), "?")
    return f"{device:<9} pos {fmt_vec(r.vector3())}  rot {fmt_vec(r.quaternion())}"


def read_song_started(r: Reader):
    return f"track '{r.string()}'  song time {r.double():.3f}s"


def read_note_hit(r: Reader):
    lane, note, offset, accuracy = r.int(), r.int(), r.float(), r.float()
    timing = "late" if offset > 0 else "early"
    return f"lane {lane}  note #{note}  {abs(offset) * 1000:5.1f}ms {timing}  accuracy {accuracy:.2f}"


def read_note_missed(r: Reader):
    return f"lane {r.int()}  note #{r.int()}"


def read_empty_press(r: Reader):
    return f"lane {r.int()}"


def read_lane_layout(r: Reader):
    return f"hit line height {r.float():.3f}  horizontal factor {r.float():.3f}"


def read_member_joined(r: Reader):
    member_id, prefab = r.int(), r.int()
    position, rotation = r.vector3(), r.quaternion()
    threshold, amplitude, popped_in = r.float(), r.float(), r.bool()
    return (f"id {member_id}  prefab {prefab}  pos {fmt_vec(position)}  rot {fmt_vec(rotation)}  "
            f"threshold {threshold:.2f}  amplitude {amplitude:.2f}{'  (popped in)' if popped_in else ''}")


def read_member_left(r: Reader):
    return f"id {r.int()}"


def read_engagement(r: Reader):
    engagement = r.float()
    bar = "#" * round(engagement * 20)
    return f"{engagement:.3f}  [{bar:<20}]"


def read_cheer(r: Reader):
    return f"clip {r.int()}  pitch {r.float():.2f}  volume {r.float():.2f}"


def read_nothing(r: Reader):
    return ""


EVENTS = {
    1: ("Pose", read_pose),
    10: ("SongStarted", read_song_started),
    11: ("SongFinished", read_nothing),
    12: ("NoteHit", read_note_hit),
    13: ("NoteMissed", read_note_missed),
    14: ("EmptyPress", read_empty_press),
    15: ("LaneLayoutChanged", read_lane_layout),
    20: ("AudienceMemberJoined", read_member_joined),
    21: ("AudienceMemberLeft", read_member_left),
    22: ("EngagementChanged", read_engagement),
    23: ("CrowdCheered", read_cheer),
    24: ("MoshPitStarted", read_nothing),
    25: ("MoshPitEnded", read_nothing),
}
NAME_WIDTH = max(len(name) for name, _ in EVENTS.values())


def pick_recording() -> Path:
    recordings = sorted(DEFAULT_RECORDINGS_DIR.rglob("*" + EXTENSION), key=lambda p: p.stat().st_mtime, reverse=True)
    if not recordings:
        sys.exit(f"No {EXTENSION} files found under {DEFAULT_RECORDINGS_DIR}. Pass a file path instead.")

    for index, path in enumerate(recordings):
        size_kb = path.stat().st_size / 1024
        print(f"  [{index}] {path.relative_to(DEFAULT_RECORDINGS_DIR)}  ({size_kb:.1f} KB)")
    choice = input("Recording to print [0]: ").strip() or "0"
    try:
        return recordings[int(choice)]
    except (ValueError, IndexError):
        sys.exit(f"Invalid choice: {choice}")


def decompress_blocks(r: Reader):
    """Returns the concatenated event bytes, the block count and a problem description (None if the file is intact)."""
    events = bytearray()
    block_count = 0
    while not r.at_end:
        block_start = r.offset
        try:
            length = r.int()
            block = r.bytes(length)
        except TruncatedError:
            return events, block_count, f"last block at byte {block_start} is cut off (recording was interrupted)"
        try:
            events += zlib.decompress(block, wbits=-15)
        except zlib.error as error:
            return events, block_count, f"block at byte {block_start} doesn't decompress ({error})"
        block_count += 1
    return events, block_count, None


def print_recording(path: Path, hide_poses: bool, summary_only: bool):
    file_reader = Reader(path.read_bytes())

    try:
        version = file_reader.ushort()
        scene = file_reader.string()
        started_at = from_dotnet_binary(file_reader.long())
        pose_rate = file_reader.float()
    except TruncatedError:
        sys.exit("File is too short to contain a header.")

    supported = "" if version in SUPPORTED_FORMAT_VERSIONS else f"  (this script reads v{', v'.join(map(str, SUPPORTED_FORMAT_VERSIONS))})"
    print(f"Recording   {path}")
    print(f"Format      v{version}{supported}")
    print(f"Scene       {scene}")
    print(f"Started     {started_at:%Y-%m-%d %H:%M:%S} UTC")
    print(f"Pose rate   {pose_rate:g} Hz")
    print()

    problem = None
    if version >= FIRST_COMPRESSED_VERSION:
        payload_size = len(file_reader.data) - file_reader.offset
        events, block_count, problem = decompress_blocks(file_reader)
        r = Reader(bytes(events))
        compression = (f"Blocks      {block_count}, {payload_size / 1024:.1f} KB compressed -> {len(events) / 1024:.1f} KB"
                       + (f" ({len(events) / payload_size:.1f}x)" if payload_size else ""))
    else:
        r = file_reader
        compression = None

    counts = Counter()
    last_time = 0.0

    while not r.at_end:
        event_start = r.offset
        try:
            type_id = r.byte()
            time = r.double()
            if type_id not in EVENTS:
                print(f"\nUnknown event type {type_id} at byte {event_start}, can't continue reading.")
                break
            name, read_payload = EVENTS[type_id]
            description = read_payload(r)
        except TruncatedError:
            problem = problem or f"events end mid-event at byte {event_start} of {len(r.data)} (recording was cut off)"
            break

        counts[name] += 1
        last_time = time
        if summary_only or (hide_poses and name == "Pose"):
            continue
        print(f"{format_time(time)}  {name:<{NAME_WIDTH}}  {description}")

    print()
    print(f"Duration    {format_time(last_time)}")
    if compression:
        print(compression)
    print(f"Events      {sum(counts.values())}")
    for name, count in counts.most_common():
        print(f"  {name:<{NAME_WIDTH}}  {count}")
    if problem:
        print(f"\nWarning: {problem}.")


def format_time(seconds: float) -> str:
    minutes, rest = divmod(seconds, 60)
    return f"{int(minutes):02d}:{rest:06.3f}"


def main():
    parser = argparse.ArgumentParser(description="Pretty print a concert recording.")
    parser.add_argument("file", nargs="?", type=Path, help=f"{EXTENSION} file; omit to pick from {DEFAULT_RECORDINGS_DIR}")
    parser.add_argument("--hide-poses", action="store_true", help="skip pose events, which make up most of the file")
    parser.add_argument("--summary", action="store_true", help="only print the header and event counts")
    args = parser.parse_args()

    path = args.file or pick_recording()
    if not path.is_file():
        sys.exit(f"Not a file: {path}")
    print_recording(path, args.hide_poses, args.summary)


if __name__ == "__main__":
    main()
