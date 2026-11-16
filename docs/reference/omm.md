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

A keyword whose value the data model refuses throws `OmmParseException` naming the keyword, its value, its block and the
rule, the data model's exception as `InnerException` (fourth round of
[#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)): `ECCENTRICITY` negative,
`INCLINATION` outside [0, 180], `RA_OF_ASC_NODE`, `ARG_OF_PERICENTER` or `MEAN_ANOMALY` outside [-360, 360),
`MEAN_MOTION` or `SEMI_MAJOR_AXIS` not above 0, `GM` not above 0, `ELEMENT_SET_NO` outside [0, 9999], `REV_AT_EPOCH`
negative, a negative spacecraft parameter, an empty `OBJECT_NAME`, `OBJECT_ID`, `CENTER_NAME`, `REF_FRAME`,
`TIME_SYSTEM`, `MEAN_ELEMENT_THEORY` or `ORIGINATOR`, or a blank `USER_DEFINED` parameter name, for example
`ECCENTRICITY '-0.5' in meanElements is invalid: Eccentricity must be non-negative.` Before 11.0.0 the
`ArgumentOutOfRangeException` or `ArgumentException` of the data model's constructor crossed the reader, naming its
parameter. With `validateSchema` the schema rejects most of these values first (`OmmValidationException`), but not those
its types allow: `MEAN_MOTION` and `SEMI_MAJOR_AXIS` are `xsd:double` and the metadata strings `xsd:string`, so a
`MEAN_MOTION` of 0 or an empty `OBJECT_NAME` throws `OmmParseException` with the default validation too.
`TryLoadFromFile` returns false with a `PARSE_ERROR` and `OmmValidator.ValidateFile` an error, both naming the keyword.
The constructors of the data model (`MeanElements`, `OmmMetadata`, ...) keep their `ArgumentException`; a ranged
parameter of `MeanElements` (a, n, e, i, the three angles, GM) or `SpacecraftParameters` refuses NaN and ±Infinity with
its `ArgumentOutOfRangeException`, and so does `TLE.ToOmm`, which builds them, for such a value, where it wrote `NaN` ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)).

A numeric value is a real number: a text that .NET reads as NaN or ±Infinity (`NaN`, `Infinity` or `-Infinity` in any
case, or a value beyond the range of a `double` such as `1e400`, which .NET rounds to ±Infinity) throws
`OmmParseException` naming the keyword and the text, for every numeric keyword of the message ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708)), for example
`Failed to parse numeric value 'NaN' for field 'ECCENTRICITY': not a finite number.` Before 11.0.0 the document loaded
with the value, which every range check passed, the comparisons with NaN being false. The schema step does not stop
these values: .NET's schema validator accepts NaN against the bound facets of NDM/XML 4.0.0 and `Infinity` or an
overflow as an `xsd:double`. `TryLoadFromFile` returns false with a `PARSE_ERROR` and `OmmValidator.ValidateFile` an error,
both naming the keyword. The lexical `INF` and `-INF` of `xsd:double` throw the parse exception, as before.

### Schema validation

`validateSchema` and `OmmValidator.ValidateSchema` check the document against the CCSDS NDM/XML 4.0.0 schemas (CCSDS
505.0-B-3) in its form, as SANA publishes them: a root `ndm` or `omm` without a namespace, every element unqualified (the
form of Space-Track and of the writer, SANA's `ndmxml_unqualified` set) or every element below the root in the
namespace `urn:ccsds:schema:ndmxml` (the qualified set). Any other root is an error, and so is an `ndm` that holds no
`omm` child (an empty `ndm`, or one of other messages only), which the NDM master accepts but in which the reader
finds no OMM (`OMM007`; reported valid before 11.0.0, `Load*` then throwing `OmmParseException`).
A root in the namespace, the library's earlier form of a bare message, is checked as before, against the OMM schema with that root declared in the
namespace (an `ndm` root in the namespace has no declaration and is rejected, by SANA too). The unqualified set is the
embedded one with its `elementFormDefault` set to `unqualified`, the one difference between the two sets SANA publishes.

Before the third round of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668) an
unqualified document matched no declaration of the validator, so it was reported valid with a warning per element and
nothing below its root was checked: an OMM with `ECCENTRICITY` -0.5 threw `ArgumentOutOfRangeException` from
`MeanElements` and one with `INCLINATION` in `rad` loaded, and undeclared elements were ignored. Such a document now
makes `Load*` throw `OmmValidationException`; fix the document, or pass `validateSchema: false` to read it without the
schema step, where a keyword out of its range, as that `ECCENTRICITY`, throws `OmmParseException` since the fourth round
(above). A valid document, the writer's output included, validates without a warning.

### Content validation

`validateContent`, `Validate()` and `OmmValidator.Validate` check the physical constraints of the data model's values. Each
error check is the negation of its admitted range, so that NaN, in no range, is an error (`OMM002`), as is an infinite
value where the range is half-open: a covariance variance must be finite and non-negative, which the covariance
constructors, without a range, do not check ([#708](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/708); before 11.0.0 a NaN variance was valid). The warnings and the
information levels (an unusual drag coefficient, a large eccentricity) are unchanged.

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
