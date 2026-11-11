"""The lunar occultation of a star seen from DSS-13, the reference of #533: a star is a direction, read at the epoch.

The convention: a catalog position at an epoch is the direction of the light that reaches the solar system barycenter
then (as SOFA's iauPmsafe reads a catalog: only the change of the light time is corrected, to be verified by S. Guillet),
so the star seen at t is its catalog position moved by its proper motion to t, minus the observer's position from the
barycenter at t: no light time on the star, whatever the correction. The field-of-view search reads a star the same way
since #517, as CSPICE's gfrfov reads a ray.

The star (the library's Star): right ascension and declination at the catalog epoch S = 2026-03-01 12:00 TDB
(825638400 s), placed on the Moon's LT direction from DSS-13 at S, at the distance D from DSS-13, its parallax the
inverse of its distance from the barycenter in parsecs (Constants.Parsec2Meters = 3.085677581e16 m); its proper motion in
right ascension pmra (rad per Julian year of 365.25 days, added to the right ascension, as Star.GetEquatorialCoordinates
does).

The occultation: the Moon a sphere of 1737.4 km (pck00011's radii), its position from DSS-13 that of spkpos with the
correction (NONE, LT or CN), the star as above; the entry and the exit are the zeros of asin(R/d) - sep(t) around S,
found by bisection to 1e-9 s. The windows of the library (Star as a point target, the Moon an ellipsoid in IAU_MOON,
OccultationType.Any) are compared with them.

The direction of Star.GetEphemeris(S + 1 h, DSS-13, ICRF, correction): the unit vector of the star minus DSS-13 at the
epoch; with +S, rotated by stelab with DSS-13's velocity from the barycenter.

Run: python3 -I star_occultation_reference.py, from this directory (needs spiceypy).
"""
import math
import os

import spiceypy as sp

HERE = os.path.dirname(os.path.abspath(__file__))
KERNELS = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data', 'SolarSystem')
for f in sorted(os.listdir(KERNELS)):
    if f.split('.')[-1].lower() in ('bsp', 'tf', 'tpc', 'tls', 'bpc', 'ker'):
        sp.furnsh(os.path.join(KERNELS, f))

S = 825638400.0
PARSEC = 3.085677581e16  # m
YEAR = 365.25 * 86400.0
R_MOON = 1737.4  # km
ARCSEC = math.pi / 180.0 / 3600.0


def dss13(t):
    return sp.spkezr('399013', t, 'J2000', 'NONE', '0')[0]


def star_definition(distance_pc):
    u = sp.vhat(sp.spkpos('301', S, 'J2000', 'LT', '399013')[0])
    p = sp.vadd(dss13(S)[:3], sp.vscl(distance_pc * PARSEC / 1000.0, u))  # km from the barycenter
    r, ra, dec = sp.recrad(p)
    parallax = PARSEC / 1000.0 / r  # arcsec
    return ra, dec, parallax


def star_from_dss13(t, ra0, dec0, parallax, pmra):
    ra = ra0 + (t - S) / YEAR * pmra
    d = PARSEC / 1000.0 / parallax
    p = sp.radrec(d, ra, dec0)
    return sp.vsub(p, dss13(t)[:3])


def margin(t, star, abcorr):
    moon = sp.spkpos('301', t, 'J2000', abcorr, '399013')[0]
    return math.asin(R_MOON / sp.vnorm(moon)) - sp.vsep(moon, star(t))


def root(f, a, b):
    fa = f(a)
    for _ in range(200):
        m = 0.5 * (a + b)
        if b - a < 1e-9:
            break
        fm = f(m)
        if (fm > 0) == (fa > 0):
            a, fa = m, fm
        else:
            b = m
    return 0.5 * (a + b)


def events(star, abcorr):
    f = lambda t: margin(t, star, abcorr)
    grid = [S - 3 * 3600.0 + 60.0 * i for i in range(361)]
    values = [f(t) for t in grid]
    out = []
    for i in range(len(grid) - 1):
        if (values[i] > 0) != (values[i + 1] > 0):
            out.append(root(f, grid[i], grid[i + 1]))
    return out


for distance in (20.0, 100.0):
    ra, dec, parallax = star_definition(distance)
    print(f'star {distance:g} pc: ra {ra!r} dec {dec!r} parallax {parallax!r}')
    for pm in (0.0, 0.1):
        pmra = pm * ARCSEC
        star = lambda t: star_from_dss13(t, ra, dec, parallax, pmra)
        for abcorr in ('NONE', 'LT', 'CN'):
            print(f'  pmra {pm:g} arcsec/yr {abcorr}: {", ".join(repr(x) for x in events(star, abcorr))}')
        t = S + 3600.0
        u = sp.vhat(star(t))
        state = dss13(t)
        aberrated = sp.vhat(sp.stelab(star(t), state[3:]))
        print(f'  pmra {pm:g} direction at S + 1 h: {[float(x) for x in u]!r}; with +S: {[float(x) for x in aberrated]!r}')
