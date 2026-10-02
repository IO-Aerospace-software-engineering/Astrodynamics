# Orbit Creation

This tutorial covers how to create and manipulate orbits using Cartesian state vectors, Keplerian elements, and equinoctial elements. You will also learn to compute orbital properties, perform two-body propagation, and construct special orbits.

## Cartesian State Vector

The most direct way to define an orbit is with position and velocity vectors:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

var earth = PlanetsAndMoons.EARTH_BODY;
var epoch = new Time(2024, 6, 21, 12, 0, 0);

var sv = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 8000.0, 0.0),
    earth, epoch, Frames.Frame.ICRF);
```

Position is in meters, velocity in meters per second. The observer (central body), epoch, and reference frame complete the orbit definition.

## Orbital Properties

Query derived quantities directly from any orbital parameter set:

```csharp
Console.WriteLine($"Semi-major axis: {sv.SemiMajorAxis():F0} m");
Console.WriteLine($"Eccentricity:    {sv.Eccentricity():F6}");
Console.WriteLine($"Inclination:     {sv.Inclination() * Constants.Rad2Deg:F4} deg");
Console.WriteLine($"Period:          {sv.Period().TotalMinutes:F2} min");
Console.WriteLine($"Specific energy: {sv.SpecificOrbitalEnergy():F2} J/kg");
Console.WriteLine($"Apogee altitude: {sv.ApogeeVector().Magnitude() - earth.EquatorialRadius:F0} m");
```

All angular quantities are in radians. Use `Constants.Rad2Deg` to convert to degrees for display.

## Keplerian Elements

Create an orbit from classical elements (a, e, i, RAAN, AoP, M):

```csharp
var kep = new KeplerianElements(
    a: 7000000.0,
    e: 0.001,
    i: 51.6 * Constants.Deg2Rad,
    raan: 100.0 * Constants.Deg2Rad,
    aop: 90.0 * Constants.Deg2Rad,
    m: 0.0,
    observer: earth,
    epoch: epoch,
    frame: Frames.Frame.ICRF);

// Convert to Cartesian
var svFromKep = kep.ToStateVector();
Console.WriteLine($"Position: {svFromKep.Position}");
```

The anomaly parameter `m` is the mean anomaly in radians. All angles follow the convention: radians in, radians out.

## Two-Body Propagation with AtEpoch

Propagate Keplerian elements analytically to a future epoch:

```csharp
var laterKep = kep.AtEpoch(epoch.AddDays(1));

Console.WriteLine($"Mean anomaly after 1 day: {laterKep.MeanAnomaly() * Constants.Rad2Deg:F4} deg");
```

`AtEpoch` uses Kepler's equation (two-body) — it is fast but does not include perturbations. For perturbed orbits, use numerical propagation (see [Numerical Propagation](numerical-propagation.md)).

## Equinoctial Elements

Equinoctial elements avoid singularities at zero eccentricity and zero inclination:

```csharp
var equinoctial = sv.ToEquinoctial();

Console.WriteLine($"Semi-major axis: {equinoctial.SemiMajorAxis():F0} m");
Console.WriteLine($"h: {equinoctial.H():F6}");
Console.WriteLine($"k: {equinoctial.K():F6}");
Console.WriteLine($"p: {equinoctial.P():F6}");
Console.WriteLine($"q: {equinoctial.Q():F6}");
```

They are preferred for numerical orbit determination and low-eccentricity orbits where Keplerian elements become poorly defined.

## Special Orbits

### Geosynchronous Orbit

Generate a GEO orbit at a given longitude:

```csharp
var geoOrbit = earth.GeosynchronousOrbit(
    longitude: 0.0,
    inclination: 0.0,
    epoch: epoch);

Console.WriteLine($"GEO semi-major axis: {geoOrbit.SemiMajorAxis():F0} m");
Console.WriteLine($"GEO period: {geoOrbit.Period().TotalHours:F4} hours");
```

### Sun-Synchronous Orbit

Generate an SSO for a given altitude and eccentricity:

```csharp
var ssoOrbit = earth.HelioSynchronousOrbit(
    semiMajorAxis: 7000000.0,
    eccentricity: 0.001,
    epoch: epoch);

Console.WriteLine($"SSO inclination: {ssoOrbit.Inclination() * Constants.Rad2Deg:F4} deg");
```

The inclination is computed to produce the required nodal precession rate (~0.9856 deg/day) for Sun synchronism.

## Summary

- Use `StateVector` for Cartesian, `KeplerianElements` for classical, and `ToEquinoctial` for singularity-free representations.
- All representations share the same property methods (`SemiMajorAxis()`, `Eccentricity()`, `Period()`, etc.).
- `AtEpoch` provides fast two-body analytical propagation from Keplerian elements.
- `GeosynchronousOrbit` and `HelioSynchronousOrbit` are convenience builders for common orbit types.
