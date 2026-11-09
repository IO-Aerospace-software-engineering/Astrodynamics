"""Reference values of the 2D probability of collision, at 50 digits, for the tests of #610, #611 and #612 (lot 6 of #457).

The quantity is CARA's Pc2D (NASA CARA Analysis Tools, Pc2D_Foster): the integral of the Gaussian N(0, C2D) of the
encounter plane over the disc of the combined hard-body radius R centred on the in-plane miss, each eigenvalue of C2D
raised to at least (1e-4 R)^2 (CARA's clip); Frisbee's maximum Pc (CARA's FrisbeeMaxPc) when one covariance is absent:
C2D + V_c u u^T, u along the miss, V_c = m^2 (k^2 - 1) / k^2, k^2 the Mahalanobis distance of the miss, C2D unchanged when
k^2 <= 1. Every input is a double, read exactly in mpmath at 50 digits, so the reference is exact for the inputs and
measures the computation alone:

    projection   the relative state rho = r2 - r1, nu = v2 - v1; the miss is the projection of rho on the plane normal
                 to nu, e1 along it, e2 = nu^ x e1; C2D = P C3 P^T, P = [e1; e2], C3 the sum of the two 3x3 position
                 blocks in inertial axes, each rotated from its own RTN (R = r^, N = (r x v)^, T = N x R) when given in
                 RTN; the off-diagonal term of C2D averaged (the antisymmetric part of an input drops out);
    eigenvalues  the closed form of the 2x2 at 50 digits; the clip applied to both;
    integral     in the principal axes, x = x_c + R sin(t), h = R cos(t),
                 P = int_{-pi/2}^{pi/2} h phi(x; l1) 1/2 [erfc((|y_c| - h)/sqrt(2 l2)) - erfc((|y_c| + h)/sqrt(2 l2))] dt,
                 split at the Gaussian's mean and +-1, 2, 4, 8, 16 sigma in x and at the chords reaching |y_c| + k sigma;
                 computed by tanh-sinh and by Gauss-Legendre, kept when the two agree to 1e-30 relative (both written).

The families of rows:

    encounters   the seven collision_probability encounters of lot 6 (the lot's design, section 1.3; no third-party
                 input, choice 25, S. Guillet, 2026-11-09), each a regime of CARA's test set: 001 a high Pc (2.1e-2),
                 002 a mid Pc (2.7e-5), 003 a low Pc (1.1e-8), 004 a Pc near 5e-12 (the 1e-12 floor of the suite's
                 cases), 005 a hard-body radius much larger than the covariance (#610), 006 a combined covariance not
                 positive definite in the encounter plane, its eigenvalues (1000 km)^2 and -1 m^2 (#612), 007
                 Frisbee's maximum Pc, the secondary covariance absent. Two states at TCA in ICRF (km, km/s), each 3x3
                 position covariance in its own RTN (km^2, the CDM's lower triangle R, TR, TT, NR, NT, NN), the
                 combined hard-body radius (km); the states at the linear TCA (rho normal to nu to the doubles);
    D1, D2, D3   the reproductions of #610, #611 and #612: miss2D and C2D given in the encounter plane (m, m^2);
    CARA         the published cases of IO's own checks: NASA CARA Pc2D_Foster_UnitTest.m Omitron case 01 and Alfano
                 case 03 (3D inputs as the tests build them), Frisbee AAS 15-717 table 1 (2D, the maximum Pc);
    erf          erf and erfc at points of each branch of fdlibm's s_erf.c, for the private erf/erfc of the Pc path.

Run: python3 collision_probability_reference.py > collision_probability_reference.json (mpmath only; about 20 seconds).
"""
import json
import math

import mpmath
from mpmath import mp, mpf, matrix, sqrt, exp, erfc, erf, pi, asin, acos, cos, sin, atan2, quad, nstr

mp.dps = 50

DIGITS = 25


def digits(x):
    return nstr(x, DIGITS, min_fixed=-mpmath.inf, max_fixed=mpmath.inf) if x != 0 else '0'


def vec(v):
    return matrix([mpf(c) for c in v])


def norm(v):
    return sqrt(sum(c * c for c in v))


def unit(v):
    return v / norm(v)


def cross(a, b):
    return matrix([a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]])


def dot(a, b):
    return sum(a[i] * b[i] for i in range(3))


def rtn_rows(r, v):
    """Rows R, T, N of the RTN frame of (r, v): a vector in ICRF times this matrix is in RTN."""
    radial = unit(r)
    normal = unit(cross(r, v))
    along = cross(normal, radial)
    return matrix([[radial[i] for i in range(3)], [along[i] for i in range(3)], [normal[i] for i in range(3)]])


def lower_to_matrix(lower):
    rr, tr, tt, nr, nt, nn = (mpf(c) for c in lower)
    return matrix([[rr, tr, nr], [tr, tt, nt], [nr, nt, nn]])


def rtn_to_inertial(lower, r, v):
    m = rtn_rows(r, v)
    return m.T * lower_to_matrix(lower) * m


def project(rho, nu, c3):
    """IO's encounter-plane frame: e1 along the in-plane miss, e2 = nu^ x e1; the miss (m1, 0) and C2D (a, b, c)."""
    nu_hat = unit(nu)
    in_plane = rho - nu_hat * dot(rho, nu_hat)
    e1 = unit(in_plane)
    e2 = cross(nu_hat, e1)
    p = matrix([[e1[i] for i in range(3)], [e2[i] for i in range(3)]])
    c2 = p * c3 * p.T
    return [dot(rho, e1), dot(rho, e2)], (c2[0, 0], (c2[0, 1] + c2[1, 0]) / 2, c2[1, 1])


def principal(miss, cov):
    a, b, c = (mpf(x) for x in cov)
    tr, disc = a + c, sqrt((a - c) ** 2 + 4 * b * b)
    l1, l2 = (tr + disc) / 2, (tr - disc) / 2
    th = atan2(2 * b, a - c) / 2
    x = mpf(miss[0]) * cos(th) + mpf(miss[1]) * sin(th)
    y = -mpf(miss[0]) * sin(th) + mpf(miss[1]) * cos(th)
    return x, y, l1, l2


def disc_integral(xc, yc, l1, l2, radius, method):
    radius = mpf(radius)
    yc = abs(yc)
    s1, s2 = sqrt(l1), sqrt(l2)

    def f(t):
        h = radius * cos(t)
        x = xc + radius * sin(t)
        return h * exp(-x * x / (2 * l1)) / sqrt(2 * pi * l1) * (
            erfc((yc - h) / sqrt(2 * l2)) - erfc((yc + h) / sqrt(2 * l2))) / 2

    points = {-pi / 2, pi / 2, mpf(0)}
    for k in (0, 1, -1, 2, -2, 4, -4, 8, -8, 16, -16):
        u = (k * s1 - xc) / radius
        if -1 < u < 1:
            points.add(asin(u))
        reach = (yc + k * s2) / radius
        if 0 < reach < 1:
            points.add(acos(reach))
            points.add(-acos(reach))
    return quad(f, sorted(points), method=method, maxdegree=10)


def pc_of(miss, cov, radius, maximum=False):
    """The Pc (or Frisbee's maximum Pc) of an encounter-plane miss and covariance, with CARA's clip; and its facts."""
    x, y, l1, l2 = principal(miss, cov)
    clip = (mpf('1e-4') * mpf(radius)) ** 2
    c1, c2 = max(l1, clip), max(l2, clip)
    remediated = c1 != l1 or c2 != l2
    k2 = None
    if maximum:
        k2 = x * x / c1 + y * y / c2
        if k2 > 1:
            m2 = x * x + y * y
            vc = m2 * (k2 - 1) / k2
            u1, u2 = x / sqrt(m2), y / sqrt(m2)
            # The augmented covariance in the principal axes of the known one, decomposed again, clipped again
            aug_miss = [x, y]
            aug_cov = (c1 + vc * u1 * u1, vc * u1 * u2, c2 + vc * u2 * u2)
            x, y, a1, a2 = principal(aug_miss, aug_cov)
            c1, c2 = max(a1, clip), max(a2, clip)
            remediated = remediated or c1 != a1 or c2 != a2
    p = disc_integral(x, y, c1, c2, radius, 'tanh-sinh')
    q = disc_integral(x, y, c1, c2, radius, 'gauss-legendre')
    agreement = abs(p - q) / p if p else abs(q)
    if agreement > mpf('1e-30'):
        raise SystemExit(f'quadratures disagree: {nstr(p, 30)} {nstr(q, 30)}')
    out = {'pc': digits(p), 'quadrature_agreement': nstr(agreement, 2), 'lambda_major': digits(l1),
           'lambda_minor': digits(l2), 'remediated': bool(remediated)}
    if maximum:
        out['k_squared'] = digits(k2)
    return out


def encounter_reference(e):
    r1, v1, r2, v2 = vec(e['r1']), vec(e['v1']), vec(e['r2']), vec(e['v2'])
    c3 = matrix(3, 3)
    if e['cov1_rtn'] is not None:
        c3 += rtn_to_inertial(e['cov1_rtn'], r1, v1)
    if e['cov2_rtn'] is not None:
        c3 += rtn_to_inertial(e['cov2_rtn'], r2, v2)
    miss, cov = project(r2 - r1, v2 - v1, c3)
    maximum = (e['cov1_rtn'] is None) != (e['cov2_rtn'] is None)
    out = pc_of(miss, cov, e['hbr'], maximum)
    out['miss'] = digits(norm(matrix([miss[0], miss[1], 0])))
    out['maximum'] = maximum
    return out


# ---- The designed encounters ----

# The seven collision_probability encounters of lot 6 of #457, as the lot's design gives them (§1.3, choice 25: designed
# in the suite's catalog in the regimes of CARA's test set, no third-party input; object 1 from elements, object 2 from
# object 1's velocity rotated about r1^, the miss laid normal to nu and tuned to the target Pc): the doubles of the
# inputs.yaml of the suite's cases (conformance-tests, lot-6 branch 970a3bf). The reference below is computed from them,
# independently of the suite's generator.
ENCOUNTERS = [
    {
        'name': 'collision_probability_synthetic_high_001',
        'r1': [-302.6079836212348, -1552.3001116007206, 6902.172228098769],
        'v1': [-5.780434938893088, -4.607822719200638, -1.2893277595617945],
        'cov1_rtn': [6.4e-05, 9.6e-05, 0.0016, 2.4000000000000003e-06, -2.4e-05, 3.6e-05],
        'r2': [-302.61022468604125, -1552.323929047974, 6902.19390078284],
        'v2': [5.16566454144894, -5.354231640363491, -0.9777206147489946],
        'cov2_rtn': [0.000625, -0.00225, 0.0225, 4.5e-05, 0.00054, 0.000324],
        'hbr': 0.015,
    },
    {
        'name': 'collision_probability_synthetic_mid_002',
        'r1': [3994.560474297074, -3789.254417110051, -4133.12814434483],
        'v1': [1.9772632255384104, 6.266340704116273, -3.8296747425663358],
        'cov1_rtn': [0.04, 0.15, 2.25, -0.006, 0.022500000000000003, 0.0225],
        'r2': [3994.9598075847393, -3787.665689599236, -4132.808709184362],
        'v2': [6.0720248540600466, 4.0295097340487676, 2.1763650216714368],
        'cov2_rtn': [0.16, -0.36, 9.0, 0.01, 0.30000000000000004, 0.0625],
        'hbr': 0.02,
    },
    {
        'name': 'collision_probability_synthetic_low_003',
        'r1': [-1911.0644212705727, -5908.371453139248, 3575.778535115357],
        'v1': [1.6599751245096006, 3.37459831569408, 6.448190752627052],
        'cov1_rtn': [0.0009, 0.0018000000000000002, 0.09, 0.0, 0.0, 0.0004],
        'r2': [-1910.6436277219398, -5909.1315359907585, 3575.630560326054],
        'v2': [-6.754943160618439, 0.5089961178126002, -2.761812135115889],
        'cov2_rtn': [0.0064, -0.028800000000000003, 0.81, 0.00024, 0.0054, 0.0036],
        'hbr': 0.02,
    },
    {
        'name': 'collision_probability_synthetic_floor_004',
        'r1': [4934.22176669829, 212.4985225124609, 4853.149713150022],
        'v1': [-5.158668835026957, -1.6307234140360465, 5.322368801650167],
        'cov1_rtn': [0.0025, 0.003, 0.36, 0.0, 0.0, 0.0016],
        'r2': [4929.432111503135, 203.86336667483852, 4858.643644963533],
        'v2': [-0.07336760549403824, -7.578052422643333, 0.40798794101017416],
        'cov2_rtn': [0.0225, 0.07500000000000001, 6.25, 0.0, 0.025, 0.01],
        'hbr': 0.004,
    },
    {
        'name': 'collision_probability_synthetic_large_hbr_005',
        'r1': [4937.991989654868, -3142.3660941833127, 3413.167795332686],
        'v1': [0.6974804575552094, 6.094503651130246, 4.606778466921783],
        'cov1_rtn': [1.6000000000000003e-07, 4.8000000000000006e-08, 3.6e-07, 0.0, 0.0, 9e-08],
        'r2': [4937.953040484434, -3142.3392995452737, 3413.255483153362],
        'v2': [-5.206632594279286, -3.006050729878625, 4.765132899385091],
        'cov2_rtn': [2.5e-07, -4.000000000000001e-08, 6.400000000000001e-07, 0.0, 2.8000000000000003e-08, 1.225e-07],
        'hbr': 0.1,
    },
    {
        'name': 'collision_probability_synthetic_npd_006',
        'r1': [504.77306169219827, -874.2925891429867, 7005.764755937203],
        'v1': [-6.498370771676425, -3.7530720236866104, 0.007427568053290112],
        'cov1_rtn': [0.0004, 0.0, 0.01, 0.0, 0.0, 0.0001],
        'r2': [504.78021131211716, -874.3049592504818, 7005.863729991255],
        'v2': [-3.770862158159543, -6.46648702598876, -0.5287319909097317],
        'cov2_rtn': [999999.9326148671, 66.98607928961111, 0.2649104124800548, 249.99545131168975, 1.021033358968612, 3.791973720442615],
        'hbr': 0.02,
    },
    {
        'name': 'collision_probability_synthetic_frisbee_max_007',
        'r1': [-5163.675347519388, -3548.156404199443, 3131.414170902977],
        'v1': [4.537777742307794, -1.462317958510397, 5.843368129939845],
        'cov1_rtn': [0.0016, 0.003, 0.0625, -0.00012, 0.0, 0.0009],
        'r2': [-5164.294293703191, -3547.6299259655975, 3133.6383899323987],
        'v2': [-3.4022182849123626, 6.499182248870712, 1.7493527978391024],
        'cov2_rtn': None,
        'hbr': 0.01,
    },
]


def encounters():
    out = []
    for spec in ENCOUNTERS:
        e = dict(spec)
        e['exact'] = encounter_reference(e)
        out.append(e)
    return out


# ---- The reproductions of #610, #611, #612 (encounter plane, m and m^2) ----

def plane_row(name, miss, cov, radius):
    return {'name': name, 'miss': [float(miss[0]), float(miss[1])], 'cov': [float(c) for c in cov], 'hbr': float(radius),
            'exact': pc_of(miss, cov, radius)}


def rotated(l1, l2, degrees):
    th = mpmath.radians(mpf(degrees))
    a = mpf(l1) * cos(th) ** 2 + mpf(l2) * sin(th) ** 2
    b = (mpf(l1) - mpf(l2)) * cos(th) * sin(th)
    c = mpf(l1) * sin(th) ** 2 + mpf(l2) * cos(th) ** 2
    return [float(a), float(b), float(c)], [float(100 * cos(th)), float(100 * sin(th))]


def reproductions():
    rows = []
    for x in (30.0, 99.5):
        rows.append(dict(plane_row(f'#610 diag(1, 0.25), R 100, miss ({x}, 0)', (x, 0.0), (1.0, 0.0, 0.25), 100.0), issue='610'))
    for x in (4.0, 5.0, 16.0, 19.0, 20.0):
        rows.append(dict(plane_row(f'#610 diag(0.25, 0.09), R 20, miss ({x}, 0)', (x, 0.0), (0.25, 0.0, 0.09), 20.0), issue='610'))
    for x in (0.0, 2000.0, 4000.0, 5000.0, 5200.0, 5400.0, 6000.0, 6500.0, 7000.0):
        rows.append(dict(plane_row(f'#611 diag(1e6, 4e4), R 10, miss ({x}, 0)', (x, 0.0), (1.0e6, 0.0, 4.0e4), 10.0), issue='611'))
    for l2, degrees in ((1e-4, 0), (1e-6, 0), (0.0, 0), (-1.0, 0), (1e-4, 30), (1e-6, 30), (0.0, 30), (-1.0, 30), (1e-2, 30)):
        if degrees == 0:
            cov, miss = [1.0e12, 0.0, float(l2)], [100.0, 0.0]
        else:
            cov, miss = rotated(mpf('1e12'), mpf(l2), degrees)
        rows.append(dict(plane_row(f'#612 lambda1 1e12, lambda2 {l2}, axis {degrees} deg, R 20', miss, cov, 20.0), issue='612'))
    return rows


# ---- IO's published checks (NASA CARA Pc2D_Foster_UnitTest.m, Frisbee AAS 15-717) ----

def scaled(rows, scale):
    return [[float(v) * scale for v in row] for row in rows]


def cara_rows():
    out = []
    omitron = {
        'name': 'Omitron case 01 (CARA Pc2D_Foster_UnitTest.m), published 2.70601573490125e-05',
        'r1': [378.39559, 4305.721887, 5752.767554], 'v1': [2.360800244, 5.580331936, -4.322349039],
        'cov1': [[44.5757544811362, 81.6751751052616, -67.8687662707124],
                 [81.6751751052616, 158.4534029561630, -128.6169216448570],
                 [-67.8687662707124, -128.6169216448580, 105.4905425627010]],
        'r2': [374.5180598, 4307.560983, 5751.130418], 'v2': [-5.388125081, -3.946827739, 3.322820358],
        'cov2': [[2.31067077720423, 1.69905293875632, -1.41701645776610],
                 [1.69905293875632, 1.24957388457206, -1.04174164279599],
                 [-1.41701645776610, -1.04174164279599, 0.869260558223714]],
        'frame': 'inertial', 'hbr': 0.020}
    alfano = {
        'name': 'Alfano case 03 (CARA Pc2D_Foster_UnitTest.m), published 1.00351176e-01',
        'r1': [153.951475, 41874.153995, 0.0], 'v1': [3.066874624, -0.011411025, 0.0],
        'cov1': scaled([[1.988980036134080e+01, -3.524149328959712e+02, 0.0],
                        [-3.524149328959712e+02, 6.496747606851101e+03, 0.0],
                        [0.0, 0.0, 1.205040573210700e+00]], 1.0 / 1e6),
        'r2': [153.951973, 41874.156745, 0.002752], 'v2': [3.066864623, -0.000044999, -0.011356027],
        'cov2': scaled([[1.746930568576392e+01, -3.305057350225742e+02, -1.279563505801461e-15],
                        [-3.305057350225742e+02, 6.542324010830698e+03, -4.449721840993348e-13],
                        [-1.279563505801461e-15, -4.449721840993348e-13, 1.177810899317289e+00]], 1.0 / 1e6),
        'frame': 'rtn', 'hbr': 0.015}
    for case in (omitron, alfano):
        r1, v1, r2, v2 = vec(case['r1']), vec(case['v1']), vec(case['r2']), vec(case['v2'])
        c1, c2 = matrix(case['cov1']), matrix(case['cov2'])
        if case['frame'] == 'rtn':
            m1, m2 = rtn_rows(r1, v1), rtn_rows(r2, v2)
            c1, c2 = m1.T * c1 * m1, m2.T * c2 * m2
        miss, cov = project(r2 - r1, v2 - v1, c1 + c2)
        out.append({'name': case['name'], 'exact': pc_of(miss, cov, case['hbr'])})
    for radius in (5.0, 10.0, 20.0, 25.0):
        out.append({'name': f'Frisbee AAS 15-717 table 1, R {radius}', 'hbr': radius,
                    'exact': pc_of((1000.0, 200.0), (722500.0, 0.0, 2500.0), radius, maximum=True)})
    return out


# ---- erf and erfc at each branch of fdlibm ----

ERF_POINTS = [1e-18, 1e-10, 0.1, 0.3, 0.5, 0.8, 0.9, 1.0, 1.2, 1.5, 2.0, 2.5, 2.9, 3.5, 5.0, 5.9, 6.5, 10.0, 20.0,
              27.0, 27.5, 28.5]


def erf_rows():
    return [{'x': x, 'erf': digits(erf(mpf(x))), 'erfc': digits(erfc(mpf(x)))} for x in ERF_POINTS]


def main():
    header = {'generated_by': 'tools/collision_probability/collision_probability_reference.py, mpmath ' +
              mpmath.__version__ + ' at 50 digits', 'units': 'encounters: km, km/s, km^2; reproductions: m, m^2'}
    doc = dict(header)
    doc['encounters'] = encounters()
    doc['reproductions'] = reproductions()
    doc['cara'] = cara_rows()
    doc['erf'] = erf_rows()
    text = json.dumps(doc, indent=1)
    json.loads(text)
    print(text)


if __name__ == '__main__':
    main()
