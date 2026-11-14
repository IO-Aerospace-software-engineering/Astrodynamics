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

The constructor checks the text before it reads any kernel (#675): a null or empty line or name, a line of another
length than 69 characters or that does not start with its line number (`ArgumentException`), catalog numbers that
differ or a wrong checksum (`InvalidOperationException`). Before 11.0.0 it built the Earth of the TLE first, so that
without kernels a malformed TLE threw the kernel error, and CSPICE printed the banners of its failed reads on the
standard output. A valid TLE still needs the kernels that define the Earth (its GM, radii and frame, which its
`Observer` carries and `MeanSemiMajorAxis` uses): it throws `InvalidOperationException` without them.

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

## Numerical propagation of a TLE

A spacecraft whose initial orbit is a TLE is propagated numerically by `Spacecraft.Propagate`, `PropagateAsync`,
`Scenario.SimulateAsync`, `BatchPropagator`, `CentralBodyPropagator` and the builder, as any spacecraft (#675; before
11.0.0 every path threw "must be defined in inertial frame"). Its start is the SGP4 state at the start of the window,
rotated once to ICRF, on the Earth of the TLE; the propagation runs in ICRF and its states are in ICRF
([Propagation Frame](propagators.md#propagation-frame)). The TLE is not SGP4 then: the central body is the Earth of
the TLE, a point mass without atmosphere, and B* is not used. For the ISS (`21020.53488036`), RK7(8) at 1e-9, the
propagation is 53.663 km, 275.692 km and 1118.386 km from SGP4 after 1 h, 6 h and 24 h. An Earth with a geopotential or
an atmosphere in the bodies does not replace it: it is ignored for gravity, its atmosphere used for drag, and drag
without one is dropped (Known limitations of 11.0.0). For a geopotential and drag, give the spacecraft a TEME state on
an Earth that carries them:

```csharp
var start = window.StartDate.ToTDB();
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new Nrlmsise00Model(SpaceWeather.Nominal));
var sgp4 = tle.ToStateVector(start);
var spacecraft = new Spacecraft(-1001, "ISS", 420000.0, 450000.0, new Clock("iss", 65536),
    new StateVector(sgp4.Position, sgp4.Velocity, earth, start, Frame.TEME), 1600.0, 2.2);
spacecraft.Propagate(window, new CelestialItem[] { earth }, new RK78Integrator(), true, true, TimeSpan.FromSeconds(60.0));
```

With EGM2008 at degree 10 and no drag, the same ISS is 0.555 km, 0.788 km and 7.217 km from SGP4 after 1 h, 6 h and
24 h. The command `astro propagate -t` does so, on the Earth of the CLI (EGM2008 at degree 10, the U.S. Standard
Atmosphere 1976 with `-a`); its centre of motion must be the Earth (399).

A TLE spacecraft reads its stored states after a numerical propagation by any path (`GetEphemeris`, `WriteEphemeris`,
the window searches, the conjunction analysis with the check of #527, the avoidance options on its force model), and
SGP4 otherwise: unpropagated, or sampled by `TLEPropagator`, which adds its states without changing the rule. One read
of a sampled spacecraft is not SGP4 at the instant: `GetEphemeris` from its Earth without aberration interpolates the
samples of `TLEPropagator`, as before (SGP4 at the samples, 7.6e-4 m from it between them for the ISS at 60 s); its
geometric state, and the reads from any other body, are SGP4.
`Spacecraft.ToOpm` writes the SGP4 state (#658). For SGP4 itself, use `TLEPropagator` or `tle.ToStateVector(t)`.

## See Also

- [TLE & OMM Tutorial](../tutorials/tle-and-omm.md)
- [OMM](omm.md)
