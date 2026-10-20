# Force Models

Force models compute accelerations applied to a spacecraft during numerical propagation. Gravity, drag and direct solar radiation pressure cover the standard perturbations; albedo and thermal radiation pressure add the radiation emitted or reflected by the central body.

## Gravity, Drag And Solar Radiation

### GravitationalAcceleration

Central-body two-body gravity. Always present as the primary force.

```csharp
new GravitationalAcceleration(centralBody)
```

### ThirdBodyPerturbation

Third-body gravitational perturbation using Battin's numerically stable formula to avoid catastrophic cancellation when the perturbing body is distant.

```csharp
new ThirdBodyPerturbation(perturbingBody, centralBody)
```

### AtmosphericDrag

Atmosphere-relative velocity with co-rotation of the central body's atmosphere.

```csharp
new AtmosphericDrag(spacecraft, celestialBody)
```

| Property | Default | Description |
|----------|---------|-------------|
| `DragCoefficient` (Cd) | 2.2 | Set on the `Spacecraft` constructor |
| `SectionalArea` | -- | Cross-sectional area (m2) |

### SolarRadiationPressure

Cannonball SRP model with configurable reflectivity coefficient and continuous shadow fraction for penumbra transitions.

```csharp
new SolarRadiationPressure(spacecraft, occultingBodies)
```

The occulting bodies are the bodies that can eclipse the Sun. The Sun itself is ignored if it is among them, so the
propagator can pass its whole body list, Sun included.

| Property | Default | Description |
|----------|---------|-------------|
| `SolarRadiationCoeff` (Cr) | 1.0 | Set on the `Spacecraft` constructor |

!!! note "Geometric positions"
    SRP, albedo and thermal pressure read the geometric positions (`Aberration.None`) of the Sun and of the occulting,
    reflecting or emitting bodies, as the cannonball model of Montenbruck and Gill, *Satellite Orbits*, §3.4, does. The
    acceleration therefore does not depend on the observer the state is expressed relative to: the same physical state
    relative to the Earth or to the Sun gives the same acceleration. The light time and the aberration from the
    spacecraft, of order v/c, are neglected: the shadow axis of the Earth is off by the annual aberration, about 20",
    some 680 m at 7000 km from its centre (about 0.1 s on an eclipse entry in LEO). The positions do not depend on the
    state of the spacecraft, so these forces do not depend on its velocity.

### GeopotentialGravitationalField

EGM2008 spherical harmonics up to degree 70 with geodesy-normalized Legendre functions (no Condon-Shortley phase).

| Degree | Use Case |
|--------|----------|
| 2 | Fast J2-only preliminary analysis |
| 10 | Good LEO production baseline |
| 20-30 | Higher-fidelity geodesy and mission analysis |
| 70 | Maximum supported EGM2008 fidelity |

!!! warning "Thread Safety"
    `GeopotentialGravitationalField` is **not** thread-safe. Create one `CelestialBody` with geopotential per propagation task.

## Radiation From The Central Body

### AlbedoRadiationPressure

Models sunlight reflected off a celestial body using a Lambertian sphere approximation. The acceleration is:

```
a = (L_sun / 4*pi*c) * alpha * Cr * (A/m) * (2/3*pi) * (R_body / r_sun)^2 * f(phi) / r_sc^2
```

Where `f(phi) = (pi - phi) * cos(phi) + sin(phi)` is the Lambertian visibility function. Acceleration is zero on the dark side.

```csharp
new AlbedoRadiationPressure(spacecraft, reflectingBody)
```

**Configuration**: Set `albedo` on the `CelestialBody` constructor, then call `IncludeAlbedo()` on the builder.

| Body | Bond Albedo |
|------|-------------|
| Earth | 0.306 |
| Moon | 0.12 |
| Venus | 0.77 |
| Mars | 0.25 |

### ThermalRadiationPressure

Models infrared radiation emitted by a celestial body using an isotropic emitter model. Always present regardless of Sun geometry or eclipse state.

```
a = Cr * (A/m) * epsilon * sigma * T_eff^4 * R^2 / (c * r^2)
```

```csharp
new ThermalRadiationPressure(spacecraft, emittingBody)
```

**Configuration**: Set `thermalEffectiveTemperature` (Kelvin) and `thermalEmissivity` on the `CelestialBody` constructor, then call `IncludeThermalRadiation()` on the builder.

| Body | T_eff (K) | Emissivity |
|------|-----------|------------|
| Earth | 254 | 0.95 |
| Moon | 270 | 0.95 |

## Atmosphere Models

| Model | Description |
|-------|-------------|
| `IAtmosphericModel` | Common interface for density queries |
| `EarthStandardAtmosphere` | Low-altitude analytical Earth model |
| `MarsStandardAtmosphere` | Simple Mars atmospheric model |
| `Nrlmsise00Model` | Time-varying Earth model for drag analysis (thread-safe) |

## Example

```csharp
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
```

## Partial Derivatives

When the state transition matrix is integrated (RK7(8)), each force also gives the partial derivatives of its
acceleration with respect to the state and to the drag and reflectivity coefficients
([Covariance Propagation](covariance-propagation.md)). They do not change the trajectory. The columns of the sensitivity
Ψ follow the forces: Cd with `AtmosphericDrag`, Cr with a radiation force.

| Force | `∂a/∂r`, `∂a/∂v` | `∂a/∂Cd`, `∂a/∂Cr` |
|-------|------------------|--------------------|
| `GravitationalAcceleration` | Analytic: point mass; geopotential by the Cunningham functions | None |
| `ThirdBodyPerturbation` | Analytic | None |
| `AtmosphericDrag` | Analytic in the velocity and the co-rotation; density gradient by central differences of the density model alone, one-sided on the side of the state where the stencil straddles an altitude at which the density of NRLMSISE-00 jumps (the cutoffs of its mixing, 160 to 450 km) | Exact (`∂a/∂Cd`) |
| `SolarRadiationPressure` | Analytic outside the penumbra; central differences of the acceleration in the penumbra, so that the derivative of the shadow fraction is included | Exact (`∂a/∂Cr`) |
| `ThermalRadiationPressure` | Analytic | Exact (`∂a/∂Cr`) |
| `AlbedoRadiationPressure` | Central differences of the acceleration | Exact (`∂a/∂Cr`) |
| A force written outside the library | Central differences of `Apply` | None |

`Cr` is shared by SRP, albedo and thermal pressure, so its sensitivity sums the three. The formulas, their references,
the finite-difference steps and the measured accuracy are in the
[covariance propagation provenance](covariance-propagation-provenance.md).

## See Also

- [Propagator Builder](propagator-builder.md)
- [Propagators](propagators.md)
- [Integrators](integrators.md)
