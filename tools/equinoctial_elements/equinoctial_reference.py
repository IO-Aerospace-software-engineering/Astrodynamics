"""Reference states of equinoctial elements, at 60 digits, for the tests of #452.

The elements are the modified equinoctial elements of the library (retrograde factor j = 1): the semi-latus rectum p,
f = e cos(Ω + ω), g = e sin(Ω + ω), h = tan(i/2) cos Ω, k = tan(i/2) sin Ω and the true longitude L = Ω + ω + ν. Their
state is

    s² = 1 + h² + k²,
    f̂ = (1 − k² + h², 2hk, −2k) / s²,  ĝ = (2hk, 1 + k² − h², 2h) / s²,
    r = p / (1 + f cos L + g sin L),
    r = r (cos L f̂ + sin L ĝ),  v = √(μ/p) (−(g + sin L) f̂ + (f + cos L) ĝ).

f̂ and ĝ are the first two columns of Q in the GMAT Mathematical Specification R2026a, section 3.1.4, eqs 3.44 to 3.47,
with j = 1 (GMAT's p is k here and its q is h); the radius and the velocity with the true longitude are the form of
Walker, Ireland and Owens, "A set of modified equinoctial orbit elements", Celestial Mechanics 36, 1985 (to be verified
by S. Guillet). Each state is checked here against the classical route Rz(Ω) Rx(i) Rz(ω) applied to the perifocal state,
with any consistent choice of the angles where they are undefined; the largest difference is written to the file.

The states at another epoch are the exact two-body states: the mean anomaly of the elements, advanced by n Δt, Kepler's
equation solved at 60 digits (elliptic and hyperbolic, by Newton's method to 1e-55; Barker's equation in closed form),
and the state at the new true longitude, which is written with each state. The Keplerian inputs (a, e, i, Ω, ω, M) are
taken the same way, through their perifocal state, with p = a (1 − e²), or 2q for a parabola. Equinoctial elements
whose P is not finite (a parabola) take p = q (1 + e), with q their perigee radius and e = √(f² + g²) of their doubles:
the conic of their eccentricity with its periapsis at q, as SPICE CONICS (rp (1 + e)).

Each case also carries its Keplerian angles under these conventions (Ω = 0 for h = k = 0, ϖ = Ω for f = g = 0, ω = ϖ − Ω,
ν = L − ϖ), its mean longitude ϖ + M, with ϖ = atan2(g, f), or Ω for a circular orbit, every angle modulo 2π, and the
state and the time from the epoch at the true anomaly 0.5 rad, the next passage on an ellipse (ToStateVector(ν)).

The cases are those of the design of #452: the circular and equatorial elements of the issue, the signed zeros of f, g,
h and k (the 16 sign patterns of f = g = h = k = 0), inputs within 1e-12 of the singular values, products of the
argument of periapsis that underflow, the true longitude far outside [0, 2π), the retrograde equatorial orbit (h =
±tan(π/2) in doubles) and h or k beyond 1e150, where their squares overflow, the conformance cases 151 to 154, Keplerian
elements whose ToEquinoctial gives every sign pattern of the zeros, parabolic and near-parabolic elements (|e − 1| <
1e-6), and the mean elements of TLE.Create with the fields of line 2 (degrees, 4 decimals) under the conventions of the
library: Ω = 0 for an equatorial orbit, ω = 0 for a circular one.

Every input is a double, written with repr so that −0.0 survives; the references are exact for these doubles, so they
measure the computation alone. μ is the Earth GM of the kernels the tests load, as the library reads it (CSPICE
bodvcd_c, km³/s², times 1e9 in doubles): gm_de440.tpc for IO.Astrodynamics.Tests, gm_de431.tpc for the CLI tests; the
tests check that the GM of their Earth is this double.

Run: python3 -I equinoctial_reference.py > equinoctial_reference.json (needs mpmath and spiceypy), from this directory,
and copy the output to IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/EquinoctialElementsReference/.
"""
import json
import math
import os

import mpmath as mp
import spiceypy as spice

mp.mp.dps = 60

D = math.pi / 180.0  # Constants.Deg2Rad
HERE = os.path.dirname(os.path.abspath(__file__))
NET = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net')
KERNELS = {
    'gm_de440': os.path.join(NET, 'IO.Astrodynamics.Tests', 'Data', 'SolarSystem', 'gm_de440.tpc'),
    'gm_de431': os.path.join(NET, 'IO.Astrodynamics.CLI.Tests', 'Data', 'SolarSystem', 'gm_de431.tpc'),
}
SOLVER_TOLERANCE = mp.mpf(10) ** -55


def earth_gm(path):
    """BODY399_GM of a text PCK as the library reads it: through CSPICE (bodvcd_c), km³/s², times 1e9 in doubles. The
    parser of CSPICE does not round every decimal to the nearest double: it reads 3.9860043550702266D+05 as
    398600.4355070226, one unit in the last place below, so the text of the kernel would not give the GM of the
    library."""
    spice.kclear()
    spice.furnsh(path)
    try:
        return float(spice.bodvcd(399, 'GM', 1)[1][0]) * 1e9
    finally:
        spice.kclear()


def text(x):
    """A double as repr writes it, with the infinities and NaN as .NET reads them."""
    x = float(x)
    if math.isnan(x):
        return 'NaN'
    if math.isinf(x):
        return 'Infinity' if x > 0 else '-Infinity'
    return repr(x)


def mpf(s):
    return mp.mpf(float(s.replace('Infinity', 'inf')))


def frame_vectors(h, k):
    s2 = 1 + h * h + k * k
    f_hat = [(1 - k * k + h * h) / s2, 2 * h * k / s2, -2 * k / s2]
    g_hat = [2 * h * k / s2, (1 + k * k - h * h) / s2, 2 * h / s2]
    return f_hat, g_hat


def equinoctial_state(p, f, g, h, k, l, mu):
    fv, gv = frame_vectors(h, k)
    c, s = mp.cos(l), mp.sin(l)
    r = p / (1 + f * c + g * s)
    w = mp.sqrt(mu / p)
    return [r * (c * fv[j] + s * gv[j]) for j in range(3)], [w * (-(g + s) * fv[j] + (f + c) * gv[j]) for j in range(3)]


def angles(f, g, h, k, l):
    """e, i, Ω, ω, ν of the elements: exact where they are defined, a consistent choice where they are not."""
    e = mp.sqrt(f * f + g * g)
    t = mp.sqrt(h * h + k * k)
    node = mp.atan2(k, h) if t > 0 else mp.mpf(0)
    periapsis_longitude = mp.atan2(g, f) if e > 0 else node
    return e, 2 * mp.atan(t), node, periapsis_longitude - node, l - periapsis_longitude


def perifocal_state(p, e, i, node, periapsis, nu, mu):
    co, so, ci, si, cw, sw = mp.cos(node), mp.sin(node), mp.cos(i), mp.sin(i), mp.cos(periapsis), mp.sin(periapsis)
    px = [co * cw - so * sw * ci, so * cw + co * sw * ci, sw * si]
    py = [-co * sw - so * cw * ci, -so * sw + co * cw * ci, cw * si]
    r = p / (1 + e * mp.cos(nu))
    x, y = r * mp.cos(nu), r * mp.sin(nu)
    w = mp.sqrt(mu / p)
    vx, vy = -w * mp.sin(nu), w * (e + mp.cos(nu))
    return [x * px[j] + y * py[j] for j in range(3)], [vx * px[j] + vy * py[j] for j in range(3)]


def solve_elliptic(m, e):
    reduced = m - 2 * mp.pi * mp.floor((m + mp.pi) / (2 * mp.pi))  # [−π, π)
    big_e = reduced if e < mp.mpf('0.8') else mp.pi * mp.sign(reduced)
    for _ in range(200):
        step = (big_e - e * mp.sin(big_e) - reduced) / (1 - e * mp.cos(big_e))
        big_e -= step
        if abs(step) < SOLVER_TOLERANCE:
            break
    assert abs(big_e - e * mp.sin(big_e) - reduced) < SOLVER_TOLERANCE * 10
    return big_e + (m - reduced)


def solve_hyperbolic(m, e):
    big_h = mp.asinh(m / e)
    for _ in range(400):
        step = (e * mp.sinh(big_h) - big_h - m) / (e * mp.cosh(big_h) - 1)
        big_h -= step
        if abs(step) < SOLVER_TOLERANCE:
            break
    assert abs(e * mp.sinh(big_h) - big_h - m) < SOLVER_TOLERANCE * 10 * max(1, abs(m))
    return big_h


def true_to_mean(nu, e):
    if e < 1:
        big_e = 2 * mp.atan2(mp.sqrt(1 - e) * mp.sin(nu / 2), mp.sqrt(1 + e) * mp.cos(nu / 2))
        return big_e - e * mp.sin(big_e)
    if e > 1:
        big_h = 2 * mp.atanh(mp.sqrt((e - 1) / (e + 1)) * mp.tan(nu / 2))
        return e * mp.sinh(big_h) - big_h
    d = mp.tan(nu / 2)
    return d + d ** 3 / 3


def mean_to_true(m, e):
    if e < 1:
        big_e = solve_elliptic(m, e)
        return 2 * mp.atan2(mp.sqrt(1 + e) * mp.sin(big_e / 2), mp.sqrt(1 - e) * mp.cos(big_e / 2))
    if e > 1:
        return 2 * mp.atan(mp.sqrt((e + 1) / (e - 1)) * mp.tanh(solve_hyperbolic(m, e) / 2))
    return 2 * mp.atan(2 * mp.sinh(mp.asinh(mp.mpf(3) / 2 * m) / 3))  # D + D³/3 = M, Barker in closed form


def mean_motion(p, e, mu):
    if e == 1:
        return mp.sqrt(mu / (2 * (p / 2) ** 3))
    return mp.sqrt(mu / abs(p / (1 - e * e)) ** 3)


def true_longitude_at(p, f, g, h, k, l, mu, dt):
    """The true longitude of the two-body orbit of the elements dt seconds after their epoch."""
    if dt == 0:
        return l
    e, _, node, periapsis, nu = angles(f, g, h, k, l)
    nu = mp.atan2(mp.sin(nu), mp.cos(nu))
    m = true_to_mean(nu, e) + mean_motion(p, e, mu) * dt
    return node + periapsis + mean_to_true(m, e)


def equinoctial_state_at(p, f, g, h, k, l, mu, dt):
    """The exact two-body state of the elements dt seconds after their epoch."""
    return equinoctial_state(p, f, g, h, k, true_longitude_at(p, f, g, h, k, l, mu, dt), mu)


def mean_longitude(f, g, h, k, l):
    """ϖ + M, with ϖ the longitude of periapsis (Ω for a circular orbit) and M the mean anomaly of ν = L − ϖ."""
    e, _, node, periapsis, nu = angles(f, g, h, k, l)
    return node + periapsis + true_to_mean(mp.atan2(mp.sin(nu), mp.cos(nu)), e)


def time_to_true_anomaly(p, f, g, h, k, l, mu, nu_target):
    """The time from the epoch of the elements to the true anomaly nu_target: the next passage on an ellipse, the only
    one on a hyperbola."""
    e, _, _, _, nu = angles(f, g, h, k, l)
    m0 = true_to_mean(mp.atan2(mp.sin(nu), mp.cos(nu)), e)
    dm = true_to_mean(nu_target, e) - m0
    if e < 1:
        dm = dm % (2 * mp.pi)
    return dm / mean_motion(p, e, mu)


def keplerian_state_at(a, e, i, node, periapsis, m, mu, perigee_radius, dt):
    p = 2 * perigee_radius if e == 1 else a * (1 - e * e)
    return perifocal_state(p, e, i, node, periapsis, mean_to_true(m + mean_motion(p, e, mu) * dt, e), mu)


def turn(x):
    """x modulo 2π, in [0, 2π): the angles are written reduced, so that a test compares them without the rounding of a
    large true longitude."""
    return x - 2 * mp.pi * mp.floor(x / (2 * mp.pi))


def pair(x):
    """x as two doubles [hi, lo]: hi the double nearest x, lo the double nearest x − hi. hi + lo is x to about 1e-32 of
    it, so the error of a computation in doubles, (computed − hi) − lo, is measured without the rounding of the
    reference to a double (half a unit in the last place, of the order of the error measured)."""
    hi = float(x)
    return [text(hi), text(float(x - mp.mpf(hi)))]


def vector(x):
    return [pair(c) for c in x]


class Generator:
    def __init__(self):
        self.gm = {name: earth_gm(path) for name, path in KERNELS.items()}
        self.walker_vs_classical = mp.mpf(0)

    def check_against_classical(self, p, f, g, h, k, l, mu, position):
        # At 80 digits: the classical route subtracts the angles from L (up to 1e10 rad here) and takes i within 1e-308
        # rad of π, where 60 digits would leave 1e-50 of |r|
        with mp.workdps(80):
            e, i, node, periapsis, nu = angles(f, g, h, k, l)
            classical, _ = perifocal_state(p, e, i, node, periapsis, nu, mu)
            difference = mp.sqrt(sum((classical[j] - position[j]) ** 2 for j in range(3)))
        self.walker_vs_classical = max(self.walker_vs_classical, difference)
        assert difference < mp.mpf(10) ** -45, difference

    def equinoctial(self, name, group, p, f, g, h, k, l, dts=(0.0, 600.0, -3000.0), gm='gm_de440', perigee_radius=None):
        case = {'name': name, 'group': group, 'p': text(p), 'f': text(f), 'g': text(g), 'h': text(h), 'k': text(k),
                'l': text(l), 'gm': gm}
        if perigee_radius is not None:
            case['perigee_radius'] = text(perigee_radius)
        mu = mp.mpf(self.gm[gm])
        x = [mpf(case[n]) for n in ('p', 'f', 'g', 'h', 'k', 'l')]
        e = mp.sqrt(x[1] * x[1] + x[2] * x[2])
        if not mp.isfinite(x[0]):
            # No finite P (a parabola): the conic of the eccentricity of the elements with its periapsis at q, as SPICE
            # CONICS, rp (1 + e)
            x[0] = mpf(case['perigee_radius']) * (1 + e)
        states = []
        for dt in dts:
            dt = mp.mpf(dt)
            position, velocity = equinoctial_state_at(*x, mu, dt)
            if dt == 0:
                self.check_against_classical(*x, mu, position)
            states.append({'dt': text(dt), 'l': pair(turn(true_longitude_at(*x, mu, dt))), 'r': vector(position),
                           'v': vector(velocity)})
        case['states'] = states
        # The Keplerian angles of the conventions of #452: Ω = 0 when h = k = 0, ϖ = Ω when f = g = 0, ω = ϖ − Ω and
        # ν = L − ϖ, modulo 2π
        _, _, node, periapsis, nu = angles(*x[1:])
        case['angles'] = {'node': pair(turn(node)), 'periapsis': pair(turn(periapsis)), 'nu': pair(turn(nu))}
        if abs(e - 1) >= mp.mpf('1e-6'):
            nu = mp.mpf('0.5')
            position, velocity = equinoctial_state(*x[:5], node + periapsis + nu, mu)
            case['mean_longitude'] = pair(turn(mean_longitude(*x[1:])))
            case['at_true_anomaly'] = {'nu': text(0.5), 'dt': pair(time_to_true_anomaly(*x, mu, nu)),
                                       'r': vector(position), 'v': vector(velocity)}
        return case

    def keplerian(self, name, group, a, e, i, node, periapsis, m, perigee_radius=None, dts=(0.0, 600.0)):
        case = {'name': name, 'group': group, 'a': text(a), 'e': text(e), 'i': text(i), 'o': text(node),
                'w': text(periapsis), 'm': text(m), 'gm': 'gm_de440'}
        if perigee_radius is not None:
            case['perigee_radius'] = text(perigee_radius)
        mu = mp.mpf(self.gm['gm_de440'])
        x = [mpf(case[n]) for n in ('a', 'e', 'i', 'o', 'w', 'm')]
        rp = mpf(case['perigee_radius']) if perigee_radius is not None else None
        case['states'] = [{'dt': text(dt), 'r': vector(s[0]), 'v': vector(s[1])}
                          for dt in dts for s in [keplerian_state_at(*x, mu, rp, mp.mpf(dt))]]
        return case


def tle_field(x):
    """An angle of line 2, degrees with 4 decimals, normalized to [0, 360); away from a rounding boundary, so that the
    text does not depend on the rounding of the computation."""
    x = x % 360
    scaled = x * 10000
    assert abs(scaled - mp.floor(scaled) - mp.mpf('0.5')) > mp.mpf('0.01'), x
    return f'{float(x):.4f}'


def tle_create(name, p, f, g, h, k, l):
    """Mean elements for TLE.Create, and the fields of line 2 under the conventions of the library (#452)."""
    case = {'name': name, 'p': text(p), 'f': text(f), 'g': text(g), 'h': text(h), 'k': text(k), 'l': text(l)}
    fm, gm, hm, km, lm = (mpf(case[n]) for n in ('f', 'g', 'h', 'k', 'l'))
    e = mp.sqrt(fm * fm + gm * gm)
    node = mp.atan2(km, hm) if hm != 0 or km != 0 else mp.mpf(0)
    periapsis_longitude = mp.atan2(gm, fm) if e > 0 else node
    nu = lm - periapsis_longitude
    deg = 180 / mp.pi
    case['line2'] = {'inclination': tle_field(2 * mp.atan(mp.sqrt(hm * hm + km * km)) * deg),
                     'raan': tle_field(node * deg),
                     'argument_of_perigee': tle_field((periapsis_longitude - node) * deg),
                     'mean_anomaly': tle_field(true_to_mean(nu, e) * deg)}
    return case


def main():
    gen = Generator()
    t15 = math.tan(15.0 * D)
    t258 = math.tan(25.8 * D)
    h155, k155 = 0.20526098990007927, 0.17223442092023988  # tan(15°) cos 40°, tan(15°) sin 40°
    f156, g156 = -0.02499999999999999, 0.04330127018922194  # 0.05 cos 120°, 0.05 sin 120°
    tan_half_pi = math.tan(math.pi / 2.0)
    eq = gen.equinoctial
    cases = [
        # The circular and equatorial elements of the issue (p m, f, g, h, k, L rad)
        eq('circular, i 30, node 40 (suite 155)', 'issue', 7000.0e3, 0.0, 0.0, h155, k155, 115.0 * D),
        eq('circular, i 51.6, node 200', 'issue', 6778.137e3, 0.0, 0.0, t258 * math.cos(200.0 * D),
           t258 * math.sin(200.0 * D), 75.0 * D),
        eq('circular, i 30, node 0', 'issue', 7000.0e3, 0.0, 0.0, t15, 0.0, 115.0 * D),
        eq('equatorial, e 0.05, lop 120 (suite 156)', 'issue', 7980.0e3, f156, g156, 0.0, 0.0, 150.0 * D),
        eq('equatorial, e 0.05, lop 270', 'issue', 7980.0e3, 0.05 * math.cos(270.0 * D), 0.05 * math.sin(270.0 * D),
           0.0, 0.0, 30.0 * D),
        eq('equatorial, e 0.05, lop 0', 'issue', 7980.0e3, 0.05, 0.0, 0.0, 0.0, 150.0 * D),
        eq('equatorial, e 0.9, lop 250, L 300', 'issue', 13300.0e3, 0.9 * math.cos(250.0 * D),
           0.9 * math.sin(250.0 * D), 0.0, 0.0, 300.0 * D),
        eq('equatorial hyperbola, e 1.5, lop 345, L 45', 'issue', 17500.0e3, 1.5 * math.cos(345.0 * D),
           1.5 * math.sin(345.0 * D), 0.0, 0.0, 45.0 * D),
        eq('f = g = h = k = 0', 'issue', 7000.0e3, 0.0, 0.0, 0.0, 0.0, 115.0 * D),
        eq('f = g = 1e-12', 'issue', 7000.0e3, 1e-12, 1e-12, h155, k155, 115.0 * D),
        eq('h = 1e-12', 'issue', 7980.0e3, f156, g156, 1e-12, 0.0, 150.0 * D),
    ]
    # The signed zeros, given directly
    for fs in (0.0, -0.0):
        for gs in (0.0, -0.0):
            cases.append(eq(f'circular, f {fs!r}, g {gs!r}, node 200', 'signed', 6778.137e3, fs, gs,
                            t258 * math.cos(200.0 * D), t258 * math.sin(200.0 * D), 75.0 * D))
    for hs in (0.0, -0.0):
        for ks in (0.0, -0.0):
            cases.append(eq(f'equatorial, h {hs!r}, k {ks!r}, lop 270', 'signed', 7980.0e3, 0.05 * math.cos(270.0 * D),
                            0.05 * math.sin(270.0 * D), hs, ks, 30.0 * D))
    # The 16 sign patterns of f = g = h = k = 0
    for fs in (0.0, -0.0):
        for gs in (0.0, -0.0):
            for hs in (0.0, -0.0):
                for ks in (0.0, -0.0):
                    cases.append(eq(f'all zero, f {fs!r}, g {gs!r}, h {hs!r}, k {ks!r}', 'signed', 7000.0e3, fs, gs, hs,
                                    ks, 115.0 * D))
    # Within 1e-12 of the singular values, and the true longitude outside [0, 2π) by less than a turn
    cases += [
        eq('h = k = 1e-12', 'near', 7980.0e3, f156, g156, 1e-12, 1e-12, 150.0 * D),
        eq('f = 1e-12, g = 0', 'near', 7000.0e3, 1e-12, 0.0, h155, k155, 115.0 * D),
        eq('h = tan(2.5e-11), k = 0 (tilt 5e-11)', 'near', 7980.0e3, f156, g156, math.tan(2.5e-11), 0.0, 150.0 * D),
        eq('L = 0, circular equatorial', 'near', 7000.0e3, 0.0, 0.0, 0.0, 0.0, 0.0),
        eq('L = -1 rad, circular inclined', 'near', 7000.0e3, 0.0, 0.0, h155, k155, -1.0),
    ]
    # The products of the argument of periapsis underflow: e tan(i/2) below the smallest normal double
    cases += [
        eq('h = k = 5e-324, e 0.05', 'underflow', 7980.0e3, f156, g156, 5e-324, 5e-324, 150.0 * D),
        eq('f = g = 5e-324, i 30, node 40', 'underflow', 7000.0e3, 5e-324, 5e-324, h155, k155, 115.0 * D),
        eq('f = -g = -h = k = 1e-200', 'underflow', 7000.0e3, 1e-200, -1e-200, -1e-200, 1e-200, 115.0 * D),
        eq('h = -6.73e-320, k = -0.0, e 0.05', 'underflow', 7980.0e3, f156, g156, -6.73e-320, -0.0, 150.0 * D),
        eq('h = 1e-310, k = 0, e 0.05', 'underflow', 7980.0e3, f156, g156, 1e-310, 0.0, 150.0 * D),
    ]
    # The true longitude beyond a turn
    cases += [
        eq('L = 40 rad, equatorial e 0.05', 'large L', 7980.0e3, f156, g156, 0.0, 0.0, 40.0),
        eq('L = 1e3 rad, circular equatorial', 'large L', 7000.0e3, 0.0, 0.0, 0.0, 0.0, 1e3),
        eq('L = 1e6 rad, circular, i 30, node 40', 'large L', 7000.0e3, 0.0, 0.0, h155, k155, 1e6),
        eq('L = 1e10 rad, equatorial e 0.05', 'large L', 7980.0e3, f156, g156, 0.0, 0.0, 1e10),
        eq('L = -1e10 rad, circular equatorial', 'large L', 7000.0e3, 0.0, 0.0, 0.0, 0.0, -1e10),
    ]
    # The retrograde equatorial orbit: h = tan(π/2) in doubles, finite
    cases += [
        eq('retrograde equatorial, h = tan(pi/2), k = 0, e 0.05', 'retro', 7980.0e3, f156, g156, tan_half_pi, 0.0,
           150.0 * D),
        eq('retrograde equatorial, h = -tan(pi/2), k = tan(pi/2) sin(pi), e 0.05', 'retro', 7980.0e3, f156, g156,
           -tan_half_pi, tan_half_pi * math.sin(math.pi), 150.0 * D),
        eq('retrograde equatorial, circular, h = tan(pi/2)', 'retro', 7000.0e3, 0.0, 0.0, tan_half_pi, 0.0, 115.0 * D),
        eq('retrograde i 179.9, circular', 'retro', 7000.0e3, 0.0, 0.0, math.tan(89.95 * D) * math.cos(40.0 * D),
           math.tan(89.95 * D) * math.sin(40.0 * D), 115.0 * D),
    ]
    # h or k beyond 1e150 (i within 2e-150 rad of π), where h² + k² overflows a double
    cases += [
        eq('h = 1.4e154, e 0.05', 'huge', 7980.0e3, f156, g156, 1.4e154, 0.0, 150.0 * D),
        eq('h = k = 1e160, e 0.05', 'huge', 7980.0e3, f156, g156, 1e160, 1e160, 150.0 * D),
        eq('k = 1e200, circular', 'huge', 7000.0e3, 0.0, 0.0, 0.0, 1e200, 115.0 * D),
        eq('h = largest double, e 0.05', 'huge', 7980.0e3, f156, g156, 1.7976931348623157e308, 0.0, 150.0 * D),
    ]
    # Conformance cases 151 to 154 (orbital_elements, modeq2cart), as the runner builds them
    cases += [
        eq('suite 151, generic', 'suite', 7425.0 * 1000.0, -0.01736481776669301, 0.09848077530122082,
           0.35721239031346064, 0.2997367849639794, 210.0 * D),
        eq('suite 152, e 0.9', 'suite', 13299.999999999996 * 1000.0, 0.15628335990023698, -0.8863269777109873,
           0.608229664883436, 0.10724730038621773, 90.0 * D),
        eq('suite 153, hyperbola', 'suite', 25000.0 * 1000.0, 1.4488887394336025, -0.388228567653781,
           0.15764939443949177, -0.27305676095166614, 45.0 * D),
        eq('suite 154, i 120', 'suite', 7425.0 * 1000.0, -0.01736481776669301, 0.09848077530122082,
           1.3268278963378763, 1.1133407984528383, 210.0 * D),
    ]
    # The CLI test kernels carry gm_de431
    cli_names = ('circular, i 30, node 40 (suite 155)', 'equatorial, e 0.05, lop 120 (suite 156)', 'suite 151, generic')
    cli = [eq(c['name'], 'cli', *[float(c[n]) for n in ('p', 'f', 'g', 'h', 'k', 'l')], dts=(0.0, 600.0), gm='gm_de431')
           for c in cases if c['name'] in cli_names]
    # Parabolic elements built by hand; P not finite comes from KeplerianElements (NaN) and StateVector (±∞). Within
    # the parabolic band, |e − 1| < 1e-6, the state is the conic of e: P, or q (1 + e) when P is not finite
    e_below, e_above = 1.0 - 9e-7, 1.0 + 9e-7
    parabolic = [
        eq('parabolic, P 14000 km finite, no perigee radius', 'parabolic', 14000.0e3, 0.6, 0.8, 0.1, 0.2, 1.0,
           dts=(0.0,)),
        eq('parabolic, P 14000 km and perigee radius 7000 km', 'parabolic', 14000.0e3, 0.6, 0.8, 0.1, 0.2, 1.0,
           dts=(0.0,), perigee_radius=7000.0e3),
        eq('parabolic, P NaN, perigee radius 7000 km', 'parabolic', math.nan, 0.6, 0.8, 0.1, 0.2, 1.0, dts=(0.0,),
           perigee_radius=7000.0e3),
        eq('near-parabolic, e 1 - 9e-7, P 14000 km and perigee radius P / (1 + e)', 'parabolic', 14000.0e3,
           e_below * math.cos(0.3), e_below * math.sin(0.3), 0.1, 0.05, 0.5, dts=(0.0,),
           perigee_radius=14000.0e3 / (1.0 + e_below)),
        eq('near-parabolic, e 1 - 9e-7, P +Infinity, perigee radius 7000 km', 'parabolic', math.inf,
           e_below * math.cos(0.3), e_below * math.sin(0.3), 0.1, 0.05, 0.5, dts=(0.0,), perigee_radius=7000.0e3),
        eq('near-parabolic, e 1 + 9e-7, P -Infinity, perigee radius 7000 km', 'parabolic', -math.inf,
           e_above * math.cos(0.3), e_above * math.sin(0.3), 0.1, 0.05, 0.5, dts=(0.0,), perigee_radius=7000.0e3),
    ]
    # Without a perigee radius, PerigeeRadius() is the semi-latus rectum of the state over 1 + e: P / (1 + e) exactly
    fm, gm = mpf(parabolic[0]['f']), mpf(parabolic[0]['g'])
    parabolic[0]['perigee_radius_reference'] = pair(mpf(parabolic[0]['p']) / (1 + mp.sqrt(fm * fm + gm * gm)))
    undefined = [
        {'name': 'parabolic, P NaN, no perigee radius', 'p': 'NaN', 'f': text(0.6), 'g': text(0.8), 'h': text(0.1),
         'k': text(0.2), 'l': text(1.0)},
        {'name': 'elliptic, P NaN', 'p': 'NaN', 'f': text(0.1), 'g': text(0.2), 'h': text(0.1), 'k': text(0.2),
         'l': text(1.0)},
        {'name': 'parabolic, P +Infinity, no perigee radius', 'p': 'Infinity', 'f': text(0.6), 'g': text(0.8),
         'h': text(0.1), 'k': text(0.2), 'l': text(1.0)},
    ]

    kep = gen.keplerian
    keplerian = [
        # The Keplerian cases of the issue, through ToEquinoctial, with their signed zeros
        kep('e 0, i 30, node 40', 'issue', 7000.0e3, 0.0, 30.0 * D, 40.0 * D, 0.0, 75.0 * D),
        kep('e 0, i 51.6, node 100, aop 30', 'issue', 6778.137e3, 0.0, 51.6 * D, 100.0 * D, 30.0 * D, 1.0),
        kep('e 0.05, i 0, aop 120', 'issue', 8000.0e3, 0.05, 0.0, 0.0, 120.0 * D, 1.0),
        kep('e 0.05, i 0, node 180, aop 300 (h = -0.0)', 'issue', 8000.0e3, 0.05, 0.0, 180.0 * D, 300.0 * D, 1.0),
        kep('e 0, i 51.6, node 100, aop 80 (node + aop = 180, f = -0.0)', 'issue', 6778.137e3, 0.0, 51.6 * D,
            100.0 * D, 80.0 * D, 1.0),
    ]
    for node in (0.0, 90.0, 180.0, 270.0):
        for periapsis in (0.0, 90.0, 180.0, 270.0):
            keplerian.append(kep(f'circular equatorial, node {node:.0f}, aop {periapsis:.0f}', 'circular equatorial',
                                 7000.0e3, 0.0, 0.0, node * D, periapsis * D, 1.0))
    for e, a in ((0.0, 7000.0e3), (0.05, 7980.0e3)):
        for node in (0.0, 180.0):
            keplerian.append(kep(f'retrograde equatorial, e {e}, i pi, node {node:.0f}, aop 30', 'retro', a, e,
                                 math.pi, node * D, 30.0 * D, 1.0))
    keplerian += [
        kep('e 0.9, i 0, aop 250', 'high eccentricity', 70000.0e3, 0.9, 0.0, 0.0, 250.0 * D, 0.5),
        kep('hyperbola e 1.5, i 0, aop 345', 'high eccentricity', -14000.0e3, 1.5, 0.0, 0.0, 345.0 * D, 0.5),
        kep('parabolic, i 0.2, node 0.3, aop 0.4, M 0', 'parabolic', math.inf, 1.0, 0.2, 0.3, 0.4, 0.0,
            perigee_radius=6800000.0, dts=(0.0,)),
        kep('parabolic, i 0, aop 0.4, M 0.5', 'parabolic', math.inf, 1.0, 0.0, 0.0, 0.4, 0.5, perigee_radius=6800000.0,
            dts=(0.0,)),
        kep('parabolic, i 30, node 40, aop 0, M -0.3', 'parabolic', math.inf, 1.0, 30.0 * D, 40.0 * D, 0.0, -0.3,
            perigee_radius=7000000.0, dts=(0.0,)),
    ]

    tle = [
        tle_create('mean, circular, i 51.6, node 40, L 115', 6778.137e3, 0.0, 0.0, t258 * math.cos(40.0 * D),
                   t258 * math.sin(40.0 * D), 115.0 * D),
        tle_create('mean, equatorial, e 0.001, lop 120, L 150', 6778.137e3, 0.001 * math.cos(120.0 * D),
                   0.001 * math.sin(120.0 * D), 0.0, 0.0, 150.0 * D),
        # g = -0.0: the argument of perigee is +0.0, not -0.0, which line 2 would print as -0.0000
        tle_create('mean, equatorial, e 0.001, g -0.0, L 1 rad', 6778.137e3, 0.001, -0.0, 0.0, 0.0, 1.0),
    ]

    print(json.dumps({
        'generated_by': 'tools/equinoctial_elements/equinoctial_reference.py, mpmath ' + mp.__version__ +
                        ' at 60 digits, GM by ' + spice.tkvrsn('TOOLKIT'),
        'units': 'm, m/s, rad, s; the fields of line 2 of a TLE in degrees',
        'states': 'each component of r and v is a pair [hi, lo] of doubles whose sum is the 60-digit value',
        'gm': {name: text(value) for name, value in gen.gm.items()},
        'walker_vs_classical_m': mp.nstr(gen.walker_vs_classical, 3),
        'equinoctial': cases, 'cli': cli, 'parabolic': parabolic, 'undefined': undefined, 'keplerian': keplerian,
        'tle_create': tle}, indent=1, ensure_ascii=False))


if __name__ == '__main__':
    main()
