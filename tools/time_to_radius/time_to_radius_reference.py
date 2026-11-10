"""Reference times to a radius on a hyperbola, at 50 digits, for the tests of #627.

From the periapsis (M0 = 0), the hyperbolic anomaly at the radius r is cosh H = (1 + r/|a|)/e, the mean anomaly
M = e sinh H - H and the time t = M / n, n = sqrt(mu/|a|^3) (Vallado, Fundamentals of Astrodynamics and Applications,
4th ed., section 2.2, the hyperbolic Kepler equation). As r goes to infinity, t goes to infinity (t ~ r / v_inf): the
orbit never reaches an infinite radius.

The orbits are those of the issue: a = -1.5e11 m, e = 2 about the Sun at 1e12 m and 1e15 m; a = -5e6 m, e = 2 about the
Moon to its sphere of influence, a_Moon (GM_Moon / GM_Earth)^(2/5), a_Moon the osculating semi-major axis of the Moon
about the Earth in ECLIPJ2000 with GM_Earth at 2026-01-20T06:00:00 TDB (spkezr, oscelt), as CelestialBody computes it.
Every GM is the kernel's (gm_de440.tpc, km^3/s^2, times 1e9 in doubles), as the library reads it.

Run: python3 -I time_to_radius_reference.py from this directory (needs mpmath and spiceypy); the values are copied into
IO.Astrodynamics.Net/IO.Astrodynamics.Tests/OrbitalParameters/TimeToRadiusTests.cs.
"""
import os

import mpmath as mp
import spiceypy as spice

mp.mp.dps = 50
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data', 'SolarSystem')
for kernel in ('latest_leapseconds.tls', 'de440s.bsp', 'gm_de440.tpc', 'pck00011.tpc'):
    spice.furnsh(os.path.join(DATA, kernel))


def gm(naif_id):
    return float(spice.bodvcd(naif_id, 'GM', 1)[1][0]) * 1e9


def time_to_radius(mu, a, e, r):
    mu, a, e, r = mp.mpf(mu), mp.mpf(a), mp.mpf(e), mp.mpf(r)
    h = mp.acosh((1 + r / abs(a)) / e)
    return (e * mp.sinh(h) - h) / mp.sqrt(mu / abs(a) ** 3)


et = spice.str2et('2026-01-20 06:00:00 TDB')
sun, earth, moon = gm(10), gm(399), gm(301)
print(f'GM Sun {sun!r}, GM Earth {earth!r}, GM Moon {moon!r}')
for r in (1e12, 1e15):
    print(f'Sun, a = -1.5e11, e = 2, r = {r!r}: t = {mp.nstr(time_to_radius(sun, -1.5e11, 2.0, r), 20)} s')

state, _ = spice.spkezr('301', et, 'ECLIPJ2000', 'NONE', '399')
elements = spice.oscelt([x * 1e3 for x in state], et, earth)
a_moon = float(elements[0]) / (1.0 - float(elements[1]))
soi = a_moon * (moon / earth) ** 0.4
print(f'Moon SOI {soi!r} m (a_Moon {a_moon!r} m)')
print(f'Moon, a = -5e6, e = 2, r = SOI: t = {mp.nstr(time_to_radius(moon, -5e6, 2.0, soi), 20)} s')
