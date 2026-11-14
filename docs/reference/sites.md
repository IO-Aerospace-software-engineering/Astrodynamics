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

Both searches take a distance in m. CSPICE searches the distance of a site from the kernels to a spacecraft from the
kernels, and its illumination (`FindWindowsOnIlluminationConstraint`, `FindDayWindows`) whatever the source: its surface
point is its `Planetodetic`, the coordinates of its position at J2000 TDB (DSS-13 has moved 0.467 m since, 1.4 ms of a
sunrise at most). CSPICE's `gfilum_c` takes the normal of a point above the ellipsoid from `surfnm_c`, tilted from the
geodetic normal that the library's search takes (0.11″ at DSS-13, 1071 m up at 35.2° of latitude): there the sunrise
of the site from the kernels and that of the site built from its coordinates are 4.96 ms apart. Before 11.0.0 the
altitude reached CSPICE in km and the threshold of the distance in km/1000, so these searches were seconds off
([#488](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/488)) and 1000 times off
([#466](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/466)).

Both paths take every value of `RelationnalOperator` (#482). CSPICE has no `>=`, `<=` or `!=`: on its path
`GreaterOrEqual` and `LowerOrEqual` are searched as `Greater` and `Lower`, whose windows are closed, so for a quantity
that crosses the value they are the windows of the managed search, which bisects the relation itself; the two differ only
where the quantity stays equal to the value over an interval, which CSPICE's `>` leaves out. `NotEqual` is, on both
paths, the search window less the crossings of `Equal`, a window from each crossing to the next. Before 11.0.0 the three
operators ended the process on the CSPICE path (the native parser of the relation threw across P/Invoke); the same
applies to `SpiceAPI.FindWindowsOnDistanceConstraint`, `FindWindowsOnCoordinateConstraint` and
`FindWindowsOnIlluminationConstraint`, and to the command line (`<=`, `>=` and `!`).

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

`ToEquatorial()` gives the right ascension and declination in ICRF, whatever the frame of the state (#680). The
coordinate searches of `CoordinateSystem.RaDec` take them in their search frame instead, as CSPICE `gfposc`: the
declination in the site's frame is the elevation.

## See Also

- [Geometry & Visibility](../tutorials/geometry-and-visibility.md)
