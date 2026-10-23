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
  bodies, `NONE` without any) and `SOLAR_RAD_PRESSURE` describe the forces the propagation actually used. The library
  does not know the name of a model whose constants are given; set it through `ProtectedParticipantMetadata` or
  `SecondaryParticipantMetadata` (`GravityModel = "EGM-96: 36D 36O"`), which take precedence;
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
