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

## Deep Space: The Moon As A Probe

A massless probe started at the Moon's state of J2000 TDB, about an Earth of GM_Earth + GM_Moon, with the Sun and the
barycentres of Jupiter and Saturn as point masses, follows the Moon of the ephemeris for 25 days:

```csharp
var moon = new CelestialBody(PlanetsAndMoons.Moon, Frame.ICRF, Time.J2000Tdb);
var sun = new CelestialBody(Stars.Sun);

// The Moon moves about the Earth under the attraction of both masses, so the Earth's central term is GM_Earth + GM_Moon:
// a field of degree 0 with the GM of the model is a point mass of that GM. With the Earth's GM alone, a massless probe
// started at the Moon's state would end about 54 310 km from the Moon after 25 days
double gmEarthMoon = PlanetsAndMoons.EarthBody.GM + moon.GM;
var earthMoonField = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 0, gmEarthMoon, 6378136.3)
{
    CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
};
var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, Time.J2000Tdb, earthMoonField);

var start = Time.J2000Tdb;
var end = start.AddDays(25);

var mission = new Mission("DeepSpaceTest");
var scenario = new Scenario("LunarOrbit", mission, new Window(start, end));

// Start at the Moon's position and velocity
var moonState = moon.GetEphemeris(start, earth, Frame.ICRF, Aberration.None);

var clock = new Clock("Clock", 256);
var spacecraft = new Spacecraft(-1001, "Probe", 100.0, moonState, 10000.0, clock);

scenario.AddSpacecraft(spacecraft);

// Add gravitational perturbations from multiple bodies
scenario.AddCelestialItem(sun);
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(new Barycenter(5));  // Jupiter barycenter
scenario.AddCelestialItem(new Barycenter(6));  // Saturn barycenter

// Propagate at a 5-minute step. Velocity-Verlet is of the second order: it ends about 470 m from an exact integration of
// these forces after 25 days
var summary = scenario.Simulate(new PropagationOptions
{
    Integrator = new VVIntegratorOptions { Step = TimeSpan.FromMinutes(5) },
    OutputStep = TimeSpan.FromMinutes(5)
});

// Compare the propagated Moon with the Moon of the ephemeris (DE440)
var propagatedMoon = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);
var ephemerisMoon = moon.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);

var positionDifference = (propagatedMoon.Position - ephemerisMoon.Position).Magnitude();
var velocityDifference = (propagatedMoon.Velocity - ephemerisMoon.Velocity).Magnitude();

Console.WriteLine($"Propagated Moon - DE440 Moon after 25 days:");
Console.WriteLine($"  Position difference: {positionDifference:F1} m");
Console.WriteLine($"  Velocity difference: {velocityDifference:F4} m/s");
```

The propagated Moon ends about 2.0 km from the Moon of DE440 after 25 days: 1.5 km are the forces the example leaves
out (the figures of the Earth and the Moon, the other planets, relativity), which an independent integration of the
example's forces (point masses with the kernel GMs, the bodies read from SPICE) ends at, and the rest is the error of
Velocity-Verlet at 5 minutes, 470 m from that integration (#704).

## See Also

- [Propagators](../reference/propagators.md#propagation-options) — the settings of `PropagationOptions`
- [Force Models](../reference/force-models.md) — physics equations and parameter tables
- [Thread Safety](thread-safety.md) — constraints for parallel propagation
