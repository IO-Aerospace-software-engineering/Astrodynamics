"""Reference times to a mean anomaly on an ellipse, at 60 digits, for the tests of #474.

On an ellipse the next passage at the mean anomaly M_t after an epoch at the mean anomaly M_0 is
t = ((M_t - M_0) mod 2pi) / n, n = sqrt(mu / a^3), whatever the turn of either anomaly (Vallado, Fundamentals of
Astrodynamics and Applications, 4th ed., section 2.2, Kepler's equation); at a true anomaly nu, M_t = E - e sin E with
tan(E/2) = sqrt((1 - e)/(1 + e)) tan(nu/2). The epochs of the tests are J2000 TDB; about the Earth the library counts TT
seconds, about the Sun TDB seconds, and so does the reference: it is the elapsed time of the two-body motion.

The orbits are those of the issue: about the Earth, a = 1e7 m, e = 0.3, i = 0.5, Omega = 0.3, omega = 0.2 rad, at
M = 5 rad (a StateVector holds it as 5 - 2pi) and at M = 1 rad; about the Sun, the comet a = 1e4 au, e = 0.999999 (the
double 1 - 1e-6, the one eccentricity at which KeplerianElements keeps its mean anomaly unreduced), at M = -0.02 rad and
at M = 2pi - 0.001 rad moved by 300 Julian years (9 467 280 000 s, M + n t unreduced). Every GM is the kernel's
(gm_de440.tpc, km^3/s^2, times 1e9 in doubles), and the au is 149 597 870 700 m, as the tests build the orbits.

Run: python3 -I epoch_at_mean_anomaly_reference.py from this directory (needs mpmath and spiceypy); the values are copied
into IO.Astrodynamics.Net/IO.Astrodynamics.Tests/OrbitalParameters/EpochAtMeanAnomalyTests.cs.
"""
import math
import os

import mpmath as mp
import spiceypy as spice

mp.mp.dps = 60
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data', 'SolarSystem')
spice.furnsh(os.path.join(DATA, 'gm_de440.tpc'))
TWO_PI = 2 * mp.pi


def gm(naif_id):
    return float(spice.bodvcd(naif_id, 'GM', 1)[1][0]) * 1e9


def mean_motion(mu, a):
    return mp.sqrt(mp.mpf(mu) / mp.mpf(a) ** 3)


def to_mean_anomaly(target, m0, mu, a):
    return mp.fmod(mp.mpf(target) - mp.mpf(m0), TWO_PI) % TWO_PI / mean_motion(mu, a)


def mean_anomaly_at(nu, e):
    nu, e = mp.mpf(nu), mp.mpf(e)
    ea = 2 * mp.atan(mp.sqrt((1 - e) / (1 + e)) * mp.tan(nu / 2))
    return ea - e * mp.sin(ea)


earth, sun = gm(399), gm(10)
au = 149597870700.0
print(f'GM Earth {earth!r}, GM Sun {sun!r}')
a, e = 1e7, 0.3
print(f'Earth period {mp.nstr(TWO_PI / mean_motion(earth, a), 20)} s')
print(f'M0 = 5, target 5.5: {mp.nstr(to_mean_anomaly(5.5, 5.0, earth, a), 20)} s')
print(f'M0 = 5, nu = 300 deg: {mp.nstr(to_mean_anomaly(mean_anomaly_at(math.radians(300.0), e), 5.0, earth, a), 20)} s')
for target in (-7.0, 8.0, 0.5, 1.0 + 4 * math.pi):
    print(f'M0 = 1, target {target!r}: {mp.nstr(to_mean_anomaly(target, 1.0, earth, a), 20)} s')
print(f'M0 = 5, target -7: {mp.nstr(to_mean_anomaly(-7.0, 5.0, earth, a), 20)} s')
print(f'M0 = 5, target 8: {mp.nstr(to_mean_anomaly(8.0, 5.0, earth, a), 20)} s')

ac, ec = 1e4 * au, 1.0 - 1e-6
n = mean_motion(sun, ac)
print(f'comet period {mp.nstr(TWO_PI / n, 20)} s, e = {ec!r}')
print(f'M0 = -0.02, target 2pi - 0.01: {mp.nstr(to_mean_anomaly(2 * math.pi - 0.01, -0.02, sun, ac), 20)} s')
m300 = mp.mpf(2 * math.pi - 0.001) + n * 9467280000
print(f'M0 = 2pi - 0.001 after 300 yr: M = {mp.nstr(m300, 20)}')
print(f'  target 0.002: {mp.nstr(to_mean_anomaly(0.002, m300, sun, ac), 20)} s')
print(f'  target 0.0005: {mp.nstr(to_mean_anomaly(0.0005, m300, sun, ac), 20)} s (beyond TimeSpan.MaxValue, 922337203685.4775807 s)')
