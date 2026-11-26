# Numerical Propagation

This tutorial covers high-fidelity orbit propagation with configurable force models. You will start with the default settings (the adaptive RK7(8) integrator), then tune the integrator, and learn how to select force models, configure geopotential gravity, and extract dense output. Every setting of a propagation is a value of `PropagationOptions` ([Propagation Options](../reference/propagators.md#propagation-options)).

## Basic Propagation

The simplest way to propagate uses `Spacecraft.Propagate`, which builds the integrator and the force models from its options:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

SpiceApi.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

var orbit = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7700.0, 0.0),
    earth, epoch, Frame.ICRF);

var clock = new Clock("DemoClock", 65536);  // 65536 ticks per second
var spacecraft = new Spacecraft(-1001, "DemoSat", 100.0, orbit, 10000.0, clock);

var solution = spacecraft.Propagate(
    new Window(epoch, epoch.AddDays(1)),
    new PropagationOptions { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY } });
```

This uses the adaptive RK7(8) integrator at its defaults (tolerances of 1e-9) with central body gravity, solar and lunar third-body perturbations. The output is sampled at 60-second intervals, the default `OutputStep`; `spacecraft.Propagate(window)` alone propagates about the central body only.

## Adaptive RK7(8) Integrator

!!! tip "Why RK7(8)"
    The Prince-Dormand RK7(8) integrator provides 8th-order accuracy with adaptive step control and sub-step event refinement. It is the recommended integrator for high-fidelity propagation.

Configure the integrator with tolerance and step-size bounds:

```csharp
var integrator = new RK78IntegratorOptions
{
    AbsoluteTolerance = 1e-10,
    RelativeTolerance = 1e-10,
    InitialStep = TimeSpan.FromSeconds(30.0),
    MinStep = TimeSpan.FromMilliseconds(1.0),
    MaxStep = TimeSpan.FromSeconds(300.0)
};
```

| Property | Description |
|-----------|-------------|
| `AbsoluteTolerance` | Absolute error threshold per step (m, m/s); 1e-9 by default |
| `RelativeTolerance` | Relative error threshold per step; 1e-9 by default |
| `InitialStep` | Starting step size; 60 s by default |
| `MinStep` | Minimum allowed step size; 10 ticks (1 µs) by default |
| `MaxStep` | Maximum allowed step size; one day by default |

Tighter tolerances produce more accurate results at the cost of smaller (more numerous) steps. For typical LEO propagation, tolerances of `1e-10` with a max step of 300 seconds work well. A point-mass LEO day ends 1.36 m from the Keplerian closed form at the defaults.

!!! warning "Velocity-Verlet"
    The fixed-step Velocity-Verlet integrator is `new VVIntegratorOptions { Step = ... }`, 1 s by default. Its error
    grows as the square of its step: the same LEO day ends 271 m from Kepler at 1 s, 27.1 km at 10 s and 974 km at 60 s.
    Before 11.0.0, the members that took no integrator stepped Velocity-Verlet at the output step.

## Direct Propagator Construction

!!! tip
    Use `CentralBodyPropagator` directly for full control over integrator selection and output cadence.

```csharp
// Inputs: epoch (Time), spacecraft (Spacecraft), integrator (RK78IntegratorOptions)
var options = new PropagationOptions
{
    Integrator = integrator,
    OutputStep = TimeSpan.FromSeconds(10.0),
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
};

var propagator = new CentralBodyPropagator(new Window(epoch, epoch.AddHours(2)), spacecraft, options);
var solution = propagator.Propagate();
```

The `OutputStep` setting controls the output interval. The integrator steps adaptively regardless of this value — dense output interpolation fills in the requested epochs, every `OutputStep` from the start of the window and the window end last.

## Force Models

### Central Body Gravity

Always included. Uses the observer's gravitational parameter for two-body acceleration.

### Third-Body Perturbations

Pass perturbing bodies in `ThirdBodies`. The framework uses Battin's numerically stable formulation to avoid catastrophic cancellation:

```csharp
// Inputs: earth (CelestialBody)
var bodies = new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY,
    Barycenters.JUPITER_BARYCENTER };
```

### Geopotential Gravity (EGM2008)

For higher-fidelity Earth gravity, attach a geopotential model to the central body:

```csharp
// Inputs: epoch (Time)
var earthWithGeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
```

The second argument to `GeopotentialModelParameters` is the maximum degree of the spherical harmonic expansion. Higher degrees are more accurate but slower. Degree 10 is a good balance for LEO; degree 70 is available for precision work.

### Atmospheric Drag and Solar Radiation Pressure

Enable drag and SRP in the options:

```csharp
// Inputs: window (Window), spacecraft (Spacecraft), integrator (RK78IntegratorOptions)
var solution = spacecraft.Propagate(window, new PropagationOptions
{
    Integrator = integrator,
    OutputStep = TimeSpan.FromSeconds(10.0),
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    AtmosphericDrag = true,
    SolarRadiationPressure = true
});
```

Drag uses the atmosphere of the central body with default Cd = 2.2: its atmospheric model, or NRLMSISE-00 at nominal
space weather for an Earth built without one. Another central body needs a model when drag is on (the propagation throws
`InvalidOperationException` otherwise), and about the Sun (a star or barycentre observer) the listed bodies with an
atmosphere give the drag, an Earth always having one; a listed Earth adds its drag about any central body. The central body takes part whether or not it is listed, with the models of the observer, and a listed
body of its NAIF identifier must have the same models (`ArgumentException` otherwise). SRP uses a cannonball model with
continuous shadow fraction computation, the central body among the occulting bodies.

## Albedo and Thermal Radiation Pressure

!!! tip "Radiation of the central body"
    `AlbedoRadiationPressure` and `ThermalRadiationPressure` act on the central body, which carries the coefficients:
    build the orbit on a body given them. (The fluent `CentralBodyPropagatorBuilder` of 10.x was removed in 11.0.0.)

```csharp
// Inputs: epoch (Time), integrator (RK78IntegratorOptions)
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    albedo: 0.306,
    thermalEffectiveTemperature: 254.0,
    thermalEmissivity: 0.95);
var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7700.0, 0.0), earth, epoch, Frame.ICRF);
var spacecraft = new Spacecraft(-1002, "RadiationSat", 100.0, orbit, 10000.0, new Clock("RadiationClock", 65536));

var solution = spacecraft.Propagate(new Window(epoch, epoch.AddHours(2)), new PropagationOptions
{
    Integrator = integrator,
    OutputStep = TimeSpan.FromSeconds(10),
    ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
    AlbedoRadiationPressure = true,
    ThermalRadiationPressure = true
});
```

- **Albedo radiation pressure**: Models reflected sunlight from the central body (Lambertian sphere). Requires `albedo > 0` (`ArgumentException` otherwise).
- **Thermal radiation pressure**: Models infrared emission from the central body (isotropic emitter). Requires `thermalEffectiveTemperature > 0` and `thermalEmissivity > 0` (`ArgumentException` otherwise). Always present regardless of eclipse state.

## Dense Output

The propagation solution gives the state at any epoch of its window (`PropagationSolution.Window`), in the frame of the
propagation and about its central body; an epoch outside the window throws `ArgumentOutOfRangeException`:

```csharp
// Inputs: solution (PropagationSolution), epoch (Time)
var state = solution.GetStateVector(epoch.AddHours(12));

Console.WriteLine($"Position: {state.Position}");
Console.WriteLine($"Velocity: {state.Velocity}");
```

In RK7(8) the state is computed by a shortened RK7(8) step from the start of the accepted step that holds the epoch, at
the accuracy of the integrator; in Velocity-Verlet it is the cubic Hermite interpolation of the accepted step, from the
position, velocity and acceleration stored at its ends. Neither re-integrates the propagation. `GetStateVector` is the
one public reader of a solution (decision 54 of #668: the former `InterpolateAt`, which returned an unnamed tuple and
clamped outside the window, is internal since 11.0.0).

## Summary

- `Spacecraft.Propagate` is the quickest way to run a propagation; `PropagationOptions` holds every setting, RK7(8) at 1e-9 every 60 s by default.
- The RK7(8) integrator provides adaptive step control and 8th-order accuracy; Velocity-Verlet takes its own step.
- Force models include two-body gravity, geopotential harmonics, third-body perturbations, drag, SRP, albedo, and thermal radiation.
- Dense output via `GetStateVector` provides states at arbitrary times of the window without re-propagation.
