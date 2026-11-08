# TLE

`TLE` stores mean Earth-orbit elements that must be propagated with SGP4 or SDP4 to obtain physical states.

!!! warning
    TLE stores **mean** elements. Calling `ToStateVector()` on mean `KeplerianElements` throws `InvalidOperationException`. Use `TLE.ToStateVector()` or `TLE.ToOsculating()` instead.

## Constructor

```csharp
var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
```

## Properties

| Property | Description |
|----------|-------------|
| `Name` | Object name |
| `Epoch` | Epoch of elements |
| `ElementsType` | Always `Mean` |
| `MeanSemiMajorAxis` | Mean semi-major axis (m) |
| `MeanEccentricity` | Mean eccentricity |
| `MeanInclination` | Mean inclination (rad) |
| `MeanAscendingNode` | Mean RAAN (rad) |
| `BallisticCoefficient` | B* drag coefficient (1/earth radii) |
| `FirstDerivationMeanMotion` | ṅ/2, the first-derivative field of line 1 (rev/day²), OMM's `MEAN_MOTION_DOT` |
| `SecondDerivativeMeanMotion` | n̈/6, the second-derivative field of line 1 (rev/day³), OMM's `MEAN_MOTION_DDOT` |
| `NoradCatalogId` | NORAD catalog number, Alpha-5 decoded (`T5544` reads as 275544) |

## Methods

| Method | Description |
|--------|-------------|
| `ToStateVector()` | Propagate to TLE epoch via SGP4/SDP4 |
| `ToStateVector(Time)` | Propagate to specified epoch |
| `ToOsculating()` | Convert to osculating StateVector at TLE epoch |
| `ToOsculating(Time)` | Convert to osculating StateVector at epoch |
| `ToMeanKeplerianElements()` | Get mean Keplerian elements directly |
| `ToOmm(string originator)` | Convert to CCSDS OMM |
| `Create(OrbitalParameters, string, int, string, ...)` | Create TLE from mean orbital parameters |

## SGP4/SDP4

The model is CSPICE's `evsgp4_c`, Vallado's 2006 code (Vallado, Crawford, Hujsak and Kelso, AIAA 2006-6753), in the
AFSPC mode, with the WGS-72 "old" constants of Vallado's `wgs72old`: KE = 7.43669161e-2 exactly (not derived from GM
and the radius), R = 6378.135 km, J2 = 1.082616e-3, J3 = −2.53881e-6, J4 = −1.65597e-6, q0 = 120 km, s0 = 78 km. No
other mode (the improved mode of Vallado's code) or constants (WGS-72, WGS-84) are offered.

tsince, SGP4's time argument, is the TT seconds from the TLE epoch to the requested instant over 60, whatever the time
frame of the instant: away from a leap second, the UTC days of Vallado's driver. The library calls `evsgp4_c` at the
epoch `getelm_c` reads plus those seconds (#599). Against Vallado's code in this configuration (`sgp4` 2.27,
`tools/sgp4_reference`), the 29 element sets of Vallado's verification set that SGP4 evaluates agree to 7.6e-5 m and
1.3e-8 m/s per component, CSPICE's own difference from it.

The epoch is the UTC instant of the field, exact: the day fraction is read in decimal and rounded to the 100 ns tick
(#603). `Create` and `ToTLE` write the UTC of the elements' epoch, whatever its time frame (#479), each field the
nearest value its columns hold: the epoch to the 1e-8 day with the carry into the next day and year (#600), B* and n̈/6
as a five-digit mantissa and a one-digit exponent, zero below 5e-11 (#601), e in [0, 0.9999999] (#602), a zero without
a sign (#480).

The states of SGP4/SDP4 are in `Frame.TEME`, the managed TEME frame ([Frames](frames.md#teme)); `ToFrame(Frame.ICRF)`
needs no Earth orientation kernel, at any epoch. The Earth a TLE refers to is built in an inertial frame, so that a TLE
outside the coverage of the Earth's binary PCK reads nothing it does not need (#597). A failed SGP4 evaluation (a
decayed or suborbital orbit, an eccentricity out of range) throws `InvalidOperationException` instead of returning a
zero state (#454).

NORAD catalog numbers run from 0 to 339999. Above 99999 they are written with the Alpha-5
convention of the 18th Space Defense Squadron, which replaces the first digit with a letter
(`I` and `O` excluded) without changing the column layout.

## See Also

- [TLE & OMM Tutorial](../tutorials/tle-and-omm.md)
- [OMM](omm.md)
