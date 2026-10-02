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
