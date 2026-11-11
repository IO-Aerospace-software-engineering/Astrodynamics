"""CSPICE references of the operators CSPICE has not, for #482: GreaterOrEqual, LowerOrEqual and NotEqual on the CSPICE
path of the window searches.

The library sends GreaterOrEqual and LowerOrEqual to CSPICE as '>' and '<' (closed windows), and forms NotEqual as the
search window less the instants of '=', a window from each crossing to the next (the managed search's definition). This
script runs gfdist, gfposc and gfilum in spiceypy (CSPICE N0067) with '>', '<' and '=' as the native layer runs them
(IO.Astrodynamics/Constraints/GeometryFinder.cpp, API/Proxy.cpp: the ends of the search window are Time ticks, handed to
CSPICE as ticks / 1e7 seconds, each ET of the result the nearest tick, #625), and prints for each search the windows of
'>', '<' and '=' and the complement of '=' in the search window, as Time.ToString prints them.

The searches:
  - SpiceApiTest's: the Earth-Moon distance > 400 000 km, 2007-01-01 to 2007-04-01, step 1 day; the Moon's Z in
    DSS-13_TOPO from DSS-13 against 0, 2023-02-19 12:00 to 2023-02-20 12:00 TDB, step 60 s; the incidence of the Sun at
    longitude 2.2 deg, latitude 48 deg, altitude 0 on pck00011 against 90.8 deg, CN+S, 2021-05-17/18, step 4.5 h;
  - with the LRO SPK of the tests: the declination in J2000 of LRO seen from the Moon (lro.FindWindowsOnCoordinateConstraint
    (moon, ...)) against 0 rad, and the range of DSS-13 seen from LRO (dss13.FindWindowsOnCoordinateConstraint(lro, ...))
    against 372 000 km,
    755870520 to 755913720 s TDB (2023-12-15 00:00:51 to 12:00:51 UTC), step 60 s, no aberration (#482's
    setup, on whole TDB seconds);
  - the CLI's: the Earth-Moon distance against 400 000 km, 2024-03-01 to 2024-03-21 TDB, step 10 days, on the kernels of
    IO.Astrodynamics.CLI.Tests/Data, as Window.ToString prints it there.

Run: python3 -I operators_reference.py, from this directory (needs spiceypy).
"""
import datetime as D
import glob
import math
import os
from fractions import Fraction

import spiceypy as sp

HERE = os.path.dirname(os.path.abspath(__file__))
NET = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net')
TESTS = os.path.join(NET, 'IO.Astrodynamics.Tests', 'Data')
CLI = os.path.join(NET, 'IO.Astrodynamics.CLI.Tests', 'Data')
KERNELS = ('bsp', 'spk', 'tf', 'tpc', 'tls', 'bpc', 'ck', 'tsc', 'ti', 'ker')


def tick(x):
    return round(Fraction(x) * 10 ** 7)


def label(t):
    s, r = divmod(t, 10 ** 7)
    return (D.datetime(2000, 1, 1, 12) + D.timedelta(seconds=s)).isoformat() + '.%07d' % r


def span(t):
    s, r = divmod(t, 10 ** 7)
    h, rest = divmod(s, 3600)
    m, s = divmod(rest, 60)
    return f'{h:02d}:{m:02d}:{s:02d}' + (f'.{r:07d}' if r else '')


def windows(a, b, search, relate):
    cn = sp.cell_double(2000)
    sp.wninsd(tick(a) / 1e7, tick(b) / 1e7, cn)
    r = search(cn, relate)
    return [tuple(tick(x) for x in sp.wnfetd(r, i)) for i in range(sp.wncard(r))]


def complement(a, b, crossings):
    start, end = tick(a), tick(b)
    out, cursor = [], start
    for x, y in sorted(crossings):
        if x > cursor:
            out.append((cursor, x))
        cursor = max(cursor, y)
    if end > cursor:
        out.append((cursor, end))
    return out


def show(name, ws, cli=False):
    if cli:
        text = ' | '.join(f'From {label(x)} TDB to {label(y)} TDB - Length {span(y - x)}' for x, y in ws)
    else:
        text = ' '.join(f'[{label(x)} TDB, {label(y)} TDB]' for x, y in ws)
    print(f'{name} ({len(ws)}): {text}')


def run(name, a, b, search, cli=False):
    for relate in ('>', '<', '='):
        show(f'{name} {relate}', windows(a, b, search, relate), cli)
    show(f'{name} !', complement(a, b, windows(a, b, search, '=')), cli)


def load(directory, extra=()):
    sp.kclear()
    for root, _, files in sorted(os.walk(directory)):
        for f in sorted(files):
            if f.split('.')[-1].lower() in KERNELS:
                sp.furnsh(os.path.join(root, f))
    for f in extra:
        sp.furnsh(f)


load(os.path.join(TESTS, 'SolarSystem'), glob.glob(os.path.join(TESTS, 'LRO', '*.bsp')))


def dist(cn, relate):
    r = sp.cell_double(2000)
    sp.gfdist('301', 'NONE', '399', relate, 400000.0, 0.0, 86400.0, 1000, cn, r)
    return r


def coord(cn, relate):
    r = sp.cell_double(2000)
    sp.gfposc('301', 'DSS-13_TOPO', 'NONE', '399013', 'RECTANGULAR', 'Z', relate, 0.0, 0.0, 60.0, 1000, cn, r)
    return r


def ilum(cn, relate):
    radii = sp.bodvcd(399, 'RADII', 3)[1]
    f = (radii[0] - radii[2]) / radii[0]
    p = sp.georec(2.2 * math.pi / 180.0, 48.0 * math.pi / 180.0, 0.0, radii[0], f)
    r = sp.cell_double(2000)
    sp.gfilum('Ellipsoid', 'INCIDENCE', '399', '10', 'ITRF93', 'CN+S', '10', p, relate,
              math.pi * 0.5 - (-0.8 * math.pi / 180.0), 0.0, 4.5 * 3600.0, 1000, cn, r)
    return r


def lro_from_moon(cn, relate):
    r = sp.cell_double(2000)
    sp.gfposc('-85', 'J2000', 'NONE', '301', 'RA/DEC', 'DECLINATION', relate, 0.0, 0.0, 60.0, 1000, cn, r)
    return r


def dss13_from_lro(cn, relate):
    r = sp.cell_double(2000)
    sp.gfposc('399013', 'J2000', 'NONE', '-85', 'RA/DEC', 'RANGE', relate, 372000.0, 0.0, 60.0, 1000, cn, r)
    return r


run('distance', 220881665.18391809, 228657665.18565452, dist)
run('coordinate', 730036800.0, 730123200.0, coord)
run('illumination', 674524800.0, 674611200.0, ilum)
LRO_START, LRO_END = 755870520.0, 755913720.0
run('lro-moon declination', LRO_START, LRO_END, lro_from_moon)
run('dss13-lro range', LRO_START, LRO_END, dss13_from_lro)

load(CLI)


def cli_dist(cn, relate):
    r = sp.cell_double(2000)
    sp.gfdist('301', 'NONE', '399', relate, 400000.0, 0.0, 10 * 86400.0, 1000, cn, r)
    return r


def et(iso):
    d = D.datetime.fromisoformat(iso) - D.datetime(2000, 1, 1, 12)
    return d.days * 86400.0 + d.seconds


run('cli distance', et('2024-03-01T00:00:00'), et('2024-03-21T00:00:00'), cli_dist, cli=True)
