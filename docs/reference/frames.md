# Frames & Orientation

## Transform Convention

The frame API always returns rotations from the source frame to the destination frame named by the method.

| API | Returned rotation |
|-----|-------------------|
| `frame.GetStateOrientationToICRF(epoch)` | `frame -> ICRF` |
| `frame.ToFrame(targetFrame, epoch)` | `frame -> targetFrame` |

Apply the returned quaternion to rotate a vector from the source frame into the destination frame. Use the conjugate for the inverse direction.

## Frame

`Frame` represents a named reference frame.

| Static Properties | Description |
|-------------------|-------------|
| `Frame.ICRF` | International Celestial Reference Frame (J2000) |
| `Frame.ECLIPTIC_J2000` | Ecliptic plane at J2000 |
| `Frame.TEME` | True Equator Mean Equinox (for TLE) |
| `Frame.ECLIPTIC_B1950` | Ecliptic plane at B1950 |
| `Frame.GALACTIC_SYSTEM2` | Galactic coordinate system |
| `Frame.B1950` | B1950 equatorial frame |
| `Frame.FK4` | FK4 frame |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Frame(string name)` | Create frame by name (e.g., "IAU_EARTH", "ITRF93") |

### Methods

| Method | Description |
|--------|-------------|
| `GetStateOrientationToICRF(Time epoch)` | Get orientation relative to ICRF |
| `ToFrame(Frame target, Time epoch)` | Get transformation to another frame |

## StateOrientation

`StateOrientation` packages a rotation, angular velocity, epoch, and source frame.

| Member | Description |
|--------|-------------|
| `Rotation` | Quaternion from `ReferenceFrame` to destination |
| `AngularVelocity` | Angular velocity of the transform |
| `ReferenceFrame` | Source frame of the transform |
| `AtDate(Time)` | Propagate using constant angular velocity |
| `RelativeTo(Frame)` | Re-express in another destination frame |

## Earth Orientation Frames

!!! note
    These frames are exposed as `Frame.GCRF`, `Frame.CIRS` and `Frame.TIRS`, alongside
    `Frame.ICRF` and the other predefined frames.

| Frame | Description |
|-------|-------------|
| `Frame.GCRF` | Geocentric realization of ICRS, including IAU frame bias |
| `Frame.CIRS` | Celestial intermediate reference system using CIO-based precession-nutation |
| `Frame.TIRS` | Terrestrial intermediate reference system adding Earth rotation angle |

`Frame.TIRS` uses `NullEop`, which returns zero for UT1-UTC and polar motion. Construct
`new TirsFrame(eop)` with your own `IEarthOrientationParameters` implementation when you need real
Earth orientation data.

### Example

```csharp
var cirsToIcrf = Frame.CIRS.GetStateOrientationToICRF(epoch);
var positionIcrf = positionCirs.Rotate(cirsToIcrf.Rotation);
```

### Model And Provenance

`Iau2006Model` implements the IAU 2006/2000A precession-nutation chain: IAU 2006 precession
(Fukushima-Williams angles) with IAU 2000B nutation, 77 luni-solar terms. Accuracy is about 1 mas
for nutation and sub-microarcsecond for precession and frame bias.

The implementation follows the
[IERS Conventions 2010](https://www.iers.org/IERS/EN/Publications/TechnicalNotes/tn36.html), which
is the source of the coefficients. The reference algorithms of the IAU SOFA library were used to
validate the results; no SOFA code is distributed with this framework.

## See Also

- [Coordinate Conversions](../tutorials/coordinate-conversions.md)
- [Standards & Units](../standards-and-units.md#frame-transform-convention)
