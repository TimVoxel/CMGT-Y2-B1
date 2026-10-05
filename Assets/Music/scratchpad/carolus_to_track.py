# Builds the Carolus Rex Track asset: vocal melody, with the orchestra line filling the parts without vocals.
# Same pitch-contour lane mapping as midi_to_track.py (The Lasting Dose).
from collections import Counter
from midiparse import parse

MIDI = "C:/Development/ConcertPrototype/Assets/Music/Tracks/carolus-rex.mid"
OUT = "C:/Development/ConcertPrototype/Assets/Music/Tracks/CarolusRex.asset"
CLIP_GUID = "e1ea500d44f53944ca40450a1cde34fd"   # Carolus rex.mp3
LEAD, FILL = 7, 5     # vocal melody, orchestra/strings
FILL_GAP = 4.0        # vocal gaps longer than this get the orchestra line
LANES = 6
AUDIO_OFFSET = -0.17  # MIDI time -> mp3 time, from onset cross-correlation + the 21.7s band entrance
MIN_TIME = 0.5        # drop anything before the mp3 has faded in
MERGE = 0.1
WINDOW = 4.0
BPM, DURATION = 88, 293.85

_, _, _, _, ev = parse(MIDI)
def onsets(ch):
    return sorted((s, d[2]) for s, _, k, d in ev if k == 'on' and d[1] == ch)

lead = onsets(LEAD)
bounds = [0.0] + [s for s, _ in lead] + [DURATION]
gaps = [(a, b) for a, b in zip(bounds, bounds[1:]) if b - a > FILL_GAP]
fill = [(s, p) for s, p in onsets(FILL) if any(a + 0.5 < s < b - 0.5 for a, b in gaps)]
raw = sorted(lead + fill)
raw = [(s, p) for s, p in raw if s + AUDIO_OFFSET >= MIN_TIME]

notes = []
for s, p in raw:
    if notes and s - notes[-1][0] < MERGE:
        notes[-1] = (notes[-1][0], max(notes[-1][1], p))
    else:
        notes.append((s, p))

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
  m_Name: CarolusRex
  m_EditorClassIdentifier: 
  _clip: {{fileID: 8300000, guid: {CLIP_GUID}, type: 3}}
  _bpm: {BPM}
  _timingOffset: 0
  _firstBeatTime: {AUDIO_OFFSET}
  _halfTimeClaps: 0
  _isMetal: 1
  _notes:
"""
for (s, _), lane in zip(notes, lanes):
    out += f"  - _noteIndex: {lane}\n    _position: {s + AUDIO_OFFSET:.3f}\n"
out += f"  _duration: {DURATION}\n"
open(OUT, "w", newline="\n").write(out)

print(len(lead), "vocal +", len(fill), "fill ->", len(notes), "notes; lanes", sorted(Counter(lanes).items()))
print("fill gaps", [(round(a, 1), round(b, 1)) for a, b in gaps])
print("first", [(round(s + AUDIO_OFFSET, 2), l) for (s, _), l in zip(notes[:10], lanes[:10])])
g = [b[0] - a[0] for a, b in zip(notes, notes[1:])]
print("min gap %.3f, max gap %.1f, per 30s" % (min(g), max(g)), [sum(1 for s, _ in notes if i * 30 <= s < (i + 1) * 30) for i in range(10)])
