# Finds the MIDI -> mp3 time offset by cross-correlating an audio onset envelope with MIDI note onsets.
import numpy as np
from midiparse import parse
SR, HOP = 22050, 256
a = np.fromfile("song.raw", dtype=np.int16).astype(np.float32) / 32768
n = len(a) // HOP
frames = a[:n * HOP].reshape(n, HOP)
spec = np.abs(np.fft.rfft(frames * np.hanning(HOP), axis=1))
flux = np.maximum(np.diff(np.log1p(spec * 10), axis=0), 0).sum(1)
flux = np.concatenate([[0], flux]); flux -= np.convolve(flux, np.ones(43) / 43, 'same'); flux = np.maximum(flux, 0)
_, _, _, _, ev = parse("C:/Development/ConcertPrototype/Assets/Music/Tracks/carolus-rex.mid")
fr = SR / HOP
m = np.zeros(n)
for s, _, k, d in ev:
    if k == 'on' and int(s * fr) < n: m[int(s * fr)] += 1
best = []
for lag in range(int(-3 * fr), int(3 * fr)):
    best.append((np.dot(flux[max(0, lag):n + min(0, lag)], m[max(0, -lag):n - max(0, lag)]), lag / fr))
best.sort(reverse=True)
print([(round(o, 3), round(v, 1)) for v, o in best[:5]])
