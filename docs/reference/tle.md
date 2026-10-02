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
| `BallisticCoefficient` | B* drag coefficient |
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

NORAD catalog numbers run from 0 to 339999. Above 99999 they are written with the Alpha-5
convention of the 18th Space Defense Squadron, which replaces the first digit with a letter
(`I` and `O` excluded) without changing the column layout.

## See Also

- [TLE & OMM Tutorial](../tutorials/tle-and-omm.md)
- [OMM](omm.md)
