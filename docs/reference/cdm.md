# CCSDS CDM

!!! note
    The CDM types live in the `IO.Astrodynamics.CCSDS.CDM` namespace.

The CDM module supports constructing, reading, writing, and validating CCSDS Conjunction Data Messages in XML format.

## Cdm Object Model

`Cdm` is the root object representing a complete Conjunction Data Message with header, relative metadata, and two object entries.

## Static Methods

| Method | Description |
|--------|-------------|
| `Cdm.ReadFromFile(path)` | Parse a CDM XML file into a `Cdm` object |
| `Cdm.ReadFromString(xml)` | Parse a CDM XML string |
| `Cdm.ReadFromStream(stream)` | Parse from a stream |
| `Cdm.ValidateSchema(path)` | Validate a CDM file against the CCSDS schema |
| `Cdm.ValidateSchemaFromXml(xml)` | Validate a CDM XML string against the schema |

The readers read one CDM: a standalone `<cdm>`, or the one `<cdm>` of an `<ndm>` or of another root. A document that holds
several CDMs, at any depth below the root, throws `CdmParseException` naming their number (#679); before 11.0.0 the first one was returned. Split such
a document into one CDM per document.

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
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)). The readers do not check the
schema: `Cdm.ValidateSchemaFromXml` rejects these values too.

### Schema validation

`Cdm.ValidateSchema` and `Cdm.ValidateSchemaFromXml` check a document whose root, `ndm` or `cdm`, has no namespace
against the CCSDS NDM/XML 4.0.0 schemas (CCSDS 505.0-B-3) in its form, as SANA publishes them: every element unqualified
(SANA's `ndmxml_unqualified` set) or every element below the root in the namespace `urn:ccsds:schema:ndmxml` (the
qualified set). Any other root is a `CDM001` error, and so is an `ndm`, without a namespace or in it, that holds no `cdm`
child (an empty `ndm`, or one of other messages only), which the NDM schema accepts but in which the reader finds no CDM
(reported valid before 11.0.0). A root in the namespace, the form `CdmWriter` writes
([#615](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/615)), is checked as before, against
the CDM schema with that root declared in the namespace. Before the third round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) a document in a standard form
matched no declaration of the validator, so it was reported valid with a warning per element and nothing below its root
was checked: a `COLLISION_PROBABILITY` of 1.5 was reported valid
([#614](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/614)).

## Instance Methods

| Method | Description |
|--------|-------------|
| `WriteToFile(path)` | Serialize the CDM to an XML file |
| `WriteToString()` | Serialize the CDM to an XML string |

## Supporting Types

| Type | Description |
|------|-------------|
| `CdmWriter` | Direct object-to-XML writer |
| `CdmReader` | Direct XML-to-object reader |
| `CdmValidator` | Schema and content validation |

## Export from EncounterCase

`EncounterCase.ToCdm(CdmExportOptions)` converts a conjunction assessment result directly into a `Cdm` object.

### CdmExportOptions

| Property | Type | Description |
|----------|------|-------------|
| `Originator` | `string` | Originating organization |
| `MessageId` | `string` | Unique message identifier |
| `MessageFor` | `string` | Intended recipient |
| `CollisionProbabilityMethod` | `string` | Overrides the method name, `FOSTER-2D` by default |
| `AllowStaleCovariance` | `bool` | Allows exporting an encounter flagged `StaleCovarianceUsed` (refused by default) |

The covariance of each participant is the one the conjunction analysis used (see
[Conjunction Assessment](conjunction-assessment.md#covariance-at-tca)): propagated to TCA, given at TCA, or the
initial-state covariance held fixed in RTN (not propagated), whose age at TCA a comment gives.

Inferred metadata, which `ProtectedParticipantMetadata` and `SecondaryParticipantMetadata` override:

- `MANEUVERABLE` of object 1 is `YES` when the `ManeuverConstraints.MaxDeltaVMetersPerSecond` of the protected profile
  is positive, `NO` otherwise. Object 2 is `YES` for a spacecraft with at least one engine, `N/A` otherwise: N/A means
  "not available" or "not applicable" (CCSDS 508.0-B-1 Cor. 2, 1.4.3.1, to which the `MANEUVERABLE` row of Table 3-3
  refers), and the library knows the model of the object, not its maneuver capacity
  ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): `YES` for any spacecraft
  before, an engineless one or a debris wrapped in a spacecraft included);
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
  (CCSDS 508.0-B-1 Cor. 2, Table 3-3, optional; wording to be verified by S. Guillet), not whether the spacecraft has a
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

The reader converts each value from its `units` attribute. A length of the `lengthTypeUO` type (`MISS_DISTANCE`,
`RELATIVE_POSITION_R/T/N`, `SCREEN_VOLUME_X/Y/Z`), whose attribute the schema makes optional with `m` as its only unit, is
read in m when the attribute is absent ([#613](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/613); km before 11.0.0). A state vector position, whose
`units` the schema requires, defaults to km.

Dates are read in both forms of the NDM/XML `epochType` ([#490](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/490)): calendar (`2026-01-13T05:00:55.752768`)
and day of year (`2026-013T05:00:55.752768`), with any fraction, an optional `Z` or `±hh:mm` offset, as UTC. The OPM
and OMM readers share the parser (`CcsdsEpochParser`). The writer emits the calendar form.

Standards-compliant CDM export requires covariance for both participants, so the exported
`COLLISION_PROBABILITY_METHOD` is `FOSTER-2D`.

## Example

```csharp
using IO.Astrodynamics.CCSDS.CDM;

var cdm = encounter.ToCdm(new CdmExportOptions
{
    Originator = "MyOpsCenter",
    MessageId = "CDM-20260315-001",
    MessageFor = "Protected"
});

cdm.WriteToFile("encounter.cdm.xml");

var validation = Cdm.ValidateSchema("encounter.cdm.xml");
if (!validation.IsValid)
    throw new InvalidOperationException("CDM failed schema validation.");

var parsed = Cdm.ReadFromFile("encounter.cdm.xml");
```

## See Also

- [Conjunction Assessment](conjunction-assessment.md)
- [Avoidance Studies](avoidance.md)
- [OMM](omm.md)
- [OPM](opm.md)
