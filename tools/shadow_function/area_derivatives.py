"""The reduced derivatives of the overlap area of two discs used by the SRP partials in the penumbra (#423).

GMAT Mathematical Specification, R2026a, eq. 3.150 gives the area A of the overlap of the solar disc (angular radius
R_s) and of the disc of an occulting body (R_b) whose centres are D apart:

    A = R_s^2 acos(c1 / R_s) + R_b^2 acos((D - c1) / R_b) - D c2,
    c1 = (D^2 + R_s^2 - R_b^2) / (2 D), c2 = sqrt(R_s^2 - c1^2).

Its eq. 4.100 differentiates it term by term. SolarRadiationPressure.OverlapAreaDerivatives uses the reduced form, in
which the terms in the derivatives of c1 and c2 cancel:

    dA/dD = -2 c2, dA/dR_s = 2 R_s acos(c1 / R_s), dA/dR_b = 2 R_b atan2(c2, D - c1).

This script checks the three identities symbolically (sympy), then numerically at 50 digits over 200 random geometries
of the penumbra. Run: python3 -I area_derivatives.py (needs sympy).
"""
import random

import sympy as sp


def main():
    d, rs, rb = sp.symbols('D R_s R_b', positive=True)
    c1 = (d ** 2 + rs ** 2 - rb ** 2) / (2 * d)
    c2 = sp.sqrt(rs ** 2 - c1 ** 2)
    area = rs ** 2 * sp.acos(c1 / rs) + rb ** 2 * sp.acos((d - c1) / rb) - d * c2

    # atan2(c2, D - c1) = acos((D - c1) / R_b) inside the penumbra, since c2 = sqrt(R_b^2 - (D - c1)^2)
    reduced = {d: -2 * c2, rs: 2 * rs * sp.acos(c1 / rs), rb: 2 * rb * sp.acos((d - c1) / rb)}
    numeric = {d: -2 * c2, rs: 2 * rs * sp.acos(c1 / rs), rb: 2 * rb * sp.atan2(c2, d - c1)}

    random.seed(423)
    for variable in (d, rs, rb):
        derivative = sp.diff(area, variable)
        print(f'd A / d {variable}: symbolic difference {sp.simplify(derivative - reduced[variable])}')
        worst = 0
        for _ in range(200):
            sun = random.uniform(1e-3, 1e-2)
            body = random.uniform(0.05, 1.5)
            separation = random.uniform(abs(body - sun) * 1.0000001, (body + sun) * 0.9999999)
            values = {d: sp.Rational(separation), rs: sp.Rational(sun), rb: sp.Rational(body)}
            expected = sp.N(derivative.subs(values), 50)
            actual = sp.N(numeric[variable].subs(values), 50)
            worst = max(worst, abs(actual - expected) / abs(expected))
        print(f'd A / d {variable}: worst relative difference at 50 digits, 200 geometries {sp.N(worst, 3)}')


if __name__ == '__main__':
    main()
