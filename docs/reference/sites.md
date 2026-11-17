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
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get site state relative to observer: a site of the kernels seen from a body, a barycenter or a kernel spacecraft by `spkezr_c`; a site given by its coordinates corrected as `spkcpt_c` corrects it, the site itself at t ∓ lt ([Light time of a site](#light-time-of-a-site)) |
| `AngularSeparation(Time, ILocalizable, ILocalizable, Aberration)` | Angular separation |
| `FindWindowsOnDistanceConstraint(...)` | Find distance constraint windows |
| `FindWindowsOnCoordinateConstraint(...)` | Find coordinate constraint windows of this site seen from an observer; for the visibility of a spacecraft, search the spacecraft from the site: `spacecraft.FindWindowsOnCoordinateConstraint(window, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination, RelationnalOperator.Greater, mask, 0.0, aberration, step)` (the declination in the site's frame is the elevation) |
| `FindWindowsOnOccultationConstraint(...)` | Find occultation windows ([Occultations](occultations.md)) |
| `FindWindowsOnIlluminationConstraint(...)` | Find the windows of the incidence angle of a source ([Illumination](#illumination)) |
| `FindDayWindows(Window, double)` | Find daylight windows |
| `WriteFrameAsync(FileInfo)` | Write the frame kernel of the topocentric frame `<NAME>_TOPO`; its kernel variable `FRAME_<NAME>_TOPO` follows SPICE's rules (Kernel Required Reading, "Variable Name Rules"): no blank, tab or other whitespace or control character, quote, parenthesis, comma or '=', 32 bytes at most in UTF-8, so 21 ASCII characters for the name; another name throws `InvalidOperationException` and nothing is written (#676). For a site from the kernels whose frame id the loaded kernels define, that kernel frame under its kernel name, as a TK matrix of the body frame; one not fixed in the body frame throws `InvalidOperationException` (#690) |

### Window searches

A search on a site built from its coordinates, or on a target that is not in the kernels, is the library's
(`GeometryFinder`); CSPICE's `gf*` routines search kernel objects. The library samples the quantity at every step from
the start of the search window and at its end, and locates each transition to 1 µs; a window that holds at a bound of
the search window starts or ends there. `LocalMin` and `LocalMax` give the extremes inside the search window, and
`AbsoluteMin` and `AbsoluteMax` the extreme over it, its bounds included, with `adjustValue` the windows within it of the
extreme; `Equal` gives the crossings of the value, as windows of zero length.

Between the samples the library looks for the extremes of the quantity that the samples hide, as the occultation search
does ([Occultations](occultations.md)): where three consecutive samples show an extreme of the quantity that does not
reach the value, Brent's `localmin` locates it between their two neighbors (the first and last intervals likewise), and
an extreme beyond the value becomes a sample. Every window, gap and crossing is therefore found when the step is at most
half the time between two extremes of the quantity, a jump of a branch cut (a right ascension, a longitude, an azimuth)
counting as one, down to the resolution of `localmin`, about 2 √ε h + 2τ/3 for a step h and τ = 1 µs (2.5 µs at a
1 min step, 19 µs at 10 min, 41 µs at 22.5 min: a narrower window or gap can be missed, and a tangency between two
samples, an extreme exactly at the value, is not found; #668): the 2 passes of about 3 min of a LEO
above 30° from DSS-13 at a 20 min step, as CSPICE finds them. CSPICE asks for a step shorter than the shortest interval
where the quantity is monotone ([`gfdist_c`](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/gfdist_c.html),
Particulars, "The Search Process" and "Step Size"; the same in `gfposc_c` and `gfilum_c`, where a longitude or a right
ascension needs its sine or cosine monotone); neither rule depends on the length of the windows. A `bool` quantity given
to `GeometryFinder.FindWindowsWithCondition` has nothing to search between its samples: its step must be shorter than
every window and every gap. Before 11.0.0 the library decided each transition from the samples alone, and lost the
windows, gaps and crossings between two samples
([#678](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/678)).

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
counts, the edges on the threshold within 4e-7 s (the latitude and the altitude of DSS-13 from the Moon; the
longitudes within 3e-7 s); an edge where the longitude jumps from 2π to 0 lies within 6.6e-11
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
// Inputs: window (Window), step (TimeSpan), site (Site), moon (CelestialBody)
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
earth, coordinates)` and `DSS-13_TOPO`). A coordinate search in that frame is the library's, also when a kernel
spacecraft takes part (third review of [#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690); before, CSPICE's `gfposc_c` got the frame by name and returned
no window, or for a site named as a kernel frame searched in that kernel frame, [#558](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/558)), and
`SpiceAPI.FindWindowsOnCoordinateConstraint` throws `ArgumentException` for it unless a loaded kernel defines its id
under its name. `SpiceAPI.TransformFrame` with it is the library's frame composed through ICRF, as for TEME: the
orientation `ToFrame` uses, bit for bit, within 9.3e-16 rad and 5.7e-20 rad/s of SPICE's `sxform_c` in the frame kernel
`WriteFrameAsync` writes for the site (the same on Linux x64, Windows x64 and macOS arm64, CI run 43773937161; fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), before, it threw).

A site from the kernels (`new Site(13, "DSS-13", earth)`) has the frame the loaded kernels define for its id,
1000000 + NaifId (`DSS-13_TOPO`, 1399013, of `earth_topo_201023.tf`), on every path
([#690](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/690)): SPICE reads it by the name the
kernels give that id, whatever the name of the site (`new Site(13, "GOLDSTONE", earth)` reads `DSS-13_TOPO`), and the
library rotates by it (`ToFrame(site.Frame)`, `GetHorizontalCoordinates`, the managed coordinate searches), read from
SPICE at each call. Before 11.0.0 the library rotated by a frame it built from `Planetodetic`, the position of the
station at J2000 TDB on the `pck00011` ellipsoid, while SPICE read the kernel frame by name: the two departed by
7.8e-8 rad for DSS-13, most of it the epoch (0.41 m of plate motion between J2000 and the 2020-10-23 of the frame
kernel, 0.47 m by 2024), 2.1e-9 rad of it the spheroid; a site named otherwise than its kernel frame threw
SPICE(UNKNOWNFRAME) on a read by name. A site from the kernels whose frame no loaded kernel defines keeps the frame of
its `Planetodetic`, and an ephemeris read in it is read in J2000 and rotated by that frame, as for a site built from
coordinates (third review of #690; before, its own name, `Name + "_TOPO"`, reached `spkezr_c`, which threw
SPICE(UNKNOWNFRAME), while `ToFrame` and `GetHorizontalCoordinates` used that frame): within 7.4e-16 in position and
5.9e-16 rad in frame of SPICE in a frame kernel of NAIF's topocentric convention on its `Planetodetic` (measured on Linux
x64). The choice is made at each read, so a frame kernel of its id loaded later is read by name. A coordinate search
in that frame is the library's, also when a kernel spacecraft takes part, as in the frame of a site built from
coordinates: within 5.0e-7 s of `gfposc_c` in that frame kernel for a site at DSS-13's coordinates seen from a LEO
(measured on Linux x64; before, `gfposc_c` got the site's own name and returned no window), and CSPICE's once a frame
kernel of its id is loaded; `SpiceAPI.FindWindowsOnCoordinateConstraint` with it throws `ArgumentException`, and
`SpiceAPI.TransformFrame` with it composes that frame through ICRF, as for a site built from coordinates (fourth round
of #668; before, it threw). `Planetodetic` and the illumination
angles do not move: `IlluminationIncidence` and `IlluminationEmission` take the normal of the ellipsoid at
`Planetodetic`, the normal `gfilum_c` takes for the site in `FindWindowsOnIlluminationConstraint` (#488), not the
zenith of the kernel frame. `WriteFrameAsync` of such a site writes that kernel frame: its name and id in the
kernels, and its rotation to the frame of the body read from SPICE, as `TKFRAME_<id>_MATRIX` (column order, one column
per line, CSPICE reading 132 characters of a line), so that the file defines the frame the kernels define to the
rounding of the matrix (2.1e-16 rad for DSS-13); loaded beside those kernels it assigns their frame again. A kernel
frame not fixed in the frame of the body (read at J2000 TDB and 6 h later) cannot be written as a TK frame of the body
and throws `InvalidOperationException`. Before 11.0.0 the writer wrote the frame of `Planetodetic` under the kernel's
id, 7.76e-8 rad from `DSS-13_TOPO`, and for a site named otherwise renamed the frame of that id. To keep the frame of
the coordinates at J2000, build the site from them: `new Site(id, name, body, kernelSite.Planetodetic)`.

### Light time of a site

A site of the kernels seen from a body, a barycenter or a spacecraft of the kernels is read by CSPICE (`spkezr_c`),
which corrects it. A site given by its coordinates is corrected by the library as CSPICE's `spkcpt_c` corrects it, for
every observer, its own body included: the site itself is taken at t − lt (t + lt for the transmission corrections), its
position fixed in the body-fixed frame of its body turned to ICRF at that epoch, lt the light time of `spkltc` (one
iteration for `LT` and `XLT`, converged for `CN` and `XCN`), the velocity scaled by the rate of the light time, and for
the `+S` corrections the stellar aberration of `stelab` (`stlabx` for transmission) from the observer's velocity
([#489](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/489); the reference-body path of
[Celestial bodies](celestial-bodies.md)). It agrees with `spkcpt_c` to 2.4e-5 m and 3.7e-9 m/s from the Moon, the Earth
and LRO, the sites at the poles and on the equator included, and to 6.8e-5 m from the Sun and the Mars barycenter.
Without correction the state is unchanged, bit for bit. The window searches, `SubObserverPoint` and `IsInFOV` of such a
site read it this way: the 400000 km window of the example below, with `LT`, is within 1e-7 s of the window of
`spkcpt_c`.

Before 11.0.0 the correction of a site given by its coordinates, seen from an observer of the kernels, was that of the
observer seen from the site's body (`OrbitalParameters.RelativeTo`), the site never moved back in time: 82.7 km from
`spkcpt_c` seen from the Moon with `LT`, about 15 000 km from the Sun, the geometric state seen from its own body (640 m
with `LT`), and the window of the example about 340 s wider at each end. `OrbitalParameters.RelativeTo(ILocalizable,
Aberration)` keeps its behaviour: it holds one state, not the trajectory of the object, so its correction applies to the
new origin as seen from the current one (decided by S. Guillet, 2026-11-17).

### Example

```csharp
// Inputs: earth and moon (CelestialBody), epoch (Time)
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
conversion on the ellipsoid of its body; the topocentric frame of a site built from coordinates is built from them on
that same ellipsoid, so it can differ slightly from a frame kernel built on another spheroid (DSS-13: 9.5e-7° in
latitude between the `pck00011` ellipsoid and the WGS84 spheroid of `earth_topo_201023.tf`, 2.1e-9 rad of the axes). A
site from the kernels has the kernel frame of its id ([Reads in the site's frame](#reads-in-the-sites-frame), #690).

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
