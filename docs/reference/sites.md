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
| `FindWindowsOnIlluminationConstraint(...)` | Find the windows of the incidence angle of a source ([Illumination](#illumination)) |
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

#### Planetographic coordinates

`CoordinateSystem.Planetographic` is CSPICE's PLANETOGRAPHIC on both paths ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682)): the longitude, the latitude
and the altitude of `recpgr_c` ([header](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/recpgr_c.html),
Detailed_Output and Particulars), `gfposc_c`'s coordinates (Detailed_Input `coord`):

- the latitude and the altitude are the geodetic ones on the spheroid of the body (its equatorial radius and
  flattening), those of `Geodetic`;
- the longitude is in [0, 2π), positive west (from +X toward −Y) for a body whose spin is prograde, positive east for a
  retrograde spin and for the Earth, the Moon and the Sun; the spin is read as `recpgr_c` reads it, from the sign of the
  rate of the prime meridian W, which the managed path takes from the angular velocity of the body's frame at the start
  of the search (prograde for Mercury, Mars, Pluto; retrograde for Venus and Uranus);
- `Coordinate.Radius` is not one of them and throws `ArgumentOutOfRangeException` on the managed path.

The managed search works in the body-fixed frame of the observer's body, as for `Geodetic`; `gfposc_c` takes the body of
the frame's centre, so the two agree for a frame centred on the observer. A kernel can override the sense of the
longitude of a body for CSPICE with `BODY<id>_PGR_POSITIVE_LON` (GF Required Reading, "Meridian crossing"); the managed
path does not read it, and no kernel of the library defines it. Measured against `gfposc_c` (the Moon from the Earth in
ITRF93, the Earth and the Sun from Mercury, the Earth from Venus, DSS-13 from Mercury and from the Moon): the same window
counts, the edges on the threshold within 4e-7 s; an edge where the longitude jumps from 2π to 0 lies within 6.6e-11
rad of the cut on both paths, its time within that angle over the rate of the longitude (up to 2.4e-4 s for the Sun
from Mercury, which moves 7e-8 rad/s). Before 11.0.0 the managed `Planetographic` was the planetocentric longitude in
(−π, π], latitude and radius.

#### Illumination

`FindWindowsOnIlluminationConstraint` searches the incidence angle of a source at the site. The emission and phase angles
are measured at the point towards an observer (`gfilum_c` Particulars), which the search does not take: they throw
`ArgumentException` ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682); before 11.0.0 the managed path measured them from the site itself, 0, and
`gfilum_c` was given the site as its observer). With an observer, search them with
`SpiceAPI.FindWindowsOnIlluminationConstraint` and the observer's NAIF id, or with the library's search on the angle:

```csharp
var windows = new GeometryFinder().FindWindowsWithCondition(window,
    t => site.IlluminationEmission(t, moon, Aberration.None), RelationnalOperator.Lower, Constants.PI2, step);
```

which is within 4.4 ms of `gfilum_c` from the Moon at DSS-13 (the normal above the ellipsoid, as for the incidence).
The method is ELLIPSOID, the only one `gfilum_c` supports (Detailed_Input `method`), whatever its case and its leading
or trailing whitespace; any other, DSK forms included, throws `ArgumentException` on both paths (before 11.0.0, no
window on the CSPICE path and the ellipsoid on the managed one).

#### Reads in the site's frame

A site built from coordinates has a frame only the library holds (`SiteFrame`): an ephemeris read in it is read in
J2000 and rotated by it, from any observer ([#682](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/682), as [TEME, CIRS and TIRS](frames.md#teme)); before 11.0.0
a read from an observer other than the site's body threw SPICE(UNKNOWNFRAME). The rotation is that of the site's own
coordinates, also when its name is that of a station whose frame a loaded kernel defines (`new Site(13, "DSS-13",
earth, coordinates)` and `DSS-13_TOPO`); the CSPICE coordinate search in that frame (a kernel spacecraft taking part)
returns no window ([#558](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/558)).

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
