"""References of the conic state of Keplerian elements given by their true anomaly, at 60 digits, for the tests of #472.

For the doubles a, e, i, node, argument of periapsis and nu that the tests give KeplerianElements (q = 7000 km,
a = fl(q / (1 - e)), i = 0.2, node 0.3 and argument 0.4 rad, as in the issue), the reference is the perifocal state of
the conic of these doubles,

    p = a (1 - e^2),  r = p / (1 + e cos nu) (cos nu, sin nu, 0),  v = sqrt(mu / p) (-sin nu, e + cos nu, 0),

turned by R3(node) R1(i) R3(argument) (D. A. Vallado, Fundamentals of Astrodynamics and Applications, 4th ed., 2013,
algorithm 10 COE2RV; confirmed by S. Guillet, 2026-11-15), evaluated with mpmath at 60 digits, and p itself, the
semi-latus rectum of ToEquinoctial().P: the inputs are doubles, so the reference is exact for them and measures the
computation alone. mu is the Earth GM of gm_de440.tpc as the library reads it (CSPICE bodvcd_c, km^3/s^2, times 1e9 in
doubles; 398600435507022.6).

The points (the issue's tables):

    near e = 1   e in {0.999, 0.9999, 0.99999, 0.999999, 1.000002, 1.00001, 1.0001, 1.001}, outside the band
                 |e - 1| < 1e-6 of IsParabolic;
    controls     e in {0.1, 0.3, 0.5, 0.9, 0.99, 1.01, 1.1, 1.2, 1.5, 2, 3}, inside and outside the bands of the
                 rewrite (0.5 <= e <= 2 for p, 0.5 <= e <= 1.2 for the half-angle forms);
    ellipse nu   0, pi/2, 1, 2, 4, 5, fl(pi), and pi - d, pi + d for d in {1e-1, ..., 1e-6} (the apoapsis);
    hyperbola nu 0, +-f nu_inf for f in {0.1, 0.5, 0.9, 0.99, 0.999, 0.9999}, nu_inf = acos(-1/e) (the asymptote).

Each value is written in its shortest round-trip form ("R"), a double, and its rest to the 60-digit value, so that an
error of the order of an ulp is measured without the rounding of the reference.

Run: python3 -I conic_state_reference.py > ../../IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/ConicStateReference/conic_state_reference.json
(needs mpmath 1.4.1; a second or so).
"""
import json
import math

import mpmath as mp

mp.mp.dps = 60

MU = 398600435507022.6
Q = 7000000.0
I, NODE, ARGUMENT = 0.2, 0.3, 0.4
NEAR = [0.999, 0.9999, 0.99999, 0.999999, 1.000002, 1.00001, 1.0001, 1.001]
CONTROLS = [0.1, 0.3, 0.5, 0.9, 0.99, 1.01, 1.1, 1.2, 1.5, 2.0, 3.0]


def pair(x):
    hi = float(x)
    return [hi, float(x - mp.mpf(hi))]


def rotation():
    co, so, ci, si, cw, sw = (mp.cos(NODE), mp.sin(NODE), mp.cos(I), mp.sin(I), mp.cos(ARGUMENT), mp.sin(ARGUMENT))
    px = [co * cw - so * sw * ci, so * cw + co * sw * ci, sw * si]
    py = [-co * sw - so * cw * ci, -so * sw + co * cw * ci, cw * si]
    return px, py


def state(a, e, nu):
    a, e, nu = mp.mpf(a), mp.mpf(e), mp.mpf(nu)
    p = a * (1 - e * e)
    px, py = rotation()
    r = p / (1 + e * mp.cos(nu))
    x, y = r * mp.cos(nu), r * mp.sin(nu)
    w = mp.sqrt(MU / p)
    vx, vy = -w * mp.sin(nu), w * (e + mp.cos(nu))
    pos = [x * px[k] + y * py[k] for k in range(3)]
    vel = [vx * px[k] + vy * py[k] for k in range(3)]
    return p, pos, vel


def anomalies(e):
    if e < 1:
        out = [0.0, math.pi / 2, 1.0, 2.0, 4.0, 5.0, math.pi]
        for k in range(1, 7):
            d = 10.0 ** -k
            out += [math.pi - d, math.pi + d]
        return out
    nu_inf = math.acos(-1.0 / e)
    out = [0.0]
    for f in [0.1, 0.5, 0.9, 0.99, 0.999, 0.9999]:
        out += [f * nu_inf, -f * nu_inf]
    return out


def main():
    cases = []
    for e in NEAR + CONTROLS:
        a = Q / (1.0 - e)
        for nu in anomalies(e):
            p, pos, vel = state(a, e, nu)
            cases.append({'e': e, 'a': a, 'nu': nu, 'p': pair(p), 'r': [pair(c) for c in pos],
                          'v': [pair(c) for c in vel], 'near': e in NEAR})
    print(json.dumps({'mu': MU, 'i': I, 'node': NODE, 'argument': ARGUMENT, 'cases': cases}, indent=1))


if __name__ == '__main__':
    main()
