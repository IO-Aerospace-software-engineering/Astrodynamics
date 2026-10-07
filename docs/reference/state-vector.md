# StateVector

`StateVector` stores Cartesian position and velocity in a frame around an observer body.

## Constructor

```csharp
var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 8000.0, 0.0),
    earth, epoch, Frames.Frame.ICRF);
```

An optional 6x6 covariance matrix can be attached:

```csharp
var sv = new StateVector(position, velocity, earth, epoch, Frame.ICRF, covarianceMatrix);
```

The covariance is 6x6, in `m²`, `m²/s` and `m²/s²`, ordered `X, Y, Z, X_DOT, Y_DOT, Z_DOT` in the frame of the
state.

| Operation | What happens to the covariance |
|-----------|--------------------------------|
| `ToFrame(frame)` | Transformed with the Jacobian of the state transformation, `P' = J P Jᵀ`, symmetrized. Towards a rotating frame (ITRF93, TIRS, body-fixed frames) `J` includes the angular velocity term, so the velocity covariance also depends on the position covariance. See [Frames](frames.md#states-and-covariance). |
| `RotateCovarianceToRtn(covariance)` / `RotateCovarianceFromRtn(covariance)` | Pure rotation `diag(R, R)` into or out of RTN; the rotation rate of the RTN frame is not applied. This is the convention of the conjunction analysis and of the CDM export. |
| `Matrix.TransformCovariance(covariance, rotation)` | Pure rotation `diag(R, R)`, valid only between inertial frames. |

## Properties

| Property | Description |
|----------|-------------|
| `Position` | Position vector (m) |
| `Velocity` | Velocity vector (m/s) |
| `Observer` | Central body |
| `Epoch` | Time of state |
| `Frame` | Reference frame |

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
| `PerigeeVector()` / `ApogeeVector()` | Apse position vectors |
| `PerigeeVelocity()` / `ApogeeVelocity()` | Apse velocities (m/s) |

## Conversion Methods

| Method | Description |
|--------|-------------|
| `ToKeplerianElements()` | Convert to Keplerian elements |
| `ToEquinoctial()` | Convert to equinoctial elements |
| `ToEquatorial()` | Convert to equatorial coordinates |
| `ToFrame(Frame)` | Transform to different reference frame |
| `RelativeTo(ILocalizable, Aberration)` | Transform to different center |
| `Inverse()` | Invert position and velocity |
| `ToTLE(TLE.Configuration)` | Convert to TLE format |

## See Also

- [Keplerian Elements](keplerian-elements.md)
- [Orbit Creation](../tutorials/orbit-creation.md)
