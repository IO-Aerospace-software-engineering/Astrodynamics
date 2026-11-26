# Force Models

Force models compute accelerations applied to a spacecraft during numerical propagation. Gravity, drag and direct solar radiation pressure cover the standard perturbations; albedo and thermal radiation pressure add the radiation emitted or reflected by the central body.

## Gravity, Drag And Solar Radiation

### GravitationalAcceleration

Central-body two-body gravity. Always present as the primary force.

```csharp-signature
new GravitationalAcceleration(centralBody)
```

### ThirdBodyPerturbation

Third-body gravitational perturbation using Battin's numerically stable formula to avoid catastrophic cancellation when the perturbing body is distant, and the direct difference −μ ((r − d) / |r − d|³ + d / |d|³) where the spacecraft is closer to the perturbing body than half its distance to the central body (|r − d| < |d| / 2), where Battin's 1 + q = |r − d|² / |d|² would cancel: in SSB mode about the Earth (the Sun the central body, the Earth a third body) it put a relative noise of about 1e-7 on the Earth's attraction before 11.0.0 (#576, #544). The acceleration is within a few ε of the exact one at every distance (5.4e-15 relative bound), and Battin's bit for bit where it applies, so a propagation about the Earth with the Moon and the Sun does not move.

```csharp-signature
new ThirdBodyPerturbation(perturbingBody, centralBody)
```

### AtmosphericDrag

Atmosphere-relative velocity with co-rotation of the central body's atmosphere.

```csharp-signature
new AtmosphericDrag(spacecraft, celestialBody)
```

| Property | Default | Description |
|----------|---------|-------------|
| `DragCoefficient` (Cd) | 2.2 | Set on the `Spacecraft` constructor |
| `SectionalArea` | -- | Cross-sectional area (m2) |

The density is that of the body's atmosphere: its atmospheric model, or NRLMSISE-00 at nominal space weather for an
Earth built without one; another body without a model throws `ArgumentException`. In a propagation that includes drag
(#674):

- about a central body other than the Sun, its atmosphere gives the drag, whether or not it is listed; without one (a
  Moon, a Mars, built without a model) the propagation throws `InvalidOperationException` naming it, before anything
  changes, whatever the listed bodies;
- about the Sun (a star or barycentre observer, SSB mode), the listed bodies with an atmosphere give the drag, and the
  propagation throws when none has one (no Earth listed, and no body with a model);
- in both cases, the other listed bodies with an atmosphere add their drag.

An Earth always has an atmosphere: wherever the library decides whether a body has one (drag about the central body, in
SSB mode, from a listed non-central body, and `AtmosphericDrag`), an Earth without a model takes NRLMSISE-00 at nominal
space weather (decided by S. Guillet, 2026-11-14). `HasAtmosphericModel` says whether a model was given, not whether the
body has an atmosphere.

### SolarRadiationPressure

Cannonball SRP model with configurable reflectivity coefficient and continuous shadow fraction for penumbra transitions.

```csharp-signature
new SolarRadiationPressure(spacecraft, occultingBodies)
```

The occulting bodies are the bodies that can eclipse the Sun. The Sun itself is ignored if it is among them, so the
propagator can pass its whole body list, Sun included.

| Property | Default | Description |
|----------|---------|-------------|
| `SolarRadiationCoefficient` (Cr) | 1.0 | Set on the `Spacecraft` constructor |

!!! note "Shadow fraction"
    The SRP is scaled by `1 − ν`, `ν` the largest shadow fraction of the occulting bodies
    (`CelestialItem.ShadowFraction`): the fraction of the solar disc covered by the disc of the body, 0 in sunlight, 1
    in total eclipse, `b² / a²` in an annular eclipse of angular radii `a` (the Sun) and `b`. In the penumbra it is the
    sum of the two circular segments cut by the common chord, computed without cancellation (Heron's half chord in the
    form of Kahan, the half angles from atan2): within a few 1e-16 of the exact value of the angles everywhere, and
    always in [0, 1]. Before 10.2.0-preview-1 a closed form of the same area cancelled near the contacts of the
    penumbra, by up to 1.7e-4 in LEO, and could return values outside [0, 1]
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

The degree is at most the highest degree of the model file, which must hold every coefficient (n, m) with
2 ≤ n ≤ degree: a degree above the file throws `ArgumentOutOfRangeException` (parameter `maxDegrees`, the message naming
the degree asked and the file's), a missing coefficient `ArgumentException` naming n and m, when the field or the body
is built (#689; before 11.0.0 both were read as zeros, so a degree above the file ran at the file's degree). Degrees 0
and 1, absent from the models, are the point mass.

The coefficients of a model are normalized to its own GM and reference radius, which the coefficient file does not
carry. The harmonics are evaluated with them, and the central term with the GM of the SPICE kernels:

```
a = −GM_kernel r / r³ + ∇[(GM_model / r) Σ_(n≥2) (a_model / r)ⁿ Σ_m P̄_nm(sin φ) (C̄_nm cos mλ + S̄_nm sin mλ)]
```

| Body | `GeopotentialModelParameters` | GM_model, a_model |
|------|-------------------------------|-------------------|
| Earth (NAIF 399) | `Egm2008(degree)`, `(path, degree)` or `(stream, degree)` | EGM2008: 3.986004415e14 m³/s², 6378136.3 m (Pavlis et al., 2012) |
| Any other body | `(path, degree)` or `(stream, degree)` | the GM and the equatorial radius of its kernels |
| Any body | `(path, degree, gravitationalParameter, referenceRadius)` or the same with a stream | the given ones |

By default the central term keeps the GM of the kernels (DE440's for the Earth, 3.98600435507e14 m³/s²), the GM of
every other point-mass computation of the library (`CelestialItem.GM`, the third bodies, the element conversions).
That GM is TDB-compatible, while a propagation about the Earth counts TT seconds
([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501),
[Time & Windows](time-and-windows.md#the-time-argument-of-a-propagation)): the default pairs a TDB-compatible GM with the
TT time argument, 1.5e-8 from the TT-compatible value (the compatibility of a GM is that of the time scale it is used
with: IERS Conventions (2010), G. Petit and B. Luzum (eds.), IERS Technical Note 36, chapter 10 and Table 1.1; confirmed by S. Guillet, 2026-10-29). The TT-compatible value is the GM of
EGM2008, which GMAT applies from the header of a `.cof`: `GeopotentialCentralTerm.ModelGravitationalParameter` below. Scaled to TDB-compatible units
it is within 2.3e-10 of DE440's (Klioner et al., 2010). The GMs and the DE440 positions of the third bodies are not
scaled either, as GMAT and the suite do not scale them: 1.48e-8 of the third-body acceleration, about 1e-14 m/s² in LEO
for the Moon's tidal term (computed). Before 10.2.0-preview-1 the harmonics took the GM and the equatorial radius of
the kernels (6378136.6 m), 7.9e-8 of the harmonics away from EGM2008
([#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410)).

```csharp
// Inputs: epoch (Time)
// A model other than EGM2008, in the same file format, with its own constants
var mars = new CelestialBody(PlanetsAndMoons.Mars, Frame.ICRF, epoch,
    new GeopotentialModelParameters("mars_model", 20, 4.282837362069909e13, 3396000.0));
```

| Degree | Use Case |
|--------|----------|
| 2 | Fast J2-only preliminary analysis |
| 10 | Good LEO production baseline |
| 20-30 | Higher-fidelity geodesy and mission analysis |
| 70 | Maximum supported EGM2008 fidelity |

#### Geopotential model parameters

`GeopotentialModelParameters` is a value (R06 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
lot L4, 11.0.0): a sealed record of its source, an `int` degree (`GeopotentialDegree`, 0 or more), its constants and
`CentralTerm`, equal to another of the same source (the full path of a model file, EGM2008 embedded, or streams of the
same bytes), degree, constants and central term. `GeopotentialModelParameters.Egm2008(degree)` is EGM2008, tide free,
embedded in the library to degree 70 (`Egm2008MaximumDegree`): the coefficients of `EGM2008_to70_TideFree`, the file of
the tests, byte for byte, so its field is that file's at every degree (`GeopotentialOptionsTests`, degrees 2, 10 and 70,
bit for bit), with no file to ship. A stream is read when the parameters are built and left open; a path is checked
then (`FileNotFoundException`) and read at the first body or propagation. Before 11.0.0 the parameters were a class
compared by reference, holding the `StreamReader` of their file (`GeopotentialModelPath`), with a `ushort` degree.

A propagation takes the geopotential of its central body, or `PropagationOptions.Geopotential` as a fallback for a body
without one, an equal model, or one of the same coefficients from another source, accepted, another throwing
([Propagation Options](propagators.md#propagation-options)). Each run builds its own field from the model, over the
coefficients the model reads once, so propagations sharing a body never share a field.

!!! warning "Thread Safety"
    `GeopotentialGravitationalField` is **not** thread-safe: a body's own field, which its
    `EvaluateGravitationalAcceleration` evaluates, serves one thread at a time. A propagation builds its own field from
    the body's model (11.0.0, R06 of #668), so one `CelestialBody` with a geopotential may serve any number of
    propagations, on any thread. The field has no public constructor since 11.0.0: it is built from
    `GeopotentialModelParameters` by a body or a propagation.

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
of the model's GM, computed in the frame of the state without the body's orientation, so that it holds beyond the
coverage of the body's orientation data (lot 4 of #457). The option changes the field only: `CelestialItem.GM`, the third-body perturbations, the
conversions of orbital parameters (a Keplerian orbit given to the propagation) and the two-body orbits of the library
keep the GM of the kernels. For a body whose harmonics take the kernel GM (any body
other than the Earth without constants of the model), both choices give the same field, bit for bit.

```csharp
// Inputs: epoch (Time)
// EGM2008 with its own GM in the central term too, as GMAT evaluates EGM2008 from a .cof header
var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
    {
        CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
    });
```

For the Earth with EGM2008, the TT-compatible 3.986004415e14 m³/s² moves the whole acceleration by 1.5e-8 of itself:
about 20 m a day in LEO and 8 m a day in GEO, 15.5 m, 6.9 m and 18.4 m on the propagators 001 to 003 of the conformance
suite. With the option, the library reproduces GMAT R2026a's runs of those cases (EGM2008 from a `.cof` with the
model's GM, over the TT seconds of the day) to 1.9 cm, 0.83 cm and 1.1 cm by RK7(8) at 1e-11, and 002 to 39.7 cm by
Velocity-Verlet at 1 s (`GmatConformancePropagatorTests`, Linux, Windows and macOS arm64, 2026-11-01); the rest is the orientation of the Earth
and the GMs of the third bodies in the two tools. Before the TT time argument of #501 it was 8.8 cm, 6.0 cm, 15.6 cm
and 33.7 cm (2026-10-27): the change of TDB − TT over the day, |v|·δ, was most of the RK7(8) residual.

## Radiation From The Central Body

### AlbedoRadiationPressure

Models sunlight reflected off a celestial body using a Lambertian sphere approximation. The acceleration is:

```
a = (L_sun / 4*pi*c) * alpha * Cr * (A/m) * (2/3*pi) * (R_body / r_sun)^2 * f(phi) / r_sc^2
```

Where `f(phi) = (pi - phi) * cos(phi) + sin(phi)` is the Lambertian visibility function. Acceleration is zero on the dark side.

```csharp-signature
new AlbedoRadiationPressure(spacecraft, reflectingBody)
```

**Configuration**: Set `albedo` on the `CelestialBody` constructor of the central body, then set `PropagationOptions.AlbedoRadiationPressure` ([Propagation Options](propagators.md#propagation-options)); a body other than the central body goes through a force list of your own (`new AlbedoRadiationPressure(spacecraft, body)` added to an integrator given to the `CentralBodyPropagator` constructor that takes one).

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

```csharp-signature
new ThermalRadiationPressure(spacecraft, emittingBody)
```

**Configuration**: Set `thermalEffectiveTemperature` (Kelvin) and `thermalEmissivity` on the `CelestialBody` constructor of the central body, then set `PropagationOptions.ThermalRadiationPressure`; a body other than the central body goes through a force list of your own, as for the albedo.

| Body | T_eff (K) | Emissivity |
|------|-----------|------------|
| Earth | 254 | 0.95 |
| Moon | 270 | 0.95 |

## Atmosphere Models

| Model | Description |
|-------|-------------|
| `IAtmosphericModel` | Common interface for density queries |
| `EarthStandardAtmosphere` | The U.S. Standard Atmosphere 1976 from −5 to 1000 km, of the altitude alone: its layers below 86 km joined by transitions of ±100 m, its thermosphere above (#437); a mean static model, not for orbit determination; the base atmosphere of an Earth built without a model, which answers its altitude-only reads (`GetAtmosphere(double)`, a context without the geodetic position or the epoch), not its drag (decided by S. Guillet, 2026-11-14) |
| `MarsStandardAtmosphere` | Simple Mars atmospheric model of the altitude: the NASA Glenn curve fit below 49.9 km, ρ = p / [0.1921 (T + 273.1)] with the fit's own 273.1 (#438; 273.15 before 11.0.0), its branches joined over 500 m at 7 km (#433), the isothermal layer of Seiff (1982) at 139 K above; positive and decreasing at every altitude, but no thermosphere, so drag in orbit above about 120 km is underestimated (#431) |
| `Nrlmsise00Model` | Time-varying Earth model for drag analysis (thread-safe, its calls serialized on a lock of the instance); at nominal space weather, the atmosphere of an Earth built without a model, one instance per thread |

## Example

```csharp
var earth = new CelestialBody(PlanetsAndMoons.Earth,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
```

## The Atmosphere API

Every model implements `IAtmosphericModel`, which reads an `IAtmosphericContext` (the `AtmosphericContext` record) and
returns an `Atmosphere` record; a model's own data come in its `Details`, `NrlmsiseDetails` for NRLMSISE-00 and null
for the altitude-only models.

| Method of `IAtmosphericModel` | Description |
|-------------------------------|-------------|
| `GetAtmosphere(IAtmosphericContext context)` | The temperature, pressure, density and details at the context |
| `GetTemperature(IAtmosphericContext context)` | Temperature (°C) |
| `GetPressure(IAtmosphericContext context)` | Pressure (kPa) |
| `GetDensity(IAtmosphericContext context)` | Total mass density (kg/m³) |

| Property of `Atmosphere` | Description |
|--------------------------|-------------|
| `Temperature` | Temperature (°C) |
| `Pressure` | Pressure (kPa) |
| `Density` | Total mass density (kg/m³) |
| `Details` | The model's own data (`IAtmosphericDetails`), null for the altitude-only models |

| Member of `AtmosphericContext` | Description |
|--------------------------------|-------------|
| `Altitude` | Altitude above the reference surface (m), required |
| `GeodeticLatitude`, `GeodeticLongitude` | Geodetic latitude and longitude (rad), optional |
| `Epoch` | Epoch of the calculation, optional |
| `FromAltitude(double altitude)` | A context of the altitude alone |
| `FromPlanetodetic(Planetodetic planetodetic, Time epoch)` | A full context: the altitude, geodetic latitude and longitude of the coordinates (11.0.0; before, three doubles in the order altitude, latitude, longitude) |

```csharp
var model = new EarthStandardAtmosphere();
var context = AtmosphericContext.FromAltitude(10000);  // 10 km

// Get individual values
var temp = model.GetTemperature(context);     // °C
var pressure = model.GetPressure(context);    // kPa
var density = model.GetDensity(context);      // kg/m³

// Or get complete atmospheric data
var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"T={atmosphere.Temperature:F1}°C, P={atmosphere.Pressure:F3} kPa, ρ={atmosphere.Density:E3} kg/m³");
```

### NRLMSISE-00

`Nrlmsise00Model` needs a full context, the position and the epoch, and is thread-safe.

| Constructor | Description |
|-------------|-------------|
| `Nrlmsise00Model()` | Nominal space weather (F10.7 = 150, Ap = 4) |
| `Nrlmsise00Model(SpaceWeather weather)` | The given space weather |
| `Nrlmsise00Model(NrlmsiseFlags flags, SpaceWeather weather)` | The given switches and space weather |

| Property of `SpaceWeather` | Description |
|----------------------------|-------------|
| `F107` | Daily 10.7 cm solar radio flux |
| `F107A` | 81-day average of F10.7 |
| `Ap` | Daily magnetic index |
| `ApArray` | Optional 7-element Ap history |
| `Nominal`, `Moderate` | F10.7 150, Ap 4 and 7 |
| `SolarMinimum`, `SolarMaximum` | F10.7 70 and 250, Ap 4 and 15 |

| Property of `NrlmsiseDetails` | Description |
|-------------------------------|-------------|
| `HeliumDensity`, `AtomicOxygenDensity`, `NitrogenDensity`, `MolecularOxygenDensity`, `ArgonDensity` | Number densities of He, O, N₂, O₂ and Ar (m⁻³) |
| `HydrogenDensity`, `AtomicNitrogenDensity` | Number densities of H and N (m⁻³, zero below 72.5 km) |
| `AnomalousOxygenDensity` | Anomalous oxygen density (m⁻³) |
| `ExosphericTemperature` | Exospheric temperature (K) |

```csharp
// Use nominal conditions
var model = new Nrlmsise00Model();

// Or specify space weather
var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
var weatherModel = new Nrlmsise00Model(spaceWeather);

// Or use preset conditions
var solarMinModel = new Nrlmsise00Model(SpaceWeather.SolarMinimum);
var solarMaxModel = new Nrlmsise00Model(SpaceWeather.SolarMaximum);

// Full context required
var context = AtmosphericContext.FromPlanetodetic(
    new Planetodetic(0, 0, 400000),  // prime meridian, equator, 400 km
    new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));

var atmosphere = model.GetAtmosphere(context);
Console.WriteLine($"Density at 400 km: {atmosphere.Density:E3} kg/m³");

// Access model-specific details via pattern matching
if (atmosphere.Details is NrlmsiseDetails details)
{
    Console.WriteLine($"Atomic Oxygen: {details.AtomicOxygenDensity:E3} m⁻³");
    Console.WriteLine($"Molecular Nitrogen: {details.NitrogenDensity:E3} m⁻³");
    Console.WriteLine($"Exospheric Temp: {details.ExosphericTemperature:F0} K");
}
```

### The atmosphere of a body

`CelestialBody.GetAtmosphere` reads the body's model. An Earth built without one answers a full context with
NRLMSISE-00 at nominal space weather, the atmosphere of its drag (#674), and an altitude alone or a partial context
with the U.S. Standard Atmosphere 1976, its base atmosphere; a null context, or another body without a model, gives a NaN
temperature and a zero pressure and density.

```csharp
var earth = PlanetsAndMoons.EarthBody;

// Altitude only: the U.S. Standard Atmosphere 1976, the base atmosphere of an Earth without a model
var atm1 = earth.GetAtmosphere(10000);  // 10 km altitude

// Full context: NRLMSISE-00 at nominal space weather for an Earth without a model
var context = AtmosphericContext.FromPlanetodetic(
    new Planetodetic(-75 * Constants.Deg2Rad, 45 * Constants.Deg2Rad, 400000),  // longitude, latitude (rad), altitude (m)
    new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));
var atm2 = earth.GetAtmosphere(context);

// Access NRLMSISE-00 specific data
if (atm2.Details is NrlmsiseDetails details)
{
    Console.WriteLine($"O₂ density: {details.MolecularOxygenDensity:E3} m⁻³");
}
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

- [Propagators](propagators.md)
- [Integrators](integrators.md)
