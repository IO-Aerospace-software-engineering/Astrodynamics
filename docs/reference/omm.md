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
