# Keplerian Elements

`KeplerianElements` represents classical orbital elements in osculating or mean form.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `KeplerianElements(a, e, i, raan, aop, m, observer, epoch, frame)` | Create from elements |
| `KeplerianElements(..., perigeeRadius)` | Create parabolic orbit: within 1e-6 of e = 1 with the a of the same conic ([the band](#the-near-parabolic-band)) |
| `KeplerianElements(..., OrbitalElementsType)` | Create with explicit element type |

Every element is finite (#726): the constructor throws `ArgumentException` naming the parameter for a semi-major axis
NaN, −∞, or +∞ outside the parabolic band, and for an angle (i, Ω, ω, M, the optional true anomaly) that is not
finite; `ArgumentOutOfRangeException` (`eccentricity`) for an eccentricity not finite or negative (#476), and
(`perigeeRadius`) for a perigee radius given and not finite and above 0. The one infinite element is the semi-major
axis of a parabola, +∞ with its perigee radius at e = 1 (to rounding, [the band](#the-near-parabolic-band)). `FromOMM` checks the same, its mean motion finite and above 0.

### Factory Methods

| Method | Description |
|--------|-------------|
| `FromOMM(meanMotion, e, i, raan, aop, m, observer, epoch, frame)` | Create mean elements from OMM data (units: rev/day, degrees) |

## Properties

| Property | Description |
|----------|-------------|
| `A` | Semi-major axis (m) |
| `E` | Eccentricity |
| `I` | Inclination (rad) |
| `RAAN` | Right ascension of ascending node (rad) |
| `AOP` | Argument of periapsis (rad) |
| `M` | Mean anomaly (rad) |
| `ElementsType` | `Osculating` or `Mean` |

## Methods

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Convert to state vector at epoch |
| `ToStateVector(Time)` | Convert at specified epoch |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `AtEpoch(Time)` | Two-body propagation to new epoch; within 1e-6 of e = 1, Barker's anomaly at Barker's rate ([the band](#the-near-parabolic-band)) |
| `EccentricAnomaly()` / `TrueAnomaly()` | From M, on an ellipse or a hyperbola by Kepler's equation: Newton's method from an upper bound of the root, to its rounding floor, at most 50 iterations; a non-finite M or e throws `InvalidOperationException` (#453). On a parabola, Barker's closed form |
| `Period()` | Orbital period of an ellipse, 2π √(a³/μ) (or the `period` given); a parabola or a hyperbola (a not finite and above 0) has none and throws `InvalidOperationException` naming e and a, whatever period was given (#663) |
| `PerigeeRadius()` / `ApogeeVector().Magnitude()` | Apse radii (m) |
| `PerigeeVector()` / `ApogeeVector()` | Apse position vectors, a(1 − e) P̂ and −a(1 + e) P̂, P̂ the perifocal axis of Ω, ω and i, defined at e = 0 (#686); mean elements have no position and throw `InvalidOperationException`, as `ToStateVector()` does: read `PerigeeRadius()` |
| `IsCircular()` / `IsElliptical()` / `IsParabolic()` / `IsHyperbolic()` | Orbit type checks |

## The near-parabolic band

Within 1e-6 of e = 1 (`IsParabolic()`, |e − 1| < 1e-6) the library takes the anomalies of a parabola, Barker's equation
(#368): the eccentric anomaly is D = tan(ν/2) and the mean anomaly D + D³/3, on both sides of e = 1, and the two-body
motion (`AtEpoch`, `ToStateVector(Time)`, `ToKeplerianElements(Time)`, `EpochAtMeanAnomaly`, `TimeToRadius`) advances
it at Barker's rate √(μ/(2q³)), q the perigee radius, where it is well conditioned (#459, option 1, decided by
S. Guillet, 2026-11-17). The geometry is that of the conic:

- `SemiMajorAxis()` is the conic's, −μ/(2ξ) for a state (about 1.4e10 km at |e − 1| = 5e-7, negative above e = 1), and
  +Infinity for e = 1 exactly or a zero energy (ξ = 0, which rounding can give the state of a parabola whose e is off 1); `MeanMotion()` is √(μ/|a|³), Barker's √(μ/(2q³)) only when the semi-major axis is
  +Infinity. Before 11.0.0 both were the parabola's in the whole band.
- The state of elements in the band has p = q (1 + e), the conic's semi-latus rectum (2q at e = 1), and `ToEquinoctial()`
  gives P = q (1 + e) (#462).
- `KeplerianElements` in the band need the perigee radius q, with the semi-major axis of the same conic: a (1 − e) = q to
  rounding, |q/a − (1 − e)| ≤ 6 ε (1 + e) (twice the 3.0 ε (1 + e) measured over the elements the library builds there,
  from states, equinoctial elements, the two-body motion and SPICE's `oscltx`; provisional), and +Infinity at e = 1
  exactly; a = +Infinity is accepted within a few ulps of e = 1 only (the state of an exact parabola, whose e rounds off
  1). Any other a, the a of another conic, throws `ArgumentException` (`semiMajorAxis`), as before 11.0.0: the semi-major
  axis, the mean motion, the period and the apoapsis come from a, the state and P from q. Migration from 10.x, where the
  band required +Infinity: give q / (1 − e).
- `SpiceAPI.ConvertStateVectorToConicOrbitalElement` gives in the band the conic's a of `oscltx` (−μ/(2ξ)), +Infinity
  where `oscltx` gives none (e = 1 or a zero energy), and the perigee radius.

The band model is Barker's anomaly on the conic: at |e − 1| = 5e-7 and q = 7000 km, 600 s after ν0 = 0.5 rad, it is
0.088 m from the exact motion of the conic, 0.16 m at 9e-7 and 0.54 m from the periapsis at 5e-7, and 1.8e-7 m at
|e − 1| = 1e-12 (60-digit references of `tools/near_parabolic`). Kepler's equation solved for the conic in doubles
(`KeplerEquation`, the solvers of #467, from the state's a, e and ν) is better at the edges of the band (2.3 to 5.8 mm at
|e − 1| = 5e-7 to 9e-7) and kilometres off near e = 1 (2.8 to 5.0 km at 1e-12), where a (cos E − e) cancels and Barker's
law holds (`NearParabolicBandTests.Issue459_KeplersEquationForTheConic_AgainstBarkersLaw`, on the same references).
Neither holds over the whole band, so Barker's law is kept for the motion (decided by S. Guillet, 2026-11-17); a motion
that holds over it (universal variables, or a band narrowed to where Barker's law is the better one) would change what
`IsParabolic()` means across the library and is not part of 11.0.0.

In doubles the band is not symmetric: fl(1 − 1e-6) = 0.999999 is outside it (the largest eccentricity `IsElliptical()`
accepts), the next double, 0.9999990000000001, inside it; the double nearest 1.000001 is inside it. So elements given at
e = 0.999999 can come back in the band from their state, whose eccentricity carries rounding (about one time in four,
#459): their semi-major axis is then the conic's (since 11.0.0) and their anomalies Barker's.

## Equality

`==`, `!=`, `Equals` and `GetHashCode` of every orbital parameters type are value equality within one concrete type,
and compute no state (#475, decided by S. Guillet, 2026-11-17), whether the operands are typed `OrbitalParameters` or
as their type: two sets are equal when they are of the same type, with the same values (A, E, I, RAAN, AOP and M; P, F,
G, H, K and L0; position and velocity; the two lines and the name of a TLE), the same observer (by NAIF id), epoch (an
instant, #512), frame and `ElementsType`. Equality never throws, mean elements and NaN elements included, `x.Equals(null)`
is false, and equal sets share a hash code, so hash sets and dictionaries keep one of equal copies. A mean set and an
osculating set of the same numbers are different orbits, and so are a set and its own `ToStateVector()`, and a TLE and
its `ToMeanKeplerianElements()`: to compare two orbits, compare their states, `x.ToStateVector() == y.ToStateVector()`
(which throws for mean elements). NaN equals NaN and +0 equals −0, as for `double.Equals`. The sets are immutable, but
for the work states of an integration, which the integrator updates in place: the state it hands to
`IEventDetector.Evaluate`, and those it hands to `ForceBase.Apply` of a force added by `Integrator.AddForce`. A detector
or a force that keeps such a state as a key keeps a copy of it, whose hash code does not change.

Before 11.0.0, through `OrbitalParameters` `==` and `Equals` compared the states of both operands: they threw for mean
elements (even against null) and e = NaN, ran SGP4 for a TLE, and found two different sets whose states were NaN equal;
the hash code held the identity of the instance, so equal copies hashed differently.

## EquinoctialElements

`EquinoctialElements` has no singularity for circular orbits (e = 0) or for prograde equatorial ones (i = 0), where the Keplerian ω and Ω are undefined: `ToStateVector()` computes the state from the elements directly, and `ToKeplerianElements()` takes ω = 0 for an exactly circular orbit (f = g = 0) and Ω = 0 for an exactly equatorial one (h = k = 0). Its only singular orbit is the retrograde equatorial one (i = 180°), where h = tan(i/2) cos Ω and k = tan(i/2) sin Ω grow without bound.

| Property | Description |
|----------|-------------|
| `P` | Semi-latus rectum a (1 − e²) (m); `ToEquinoctial()` gives q (1 + e) within 1e-6 of e = 1, q the perigee radius: 2q for a parabola, h²/μ for a state (#462) |
| `F` / `G` | Eccentricity components e cos(ω + Ω) / e sin(ω + Ω) |
| `H` / `K` | Inclination components tan(i/2) cos Ω / tan(i/2) sin Ω |
| `L0` | True longitude Ω + ω + ν (rad) |

The classical equinoctial set (GMAT's EquinoctialH, K, P and Q) is h = G, k = F, p = K and q = H.

The constructor refuses what no conic has (#481, #726): F, G, H, K and L0 not finite (`ArgumentException` naming the
parameter), P not finite and above 0 (`ArgumentOutOfRangeException`, `p`; a non-finite P only for a parabola,
|e − 1| < 1e-6, given its perigee radius), and a perigee radius given and not finite and above 0 (`perigeeRadius`).

A parabola (|e − 1| < 1e-6, e = √(F² + G²)) given a finite P and no perigee radius has its perigee radius
q = P / (1 + e), P / 2 at e = 1, derived when it is built, so that every path (`PerigeeRadius()`,
`ToKeplerianElements()`, `AtEpoch`, a propagation) reads the same q from the first call (#477). A non-finite P needs the
`perigeeRadius` argument; its state is then the conic of p = q (1 + e).

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Convert to state vector |
| `ToKeplerianElements()` | Convert to Keplerian |
| `EquinoctialEx()` / `EquinoctialEy()` | Eccentricity components, `F` / `G` |
| `Hx()` / `Hy()` | Inclination components, `H` / `K` |
| `Lv()` | True longitude (rad), `L0` |

## See Also

- [StateVector](state-vector.md)
- [TLE](tle.md)
- [Orbit Creation](../tutorials/orbit-creation.md)
