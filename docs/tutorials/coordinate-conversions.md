# Coordinate Conversions

This tutorial covers reference frame transforms, Earth orientation, and coordinate representations. You will learn to convert between inertial, ecliptic, and body-fixed frames, extract equatorial coordinates, and work with planetodetic positions.

## Frame Transform Convention

All frame transforms go through the International Celestial Reference Frame (ICRF) as the pivot. When you call `ToFrame`, the library converts from the source frame to ICRF and then from ICRF to the target frame.

## Transforming Between Inertial Frames

Convert a state vector from ICRF to the ecliptic J2000 frame:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
var earth = PlanetsAndMoons.EarthBody;

var svICRF = new StateVector(
    new Vector3(6800000.0, 0.0, 0.0),
    new Vector3(0.0, 7700.0, 0.0),
    earth, epoch, Frame.ICRF);

var svEcliptic = svICRF.ToFrame(Frame.ECLIPJ2000);

Console.WriteLine($"ICRF position:     {svICRF.Position}");
Console.WriteLine($"Ecliptic position: {svEcliptic.Position}");
```

The obliquity of the ecliptic (~23.4 deg) rotates the Z-axis component between these frames.

## Body-Fixed Frames

Transform to a body-fixed frame to get coordinates that rotate with the body:

```csharp
// Inputs: svICRF (StateVector)
var earthFrame = new Frame("IAU_EARTH");
var svBodyFixed = svICRF.ToFrame(earthFrame);

Console.WriteLine($"Body-fixed position: {svBodyFixed.Position}");
```

Body-fixed frames are essential for ground track computation and surface-relative geometry.

If the state carries a covariance, `ToFrame` transforms it too. Towards a body-fixed frame the velocity picks up the
rotation of the frame (`v' = R v - (R ω) × r'`), and so does the covariance: a position uncertainty adds a velocity
uncertainty, about 7 mm/s for 100 m in an Earth-fixed frame. See [Frames](../reference/frames.md#states-and-covariance).

## Earth Orientation Frames

!!! note
    These high-accuracy Earth orientation frames account for precession, nutation and the
    Earth rotation angle.

These three frames follow the CIO-based IAU chain, IAU 2006 precession with IAU 2000B nutation:

| Frame | Description |
|-------|-------------|
| GCRF | Geocentric Celestial Reference Frame (same axes as ICRF) |
| CIRS | Celestial Intermediate Reference System (accounts for precession and nutation) |
| TIRS | Terrestrial Intermediate Reference System (accounts for Earth rotation angle) |

```csharp
// Inputs: svICRF (StateVector), epoch (Time)
// Get the CIRS-to-ICRF orientation at a given epoch
var cirsToIcrf = Frame.CIRS.GetStateOrientationToIcrf(epoch);

// Rotate a position from ICRF to CIRS (use the conjugate)
var positionIcrf = svICRF.Position;
var positionCirs = positionIcrf.Rotate(cirsToIcrf.Rotation.Conjugate());

// Rotate it back from CIRS to ICRF
var positionBack = positionCirs.Rotate(cirsToIcrf.Rotation);
```

These frames are critical for precise orbit determination and high-accuracy ground station modeling.

## Equatorial Coordinates

Extract right ascension and declination from a state vector. They are in ICRF whatever the frame of the state: a
state in another frame is rotated to ICRF at its epoch first (#680). Coordinates known as angles are built by
`Equatorial.FromRightAscensionDeclination(rightAscension, declination, epoch)`, or with a distance
`(rightAscension, declination, distance, epoch)`, in the order of the name (11.0.0: the constructor of two angles took
the declination first, so a swap compiled).

```csharp
// Inputs: svICRF (StateVector)
var eq = svICRF.ToEquatorial();

Console.WriteLine($"RA:  {eq.RightAscension * Constants.Rad2Deg:F4} deg");
Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4} deg");
Console.WriteLine($"Distance: {eq.Distance:F0} m");

// Coordinates given as angles, right ascension first as the factory's name says (Vega, J2000)
var vega = Equatorial.FromRightAscensionDeclination(279.23473479 * Constants.Deg2Rad, 38.78368896 * Constants.Deg2Rad,
    svICRF.Epoch);
Vector3 towardVega = vega.ToDirection(); // unit vector in ICRF
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

Convert between geodetic and planetocentric coordinates with the ellipsoid of the body; the conversion is exact at any
altitude, on the surface as in orbit:

```csharp
// Inputs: coords (Planetodetic)
var earth = PlanetsAndMoons.EarthBody;
var planetocentric = coords.ToPlanetocentric(earth);
var back = planetocentric.ToPlanetodetic(earth); // coords, to about 1e-15
```

The geodetic coordinates of a state take one call, on the ellipsoid of its observer and in its body-fixed frame at the
state's epoch (the body's `Frame`: ITRF93 for the Earth, not the `IAU_EARTH` of the block above; the geodetic
coordinates are those of CSPICE `recgeo_c` but at the centre of the body and on the polar axis with a negative-zero
coordinate, see [Sites](../reference/sites.md#planetodetic)) (an observer that is not a `CelestialBody` throws `InvalidOperationException`), and the body-fixed
position of a geodetic point one more:

```csharp
// Inputs: svICRF (StateVector), coords (Planetodetic)
var earth = PlanetsAndMoons.EarthBody;
Planetodetic below = svICRF.ToPlanetodetic();            // in ITRF93, the Earth's body frame, at the state's epoch
Vector3 position = coords.ToCartesianCoordinates(earth); // body-fixed position, m, as CSPICE georec_c
```

## Summary

- All frame transforms pivot through ICRF. Use `ToFrame` to convert between any supported frames.
- Body-fixed frames (`IAU_EARTH`, `IAU_MOON`, etc.) rotate with the body.
- GCRF, CIRS, and TIRS frames provide the IAU CIO-based Earth orientation chain.
- `ToEquatorial` extracts the ICRF RA/Dec whatever the frame of the state; `Planetodetic` represents geodetic surface coordinates, and `ToPlanetodetic()` gives those of a state in one call.
