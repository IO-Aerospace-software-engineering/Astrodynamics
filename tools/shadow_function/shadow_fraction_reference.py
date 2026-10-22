"""Reference values of the shadow fraction of two discs, at 60 digits, for the tests of #434.

The shadow fraction nu is the part of the solar disc (angular radius a) covered by the disc of an occulting body
(angular radius b) whose centre is c away. In a partial eclipse, |a - b| < c < a + b, the covered area is the sum of the
two circular segments cut by the common chord (E. W. Weisstein, "Circle-Circle Intersection", MathWorld, eqs. 10 to 13,
read on 2026-10-22):

    A = a^2 acos(d1 / a) - d1 sqrt(a^2 - d1^2) + b^2 acos(d2 / b) - d2 sqrt(b^2 - d2^2),
    d1 = (c^2 - b^2 + a^2) / (2 c), d2 = c - d1,

and nu = A / (pi a^2); nu = 1 when c <= b - a (total), b^2 / a^2 when c <= a - b (annular), 0 when c >= a + b.

The geometries are those of the library's cases: the Sun seen from 1 au, the Earth from a LEO at 400 km and 700 km and
from GEO, the Moon slightly smaller and slightly larger than the Sun. For each, separations at relative depths of 1e-12
to 1e-2 of the width of the penumbra inside each contact, and at fractions of that width across it. Each input is the
double nearest to its target, written with 17 significant digits; the reference is the exact shadow fraction of these
doubles, evaluated with mpmath at 60 digits, so that it does not share the rounding of a computation in doubles.

Run: python3 -I shadow_fraction_reference.py > shadow_fraction_reference.json (needs mpmath).
"""
import json
import math

import mpmath as mp

mp.mp.dps = 60

AU = 149597870700.0
SUN_RADIUS = 695700000.0
EARTH_RADIUS = 6378136.6
SUN = math.asin(SUN_RADIUS / AU)

GEOMETRIES = {
    'LEO 400 km': (SUN, math.asin(EARTH_RADIUS / (EARTH_RADIUS + 400.0e3))),
    'LEO 700 km': (SUN, math.asin(EARTH_RADIUS / (EARTH_RADIUS + 700.0e3))),
    'GEO': (SUN, math.asin(EARTH_RADIUS / 42164137.0)),
    'Moon smaller than the Sun': (SUN, 4.3e-3),
    'Moon larger than the Sun': (SUN, 4.66e-3),
}

DEPTHS = [1e-12, 1e-10, 1e-8, 1e-6, 1e-4, 1e-2]
FRACTIONS = [0.1, 0.25, 0.5, 0.75, 0.9]


def exact(a, b, c):
    a, b, c = mp.mpf(a), mp.mpf(b), mp.mpf(c)
    if c >= a + b:
        return mp.mpf(0)
    if c <= b - a:
        return mp.mpf(1)
    if c <= a - b:
        return b * b / (a * a)
    d1 = (c * c - b * b + a * a) / (2 * c)
    d2 = c - d1
    area = a * a * mp.acos(d1 / a) - d1 * mp.sqrt(a * a - d1 * d1) + b * b * mp.acos(d2 / b) - d2 * mp.sqrt(
        b * b - d2 * d2)
    return area / (mp.pi * a * a)


def main():
    points = []
    for name, (a, b) in GEOMETRIES.items():
        inner = abs(b - a)
        outer = a + b
        width = outer - inner
        separations = []
        for depth in DEPTHS:
            separations.append((f'outer contact, {depth:.0e} of the width inside', outer - depth * width))
            separations.append((f'inner contact, {depth:.0e} of the width inside', inner + depth * width))
        for fraction in FRACTIONS:
            separations.append((f'{fraction} of the width', inner + fraction * width))
        for label, c in separations:
            points.append({'geometry': name, 'where': label, 'a': a, 'b': b,
                           'c': c, 'inner_depth': mp.nstr(mp.mpf(c) - mp.mpf(inner), 20),
                           'outer_depth': mp.nstr(mp.mpf(outer) - mp.mpf(c), 20),
                           'shadow_fraction': mp.nstr(exact(a, b, c), 30)})
    print(json.dumps({'generated_by': 'tools/shadow_function/shadow_fraction_reference.py, mpmath ' + mp.__version__ +
                      ' at 60 digits', 'units': 'rad', 'points': points}, indent=1))


if __name__ == '__main__':
    main()
