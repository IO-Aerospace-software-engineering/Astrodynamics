"""Exact nearest ticks of seconds, and the LRO state at the start of its SPK, for the tests of #625.

A tick of System.DateTime is 100 ns. Each value is a double; its exact value is a binary rational (Python Fraction),
and the reference is the tick count nearest it, ties to even (round of a Fraction is round half to even), the result
Time.SecondsToTicks must give: Time.Create, AddSeconds, AddMilliseconds and AddMicroseconds, and the TDB - TT offset of
the TDB conversions. The truncation toward zero of DateTime.AddSeconds and TimeSpan.FromSeconds is written beside it.

The values:
- those of the issue: +-0.12345678999 s, and a and b, the start and the end of the coverage of -85 (LRO) in the test
  kernel Data/LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp (spkcov);
- exact ties, odd multiples of 2^-8 s (39062.5 ticks and so on), with and without whole seconds, both signs;
- the doubles nearest (n + 1/2) ticks, |n| < 2^40, which are not ties;
- 8 doubles near a half tick whose fraction times 1e7, rounded in double, is a half while the exact product is on one
  side of it (found by a search, seed 625): rounding that product, as the issue's helper does, misses their nearest tick;
- 1500 uniform ET from 1968-04-24 (-2^30 s) to 2031-09-09 (+2^30.8 s), seed 625, both signs;
- near ties of values above -1 unit and below 0, where the fraction 1 - |value| of a floor would round (the review of
  #625): the decimal literals -(k + 1/2) 1e-7 s, -(k + 1/2) 1e-4 and -(k + 1/2) 0.1 (half ticks read as milliseconds and
  as microseconds), with their positive mirrors, and 60 doubles nearest -(n + 1/2) ticks below a second.

Per value: s, its repr; ticks, the nearest tick; truncated, the truncation toward zero. Each value is also read as
milliseconds and as microseconds (the ticks nearest value x 1e4 and value x 10), the references of AddMilliseconds and
AddMicroseconds: ms_ticks and us_ticks.

The LRO state is spkezr(-85, a, J2000, NONE, 399) and spkezr(-85, b, ...) in m and m/s, what new Spacecraft(-85, "LRO",
Time.CreateTDB(a)) reads about the Earth (#620).

Run: python3 -I seconds_to_ticks_reference.py > seconds_to_ticks_reference.json (needs spiceypy), from this directory,
and copy the output to IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SecondsToTicksReference/.
"""
import json
import math
import os
import random
from fractions import Fraction

import spiceypy as spice

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data')
TICKS = 10_000_000


def nearest(value, per_unit=TICKS):
    return round(Fraction(value) * per_unit)


def truncated(value, per_unit=TICKS):
    return math.trunc(Fraction(value) * per_unit)


spice.furnsh(os.path.join(DATA, 'SolarSystem', 'latest_leapseconds.tls'))
spice.furnsh(os.path.join(DATA, 'SolarSystem', 'de440s.bsp'))
spice.furnsh(os.path.join(DATA, 'LRO', 'lro-reconstructed-2023349_2024075_v01_little_endian.bsp'))
cover = spice.spkcov(os.path.join(DATA, 'LRO', 'lro-reconstructed-2023349_2024075_v01_little_endian.bsp'), -85)
assert spice.wncard(cover) == 1
a, b = (float(x) for x in spice.wnfetd(cover, 0))

values = [('issue', 0.12345678999), ('issue', -0.12345678999), ('coverage start', a), ('coverage end', b)]
for k in (1, 3, 5, 255):
    for whole in (0.0, 1.0, 755870469.0, -1000.0):
        for sign in (1.0, -1.0):
            values.append(('tie', sign * (whole + k / 256.0)))
rng = random.Random(625)
for _ in range(200):
    n = rng.randrange(-2 ** 40, 2 ** 40)
    values.append(('near tie', float(Fraction(2 * n + 1, 2 * TICKS))))
def product_rounded(value):
    whole = math.floor(value)
    return int(whole) * TICKS + round((value - whole) * float(TICKS))


hazards = []
while len(hazards) < 8:
    candidate = float(rng.randrange(-2 ** 20, 2 ** 20) + Fraction(2 * rng.randrange(TICKS) + 1, 2 * TICKS))
    if product_rounded(candidate) != nearest(candidate):
        hazards.append(candidate)
values += [('double rounding', x) for x in hazards]
for _ in range(1500):
    values.append(('grid', rng.uniform(-2.0 ** 30, 2.0 ** 30.8)))
# Near ties of values in (-1, 0) units, where 1 - |value| is not exact in double (review of #625), and their positive
# mirrors: the decimal literals -(k + 1/2) 1e-7 s, -(k + 1/2) 1e-4 (half ticks as ms), -(k + 1/2) 0.1 (as us), and the
# doubles nearest -(n + 1/2) ticks below a second
for k in [0, 1, 2, 3, 4, 7, 12, 24, 99, 123, 999, 4999] + sorted(rng.sample(range(5000, 5_000_000), 60)):
    for sign in ('-', ''):
        values.append(('sub-unit near tie', float(f'{sign}{k}.5e-7')))
for k in [0, 1, 2, 3, 4, 9, 24, 99, 499, 4999]:
    for sign in ('-', ''):
        values.append(('sub-unit near tie', float(f'{sign}{k}.5e-4')))
for k in range(5):
    for sign in ('-', ''):
        values.append(('sub-unit near tie', float(f'{sign}0.{k}5')))
for _ in range(60):
    n = rng.randrange(TICKS // 2)
    values.append(('sub-unit near tie', -float(Fraction(2 * n + 1, 2 * TICKS))))

rows = []
for kind, value in values:
    rows.append({'kind': kind, 's': repr(value), 'ticks': nearest(value), 'truncated': truncated(value),
                 'ms_ticks': nearest(value, 10_000), 'us_ticks': nearest(value, 10)})

states = {}
for name, et in (('start', a), ('end', b)):
    state, _ = spice.spkezr('-85', et, 'J2000', 'NONE', '399')
    states[name] = {'et': repr(et), 'state_m': [repr(float(x) * 1000.0) for x in state]}

text = json.dumps({
    'description': 'Exact nearest ticks (100 ns, ties to even) of doubles in seconds, and the state of LRO at the ends '
                   'of its SPK, for #625; tools/seconds_to_ticks/seconds_to_ticks_reference.py, Python Fraction and '
                   f'spiceypy {spice.__version__} ({spice.tkvrsn("TOOLKIT")}).',
    'coverage': {'start': repr(a), 'end': repr(b)},
    'lro_states': states,
    'values': ['@'],
}, indent=1)
print(text.replace('"@"', ',\n  '.join(json.dumps(row) for row in rows)))
