"""The windows of SpiceApiTest replicated in spiceypy (CSPICE N0067), for #625.

Each search as the native layer runs it (IO.Astrodynamics/Constraints/GeometryFinder.cpp, API/Proxy.cpp): the ends of
the search window are Time ticks, handed to CSPICE as ticks / 1e7 seconds, and each ET of the result becomes a Time
tick again; mode trunc is the truncation of every conversion before #625, mode nearest the nearest tick after it. Mode
trunc gives every string SpiceApiTest held before #625, which validates the replica; mode nearest gives those it holds.

Run: python3 -I spice_windows_reference.py, from this directory (needs spiceypy).
"""
import glob, math, datetime as dt
from fractions import Fraction
import spiceypy as sp
import os
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests',
                 'Data', 'SolarSystem')
for f in sorted(glob.glob(D + '/*')):
    if not f.endswith('EGM2008_to70_TideFree'): sp.furnsh(f)
TICK = {'trunc': lambda x: math.trunc(Fraction(x) * 10 ** 7), 'nearest': lambda x: round(Fraction(x) * 10 ** 7)}
def label(t):
    s, r = divmod(t, 10 ** 7)
    return (dt.datetime(2000, 1, 1, 12) + dt.timedelta(seconds=s)).isoformat() + '.%07d' % r
def run(name, a, b, search):
    for mode in ('trunc', 'nearest'):
        cn = sp.cell_double(2000); sp.wninsd(TICK[mode](a) / 1e7, TICK[mode](b) / 1e7, cn)
        r = search(cn)
        out = []
        for i in range(sp.wncard(r)):
            x, y = sp.wnfetd(r, i); out.append(f'[{label(TICK[mode](x))}, {label(TICK[mode](y))}]')
        print(name, mode, ' '.join(out))
def dist(cn):
    r = sp.cell_double(2000); sp.gfdist('301', 'NONE', '399', '>', 400000.0, 0.0, 86400.0, 1000, cn, r); return r
def occ(cn):
    r = sp.cell_double(2000); sp.gfoclt('ANY', '301', 'ELLIPSOID', 'IAU_MOON', '10', 'ELLIPSOID', 'IAU_SUN', 'LT', '399', 3600.0, cn, r); return r
def coord(cn):
    r = sp.cell_double(2000); sp.gfposc('301', 'DSS-13_TOPO', 'NONE', '399013', 'RECTANGULAR', 'Z', '>', 0.0, 0.0, 60.0, 1000, cn, r); return r
def ilum(cn):
    radii = sp.bodvcd(399, 'RADII', 3)[1]
    f = (radii[0] - radii[2]) / radii[0]
    p = sp.georec(2.2 * math.pi / 180.0, 48.0 * math.pi / 180.0, 0.0, radii[0], f)
    r = sp.cell_double(2000)
    sp.gfilum('Ellipsoid', 'INCIDENCE', '399', '10', 'ITRF93', 'CN+S', '10', p, '<', math.pi * 0.5 - (-0.8 * math.pi / 180.0), 0.0, 4.5 * 3600.0, 1000, cn, r)
    return r
run('distance', 220881665.18391809, 228657665.18565452, dist)
run('occultation', 61473664.183390938, 61646464.183445148, occ)
run('coordinate', 730036800.0, 730123200.0, coord)
run('illumination', 674524800.0, 674611200.0, ilum)
