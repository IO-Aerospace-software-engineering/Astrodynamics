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

The covariance of each participant is resolved as in the conjunction analysis: the covariance at
TCA, otherwise the initial-state covariance held fixed in RTN (not propagated). In the second case a
comment gives its age at TCA. See
[Conjunction Assessment](conjunction-assessment.md).

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
