# CCSDS OMM

The `IO.Astrodynamics.CCSDS.OMM` module supports loading, validating, serializing, and converting CCSDS Orbit Mean-Elements Messages.

## Omm Class

`Omm` represents a parsed OMM message containing mean orbital elements and associated metadata.

### Loading

| Method | Description |
|--------|-------------|
| `Omm.LoadFromFile(path, validateSchema?, validateContent?)` | Load from file with optional validation |
| `Omm.LoadFromString(xml, validateSchema?, validateContent?)` | Load from XML string |
| `Omm.LoadFromStream(stream, validateSchema?, validateContent?)` | Load from stream |
| `Omm.TryLoadFromFile(path, out omm, out result, ...)` | Non-throwing load with validation result |

Each load reads one OMM: a standalone `<omm>`, or the one `<omm>` of an `<ndm>` or of another root (messages of
another type beside it are ignored). A document that holds several OMMs, such as a catalog NDM, or a second OMM at any depth below the root, throws
`OmmParseException` naming their number, `TryLoadFromFile` returns false with a `PARSE_ERROR` and
`OmmValidator.ValidateFile` reports it invalid (#679); before 11.0.0 the first one was read. Split such a document into
one OMM per document.

The OMM itself holds one header and one body, the body one segment, the segment one metadata and one data section, and
the data at most one of each block (NDM/XML 4.0.0, `ndmxml-4.0.0-omm-3.0.xsd`; CCSDS 502.0-B-3). An OMM
with a second segment, or a second header, body, metadata, data section or block, throws `OmmParseException` naming
the element and its count without `validateSchema`; with it the schema rejects the document first, an
`OmmValidationException` (`TryLoadFromFile` returns false with the schema error and `OmmValidator.ValidateFile` reports it
invalid; without schema validation, a `PARSE_ERROR` and the reader's error). Before 11.0.0 the first one was read and
the others dropped, a whole second segment included. Split such a document into one message per segment.

### Schema validation

`validateSchema` and `OmmValidator.ValidateSchema` check the document against the CCSDS NDM/XML 4.0.0 schemas (CCSDS
505.0-B-3) in its form, as SANA publishes them: a root `ndm` or `omm` without a namespace, every element unqualified (the
form of Space-Track and of the writer, SANA's `ndmxml_unqualified` set) or every element below the root in the
namespace `urn:ccsds:schema:ndmxml` (the qualified set). Any other root is an error. A root in the namespace, the
library's earlier form of a bare message, is checked as before, against the OMM schema with that root declared in the
namespace (an `ndm` root in the namespace has no declaration and is rejected, by SANA too). The unqualified set is the
embedded one with its `elementFormDefault` set to `unqualified`, the one difference between the two sets SANA publishes.

Before the third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) an
unqualified document matched no declaration of the validator, so it was reported valid with a warning per element and
nothing below its root was checked: an OMM with `ECCENTRICITY` -0.5 threw `ArgumentOutOfRangeException` from
`MeanElements` and one with `INCLINATION` in `rad` loaded, and undeclared elements were ignored. Such a document now
makes `Load*` throw `OmmValidationException`; fix the document, or pass `validateSchema: false` to read it as before. A
valid document, the writer's output included, validates without a warning.

### Saving

| Method | Description |
|--------|-------------|
| `SaveToFile(path, validateBeforeSave?)` | Save to file |
| `SaveToStream(stream, validateBeforeSave?)` | Save to stream |
| `ToXml()` | Serialize to XML string |

### Factory Methods

| Method | Description |
|--------|-------------|
| `Omm.CreateForTle(...)` | Create an OMM from TLE-compatible data |
| `Omm.CreateMinimal(...)` | Create a minimal OMM with required fields only |

### Properties

| Property | Description |
|----------|-------------|
| `IsTleCompatible` | Whether this OMM can be converted to a TLE: it holds TLE parameters, its `MEAN_ELEMENT_THEORY` is SGP4, its `REF_FRAME` TEME and its `CENTER_NAME` the Earth (each read trimmed and in any letter case, so ` TEME ` and `sgp4` are TEME and SGP4). SGP4-XP and DSST are not compatible: the library runs the classic SGP4 (CSPICE `evsgp4_c`). Before 11.0.0 only the TLE parameters were checked (#679) |
| `ObjectName` | Satellite name |
| `CosparId` | Full COSPAR international designator (e.g., `1998-067A`) |

### Conversion & Validation

| Method | Description |
|--------|-------------|
| `ToTle()` | Convert to a `TLE` object (requires `IsTleCompatible`; an OMM with TLE parameters that is not SGP4, TEME and Earth throws `InvalidOperationException` naming each keyword at fault and its value, #679); `EPOCH` is read in `TIME_SYSTEM` (UTC, TAI, TDB, TT or GPS) and written as its UTC, another time system throws (#485) |
| `Validate()` | Validate and return `OmmValidationResult` |
| `IsValid()` | Quick validity check (returns `bool`) |

## Supporting Types

| Type | Description |
|------|-------------|
| `OmmReader` | Direct XML-to-object reader |
| `OmmWriter` | Direct object-to-XML writer |
| `OmmValidator` | Schema and content validation |

## COSPAR ID Format

OMM uses full COSPAR IDs such as `1998-067A`. TLE uses abbreviated IDs such as `98067A`. The conversion layer handles this automatically when calling `ToTle()`.

## Example

```csharp
// Load and convert to TLE
var omm = Omm.LoadFromFile("satellite.omm",
    validateSchema: true, validateContent: true);

if (omm.IsTleCompatible)
{
    var tle = omm.ToTle();
    var state = tle.ToOsculating(new Time(2024, 6, 1, 12, 0, 0));
    Console.WriteLine($"Position: {state.Position}");
}

// Save back to XML
omm.SaveToFile("output.omm");
```

## See Also

- [TLE](tle.md)
- [OPM](opm.md)
- [CDM](cdm.md)
