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

Each load reads one OPM: a standalone `<opm>`, or the one `<opm>` of an `<ndm>` or of another root (messages of
another type beside it are ignored). A document that holds several OPMs, such as a catalog NDM, or a second OPM at any depth below the root, throws
`OpmParseException` naming their number, `TryLoadFromFile` returns false with a `PARSE_ERROR` and
`OpmValidator.ValidateFile` reports it invalid (#679); before 11.0.0 the first one was read. Split such a document into
one OPM per document.

The OPM itself holds one header and one body, the body one segment, the segment one metadata and one data section, and
the data at most one of each block, the maneuvers excepted (NDM/XML 4.0.0, `ndmxml-4.0.0-opm-3.0.xsd`; CCSDS 502.0-B-3). An OPM
with a second segment, or a second header, body, metadata, data section or block, throws `OpmParseException` naming
the element and its count without `validateSchema`; with it the schema rejects the document first, an
`OpmValidationException` (`TryLoadFromFile` returns false with the schema error and `OpmValidator.ValidateFile` reports it
invalid; without schema validation, a `PARSE_ERROR` and the reader's error). Before 11.0.0 the first one was read and
the others dropped, a whole second segment included. Split such a document into one message per segment.

A keyword whose value the data model refuses throws `OpmParseException` naming the keyword, its value, its block and the
rule, the data model's exception as `InnerException` (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)): `ECCENTRICITY` negative,
`SEMI_MAJOR_AXIS` not above 0 for an `ECCENTRICITY` below 1 or not below 0 above 1 (the message gives both keywords),
`INCLINATION` outside [0, 180], `GM` not above 0, `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER`, `TRUE_ANOMALY` or
`MEAN_ANOMALY` outside [-360, 360) (the `angleRange` of the schema and of the OMM's angles; [#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708): not checked
before 11.0.0, so a document read without the schema step kept an angle of 1000 or NaN), a negative `MASS`, `SOLAR_RAD_AREA`, `SOLAR_RAD_COEFF`,
`DRAG_AREA` or `DRAG_COEFF`, a negative `MAN_DURATION`, a positive `MAN_DELTA_MASS`, or an empty `MAN_REF_FRAME`,
`OBJECT_NAME`, `OBJECT_ID`, `CENTER_NAME`, `REF_FRAME`, `TIME_SYSTEM` or `ORIGINATOR`, for example
`SEMI_MAJOR_AXIS '-6878.137' in keplerianElements with ECCENTRICITY '0.0001234' is invalid: Semi-major axis must be positive for elliptical orbits (e < 1).` Before 11.0.0 the `ArgumentOutOfRangeException` or
`ArgumentException` of the data model's constructor crossed the reader, naming its parameter. With `validateSchema` the
schema rejects most of these values first (`OpmValidationException`), but not those its types allow: `SEMI_MAJOR_AXIS`
is `xsd:double` and the strings `xsd:string`, so a `SEMI_MAJOR_AXIS` of the wrong sign or an empty `OBJECT_NAME` throws
`OpmParseException` with the default validation too. These rules go beyond the schema and are the library's
([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708), decided by S. Guillet, 2026-11-16): a = p / (1 − e²) with a semi-latus rectum p above 0
gives no conic for the other sign, CCSDS 502.0-B-3 makes `OBJECT_NAME` and the other metadata mandatory, each with a
non-empty value (7.5.1; 8.13.1 for ODM/XML), and the parse exception names the keyword. The readers refuse these schema-valid
values, as before 11.0.0. A `MASS` of 0 is read, `massType` being a `nonNegativeDouble` (its
`minInclusive` 0.0), as for the `MASS` of an OMM: `OpmData` and `OpmValidator` take a mass of 0 or more ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708); before
11.0.0 a mass of 0 threw `OpmParseException`, `Mass must be positive.`). `ToSpacecraft` builds a spacecraft of mass 0.
`TryLoadFromFile` returns false with a `PARSE_ERROR` and `OpmValidator.ValidateFile` an error, both naming the keyword.
The constructors of the data model (`OpmKeplerianElements`, `OpmData`, ...) keep their `ArgumentException`; a ranged
parameter of `OpmKeplerianElements` (a, e, i, GM, and the four angles in [-360, 360) since [#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)), `OpmData` (the five spacecraft parameters) or `OpmManeuverParameters`
(`MAN_DURATION`, `MAN_DELTA_MASS`) refuses NaN and ±Infinity with its `ArgumentOutOfRangeException`, and so does
`Spacecraft.ToOpm`, which builds them, for such a value, where it wrote `NaN`
([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)). The values without a range are real numbers too ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717)): a component of `OpmStateVector`, a term of
`CovarianceMatrix` and a ΔV component of `OpmManeuverParameters` throw `ArgumentOutOfRangeException` naming the parameter
(`x` ... `zDot`, `cxX` ... `czDotZDot`, `manDv1` ... `manDv3`) for NaN or ±Infinity, from the constructors and from the
factories that build them (`OpmStateVector.FromStateVector`, `CovarianceMatrix.FromFullMatrix`, `FromMatrix`,
`FromMatrixWithUnitConversion`, `OpmManeuverParameters.CreateImpulsive`), so from `Opm.CreateFromStateVector`,
`Spacecraft.ToOpm` and `ImpulseManeuver.ToOpmManeuverParameters`. Before 11.0.0 they built it, `Validate()` passed it
and `SaveToFile` wrote `NaN`, `Infinity` or `-Infinity`, which the reader refuses (`Infinity` is not an `xsd:double`
literal). The data model holds only what its reader reads.

A numeric value is a real number: a text that .NET reads as NaN or ±Infinity (`NaN`, `Infinity` or `-Infinity` in any
case, or a value beyond the range of a `double` such as `1e400`, which .NET rounds to ±Infinity) throws
`OpmParseException` naming the keyword and the text, for every numeric keyword of the message ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)), for example
`Failed to parse numeric value 'NaN' for field 'ECCENTRICITY': not a finite number.` Before 11.0.0 the document loaded
with the value, which every range check passed, the comparisons with NaN being false. The schema step does not stop
these values: .NET's schema validator accepts NaN against the bound facets of NDM/XML 4.0.0 and `Infinity` or an
overflow as an `xsd:double`. `TryLoadFromFile` returns false with a `PARSE_ERROR` and `OpmValidator.ValidateFile` an error,
both naming the keyword. The lexical `INF` and `-INF` of `xsd:double` throw the parse exception, as before.

A date (`EPOCH`, `CREATION_DATE`, `MAN_EPOCH_IGNITION`) is read in the two forms of CCSDS 502.0-B-3: calendar
(`2026-01-13T05:00:55.752768`) or day of year (`2026-013T05:00:55.752768`), each field with its leading zeros, any
fraction, an optional `Z` or `±hh:mm` offset of the NDM/XML `epochType`, as UTC. Any other text throws `OpmParseException`
naming the field and the text ([#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619), decided by S. Guillet, 2026-11-17): a time alone, a date alone, a bare
number, a space or a lowercase `t` for the `T`, a decimal comma, a missing second, a short offset, a blank around the
date. Before 11.0.0 the reader passed such a text to `DateTime.TryParse`, which gave a time alone the day of the read
and `1.5` or `1,5` 5 January of the current year.

A state vector component (`X` ... `Z_DOT`, km, km/s) whose value in m or m/s, its product by 1000, is beyond the range of
a `double` (above about 1.8e305), and a covariance term (`CX_X` ... `CZ_DOT_Z_DOT`) whose product by 1e6 is, throw
`OpmParseException` naming the keyword and the text, as the CDM reader refuses such a value
([#729](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/729)); `new OpmStateVector(...)`, `new CovarianceMatrix(...)` and their factories throw
`ArgumentOutOfRangeException` naming the parameter. Before 11.0.0 the document loaded and `ToStateVector` gave ±Infinity
in the state or its covariance.

### Schema validation

`validateSchema` and `OpmValidator.ValidateSchema` check the document against the CCSDS NDM/XML 4.0.0 schemas (CCSDS
505.0-B-3) in its form, as SANA publishes them: a root `ndm` or `opm` without a namespace, every element unqualified (the
form of Space-Track and of the writer, SANA's `ndmxml_unqualified` set) or every element below the root in the
namespace `urn:ccsds:schema:ndmxml` (the qualified set). Any other root is an error, and so is an `ndm` that holds no
`opm` child (an empty `ndm`, or one of other messages only), which the NDM master accepts but in which the reader
finds no OPM (`OPM006`; reported valid before 11.0.0, `Load*` then throwing `OpmParseException`).
A root in the namespace, the library's earlier form of a bare message, is checked as before, against the OPM schema with that root declared in the
namespace (an `ndm` root in the namespace has no declaration and is rejected, by SANA too). The unqualified set is the
embedded one with its `elementFormDefault` set to `unqualified`, the one difference between the two sets SANA publishes.

Before the third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) an
unqualified document matched no declaration of the validator, so it was reported valid with a warning per element and
nothing below its root was checked: an OPM whose `X` is in `units="m"` loaded with its value read as km, and undeclared
elements were ignored. Such a document now makes `Load*` throw `OpmValidationException`; fix the document, or pass
`validateSchema: false` to read it without the schema step, where a keyword out of its range throws `OpmParseException`
since the fourth round (above). A valid document, the writer's output included, validates without a warning.

### Content validation

`validateContent`, `Validate()` and `OpmValidator.Validate` check the physical constraints of the data model's values. Each
error check is the negation of its admitted range, so that NaN, in no range, is an error (`OPM002`), as is an infinite
value where the range is half-open: a covariance variance must be finite and non-negative; the covariance constructors
refuse a non-finite term ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717)) but take a negative one, which the validator reports ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708); before 11.0.0 a NaN
variance was valid). The warnings and the
information levels (an unusual drag coefficient, a large eccentricity) are unchanged.

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
| `ToSpacecraft(naifId, maxMass, clock, observer)` | Create a `Spacecraft` from OPM data, under the rules of its constructor: `naifId` in [-2147483, -1], and `clock` not attached to a spacecraft of another NAIF id (`ArgumentException`, #676); the observer as on `ToStateVector` |
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
The Keplerian elements are those of `OpmKeplerianElements.FromKeplerianElements` with the true anomaly, each angle
written as its value in [0, 360), inside the schema's [-360, 360). `FromKeplerianElements` writes 0 for an angle of
2π, which the [0, 2π) normalization of `KeplerianElements` gives for a negative angle below half the ulp of 2π, where it
wrote 360 before 11.0.0, which the schema rejects; the elements of `ToOpm`, computed from a state, never reach 2π, so
its output is unchanged ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)); an angle inside [0, 360) is written as before, bit for bit.

The covariance of the initial state (`StateVector.Covariance`, m², m²/s, m²/s²) is exported, in km², km²/s and km²/s², when
the OPM is written at the initial epoch (the default, or the same instant in any time frame) (#551). At another epoch the
OPM carries no covariance: moving it there would take the state transition matrix, which the export does not compute.

## Example

```csharp
// Inputs: spacecraft (Spacecraft), epoch (Time), earth (CelestialBody)
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
