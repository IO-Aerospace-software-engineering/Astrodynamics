"""The results a libm within one ulp can give for TimeToRadius on the hyperbola of #627 (the second review of #627).

TimeToRadius computes, in doubles, c = (p/r - 1)/e, nu = acos(c), tan(nu/2), x = sqrt((e-1)/(e+1)) tan(nu/2),
H = 2 atanh(x), M = e sinh(H) - H and t = M/n. .NET's Math.Acos, Tan, Atanh and Sinh call the platform's libm (glibc on
Linux, UCRT on Windows, Apple's libm on macOS), which promise an error below one ulp, not the correctly rounded result:
each call may return either double that brackets the exact value. The other operations are IEEE (sqrt and the four
operations correctly rounded). Far out on the hyperbola, near the asymptote, one ulp of nu moves t by several 1e-2 s, so
the result depends on the platform's libm and a bound measured on one platform can fail on another.

This script runs the chain with every choice of the two neighbours for each of the four calls, and c itself and its two
neighbours (the rounding upstream, p from the angular momentum), and gives for each radius the worst distance of t from
the closed form at 50 digits (the reference of time_to_radius_reference.py); the first column is the chain with the
correctly rounded calls (glibc's on these values). The bound of the test is twice that worst value.

Run: python3 -I time_to_radius_libm_envelope.py (needs mpmath).
"""
import math

import mpmath as mp

mp.mp.dps = 50
MU = 1.3271244004127939e20  # GM of the Sun, gm_de440.tpc, as the library reads it
A, E = -1.5e11, 2.0
P = A * (1 - E * E)


def neighbours(exact):
    """The doubles that bracket an exact value: the value itself when it is a double."""
    low = float(exact)
    if mp.mpf(low) > exact:
        low = math.nextafter(low, -math.inf)
    return [low] if mp.mpf(low) == exact else [low, math.nextafter(low, math.inf)]


def closed_form(r):
    h = mp.acosh((1 + mp.mpf(r) / abs(mp.mpf(A))) / E)
    return (E * mp.sinh(h) - h) / mp.sqrt(mp.mpf(MU) / abs(mp.mpf(A)) ** 3)


def chain(atanh_x, sinh_h):
    return (E * sinh_h - 2 * atanh_x) / math.sqrt(MU / abs(A) ** 3)


for r in (1e12, 1e13, 1e14, 1e15):
    reference = closed_form(r)
    c0 = max(-1.0, min(1.0, (P / r - 1) / E))
    rounded = None
    worst = 0.0
    for c in (math.nextafter(c0, -1.0), c0, math.nextafter(c0, 1.0)):
        for nu in neighbours(mp.acos(mp.mpf(c))):
            for t in neighbours(mp.tan(mp.mpf(nu / 2))):
                x = math.sqrt((E - 1) / (E + 1)) * t
                for h2 in neighbours(mp.atanh(mp.mpf(x))):
                    for sh in neighbours(mp.sinh(mp.mpf(2 * h2))):
                        worst = max(worst, abs(float(mp.mpf(chain(h2, sh)) - reference)))
        if c == c0:
            nu = float(mp.acos(mp.mpf(c)))
            t = float(mp.tan(mp.mpf(nu / 2)))
            h2 = float(mp.atanh(mp.mpf(math.sqrt((E - 1) / (E + 1)) * t)))
            rounded = float(mp.mpf(chain(h2, float(mp.sinh(mp.mpf(2 * h2))))) - reference)
    print(f'r = {r:.0e} m: correctly rounded {rounded:+.3e} s, worst within one ulp {worst:.3e} s')
