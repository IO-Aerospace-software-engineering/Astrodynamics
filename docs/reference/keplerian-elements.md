# Keplerian Elements

`KeplerianElements` represents classical orbital elements in osculating or mean form.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `KeplerianElements(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly, observer, epoch, frame)` | Create from elements, outside [the band](#the-near-parabolic-band) |
| `KeplerianElements(..., OrbitalElementsType)` | Create with explicit element type |

One meaning per constructor (#668, R23): the constructor takes the six elements with the mean anomaly, a parabola is
built from its perigee radius by `Parabolic`, and elements given with their true anomaly by `FromTrueAnomaly`. Before
11.0.0 the constructor took the optional `trueAnomaly`, `period` and `perigeeRadius`, stored unchecked beside the
elements (a true anomaly 6177.2 km off the mean anomaly's state was accepted), and its RAAN parameter was spelled
`rigthAscendingNode`.

Every element is finite (#726): the constructor throws `ArgumentException` naming the parameter for a semi-major axis
NaN or ±∞, and for an angle (i, Ω, ω, M) that is not finite; `ArgumentOutOfRangeException` (`eccentricity`) for an
eccentricity not finite or negative (#476), and `ArgumentException` (`eccentricity`) within 1e-6 of e = 1, which
`Parabolic` builds. Outside the band the semi-major axis has the sign of the conic, a (1 − e) > 0: a > 0 for e < 1 and
a < 0 for e > 1, a = 0 refused, or `ArgumentException` (`semiMajorAxis`, its message naming a and e; #738: before
11.0.0, accepted, with a NaN velocity and an `AtEpoch` that named the mean anomaly). `FromOMM` checks the same, its mean
motion finite and above 0.

### Factory Methods

| Method | Description |
|--------|-------------|
| `FromTrueAnomaly(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, trueAnomaly, observer, epoch, frame)` | Osculating elements of the constructor's conics whose true anomaly is ν: M(ν) from the closed-form eccentric anomaly (`OrbitalParameters.TrueAnomalyToMeanAnomaly`), `TrueAnomaly()` ν reduced to [0, 2π), the range of the true anomaly computed from M (ν as given when in it), the state and the equinoctial elements computed from ν as given (its reduction would lose up to half an ulp of 2π, which the state near an apsis or an asymptote amplifies); `ArgumentException` (`trueAnomaly`) for a ν not finite, `ArgumentOutOfRangeException` (`trueAnomaly`) for a ν of a hyperbola whose reduction to (−π, π] is on or beyond its asymptotes |
| `Parabolic(perigeeRadius, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly, observer, epoch, frame, eccentricity = 1.0)` | Osculating elements of a parabola, or of an orbit within 1e-6 of e = 1, from its perigee radius q: a = q / (1 − e), +∞ at e = 1, the mean anomaly Barker's; `ArgumentOutOfRangeException` (`perigeeRadius`) for a q not finite and above 0, (`eccentricity`) for an e outside the band |
| `FromOMM(meanMotion, e, i, raan, aop, m, observer, epoch, frame)` | Create mean elements from OMM data (units: rev/day, degrees) |

```csharp
// Inputs: earth (CelestialBody), epoch (Time)
var transfer = KeplerianElements.FromTrueAnomaly(24396e3, 0.7283, 0.1, 0.2, 0.3, Constants.Deg2Rad * 30.0, earth, epoch, Frame.ICRF);
var flyby = KeplerianElements.Parabolic(6800e3, 0.5, 0.3, 0.2, 0.0, earth, epoch, Frame.ICRF);   // a = +Infinity
Console.WriteLine($"{transfer.TrueAnomaly()} rad, {transfer.MeanAnomaly()} rad; q = {flyby.PerigeeRadius()} m");
```

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
| `ToStateVector()` | Convert to state vector at epoch: the perifocal state of the conic, p / (1 + e cos ν) and √(μ/p)(−sin ν, e + cos ν), with p = (a (1 − e))(1 + e) for 0.5 ≤ e ≤ 2 and the half-angle forms (1 − e) + 2e cos²(ν/2) and 2 cos²(ν/2) − (1 − e) for 0.5 ≤ e ≤ 1.2, which do not cancel near e = 1, the apoapsis and the asymptote (#472, decided by S. Guillet, 2026-11-18; before 11.0.0, up to 8.1e-11 off there); unchanged below e = 0.5 and above e = 2 |
| `ToStateVector(Time)` | Convert at specified epoch |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `AtEpoch(Time)` | Two-body propagation to new epoch; within 1e-6 of e = 1, the conic's motion in universal variables, Barker's law at e = 1 ([the band](#the-near-parabolic-band)) |
| `EccentricAnomaly()` / `TrueAnomaly()` | From M, on an ellipse or a hyperbola by Kepler's equation: Newton's method from an upper bound of the root, to its rounding floor, at most 50 iterations; a non-finite M or e throws `InvalidOperationException` (#453). For 0.5 ≤ e ≤ 2 the residual is (1 − e) E + e (E − sin E) − M, (e − 1) H + e (sinh H − H) − M on the hyperbola, which does not cancel near the periapsis at e → 1: E within 2 ulp of the root (#484; before 11.0.0, up to 9.3e5 ulp at e = 0.999999); `StateVector.MeanAnomaly()` and `TrueAnomalyToMeanAnomaly` take the same forms. On a parabola, Barker's closed form |
| `Period()` | Orbital period of an ellipse, 2π √(a³/μ) (or the `period` given); a parabola or a hyperbola (a not finite and above 0) has none and throws `InvalidOperationException` naming e and a, whatever period was given (#663) |
| `PerigeeRadius()` / `ApogeeVector().Magnitude()` | Apse radii (m) |
| `PerigeeVector()` / `ApogeeVector()` | Apse position vectors, a(1 − e) P̂ and −a(1 + e) P̂, P̂ the perifocal axis of Ω, ω and i, defined at e = 0 (#686); within 1e-6 of e = 1 the periapsis is q P̂, q the perigee radius, and `PerigeeVelocity()` √(2μ/q) for a parabola (#737: NaN before 11.0.0); an open orbit (a not finite and above 0) has no apoapsis, and `ApogeeVector()` and `ApogeeVelocity()` throw `InvalidOperationException` naming e and a, as `Period()` (#737); mean elements have no position and throw `InvalidOperationException`, as `ToStateVector()` does: read `PerigeeRadius()` |
| `IsCircular()` / `IsElliptical()` / `IsParabolic()` / `IsHyperbolic()` | Orbit type checks |

## The near-parabolic band

Within 1e-6 of e = 1 (`IsParabolic()`, |e − 1| < 1e-6) the library takes the anomalies of a parabola, Barker's equation
(#368): the eccentric anomaly is D = tan(ν/2) and the mean anomaly D + D³/3, on both sides of e = 1. The two-body
motion (`AtEpoch`, `ToStateVector(Time)`, `ToKeplerianElements(Time)`, `EpochAtMeanAnomaly`, `TimeToRadius`) is the
exact motion of the conic of e and q in universal variables (#753, decided by S. Guillet, 2026-11-18; below). The
geometry is that of the conic (#459, option 1, decided by S. Guillet, 2026-11-17):

- `SemiMajorAxis()` is the conic's, −μ/(2ξ) for a state (about 1.4e10 km at |e − 1| = 5e-7, negative above e = 1), and
  +Infinity for e = 1 exactly or a zero energy (ξ = 0, which rounding can give the state of a parabola whose e is off 1); `MeanMotion()` is √(μ/|a|³), Barker's √(μ/(2q³)) only when the semi-major axis is
  +Infinity. Before 11.0.0 both were the parabola's in the whole band.
- The state of elements in the band has p = q (1 + e), the conic's semi-latus rectum (2q at e = 1), and `ToEquinoctial()`
  gives P = q (1 + e) (#462).
- `KeplerianElements` in the band need the perigee radius q, not derived from a finite semi-major axis, whose
  q = a (1 − e) would carry a relative rounding of 1e-16/|1 − e| (decided by S. Guillet, 2026-11-18), with the semi-major axis of the same conic: a (1 − e) = q to
  rounding, |q/a − (1 − e)| ≤ 6 ε (1 + e) (twice the 3.0 ε (1 + e) measured over the elements the library builds there,
  from states, equinoctial elements, the two-body motion and SPICE's `oscltx`, on Linux x64, Windows x64 and macOS
  arm64), and +Infinity at e = 1
  exactly; a = +Infinity is accepted within a few ulps of e = 1 only (the state of an exact parabola, whose e rounds off
  1). Any other a, the a of another conic, throws `ArgumentException` (`semiMajorAxis`), as before 11.0.0: the semi-major
  axis, the mean motion, the period and the apoapsis come from a, the state and P from q. Migration from 10.x, where the
  band required +Infinity: give q / (1 − e).
- The internal `SpiceAPI.ConvertStateVectorToConicOrbitalElement` (public before 11.0.0) gives in the band the conic's a of `oscltx` (−μ/(2ξ)), +Infinity
  where `oscltx` gives none (e = 1 or a zero energy), and the perigee radius.

The motion of the band counts from the periapsis in universal variables, the universal anomaly χ with z = (1 − e) χ²/q
and the Stumpff functions C(z) = (1 − cos √z)/z and S(z) = (√z − sin √z)/z^(3/2) (their hyperbolic forms for z < 0):
√μ t = q χ + e χ³ S(z), solved by Newton's method from Barker's root inside its bracket, and D = tan(ν/2) =
√((1 + e)/q) χ (1 − z S)/(2 − z C), the form of CSPICE's `prop2b_c` (D. A. Vallado, *Fundamentals of Astrodynamics and
Applications*, 4th ed., 2013, algorithm 8, confirmed by S. Guillet, 2026-11-18). Nothing cancels on either side of e = 1, and at
e = 1 it is Barker's equation; the elements at the new epoch carry the true anomaly of the motion and Barker's mean
anomaly of it. Against the exact two-body motion of the conic at 60 digits (`tools/near_parabolic`), |e − 1| from 1e-12
to 9.9e-7 on both sides, ν0 from −1.5 to 2 rad, 600 s and a day later and an hour earlier, the state is within 6.8e-15
of |r| and 3.6e-15 of |v| (`NearParabolicBandTests.Issue753_*`). Before 11.0.0 the band advanced Barker's anomaly at
Barker's rate √(μ/(2q³)): 0.088 m off after 600 s at |e − 1| = 5e-7, 0.16 m at 9e-7, 0.54 m from the periapsis, and
3.1 km after a day at 9.9e-7; Kepler's equation of the conic in doubles is 2.3 to 5.8 mm off at the edges and 2.8 to
5.0 km at |e − 1| = 1e-12, where a (cos E − e) cancels. `IsParabolic()` keeps its meaning, at e = 1 exactly the motion is
Barker's law and outside the band Kepler's equation, both bit for bit as before.

In doubles the band is not symmetric: fl(1 − 1e-6) = 0.999999 is outside it (the largest eccentricity `IsElliptical()`
accepts), the next double, 0.9999990000000001, inside it; the double nearest 1.000001 is inside it. So elements given at
e = 0.999999 can come back in the band from their state, whose eccentricity carries rounding (about one time in four,
#459): their semi-major axis is then the conic's (since 11.0.0) and their anomalies Barker's.

## Equality

`==`, `!=`, `Equals` and `GetHashCode` of every orbital parameters type are value equality within one concrete type,
and compute no state (#475, decided by S. Guillet, 2026-11-17), whether the operands are typed `OrbitalParameters` or
as their type: two sets are equal when they are of the same type, with the same values (A, E, I, RAAN, AOP and M; P, F,
G, H, K and L0; and, within 1e-6 of e = 1, the perigee radius given or derived at construction, from which the state of
the band comes (#459), so that two parabolas of a = +Infinity and different perigee radii differ; position and velocity;
the two lines and the name of a TLE), the same observer (by NAIF id), epoch (an instant, #512), frame and
`ElementsType`. Equality never throws, mean elements and NaN states included (the elements refuse a non-finite value
since #476 and #726, a `StateVector` does not), `x.Equals(null)`
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
|e − 1| < 1e-6, given its perigee radius), a perigee radius given and not finite and above 0 (`perigeeRadius`), and a
perigee radius given with a finite P, which fixes q (`ArgumentException`, `perigeeRadius`; #668, R23: before 11.0.0 it
was stored unchecked, and `PerigeeRadius()` returned 1 m for P = 7000 km given 1 m).

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
