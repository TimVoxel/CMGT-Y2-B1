import struct, sys
from collections import defaultdict

def read_vlq(d, i):
    v = 0
    while True:
        b = d[i]; i += 1
        v = (v << 7) | (b & 0x7f)
        if not b & 0x80: return v, i

def parse(path):
    d = open(path, 'rb').read()
    assert d[:4] == b'MThd'
    fmt, ntrk, div = struct.unpack('>HHH', d[8:14])
    i = 14
    events = []  # (tick, kind, data)
    names = {}
    for t in range(ntrk):
        assert d[i:i+4] == b'MTrk', d[i:i+4]
        ln = struct.unpack('>I', d[i+4:i+8])[0]
        j = i + 8; end = j + ln; tick = 0; status = None
        while j < end:
            dt, j = read_vlq(d, j); tick += dt
            b = d[j]
            if b == 0xFF:
                typ = d[j+1]; l, k = read_vlq(d, j+2); data = d[k:k+l]; j = k + l
                if typ == 0x51: events.append((tick, 'tempo', int.from_bytes(data, 'big')))
                elif typ == 0x03: names[t] = data.decode('latin1')
                elif typ == 0x58: events.append((tick, 'timesig', tuple(data)))
                continue
            if b in (0xF0, 0xF7):
                l, k = read_vlq(d, j+1); j = k + l; continue
            if b & 0x80: status = b; j += 1
            hi = status & 0xF0; ch = status & 0x0F
            if hi in (0xC0, 0xD0): p = (d[j],); j += 1
            else: p = (d[j], d[j+1]); j += 2
            if hi == 0x90 and p[1] > 0: events.append((tick, 'on', (t, ch, p[0], p[1])))
            elif hi == 0x80 or (hi == 0x90 and p[1] == 0): events.append((tick, 'off', (t, ch, p[0])))
            elif hi == 0xC0: events.append((tick, 'prog', (t, ch, p[0])))
        i = end
    events.sort(key=lambda e: e[0])
    # tempo map -> seconds
    tempo = 500000; last_tick = 0; secs = 0.0
    out = []
    for tick, kind, data in events:
        secs += (tick - last_tick) * tempo / 1e6 / div; last_tick = tick
        if kind == 'tempo': tempo = data
        out.append((secs, tick, kind, data))
    return fmt, ntrk, div, names, out

if __name__ == '__main__':
    fmt, ntrk, div, names, ev = parse(sys.argv[1])
    print('fmt', fmt, 'tracks', ntrk, 'div', div, names)
    tempos = [(round(s,2), round(60e6/d,2)) for s,_,k,d in ev if k=='tempo']
    print('tempos', len(tempos), tempos[:10])
    print('timesig', [(round(s,2), d) for s,_,k,d in ev if k=='timesig'][:5])
    progs = {(d[0], d[1]): d[2] for s,_,k,d in ev if k=='prog'}
    print('programs', progs)
    stats = defaultdict(list)
    for s,_,k,d in ev:
        if k == 'on': stats[(d[0], d[1])].append((s, d[2]))
    for key, ns in sorted(stats.items()):
        ps = [p for _,p in ns]
        print(key, 'n', len(ns), 'first %.2f last %.2f' % (ns[0][0], ns[-1][0]), 'pitch', min(ps), max(ps), 'avg %.1f' % (sum(ps)/len(ps)))
