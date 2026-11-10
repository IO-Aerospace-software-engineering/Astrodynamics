"""UTC instants whose TDB label, back in TAI, is not their TAI: an exact replica of the conversions of Time
(UTC + 37 s = TAI, + 32.184 s = TT, + the TDB - TT series at the TT centuries, rounded or truncated to the tick; back,
- 32.184 s - the series at the TDB centuries). Validated by the instants the tests held under truncation (mode trunc gives them back), for #625.

Run: python3 -I label_round_trip_search.py 8000000 (the number of random UTC instants of 2021, seed 625).
"""
import math, datetime as D, random, sys
from fractions import Fraction
def ticks(dt): d = dt - D.datetime(1, 1, 1); return (d.days * 86400 + d.seconds) * 10 ** 7 + d.microseconds * 10
J = ticks(D.datetime(2000, 1, 1, 12))
def delta(t):
    c = ((t - J) / 864000000000.0) / 36525.0
    g = 2 * math.pi * (357.528 + 35999.05 * c) / 360.0
    return 0.001658 * math.sin(g + 0.0167 * math.sin(g))
MODES = {'round': lambda x: round(Fraction(x) * 10 ** 7), 'trunc': lambda x: math.trunc(Fraction(x) * 10 ** 7)}
def back(u, f, leap=37):
    tai = u + leap * 10 ** 7; tt = tai + 321840000; tdb = tt + f(delta(tt)); return tdb - 321840000 - f(delta(tdb)) - tai
later = ticks(D.datetime(2021, 6, 2, 14, 51, 26)) + 1940007
off = ticks(D.datetime(2021, 3, 20, 0, 5, 5)) + 969684
for m, f in MODES.items(): print(m, 'later', back(later, f), 'off', back(off, f))
rng = random.Random(625); base = ticks(D.datetime(2021, 1, 1)); span = ticks(D.datetime(2022, 1, 1)) - base
found = {1: [], -1: []}
for i in range(int(sys.argv[1])):
    u = base + rng.randrange(span)
    d = delta(u + 37 * 10 ** 7 + 321840000) * 1e7
    if abs(abs(d - math.floor(d)) - 0.5) > 1e-4: continue
    b = back(u, MODES['round'])
    if b: found[b].append(u)
for k, v in found.items():
    print(k, len(v), [(x, str(D.datetime(1, 1, 1) + D.timedelta(microseconds=x // 10)), x % 10) for x in v[:3]])
