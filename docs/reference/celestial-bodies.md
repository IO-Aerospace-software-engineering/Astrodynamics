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
| `HasAtmosphericModel` | Whether an atmospheric model was given (an Earth without one still has the NRLMSISE-00 default of its drag and getters) |
| `Albedo` | Bond albedo for reflected sunlight |
| `ThermalEffectiveTemperature` | Effective radiating temperature in Kelvin |
| `ThermalEmissivity` | Thermal emissivity |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `CelestialBody(NaifObject naifObject)` | Create from NAIF object |
| `CelestialBody(NaifObject, Frame, Time, GeopotentialModelParameters)` | Create with geopotential |
| `GeopotentialModelParameters(path, degree)` / `(path, degree, gravitationalParameter, referenceRadius)`, and the same with a `Stream` instead of the path | The geopotential of a body: its harmonics with the constants of EGM2008 for the Earth, of the kernels for another body, or the given ones; see [Force Models](force-models.md#geopotentialgravitationalfield). One instance builds any number of bodies, on any thread: the coefficients are read once and shared, each body with its own field (#674) |
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
of GM 0 and frame `DSS-13_TOPO`, about its body (`CenterOfMotionId` 399, `BarycenterOfMotionId` 3, `IsAsteroid` false;
before 11.0.0 an asteroid about the Sun, [#651](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/651)):
build a `Site` instead. The rule needs SPICE to name the body (identifier / 1000): SPICE's NOTO (398989) and NEW NORCIA
(398990), ESA stations of no body 398, are classed as any identifier above 1000; as observers of the reads by
identifiers they are objects of the kernels read by their identifier
([#750](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/750), see the
[SPICE API](spice-api.md)), while `new CelestialBody` and `CelestialItem.Create` of them still throw: they have no frame.

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
| `GetAtmosphere(double)` / `GetAtmosphere(AtmosphericContext)` | Get atmospheric data: from the configured atmospheric model of the body; for an Earth without one, NRLMSISE-00 at nominal space weather (the atmosphere of its drag) when the context holds the geodetic position and the epoch, and otherwise the U.S. Standard Atmosphere 1976 (`EarthStandardAtmosphere`), its base atmosphere, which needs the altitude only: `GetAtmosphere(double)` and every altitude-only or partial context (decided by S. Guillet, 2026-11-14); a null context, which holds no altitude, and another body without a model give a NaN temperature and a zero pressure and density. `GetAirDensity`, `GetAirPressure` and `GetAirTemperature` give the same values (#674) |

### Example

```csharp
// Inputs: epoch (Time)
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var state = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None).ToStateVector();
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
`stelab` (`stlabx` for transmission) of that position, with its rate from the observer's acceleration. A site given by
its coordinates as the target takes this path for every observer when the correction is not `None`, as `spkcpt_c`
corrects it ([#489](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/489),
[Light time of a site](sites.md#light-time-of-a-site)).

The corrected state is rotated to the output frame as `spkezr` rotates it
([#593](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/593)): an inertial frame, or a frame
centred on the observer (the topocentric frame of the observing site), at the epoch of the observation; any other
non-inertial frame at t − lt_c (t + lt_c for the transmission corrections), lt_c the light time from the observer to the
frame's centre by the method of the correction (that of the target when the centre is the target), with the derivative
of the rotation scaled by 1 ∓ dlt_c/dt, as CSPICE's `zzcorsxf`. The centre of a SPICE frame is SPICE's (`frinfo_c`); that
of the library's frames the site of a site frame, the spacecraft of a spacecraft frame, the Earth for TEME, CIRS and
TIRS. The centre may be a third object, neither the observer nor the target: the site of a site frame (a site given by
its coordinates included), the spacecraft of a spacecraft frame (a managed spacecraft with an attitude record or a
spacecraft from a kernel), a site of the kernels, a body, or any other object, read from SPICE by its identifier (a frame
given by name centred on a spacecraft), which throws `InvalidOperationException` where SPICE has no state of it, as
`spkezr` does. Measured against `spkezr`, `spkcpo` and `spkcpt` (locus `CENTER`): 1.7e-5 m and 1.1e-8 m/s in ITRF93 and the
topocentric frame of DSS-13, and in the frame of a site given by its coordinates centred on a third object; 2.4e-4 m in
MOON_ME centred on the target, the resolution of SPICE's own MOON_ME rotation within a few ulps of its epoch; 1.7e-3 m
in the frame of a spacecraft held as an attitude record of ITRF93, which the library reads at the 100 ns of the epoch of
its centre where CSPICE reads the double of that epoch (6e-8 s apart at most in 2024). Before 11.0.0 the frame was evaluated at the epoch of the observation: the Moon from
DSS-13 with `LT` 620 m off in ITRF93 and 1.48 km in MOON_ME, a site given by its coordinates 38 km off in its own
topocentric frame from the Moon. The CSPICE path (an observer and a target SPICE reads) is `spkezr`'s; in a frame only
the library holds (TEME, CIRS, TIRS, the frame of a site given by its coordinates, the frame of a managed spacecraft
with an attitude record), it reads J2000 and rotates by the same rule
([#739](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/739)): at t ∓ lt_c with the rate of
lt_c, lt_c the light time of the frame's centre from the observer (both read from the solar system barycenter, the
centre the frame's site or spacecraft, or the Earth from SPICE for TEME, CIRS and TIRS), at the epoch for a geometric
read and in a frame centred on the observer. Measured against `spkezr` in the frame of a site given by its coordinates
(an SPK and a TK frame of the site) and in the frame of a spacecraft held as an attitude record of ITRF93, the Moon from
the Earth and the Earth from the Moon: 2.7e-7 m and 7.6e-9 m/s, and 1.6e-3 m (the resolution of the record's epoch, as
above); TEME, CIRS and TIRS 0 from the ICRF read rotated at the epoch of the Earth, the closed form that reproduces
`spkezr` in ITRF93 to 2.4e-7 m. Before 11.0.0 this path rotated at the epoch of the observation whatever the correction:
the Earth from the Moon with `LT` 3.9e4 m off in TIRS and in those two frames, the Moon from the Earth 620 m in the frame
of a site and 670 m in that of a spacecraft, 4.3 mm in TEME and 0.9 mm in CIRS.

## See Also

- [Predefined Objects](predefined-objects.md)
- [Force Models](force-models.md)
