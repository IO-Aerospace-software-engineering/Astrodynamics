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
`StateOrientation.AtDate` applies its angular velocity in the destination axes, as the angular velocity of the source
relative to the destination, `−R ω`: it extrapolates neither a frame transform nor the attitude of a spacecraft, whose
frame follows the convention above ([Attitude and kernels](spacecraft.md#attitude-and-kernels)).

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
| `Frame.TEME` | True Equator, Mean Equinox, the frame of SGP4 states: the managed `TemeFrame`, see [TEME](#teme) |
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

### Equality

A frame is its name, compared ignoring case as SPICE compares frame names (`namfrm_c`), whatever the instance or its
type: `==`, `!=`, `Equals(Frame)`, `Equals(object)` and `GetHashCode` are one relation, so the operators, `HashSet`,
`Dictionary` and `Assert.Equal` agree
([#562](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/562)). `new Frame("TEME")` equals
`Frame.TEME`, `new Frame("GCRF")` or `new Frame("gcrf")` equals `Frame.GCRF`, `new Frame("CIRS")` and
`new Frame("TIRS")` equal `Frame.CIRS` and `Frame.TIRS`, and `new Frame("J2000")` equals `Frame.ICRF`; a frame built
by name (a CCSDS file, the CLI) takes the identity shortcut of `ToFrame` and passes the inertial-frame check of a
propagation as the static frame does. The name does not carry the Earth orientation parameters of a `TirsFrame` or a
`TemeFrame` built with a provider: it equals `Frame.TIRS` or `Frame.TEME`, so `ToFrame` between them is the identity.
Nor does it carry the definition of a `SiteFrame` or a `SpacecraftFrame`: each equals any frame of its name, a kernel
frame included, whatever its axes. A site named DSS-13 has the frame `DSS-13_TOPO`, the name of the kernel frame of the
real station (`earth_topo_201023.tf`): if its coordinates differ, `new Frame("DSS-13_TOPO").ToFrame(site.Frame, t)` is
the identity, not the rotation between the two topocentric frames. Give a site or a spacecraft a name no loaded kernel
uses for another frame.
`Frame.ICRF` is named `J2000`: `new Frame("ICRF")` is another name, not this frame. `Id` takes no part.
Equality is not routing: a frame named TEME or GCRF is routed to the managed rotation by its name (see [TEME](#teme)
and [ICRF, GCRF And EME2000](#icrf-gcrf-and-eme2000)), but a frame named CIRS or TIRS that is not a `CirsFrame` or a
`TirsFrame` is still handed to SPICE, which does not know it: `new Frame("CIRS").ToFrame(Frame.ICRF, t)` throws. Use
`Frame.CIRS` and `Frame.TIRS`.

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
explicitly; coordinates are the same in both. SPICE has no GCRF frame: `SpiceAPI` hands a frame
named GCRF (`Frame.GCRF`, or `new Frame("GCRF")` as the CLI builds it, in any case) to SPICE as
`J2000` and labels the result with the frame given. An ephemeris, an orientation (CK), a frame
transformation or a coordinate search in GCRF are then the same numbers as in J2000, and a state in
GCRF propagates as its ICRF twin (#543). `Frame.CIRS` and `Frame.TIRS` are not SPICE frames either,
and they rotate: an ephemeris read in `Frame.CIRS` or `Frame.TIRS` (an instance of `CirsFrame` or `TirsFrame`) is read in
J2000 and rotated by the frame at the epoch of the read, the numbers `ToFrame` gives on the state read in ICRF (#558). So
are the frames only the library holds, the `SiteFrame` of a site built from coordinates and the `SpacecraftFrame` of a
managed spacecraft whose attitude record holds an entry (#682; before 11.0.0 a read in them reached SPICE by name and threw
SPICE(UNKNOWNFRAME)), whatever a loaded kernel defines under their names. The frame of a managed spacecraft whose record
is empty (never propagated, no orientation added, built from a TLE or propagated by `TLEPropagator`) holds no attitude in
the library: a read in it still reaches SPICE by name, which reads the frame kernel and the CK of that id when they are
loaded (those the library wrote for an earlier object of the id) and throws SPICE(UNKNOWNFRAME) otherwise. A coordinate
search that CSPICE runs in one of them is not mapped.

### TEME

`Frame.TEME` is the True Equator, Mean Equinox frame of the SGP4/SDP4 states of a TLE, an instance of `TemeFrame`
(#451). The definition is that of Vallado, Crawford, Hujsak and Kelso (2006), "Revisiting Spacetrack Report #3",
AIAA 2006-6753 Rev2, appendix C (confirmed by S. Guillet, 2026-11-08): the pseudo Earth-fixed frame of a TEME state is
`r_PEF = R3(GMST82(UT1)) r_TEME`, with the PEF taken as the TIRS. So

```
TEME → ICRF = Q(t)ᵀ · Rz(ERA − GMST82)
```

with `Q(t)` the CIO-based precession-nutation matrix of `Frame.CIRS` (IAU 2006/2000B), the Earth rotation angle of
`Frame.TIRS`, GMST82 the IAU 1982 Greenwich mean sidereal time (the expression of SOFA `iauGmst82`) and `Rz` the active
rotation about z.

- **No file.** UT1 enters GMST82 and the ERA alike, so UT1 − UTC cancels between them to 0.03 mm in LEO and 0.2 mm at
  the geostationary radius: `Frame.TEME` takes UT1 = UTC, and `new TemeFrame(eop)` takes UT1 − UTC from a provider.
  Neither polar motion nor the Earth's binary PCK enters, so TEME ↔ ICRF works at any epoch, before 2000 and after the
  end of the PCK.
- **Quasi-inertial.** The angular velocity is zero: a state transforms as `r' = R r`, `v' = R v`, as Vallado's
  `teme2eci` and GMAT. The rotation rate of the frame (about 7e-12 rad/s) would move a LEO velocity by at most
  0.08 mm/s, a geostationary one by 0.39 mm/s. Toward a rotating frame such as ITRF93, the angular velocity of that
  frame applies: TEME → ITRF93 is TEME → ICRF, then ICRF → ITRF93 from SPICE.
- **By name.** A frame built by name, `new Frame("TEME")` in any case (a CCSDS file, the CLI, user code), is routed to
  `Frame.TEME` by `SpiceDataProvider` and `MemoryDataProvider`, and `SpiceAPI.TransformFrame` composes a TEME end
  through ICRF; the native TEME of the C++ layer is no longer used. An ephemeris read in TEME is read in J2000
  and rotated (#558). A frame built by name equals `Frame.TEME` ([Equality](#equality), #562).
- **Measured** (`tools/teme_reference`, 2026-11-08; a LEO and a GEO state at 13 epochs from 1980 to 2026): within
  0.9 mm (LEO) and 18 mm (GEO) of ERFA's CIP route with IAU 2006/2000B, 19 mm and 106 mm (0.55 mas, IAU 2000B's
  accuracy) of the route with IAU 2006/2000A, and 9.4 cm and 56 cm of GMAT R2026a's TEME → EarthICRF, whose residual is
  the kinematic terms of the 1994 equation of the equinoxes that GMAT applies from 1997-02-27 (up to 2.6 mas) and
  Vallado's TEME leaves out; velocities within 0.094 mm/s of GMAT.

Before 11.0.0 TEME was ITRF93 of the Earth's binary PCK composed with R3(GAST06) of UTC: 0.3″ to 23.3″ from the
definition over 2000–2026 (70 to 797 m in LEO, up to 4.75 km at the geostationary radius), with a spurious rotation
rate, and a zero rotation outside the PCK.

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
