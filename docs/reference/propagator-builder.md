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
| `Build()` | `CentralBodyPropagator` | Construct the configured propagator |

Every fluent method returns the builder for chaining. Calling the same method twice throws `InvalidOperationException`.

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
