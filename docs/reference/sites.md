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
| `FindWindowsOnCoordinateConstraint(...)` | Find coordinate constraint windows of this site seen from an observer; for the visibility of a spacecraft, search the spacecraft from the site: `spacecraft.FindWindowsOnCoordinateConstraint(window, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater, mask, 0.0, aberration, step)` (the declination in the site's frame is the elevation) |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation windows ([Occultations](occultations.md)) |
| `FindWindowsOnIlluminationConstraint(...)` | Find illumination windows |
| `FindDayWindows(Window, double)` | Find daylight windows |

### Window searches

A search on a site built from its coordinates, or on a target that is not in the kernels, is the library's
(`GeometryFinder`); CSPICE's `gf*` routines search kernel objects. The library samples the quantity at every step from
the start of the search window and at its end, and locates each transition to 1 µs; a window that holds at a bound of
the search window starts or ends there. `LocalMin` and `LocalMax` give the extremes inside the search window, and
`AbsoluteMin` and `AbsoluteMax` the extreme over it, its bounds included, with `adjustValue` the windows within it of the
extreme; `Equal` gives the crossings of the value, as windows of zero length. As with CSPICE, the step must be shorter
than the shortest window, the shortest gap and half the time between two extremes.

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
