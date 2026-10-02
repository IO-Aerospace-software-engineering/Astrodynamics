# Coordinate Conversions

This tutorial covers reference frame transforms, Earth orientation, and coordinate representations. You will learn to convert between inertial, ecliptic, and body-fixed frames, extract equatorial coordinates, and work with planetodetic positions.

## Frame Transform Convention

All frame transforms go through the International Celestial Reference Frame (ICRF) as the pivot. When you call `ToFrame`, the library converts from the source frame to ICRF and then from ICRF to the target frame.

## Transforming Between Inertial Frames

Convert a state vector from ICRF to the ecliptic J2000 frame:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

var epoch = new Time(2024, 6, 21, 12, 0, 0);
var earth = PlanetsAndMoons.EARTH_BODY;

var svICRF = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7700.0, 0.0),
    earth, epoch, Frame.ICRF);

var svEcliptic = svICRF.ToFrame(Frame.ECLIPTIC_J2000);

Console.WriteLine($"ICRF position:     {svICRF.Position}");
Console.WriteLine($"Ecliptic position: {svEcliptic.Position}");
```

The obliquity of the ecliptic (~23.4 deg) rotates the Z-axis component between these frames.

## Body-Fixed Frames

Transform to a body-fixed frame to get coordinates that rotate with the body:

```csharp
var earthFrame = new Frame("IAU_EARTH");
var svBodyFixed = svICRF.ToFrame(earthFrame);

Console.WriteLine($"Body-fixed position: {svBodyFixed.Position}");
```

Body-fixed frames are essential for ground track computation and surface-relative geometry.

## Earth Orientation Frames

!!! note
    These high-accuracy Earth orientation frames account for precession, nutation and the
    Earth rotation angle.

These three frames follow the IAU 2006/2000A model chain:

| Frame | Description |
|-------|-------------|
| GCRF | Geocentric Celestial Reference Frame (aligned with ICRF) |
| CIRS | Celestial Intermediate Reference System (accounts for precession and nutation) |
| TIRS | Terrestrial Intermediate Reference System (accounts for Earth rotation angle) |

```csharp
// Get the CIRS-to-ICRF orientation at a given epoch
var cirsToIcrf = Frame.CIRS.GetStateOrientationToICRF(epoch);

// Rotate a position from CIRS to ICRF
var positionIcrf = positionCirs.Rotate(cirsToIcrf.Rotation);

// Rotate a position from ICRF to CIRS (use conjugate)
var positionCirs = positionIcrf.Rotate(cirsToIcrf.Rotation.Conjugate());
```

These frames are critical for precise orbit determination and high-accuracy ground station modeling.

## Equatorial Coordinates

Extract right ascension and declination from a state vector:

```csharp
var eq = svICRF.ToEquatorial();

Console.WriteLine($"RA:  {eq.RightAscension * Constants.Rad2Deg:F4} deg");
Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4} deg");
Console.WriteLine($"Range: {eq.Range:F0} m");
```

Equatorial coordinates are useful for observation planning and sensor pointing.

## Planetodetic Coordinates

Define a surface location using geodetic longitude, latitude, and altitude:

```csharp
var coords = new Planetodetic(
    longitude: -122.0 * Constants.Deg2Rad,
    latitude: 37.0 * Constants.Deg2Rad,
    altitude: 100.0);

Console.WriteLine($"Longitude: {coords.Longitude * Constants.Rad2Deg:F2} deg");
Console.WriteLine($"Latitude:  {coords.Latitude * Constants.Rad2Deg:F2} deg");
Console.WriteLine($"Altitude:  {coords.Altitude:F0} m");
```

Longitude and latitude are in radians; altitude is in meters above the reference ellipsoid.

## Summary

- All frame transforms pivot through ICRF. Use `ToFrame` to convert between any supported frames.
- Body-fixed frames (`IAU_EARTH`, `IAU_MOON`, etc.) rotate with the body.
- Pro provides GCRF, CIRS, and TIRS frames for high-accuracy Earth orientation.
- `ToEquatorial` extracts RA/Dec; `Planetodetic` represents geodetic surface coordinates.
