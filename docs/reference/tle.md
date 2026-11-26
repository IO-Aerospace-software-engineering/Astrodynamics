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
standard output.

The constructor reads no kernel at all (R21 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), 11.0.0): the Earth of the TLE (its
`Observer`) reads its GM, radii and frame from the data provider at the first member that needs them, retried on
failure. A valid TLE gives its fields, `NoradCatalogId`, `MeanMeanMotion` and `ToOmm` without kernels;
`MeanSemiMajorAxis`, `ToMeanKeplerianElements` and SGP4 need the kernels that define the Earth and throw
`InvalidOperationException` without them (`BODY399_GM`), keeping nothing, so that the same `TLE` works once they are
loaded. Before 11.0.0 a TLE built before the kernels kept a GM of 0 for its life.

## Properties

| Property | Description |
|----------|-------------|
| `Name` | Object name |
| `Epoch` | Epoch of elements |
| `ElementsType` | Always `Mean` |
| `MeanSemiMajorAxis` | Mean semi-major axis (m), ∛(GM/n²) with the kernel GM of the Earth, read at the first use |
| `MeanMeanMotion` | Mean motion n of line 2 (rad/s): the field (rev/day) × 2π / 86400, read without kernels (decision 111 of #668) |
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
| `ToOmm(string originator)` | Convert to CCSDS OMM, `MEAN_MOTION` the field of line 2 as written, without kernels |
| `Create(OrbitalParameters, string, int, string, ...)` | Create TLE from mean orbital parameters about the Earth in `Frame.TEME` |

The inherited members of `OrbitalParameters` (`MeanMotion()`, `SemiMajorAxis()`, `Eccentricity()`, ...) are those of
the osculating SGP4 set at the epoch; the mean values are the `Mean…` properties. `MeanMeanMotion` replaces the method
`MeanMotion()` of 10.x, √(GM/a³) with a = ∛(GM/n²), within 2 ulp of the field (decision 111 of #668, decided by
S. Guillet, 2026-11-26): `tle.MeanMotion()` now binds to the osculating one, 0.14 % off for the ISS.

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
a sign (#480). Without an element set number, `Create`, the TLE `Configuration` of `ToTLE` and `Omm.ToTle` (an OMM
without `ELEMENT_SET_NO`) write `TLE.DefaultElementSetNumber`, 999, as CelesTrak and Space-Track publish (#606, decided
by S. Guillet, 2026-11-17; before 11.0.0, `Create` and `ToTLE` wrote 9999).

A TLE holds SGP4 mean elements of an Earth orbit in TEME, so `Create` throws `ArgumentException` (`meanElements`) for
elements that are not mean, not in `Frame.TEME` (frames compared by name) or not about the Earth (NAIF id 399), and
`ToTLE` throws `InvalidOperationException` before its fit for orbital parameters that are not about the Earth, in any
frame (#607, decided by S. Guillet, 2026-11-17; before 11.0.0, `Create` wrote elements in another frame or about another
centre as if they were, their angles taken as TEME's and their mean motion from their centre's GM, and `ToTLE` took an
orbit about another centre for an Earth orbit and failed in its fit). Rotate mean elements to TEME with
`ToFrame(Frame.TEME)` before `Create`, or give `ToTLE` the osculating state, which it rotates to TEME itself.

A TLE without an international designator (blank columns 10-17 of line 1, as 11801 and 88888 of Vallado's `SGP4-VER.TLE`) is carried through: `TLE.Create` takes a null or blank `cosparId` and writes eight blanks there, `TLE.ToOmm` writes `OBJECT_ID = UNKNOWN` (the element is mandatory in the ODM; CCSDS's value for an unknown designator, confirmed by S. Guillet, 2026-11-18), and `Omm.ToTle` reads `UNKNOWN`, trimmed and in any letter case, back as the blank columns ([#604](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/604), decided by S. Guillet, 2026-11-18). The fit of `OrbitalParameters.ToTLE(Configuration)` writes a null or blank `Configuration.CosparId` as the same blank columns. Before 11.0.0 `Create` threw `ArgumentException` (`cosparId`), `ToTLE` "TLE conversion using Equinoctial elements failed" (`InvalidOperationException`), `ToOmm` "Object ID is required." (`objectId`), and `ToTle` wrote `UNKNOWN ` in the designator columns. An empty `OBJECT_ID` is still refused by the reader and `OmmMetadata`, as #708 decided (2026-11-16): the decision of #604 also reads an empty value as the blank columns, but no OMM holds one, and whether the reader should accept it is open, for S. Guillet to settle.

The states of SGP4/SDP4 are in `Frame.TEME`, the managed TEME frame ([Frames](frames.md#teme)); `ToFrame(Frame.ICRF)`
needs no Earth orientation kernel, at any epoch. The Earth a TLE refers to is built in an inertial frame, so that a TLE
outside the coverage of the Earth's binary PCK reads nothing it does not need (#597). A failed SGP4 evaluation (a
decayed or suborbital orbit, an eccentricity out of range) throws `InvalidOperationException` instead of returning a
zero state (#454), with SPICE's message (#711). So does a TLE read before the kernels are loaded (SPICE's
"The variable BODY399_GM could not be found in the kernel pool."): nothing is cached by the failed read, and the same
`TLE` gives its states once the kernels are loaded (#711).

NORAD catalog numbers run from 0 to 339999. Above 99999 they are written with the Alpha-5
convention of the 18th Space Defense Squadron, which replaces the first digit with a letter
(`I` and `O` excluded) without changing the column layout.

## Numerical propagation of a TLE

A spacecraft whose initial orbit is a TLE is propagated numerically by `Spacecraft.Propagate`, `Scenario.Simulate`,
`BatchPropagator`, `MonteCarloPropagator` and `CentralBodyPropagator`, as any spacecraft (#675; before
11.0.0 every path threw "must be defined in inertial frame"). Its start is the SGP4 state at the start of the window,
rotated once to ICRF, on the Earth of the TLE; the propagation runs in ICRF and its states are in ICRF
([Propagation Frame](propagators.md#propagation-frame)). The TLE is not SGP4 then: the central body is the Earth of
the TLE, a point mass without an atmospheric model, whose drag, when asked, is NRLMSISE-00 at nominal space weather, as
for any Earth without one (#674), and B* is not used. For the ISS (`21020.53488036`), RK7(8) at 1e-9, the
propagation is 53.663 km, 275.692 km and 1118.386 km from SGP4 after 1 h, 6 h and 24 h. An Earth with a geopotential or
an atmosphere in the bodies does not replace it: bodies of one NAIF identifier must have the same models, so the
propagation throws `ArgumentException` naming them (#674; before, such an Earth was ignored for gravity and its
atmosphere used for drag). For a geopotential and an atmosphere of your choice, give the spacecraft a TEME state on an
Earth that carries them:

```csharp
// Inputs: window (Window), tle (TLE)
var start = window.StartDate.ToTDB();
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new Nrlmsise00Model(SpaceWeather.Nominal));
var sgp4 = tle.ToStateVector(start);
var spacecraft = new Spacecraft(-1001, "ISS", 420000.0, new StateVector(sgp4.Position, sgp4.Velocity, earth, start, Frame.TEME), 450000.0,
    new Clock("iss", 65536), 1600.0, 2.2);
spacecraft.Propagate(window, new PropagationOptions { AtmosphericDrag = true, SolarRadiationPressure = true });
```

The options of a propagation do it in one line (R12 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), 11.0.0): the point-mass Earth of the
TLE has no model, which counts as not given, so `PropagationOptions.Geopotential` and `PropagationOptions.Atmosphere`
fill it ([Propagation Options](propagators.md#propagation-options)), bit for bit the run of the TEME twin on an Earth
built with that model (`GeopotentialOptionsTests`):

```csharp
// Inputs: window (Window), tle (TLE)
var spacecraft = new Spacecraft(-1002, "ISS", 420000.0, tle, 450000.0, new Clock("iss2", 65536), 1600.0, 2.2);
spacecraft.Propagate(window, PropagationOptions.Default with
{
    Geopotential = GeopotentialModelParameters.Egm2008(10),
    Atmosphere = new Nrlmsise00Model(SpaceWeather.Nominal),
    AtmosphericDrag = true,
    SolarRadiationPressure = true
});
```

With EGM2008 at degree 10 and no drag, the same ISS is 0.555 km, 0.788 km and 7.217 km from SGP4 after 1 h, 6 h and
24 h. The command `astro propagate --from tle` does so, on the Earth of the CLI (EGM2008 at the degree of `--degree`, 10
by default, with `--drag` NRLMSISE-00 at nominal space weather, or the U.S. Standard Atmosphere 1976 with
`--atmosphere standard`); a TLE carries its centre, the Earth, its epoch and its frame ([command-line tool](cli.md)).

A TLE spacecraft reads its stored states after a numerical propagation by any path (`GetEphemeris`, `WriteEphemeris`,
the window searches, the conjunction analysis with the check of #527, the avoidance options on its force model), and
SGP4 otherwise: unpropagated, or sampled by `TLEPropagator`, whose samples replace the stored states without changing the rule. One read
of a sampled spacecraft is not SGP4 at the instant: `GetEphemeris` from its Earth without aberration interpolates the
samples of `TLEPropagator`, as before (SGP4 at the samples, 9.1e-4 m from it between them for the ISS at 60 s over one UTC hour, 7.6e-4 m before #598 moved the last sample to the window end), and
reads SGP4 outside them (#527; it extrapolated them before, 20.7 m 6 min before the first sample); its geometric state,
and the reads from any other body, are SGP4.
`Spacecraft.ToOpm` writes the propagated state, in TEME, after a numerical propagation, and the SGP4 state otherwise,
also when `TLEPropagator` sampled it (#658, decided by S. Guillet, 2026-11-17; the SGP4 state on every path before 11.0.0). For SGP4 itself, use `TLEPropagator` or `tle.ToStateVector(t)`.

## See Also

- [TLE & OMM Tutorial](../tutorials/tle-and-omm.md)
- [OMM](omm.md)
