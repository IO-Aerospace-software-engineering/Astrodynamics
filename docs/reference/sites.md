# Sites

## Site

Ground-based observation or tracking site.

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Site(int id, string name, CelestialBody body)` | Create from SPICE data |
| `Site(int id, string name, CelestialBody body, Planetodetic coordinates)` | Create with explicit coordinates |

### Properties

| Property | Description |
|----------|-------------|
| `Id` | Site identifier |
| `Name` | Site name |
| `NaifId` | NAIF ID (bodyId * 1000 + siteId) |
| `CelestialBody` | Parent body |

### Methods

| Method | Description |
|--------|-------------|
| `GetHorizontalCoordinates(Time, ILocalizable, Aberration)` | Get azimuth, elevation, range |
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get site state relative to observer |
| `AngularSeparation(Time, ILocalizable, ILocalizable, Aberration)` | Angular separation |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation windows |
| `FindWindowsOnIlluminationConstraint(...)` | Find illumination windows |
| `FindDayWindows(Window, double)` | Find daylight windows |

### Example

```csharp
var goldstone = new Site(13, "DSS-13", earth);
var mySite = new Site(100, "MySite", earth,
    new Planetodetic(-117.0 * Constants.Deg2Rad, 34.0 * Constants.Deg2Rad, 1000.0));
var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);
```

## LaunchSite

Specialized site for launch operations.

| Constructor | `LaunchSite(int id, string name, CelestialBody body, IEnumerable<AzimuthRange> allowedAzimuths)` |
| Method | `IsAzimuthAllowed(double azimuth)` — check if launch azimuth is allowed |

## Coordinates

### Planetodetic

| Property | Description |
|----------|-------------|
| `Longitude` | Longitude (rad) |
| `Latitude` | Latitude (rad) |
| `Altitude` | Altitude above ellipsoid (m) |

`Planetodetic.ToPlanetocentric(flattening, equatorialRadius)` and `Planetocentric.ToPlanetodetic(flattening,
equatorialRadius)` convert between geodetic and planetocentric coordinates on the ellipsoid of revolution of the body
(its equatorial radius and flattening, from the PCK), exactly at any altitude: the geodetic latitude is that of the
normal to the ellipsoid through the point, and the altitude is measured along it. Oblate, spherical and prolate bodies
are covered; the flattening must be below 1. The conversion from Cartesian coordinates is the closed form of Vermeille
(2002) with the changes of Karney (2011), without iteration; both directions agree with CSPICE `georec_c`/`recgeo_c` to
about 1e-15 (see [Validation](../guides/validation.md#reference-validations)). A custom site is placed by this
conversion on the ellipsoid of its body; the topocentric frame of a site is built from its geodetic coordinates on that
same ellipsoid, so it can differ slightly from a frame kernel built on another spheroid (DSS-13: 9.5e-7° in latitude
between the `pck00011` ellipsoid and the WGS84 spheroid of `earth_topo_201023.tf`).

### Horizontal

| Property | Description |
|----------|-------------|
| `Azimuth` | Azimuth from north (rad) |
| `Elevation` | Elevation above horizon (rad) |
| `Range` | Distance to target (m) |

### Equatorial

| Property | Description |
|----------|-------------|
| `RightAscension` | RA (rad) |
| `Declination` | Dec (rad) |
| `Distance` | Distance from observer (m) |

## See Also

- [Geometry & Visibility](../tutorials/geometry-and-visibility.md)
