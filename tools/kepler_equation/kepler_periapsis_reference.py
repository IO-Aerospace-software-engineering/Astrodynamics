"""References of Kepler's equation near the periapsis at e -> 1, at 60 digits, for the tests of #484.

Near the periapsis at e -> 1, x - e sin x and e sinh x - x cancel (they differ by about (1 - e) x + x^3 / 6), so in
doubles both the solver of E from M (KeplerEquation) and the reverse direction, M = E - e sin E
(StateVector.MeanAnomaly, OrbitalParameters.TrueAnomalyToMeanAnomaly), carry the rounding of e sin x, about eps x,
divided by 1 - e. The references measure both directions on the doubles the tests give the library:

    solver      for the doubles e and M, the root E* of E - e sin E = M (e < 1) or e sinh H - H = M (e > 1), by Newton's
                method in mpmath at 60 digits, kept only when its residual is below 1e-55 (times max(1, |M|)); written
                as a double and its rest, so that an error of an ulp is measured without the rounding of the reference.
                e in {0.5, 0.9, 0.999, 0.99999, 0.999999} and {1.00001, 1.001, 1.5, 2} (the issue's and controls inside
                the band 0.5 <= e <= 2 of the rewrite), and e in {0.3, 3} outside it; m log-spaced from 1e-15 to pi on
                the ellipse and to 10 on the hyperbola (201 values), M = +-m;
    reverse     for a double E, M* = E - e sin E (e sinh E - E on the hyperbola) at 60 digits, E log-spaced from 1e-7 to
                3 rad (201 values), at the same eccentricities;
    states      the issue's states near the periapsis: q = 7000 km, a = fl(q / (1 - e)), i = 0.2, node 0.3, argument 0.4
                rad, e in {0.999, 0.99999, 0.999999, 1.00001, 1.001}, M the double nearest the mean anomaly of nu in
                {0.5, 1, 2, 5, 10, 20, 30, 45, 60, 90} deg (and -nu on the hyperbola, whose M the constructor keeps), and
                the state of the root E* of that double M at 60 digits, the perifocal state p / (1 + e cos nu*)
                (cos nu*, sin nu*, 0), sqrt(mu / p) (-sin nu*, e + cos nu*, 0), p = a (1 - e^2), turned by
                R3(node) R1(i) R3(argument) (Vallado, Fundamentals of Astrodynamics and Applications, 4th ed., 2013,
                algorithm 10 COE2RV; confirmed by S. Guillet, 2026-11-15); and that state rounded to doubles, the input
                of the round trip state -> ToKeplerianElements() -> ToStateVector(), which the test compares with it.

mu is the Earth GM of gm_de440.tpc as the library reads it (398600435507022.6 m^3/s^2).

Run: python3 -I kepler_periapsis_reference.py > ../../IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/KeplerEquationReference/kepler_periapsis_reference.json
(needs mpmath 1.4.1; a few seconds).
"""
import json
import math

import mpmath as mp

mp.mp.dps = 60

MU = 398600435507022.6
Q = 7000000.0
I, NODE, ARGUMENT = 0.2, 0.3, 0.4
ELLIPSES = [0.3, 0.5, 0.9, 0.999, 0.99999, 0.999999]
HYPERBOLAS = [1.00001, 1.001, 1.5, 2.0, 3.0]
STATES = [0.999, 0.99999, 0.999999, 1.00001, 1.001]
NUS = [0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 30.0, 45.0, 60.0, 90.0]


def logspace(lo, hi, n):
    return [math.exp(math.log(lo) + (math.log(hi) - math.log(lo)) * k / (n - 1)) for k in range(n)]


def pair(x):
    hi = float(x)
    return [hi, float(x - mp.mpf(hi))]


def root(M, e):
    """E* for the doubles M (|M| <= pi on the ellipse) and e."""
    M, e = mp.mpf(M), mp.mpf(e)
    tol = mp.mpf(10) ** -57
    m = abs(M)
    if e < 1:
        x = min(m + e, mp.pi)
        for _ in range(400):
            d = (x - e * mp.sin(x) - m) / (1 - e * mp.cos(x))
            x -= d
            if abs(d) <= tol * max(1, x):
                break
        assert abs(x - e * mp.sin(x) - m) <= mp.mpf(10) ** -55, (M, e)
    else:
        x = mp.asinh((m + mp.cbrt(6 * m / e)) / e)
        for _ in range(400):
            d = (e * mp.sinh(x) - x - m) / (e * mp.cosh(x) - 1)
            x -= d
            if abs(d) <= tol * max(1, x):
                break
        assert abs(e * mp.sinh(x) - x - m) <= mp.mpf(10) ** -55 * max(1, m), (M, e)
    return x if M >= 0 else -x


def mean_anomaly(E, e):
    E, e = mp.mpf(E), mp.mpf(e)
    return E - e * mp.sin(E) if e < 1 else e * mp.sinh(E) - E


def true_anomaly(E, e):
    e = mp.mpf(e)
    if e < 1:
        return 2 * mp.atan2(mp.sqrt(1 + e) * mp.sin(E / 2), mp.sqrt(1 - e) * mp.cos(E / 2))
    return 2 * mp.atan(mp.sqrt((e + 1) / (e - 1)) * mp.tanh(E / 2))


def anomaly_of_true(nu, e):
    e, nu = mp.mpf(e), mp.mpf(nu)
    if e < 1:
        return 2 * mp.atan(mp.sqrt((1 - e) / (1 + e)) * mp.tan(nu / 2))
    return 2 * mp.atanh(mp.sqrt((e - 1) / (e + 1)) * mp.tan(nu / 2))


def rotation():
    co, so, ci, si, cw, sw = (mp.cos(NODE), mp.sin(NODE), mp.cos(I), mp.sin(I), mp.cos(ARGUMENT), mp.sin(ARGUMENT))
    px = [co * cw - so * sw * ci, so * cw + co * sw * ci, sw * si]
    py = [-co * sw - so * cw * ci, -so * sw + co * cw * ci, cw * si]
    return px, py


def state(a, e, nu):
    a, e = mp.mpf(a), mp.mpf(e)
    p = a * (1 - e * e)
    px, py = rotation()
    r = p / (1 + e * mp.cos(nu))
    x, y = r * mp.cos(nu), r * mp.sin(nu)
    w = mp.sqrt(MU / p)
    vx, vy = -w * mp.sin(nu), w * (e + mp.cos(nu))
    return [x * px[k] + y * py[k] for k in range(3)], [vx * px[k] + vy * py[k] for k in range(3)]


def main():
    solver, reverse, states = [], [], []
    for e in ELLIPSES + HYPERBOLAS:
        top = math.pi if e < 1 else 10.0
        for m in logspace(1e-15, top, 201):
            for M in (m, -m):
                solver.append({'e': e, 'M': M, 'E': pair(root(M, e))})
        for E in logspace(1e-7, 3.0, 201):
            reverse.append({'e': e, 'E': E, 'M': pair(mean_anomaly(E, e))})
    for e in STATES:
        a = Q / (1.0 - e)
        signs = (1.0,) if e < 1 else (1.0, -1.0)
        for nu_deg in NUS:
            for sign in signs:
                nu = sign * mp.mpf(nu_deg) * mp.pi / 180
                M = float(mean_anomaly(anomaly_of_true(nu, e), e))
                nu_star = true_anomaly(root(M, e), e)
                r, v = state(a, e, nu_star)
                states.append({'e': e, 'a': a, 'nuDegrees': sign * nu_deg, 'M': M,
                               'r': [pair(c) for c in r], 'v': [pair(c) for c in v],
                               'rDouble': [float(c) for c in r], 'vDouble': [float(c) for c in v],
                               'radius': float(mp.sqrt(sum(c * c for c in r)))})
    print(json.dumps({'mu': MU, 'q': Q, 'i': I, 'node': NODE, 'argument': ARGUMENT, 'solver': solver,
                      'reverse': reverse, 'states': states}, indent=0))


if __name__ == '__main__':
    main()
