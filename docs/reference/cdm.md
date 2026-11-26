# CCSDS CDM

!!! note
    The CDM types live in the `IO.Astrodynamics.CCSDS.CDM` namespace.

The CDM module supports constructing, reading, writing, and validating CCSDS Conjunction Data Messages in XML format.

## Cdm Object Model

`Cdm` is the root object representing a complete Conjunction Data Message with header, relative metadata, and two object entries.

## Static Methods

| Method | Description |
|--------|-------------|
| `Cdm.Load(path, options?)` | The one CDM of a file, with the checks of `CcsdsReadOptions`: the schema, then the content, both by default |
| `Cdm.LoadAll(path, options?)` | Every CDM of a file, in document order |
| `Cdm.Parse(xml, options?)` | The one CDM of an XML text, or of a `TextReader` read to its end |
| `Cdm.ParseAll(xml, options?)` | Every CDM of an XML text, or of a `TextReader` |
| `Cdm.TryLoad(path, out cdm, options?)` | `Load` without an exception: false for a missing file or a document the checks or the reader refuse |
| `Cdm.TryParse(xml, out cdm, options?)` | `Parse` without an exception |

The CDM shares the verb set of the OMM and the OPM (R22 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), lot L7). Before 11.0.0 it was read
by `Cdm.ReadFromFile`, `ReadFromString` and `ReadFromStream`, which ran no check, and validated by the static
`Cdm.ValidateSchema` and `ValidateSchemaFromXml`: `Load` and `Parse` now check the schema and the content by default,
a failure throwing `CdmValidationException` (a `CcsdsFormatException`) with its `CdmValidationResult`;
`new CcsdsReadOptions { ValidateSchema = false, ValidateContent = false }` reads as the former readers did. `CdmReader`,
`CdmWriter` and `CdmValidator` are internal.

The exceptions of the CDM reader, `CdmParseException` and `CdmValidationException`, derive from `CcsdsFormatException`, a `FormatException` of
`IO.Astrodynamics.CCSDS` shared by the OMM, OPM and CDM readers, so that one `catch (CcsdsFormatException)` covers a
document that cannot be read, at parsing or at validation (11.0.0, R03 of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668): before, each derived from
`Exception` alone).

The readers read one CDM: a standalone `<cdm>`, or the one `<cdm>` of an `<ndm>` or of another root. A document that holds
several CDMs, at any depth below the root, throws `CdmParseException` naming their number and `LoadAll` (#679); before 11.0.0 the first one was returned.
`LoadAll` and `ParseAll` read every CDM of such a document.

The CDM itself holds one header and one body, the body one `relativeMetadataData` and exactly two segments, one per
object, each segment one metadata and one data section, and each data section and the `relativeMetadataData` at most
one of each block (NDM/XML 4.0.0, `ndmxml-4.0.0-cdm-1.0.xsd`; CCSDS 508.0-B-1, 3.2). A body with another number of
segments throws `CdmParseException` naming it, where before 11.0.0 the `Cdm` constructor threw `ArgumentException`; a
second header, body, `relativeMetadataData`, metadata, data section or block throws `CdmParseException` naming the
element and its count, where the first was read (a second `relativeMetadataData`'s miss distance dropped).

An enumerated keyword whose value is not one of its own (`OBJECT`, `REF_FRAME`, `COVARIANCE_METHOD`, `MANEUVERABLE`,
`SCREEN_VOLUME_FRAME`, `SCREEN_VOLUME_SHAPE`, `SOLAR_RAD_PRESSURE`, `EARTH_TIDES`, `INTRACK_THRUST`; CCSDS 508.0-B-1
table 3-3) throws `CdmParseException` naming the keyword and its value, the `FormatException` of the enumeration as
`InnerException`, for example `REF_FRAME 'TEME' is not one of the values of the keyword: Unsupported CDM reference frame
'TEME'.` Before 11.0.0 that `FormatException` crossed the reader (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). With the schema check
(`CcsdsReadOptions.ValidateSchema`, the default) the schema rejects these values first, `CdmValidationException`.

A numeric value is a real number: a text that .NET reads as NaN or ±Infinity (`NaN`, `Infinity` or `-Infinity` in any
case, or a value beyond the range of a `double` such as `1e400`) throws `CdmParseException` naming the keyword and the
text, for every numeric keyword (the miss distance, the states, the covariances, the probability; [#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)), for example
`Invalid numeric value for MISS_DISTANCE: 'NaN' is not a finite number.` A value with a comma throws the same exception
(`Invalid numeric value for MISS_DISTANCE: '118,1'.`): `xsd:double` has no comma (XML Schema Part 2, 3.2.5.1), and
before 11.0.0 the reader dropped every comma wherever it stood, so a decimal comma read as another number (`118,1` as
1181 m, a `COLLISION_PROBABILITY` of `4,835E-05` as 0.04835; [#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717), the number half of
[#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619)). The check applies to the value the CDM holds,
in m or m/s, too: a finite text in km or km/s whose product by 1000 is beyond the range of a `double` (above about
1.8e305 km, as `1e306`) throws `CdmParseException` naming the keyword, its text and its unit
(`Invalid numeric value for X: '1e306' km is not a finite number in SI units.`), where it was read as ±Infinity. Before 11.0.0 the CDM was read with the value,
and `Cdm.Validate` reported a NaN miss distance valid; it reports a negative miss distance as an error, NaN and
±Infinity no longer building (below).

The records of a CDM hold real numbers only, as the reader reads them ([#717](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/717)): every real value of
`CdmRelativeMetadataData`, `CdmRelativeStateVector`, `CdmOdParameters`, `CdmAdditionalParameters`, `CdmStateVector` and
`CdmCovarianceMatrix` (a `double`, a `double?`, each component of a `Vector3`, each term of the covariance `Matrix`)
throws `ArgumentOutOfRangeException` naming the property for NaN or ±Infinity, whether set by an object initializer, a
`with` expression or `CdmCovarianceMatrix.FromRtnCovariance`; an optional value may stay null. Before 11.0.0 the record built,
`Cdm.Validate` passed it (but the miss distance) and `WriteToString` (`ToXml` since 11.0.0) wrote `NaN`, `Infinity` or `-Infinity`, which the
reader refuses. `Cdm.Validate` keeps its own check of a miss distance of 0 or more. `StateCovarianceRtn` refuses a
matrix that is not 6×6, `default(Matrix)` included, with `ArgumentException` naming the property, before its terms
([#730](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/730)): before 11.0.0 it built, and `WriteToString` threw `IndexOutOfRangeException` or
`NullReferenceException` (`Cdm.Validate` reported the size when it ran).
`CdmCovarianceMatrix` holds a copy of the matrix it is given, and `StateCovarianceRtn` returns a copy of the one it holds
(#717, when a public `Matrix.Set` wrote the array every copy of the struct shares; `Matrix` is immutable since 11.0.0,
#668, R26). To change a term, build the record again with a changed copy:
`with { StateCovarianceRtn = record.StateCovarianceRtn.With(row, column, value) }`.

### Schema validation

`CcsdsReadOptions.ValidateSchema` checks a document whose root, `ndm` or `cdm`, has no namespace
against the CCSDS NDM/XML 4.0.0 schemas (CCSDS 505.0-B-3) in its form, as SANA publishes them: every element unqualified
(SANA's `ndmxml_unqualified` set) or every element below the root in the namespace `urn:ccsds:schema:ndmxml` (the
qualified set). Any other root is a `CDM001` error, and so is an `ndm`, without a namespace or in it, that holds no `cdm`
child (an empty `ndm`, or one of other messages only), which the NDM schema accepts but in which the reader finds no CDM
(reported valid before 11.0.0). A root in the namespace, the form `CdmWriter` wrote before 11.0.0
([#615](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/615)), is checked as before, against
the CDM schema with that root declared in the namespace, so that those files still validate. Before the third round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) a document in a standard form
matched no declaration of the validator, so it was reported valid with a warning per element and nothing below its root
was checked: a `COLLISION_PROBABILITY` of 1.5 was reported valid
([#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614)).

### Written form

`Cdm.ToXml` and `Cdm.Save`, and so `EncounterCase.ToCdm(...).Save(...)`, write the qualified form
of CCSDS 508.0-B-1 Cor. 2, 4.3.3: the root, `cdm` or the `ndm` that wraps it (`CcsdsWriteOptions.WrapInNdmContainer`, the default), in no
namespace with `xmlns:ndm="urn:ccsds:schema:ndmxml"` declared on it, and every element below it in that namespace under
the prefix `ndm`:

```xml
<ndm xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ndm="urn:ccsds:schema:ndmxml"
     xsi:noNamespaceSchemaLocation="https://sanaregistry.org/r/ndmxml_qualified/ndmxml-4.0.0-master-4.0.xsd">
  <ndm:cdm id="CCSDS_CDM_VERS" version="1.0">
    <ndm:header>
```

The SANA NDM/XML 4.0.0 qualified master, which declares `ndm` and `cdm` in no namespace and which
`xsi:noNamespaceSchemaLocation` names (4.3.3.4), accepts it. Before 11.0.0 the
root was in the namespace too (`<ndm xmlns="urn:ccsds:schema:ndmxml">`,
[#615](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/615)), which that master rejects at the
root ("No matching global declaration available for the validation root"); `Cdm.Load` and `Cdm.Parse`, schema check
included, still accept that form. A reader that matches local names reads both.

## Instance Methods

| Method | Description |
|--------|-------------|
| `Save(path, options?)` | Write the CDM to a file, in UTF-8, or to a `Stream` left open, with the output of `CcsdsWriteOptions`: the content validated, wrapped in an `ndm` and indented by default |
| `ToXml(options?)` | The XML text, as `Save` writes it |
| `Validate()` | The content check, a `CdmValidationResult` |
| `IsValid()` | Whether `Validate()` reports no error |

Before 11.0.0 the writers, `WriteToFile`, `WriteToString` and `WriteToStream`, did not validate: `Save` and `ToXml`
throw `CdmValidationException` for a CDM whose content `Validate()` refuses (a blank `MESSAGE_ID`, a negative miss
distance, a segment without metadata), `new CcsdsWriteOptions { ValidateContent = false }` writing it as before.
`EncounterCase.ToCdm` builds CDMs that validate.

## Export from EncounterCase

`EncounterCase.ToCdm(CdmExportOptions)` converts a conjunction assessment result directly into a `Cdm` object.

### CdmExportOptions

| Property | Type | Description |
|----------|------|-------------|
| `Originator` | `string` | Originating organization |
| `MessageId` | `string` | Unique message identifier |
| `MessageFor` | `string` | Intended recipient |
| `CollisionProbabilityMethod` | `string` | Overrides the method name, `FOSTER-1992` by default |
| `AllowStaleCovariance` | `bool` | Allows exporting an encounter flagged `StaleCovarianceUsed` (refused by default) |

The covariance of each participant is the one the conjunction analysis used (see
[Conjunction Assessment](conjunction-assessment.md#covariance-at-tca)): propagated to TCA, given at TCA, or the
initial-state covariance held fixed in RTN (not propagated), whose age at TCA a comment gives.

Inferred metadata, which `ProtectedParticipantMetadata` and `SecondaryParticipantMetadata` override:

- `MANEUVERABLE` of object 1, the protected spacecraft whose avoidance the library computes, is always `YES`, the
  post-maneuver encounter of an avoidance option included (decision 109 of #668, decided by S. Guillet, 2026-11-26;
  before 11.0.0, `YES` when the `ManeuverConstraints.MaxDeltaVMetersPerSecond` of the protected profile was positive,
  5 m/s by default). Object 2 is `YES` for a spacecraft with at least one engine, `N/A` otherwise: N/A means
  "not available" or "not applicable" (CCSDS 508.0-B-1 Cor. 2, 1.4.3.1, to which the `MANEUVERABLE` row of Table 3-3
  refers), and the library knows the model of the object, not its maneuver capacity
  ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): `YES` for any spacecraft
  before, an engineless one or a debris wrapped in a spacecraft included). An engine counts whatever its tank holds,
  an empty tank (allowed since 11.0.0, decision 76 of #668) included; `SecondaryParticipantMetadata` overrides it;
- `AREA_PC` is π r² for each object, r the protected spacecraft's `HardBodyRadius` for object 1 and, for object 2,
  the combined radius minus it: the radius of the secondary the analysis resolved, its own `HardBodyRadius` when it
  gives one ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): π·5² for any
  secondary under the default options). π r² is the exporter's convention; 508 defines `AREA_PC` as the actual area of
  the object (Annex E);
- `INTRACK_THRUST` is omitted: it states whether in-track thrust modeling was used for the orbit determination of the
  object (CCSDS 508.0-B-1 Cor. 2, Table 3-3, optional), not its capability, which `MANEUVERABLE` states, and the library
  runs no orbit determination. It is `NO` for a covariance the library propagated (below). Set
  `ProtectedParticipantMetadata.IntrackThrust` or `SecondaryParticipantMetadata.IntrackThrust` to state the value of an
  OD; `null` keeps the inferred value
  ([#684](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/684), decided by S. Guillet,
  2026-11-13: `YES` for a spacecraft with an engine and `NO` for one without before);
- `SOLAR_RAD_PRESSURE` is omitted, as `GRAVITY_MODEL`, `ATMOSPHERIC_MODEL`, `N_BODY_PERTURBATIONS` and `EARTH_TIDES`
  are: it states whether solar radiation pressure perturbations were used for the orbit determination of the object
  (CCSDS 508.0-B-1 Cor. 2, Table 3-3, optional; wording confirmed by S. Guillet, 2026-11-15), not whether the spacecraft has a
  reflectivity coefficient. For a covariance the library propagated it is `YES` or `NO` from the forces of the
  propagation (below). Set `ProtectedParticipantMetadata.SolarRadPressure` or
  `SecondaryParticipantMetadata.SolarRadPressure` to state the value of an OD; `null` keeps the inferred value (third
  round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668): `YES` for a
  spacecraft whose `SolarRadiationCoeff` is above 0 before, whether or not any solar radiation pressure was modeled).

### Propagated covariance

For a participant whose covariance was propagated (`CovarianceSource.Propagated`):

- `COVARIANCE_METHOD` is `CALCULATED`;
- with consider parameters, the rows `CDRG_*` and `CSRP_*` come from the extended covariance at TCA. The CDM
  parameters are Cd·A/m and Cr·A/m (CCSDS 508.0-B-1), so each cross term cov(x, C) is rotated to RTN as the state block
  (a pure rotation) and multiplied by A/m, and `CDRG_DRG`, `CSRP_SRP` are σ² (A/m)². A is the sectional area of the
  spacecraft, m its total mass at TCA. Cd and Cr are independent consider parameters: `CSRP_DRG` is 0 when both are;
- `GRAVITY_MODEL` (`EGM2008: nD nO` for the Earth's harmonics to degree and order n evaluated with the constants of
  EGM2008, its default; `GEOPOTENTIAL: nD nO` for harmonics evaluated with other constants, given to
  `GeopotentialModelParameters` or those of another body; `POINT MASS` otherwise), `ATMOSPHERIC_MODEL` (`NRLMSISE-00`,
  `US STANDARD ATMOSPHERE 1976`, `MARS STANDARD ATMOSPHERE`, `NONE` without drag), `N_BODY_PERTURBATIONS` (the third
  bodies, `NONE` without any) and `SOLAR_RAD_PRESSURE` (`YES` or `NO`, whatever the `SolarRadiationCoeff` of the
  spacecraft) describe the forces the propagation actually used. The library
  does not know the name of a model whose constants are given; set it through `ProtectedParticipantMetadata` or
  `SecondaryParticipantMetadata` (`GravityModel = "EGM-96: 36D 36O"`), which take precedence;
- `INTRACK_THRUST` is `NO`, whatever the engines of the spacecraft: the forces of the propagation hold no thrust model.
  An impulsive maneuver the propagation executed is not a thrust model in the sense of Table 3-3, and the CDM has no
  keyword for executed maneuvers, so a participant whose propagation carried impulses is `NO` too;
- comments of the data give the source of the covariance, the process noise applied (q_R, q_T, q_N in m²/s³) or
  "No process noise applied", the consider parameters and the non-linearity index.

An export with a propagated covariance and both consider parameters passes the schema and content validation and reads
back to the digit (`CdmPropagatedCovarianceTests`). A propagated covariance is at TCA and never stale, so it does not
need `AllowStaleCovariance`.

### Reference frame

States in `Frame.ICRF` (SPICE `J2000`) and `Frame.GCRF` are written with `REF_FRAME = GCRF`. Both
frames share the ICRF axes, so the label is exact and the same state gives the same CDM content in
either frame. A state in a frame named `EME2000` is labelled `EME2000`, and a state in `ITRF93` is
labelled `ITRF`.

## Unit Conversion

The managed API stores values internally in SI units. The CDM writer emits CCSDS-required XML units:

| Quantity | Internal (SI) | CDM (XML) |
|----------|---------------|-----------|
| Position (state vector) | m | km |
| Velocity | m/s | km/s |
| Miss distance | m | m |
| Relative position | m | m |

The writer writes each value in its shortest round-trip form (`"R"`), which reads back bit for bit (`G17` before
11.0.0, exact as well but longer: 0.1 written 0.10000000000000001;
[#728](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/728)). The reader converts each value from its `units` attribute. A length of the `lengthTypeUO` type (`MISS_DISTANCE`,
`RELATIVE_POSITION_R/T/N`, `SCREEN_VOLUME_X/Y/Z`), whose attribute the schema makes optional with `m` as its only unit, is
read in m when the attribute is absent ([#613](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/613); km before 11.0.0), and any units other than `m` throw
`CdmParseException` naming the keyword and the units ([#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619), decided by S. Guillet, 2026-11-17: `units="km"`
was converted before 11.0.0). A state vector position, whose `units` the schema requires, defaults to km.

Dates are read in both forms of the NDM/XML `epochType` ([#490](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/490)): calendar (`2026-01-13T05:00:55.752768`)
and day of year (`2026-013T05:00:55.752768`), with any fraction, an optional `Z` or `±hh:mm` offset, as UTC: the two
forms of CCSDS 508.0-B-1 §6.3.2.6, each field with its leading zeros. Any other text throws `CdmParseException` naming
the field and the text ([#619](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/619), decided by S. Guillet, 2026-11-17): a time alone, a date alone, a bare number
(`1.5`, which the bare-number branch of `epochType` admits but §6.3.2.6 does not), a space or a lowercase `t` for the
`T`, RFC 1123, a decimal comma, a blank around the date. Before 11.0.0 the reader passed such a text to
`DateTime.TryParse`: a `TCA` of `22:37:52.618` was read on the day of the read, `1.5` or `1,5` as 5 January of the
current year. The OPM and OMM readers share the parser (`CcsdsEpochParser`). The writer emits the calendar form, in UTC as every date of a
CDM (CCSDS 508.0-B-1 Cor. 2, 6.3.2.6), by the kind of each `DateTime`
([#617](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/617)): `Utc` as given, `Unspecified`
taken as UTC (the clock reading given, as `EncounterCase.ToCdm` already took it), `Local` converted to UTC. Before
11.0.0 the writer read a date of kind `Unspecified`, the kind of `new DateTime(y, M, d, h, m, s)`, as local time: a CDM
built by hand on a machine outside UTC was written with its dates shifted by the zone's offset (one hour early in
Paris in winter, five hours late in New York).

Standards-compliant CDM export requires covariance for both participants, so the exported
`COLLISION_PROBABILITY_METHOD` is `FOSTER-1992`: Foster and Estes (1992), the 2D Gaussian integrated over the
hard-body disc, under its name in the SANA registry `cdm_cpm` (CCSDS 508.0-B-1 Cor. 2, annex E, which gives it as an
example; [#618](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/618), decided by S. Guillet,
2026-11-17; `FOSTER-2D`, which the registry does not hold, before 11.0.0). An encounter whose Pc is the maximum of a
single covariance (`SingleCovarianceMaximumPcUsed`), which no registered value names, is refused with
`InvalidOperationException`, whatever `CollisionProbabilityMethod` says.

## Example

```csharp
using IO.Astrodynamics.CCSDS.CDM;

// Inputs: encounter (EncounterCase)
var cdm = encounter.ToCdm(new CdmExportOptions
{
    Originator = "MyOpsCenter",
    MessageId = "CDM-20260315-001",
    MessageFor = "Protected"
});

// Save validates the content and Load the schema and the content, throwing CdmValidationException
cdm.Save("encounter.cdm.xml");

var parsed = Cdm.Load("encounter.cdm.xml");
```

## Ingest

A CDM read from a provider gives the states of its objects and the encounter it describes (R19 of #668, lot L7):

| Member | Description |
|--------|-------------|
| `ToStateVector(CdmObjectRole)` | The state of an object at `TCA` (UTC), about the centre `ORBIT_CENTER` names (the Earth without one): `EME2000` and `GCRF` in `Frame.ICRF`, its covariance the RTN covariance turned on that state (`RotateCovarianceFromRtn`); `ITRF` in `Frame.ITRF93`, its covariance turned on the state in ICRF, then carried into ITRF93 (`ToFrame`). An `ORBIT_CENTER` the library does not identify, an `ITRF` state about another centre than the Earth, a segment without a state or a `REF_FRAME` throw `InvalidOperationException`, a role outside the enumeration `ArgumentOutOfRangeException` |
| `ToEncounter(combinedHardBodyRadiusMeters)` | The `EncounterCase` at `TCA` from the two states as given, in ICRF, no search (`Search` is `CloseApproachSearch.None`; decision 110 of #668, decided by S. Guillet, 2026-11-26): its Pc is `CollisionProbability.Foster2D` of the two states bit for bit (`FrisbeeMax` when one covariance is absent), its screening window `START_SCREEN_PERIOD` to `STOP_SCREEN_PERIOD` (`TCA` alone without them); the objects are `Spacecraft` of 100 kg on their states with transient NAIF ids, so `EvaluateAvoidance` follows their two-body orbits (`TrajectorySource.TwoBody`). The radius, which a CDM does not give, is finite and above 0, or `ArgumentOutOfRangeException` |

`ToCdm` of such an encounter is not a re-export of the CDM read: the masses, areas and designators it writes are those
of the transient spacecraft. The dates of the records have a `Time` accessor in UTC beside each `DateTime` one
(`CdmHeader.CreationDate`, `CdmRelativeMetadataData.Tca`, `StartScreenPeriod`, `StopScreenPeriod`, `ScreenEntryTime`,
`ScreenExitTime`, `CdmOdParameters.TimeLastObservationStart`, `TimeLastObservationEnd`), a date of unspecified kind taken
as UTC, as the writer takes it.

```csharp
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SSA;

// Inputs: cdm (Cdm)
StateVector object1 = cdm.ToStateVector(CdmObjectRole.Object1);
StateVector object2 = cdm.ToStateVector(CdmObjectRole.Object2);
EncounterCase encounter = cdm.ToEncounter(combinedHardBodyRadiusMeters: 10.0);
double? pc = CollisionProbability.Foster2D(object1, object2, 10.0); // encounter.ProbabilityOfCollision, bit for bit
Console.WriteLine(encounter);
```

## See Also

- [Conjunction Assessment](conjunction-assessment.md)
- [Avoidance Studies](avoidance.md)
- [OMM](omm.md)
- [OPM](opm.md)
