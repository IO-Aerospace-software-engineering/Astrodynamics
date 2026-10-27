# Celestial Bodies

## CelestialBody

`CelestialBody` represents a planet, moon, or star with physical properties and ephemeris access.

### Properties

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID code |
| `Name` | Body name |
| `GM` | Gravitational parameter (m³/s²) |
| `Mass` | Mass (kg) |
| `EquatorialRadius` | Equatorial radius (m) |
| `PolarRadius` | Polar radius (m) |
| `Flattening` | Flattening coefficient |
| `Frame` | Body-fixed reference frame |
| `J2` | Second zonal harmonic (oblateness) |
| `J3` | Third zonal harmonic |
| `J4` | Fourth zonal harmonic |
| `SphereOfInfluence` | Sphere of influence radius (m) |
| `GravitationalField` | Gravity model (point-mass or geopotential) |
| `HasAtmosphericModel` | Whether an atmospheric model is attached |
| `Albedo` | Bond albedo for reflected sunlight |
| `ThermalEffectiveTemperature` | Effective radiating temperature in Kelvin |
| `ThermalEmissivity` | Thermal emissivity |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `CelestialBody(NaifObject naifObject)` | Create from NAIF object |
| `CelestialBody(NaifObject, Frame, Time, GeopotentialModelParameters)` | Create with geopotential |
| `GeopotentialModelParameters(path, degree)` / `(path, degree, gravitationalParameter, referenceRadius)`, and the same with a `Stream` instead of the path | The geopotential of a body: its harmonics with the constants of EGM2008 for the Earth, of the kernels for another body, or the given ones; see [Force Models](force-models.md#geopotentialgravitationalfield) |
| `GeopotentialModelParameters.CentralTerm` (`init`) | The GM of the central term: the body's (`GeopotentialCentralTerm.BodyGravitationalParameter`, the default) or the model's (`ModelGravitationalParameter`, as GMAT applies a `.cof` header); see [Force Models](force-models.md#the-central-term-with-the-gm-of-the-model) |
| `CelestialBody(NaifObject, geopotentialModelParameters = null, atmosphericModel = null, albedo = 0, thermalEffectiveTemperature = 0, thermalEmissivity = 0)` | Create with a geopotential, an atmosphere and radiation properties (named arguments for the last three, e.g. `albedo: 0.3`) |

### Methods

| Method | Description |
|--------|-------------|
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state at epoch |
| `GetEphemeris(Window, ILocalizable, Frame, Aberration, TimeSpan)` | Get states over window |
| `GetOrientation(Frame, Time)` | Get body orientation |
| `GeosynchronousOrbit(double, double, Time)` | Compute geosynchronous orbit, of the sidereal rotation period of the body frame at the epoch |
| `GeosynchronousOrbit(double, double, Time, TimeSpan)` | Compute geosynchronous orbit of a declared period |
| `HelioSynchronousOrbit(double, double, Time)` | Compute helio-synchronous orbit |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation/eclipse windows ([Occultations](occultations.md)) |
| `AngularSeparation(Time, ILocalizable, ILocalizable, Aberration)` | Compute angular separation |
| `SubObserverPoint(CelestialBody, Time, Aberration)` | Get sub-observer point |
| `GetAtmosphere(double)` / `GetAtmosphere(IAtmosphericContext)` | Get atmospheric data |

### Example

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var state = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
```

## CelestialItem

`CelestialItem` is the shared base abstraction for natural bodies, spacecraft, and other localizable objects.

| Method | Description |
|--------|-------------|
| `GetGeometricStateRelativeTo(Time, CelestialItem)` | Get geometric state relative to a reference body in ICRF |
| `AngularSize(double distance)` | Compute angular diameter |
| `IsOcculted(CelestialItem, OrbitalParameters, Aberration)` | Type of occultation at an instant ([Occultations](occultations.md)) |
| `WriteEphemeris(FileInfo)` | Write ephemeris to SPK file |

The implementation uses a reference-body strategy for relative ephemerides to avoid numerical issues from subtracting large SSB vectors.

## See Also

- [Predefined Objects](predefined-objects.md)
- [Force Models](force-models.md)
