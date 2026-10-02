# Quick Start

## Install

```bash
dotnet add package IO.Astrodynamics
```

One package, no licence key, LGPL-3.0-or-later. It provides the whole API: orbital mechanics, SPICE
integration, numerical propagation with both integrators, maneuvers, attitude control, geometry
searches, Earth orientation frames, batch and Monte Carlo analysis, conjunction assessment, and
CCSDS OMM, OPM and CDM support.

!!! note "Coming from `IO.Astrodynamics.Pro`"
    Replace the package reference with `IO.Astrodynamics` 10.0.0 or later and drop the licence key.
    Namespaces are unchanged, so no code edit is needed. See
    [Versioning](contributing/versioning.md).

## Load SPICE Kernels

Most calculations require kernels before any ephemeris, frame, or geometry work:

```csharp
using IO.Astrodynamics;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
```

You can also load an individual file:

```csharp
SpiceAPI.Instance.LoadKernels(new FileInfo("de440.bsp"));
```

## Compute A Basic Ephemeris

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var earth = PlanetsAndMoons.EARTH_BODY;
var sun = new CelestialBody(Stars.Sun);
var epoch = new Time(2024, 6, 21, 12, 0, 0);

var stateVector = earth.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

Console.WriteLine($"Position: {stateVector.Position}");
Console.WriteLine($"Velocity: {stateVector.Velocity}");
```

## Create An Orbit

```csharp
using IO.Astrodynamics.OrbitalParameters;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 1, 1, 0, 0, 0);

var orbit = new KeplerianElements(
    a: 7000000.0,
    e: 0.001,
    i: 51.6 * Constants.Deg2Rad,
    raan: 100.0 * Constants.Deg2Rad,
    aop: 90.0 * Constants.Deg2Rad,
    m: 0.0,
    observer: earth,
    epoch: epoch,
    frame: Frames.Frame.ICRF
);

Console.WriteLine($"Period: {orbit.Period().TotalHours:F2} hours");
```

## Parse A TLE

```csharp
using IO.Astrodynamics.OrbitalParameters.TLE;

var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

var epoch = new Time(2021, 1, 21, 12, 0, 0);
var sv = tle.ToOsculating(epoch);
```

A TLE holds **mean** elements. `ToOsculating(epoch)` runs SGP4/SDP4 and returns the osculating
state, which is the form every other API expects. See [TLE & OMM](tutorials/tle-and-omm.md).

## Propagate An Orbit

=== "Velocity-Verlet"

    The fixed-step symplectic integrator:

    ```csharp
    using IO.Astrodynamics.Body.Spacecraft;
    using IO.Astrodynamics.Propagator;
    using IO.Astrodynamics.Propagator.Integrators;

    var spacecraft = new Spacecraft(
        id: -1001,
        name: "MySat",
        mass: 500.0,
        initialOrbitalParameters: orbit,
        fuelTank: fuelTank,
        engine: engine);

    var window = new Window(epoch, epoch.AddHours(2));

    using var propagator = new CentralBodyPropagator(
        window,
        spacecraft,
        new VVIntegrator(TimeSpan.FromSeconds(10.0)),
        new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
        includeAtmosphericDrag: false,
        includeSolarRadiationPressure: false,
        deltaT: TimeSpan.FromSeconds(60.0));

    var solution = propagator.Propagate();
    var (position, velocity) = solution.InterpolateAt(window.EndDate);
    ```

=== "Adaptive RK7(8)"

    The adaptive Prince-Dormand integrator with sub-step event refinement, assembled with the
    fluent builder:

    ```csharp
    using IO.Astrodynamics.Propagator;
    using IO.Astrodynamics.Propagator.Integrators;

    var integrator = new RK78Integrator(
        absoluteTolerance: 1e-10,
        relativeTolerance: 1e-10,
        initialStepSize: 30.0);

    var window = new Window(epoch, epoch.AddHours(2));

    using var propagator = new CentralBodyPropagatorBuilder(
            window, spacecraft, integrator, TimeSpan.FromSeconds(60.0))
        .WithPerturbingBody(Stars.SUN_BODY)
        .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
        .IncludeAtmosphericDrag()
        .IncludeSolarRadiationPressure()
        .Build();

    var solution = propagator.Propagate();
    var (position, velocity) = solution.InterpolateAt(window.EndDate);
    ```

## Next Steps

- [Ephemeris Queries](tutorials/ephemeris-queries.md) — query positions of planets, moons, and spacecraft
- [Coordinate Conversions](tutorials/coordinate-conversions.md) — frame transforms and Earth orientation
- [Orbit Creation](tutorials/orbit-creation.md) — Keplerian, equinoctial, and Cartesian representations
- [TLE & OMM](tutorials/tle-and-omm.md) — TLE parsing, OMM interchange, and SGP4 propagation
- [Numerical Propagation](tutorials/numerical-propagation.md) — high-accuracy orbit propagation with force models
- [API Reference](reference/index.md) — systematic type documentation
