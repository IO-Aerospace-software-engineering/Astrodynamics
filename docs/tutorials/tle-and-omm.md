# TLE & OMM

This tutorial covers Two-Line Element sets (TLE), Orbit Mean-Elements Message (OMM), and Orbit Parameter Message (OPM). You will learn to parse TLEs, propagate with SGP4, interchange between formats, and handle the critical distinction between mean and osculating elements.

## Parsing a TLE

Parse a standard two-line element set from its three-line string form (a text or a file of one or many sets is read by
`TLE.Parse`, `TLE.ParseAll`, `TLE.Load` and `TLE.LoadAll`; see [Reading TLE text](../reference/tle.md#reading-tle-text)):

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

Console.WriteLine($"Name:         {tle.Name}");
Console.WriteLine($"NORAD ID:     {tle.NoradCatalogId}");
Console.WriteLine($"Epoch:        {tle.Epoch}");
Console.WriteLine($"Inclination:  {tle.MeanInclination * Constants.Rad2Deg:F4} deg");  // the mean inclination of line 2
```

## Mean vs Osculating Elements

!!! warning "Critical Rule"
    TLE elements are **mean** elements fitted to the SGP4/SDP4 theory. Calling `ToStateVector()` on mean Keplerian elements is **not valid** — it would mix mean elements with a two-body conversion and produce incorrect results.

To get a physical state vector, always use `ToOsculating`:

```csharp
// Inputs: tle (TLE)
// Correct: osculating state vector at TLE epoch
var sv = tle.ToOsculating();

// Correct: mean Keplerian elements (for display or comparison only)
var meanKep = tle.ToMeanKeplerianElements();

// Correct: SGP4-propagated state vector at a specific time
var svAtTime = tle.ToStateVector(new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB));
```

- `ToOsculating()` returns the osculating state at the TLE epoch.
- `ToStateVector(epoch)` propagates with SGP4/SDP4 to the given epoch and returns the osculating state.
- `ToMeanKeplerianElements()` returns the raw mean elements for inspection.

Mean elements stay mean through `AtEpoch(t)` and `ToKeplerianElements(t)` (#463), so their `ToStateVector()` throws
there too, and a numerical propagation (`CentralBodyPropagator`, `Spacecraft.Propagate`, `Scenario.Simulate`)
refuses a spacecraft whose initial orbit is a set of mean elements other than a TLE, with an `ArgumentException` that
points to `TLEPropagator` and `TLE.ToOsculating()`. `AtEpoch` of mean elements advances the mean anomaly at the two-body
mean motion only, without the secular rates of the node and of the periapsis of SGP4: a TLE's own `AtEpoch` is SGP4.

## SGP4 Propagation

Propagate the TLE to a future epoch using the built-in SGP4/SDP4 propagator:

```csharp
// Inputs: tle (TLE)
var futureEpoch = new Time(2021, 1, 25, 0, 0, 0, TimeScale.TDB);
var futureSv = tle.ToStateVector(futureEpoch);

Console.WriteLine($"Position at {futureEpoch}: {futureSv.Position}");
Console.WriteLine($"Velocity at {futureEpoch}: {futureSv.Velocity}");
```

SGP4 predictions degrade with time. For best accuracy, use the most recent TLE available.

The model is CSPICE's `evsgp4_c`: Vallado's 2006 code (AIAA 2006-6753) in the AFSPC mode, with the WGS-72 "old"
constants (KE = 7.43669161e-2 exactly, R = 6378.135 km, J2 = 1.082616e-3, J3 = −2.53881e-6, J4 = −1.65597e-6). The
library offers no other mode or constants. SGP4's time argument, tsince, is the TT seconds from the TLE epoch (a UTC
instant) to the epoch you ask for, whatever its time frame: `futureEpoch` above is TDB, and its UTC equivalent gives the
same state. The state is in `Frame.TEME`. A TLE that SGP4 cannot evaluate at that epoch (decayed, suborbital, an
eccentricity out of range) throws `InvalidOperationException`.

## Numerical Propagation of a TLE

A spacecraft built on a TLE is propagated numerically like any spacecraft: the propagation starts from the SGP4 state
at the start of the window, rotated once to ICRF, and integrates in ICRF. About the TLE's own Earth that is a point mass
(53.7 km from SGP4 after an hour for this ISS TLE). To give it a geopotential and drag, start from the SGP4 state in
TEME on an Earth that carries them; `TLEPropagator` gives SGP4 itself, for comparison:

```csharp
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;

// Inputs: tle (TLE)
var window = new Window(tle.Epoch, tle.Epoch.AddHours(6.0));
var start = window.StartDate.ToTDB();
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    new Nrlmsise00Model(SpaceWeather.Nominal));
var sgp4AtStart = tle.ToStateVector(start);
var numerical = new Spacecraft(-1001, "ISS-NUM", 420000.0, new StateVector(sgp4AtStart.Position, sgp4AtStart.Velocity, earth, start, Frame.TEME), 450000.0,
    new Clock("issnum", 65536), 1600.0, 2.2);
var solution = numerical.Propagate(window, new PropagationOptions { AtmosphericDrag = true });

var sampled = new Spacecraft(-1002, "ISS-SGP4", 420000.0, tle, 450000.0, new Clock("issgp4", 65536));
new TLEPropagator(window, sampled, TimeSpan.FromSeconds(60.0)).Propagate();

var end = solution.StateVectors[^1];
var sgp4AtEnd = tle.ToStateVector(end.Epoch).ToFrame(Frame.ICRF);
Console.WriteLine($"Numerical - SGP4 after 6 h: {(end.Position - sgp4AtEnd.Position).Magnitude():F0} m");
```

The solution and the stored states are in ICRF. Once propagated numerically, the spacecraft reads its stored states;
unpropagated, it reads SGP4; sampled by `TLEPropagator`, it reads SGP4 too, except `GetEphemeris` from its Earth
without aberration, which interpolates the samples (under a millimetre for the ISS at 60 s). See
[Numerical propagation of a TLE](../reference/tle.md#numerical-propagation-of-a-tle).

## Creating a TLE from a State Vector

Convert an osculating state vector back to a TLE:

```csharp
using TleConfiguration = IO.Astrodynamics.OrbitalParameters.TLE.Configuration;
// Inputs: sv (StateVector)
// TLE.Configuration collides with IO.Astrodynamics.Configuration, so the directive above aliases it

var config = new TleConfiguration(
    NoradId: 25544,
    Name: "ISS (ZARYA)",
    CosparId: "98067A",
    RevolutionsAtEpoch: 25703);

var newTle = sv.ToTLE(config);
```

The fitting process converts osculating elements to mean elements consistent with SGP4. The state may be in any frame,
elements already in TEME included, but must be about the Earth: `ToTLE` throws `InvalidOperationException` for an
orbit about another centre (#607); the TLE's epoch is the UTC of the state's epoch, rounded to the 1e-8 day of the
field. Each field of the text is the nearest value its columns hold.

A `NoradId` above 99999 is encoded with the Alpha-5 convention, up to 339999:

```csharp
// Inputs: sv (StateVector), and the TleConfiguration alias above
var alpha5 = new TleConfiguration(NoradId: 275544, Name: "MY_SAT", CosparId: "24001A");
var alpha5Tle = sv.ToTLE(alpha5);

Console.WriteLine(alpha5Tle.Line1.Substring(2, 5));  // T5544
Console.WriteLine(alpha5Tle.NoradCatalogId);         // 275544
```

## OMM: Orbit Mean-Elements Message

OMM is the CCSDS standard XML/KVN format for distributing mean elements. Load, validate, and convert:

```csharp
using IO.Astrodynamics.CCSDS.OMM;

var omm = Omm.Load("satellite.omm");

Console.WriteLine($"Object name: {omm.ObjectName}");
Console.WriteLine($"Object ID:   {omm.ObjectId}");
Console.WriteLine($"Epoch:       {omm.Epoch}");
```

### OMM to TLE

Not all OMMs can be converted to TLEs (some lack the required fields). Check compatibility first:

```csharp
// Inputs: omm (Omm)
if (omm.IsTleCompatible)
{
    var tleFromOmm = omm.ToTle();
    Console.WriteLine($"TLE line 1: {tleFromOmm.Line1}");
    Console.WriteLine($"TLE line 2: {tleFromOmm.Line2}");
}
```

### TLE to OMM

Convert a TLE to OMM for CCSDS-compliant data exchange:

```csharp
// Inputs: tle (TLE)
var newOmm = tle.ToOmm(originator: "My Organization");
```

### COSPAR ID Format

TLE uses a compact international designator (e.g., `98067A`) while OMM uses the full COSPAR format with a hyphen (e.g., `1998-067A`). Conversions between the two formats are handled automatically.

## OPM: Orbit Parameter Message

OPM carries a single osculating state vector in CCSDS format:

```csharp
using IO.Astrodynamics.CCSDS.OPM;

var earth = PlanetsAndMoons.EARTH_BODY;

var opm = Opm.Load("spacecraft.opm");
var stateVector = opm.ToStateVector(earth);

Console.WriteLine($"Position: {stateVector.Position}");
Console.WriteLine($"Velocity: {stateVector.Velocity}");
Console.WriteLine($"Epoch:    {stateVector.Epoch}");
```

Unlike TLE/OMM, OPM elements are osculating — they can be used directly for numerical propagation without conversion.

## Summary

- Parse TLEs from standard two-line format, with the constructor or `TLE.Parse`/`TLE.LoadAll` (a malformed set throws at its line and column); use `ToOsculating()` or `ToStateVector(epoch)` for physical states.
- Never call `ToStateVector()` on mean Keplerian elements — always go through the SGP4 propagator.
- OMM and TLE are interchangeable when the OMM has TLE-compatible fields (`IsTleCompatible`): TLE parameters, the SGP4 theory, TEME and the Earth.
- OPM provides osculating state vectors in CCSDS format, ready for direct use.
- COSPAR ID format differences between TLE and OMM are handled automatically.
