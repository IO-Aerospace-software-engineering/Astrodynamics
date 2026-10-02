# Standards & Units

## SI Units

IO.Astrodynamics uses SI units throughout.

| Quantity | Unit | Symbol |
|----------|------|--------|
| Length | Meters | m |
| Mass | Kilograms | kg |
| Time | Seconds | s |
| Angle | Radians | rad |
| Velocity | Meters per second | m/s |
| Acceleration | Meters per second squared | m/s2 |
| Gravitational parameter | m3/s2 | m3/s2 |
| Temperature | Celsius | deg C |
| Pressure | Kilopascals | kPa |
| Density | Kilograms per cubic meter | kg/m3 |

## Physical Constants

Defined in `IO.Astrodynamics.Constants`:

```csharp
public const double G = 6.67430e-11;
public const double AU = 149597870700.0;
public const double C = 299792458.0;
public const double g0 = 9.80665;
public const double Deg2Rad = Math.PI / 180.0;
public const double Rad2Deg = 180.0 / Math.PI;
```

## Reference Standards

- Time: IERS conventions with leap seconds from NAIF LSK kernels
- Ephemerides: JPL DE series
- Frames: IAU, IAG, and IERS conventions
- Gravity: EGM2008 spherical harmonics up to degree and order 70
- Atmosphere: U.S. Standard Atmosphere 1976 and NRLMSISE-00
- TLE propagation: SGP4 and SDP4 compatibility

## Frame Transform Convention

Frame transforms in the managed API use a consistent source-to-destination convention.

- `frame.GetStateOrientationToICRF(epoch)` returns a rotation `frame -> ICRF`
- `frame.ToFrame(targetFrame, epoch)` returns a rotation `frame -> targetFrame`
- applying the returned quaternion rotates a vector from the source frame into the destination frame

Example:

```csharp
var so = Frame.CIRS.GetStateOrientationToICRF(epoch);
var positionIcrf = positionCirs.Rotate(so.Rotation);
var positionCirs = positionIcrf.Rotate(so.Rotation.Conjugate());
```

`StateOrientation.ReferenceFrame` identifies the source frame of the rotation, not the destination frame.

## Earth-Centered Inertial And Intermediate Frames

!!! note
    These frames are exposed as `Frame.GCRF`, `Frame.CIRS` and `Frame.TIRS`.

- `ICRF`: shared inertial frame used throughout the API
- `GCRF`: geocentric realization of ICRS, differing from SPICE `J2000` by the IAU frame bias
- `CIRS`: celestial intermediate frame using IAU 2006/2000 CIO-based precession-nutation
- `TIRS`: terrestrial intermediate frame adding Earth rotation angle to `CIRS`

These frames are useful when you need an explicit IAU Earth-orientation chain instead of relying only on SPICE-defined built-in frames.

## NAIF IDs

- Sun: `10`
- Earth: `399`
- Moon: `301`
- Barycenters include `0` for the solar-system barycenter and `3` for the Earth-Moon barycenter

Be explicit about units when consuming external data, especially for OMM, OPM, and TLE-derived values where source conventions may differ from internal SI storage.

## SSA And CDM Units

Internally, the SSA workflow stays in SI units:

- miss distance in meters
- relative position in meters
- relative velocity in meters per second
- covariance in `m²`, `m²/s`, and `m²/s²`
- hard-body radius in meters

When you export a CCSDS CDM, the XML writer converts values to the units required by the schema:

- `X`, `Y`, `Z` state-vector elements use `km`
- `X_DOT`, `Y_DOT`, `Z_DOT` use `km/s`
- `MISS_DISTANCE` and `RELATIVE_POSITION_*` use `m`
- covariance terms use the CCSDS units defined by the CDM schema

That split is intentional: end users work in SI inside the managed API, while interchange files follow CCSDS rules.
