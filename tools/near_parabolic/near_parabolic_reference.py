"""References of the near-parabolic band, at 60 digits, for the tests of #459, #462, #477, #478 and #753.

The band is |e - 1| < 1e-6, where the library takes the anomalies of a parabola (Barker's equation, #368). The
references are closed forms evaluated with mpmath at 60 digits on the doubles the tests give the library, so they
measure the computation alone:

- the conic of a state: h = r x v, e = |v x h / mu - r / |r||, p = h^2 / mu, q = p / (1 + e), the specific energy
  xi = v^2 / 2 - mu / |r|, the semi-major axis a = -mu / (2 xi) and the mean motion n = sqrt(mu / |a|^3) of every
  e != 1 (Vallado, Fundamentals of Astrodynamics and Applications, 4th ed., 2013, algorithm 9 RV2COE; to be verified by
  S. Guillet);
- Barker's equation: D = tan(nu / 2), M = D + D^3 / 3, t - T = sqrt(2 q^3 / mu) M (Vallado, section 2.2), and its
  inverse in closed form;
- the exact two-body motion of the conic: Kepler's elliptic (M = E - e sin E) or hyperbolic (M = e sinh H - H) equation
  solved at 60 digits, n = sqrt(mu / |a|^3);
- the perifocal state of a conic, r = p / (1 + e cos nu), v = sqrt(mu / p) (-sin nu, e + cos nu), rotated by
  R3(-Omega) R1(-i) R3(-omega) (Vallado, algorithm 10 COE2RV; confirmed by S. Guillet, 2026-11-15);
- the modified equinoctial elements to Cartesian (Walker, Ireland and Owens, Celestial Mechanics 36, 1985; confirmed by
  S. Guillet, 2026-10-27), as tools/equinoctial_elements writes them.

The band model of the library before #753 (#459, option 1, decided by S. Guillet, 2026-11-17) was Barker's anomaly
advanced at Barker's rate sqrt(mu / (2 q^3)), with the conic's semi-latus rectum q (1 + e) for the state; its distance
to the exact motion of the conic is the model error the tests print. Since #753 (decided by S. Guillet, 2026-11-18) the
library moves the band as the exact conic, in universal variables, which sections 9 and 10 measure: the conic's times
to a true anomaly and a radius, and its two-body states over |e - 1| from 1e-12 to 1e-5.

Each value is written as a C# literal, a double or a pair (hi, lo) of doubles whose sum is the 60-digit value, so that
an error of the order of an ulp is measured without the rounding of the reference. mu is the Earth GM of gm_de440.tpc
as the library reads it (CSPICE bodvcd_c, km^3/s^2, times 1e9 in doubles).

Run: python3 -I near_parabolic_reference.py from this directory (needs mpmath and spiceypy); the values are copied into
IO.Astrodynamics.Net/IO.Astrodynamics.Tests/OrbitalParameters/NearParabolicBandTests.cs.
"""
import math
import os

import mpmath as mp
import spiceypy as spice

mp.mp.dps = 60
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data', 'SolarSystem')
spice.furnsh(os.path.join(DATA, 'gm_de440.tpc'))
MU_DOUBLE = float(spice.bodvcd(399, 'GM', 1)[1][0]) * 1e9
MU = mp.mpf(MU_DOUBLE)
DEG = math.pi / 180.0  # Constants.Deg2Rad


def pair(x):
    hi = float(x)
    lo = float(x - mp.mpf(hi))
    return f'({hi!r}, {lo!r})'


def vec(xs):
    return 'new[] { ' + ', '.join(repr(float(x)) for x in xs) + ' }'


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def norm(a):
    return mp.sqrt(dot(a, a))


def rotation(i, node, periapsis):
    co, so, ci, si, cw, sw = mp.cos(node), mp.sin(node), mp.cos(i), mp.sin(i), mp.cos(periapsis), mp.sin(periapsis)
    px = [co * cw - so * sw * ci, so * cw + co * sw * ci, sw * si]
    py = [-co * sw - so * cw * ci, -so * sw + co * cw * ci, cw * si]
    return px, py


def perifocal_state(p, e, i, node, periapsis, nu):
    px, py = rotation(i, node, periapsis)
    r = p / (1 + e * mp.cos(nu))
    x, y = r * mp.cos(nu), r * mp.sin(nu)
    w = mp.sqrt(MU / p)
    vx, vy = -w * mp.sin(nu), w * (e + mp.cos(nu))
    return [x * px[j] + y * py[j] for j in range(3)], [vx * px[j] + vy * py[j] for j in range(3)]


class Conic:
    """The exact conic of a state given as doubles."""

    def __init__(self, r, v):
        self.r = [mp.mpf(x) for x in r]
        self.v = [mp.mpf(x) for x in v]
        h = cross(self.r, self.v)
        self.h = h
        ev = [c / MU - x / norm(self.r) for c, x in zip(cross(self.v, h), self.r)]
        self.ev = ev
        self.e = norm(ev)
        self.p = dot(h, h) / MU
        self.q = self.p / (1 + self.e)
        self.xi = dot(self.v, self.v) / 2 - MU / norm(self.r)
        self.a = -MU / (2 * self.xi)
        self.n = mp.sqrt(MU / abs(self.a) ** 3)
        # True anomaly, signed by r . v
        cos_nu = dot(ev, self.r) / (self.e * norm(self.r))
        nu = mp.acos(max(-1, min(1, cos_nu)))
        self.nu = nu if dot(self.r, self.v) >= 0 else -nu
        # Perifocal axes from the state
        self.px = [x / self.e for x in ev]
        hn = [x / norm(h) for x in h]
        self.py = cross(hn, self.px)

    def state_at_nu(self, nu, p=None):
        p = self.p if p is None else p
        r = p / (1 + self.e * mp.cos(nu))
        x, y = r * mp.cos(nu), r * mp.sin(nu)
        w = mp.sqrt(MU / p)
        vx, vy = -w * mp.sin(nu), w * (self.e + mp.cos(nu))
        return [x * self.px[j] + y * self.py[j] for j in range(3)], [vx * self.px[j] + vy * self.py[j] for j in range(3)]

    def kepler_mean(self, nu):
        e = self.e
        if e < 1:
            big_e = 2 * mp.atan(mp.sqrt((1 - e) / (1 + e)) * mp.tan(nu / 2))
            return big_e - e * mp.sin(big_e)
        big_h = 2 * mp.atanh(mp.sqrt((e - 1) / (e + 1)) * mp.tan(nu / 2))
        return e * mp.sinh(big_h) - big_h

    def kepler_nu(self, m):
        e = self.e
        if e < 1:
            big_e = mp.findroot(lambda x: x - e * mp.sin(x) - m, m / (1 - e) if abs(m) < 1e-3 else m)
            return 2 * mp.atan(mp.sqrt((1 + e) / (1 - e)) * mp.tan(big_e / 2))
        big_h = mp.findroot(lambda x: e * mp.sinh(x) - x - m, mp.asinh(m / e) if abs(m) > 1 else m / (e - 1))
        return 2 * mp.atan(mp.sqrt((e + 1) / (e - 1)) * mp.tanh(big_h / 2))

    def conic_after(self, dt):
        """The exact two-body state dt seconds later."""
        m = self.kepler_mean(self.nu) + self.n * dt
        return self.state_at_nu(self.kepler_nu(m))

    def conic_time_to_nu(self, nu):
        return (self.kepler_mean(nu) - self.kepler_mean(self.nu)) / self.n

    def barker_rate(self):
        return mp.sqrt(MU / (2 * self.q ** 3))

    def barker_after(self, dt):
        """The band model: Barker's anomaly at Barker's rate, the state of the conic's p."""
        m = barker_mean(self.nu) + self.barker_rate() * dt
        return self.state_at_nu(barker_nu(m))

    def barker_time_to_nu(self, nu):
        return (barker_mean(nu) - barker_mean(self.nu)) / self.barker_rate()


def barker_mean(nu):
    d = mp.tan(nu / 2)
    return d + d ** 3 / 3


def barker_nu(m):
    d = 2 * mp.sinh(mp.asinh(mp.mpf(3) * m / 2) / 3)
    return 2 * mp.atan(d)


def equinoctial_state(p, f, g, h, k, l):
    s2 = 1 + h * h + k * k
    fv = [(1 - k * k + h * h) / s2, 2 * h * k / s2, -2 * k / s2]
    gv = [2 * h * k / s2, (1 + k * k - h * h) / s2, 2 * h / s2]
    c, s = mp.cos(l), mp.sin(l)
    r = p / (1 + f * c + g * s)
    w = mp.sqrt(MU / p)
    return [r * (c * fv[j] + s * gv[j]) for j in range(3)], [w * (-(g + s) * fv[j] + (f + c) * gv[j]) for j in range(3)]


def doubles(state):
    return [float(x) for x in state[0]], [float(x) for x in state[1]]


def print_state(name, state):
    r, v = state
    print(f'    {name}: r = {vec(r)}, v = {vec(v)}')


def distance(a, b):
    return norm([x - y for x, y in zip(a, b)])


print(f'mu = {MU_DOUBLE!r}')

# 1. #462: Keplerian elements of the band, q = 6800 km; P = q (1 + e) of the conic, 2q at e = 1
print('\n#462 Keplerian P = q (1 + e), q = 6.8e6 m')
for e in (1.0, 1.0 - 5e-7, 1.0 + 5e-7):
    print(f'  e = {e!r}: P = {pair(mp.mpf(6.8e6) * (1 + mp.mpf(e)))}')

# 2. #459, #462: states at the periapsis r = (7000 km, 0, 0), v = (0, vy, 0) of the issues
print('\n#459/#462 periapsis states r = (7e6, 0, 0), v = (0, vy, 0)')
for vy in (10671.729487052813, 10671.7308, 10671.732154985518):
    c = Conic([7e6, 0.0, 0.0], [0.0, vy, 0.0])
    print(f'  vy = {vy!r}: e - 1 = {mp.nstr(c.e - 1, 6)}, P = h^2/mu = {pair(c.p)}, a = {pair(c.a)}, n = {pair(c.n)}, '
          f'q = {pair(c.q)}')
    after = doubles(c.conic_after(600))
    model = c.barker_after(600)
    print(f'    +600 s TT: conic r = {vec(after[0])}, v = {vec(after[1])}; band model off the conic by '
          f'{mp.nstr(distance(model[0], c.conic_after(600)[0]), 6)} m')

# 3. #478 and #459: q = 7000 km, i = 30 deg, Omega = 40 deg, omega = 0, nu0 = 0.5 rad, built at 60 digits with
# p = q (1 + e) and rounded to doubles
print('\n#478/#459 states q = 7e6, i = 30 deg, Omega = 40 deg, omega = 0, nu0 = 0.5 rad')
for de in (-9e-7, -5e-7, -1e-12, 1e-12, 5e-7, 9e-7):
    e = 1 + mp.mpf(de)
    q = mp.mpf(7e6)
    state = doubles(perifocal_state(q * (1 + e), e, mp.mpf(30) * mp.pi / 180, mp.mpf(40) * mp.pi / 180, 0, mp.mpf(0.5)))
    c = Conic(*state)
    r1 = float(c.p / (1 + c.e * mp.cos(1)))  # the radius at nu = 1 rad, as a double
    nu_r1 = mp.acos((c.p / r1 - 1) / c.e)  # the true anomaly of that double radius
    print(f'  e - 1 = {de!r}:')
    print_state('state', state)
    print(f'    exact e - 1 = {mp.nstr(c.e - 1, 8)}, e = {pair(c.e)}, q = {pair(c.q)}, nu0 = {pair(c.nu)}, a = {pair(c.a)}, n = {pair(c.n)}')
    print(f'    Barker time to nu = 1 rad: {pair(c.barker_time_to_nu(mp.mpf(1)))} s (conic {mp.nstr(c.conic_time_to_nu(mp.mpf(1)), 15)} s)')
    print(f'    r at nu = 1: {r1!r} m, Barker time to it {pair(c.barker_time_to_nu(nu_r1))} s')
    r2 = 2e7
    nu2 = mp.acos((c.p / r2 - 1) / c.e)
    print(f'    Barker time to 2e7 m: {pair(c.barker_time_to_nu(nu2))} s (conic {mp.nstr(c.conic_time_to_nu(nu2), 15)} s)')
    after = c.conic_after(600)
    model = c.barker_after(600)
    print(f'    +600 s TT: conic r = {vec(after[0])}, v = {vec(after[1])}; band model off the conic by '
          f'{mp.nstr(distance(model[0], after[0]), 6)} m')
    print_state('band model +600 s', doubles(model))

print('\n#478 Barker M = D + D^3/3')
for nu in (-1.0, 0.5, 1.0, 1.5):
    print(f'  nu = {nu!r}: M = {pair(barker_mean(mp.mpf(nu)))}')

# 4. #478: an exact parabola as Keplerian elements, q = 7000 km, i = 30 deg, Omega = 40 deg, omega = 0, M = +-0.3
print('\n#478 exact parabola, q = 7e6, the doubles of 30 and 40 deg')
i30, o40 = 30 * DEG, 40 * DEG
print(f'  i = {i30!r}, Omega = {o40!r}')
for m in (-0.3, 0.3):
    nu = barker_nu(mp.mpf(m))
    state = perifocal_state(mp.mpf(1.4e7), mp.mpf(1), mp.mpf(i30), mp.mpf(o40), mp.mpf(0), nu)
    print(f'  M = {m!r}: Barker state r = {vec(state[0])}, v = {vec(state[1])}')

# 5. #477: parabolic equinoctial elements without a perigee radius
print('\n#477 equinoctial parabolas')
cases = {
    'case 1': (14e6, 1.0, 0.0, 0.1, 0.05, 0.5, (0, 600, 3600)),
    'case 2': (13756274.0, 0.6, 0.8, -0.2, 0.3, 2.0, (0, 5400)),
}
for name, (p, f, g, h, k, l, times) in cases.items():
    pm, fm, gm_, hm, km, lm = (mp.mpf(x) for x in (p, f, g, h, k, l))
    e = mp.sqrt(fm * fm + gm_ * gm_)
    state = equinoctial_state(pm, fm, gm_, hm, km, lm)
    c = Conic(*doubles(state))  # its exact conic; q = P / (1 + e)
    t = mp.sqrt(hm * hm + km * km)
    node = mp.atan2(km, hm) % (2 * mp.pi)
    peri = (mp.atan2(gm_, fm) - mp.atan2(km, hm)) % (2 * mp.pi)
    nu = lm - mp.atan2(gm_, fm)
    print(f'  {name}: e - 1 = {mp.nstr(e - 1, 5)}, q = P/(1 + e) = {pair(pm / (1 + e))}, Barker n = {pair(mp.sqrt(MU / (2 * (pm / (1 + e)) ** 3)))}')
    print(f'    i = {pair(2 * mp.atan(t))}, Omega = {pair(node)}, omega = {pair(peri)}, M = {pair(barker_mean(nu))}')
    for dt in times:
        # The exact motion of the elements' parabola: Barker's equation at q = P / (1 + e) (e within 2.2e-17 of 1)
        qe = pm / (1 + e)
        m = barker_mean(nu) + mp.sqrt(MU / (2 * qe ** 3)) * dt
        nu_t = barker_nu(m)
        r = pm / (1 + e * mp.cos(nu_t))
        lt = mp.atan2(gm_, fm) + nu_t
        st = equinoctial_state(pm, fm, gm_, hm, km, lt)
        print(f'    +{dt} s TT: r = {vec(st[0])}, v = {vec(st[1])}, nu = {pair(nu_t)}')

# 6. #477 band rows: P finite, e - 1 = +-4e-7, H = 0.1, K = 0.05, L0 = 0.5, G = 0
print('\n#477 band rows')
for p, f in ((2e7, 1.0 + 4e-7), (1.6e7, 1.0 - 4e-7)):
    st = equinoctial_state(mp.mpf(p), mp.mpf(f), mp.mpf(0), mp.mpf(0.1), mp.mpf(0.05), mp.mpf(0.5))
    print(f'  P = {p!r}, F = {f!r}: r = {vec(st[0])}, v = {vec(st[1])}')

# 7. #459: the double edges of the band as Keplerian elements, q = 7000 km, a = q / (1 - e) in doubles, M = 0.3
print('\n#459 edges, q = 7e6, i = 0.7, Omega = 0.5, omega = 0.9, M = 0.3')
for e in (0.999999, math.nextafter(0.999999, 2.0), 1.000001):
    a = 7e6 / (1.0 - e)
    em = mp.mpf(e)
    in_band = abs(e - 1.0) < 1e-6
    if in_band:
        # The band model: Barker's M, the conic's p = q (1 + e)
        nu = barker_nu(mp.mpf(0.3))
        p = mp.mpf(7e6) * (1 + em)
    else:
        big_e = mp.findroot(lambda x: x - em * mp.sin(x) - mp.mpf(0.3), 1)
        nu = 2 * mp.atan(mp.sqrt((1 + em) / (1 - em)) * mp.tan(big_e / 2))
        p = mp.mpf(a) * (1 - em) * (1 + em)
    st = perifocal_state(p, em, mp.mpf(0.7), mp.mpf(0.5), mp.mpf(0.9), nu)
    print(f'  e = {e!r} (band {in_band}), a = {a!r}: r = {vec(st[0])}, v = {vec(st[1])}')

# 8. The states of 3 as the C# table of NearParabolicBandTests (BandStates)
print('\nC# BandStates')
for de in (-9e-7, -5e-7, -1e-12, 1e-12, 5e-7, 9e-7):
    e = 1 + mp.mpf(de)
    q = mp.mpf(7e6)
    state = doubles(perifocal_state(q * (1 + e), e, mp.mpf(30) * mp.pi / 180, mp.mpf(40) * mp.pi / 180, 0, mp.mpf(0.5)))
    c = Conic(*state)
    r1 = float(c.p / (1 + c.e * mp.cos(1)))
    nu_r1 = mp.acos((c.p / r1 - 1) / c.e)
    nu2 = mp.acos((c.p / 2e7 - 1) / c.e)
    after = doubles(c.conic_after(600))
    model = doubles(c.barker_after(600))

    def d(x):
        return repr(float(x))

    def dd(x):
        hi = float(x)
        return f'{hi!r}, {float(x - mp.mpf(hi))!r}'

    print(f'        new({de!r}, {vec(state[0])}, {vec(state[1])}, {d(c.e)}, {d(c.q)}, {dd(c.nu)},')
    print(f'            {dd(c.barker_time_to_nu(mp.mpf(1)))}, {r1!r}, {dd(c.barker_time_to_nu(nu_r1))}, {dd(c.barker_time_to_nu(nu2))},')
    print(f'            {dd(c.a)}, {dd(c.n)}, {vec(after[0])}, {vec(after[1])}, {vec(model[0])}),')

# 9. #753: the exact conic's times of the states of 3, the motion of the band since 11.0.0 (universal variables): the
# conic's time to nu = 1 rad, to the double radius at nu = 1 rad and to 2e7 m (Kepler's equation of the conic at 60
# digits), as the C# table BandConicTimes of NearParabolicBandTests
print('\nC# BandConicTimes (#753)')
for de in (-9e-7, -5e-7, -1e-12, 1e-12, 5e-7, 9e-7):
    e = 1 + mp.mpf(de)
    q = mp.mpf(7e6)
    state = doubles(perifocal_state(q * (1 + e), e, mp.mpf(30) * mp.pi / 180, mp.mpf(40) * mp.pi / 180, 0, mp.mpf(0.5)))
    c = Conic(*state)
    r1 = float(c.p / (1 + c.e * mp.cos(1)))
    nu_r1 = mp.acos((c.p / r1 - 1) / c.e)
    nu2 = mp.acos((c.p / 2e7 - 1) / c.e)
    print(f'        ({de!r}, {pair(c.conic_time_to_nu(mp.mpf(1)))}, {pair(c.conic_time_to_nu(nu_r1))}, '
          f'{pair(c.conic_time_to_nu(nu2))}),')

# 10. #753: the motion of the band against the exact conic, |e - 1| from 1e-12 to 9.9e-7 on both sides and just outside
# the band (1.1e-6, 1e-5), q = 7000 km, i = 30 deg, Omega = 40 deg, omega = 0, nu0 in {-1.5, 0.5, 2} rad, the state
# built at 60 digits with p = q (1 + e) and rounded to doubles, and the exact two-body state of its conic dt later,
# dt in {600, 86400, -3600} s, as the C# table BandMotions of NearParabolicBandTests
print('\nC# BandMotions (#753)')
for de in (-1e-5, -1.1e-6, -9.9e-7, -5e-7, -1e-7, -1e-8, -1e-10, -1e-12, 1e-12, 1e-10, 1e-8, 1e-7, 5e-7, 9.9e-7, 1.1e-6, 1e-5):
    e = 1 + mp.mpf(de)
    q = mp.mpf(7e6)
    for nu0 in (-1.5, 0.5, 2.0):
        state = doubles(perifocal_state(q * (1 + e), e, mp.mpf(30) * mp.pi / 180, mp.mpf(40) * mp.pi / 180, 0, mp.mpf(nu0)))
        c = Conic(*state)
        for dt in (600, 86400, -3600):
            after = c.conic_after(dt)
            print(f'        new({de!r}, {nu0!r}, {vec(state[0])}, {vec(state[1])}, {dt!r}.0, {vec(after[0])}, {vec(after[1])}),')
