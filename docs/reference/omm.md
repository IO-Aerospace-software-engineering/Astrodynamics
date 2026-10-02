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
| `IsTleCompatible` | Whether this OMM can be converted to a TLE |
| `ObjectName` | Satellite name |
| `CosparId` | Full COSPAR international designator (e.g., `1998-067A`) |

### Conversion & Validation

| Method | Description |
|--------|-------------|
| `ToTle()` | Convert to a `TLE` object (requires `IsTleCompatible`) |
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
