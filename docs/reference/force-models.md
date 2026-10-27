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

!!! note "Shadow fraction"
    The SRP is scaled by `1 − ν`, `ν` the largest shadow fraction of the occulting bodies
    (`CelestialItem.ShadowFraction`): the fraction of the solar disc covered by the disc of the body, 0 in sunlight, 1
    in total eclipse, `b² / a²` in an annular eclipse of angular radii `a` (the Sun) and `b`. In the penumbra it is the
    sum of the two circular segments cut by the common chord, computed without cancellation (Heron's half chord in the
    form of Kahan, the half angles from atan2): within a few 1e-16 of the exact value of the angles everywhere, and
    always in [0, 1]. Before 10.2.0 a closed form of the same area cancelled near the contacts of the penumbra, by up to
    1.7e-4 in LEO, and could return values outside [0, 1]
    ([#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434)).

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

The coefficients of a model are normalized to its own GM and reference radius, which the coefficient file does not
carry. The harmonics are evaluated with them, and the central term with the GM of the SPICE kernels:

```
a = −GM_kernel r / r³ + ∇[(GM_model / r) Σ_(n≥2) (a_model / r)ⁿ Σ_m P̄_nm(sin φ) (C̄_nm cos mλ + S̄_nm sin mλ)]
```

| Body | `GeopotentialModelParameters` | GM_model, a_model |
|------|-------------------------------|-------------------|
| Earth (NAIF 399) | `(path, degree)` or `(stream, degree)` | EGM2008: 3.986004415e14 m³/s², 6378136.3 m (Pavlis et al., 2012) |
| Any other body | `(path, degree)` or `(stream, degree)` | the GM and the equatorial radius of its kernels |
| Any body | `(path, degree, gravitationalParameter, referenceRadius)` or the same with a stream | the given ones |

By default the central term keeps the GM of the kernels (DE440's for the Earth, 3.98600435507e14 m³/s²),
TDB-compatible like the time and the positions of the propagation, and the GM of every other point-mass computation of
the library. The GM of EGM2008 is a TT-compatible value; scaled to TDB-compatible units it is within 2.3e-10 of
DE440's (Klioner et al., 2010). Before 10.2.0 the harmonics took the GM and the equatorial radius of the kernels
(6378136.6 m), 7.9e-8 of the harmonics away from EGM2008
([#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410)).

```csharp
// A model other than EGM2008, in the same file format, with its own constants
var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, epoch,
    new GeopotentialModelParameters("mars_model", 20, 4.282837362069909e13, 3396000.0));
```

| Degree | Use Case |
|--------|----------|
| 2 | Fast J2-only preliminary analysis |
| 10 | Good LEO production baseline |
| 20-30 | Higher-fidelity geodesy and mission analysis |
| 70 | Maximum supported EGM2008 fidelity |

!!! warning "Thread Safety"
    `GeopotentialGravitationalField` is **not** thread-safe. Create one `CelestialBody` with geopotential per propagation task.

#### The central term with the GM of the model

`GeopotentialModelParameters.CentralTerm` chooses the GM of the central term (`GeopotentialCentralTerm`):

| `CentralTerm` | Field |
|---------------|-------|
| `BodyGravitationalParameter` (the default) | `a = −GM_kernel r / r³ + ∇[(GM_model / r) Σ …]`, above |
| `ModelGravitationalParameter` | `a = −GM_model r / r³ + ∇[(GM_model / r) Σ …]`: the whole field takes the constants of the model |

The second is GMAT's convention: GMAT applies the GM of the header of a `.cof` file to the central term and to the
harmonics. It is an opt-in (decided by S. Guillet, 2026-10-27), for comparisons with a tool or a reference that applies
the model's GM to the whole field, such as the propagators 001 to 003 of the conformance suite, whose goldens are
regenerated with GMAT and EGM2008 (decision 17 of
[#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457)). The acceleration and its
partials (the state transition matrix and the covariance) take the same GM, and a field of degree 0 is the point mass
of the model's GM. The option changes the field only: `CelestialItem.GM`, the third-body perturbations, the
conversions of orbital parameters (a Keplerian orbit given to the propagation) and the two-body orbits of the library
keep the GM of the kernels. For a body whose harmonics take the kernel GM (any body
other than the Earth without constants of the model), both choices give the same field, bit for bit.

```csharp
// EGM2008 with its own GM in the central term too, as GMAT evaluates EGM2008 from a .cof header
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
    {
        CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
    });
```

For the Earth with EGM2008, the TT-compatible 3.986004415e14 m³/s² in a TDB propagation moves the whole acceleration by
1.5e-8 of itself: about 20 m a day in LEO and 8 m a day in GEO, 15.6 m, 6.8 m and 18.5 m on the propagators 001 to 003
of the conformance suite. With the option, the library reproduces GMAT R2026a's runs of those cases (EGM2008 from a
`.cof` with the model's GM) to 8.8 cm, 6.0 cm and 15.6 cm by RK7(8) at 1e-11, and 002 to 33.7 cm by Velocity-Verlet at
1 s (`GmatConformancePropagatorTests`, Linux, 2026-10-27); the rest is the orientation of the Earth and the GMs of the
third bodies in the two tools.

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
| `EarthStandardAtmosphere` | The U.S. Standard Atmosphere 1976 from −5 to 1000 km, of the altitude alone: its layers below 86 km joined by transitions of ±100 m, its thermosphere above (#437); a mean static model, not for orbit determination |
| `MarsStandardAtmosphere` | Simple Mars atmospheric model of the altitude: the NASA Glenn curve fit below 49.9 km, its branches joined over 500 m at 7 km (#433), the isothermal layer of Seiff (1982) at 139 K above; positive and decreasing at every altitude, but no thermosphere, so drag in orbit above about 120 km is underestimated (#431) |
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
| `AtmosphericDrag` | Analytic in the velocity and the co-rotation; density gradient in geodetic coordinates: dρ/dh in closed form for the U.S. Standard Atmosphere 1976 and Mars, the derivatives of NRLMSISE-00 in its inputs by finite differences, one-sided where the stencil straddles an altitude at which its density jumps (the cutoffs of its mixing, 160 to 450 km) | Exact (`∂a/∂Cd`) |
| `SolarRadiationPressure` | Analytic, the derivative of the shadow fraction included in the penumbra (GMAT Mathematical Specification, R2026a, §4.2.5), the shadow regime from the angles of the discs; zero in total eclipse | Exact (`∂a/∂Cr`) |
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
