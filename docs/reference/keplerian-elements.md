# Keplerian Elements

`KeplerianElements` represents classical orbital elements in osculating or mean form.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `KeplerianElements(a, e, i, raan, aop, m, observer, epoch, frame)` | Create from elements |
| `KeplerianElements(..., perigeeRadius)` | Create parabolic orbit |
| `KeplerianElements(..., OrbitalElementsType)` | Create with explicit element type |

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
| `AtEpoch(Time)` | Two-body propagation to new epoch |
| `EccentricAnomaly()` / `TrueAnomaly()` | From M, on an ellipse or a hyperbola by Kepler's equation: Newton's method from an upper bound of the root, to its rounding floor, at most 50 iterations; a non-finite M or e throws `InvalidOperationException` (#453). On a parabola, Barker's closed form |
| `Period()` | Orbital period |
| `PerigeeRadius()` / `ApogeeVector().Magnitude()` | Apse radii (m) |
| `PerigeeVector()` / `ApogeeVector()` | Apse position vectors, a(1 − e) P̂ and −a(1 + e) P̂, P̂ the perifocal axis of Ω, ω and i, defined at e = 0 (#686); mean elements have no position and throw `InvalidOperationException`, as `ToStateVector()` does: read `PerigeeRadius()` |
| `IsCircular()` / `IsElliptical()` / `IsParabolic()` / `IsHyperbolic()` | Orbit type checks |

## Equality

`==`, `!=`, `Equals` and `GetHashCode` of every orbital parameters type are value equality within one concrete type,
and compute no state (#475, decided by S. Guillet, 2026-11-17), whether the operands are typed `OrbitalParameters` or
as their type: two sets are equal when they are of the same type, with the same values (A, E, I, RAAN, AOP and M; P, F,
G, H, K and L0; position and velocity; the two lines and the name of a TLE), the same observer (by NAIF id), epoch (an
instant, #512), frame and `ElementsType`. Equality never throws, mean elements and NaN elements included, `x.Equals(null)`
is false, and equal sets share a hash code, so hash sets and dictionaries keep one of equal copies. A mean set and an
osculating set of the same numbers are different orbits, and so are a set and its own `ToStateVector()`, and a TLE and
its `ToMeanKeplerianElements()`: to compare two orbits, compare their states, `x.ToStateVector() == y.ToStateVector()`
(which throws for mean elements). NaN equals NaN and +0 equals −0, as for `double.Equals`.

Before 11.0.0, through `OrbitalParameters` `==` and `Equals` compared the states of both operands: they threw for mean
elements (even against null) and e = NaN, ran SGP4 for a TLE, and found two different sets whose states were NaN equal;
the hash code held the identity of the instance, so equal copies hashed differently.

## EquinoctialElements

`EquinoctialElements` has no singularity for circular orbits (e = 0) or for prograde equatorial ones (i = 0), where the Keplerian ω and Ω are undefined: `ToStateVector()` computes the state from the elements directly, and `ToKeplerianElements()` takes ω = 0 for an exactly circular orbit (f = g = 0) and Ω = 0 for an exactly equatorial one (h = k = 0). Its only singular orbit is the retrograde equatorial one (i = 180°), where h = tan(i/2) cos Ω and k = tan(i/2) sin Ω grow without bound.

| Property | Description |
|----------|-------------|
| `P` | Semi-latus rectum a (1 − e²) (m) |
| `F` / `G` | Eccentricity components e cos(ω + Ω) / e sin(ω + Ω) |
| `H` / `K` | Inclination components tan(i/2) cos Ω / tan(i/2) sin Ω |
| `L0` | True longitude Ω + ω + ν (rad) |

The classical equinoctial set (GMAT's EquinoctialH, K, P and Q) is h = G, k = F, p = K and q = H.

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
