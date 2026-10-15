# Propagator Builder

`CentralBodyPropagatorBuilder` provides a fluent API for assembling a `CentralBodyPropagator` with its force models. It prevents duplicate configuration and injects ephemeris caches automatically.

## Constructor

| Parameter | Type | Description |
|-----------|------|-------------|
| `window` | `Window` | Propagation time window |
| `spacecraft` | `Spacecraft` | Spacecraft to propagate |
| `integrator` | `Integrator` | Integrator instance (e.g., `RK78Integrator`) |
| `deltaT` | `TimeSpan` | Output time step for dense output |

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `WithPerturbingBody(CelestialItem)` | Builder | Add a single third-body perturber |
| `WithPerturbingBodies(IEnumerable<CelestialItem>)` | Builder | Add multiple perturbers at once |
| `IncludeAtmosphericDrag()` | Builder | Enable atmospheric drag |
| `IncludeSolarRadiationPressure()` | Builder | Enable direct solar radiation pressure |
| `IncludeAlbedo(CelestialBody)` | Builder | Enable albedo radiation pressure from a reflecting body |
| `IncludeThermalRadiation(CelestialBody)` | Builder | Enable thermal radiation pressure from an emitting body |
| `WithCovariancePropagation(CovariancePropagationOptions)` | Builder | Propagate the state transition matrix and, in covariance mode, the covariance ([Covariance Propagation](covariance-propagation.md)); needs an `RK78Integrator` |
| `Build()` | `CentralBodyPropagator` | Construct the configured propagator |

Every fluent method returns the builder for chaining. Calling the same method twice throws `InvalidOperationException`.

The bodies of the forces are those given to `WithPerturbingBody` and `WithPerturbingBodies`: atmospheric drag applies for
the bodies among them that have an atmospheric model, so list the central body to have its drag. `Build` checks the
covariance propagation options once all the forces are added, since the columns of Ψ follow them (Cd with drag, Cr with
a radiation force).

## Example

```csharp
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);

var integrator = new RK78Integrator(
    absoluteTolerance: 1e-10,
    relativeTolerance: 1e-10,
    initialStepSize: 30.0);

using var propagator = new CentralBodyPropagatorBuilder(
        new Window(epoch, epoch.AddHours(2)),
        spacecraft, integrator, TimeSpan.FromSeconds(60))
    .WithPerturbingBody(Stars.SUN_BODY)
    .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
    .IncludeAtmosphericDrag()
    .IncludeSolarRadiationPressure()
    .IncludeAlbedo(earth)
    .IncludeThermalRadiation(earth)
    .Build();

var solution = propagator.Propagate();
```

## See Also

- [Propagators](propagators.md)
- [Integrators](integrators.md)
- [Force Models](force-models.md)
