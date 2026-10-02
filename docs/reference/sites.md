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
