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
| `ToStateVector(observer)` | Convert to a `StateVector` (applies unit conversion); `EPOCH` is read in `TIME_SYSTEM` (UTC, TAI, TDB, TT or GPS), another time system throws (#485). The library's writers write the epoch in UTC under `TIME_SYSTEM = UTC` (#479). The observer is optional: see [The centre](#the-centre) |
| `ToSpacecraft(naifId, maxMass, clock, observer)` | Create a `Spacecraft` from OPM data; the observer as on `ToStateVector` |
| `Validate()` | Validate and return `OpmValidationResult` |

### The centre

Without an observer, `ToStateVector` and `ToSpacecraft` take the centre `CENTER_NAME` names (#679), read trimmed and in
any letter case:

| `CENTER_NAME` | Observer |
|---------------|----------|
| `SOLAR SYSTEM BARYCENTER`, `SSB`, `SOLAR_SYSTEM_BARYCENTER` | `Barycenter` 0 |
| `<PLANET> BARYCENTER`, `<PLANET>_BARYCENTER` (`MERCURY` to `PLUTO`) | `Barycenter` 1 to 9 |
| `EMB`, `EARTH MOON BARYCENTER`, `EARTH-MOON BARYCENTER` | `Barycenter` 3 |
| `SUN` | `CelestialBody` 10 |
| the SPICE name of a planet or moon of `PlanetsAndMoons` (`EARTH`, `MOON`, `MARS`, ...) | `CelestialBody` of its id |

The names are those SPICE gives the ids (`bodc2n`), which the writers emit, and NAIF's built-in synonyms of the
barycenters (NAIF IDs Required Reading). A `CelestialBody` is built in ICRF at the epoch, a `Barycenter` at the epoch.

- Any other `CENTER_NAME` without an observer (a ground station such as `DSS-13`, a site, a spacecraft, a body that only
  a kernel names) throws `ArgumentException`: pass the observer, which is then used as given.
- An observer whose NAIF id differs from the centre a known `CENTER_NAME` names throws `ArgumentException`.

Before 11.0.0 the observer was the Earth whatever `CENTER_NAME` said.

### Properties

| Property | Description |
|----------|-------------|
| `HasKeplerianElements` | Whether optional Keplerian elements are present |
| `HasSpacecraftParameters` | Whether optional spacecraft parameters are present |
| `HasManeuvers` | Whether maneuver parameters are present |

## Spacecraft Export

`Spacecraft.ToOpm(originator, epoch, includeKeplerianElements, includeSpacecraftParameters, includeManeuvers)` exports to OPM
format the state of the spacecraft's initial orbital parameters at `epoch`, their epoch by default:
`InitialOrbitalParameters.ToStateVector(epoch)`, the two-body motion of osculating elements (SGP4 for a TLE), not the
states of a propagation. Every argument is optional, so name the ones you pass (`epoch: epoch`). The state is written in
the frame of the spacecraft's initial orbital parameters, which the export does not change: there is no frame argument. The spacecraft needs a `CosparId` (`InvalidOperationException` otherwise).

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
