# Frames & Orientation

## Transform Convention

The frame API always returns rotations from the source frame to the destination frame named by the method.

| API | Returned rotation |
|-----|-------------------|
| `frame.GetStateOrientationToICRF(epoch)` | `frame -> ICRF` |
| `frame.ToFrame(targetFrame, epoch)` | `frame -> targetFrame` |

Apply the returned quaternion to rotate a vector from the source frame into the destination frame. Use the conjugate for the inverse direction.

### Angular Velocity

The angular velocity of a frame transform follows the SPICE convention (`xf2rav_c`), in rad/s:

| API | Returned angular velocity |
|-----|---------------------------|
| `frame.GetStateOrientationToICRF(epoch)` | ICRF relative to `frame`, expressed in `frame`: about `(0, 0, -7.292e-5)` for an Earth-fixed frame |
| `frame.ToFrame(targetFrame, epoch)` | `targetFrame` relative to `frame`, expressed in `frame` |

With `R` the rotation `frame -> ICRF` and `ω` the angular velocity of `GetStateOrientationToICRF`, the rotation
evolves as `R(t + dt) = R(t) exp(-[ω×] dt)`. The SPICE frames, TIRS, CIRS and the site frames all follow this
convention, which the tests check on ITRF93, IAU_MOON, MOON_ME and a DSN topocentric frame.
`StateOrientation.AtDate` applies its angular velocity in the destination axes, the convention of spacecraft
attitudes: it does not extrapolate a frame transform.

### States And Covariance

`OrbitalParameters.ToFrame(frame)` converts a whole state. With `R` and `ω` the rotation and angular velocity of
`frame.ToFrame(target, epoch)`, the position becomes `r' = R r` and the velocity `v' = R v - (R ω) × r'`: `R ω`
expresses in the target frame the angular velocity that `ToFrame` gives in the source frame. The second term is zero
between inertial frames and non-zero towards or from a rotating frame (ITRF93, TIRS, a body-fixed frame). The result
matches the states SPICE computes directly in ITRF93 to a few 1e-16 in relative terms.

A covariance carried by the state goes through the Jacobian of this transformation:

```
P' = J P Jᵀ,   J = | R              0 |
                   | -[(R ω)×] R    R |
```

`[(R ω)×]` is the cross-product matrix of `R ω`. The result is symmetrized. Towards an Earth-fixed frame, the
lower-left block makes the velocity covariance depend on the position covariance: 100 m of position uncertainty brings
about 7 mm/s of velocity uncertainty. `Matrix.TransformCovariance(covariance, rotation)` applies `diag(R, R)` only and
is reserved for changes between inertial frames.

## Frame

`Frame` represents a named reference frame.

| Static Properties | Description |
|-------------------|-------------|
| `Frame.ICRF` | International Celestial Reference Frame (SPICE `J2000`), the pivot of every transform |
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
| `AngularVelocity` | Angular velocity of the transform; for a frame transform, see [Angular Velocity](#angular-velocity) |
| `ReferenceFrame` | Source frame of the transform |
| `AtDate(Time)` | Propagate an attitude using constant angular velocity, applied in the destination axes; not for frame transforms |
| `RelativeTo(Frame)` | Re-express in another destination frame |

## Earth Orientation Frames

!!! note
    These frames are exposed as `Frame.GCRF`, `Frame.CIRS` and `Frame.TIRS`, alongside
    `Frame.ICRF` and the other predefined frames.

| Frame | Description |
|-------|-------------|
| `Frame.GCRF` | ICRS axes centred on the Earth: same axes as `Frame.ICRF` |
| `Frame.CIRS` | Celestial intermediate reference system using CIO-based precession-nutation |
| `Frame.TIRS` | Terrestrial intermediate reference system adding Earth rotation angle |

`Frame.TIRS` uses `NullEop`, which returns zero for UT1-UTC and polar motion. Construct
`new TirsFrame(eop)` with your own `IEarthOrientationParameters` implementation when you need real
Earth orientation data. Without it, TIRS is off by the actual UT1-UTC, which stays within 0.9 s: up
to about 13.5 arcseconds of Earth rotation angle, about 420 m for a point fixed on the equator. The
chain stops at TIRS, so polar motion is never applied, even with a provider that returns it. See
[Accuracy Without EOP](../standards-and-units.md#accuracy-without-eop).

The angular velocity of TIRS is `(0, 0, -Ω)` in TIRS axes, with `Ω = 2π × 1.00273781191135448 / 86400` rad/s, the
rate of the Earth rotation angle used by IAU SOFA `iauPvtob`. As there, the motion of the CIP itself
(precession-nutation, a few 1e-12 rad/s) is left out: about 1e-7 of the velocity of an Earth-fixed point. CIRS, which
only follows the CIP, takes its angular velocity from a central difference of its rotation.

### ICRF, GCRF And EME2000

`Frame.ICRF` is the SPICE frame `J2000`. SPICE treats it as aligned with the ICRF, and the DE
planetary ephemerides it carries are ICRF-aligned; the library follows the same convention.
`Frame.GCRF` has the same axes: the rotation between the two is the identity. The IAU 2006 frame
bias belongs to the precession-nutation matrix that leads from GCRF to CIRS.

The mean equator and equinox of J2000 (EME2000, the dynamical frame) is a different frame: the
~23 mas frame bias separates it from the ICRF, about 0.8 m on a LEO position (6,778 km) and 4.7 m on
a GEO position (42,164 km). No library frame models EME2000. Data labelled EME2000, such as a CCSDS
OPM with `REF_FRAME = EME2000`, is read in `Frame.ICRF`, and the bias is ignored.

Use `Frame.ICRF` for everything that comes from SPICE: ephemerides, propagation, geometry. Use
`Frame.GCRF` when you want to name the celestial end of the `GCRF -> CIRS -> TIRS` chain
explicitly; coordinates are the same in both.

### Example

```csharp
var cirsToIcrf = Frame.CIRS.GetStateOrientationToICRF(epoch);
var positionIcrf = positionCirs.Rotate(cirsToIcrf.Rotation);
```

### Model And Provenance

`Iau2006Model` implements the CIO-based chain with IAU 2006 precession (Fukushima-Williams angles)
and IAU 2000B nutation: 77 luni-solar terms with the linear arguments of Simon et al. (1994), plus
the IAU 2006 (P03) adjustments. The CIO locator `s` uses the full IAU 2006 series (66 periodic
terms). IAU 2000B is specified at about 1 mas against IAU 2000A; precession and frame bias are
sub-microarcsecond. The agreement measured against SOFA from 1990 to 2040 is published in
[Standards & Units](../standards-and-units.md#measured-agreement-with-sofa).

The implementation follows the
[IERS Conventions 2010](https://www.iers.org/IERS/EN/Publications/TechnicalNotes/tn36.html). The
nutation and CIO locator coefficient tables were extracted from the source of the IAU SOFA library
(release 2023-10-11) by the harnesses in `tools/sofa_reference`, which also produce the reference
values the tests compare against.

Separately, the native library links the SOFA C library, unmodified, for a few computations made on
the SPICE side (sidereal time, precession-nutation and UTC/TT conversions in `Frames.cpp` and
`UTC.cpp`).

This software uses routines and computations derived by the authors from software provided by SOFA
under license; it does not itself constitute software provided by and/or endorsed by SOFA.

## See Also

- [Coordinate Conversions](../tutorials/coordinate-conversions.md)
- [Standards & Units](../standards-and-units.md#frame-transform-convention)
