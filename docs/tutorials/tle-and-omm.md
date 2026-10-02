# TLE & OMM

This tutorial covers Two-Line Element sets (TLE), Orbit Mean-Elements Message (OMM), and Orbit Parameter Message (OPM). You will learn to parse TLEs, propagate with SGP4, interchange between formats, and handle the critical distinction between mean and osculating elements.

## Parsing a TLE

Parse a standard two-line element set from its three-line string form:

```csharp
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.SolarSystemObjects;

SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var tle = new TLE("ISS (ZARYA)",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

Console.WriteLine($"Name:         {tle.Name}");
Console.WriteLine($"NORAD ID:     {tle.NoradId}");
Console.WriteLine($"Epoch:        {tle.Epoch}");
Console.WriteLine($"Inclination:  {tle.Inclination * Constants.Rad2Deg:F4} deg");
```

## Mean vs Osculating Elements

!!! warning "Critical Rule"
    TLE elements are **mean** elements fitted to the SGP4/SDP4 theory. Calling `ToStateVector()` on mean Keplerian elements is **not valid** — it would mix mean elements with a two-body conversion and produce incorrect results.

To get a physical state vector, always use `ToOsculating`:

```csharp
// Correct: osculating state vector at TLE epoch
var sv = tle.ToOsculating();

// Correct: mean Keplerian elements (for display or comparison only)
var meanKep = tle.ToMeanKeplerianElements();

// Correct: SGP4-propagated state vector at a specific time
var svAtTime = tle.ToStateVector(new Time(2021, 1, 21, 12, 0, 0));
```

- `ToOsculating()` returns the osculating state at the TLE epoch.
- `ToStateVector(epoch)` propagates with SGP4/SDP4 to the given epoch and returns the osculating state.
- `ToMeanKeplerianElements()` returns the raw mean elements for inspection.

## SGP4 Propagation

Propagate the TLE to a future epoch using the built-in SGP4/SDP4 propagator:

```csharp
var futureEpoch = new Time(2021, 1, 25, 0, 0, 0);
var futureSv = tle.ToStateVector(futureEpoch);

Console.WriteLine($"Position at {futureEpoch}: {futureSv.Position}");
Console.WriteLine($"Velocity at {futureEpoch}: {futureSv.Velocity}");
```

SGP4 predictions degrade with time. For best accuracy, use the most recent TLE available.

## Creating a TLE from a State Vector

Convert an osculating state vector back to a TLE:

```csharp
// TLE.Configuration collides with IO.Astrodynamics.Configuration, so alias it
using TleConfiguration = IO.Astrodynamics.OrbitalParameters.TLE.Configuration;

var config = new TleConfiguration(
    NoradId: 25544,
    Name: "ISS (ZARYA)",
    CosparId: "98067A",
    RevolutionsAtEpoch: 25703);

var newTle = sv.ToTLE(config);
```

The fitting process converts osculating elements to mean elements consistent with SGP4.

A `NoradId` above 99999 is encoded with the Alpha-5 convention, up to 339999:

```csharp
var alpha5 = new TleConfiguration(NoradId: 275544, Name: "MY_SAT", CosparId: "24001A");
var alpha5Tle = sv.ToTLE(alpha5);

Console.WriteLine(alpha5Tle.Line1.Substring(2, 5));  // T5544
Console.WriteLine(alpha5Tle.NoradCatalogId);         // 275544
```

## OMM: Orbit Mean-Elements Message

OMM is the CCSDS standard XML/KVN format for distributing mean elements. Load, validate, and convert:

```csharp
var omm = Omm.LoadFromFile("satellite.omm",
    validateSchema: true, validateContent: true);

Console.WriteLine($"Object name: {omm.ObjectName}");
Console.WriteLine($"Object ID:   {omm.ObjectId}");
Console.WriteLine($"Epoch:       {omm.Epoch}");
```

### OMM to TLE

Not all OMMs can be converted to TLEs (some lack the required fields). Check compatibility first:

```csharp
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
var newOmm = tle.ToOmm(originator: "My Organization");
```

### COSPAR ID Format

TLE uses a compact international designator (e.g., `98067A`) while OMM uses the full COSPAR format with a hyphen (e.g., `1998-067A`). Conversions between the two formats are handled automatically.

## OPM: Orbit Parameter Message

OPM carries a single osculating state vector in CCSDS format:

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;

var opm = Opm.LoadFromFile("spacecraft.opm",
    validateSchema: true, validateContent: true);
var stateVector = opm.ToStateVector(earth);

Console.WriteLine($"Position: {stateVector.Position}");
Console.WriteLine($"Velocity: {stateVector.Velocity}");
Console.WriteLine($"Epoch:    {stateVector.Epoch}");
```

Unlike TLE/OMM, OPM elements are osculating — they can be used directly for numerical propagation without conversion.

## Summary

- Parse TLEs from standard two-line format; use `ToOsculating()` or `ToStateVector(epoch)` for physical states.
- Never call `ToStateVector()` on mean Keplerian elements — always go through the SGP4 propagator.
- OMM and TLE are interchangeable when the OMM has TLE-compatible fields (`IsTleCompatible`).
- OPM provides osculating state vectors in CCSDS format, ready for direct use.
- COSPAR ID format differences between TLE and OMM are handled automatically.
