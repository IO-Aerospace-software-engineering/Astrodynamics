# Keplerian Elements

`KeplerianElements` represents classical orbital elements in osculating or mean form.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `KeplerianElements(a, e, i, raan, aop, m, observer, epoch, frame)` | Create from elements |
| `KeplerianElements(..., perigeeRadius)` | Create parabolic orbit |
| `KeplerianElements(..., OrbitalElementsType)` | Create with explicit element type |

Every element is finite (#726): the constructor throws `ArgumentException` naming the parameter for a semi-major axis
NaN, −∞, or +∞ outside the parabolic band, and for an angle (i, Ω, ω, M, the optional true anomaly) that is not
finite; `ArgumentOutOfRangeException` (`eccentricity`) for an eccentricity not finite or negative (#476), and
(`perigeeRadius`) for a perigee radius given and not finite and above 0. The one infinite element is the semi-major
axis of a parabola, +∞ with its perigee radius. `FromOMM` checks the same, its mean motion finite and above 0.

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
