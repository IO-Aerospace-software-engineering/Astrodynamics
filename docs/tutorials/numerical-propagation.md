# Numerical Propagation

This tutorial covers high-fidelity orbit propagation with configurable force models. You will start with the community Velocity-Verlet integrator, then move to the adaptive RK7(8) integrator available in the Pro edition, and learn how to select force models, configure geopotential gravity, and extract dense output.

## Basic Propagation (Community)

The simplest way to propagate uses `Spacecraft.Propagate`, which sets up the integrator and force models automatically:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 6, 21, 12, 0, 0);

var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7700.0, 0.0),
    earth, epoch, Frame.ICRF);

var clock = new Clock("DemoClock", 1.0 / 65536.0);
var spacecraft = new Spacecraft(-1001, "DemoSat", 100.0, 10000.0, clock, orbit);

var solution = spacecraft.Propagate(
    new Window(epoch, epoch.AddDays(1)),
    new[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromSeconds(60));
```

This uses the fixed-step Velocity-Verlet integrator with central body gravity, solar and lunar third-body perturbations. The output is sampled at 60-second intervals.

## Adaptive RK7(8) Integrator

!!! tip "Why RK7(8)"
    The Prince-Dormand RK7(8) integrator provides 8th-order accuracy with adaptive step control and sub-step event refinement. It is the recommended integrator for high-fidelity propagation.

Configure the integrator with tolerance and step-size bounds:

```csharp
var integrator = new RK78Integrator(
    absoluteTolerance: 1e-10,
    relativeTolerance: 1e-10,
    initialStepSize: 30.0,
    minStepSize: 1e-3,
    maxStepSize: 300.0);
```

| Parameter | Description |
|-----------|-------------|
| `absoluteTolerance` | Absolute error threshold per step (m, m/s) |
| `relativeTolerance` | Relative error threshold per step |
| `initialStepSize` | Starting step size in seconds |
| `minStepSize` | Minimum allowed step size in seconds |
| `maxStepSize` | Maximum allowed step size in seconds |

Tighter tolerances produce more accurate results at the cost of smaller (more numerous) steps. For typical LEO propagation, tolerances of `1e-10` with a max step of 300 seconds work well.

## Direct Propagator Construction

!!! tip
    Use `CentralBodyPropagator` directly for full control over integrator selection and output cadence.

```csharp
using var propagator = new CentralBodyPropagator(
    new Window(epoch, epoch.AddHours(2)),
    spacecraft,
    integrator,
    new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    deltaT: TimeSpan.FromSeconds(10.0));

var solution = propagator.Propagate();
```

The `deltaT` parameter controls the output interval. The integrator steps adaptively regardless of this value — dense output interpolation fills in the requested epochs.

## Force Models

### Central Body Gravity

Always included. Uses the observer's gravitational parameter for two-body acceleration.

### Third-Body Perturbations

Pass perturbing bodies in the `CelestialItem[]` array. The framework uses Battin's numerically stable formulation to avoid catastrophic cancellation:

```csharp
new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY,
    PlanetsAndMoons.JUPITER_BODY }
```

### Geopotential Gravity (EGM2008)

For higher-fidelity Earth gravity, attach a geopotential model to the central body:

```csharp
var earthWithGeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
```

The second argument to `GeopotentialModelParameters` is the maximum degree of the spherical harmonic expansion. Higher degrees are more accurate but slower. Degree 10 is a good balance for LEO; degree 70 is available for precision work.

### Atmospheric Drag and Solar Radiation Pressure

Enable drag and SRP with the boolean flags:

```csharp
using var propagator = new CentralBodyPropagator(
    window, spacecraft, integrator,
    new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    includeAtmosphericDrag: true,
    includeSolarRadiationPressure: true,
    deltaT: TimeSpan.FromSeconds(10.0));
```

Drag uses the NRLMSISE-00 atmosphere model with default Cd = 2.2. SRP uses a cannonball model with continuous shadow fraction computation.

## Fluent Builder with Pro Force Models

!!! tip "Fluent configuration"
    The `CentralBodyPropagatorBuilder` provides a fluent API for constructing propagators with advanced force models including albedo and thermal radiation pressure.

```csharp
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    albedo: 0.306,
    thermalEffectiveTemperature: 254.0,
    thermalEmissivity: 0.95);

using var propagator = new CentralBodyPropagatorBuilder(
        new Window(epoch, epoch.AddHours(2)), spacecraft, integrator,
        TimeSpan.FromSeconds(10))
    .WithPerturbingBody(Stars.SUN_BODY)
    .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
    .IncludeAlbedo(earth)
    .IncludeThermalRadiation(earth)
    .Build();

var solution = propagator.Propagate();
```

- **Albedo radiation pressure**: Models reflected sunlight from the central body (Lambertian sphere). Requires `albedo > 0`.
- **Thermal radiation pressure**: Models infrared emission from the central body (isotropic emitter). Requires `thermalEffectiveTemperature > 0` and `thermalEmissivity > 0`. Always present regardless of eclipse state.

## Dense Output

The propagation solution supports interpolation at arbitrary epochs within the propagated window:

```csharp
var stateAtHalfDay = solution.InterpolateAt(epoch.AddHours(12));

Console.WriteLine($"Position: {stateAtHalfDay.Position}");
Console.WriteLine($"Velocity: {stateAtHalfDay.Velocity}");
```

Interpolation uses cubic Hermite polynomials constructed from the position, velocity, and acceleration stored at each accepted integration step. This provides smooth, continuous output without re-integrating.

## Summary

- `Spacecraft.Propagate` is the quickest way to run a propagation with default settings.
- The RK7(8) integrator provides adaptive step control and 8th-order accuracy.
- Force models include two-body gravity, geopotential harmonics, third-body perturbations, drag, SRP, albedo, and thermal radiation.
- The fluent builder enables concise configuration of advanced force model combinations.
- Dense output via `InterpolateAt` provides states at arbitrary times without re-propagation.
