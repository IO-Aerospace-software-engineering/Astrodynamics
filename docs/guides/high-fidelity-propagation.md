# High-Fidelity Propagation

One `PropagationOptions` value ([Propagation Options](../reference/propagators.md#propagation-options)) assembles a
propagation with the full suite of force models in a single declarative setup. (The fluent `CentralBodyPropagatorBuilder`
of 10.x was removed in 11.0.0.)

## Full Force Model Assembly

```csharp
// Inputs: window (Window), orbit (StateVector), outputStep (TimeSpan)
// Central body with geopotential, atmosphere, albedo, and thermal properties
var earth = new CelestialBody(PlanetsAndMoons.Earth,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    new Nrlmsise00Model(SpaceWeather.Nominal),
    albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);

// The orbit about this Earth: the central body carries the models and the coefficients
var spacecraft = new Spacecraft(-1001, "HiFiSat", 100.0, new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame), 10000.0,
    new Clock("HiFiClock", 65536));

// The adaptive integrator and every perturbation
var options = new PropagationOptions
{
    Integrator = new RK78IntegratorOptions
    {
        AbsoluteTolerance = 1e-10,
        RelativeTolerance = 1e-10,
        InitialStep = TimeSpan.FromSeconds(30.0)
    },
    OutputStep = outputStep,
    ThirdBodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody },
    AtmosphericDrag = true,
    SolarRadiationPressure = true,
    AlbedoRadiationPressure = true,
    ThermalRadiationPressure = true
};

var solution = spacecraft.Propagate(window, options);
```

## Force Models

The options support the following perturbations:

| Setting | Force Model |
|--------|-------------|
| *(automatic)* | Central-body gravity, with its geopotential when the body has one (or `Geopotential`) |
| `ThirdBodies` | Third-body perturbation |
| `AtmosphericDrag` | Atmospheric drag of the central body's atmosphere (or `Atmosphere`) |
| `SolarRadiationPressure` | Direct solar radiation pressure |
| `AlbedoRadiationPressure` | Reflected sunlight of the central body (Lambertian sphere) |
| `ThermalRadiationPressure` | Infrared emission of the central body (isotropic emitter) |

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

- [Propagators](../reference/propagators.md#propagation-options) — the settings of `PropagationOptions`
- [Force Models](../reference/force-models.md) — physics equations and parameter tables
- [Thread Safety](thread-safety.md) — constraints for parallel propagation
