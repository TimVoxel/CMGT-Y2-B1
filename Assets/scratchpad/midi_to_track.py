# Builds a Track asset from one MIDI channel, keeping the pitch contour rather than exact notes.
from midiparse import parse

MIDI = "C:/Development/ConcertPrototype/Assets/Music/Tracks/the-lasting-dose.mid"
OUT = "C:/Development/ConcertPrototype/Assets/Music/Tracks/TheLastingDose.asset"
CLIP_GUID = "0ebd0caed1688354aa92aa76f100a07e"   # The lasting dose.mp3
CHANNEL = 0          # distortion guitar
LANES = 6
AUDIO_OFFSET = 0.13  # MIDI time -> mp3 time, from onset cross-correlation
MERGE = 0.1          # onsets closer than this become one note (chords, grace notes)
WINDOW = 4.0         # seconds either side used to normalise pitch range
BPM, DURATION = 108, 252.03

_, _, _, _, ev = parse(MIDI)
raw = sorted((s, d[2]) for s, _, k, d in ev if k == 'on' and d[1] == CHANNEL)

# Collapse chords / flams: keep the highest pitch of each cluster
notes = []
for s, p in raw:
    if notes and s - notes[-1][0] < MERGE:
        notes[-1] = (notes[-1][0], max(notes[-1][1], p))
    else:
        notes.append((s, p))

# Map pitch to lane relative to the local range, then enforce contour direction
lanes = []
for i, (s, p) in enumerate(notes):
    near = [q for t, q in notes if abs(t - s) <= WINDOW]
    lo, hi = min(near), max(near)
    lane = round((p - lo) / (hi - lo) * (LANES - 1)) if hi > lo else (lanes[-1] if lanes else LANES // 2)
    if lanes:
        prev, dp = lanes[-1], p - notes[i - 1][1]
        if dp == 0:
            lane = prev
        elif dp > 0 and lane <= prev:
            lane = min(prev + 1, LANES - 1)
        elif dp < 0 and lane >= prev:
            lane = max(prev - 1, 0)
    lanes.append(lane)

out = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 71cd785156b541aba9f9844e8d2d441d, type: 3}}
  m_Name: TheLastingDose
  m_EditorClassIdentifier: 
  _clip: {{fileID: 8300000, guid: {CLIP_GUID}, type: 3}}
  _bpm: {BPM}
  _notes:
"""
for (s, _), lane in zip(notes, lanes):
    out += f"  - _noteIndex: {lane}\n    _position: {s + AUDIO_OFFSET:.3f}\n"
out += f"  _duration: {DURATION}\n"
open(OUT, "w", newline="\n").write(out)

from collections import Counter
print(len(raw), "raw ->", len(notes), "notes; lanes", sorted(Counter(lanes).items()))
print("first", [(round(s + AUDIO_OFFSET, 2), l) for (s, _), l in zip(notes[:12], lanes[:12])])
gaps = [b[0] - a[0] for a, b in zip(notes, notes[1:])]
print("min gap %.3f" % min(gaps))
