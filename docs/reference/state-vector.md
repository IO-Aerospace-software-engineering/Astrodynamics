# StateVector

`StateVector` stores Cartesian position and velocity in a frame around an observer body.

## Constructor

```csharp
// Inputs: earth (CelestialBody), epoch (Time)
var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 8000.0, 0.0),
    earth, epoch, Frame.ICRF);
```

An optional 6x6 covariance matrix can be attached:

```csharp
// Inputs: position and velocity (Vector3), earth (CelestialBody), epoch (Time), covarianceMatrix (Matrix)
var sv = new StateVector(position, velocity, earth, epoch, Frame.ICRF, covarianceMatrix);
```

The covariance is 6x6, in `m²`, `m²/s` and `m²/s²`, ordered `X, Y, Z, X_DOT, Y_DOT, Z_DOT` in the frame of the
state.

| Operation | What happens to the covariance |
|-----------|--------------------------------|
| `ToFrame(frame)` | Transformed with the Jacobian of the state transformation, `P' = J P Jᵀ`, symmetrized. Towards a rotating frame (ITRF93, TIRS, body-fixed frames) `J` includes the angular velocity term, so the velocity covariance also depends on the position covariance. See [Frames](frames.md#states-and-covariance). |
| `RotateCovarianceToRtn(covariance)` / `RotateCovarianceFromRtn(covariance)` | Pure rotation `diag(R, R)` into or out of RTN; the rotation rate of the RTN frame is not applied. This is the convention of the conjunction analysis and of the CDM export. A rectilinear state (r × v = 0) has no RTN frame: they throw `InvalidOperationException` (#687). |
| `ToStateVector(epoch)`, `ToStateVector(trueAnomaly)` | At the instant of the state's own epoch, in any time frame, the state itself, position, velocity and covariance unchanged, tagged as `epoch` is (#551). At any other instant, the two-body motion without a covariance: moving the covariance would take the state transition matrix, which this conversion does not compute; a covariance at another epoch is the one of a [covariance propagation](covariance-propagation.md). |
| `Matrix.TransformCovariance(covariance, rotation)` | Pure rotation `diag(R, R)`, valid only between inertial frames. |
| `WithRemediatedCovariance()` | The covariance made positive semi-definite by eigenvalue clipping when rounding made it indefinite, before a propagation ([below](#covariance-indefinite-from-rounding)) |
| A propagation | Not propagated unless the covariance propagation is activated ([Covariance Propagation](covariance-propagation.md)): without it, the covariance stays on the initial orbital parameters, the propagated states carry none, and no partial derivative is evaluated. With it, in `CovariancePropagationMode.Covariance`, each output state carries its propagated covariance. |

## Covariance Indefinite From Rounding

A propagation in covariance mode rejects an initial covariance that is not positive semi-definite. When rounding made it
so (a correlation of −1 written as −1.0001, for example), `WithRemediatedCovariance()` returns a
`CovarianceRemediation`:

| Member | Meaning |
|--------|---------|
| `State` | The state itself when its covariance is accepted, otherwise a new state with the same position, velocity, observer, epoch and frame and the remediated covariance |
| `Remediated` | Whether the covariance was changed |
| `SmallestCorrelationEigenvalue` | The smallest eigenvalue of the correlation matrix of the covariance given: negative when it was remediated |

It throws `InvalidOperationException` for a state without a 6×6 covariance, a value that is not finite, a negative
variance or an asymmetry that rounding does not explain. The clipping, its tolerances and its limits are those of the
conjunction analysis; see
[Covariance Propagation](covariance-propagation.md#an-initial-covariance-indefinite-from-rounding).

## Properties

| Property | Description |
|----------|-------------|
| `Position` | Position vector (m) |
| `Velocity` | Velocity vector (m/s) |
| `Observer` | Central body |
| `Epoch` | Time of state |
| `Frame` | Reference frame |
| `Covariance` | `Matrix?`: the 6×6 covariance, null when the state carries none; a copy of the one the state holds, which its constructor copied too (#722) |

Two states are equal (`==`, `Equals`, through `OrbitalParameters` or not) when their position, velocity, observer (by
NAIF id), epoch (an instant, whatever its time frame, #512) and frame are; the covariance does not take part. A state is
never equal to a set of elements, its own included ([Keplerian Elements](keplerian-elements.md#equality)).

## Orbital Element Methods

| Method | Description |
|--------|-------------|
| `SemiMajorAxis()` | Semi-major axis (m) |
| `Eccentricity()` | Eccentricity |
| `Inclination()` | Inclination (rad) |
| `AscendingNode()` | RAAN (rad) |
| `ArgumentOfPeriapsis()` | Argument of periapsis (rad) |
| `TrueAnomaly()` | True anomaly (rad) |
| `MeanAnomaly()` | Mean anomaly (rad) |
| `EccentricAnomaly()` | Eccentric anomaly (rad) |
| `Period()` | Orbital period |
| `MeanMotion()` | Mean motion (rad/s) |
| `SpecificOrbitalEnergy()` | Vis-viva energy (m²/s²) |
| `SpecificAngularMomentum()` | Angular momentum vector |
| `EccentricityVector()` | Eccentricity vector |
| `PerigeeVector()` / `ApogeeVector()` | Apse position vectors, a(1 − e) P̂ and −a(1 + e) P̂: P̂ the perifocal axis of the elements (Ω, ω, i), defined at e = 0 (at ω from the node); for a state, the part of its eccentricity vector in its orbit plane, normalized, or its unit node vector when that part is zero, x̂ for an equatorial orbit (#686); for a TLE, the axis of its osculating elements at its epoch; mean elements throw, as `ToStateVector()` and `EccentricityVector()` do |
| `PerigeeVelocity()` / `ApogeeVelocity()` | Apse velocities (m/s) |

A rectilinear state (r × v = 0: a radial motion, a zero velocity, the centre) or a non-finite one has no orbit plane and
no RTN frame, and every member that reads the plane throws `InvalidOperationException` (#687; SPICE `oscltx` signals
`SPICE(DEGENERATECASE)` on the same states):

- the RTN frame: `CreateRtnRotation()`, `ToRtn`, `FromRtn`, `RotateCovarianceToRtn`, `RotateCovarianceFromRtn` (a matrix
  with zero rows before 11.0.0);
- the plane: `Inclination()`, `AscendingNode()`, `ArgumentOfPeriapsis()`, `AscendingNodeVector()`,
  `DescendingNodeVector()`, `TrueLongitude()`, `TrueLongitude(Time)`, `MeanLongitude()` (0, 0, π, x̂, −x̂, 0 and a
  number, or NaN, before);
- the elements and the two-body motion: `ToKeplerianElements()`, `ToKeplerianElements(Time)`, `ToEquinoctial()`,
  `AtEpoch(Time)`, `TrueAnomaly(Time)` and `ToStateVector(Time)` at another instant, the two-body ephemeris of a
  spacecraft that is not propagated (equatorial elements, NaN or a NaN state before);
- at the centre (r = 0), whose eccentricity vector is zero, so that its anomalies count from the node and its periapsis
  lies along it: also `TrueAnomaly()`, `MeanAnomaly()`, `EccentricAnomaly()`, `EpochAtMeanAnomaly(double)`,
  `ToStateVector(double)`, `PerigeeVector()`, `ApogeeVector()`, `PerigeeVelocity()` and `ApogeeVelocity()` (0, the
  epoch, the state itself, the zero vector or NaN before); and `MeanAnomaly()` of a non-finite state (the message of an
  empty `Nullable` before).

A moving state off the centre keeps `Eccentricity()` (1) and `TrueAnomaly()` (π), the limits of every nearby orbit, and
the members that read no plane. Only r × v = 0 exactly throws: a state near it is ill-conditioned, its plane that of a
small angular momentum.

## Conversion Methods

| Method | Description |
|--------|-------------|
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `ToEquatorial()` | Right ascension and declination in ICRF, whatever the frame of the state (rotated at its epoch, #680), and the distance |
| `ToFrame(Frame)` | Transform to different reference frame |
| `RelativeTo(ILocalizable, Aberration)` | Transform to different center |
| `Inverse()` | Invert position and velocity |
| `ToTLE(TLE.Configuration)` | Convert to TLE format |

## See Also

- [Keplerian Elements](keplerian-elements.md)
- [Orbit Creation](../tutorials/orbit-creation.md)
