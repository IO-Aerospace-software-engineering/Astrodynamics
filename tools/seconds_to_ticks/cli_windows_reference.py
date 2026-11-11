"""The windows of the CLI GeometryFinderTests replicated in spiceypy (CSPICE N0067), for #625.

Each search as the native layer runs it (IO.Astrodynamics/Constraints/GeometryFinder.cpp, API/Proxy.cpp) on the kernels
the CLI tests load (IO.Astrodynamics.CLI.Tests/Data, every kernel under it): the ends of the search window are Time
ticks, handed to CSPICE as ticks / 1e7 seconds, and each ET of the result becomes a Time tick again, printed as
Window.ToString prints it (From ... TDB to ... TDB - Length ...). Mode trunc is the truncation of every conversion
before #625, mode nearest the nearest tick after it; trunc gives the strings the tests held before #625.

Run: python3 -I cli_windows_reference.py, from this directory (needs spiceypy).
"""
import datetime as D
import math
import os
from fractions import Fraction

import spiceypy as sp

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.CLI.Tests', 'Data')
for root, _, files in sorted(os.walk(DATA)):
    for f in sorted(files):
        if f.split('.')[-1].lower() in ('bsp', 'spk', 'tf', 'tpc', 'tls', 'bpc', 'ck', 'tsc', 'ti', 'ker'):
            sp.furnsh(os.path.join(root, f))
TICK = {'trunc': lambda x: math.trunc(Fraction(x) * 10 ** 7), 'nearest': lambda x: round(Fraction(x) * 10 ** 7)}
J = (D.datetime(2000, 1, 1, 12) - D.datetime(1, 1, 1))


def label(t):
    s, r = divmod(t, 10 ** 7)
    return (D.datetime(2000, 1, 1, 12) + D.timedelta(seconds=s)).isoformat() + '.%07d' % r


def span(t):
    s, r = divmod(t, 10 ** 7)
    h, rest = divmod(s, 3600)
    m, s = divmod(rest, 60)
    return f'{h:02d}:{m:02d}:{s:02d}' + (f'.{r:07d}' if r else '')


def run(name, a, b, search):
    for mode in ('trunc', 'nearest'):
        cn = sp.cell_double(2000)
        sp.wninsd(TICK[mode](a) / 1e7, TICK[mode](b) / 1e7, cn)
        r = search(cn)
        out = []
        for i in range(sp.wncard(r)):
            x, y = sp.wnfetd(r, i)
            tx, ty = TICK[mode](x), TICK[mode](y)
            out.append(f'From {label(tx)} TDB to {label(ty)} TDB - Length {span(ty - tx)}')
        print(name, mode, ' | '.join(out))


def et(iso):
    d = D.datetime.fromisoformat(iso) - D.datetime(2000, 1, 1, 12)
    return d.days * 86400.0 + d.seconds


def cell():
    return sp.cell_double(2000)


def coordinate(cn):
    r = cell(); sp.gfposc('10', 'ECLIPJ2000', 'NONE', '399', 'RECTANGULAR', 'Z', '=', 0.0, 0.0, 20 * 86400.0, 1000, cn, r); return r


def distance(cn):
    r = cell(); sp.gfdist('301', 'NONE', '399', '=', 400000.0, 0.0, 10 * 86400.0, 1000, cn, r); return r


def occultation(cn):
    r = cell(); sp.gfoclt('ANY', '301', 'ELLIPSOID', 'IAU_MOON', '10', 'ELLIPSOID', 'IAU_SUN', 'LT', '399', 3600.0, cn, r); return r


def fov(cn):
    r = cell(); sp.gftfov('-172600', '399', 'ELLIPSOID', 'ITRF93', 'LT', '-172', 3600.0, cn, r); return r


def illumination(cn):
    radii = sp.bodvcd(399, 'RADII', 3)[1]
    p = sp.georec(0.0349, 0.8378, 0.0, radii[0], (radii[0] - radii[2]) / radii[0])
    r = cell()
    sp.gfilum('Ellipsoid', 'INCIDENCE', '399', '10', 'ITRF93', 'CN+S', '10', p, '<', math.pi * 0.5 - (-0.8 * math.pi / 180.0),
              0.0, 4.5 * 3600.0, 1000, cn, r)
    return r


run('coordinate', et('2024-03-01T00:00:00'), et('2024-05-01T00:00:00'), coordinate)
run('distance', et('2024-03-01T00:00:00'), et('2024-03-21T00:00:00'), distance)
run('occultation', 61473664.183390938, 61646464.183445148, occultation)
run('fov', 676555200.0, 676561647.0, fov)
run('illumination', 674524800.0, 674611200.0, illumination)
