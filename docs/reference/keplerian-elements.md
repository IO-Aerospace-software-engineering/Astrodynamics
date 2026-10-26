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
| `PerigeeRadius()` / `ApogeeRadius()` | Apse radii (m) |
| `IsCircular()` / `IsElliptical()` / `IsParabolic()` / `IsHyperbolic()` | Orbit type checks |

## EquinoctialElements

`EquinoctialElements` has no singularity for circular or equatorial orbits: `ToStateVector()` computes the state from the elements directly, and `ToKeplerianElements()` takes ω = 0 for an exactly circular orbit (f = g = 0) and Ω = 0 for an exactly equatorial one (h = k = 0).

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Convert to state vector |
| `ToKeplerianElements()` | Convert to Keplerian |
| `EquinoctialEx()` / `EquinoctialEy()` | Eccentricity components |
| `Hx()` / `Hy()` | Inclination components |
| `Lv()` | True longitude (rad) |

## See Also

- [StateVector](state-vector.md)
- [TLE](tle.md)
- [Orbit Creation](../tutorials/orbit-creation.md)
