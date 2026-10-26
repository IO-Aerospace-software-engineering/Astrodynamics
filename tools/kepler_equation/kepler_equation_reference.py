"""Reference values of Kepler's equation from the mean anomaly, at 60 digits, for the tests of #453.

For a mean anomaly M and an eccentricity e, both doubles, the reference is the root of Kepler's equation for these
doubles: E - e sin E = M on an ellipse (0 <= e < 1), e sinh H - H = M on a hyperbola (e > 1), the root E* for any M,
within e of it (the equation has a single real root, its derivative being at least |1 - e| > 0). It is found by Newton's
method in mpmath at 60 digits on the reduced equation (r = M - 2 pi k, |r| <= pi, on the ellipse), from an upper bound
of the root, and kept only when its residual is below 1e-55 (times max(1, |M|) on the hyperbola): with a derivative of
at least 1e-6 on the grids, the root is then exact to 1e-49, far below the 1e-16 relative that the tests measure. The
true anomaly nu* of the root, in [0, 2 pi), and three floors of the double computation are written with it:

    floor          eps (|E| + e |sin E| + |M|) / |1 - e cos E| (the issue's; e |sinh H| + |H| + |M| over e cosh H - 1
                   on the hyperbola), the rounding of one evaluation of the step with the full E and M;
    reduced_floor  F = eps (|E_r| + e |sin E_r| + |r|) / |1 - e cos E_r| + eps |E - E_r| + eps |E| / 2, the floor of
                   the reduced equation plus the representation of the full E (the issue's floor plus eps |H| / 2 on
                   the hyperbola), which an error of the reduction near 2 pi k does not hide;
    nu_floor       |d nu / dE| F + ulp(nu) / 2, d nu / dE = sqrt(1 - e^2) / (1 - e cos E_r) (on the hyperbola
                   sqrt(e^2 - 1) / (e cosh H - 1)),

with eps = 2^-52. The families of points:

    ellipse        e in {0.1, 0.5, 0.9, 0.99, 0.999, 0.9999, 0.99999, 0.999999} (the issue's; 0.999999 is the double
                   1 - 1e-6, the largest elliptic e of OrbitalParameters.IsElliptical) and the edges {0, 1e-12, 1e-3};
                   201 values of m log-spaced from 1e-9 to pi; M = m and fl(2 pi) - m; and below fl(2 pi), where the
                   reduction by one turn is singular, fl(2 pi) itself (which SpecialFunctions.NormalizeAngle returns
                   for a negative M of 1e-17), the 8 doubles below it and fl(2 pi) - d, d log-spaced from 1e-15 to
                   1e-9;
    negative       the same e and m, M = -m and, for the issue's e, -(fl(2 pi) - m) and the opposites of the doubles
                   below fl(2 pi) (the M of a StateVector);
    turns          e in {0.1, 0.9, 0.99, 0.999, 0.99999, 0.999999}; M = +-(k fl(2 pi) +- d), k in {2, 10, 11, 1592},
                   21 values of d log-spaced from 1e-15 to pi (beyond one turn: internal calls, and KeplerianElements
                   at e = 1 - 1e-6 exactly, whose M its constructor keeps);
    hyperbola      e in {1.00001, 1.001, 1.01, 1.5, 3, 10} (the issue's) and the edges {1 + 2e-6, 100}; 201 values of m
                   log-spaced from 1e-9 to 1e4; M = +-m.

Then the states of the issue's cases (a periapsis at 7000 km, i = 40 deg, node 30 deg, periapsis argument 50 deg; the
e = 0.9 case a = 70 000 km, i = 63.4 deg, node 10 deg, argument 270 deg; a control at e = 0.1, a = 7500 km, i = 50 deg,
node 40 deg, argument 60 deg) and of its NaN regression (e = 0.99999, M = 0.0012924565239450137, a = 7000 km / (1 - e),
i = 0.1, node 0.2, argument 0.3 rad), with M the double nearest the mean anomaly of the given true anomaly: the
state of these doubles at 60 digits, the perifocal state p / (1 + e cos nu) (cos nu, sin nu, 0), sqrt(GM / p)
(-sin nu, e + cos nu, 0), p = a (1 - e^2), turned by R3(node) R1(i) R3(argument), with the GM of the test kernels; and
its floor, the root sum of squares of the relative changes of the position and of the velocity for a relative change
of 2^-53 of each element and of GM, as the conformance suite's reference.elements_state_floors computes it. A state is
flagged spice_conics where SPICE CONICS resolves it: within 0.06 of that floor (measured 2026-10-25, Linux); elsewhere
it is 3 to 1500 floors away (it is not at e = 0.1 and 0.9, and at e = 0.999, nu = 330 deg it is 0.32 m off). The state
floor counts a relative change of the full M, so near M = 2 pi (nu = 330 deg) it hides an error of the reduction by
2 pi, as the issue's floor does: the families of points guard the reduction, by their reduced floor, not the states.

Each input is a double, written so that it reads back to itself; the reference values are strings of 30 significant
digits, so the reference is exact for the inputs and measures the computation alone.

Run: python3 -I kepler_equation_reference.py > kepler_equation_reference.json (needs mpmath; about ten seconds).
"""
import json
import math

import mpmath as mp

mp.mp.dps = 60

EPS = 2.0 ** -52
TWO_PI = 2.0 * math.pi  # fl(2 pi), Constants._2PI
GM = 398600435507022.6  # the Earth of the test kernels, m^3/s^2

ELLIPSES = [0.1, 0.5, 0.9, 0.99, 0.999, 0.9999, 0.99999, 0.999999]
ELLIPSE_EDGES = [0.0, 1e-12, 1e-3]  # 1 - 1e-6 is 0.999999
HYPERBOLAS = [1.00001, 1.001, 1.01, 1.5, 3.0, 10.0]
HYPERBOLA_EDGES = [1.0 + 2e-6, 100.0]
TURNS = [0.1, 0.9, 0.99, 0.999, 0.99999, 0.999999]


def logspace(lo, hi, n):
    return [math.exp(math.log(lo) + (math.log(hi) - math.log(lo)) * k / (n - 1)) for k in range(n)]


def root(M, e):
    """(E*, reduced E*, k) for the doubles M and e: E* = E_r + 2 pi k, |E_r - r| <= e; on the hyperbola E_r = E*,
    k = 0."""
    M, e = mp.mpf(M), mp.mpf(e)
    tol = mp.mpf(10) ** -57
    if e < 1:
        k = mp.mpf(0) if abs(M) <= mp.pi else mp.nint(M / (2 * mp.pi))
        r = M - 2 * mp.pi * k
        m = abs(r)
        x = min(m + e, mp.pi)  # an upper bound of the root, where the function is convex
        for _ in range(400):
            d = (x - e * mp.sin(x) - m) / (1 - e * mp.cos(x))
            x -= d
            if abs(d) <= tol * max(1, x):
                break
        assert abs(x - e * mp.sin(x) - m) <= mp.mpf(10) ** -55, (M, e)
        reduced = x if r >= 0 else -x
        return reduced + 2 * mp.pi * k, reduced, k
    m = abs(M)
    x = mp.asinh((m + mp.cbrt(6 * m / e)) / e)  # an upper bound of the root, where the function is convex
    for _ in range(400):
        d = (e * mp.sinh(x) - x - m) / (e * mp.cosh(x) - 1)
        x -= d
        if abs(d) <= tol * max(1, x):
            break
    assert abs(e * mp.sinh(x) - x - m) <= mp.mpf(10) ** -55 * max(1, m), (M, e)
    H = x if M >= 0 else -x
    return H, H, mp.mpf(0)


def true_anomaly(E, e):
    """The true anomaly of the anomaly E, in [0, 2 pi)."""
    e = mp.mpf(e)
    if e < 1:
        nu = 2 * mp.atan2(mp.sqrt(1 + e) * mp.sin(E / 2), mp.sqrt(1 - e) * mp.cos(E / 2))
    else:
        nu = 2 * mp.atan(mp.sqrt((e + 1) / (e - 1)) * mp.tanh(E / 2))
    return nu % (2 * mp.pi)


def floors(M, e, E, reduced, k):
    """The issue's floor, the reduced floor F and the floor of nu (module docstring)."""
    Mx, ex = mp.mpf(M), mp.mpf(e)
    if e < 1:
        r = Mx - 2 * mp.pi * k
        issue = EPS * (abs(E) + ex * abs(mp.sin(E)) + abs(Mx)) / abs(1 - ex * mp.cos(E))
        base = EPS * (abs(reduced) + ex * abs(mp.sin(reduced)) + abs(r)) / abs(1 - ex * mp.cos(reduced))
        slope = mp.sqrt(1 - ex * ex) / (1 - ex * mp.cos(reduced))
    else:
        issue = EPS * (ex * abs(mp.sinh(E)) + abs(E) + abs(Mx)) / abs(ex * mp.cosh(E) - 1)
        base = issue
        slope = mp.sqrt(ex * ex - 1) / (ex * mp.cosh(E) - 1)
    reduced_floor = base + EPS * abs(E - reduced) + EPS * abs(E) / 2
    nu_floor = slope * reduced_floor + mp.mpf(math.ulp(float(true_anomaly(reduced, e)))) / 2
    return float(issue), float(reduced_floor), float(nu_floor)


def digits(x):
    """A reference value: 30 significant digits."""
    return mp.nstr(x, 30, min_fixed=-3, max_fixed=6)


def scale(x):
    """A floor, a scale of the errors: 6 significant digits."""
    return float(f'{x:.6g}')


def point(family, M, e):
    E, reduced, k = root(M, e)
    issue, reduced_floor, nu_floor = floors(M, e, E, reduced, k)
    return {'family': family, 'e': e, 'M': M, 'E': digits(E), 'nu': digits(true_anomaly(reduced, e)),
            'floor': scale(issue), 'reduced_floor': scale(reduced_floor), 'nu_floor': scale(nu_floor)}


def below_two_pi(grid):
    """fl(2 pi), the 8 doubles below it and fl(2 pi) - d, d log-spaced from 1e-15 to 1e-9, that are not on the grid."""
    out, x = {TWO_PI}, TWO_PI
    for _ in range(8):
        x = math.nextafter(x, 0.0)
        out.add(x)
    out.update(TWO_PI - d for d in logspace(1e-15, 1e-9, 13))
    return sorted(out - set(grid), reverse=True)


def points():
    out = []
    for e in ELLIPSES + ELLIPSE_EDGES:
        grid = logspace(1e-9, math.pi, 201)
        for m in grid:
            out.append(point('ellipse', m, e))
            out.append(point('ellipse', TWO_PI - m, e))
            out.append(point('negative', -m, e))
            if e in ELLIPSES:
                out.append(point('negative', -(TWO_PI - m), e))
        for M in below_two_pi([TWO_PI - m for m in grid]):
            out.append(point('ellipse', M, e))
            if e in ELLIPSES:
                out.append(point('negative', -M, e))
    for e in TURNS:
        for k in [2, 10, 11, 1592]:
            for d in logspace(1e-15, math.pi, 21):
                for s in (1.0, -1.0):
                    M = k * TWO_PI + s * d
                    out.append(point('turns', M, e))
                    out.append(point('turns', -M, e))
    for e in HYPERBOLAS + HYPERBOLA_EDGES:
        for m in logspace(1e-9, 1e4, 201):
            out.append(point('hyperbola', m, e))
            out.append(point('hyperbola', -m, e))
    return out


def radians(degrees):
    return float(mp.mpf(degrees) * mp.pi / 180)


def mean_anomaly(e, nu):
    """The mean anomaly of the true anomaly nu, rounded to a double, in [0, 2 pi) on an ellipse."""
    e, nu = mp.mpf(e), mp.mpf(nu)
    if e < 1:
        E = 2 * mp.atan(mp.sqrt((1 - e) / (1 + e)) * mp.tan(nu / 2))
        M = E - e * mp.sin(E)
        return float(M + 2 * mp.pi if M < 0 else M)
    H = 2 * mp.atanh(mp.sqrt((e - 1) / (e + 1)) * mp.tan(nu / 2))
    return float(e * mp.sinh(H) - H)


def rotate(i, node, w, x):
    """R3(node) R1(i) R3(w) x."""
    cw, sw, ci, si, cn, sn = mp.cos(w), mp.sin(w), mp.cos(i), mp.sin(i), mp.cos(node), mp.sin(node)
    a = [cw * x[0] - sw * x[1], sw * x[0] + cw * x[1], x[2]]
    b = [a[0], ci * a[1] - si * a[2], si * a[1] + ci * a[2]]
    return [cn * b[0] - sn * b[1], sn * b[0] + cn * b[1], b[2]]


def state(a, e, i, node, w, M, mu):
    """The state of the elements (mpf) at 60 digits, the true anomaly from the root of Kepler's equation for M."""
    _, reduced, _ = root(M, e)
    nu = true_anomaly(reduced, e)
    p = a * (1 - e * e)
    radius = p / (1 + e * mp.cos(nu))
    speed = mp.sqrt(mu / p)
    return (rotate(i, node, w, [radius * mp.cos(nu), radius * mp.sin(nu), 0]),
            rotate(i, node, w, [-speed * mp.sin(nu), speed * (e + mp.cos(nu)), 0]))


def norm(x):
    return mp.sqrt(sum(c * c for c in x))


def relative(x, y):
    return norm([u - v for u, v in zip(x, y)]) / norm(y)


def state_floors(elements, mu):
    """The relative floors of the position and of the velocity: the root sum of squares of their relative changes for a
    relative change of 2^-53 of each element and of GM (reference.elements_state_floors of the conformance suite)."""
    x = [mp.mpf(v) for v in elements]
    r0, v0 = state(*x, mp.mpf(mu))
    position = velocity = mp.mpf(0)
    for j in range(len(x) + 1):
        xx, m = list(x), mp.mpf(mu)
        if j < len(x):
            if xx[j] == 0:
                continue
            xx[j] *= 1 + mp.mpf(2) ** -53
        else:
            m *= 1 + mp.mpf(2) ** -53
        r1, v1 = state(*xx, m)
        position += relative(r1, r0) ** 2
        velocity += relative(v1, v0) ** 2
    return r0, v0, mp.sqrt(position), mp.sqrt(velocity)


# The issue's cases: (name, a, e, i, node, argument, nu in degrees, resolved by SPICE CONICS); at nu = 330 deg, M is
# near 2 pi, but the state floor does not guard the reduction by 2 pi there (module docstring)
CASES = [
    ('e = 0.1, nu = 110 deg (control)', 7.5e6, 0.1, 50.0, 40.0, 60.0, 110.0, False),
    ('e = 0.9, nu = 170 deg', 7.0e7, 0.9, 63.4, 10.0, 270.0, 170.0, False),
    ('e = 0.99, nu = 90 deg', 7.0e8, 0.99, 40.0, 30.0, 50.0, 90.0, True),
    ('e = 0.999, nu = 30 deg', 7.0e9, 0.999, 40.0, 30.0, 50.0, 30.0, True),
    ('e = 0.999, nu = 150 deg', 7.0e9, 0.999, 40.0, 30.0, 50.0, 150.0, True),
    ('e = 0.999, nu = 330 deg', 7.0e9, 0.999, 40.0, 30.0, 50.0, 330.0, False),
    ('e = 0.99999, nu = 5 deg', 7.0e11, 0.99999, 40.0, 30.0, 50.0, 5.0, True),
    ('e = 1.01, nu = 60 deg', -7.0e8, 1.01, 40.0, 30.0, 50.0, 60.0, True),
    ('e = 1.01, nu = -60 deg', -7.0e8, 1.01, 40.0, 30.0, 50.0, -60.0, True),
]


def state_entry(name, a, e, i, node, w, M, nu, conics):
    E, reduced, k = root(M, e)
    issue, _, _ = floors(M, e, E, reduced, k)
    r, v, position_floor, velocity_floor = state_floors([a, e, i, node, w, M], GM)
    return {'name': name, 'a': a, 'e': e, 'i': i, 'raan': node, 'aop': w, 'M': M, 'nu': nu, 'E': digits(E),
            'floor': scale(issue), 'position': [digits(c) for c in r], 'velocity': [digits(c) for c in v],
            'position_floor': scale(position_floor), 'velocity_floor': scale(velocity_floor), 'spice_conics': conics}


def states():
    out = []
    for name, a, e, i, node, w, nu_degrees, conics in CASES:
        nu = radians(nu_degrees)
        out.append(state_entry(name, a, e, radians(i), radians(node), radians(w), mean_anomaly(e, nu), nu, conics))
    # The NaN regression: the loop before #453 left the convex region from E = M and returned NaN
    e, M = 0.99999, 0.0012924565239450137
    nu = float(true_anomaly(root(M, e)[1], e))
    out.append(state_entry('NaN regression, e = 0.99999, M = 0.0012924565239450137', 7.0e6 / (1.0 - e), e, 0.1, 0.2,
                           0.3, M, nu, False))
    return out


def main():
    header = {'generated_by': 'tools/kepler_equation/kepler_equation_reference.py, mpmath ' + mp.__version__ +
              ' at 60 digits', 'units': 'rad, m, m/s, m^3/s^2', 'gm': GM}
    lines = ['{']
    lines += [f' {json.dumps(key)}: {json.dumps(value)},' for key, value in header.items()]
    lines.append(' "states": [')
    lines.append(',\n'.join('  ' + json.dumps(s) for s in states()))
    lines.append(' ],')
    lines.append(' "points": [')
    lines.append(',\n'.join('  ' + json.dumps(p) for p in points()))
    lines.append(' ]')
    lines.append('}')
    text = '\n'.join(lines)
    json.loads(text)
    print(text)


if __name__ == '__main__':
    main()
