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

Available operators: `Greater`, `Lower`, `Equal` (with tolerance), their
negations `LowerOrEqual`, `GreaterOrEqual` and `NotEqual`, and the extremes
`AbsoluteMin`, `AbsoluteMax`, `LocalMin` and `LocalMax` (see
[Enumerations](../reference/enumerations.md#relationnaloperator)).

- **Step.** The step samples the quantity; a window, a gap or a crossing
  shorter than the step is found when the step is at most half the time
  between two extremes of the quantity (the library's search, for a
  propagated spacecraft or a site built from its coordinates). CSPICE, which
  searches objects of the kernels, asks the step to be shorter than the
  shortest interval where the quantity is monotone. The same rule holds for
  the coordinate and illumination constraints; see
  [Sites](../reference/sites.md#window-searches).

## Instrument field of view

Spacecraft instruments have a field of view: a circle, an ellipse or a
rectangle about their boresight. Use `FindWindowsInFieldOfViewConstraint` to
determine when a target is in it:

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

The method returns a collection of `Window` objects, in TDB, representing
contiguous intervals when the target is inside the instrument's field of view:
the windows of CSPICE's `gftfov`, to 1 µs.

- **Shape.** With `ShapeType.Ellipsoid` the target is in view as soon as any
  part of its ellipsoid (the radii of a `CelestialBody`, oriented by the frame
  given) overlaps the field; with `ShapeType.Point`, when its center is in it.
  Sites, spacecraft and stars are points.
- **Step.** The step samples a continuous margin of the target in the field; a
  window or a gap shorter than the step is found when the step is at most half
  the time between the extremes of that margin. CSPICE, which searches when the
  spacecraft and the target both come from kernels, asks the step to be
  shorter than every window and gap.
- **Accuracy.** Within a few microseconds of `gftfov`, except where the field
  grazes the limb of the target. A propagated spacecraft's kernels
  (`WriteEphemeris`, `WriteOrientation`) describe what the library reads,
  whatever its attitude and the frame of its initial state, so `gftfov` on
  them finds the same windows. See
  [Field-of-view search](../reference/spacecraft.md#field-of-view-search).

### Setting up an instrument

Instruments are added to a spacecraft by `AddCircularInstrument`,
`AddEllipticalInstrument` or `AddRectangularInstrument`. Each has a NAIF
identifier, a name, a model, its half-angles, and three vectors: the boresight
and the reference vector in the instrument frame, and the orientation of the
instrument frame in the spacecraft frame:

```csharp
spacecraft.AddCircularInstrument(
    naifId: -1001001,
    name: "Camera",
    model: "Imager",
    fieldOfView: 5.0 * Constants.Deg2Rad,       // half-angle
    boresight: Vector3.VectorZ,                 // +Z of the instrument frame
    refVector: Vector3.VectorX,
    orientation: new Vector3(0.0, Constants.PI2, 0.0));
```

The orientation is not a direction: it is the Euler angles (x, y, z), in
radians, of the frame kernel the library writes for the instrument, as NAIF's
Frames Required Reading defines them in "Defining a TK Frame Using Euler Angles"
(https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/frames.html): `pxform(instrument, spacecraft)` =
`eul2m(x, y, z, 1, 2, 3)`. Here a rotation of the frame by π/2 about Y puts
the boresight, +Z of the instrument, along −X of the spacecraft;
the instrument's `GetBoresightInSpacecraftFrame()` returns it. A zero orientation keeps
the instrument frame on the spacecraft frame. See
[Instrument orientation](../reference/spacecraft.md#instrument-orientation).

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
windows: the spacecraft seen from the site, its declination in the site's
topocentric frame being its elevation:

```csharp
var visibleWindows = spacecraft.FindWindowsOnCoordinateConstraint(
    searchWindow,
    mySite,
    mySite.Frame,
    CoordinateSystem.RaDec,
    Coordinate.Declination,
    RelationnalOperator.Greater,
    10.0 * Constants.Deg2Rad,
    0.0,
    Aberration.LT,
    TimeSpan.FromMinutes(1));
```

Each rise and set is located to 1 µs. The culmination of a pass is the same
search over the pass with `RelationnalOperator.LocalMax` (the value is then
ignored): a window of zero length at the maximum elevation.

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
- Instrument FOV analysis uses `FindWindowsInFieldOfViewConstraint`: the
  windows of CSPICE's `gftfov`, with the target's shape, for every field shape.
- Ground sites provide horizontal coordinates (azimuth, elevation, range).
- TLE-based spacecraft work with all geometry functions through SGP4/SDP4
  propagation.
