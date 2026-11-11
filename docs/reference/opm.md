# CCSDS OPM

The `IO.Astrodynamics.CCSDS.OPM` module handles Orbit Parameter Messages for Cartesian-state interchange in the CCSDS standard format.

## Purpose

Use OPM when you need a state-vector-oriented CCSDS message rather than TLE-compatible mean elements (OMM). OPM is the standard transport format for sharing Cartesian ephemeris snapshots between systems.

## Unit Conversion

The managed API stores values in SI units internally. The OPM reader and writer handle conversion to and from CCSDS-standard units.

| Quantity | Internal (SI) | CCSDS (OPM) |
|----------|---------------|-------------|
| Position | m | km |
| Velocity | m/s | km/s |
| Angles | rad | deg |

## Opm Class

### Loading

| Method | Description |
|--------|-------------|
| `Opm.LoadFromFile(path, validateSchema?, validateContent?)` | Load from file with optional validation |
| `Opm.LoadFromString(xml, validateSchema?, validateContent?)` | Load from XML string |
| `Opm.LoadFromStream(stream, validateSchema?, validateContent?)` | Load from stream |
| `Opm.TryLoadFromFile(path, out opm, out result, ...)` | Non-throwing load with validation result |

### Saving

| Method | Description |
|--------|-------------|
| `SaveToFile(path, validateBeforeSave?, wrapInNdm?, indent?)` | Save to file |
| `SaveToStream(stream, validateBeforeSave?, wrapInNdm?, indent?)` | Save to stream |
| `ToXmlString(wrapInNdm?, indent?)` | Serialize to XML string |

### Factory Methods

| Method | Description |
|--------|-------------|
| `Opm.CreateFromStateVector(objectName, objectId, stateVector, originator?)` | Create from a state vector |
| `Opm.CreateMinimal(...)` | Create a minimal OPM with required fields only |

### Conversion

| Method | Description |
|--------|-------------|
| `ToStateVector(observer)` | Convert to a `StateVector` (applies unit conversion); `EPOCH` is read in `TIME_SYSTEM` (UTC, TAI, TDB, TT or GPS), another time system throws (#485). The library's writers write the epoch in UTC under `TIME_SYSTEM = UTC` (#479) |
| `ToSpacecraft(naifId, maxMass, clock, observer)` | Create a `Spacecraft` from OPM data |
| `Validate()` | Validate and return `OpmValidationResult` |

### Properties

| Property | Description |
|----------|-------------|
| `HasKeplerianElements` | Whether optional Keplerian elements are present |
| `HasSpacecraftParameters` | Whether optional spacecraft parameters are present |
| `HasManeuvers` | Whether maneuver parameters are present |

## Spacecraft Export

`Spacecraft.ToOpm(originator, epoch, includeKeplerianElements, includeSpacecraftParameters, includeManeuvers)` exports the
spacecraft's current state to OPM format; every argument is optional, so name the ones you pass (`epoch: epoch`). The state
is written in the frame of the spacecraft's initial orbital parameters, which the export does not change: there is no frame
argument. The spacecraft needs a `CosparId` (`InvalidOperationException` otherwise).

The covariance of the initial state (`StateVector.Covariance`, m², m²/s, m²/s²) is exported, in km², km²/s and km²/s², when
the OPM is written at the initial epoch (the default, or the same instant in any time frame) (#551). At another epoch the
OPM carries no covariance: moving it there would take the state transition matrix, which the export does not compute.

## Example

```csharp
// Export spacecraft state to OPM, in the frame of its state
var opm = spacecraft.ToOpm(epoch: epoch);
opm.SaveToFile("state.opm.xml");

// Load and convert back
var loaded = Opm.LoadFromFile("state.opm.xml");
var sv = loaded.ToStateVector(earth);
```

## See Also

- [OMM](omm.md)
- [CDM](cdm.md)
- [StateVector](state-vector.md)
- [Spacecraft](spacecraft.md)
