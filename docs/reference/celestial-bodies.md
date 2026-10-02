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
| `CelestialBody(NaifObject, albedo, thermalEffectiveTemperature, thermalEmissivity)` | Create with radiation properties |

### Methods

| Method | Description |
|--------|-------------|
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state at epoch |
| `GetEphemeris(Window, ILocalizable, Frame, Aberration, TimeSpan)` | Get states over window |
| `GetOrientation(Frame, Time)` | Get body orientation |
| `GeosynchronousOrbit(double, double, Time)` | Compute geosynchronous orbit |
| `HelioSynchronousOrbit(double, double, Time)` | Compute helio-synchronous orbit |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation/eclipse windows |
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
| `IsOcculted(CelestialItem, OrbitalParameters, Aberration)` | Check if occulted |
| `WriteEphemeris(FileInfo)` | Write ephemeris to SPK file |

The implementation uses a reference-body strategy for relative ephemerides to avoid numerical issues from subtracting large SSB vectors.

## See Also

- [Predefined Objects](predefined-objects.md)
- [Force Models](force-models.md)
