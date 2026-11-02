#!/usr/bin/env python3
"""Builds a playable song from a GarageBand for iPad project and its two exports.

    python Tools/build_song.py "My Song" --project "My Song.band" \\
        --instrument "exports/instrument.aiff" --backing "exports/backing.aiff"

Writes to Assets/Music/Tracks: "<Name> Instrument.wav", "<Name> Backing.wav", "<Name>.mid" and "<Name>.asset", with
.meta files for the stems and the asset.

Notes come from the MIDI, which is converted from --project or passed as --midi. The player's track is the one whose
pitches ring in the instrument stem and not in the backing, or --midi-track. --detect-notes finds notes in the
instrument stem instead. Needs numpy and ffmpeg on PATH.
"""
import argparse
import hashlib
import re
import subprocess
import sys
import wave
from collections import Counter
from pathlib import Path

import numpy as np

import band_to_midi

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "Assets" / "Music" / "scratchpad"))
TRACKS_DIR = REPO_ROOT / "Assets" / "Music" / "Tracks"
TRACK_SCRIPT_GUID = "71cd785156b541aba9f9844e8d2d441d"
LOSSY_EXTENSIONS = {".mp3", ".m4a", ".aac", ".ogg"}
MAX_STEM_LAG_SECONDS = 0.012
TRAILING_SILENCE_LEVEL = 0.001
TRAILING_SILENCE_KEPT_SECONDS = 0.25
PITCH_MATCH_WINDOW = 8192
PITCH_MATCH_DELAY_SECONDS = 0.05
PITCH_MATCH_MAX_NOTES = 400
PITCH_MATCH_TOLERANCE = 0.015
MIN_INSTRUMENT_DOMINANCE = 2.0
ATTACK_MATCH_SECONDS = 0.06

ONSET_WINDOW = 2048
ONSET_HOP = 256
ATTACK_HOP = 32
ATTACK_SEARCH_SECONDS = 0.04
MIN_ATTACK_LOUDNESS_RATIO = 1.05
PITCH_WINDOW = 8192
PITCH_DELAY_SECONDS = 0.02
MIN_PITCH_HZ, MAX_PITCH_HZ = 55.0, 2500.0
HARMONICS = 4
CONTOUR_WINDOW_SECONDS = 4.0

# Streaming and background loading would break sample-accurate starts. Normalizing per clip would change the mix balance.
AUDIO_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def stable_guid(path):
    return hashlib.md5(path.name.encode()).hexdigest()


def probe_sample_rate(path):
    output = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate", "-of", "csv=p=0", str(path)],
        check=True, capture_output=True, text=True).stdout
    return int(output.strip().strip(","))


def decode_stereo(path, sample_rate):
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-map", "0:a:0", "-ac", "2", "-ar", str(sample_rate), "-f", "f32le", "-"],
        check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)


def pad_to_frames(audio, frames):
    return np.vstack([audio, np.zeros((frames - len(audio), 2), dtype=np.float32)])


def write_wav(path, audio, sample_rate):
    pcm = np.round(np.clip(audio, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(sample_rate)
        out.writeframes(pcm.tobytes())


def wav_format(path):
    with wave.open(str(path), "rb") as wav:
        return wav.getnframes(), wav.getframerate()


def onset_envelope(mono, hop):
    window = np.hanning(ONSET_WINDOW)
    frames = max(0, (len(mono) - ONSET_WINDOW) // hop)
    flux = np.zeros(frames)
    previous = None
    for i in range(frames):
        spectrum = np.log1p(10.0 * np.abs(np.fft.rfft(mono[i * hop:i * hop + ONSET_WINDOW] * window)))
        if previous is not None:
            flux[i] = np.maximum(spectrum - previous, 0.0).sum()
        previous = spectrum
    return flux


# Stems of one arrangement share onsets at lag zero, so a trimmed export shows up as a large lag
def estimate_stem_lag(instrument, backing, sample_rate):
    limit = min(len(instrument), len(backing), sample_rate * 120)
    a = onset_envelope(instrument[:limit], ONSET_HOP)
    b = onset_envelope(backing[:limit], ONSET_HOP)
    a -= a.mean()
    b -= b.mean()
    size = len(a) + len(b)
    correlation = np.fft.irfft(np.fft.rfft(a, size) * np.conj(np.fft.rfft(b, size)), size)
    max_lag = int(2.0 * sample_rate / ONSET_HOP)
    lags = np.concatenate([np.arange(0, max_lag + 1), np.arange(-max_lag, 0)])
    values = np.concatenate([correlation[:max_lag + 1], correlation[-max_lag:]])
    return lags[int(np.argmax(values))] * ONSET_HOP / sample_rate


def refine_to_attack(mono, approximate_seconds, sample_rate):
    center = int(approximate_seconds * sample_rate)
    reach = int(ATTACK_SEARCH_SECONDS * sample_rate)
    start = max(0, center - reach)
    segment = np.abs(mono[start:center + reach])
    if len(segment) < ATTACK_HOP * 4:
        return approximate_seconds
    energy = np.convolve(segment, np.ones(ATTACK_HOP) / ATTACK_HOP, "same")
    rise = np.diff(energy)
    return (start + int(np.argmax(rise))) / sample_rate


# Note endings also change the spectrum, but only attacks get louder
def gets_louder_at(mono, seconds, sample_rate):
    center = int(seconds * sample_rate)
    reach = int(ATTACK_SEARCH_SECONDS * sample_rate)
    before = np.abs(mono[max(0, center - reach):center]).mean() if center > 0 else 0.0
    return np.abs(mono[center:center + reach]).mean() > before * MIN_ATTACK_LOUDNESS_RATIO


def detect_onsets(mono, sample_rate, min_gap, sensitivity):
    flux = onset_envelope(mono, ONSET_HOP)
    if flux.max() <= 0.0:
        return []
    frame_rate = sample_rate / ONSET_HOP
    average = np.convolve(flux, np.ones(int(frame_rate * 0.5)) / int(frame_rate * 0.5), "same")
    threshold = average * (1.0 + sensitivity) + flux.max() * 0.02 * sensitivity
    gap_frames = max(1, int(min_gap * frame_rate))
    onsets = []
    last_peak = -gap_frames
    for i in range(1, len(flux) - 1):
        is_peak = flux[i] >= flux[i - 1] and flux[i] > flux[i + 1] and flux[i] > threshold[i]
        if is_peak and i - last_peak >= gap_frames:
            onset = refine_to_attack(mono, (i * ONSET_HOP + ONSET_WINDOW / 2) / sample_rate, sample_rate)
            if gets_louder_at(mono, onset, sample_rate):
                last_peak = i
                onsets.append(onset)
    return onsets


def estimate_pitch(mono, onset_seconds, sample_rate):
    start = int((onset_seconds + PITCH_DELAY_SECONDS) * sample_rate)
    segment = mono[start:start + PITCH_WINDOW]
    if len(segment) < PITCH_WINDOW:
        segment = np.pad(segment, (0, PITCH_WINDOW - len(segment)))
    spectrum = np.log1p(np.abs(np.fft.rfft(segment * np.hanning(PITCH_WINDOW))))
    bin_hz = sample_rate / PITCH_WINDOW
    low, high = int(MIN_PITCH_HZ / bin_hz), int(MAX_PITCH_HZ / bin_hz)
    scores = np.zeros(high)
    for harmonic in range(1, HARMONICS + 1):
        reach = min(high, len(spectrum) // harmonic)
        scores[:reach] += spectrum[::harmonic][:reach]
    peak = low + int(np.argmax(scores[low:high]))
    return 69.0 + 12.0 * np.log2(max(peak, 1) * bin_hz / 440.0)


def merge_close_notes(notes, min_gap):
    merged = []
    for time, pitch in sorted(notes):
        if merged and time - merged[-1][0] < min_gap:
            merged[-1] = (merged[-1][0], max(merged[-1][1], pitch))
        else:
            merged.append((time, pitch))
    return merged


def assign_contour_lanes(notes, lane_count):
    lanes = []
    for i, (time, pitch) in enumerate(notes):
        nearby = [p for t, p in notes if abs(t - time) <= CONTOUR_WINDOW_SECONDS]
        low, high = min(nearby), max(nearby)
        lane = round((pitch - low) / (high - low) * (lane_count - 1)) if high > low else (lanes[-1] if lanes else lane_count // 2)
        if lanes:
            previous, step = lanes[-1], pitch - notes[i - 1][1]
            if abs(step) < 0.5:
                lane = previous
            elif step > 0 and lane <= previous:
                lane = min(previous + 1, lane_count - 1)
            elif step < 0 and lane >= previous:
                lane = max(previous - 1, 0)
        lanes.append(lane)
    return lanes


def read_midi(path):
    from midiparse import parse
    _, _, _, names, events = parse(path)
    notes_by_track = {}
    for seconds, _, kind, data in events:
        if kind == "on":
            notes_by_track.setdefault(data[0], []).append((seconds, data[2]))
    tempos = [60_000_000 / data for _, _, kind, data in events if kind == "tempo"]
    return notes_by_track, names, tempos


def describe_tracks(notes_by_track, names):
    return ", ".join(f"{track} ({names.get(track, 'unnamed')})" for track in sorted(notes_by_track))


def resolve_midi_track(requested, notes_by_track, names):
    if requested.isdigit() and int(requested) in notes_by_track:
        return int(requested)
    matches = [track for track in notes_by_track if names.get(track, "").lower() == requested.lower()]
    if len(matches) != 1:
        raise SystemExit(f"--midi-track {requested!r} doesn't match one of: {describe_tracks(notes_by_track, names)}")
    return matches[0]


def median_pitch_amplitude(mono, notes, sample_rate):
    if len(notes) > PITCH_MATCH_MAX_NOTES:
        chosen = np.random.default_rng(0).choice(len(notes), PITCH_MATCH_MAX_NOTES, replace=False)
        notes = [notes[i] for i in chosen]
    window = np.hanning(PITCH_MATCH_WINDOW)
    bin_hz = sample_rate / PITCH_MATCH_WINDOW
    amplitudes = []
    for seconds, pitch in notes:
        start = int((seconds + PITCH_MATCH_DELAY_SECONDS) * sample_rate)
        segment = mono[start:start + PITCH_MATCH_WINDOW]
        if len(segment) < PITCH_MATCH_WINDOW:
            continue
        frequency = 440.0 * 2.0 ** ((pitch - 69) / 12.0)
        low = int(frequency * (1.0 - PITCH_MATCH_TOLERANCE) / bin_hz)
        high = int(frequency * (1.0 + PITCH_MATCH_TOLERANCE) / bin_hz) + 1
        amplitudes.append(np.abs(np.fft.rfft(segment * window))[low:high].max())
    return float(np.median(amplitudes)) if amplitudes else 0.0


def find_instrument_track(notes_by_track, names, instrument, backing, sample_rate):
    scored = []
    for track, notes in sorted(notes_by_track.items()):
        in_instrument = median_pitch_amplitude(instrument, notes, sample_rate)
        in_backing = median_pitch_amplitude(backing, notes, sample_rate)
        scored.append((in_instrument / (in_backing + 1.0), track))
        print(f"  Track {track} {names.get(track, '')!r}: {len(notes)} notes, "
              f"pitch level {in_instrument:.1f} in instrument, {in_backing:.1f} in backing")
    best_dominance, best_track = max(scored)
    runner_up = max((score for score, track in scored if track != best_track), default=0.0)
    if best_dominance < MIN_INSTRUMENT_DOMINANCE or best_dominance < runner_up * MIN_INSTRUMENT_DOMINANCE:
        raise SystemExit(f"Can't tell which track is the instrument, pass --midi-track. Tracks: {describe_tracks(notes_by_track, names)}")
    print(f"Instrument is track {best_track} {names.get(best_track, '')!r}")
    return best_track


def measure_attack_offset(notes, mono, sample_rate, min_gap, sensitivity):
    onsets = np.array(detect_onsets(mono, sample_rate, min_gap, sensitivity))
    if len(onsets) == 0:
        raise SystemExit("The instrument stem has no detectable attacks to align the notes with")
    differences = [onsets[np.argmin(np.abs(onsets - seconds))] - seconds for seconds, _ in notes]
    matched = np.array([d for d in differences if abs(d) <= ATTACK_MATCH_SECONDS])
    if len(matched) * 2 < len(notes):
        print(f"WARNING: only {len(matched)}/{len(notes)} notes have an attack nearby in the stem. Check the track and the export.")
    if len(matched) == 0:
        return 0.0
    spread = np.percentile(matched, 75) - np.percentile(matched, 25)
    print(f"Attacks land {np.median(matched) * 1000.0:.1f} ms after the MIDI notes on {len(matched)}/{len(notes)} notes "
          f"(middle half within {spread * 1000.0:.1f} ms)")
    return float(np.median(matched))


def trim_shared_trailing_silence(instrument, backing, sample_rate):
    audible = (np.abs(instrument) > TRAILING_SILENCE_LEVEL).any(axis=1) | (np.abs(backing) > TRAILING_SILENCE_LEVEL).any(axis=1)
    last_audible = len(audible) - int(np.argmax(audible[::-1]))
    frames = min(len(audible), last_audible + int(TRAILING_SILENCE_KEPT_SECONDS * sample_rate))
    return instrument[:frames], backing[:frames]


def read_hand_tuned_value(asset_path, field, default):
    if asset_path.exists():
        match = re.search(rf"^  {field}: (\S+)$", asset_path.read_text(), re.MULTILINE)
        if match:
            return match.group(1)
    return default


def read_smoke_bars(asset_path):
    if not asset_path.exists():
        return []
    match = re.search(r"^  _smokeBars:\n((?:  - \d+\n)*)", asset_path.read_text(), re.MULTILINE)
    return [int(bar) for bar in re.findall(r"\d+", match.group(1))] if match else []


def write_track_asset(path, name, backing_guid, instrument_guid, bpm, first_beat, half_time_claps, is_metal, smoke_bars, notes, lanes, duration):
    timing_offset = read_hand_tuned_value(path, "_timingOffset", "0")
    instrument_volume = read_hand_tuned_value(path, "_instrumentVolume", "1")
    if smoke_bars is None:
        smoke_bars = read_smoke_bars(path)
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {TRACK_SCRIPT_GUID}, type: 3}}", f"  m_Name: {name}", "  m_EditorClassIdentifier: ",
        f"  _backingClip: {{fileID: 8300000, guid: {backing_guid}, type: 3}}",
        f"  _instrumentClip: {{fileID: 8300000, guid: {instrument_guid}, type: 3}}",
        f"  _instrumentVolume: {instrument_volume}",
        f"  _bpm: {bpm:g}", f"  _timingOffset: {timing_offset}", f"  _firstBeatTime: {first_beat:g}",
        f"  _halfTimeClaps: {int(half_time_claps)}", f"  _isMetal: {int(is_metal)}",
        "  _smokeBars:" if smoke_bars else "  _smokeBars: []",
        *[f"  - {bar}" for bar in smoke_bars],
        "  _notes:",
    ]
    for (time, _), lane in zip(notes, lanes):
        lines += [f"  - _noteIndex: {lane}", f"    _position: {time:.4f}"]
    lines.append(f"  _duration: {duration:.4f}")
    path.write_text("\n".join(lines) + "\n", newline="\n")


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("name", help='song name, e.g. "My Song"')
    parser.add_argument("--project", help=".band folder to convert to MIDI")
    parser.add_argument("--instrument", required=True, help="export with only the player's instrument")
    parser.add_argument("--backing", required=True, help="export with everything else")
    parser.add_argument("--bpm", type=float, help="overrides the tempo in the MIDI")
    parser.add_argument("--first-beat", type=float, help="seconds where beat 0 lands, defaults to the note offset")
    parser.add_argument("--midi", help="use this MIDI file instead of converting --project")
    parser.add_argument("--midi-track", help="number or name of the player's track, found automatically if left out")
    parser.add_argument("--midi-offset", type=float, help="seconds added to MIDI times, measured from the stem if left out")
    parser.add_argument("--detect-notes", action="store_true", help="find notes in the instrument stem instead of using MIDI")
    parser.add_argument("--lanes", type=int, default=6)
    parser.add_argument("--min-gap", type=float, default=0.1, help="notes closer than this become one")
    parser.add_argument("--sensitivity", type=float, default=1.0, help="higher detects fewer notes")
    parser.add_argument("--half-time-claps", action="store_true")
    parser.add_argument("--metal", action="store_true")
    parser.add_argument("--smoke-bars", type=int, nargs="*", help="bars where the smoke cannons fire, kept from the existing asset if left out")
    parser.add_argument("--output-dir", type=Path, default=TRACKS_DIR)
    parser.add_argument("--force", action="store_true", help="overwrite an existing song, including hand-edited notes")
    return parser.parse_args()


def main():
    args = parse_args()
    if not (args.project or args.midi or args.detect_notes):
        raise SystemExit("Pass --project, --midi or --detect-notes")
    if args.detect_notes and args.bpm is None and not args.project:
        raise SystemExit("--detect-notes needs --bpm or --project for the tempo")

    output_dir = args.output_dir.resolve()
    asset_name = "".join(args.name.split())
    asset_path = output_dir / f"{asset_name}.asset"
    instrument_path = output_dir / f"{args.name} Instrument.wav"
    backing_path = output_dir / f"{args.name} Backing.wav"
    midi_path = output_dir / f"{args.name}.mid"
    if not args.force and asset_path.exists():
        raise SystemExit(f"{asset_path} exists, pass --force to overwrite it")

    for source in (args.instrument, args.backing):
        if Path(source).suffix.lower() in LOSSY_EXTENSIONS:
            print(f"WARNING: {source} is lossy. Its encoder delay can shift it, export AIFF or WAV for exact sync.")

    instrument_rate, backing_rate = probe_sample_rate(args.instrument), probe_sample_rate(args.backing)
    sample_rate = instrument_rate
    if backing_rate != instrument_rate:
        print(f"WARNING: sample rates differ ({instrument_rate}, {backing_rate}), resampling the backing to {sample_rate}.")
    instrument = decode_stereo(args.instrument, sample_rate)
    backing = decode_stereo(args.backing, sample_rate)

    frames = max(len(instrument), len(backing))
    if len(instrument) != len(backing):
        print(f"Exports differ by {abs(len(instrument) - len(backing))} frames, padding the end of the shorter one.")
    instrument, backing = pad_to_frames(instrument, frames), pad_to_frames(backing, frames)
    instrument, backing = trim_shared_trailing_silence(instrument, backing, sample_rate)
    frames = len(instrument)

    peak = max(np.abs(instrument).max(), np.abs(backing).max())
    if peak > 1.0:
        print(f"Peak is {20.0 * np.log10(peak):.2f} dB over full scale, lowering both stems equally to keep the mix balance.")
        instrument, backing = instrument / peak, backing / peak
    lag = estimate_stem_lag(instrument.mean(axis=1), backing.mean(axis=1), sample_rate)
    print(f"Onset lag between stems: {lag * 1000.0:.1f} ms")
    if abs(lag) > MAX_STEM_LAG_SECONDS:
        print("WARNING: the stems may not start at the same point. Export both over the same range, from the project start.")

    instrument_mono, backing_mono = instrument.mean(axis=1), backing.mean(axis=1)
    bpm = args.bpm
    note_offset = args.midi_offset if args.midi_offset is not None else 0.0
    output_dir.mkdir(parents=True, exist_ok=True)
    if args.detect_notes:
        if bpm is None:
            bpm = band_to_midi.read_tempo(args.project)
        notes = [(t, estimate_pitch(instrument_mono, t, sample_rate))
                 for t in detect_onsets(instrument_mono, sample_rate, args.min_gap, args.sensitivity)]
    else:
        source_midi = args.midi or midi_path
        if not args.midi:
            band_to_midi.convert(args.project, midi_path)
        notes_by_track, track_names, tempos = read_midi(source_midi)
        if not notes_by_track:
            raise SystemExit(f"{source_midi} has no notes")
        if len(set(tempos)) > 1:
            print("WARNING: the MIDI changes tempo. Note times follow it, but the crowd's beat uses one tempo.")
        if bpm is None:
            bpm = tempos[0] if tempos else 120.0
        if args.midi_track:
            track = resolve_midi_track(args.midi_track, notes_by_track, track_names)
        else:
            track = find_instrument_track(notes_by_track, track_names, instrument_mono, backing_mono, sample_rate)
        notes = sorted(notes_by_track[track])
        if args.midi_offset is None:
            note_offset = measure_attack_offset(notes, instrument_mono, sample_rate, args.min_gap, args.sensitivity)
        notes = [(seconds + note_offset, pitch) for seconds, pitch in notes]
    notes = merge_close_notes(notes, args.min_gap)
    if not notes:
        raise SystemExit("No notes found, the Track needs at least one")
    lanes = assign_contour_lanes(notes, args.lanes)

    write_wav(instrument_path, instrument, sample_rate)
    write_wav(backing_path, backing, sample_rate)
    if wav_format(instrument_path) != wav_format(backing_path):
        raise SystemExit("Written stems differ in length or sample rate")

    instrument_guid, backing_guid = stable_guid(instrument_path), stable_guid(backing_path)
    Path(f"{instrument_path}.meta").write_text(AUDIO_META.format(guid=instrument_guid), newline="\n")
    Path(f"{backing_path}.meta").write_text(AUDIO_META.format(guid=backing_guid), newline="\n")
    write_track_asset(asset_path, asset_name, backing_guid, instrument_guid, bpm,
                      args.first_beat if args.first_beat is not None else note_offset,
                      args.half_time_claps, args.metal, args.smoke_bars, notes, lanes, frames / sample_rate)
    Path(f"{asset_path}.meta").write_text(ASSET_META.format(guid=stable_guid(asset_path)), newline="\n")

    gaps = [b[0] - a[0] for a, b in zip(notes, notes[1:])]
    print(f"{asset_name}: {frames} frames at {sample_rate} Hz ({frames / sample_rate:.2f}s), {bpm:g} BPM")
    print(f"{len(notes)} notes, lanes {sorted(Counter(lanes).items())}, min gap {min(gaps, default=0.0):.3f}s")
    print(f"Add {asset_path.name} to ConcertSet's set list and ReplaySong's tracks.")


if __name__ == "__main__":
    main()
