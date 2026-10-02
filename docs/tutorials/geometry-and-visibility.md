# Geometry and Visibility

This tutorial covers geometric analysis: eclipse and occultation searches,
distance constraints, instrument field-of-view checks, and ground station
operations.

## Occultation and eclipse windows

An **occultation** occurs when one body blocks the view of another. Eclipse
searches are a special case where the Sun is the occulted body.

Use `FindWindowsOnOccultationConstraint` on any celestial body to search for
time windows when the geometry is satisfied:

```csharp
// Find lunar eclipses (Earth occults the Moon as seen from the Sun)
var lunarEclipses = sun.FindWindowsOnOccultationConstraint(
    searchWindow,
    occultingBody: earth,
    occultingShape: ShapeType.Ellipsoid,
    occultedBody: moon,
    occultedShape: ShapeType.Ellipsoid,
    occultationType: OccultationType.Any,
    aberration: Aberration.None,
    stepSize: TimeSpan.FromHours(1));
```

The `occultationType` parameter controls what you are looking for:

| Value | Meaning |
|-------|---------|
| `OccultationType.Full` | Target completely hidden |
| `OccultationType.Partial` | Target partially hidden |
| `OccultationType.Annular` | Occulting body inside target disk |
| `OccultationType.Any` | Any of the above |

The `stepSize` sets the initial search grid. Use a step smaller than the
shortest event you expect to find.

### Spacecraft eclipse windows

To find when a spacecraft enters Earth's shadow:

```csharp
var eclipseWindows = spacecraft.FindWindowsOnOccultationConstraint(
    searchWindow,
    occultingBody: earth,
    occultingShape: ShapeType.Ellipsoid,
    occultedBody: sun,
    occultedShape: ShapeType.Ellipsoid,
    occultationType: OccultationType.Any,
    aberration: Aberration.None,
    stepSize: TimeSpan.FromMinutes(5));
```

## Distance constraints

Search for time windows when the distance between two bodies satisfies a
relational constraint:

```csharp
// Find when the Moon is farther than 400,000 km from Earth
var farMoonWindows = earth.FindWindowsOnDistanceConstraint(
    searchWindow,
    moon,
    RelationnalOperator.Greater,
    400000000,          // meters
    Aberration.None,
    TimeSpan.FromHours(24));
```

Available operators: `Greater`, `Less`, `Equal` (with tolerance), and their
negations.

## Instrument field of view

Spacecraft instruments have a defined field-of-view cone. Use
`FindWindowsInFieldOfViewConstraint` to determine when a target body enters
that cone:

```csharp
var visibilityWindows = spacecraft.Instruments.First()
    .FindWindowsInFieldOfViewConstraint(
        window,
        spacecraft,
        earth,
        earth.Frame,
        ShapeType.Ellipsoid,
        Aberration.LT,
        TimeSpan.FromSeconds(60));
```

The method returns a collection of `Window` objects representing contiguous
intervals when the target is inside the instrument's field of view.

### Setting up an instrument

Instruments are attached to a spacecraft at construction. Each instrument has a
name, boresight direction, field-of-view shape, and angular half-width:

```csharp
var instrument = new Instrument(
    naifId: -1001,
    name: "Camera",
    shape: InstrumentShape.Circular,
    orientation: new Vector3(0, 0, 1),   // boresight along +Z
    fovHalfAngle: 5.0 * Constants.Deg2Rad);
```

## Ground site operations

### Kernel-backed sites

Sites registered in SPICE kernels are created by NAIF ID:

```csharp
var goldstone = new Site(13, "DSS-13", earth);
```

### Custom sites

For sites not in any kernel, supply geodetic coordinates:

```csharp
var mySite = new Site(100, "MySite", earth,
    new Planetodetic(
        -117.0 * Constants.Deg2Rad,   // longitude (radians)
         34.0 * Constants.Deg2Rad,    // latitude  (radians)
         1000.0));                    // altitude  (meters)
```

### Horizontal coordinates

Compute azimuth and elevation of a target as seen from a ground site:

```csharp
var horizontal = goldstone.GetHorizontalCoordinates(
    epoch, moon, Aberration.LT);

Console.WriteLine($"Azimuth:   {horizontal.Azimuth * Constants.Rad2Deg:F2} deg");
Console.WriteLine($"Elevation: {horizontal.Elevation * Constants.Rad2Deg:F2} deg");
Console.WriteLine($"Range:     {horizontal.Range / 1000.0:F1} km");
```

Objects below the horizon have negative elevation.

### Site visibility windows

Combine horizontal coordinates with an elevation mask to find visibility
windows:

```csharp
var visibleWindows = mySite.FindWindowsOnCoordinateConstraint(
    searchWindow,
    spacecraft,
    mySite.Frame,
    CoordinateSystem.RaDec,
    Coordinate.Declination,
    RelationnalOperator.Greater,
    10.0 * Constants.Deg2Rad,
    0.0,
    Aberration.LT,
    TimeSpan.FromMinutes(1));
```

## TLE-based satellite tracking

For operational satellites you often have TLE data rather than a full
ephemeris. Propagate the TLE and query geometry from a ground site:

```csharp
// Parse TLE
var tle = new TLE(
    "ISS (ZARYA)",
    "1 25544U 98067A   24100.50000000  .00016717  00000-0  10270-3 0  9005",
    "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.49750667  1007");

// Create spacecraft from TLE
var iss = new Spacecraft(-25544, "ISS", tle);

// Track from ground site
var coords = mySite.GetHorizontalCoordinates(epoch, iss, Aberration.None);
```

!!! note
    TLE-propagated spacecraft use the SGP4/SDP4 propagator automatically.
    Mean orbital elements from a TLE cannot be converted to state vectors
    directly --- use `TLE.ToOsculating()` if you need a `StateVector`.

## Summary

- Use `FindWindowsOnOccultationConstraint` for eclipse and occultation searches.
- Use `FindWindowsOnDistanceConstraint` for range-based event detection.
- Instrument FOV analysis uses `FindWindowsInFieldOfViewConstraint`.
- Ground sites provide horizontal coordinates (azimuth, elevation, range).
- TLE-based spacecraft work with all geometry functions through SGP4/SDP4
  propagation.
