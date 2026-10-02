# Ephemeris Queries

This tutorial shows how to query the positions and velocities of celestial bodies using SPICE ephemerides. You will learn to retrieve state vectors at a single epoch, generate time series, apply aberration corrections, and compute angular separations.

## Prerequisites

Load SPICE kernels before calling any ephemeris function:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
```

## Setting Up Bodies and Epochs

Create the celestial bodies you want to query and define a reference epoch:

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var sun = new CelestialBody(Stars.Sun);
var epoch = new Time(2024, 6, 21, 12, 0, 0);
```

`PlanetsAndMoons.EARTH_BODY` is a pre-built convenience instance. For other bodies, construct a `CelestialBody` from a NAIF ID constant.

## Single-Epoch Query

Retrieve the Earth-Sun state vector in the ICRF frame at one instant:

```csharp
var stateVector = earth.GetEphemeris(epoch, sun, Frames.Frame.ICRF, Aberration.None)
    .ToStateVector();

Console.WriteLine($"Position: {stateVector.Position} m");
Console.WriteLine($"Velocity: {stateVector.Velocity} m/s");
```

The returned `StateVector` contains position and velocity in SI units (meters and meters per second).

## Time Series

Generate a sequence of states over a time window at a fixed step size:

```csharp
var window = new Window(epoch, epoch.AddDays(1));
var states = moon.GetEphemeris(window, earth, Frames.Frame.ICRF, Aberration.LT,
    TimeSpan.FromHours(1));

foreach (var sv in states)
{
    Console.WriteLine($"{sv.Epoch}  range = {sv.Position.Magnitude():F0} m");
}
```

This returns an array of `StateVector` objects — one per step — spanning the full window.

## Aberration Corrections

The `Aberration` parameter controls light-time and stellar aberration corrections:

| Value | Meaning |
|-------|---------|
| `Aberration.None` | Geometric position — no corrections |
| `Aberration.LT` | One-way light-time correction |
| `Aberration.LTS` | Light-time plus stellar aberration |

Use `None` for propagation and force-model queries. Use `LT` or `LTS` for observational geometry (e.g., where does the Moon appear from Earth?).

```csharp
var geometric = earth.GetEphemeris(epoch, moon, Frames.Frame.ICRF, Aberration.None);
var apparent = earth.GetEphemeris(epoch, moon, Frames.Frame.ICRF, Aberration.LTS);
```

## Angular Separation

Compute the angular separation between two targets as seen from an observer:

```csharp
double angSep = earth.AngularSeparation(epoch, moon, sun, Aberration.None);
Console.WriteLine($"Moon-Sun angular separation: {angSep * Constants.Rad2Deg:F4} deg");
```

The result is in radians. Near-zero values indicate the Moon is close to the Sun on the sky (useful for conjunction/eclipse geometry).

## Summary

- Use `GetEphemeris` with a single epoch for point queries and with a `Window` for time series.
- Choose the aberration correction that matches your use case: `None` for dynamics, `LT`/`LTS` for observation.
- `AngularSeparation` measures the apparent angle between two targets from an observer.
