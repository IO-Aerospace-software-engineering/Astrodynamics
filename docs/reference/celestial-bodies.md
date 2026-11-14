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

A body built from its identifier (`new CelestialBody(id)`, `CelestialItem.Create(id)`) reads its constants from the
loaded kernels through the data provider (`SpiceAPI.GetCelestialBodyInfo` for the default `SpiceDataProvider`):

- its name, which SPICE gives (`bodc2n_c`, built-in names or a kernel's `NAIF_BODY_NAME`); an identifier SPICE does not
  name throws `ArgumentException` (#672), as one that is not a celestial body does (#560);
- its GM from `BODYnnn_GM`, 0 when the kernel pool has none (Elara, Nereid: 39 bodies of `pck00011.tpc` have radii and
  no GM in `gm_de440.tpc`);
- its radii from `BODYnnn_RADII`, 0 when the pool has none (Pallas, Nix, Didymos), the flattening then 0;
- its body-fixed frame, the one `cnmfrm_c` associates with its name; a body without one (Kerberos, Styx) throws
  `InvalidOperationException`.

Before #672 a missing `BODYnnn_RADII` or `BODYnnn_GM` left the GM 0 and the frame undefined, so Pallas could not be
built and Elara was built with a garbage frame name. A site identifier (399013, DSS-13) builds a body named after the site,
of GM 0 and frame `DSS-13_TOPO`, classed as an asteroid around the Sun
([#651](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/651)): build a `Site` instead.

### Methods

| Method | Description |
|--------|-------------|
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state at epoch |
| `GetEphemeris(Window, ILocalizable, Frame, Aberration, TimeSpan)` | Get states over window: every step from the start, in the window's calendar, up to its end (across a leap second the end bounds them, #682) |
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

`Satellites` lists the items whose initial orbital parameters have the item as observer: the instances, by reference,
held weakly, so that a spacecraft referenced nowhere else leaves it when the garbage collector collects it, and safe
across threads, constructions about one body on several threads included (#676). Two items of one NAIF id are both
listed; an item is listed at the end of its constructor, after every check, so a constructor that throws lists nothing;
the order of the enumeration is not specified, and `Count` enumerates. A natural body or a barycentre is listed by its
observer, a spacecraft or a body of custom parameters by a `CelestialBody` observer only. Before 11.0.0 it was a set
compared by NAIF id, without a lock, that kept its items alive.

The implementation uses a reference-body strategy for relative ephemerides to avoid numerical issues from subtracting large SSB vectors.
When CSPICE cannot read the pair (an observer or a target that is a site, a spacecraft not from a kernel or a star), the
library applies the aberration corrections itself, as CSPICE's `spkaps` does
([#491](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/491)): the light time of `spkltc`, one
iteration for `LT` and `XLT`, converged for `CN` and `XCN`, the target taken at t − lt (t + lt for the transmission
cases) and the velocity scaled by the rate of the light time; for the `+S` corrections, the stellar aberration of
`stelab` (`stlabx` for transmission) of that position, with its rate from the observer's acceleration. The state is
rotated to the output frame at the epoch of the observation: a non-inertial frame centred on another body than the
observer is not evaluated at t − lt of its centre, as `spkezr` does
([#593](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/593)); an inertial frame or the
topocentric frame of the observing site is not affected.

## See Also

- [Predefined Objects](predefined-objects.md)
- [Force Models](force-models.md)
