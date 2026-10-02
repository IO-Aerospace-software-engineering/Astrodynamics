# High-Fidelity Propagation

The `CentralBodyPropagatorBuilder` fluent API assembles a propagator with the full suite of force models in a single declarative setup.

## Full Force Model Assembly

```csharp
// Central body with geopotential, atmosphere, albedo, and thermal properties
var earth = new CelestialBody(PlanetsAndMoons.EARTH,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    new Nrlmsise00Model(SpaceWeather.Nominal),
    albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);

// Adaptive integrator
var integrator = new RK78Integrator(
    absoluteTolerance: 1e-10,
    relativeTolerance: 1e-10,
    initialStepSize: 30.0);

// Assemble propagator with all perturbations
using var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, integrator, deltaT)
    .WithPerturbingBody(Stars.SUN_BODY)
    .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
    .IncludeAtmosphericDrag()
    .IncludeSolarRadiationPressure()
    .IncludeAlbedo(earth)
    .IncludeThermalRadiation(earth)
    .Build();

var solution = propagator.Propagate();
```

## Force Models

The builder supports the following perturbations:

| Method | Force Model | Source |
|--------|-------------|--------|
| *(automatic)* | Central-body gravity | Community |
| `WithPerturbingBody(body)` | Third-body perturbation | Community |
| `IncludeAtmosphericDrag()` | Atmospheric drag | Community |
| `IncludeSolarRadiationPressure()` | Direct solar radiation pressure | Community |
| `IncludeAlbedo(body)` | Reflected sunlight (Lambertian sphere) | Pro |
| `IncludeThermalRadiation(body)` | Infrared emission (isotropic emitter) | Pro |

## Geopotential Degree Guidance

| Degree | Use Case |
|--------|----------|
| `2` | Fast J2-only preliminary analysis |
| `10` | Good LEO production baseline |
| `20-30` | Higher-fidelity geodesy and mission analysis |
| `70` | Maximum supported EGM2008 fidelity |

!!! note "Performance trade-off"
    Higher geopotential degree increases computation time significantly. Degree 10 is sufficient for most LEO missions. Reserve degree 70 for geodetic satellites or when matching external high-fidelity references.

## Typical Acceleration Magnitudes (LEO)

For context, approximate acceleration magnitudes at 400 km altitude:

- **Direct SRP**: ~4.5e-8 m/s^2 (reference)
- **Albedo**: ~10-20x smaller than direct SRP
- **Thermal (IR)**: ~15% of direct SRP, always present regardless of eclipse

## See Also

- [Propagator Builder](../reference/propagator-builder.md) — full builder method table
- [Force Models](../reference/force-models.md) — physics equations and parameter tables
- [Thread Safety](thread-safety.md) — constraints for parallel propagation
