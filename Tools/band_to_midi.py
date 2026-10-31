#!/usr/bin/env python3
"""Converts a GarageBand for iPad .band project to a standard MIDI file, one MIDI track per GarageBand track.

    python Tools/band_to_midi.py "My Song.band" "My Song.mid"

Only software instrument regions have notes, so audio recordings are skipped. The format was worked out from one
GarageBand for iOS 2.3.18 project and is undocumented, so unexpected data raises BandFormatError rather than guessing.
"""
import plistlib
import struct
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

TICKS_PER_QUARTER = 960
SONG_ORIGIN_TICK = 34560
SEQUENCE_ORIGIN_TICK = 38400
CHUNK_HEADER_SIZE = 36
FIRST_CHUNK_OFFSET = 24
EVENT_SIZE = 16
REGION_RECORD_SIZE = 80
REGION_RECORD_MARKER = 0x20
REGION_TRANSPOSE_OFFSET = 53
EVENT_CONTINUATION_MARKER = 0x89
UNBOUNDED_REGION_LENGTH = 0x3FFFFFFF
ROOT_FOLDER_NAME = "Root Folder"
SEQUENCE_CLASS_ID = 0x17
NOTES_PER_DRUM_CHANNEL = 9


class BandFormatError(Exception):
    pass


@dataclass
class Sequence:
    name: str
    events_id: int
    window_start_ticks: int
    window_ticks: int


@dataclass
class BandNote:
    tick: int
    pitch: int
    velocity: int
    length: int


@dataclass
class BandTrack:
    number: int
    name: str
    notes: list


def read_song_metadata(band_path):
    path = Path(band_path) / "Output" / "metadata.plist"
    if not path.exists():
        raise BandFormatError(f"{path} is missing")
    return plistlib.loads(path.read_bytes())


def read_tempo(band_path):
    tempo = read_song_metadata(band_path).get("com_apple_garageband_metadata_songTempo")
    if not tempo:
        raise BandFormatError(f"No tempo in {band_path}")
    return float(tempo)


def read_song_blob(band_path):
    archive = plistlib.loads((Path(band_path) / "projectData").read_bytes())
    objects = archive["$objects"]
    logic_model = objects[archive["$top"]["DfDocument logic model"]["CF$UID"]]
    return objects[logic_model["DfLogicModelLogicSong"]["CF$UID"]]["NS.data"]


def walk_chunks(blob):
    offset = FIRST_CHUNK_OFFSET
    while offset + CHUNK_HEADER_SIZE <= len(blob):
        size = struct.unpack_from("<Q", blob, offset + 28)[0]
        end = offset + CHUNK_HEADER_SIZE + size
        if end > len(blob):
            raise BandFormatError(f"Chunk at {offset} runs past the end of the song data")
        yield blob[offset:offset + 4], blob[offset:offset + CHUNK_HEADER_SIZE], blob[offset + CHUNK_HEADER_SIZE:end]
        offset = end


def parse_sequence(data):
    name_length = struct.unpack_from("<H", data, 16)[0]
    name = data[18:18 + name_length].decode("utf-8", "replace")
    padded_name_length = name_length + name_length % 2
    return Sequence(name, struct.unpack_from("<I", data, 8)[0],
                    struct.unpack_from("<i", data, 22 + padded_name_length)[0],
                    struct.unpack_from("<I", data, 78 + padded_name_length)[0])


def read_chunks(blob):
    sequences = {}
    events = {}
    root_events_id = None
    for tag, header, data in walk_chunks(blob):
        if tag == b"qeSM" and struct.unpack_from("<H", header, 6)[0] == SEQUENCE_CLASS_ID:
            sequence = parse_sequence(data)
            sequences[struct.unpack_from("<I", header, 10)[0]] = sequence
            if sequence.name == ROOT_FOLDER_NAME:
                root_events_id = sequence.events_id
        elif tag == b"qSvE":
            events[struct.unpack_from("<I", header, 14)[0]] = data
    if root_events_id is None:
        raise BandFormatError("No root folder in the song data")
    return sequences, events, events[root_events_id]


def read_sequence_notes(events):
    notes = []
    for offset in range(0, len(events) - 2 * EVENT_SIZE + 1, EVENT_SIZE):
        if events[offset] & 0xF0 == 0x90 and events[offset + EVENT_SIZE + 7] == EVENT_CONTINUATION_MARKER:
            notes.append(BandNote(
                struct.unpack_from("<I", events, offset + 4)[0] - SEQUENCE_ORIGIN_TICK,
                events[offset + 12],
                events[offset + 11],
                struct.unpack_from("<I", events, offset + EVENT_SIZE + 12)[0]))
    return notes


# A region shows a window of its sequence. Notes outside the window don't sound. A set length repeats the window.
def expand_region(sequence_notes, region_start, region_length, sequence, transpose=0):
    window_start, window = sequence.window_start_ticks, sequence.window_ticks
    if window <= 0:
        raise BandFormatError(f"Sequence {sequence.name!r} has no window length")
    visible = [n for n in sequence_notes if window_start <= n.tick < window_start + window]
    total_length = window if region_length == UNBOUNDED_REGION_LENGTH else region_length
    expanded = []
    for repeat_start in range(0, total_length, window):
        for note in visible:
            start = repeat_start + note.tick - window_start
            if start < total_length:
                end = min(start + note.length, repeat_start + window, total_length)
                expanded.append(BandNote(region_start + start, note.pitch + transpose, note.velocity, end - start))
    return expanded


def read_tracks(band_path):
    sequences, events, root = read_chunks(read_song_blob(band_path))
    if len(root) % REGION_RECORD_SIZE != EVENT_SIZE:
        raise BandFormatError(f"Unexpected root folder size {len(root)}")
    notes_by_track = defaultdict(list)
    names_by_track = defaultdict(Counter)
    region_count = 0
    for offset in range(0, len(root) - EVENT_SIZE, REGION_RECORD_SIZE):
        if root[offset] != REGION_RECORD_MARKER or root[offset + EVENT_SIZE + 7] != EVENT_CONTINUATION_MARKER:
            raise BandFormatError(f"Unexpected region record at {offset} in the root folder")
        region_count += 1
        position = struct.unpack_from("<I", root, offset + 4)[0]
        track_number = root[offset + EVENT_SIZE + 4]
        length = struct.unpack_from("<I", root, offset + EVENT_SIZE + 12)[0]
        sequence = sequences.get(struct.unpack_from("<I", root, offset + 2 * EVENT_SIZE)[0])
        if sequence is None:
            raise BandFormatError(f"Region at {offset} points at an unknown sequence")
        notes = read_sequence_notes(events[sequence.events_id])
        transpose = struct.unpack_from("<b", root, offset + REGION_TRANSPOSE_OFFSET)[0]
        names_by_track[track_number][sequence.name] += 1
        notes_by_track[track_number] += expand_region(notes, position - SONG_ORIGIN_TICK, length, sequence, transpose)

    expected_regions = read_song_metadata(band_path).get("kDfMetaDataKey_numberOfRootRegions")
    if expected_regions is not None and expected_regions != region_count:
        raise BandFormatError(f"Found {region_count} regions, the project says {expected_regions}")

    return [BandTrack(number, names_by_track[number].most_common(1)[0][0], sorted(notes_by_track[number], key=lambda n: n.tick))
            for number in sorted(notes_by_track) if notes_by_track[number]]


def encode_variable_length(value):
    encoded = [value & 0x7F]
    value >>= 7
    while value:
        encoded.insert(0, (value & 0x7F) | 0x80)
        value >>= 7
    return bytes(encoded)


def encode_midi_track(timed_events):
    out = bytearray()
    previous_tick = 0
    for tick, payload in sorted(timed_events, key=lambda event: (event[0], event[1][0] & 0xF0 != 0x80, event[1])):
        out += encode_variable_length(tick - previous_tick) + payload
        previous_tick = tick
    out += b"\x00\xFF\x2F\x00"
    return b"MTrk" + struct.pack(">I", len(out)) + bytes(out)


def write_midi(path, tracks, bpm, numerator=4, denominator=4):
    denominator_power = {1: 0, 2: 1, 4: 2, 8: 3, 16: 4}[denominator]
    tempo_track = [
        (0, b"\xFF\x51\x03" + round(60_000_000 / bpm).to_bytes(3, "big")),
        (0, bytes([0xFF, 0x58, 0x04, numerator, denominator_power, 24, 8])),
    ]
    chunks = [encode_midi_track(tempo_track)]
    for index, track in enumerate(tracks):
        channel = index if index < NOTES_PER_DRUM_CHANNEL else index + 1
        name = track.name.encode("utf-8")
        events = [(0, b"\xFF\x03" + encode_variable_length(len(name)) + name)]
        for note in track.notes:
            events.append((note.tick, bytes([0x90 | channel, note.pitch, max(1, note.velocity)])))
            events.append((note.tick + max(1, note.length), bytes([0x80 | channel, note.pitch, 0])))
        chunks.append(encode_midi_track(events))
    header = b"MThd" + struct.pack(">IHHH", 6, 1, len(chunks), TICKS_PER_QUARTER)
    Path(path).write_bytes(header + b"".join(chunks))


def convert(band_path, midi_path):
    metadata = read_song_metadata(band_path)
    tracks = read_tracks(band_path)
    write_midi(midi_path, tracks, read_tempo(band_path),
               metadata.get("com_apple_garageband_metadata_songSignatureNominator", 4),
               metadata.get("com_apple_garageband_metadata_songSignatureDeNominator", 4))
    return tracks


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    for converted in convert(sys.argv[1], sys.argv[2]):
        print(f"Track {converted.number}: {converted.name}, {len(converted.notes)} notes")
