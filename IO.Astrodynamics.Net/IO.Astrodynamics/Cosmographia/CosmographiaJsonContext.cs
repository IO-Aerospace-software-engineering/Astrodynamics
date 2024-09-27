using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IO.Astrodynamics.Cosmographia.Model;

namespace IO.Astrodynamics.Cosmographia;

[JsonSerializable(typeof(LoadRootObject))]
[JsonSerializable(typeof(SiteRootObject))]
[JsonSerializable(typeof(ObservationRootObject))]
[JsonSerializable(typeof(SensorRootObject))]
[JsonSerializable(typeof(SpacecraftRootObject))]
[JsonSerializable(typeof(SpiceRootObject))]
internal partial class CosmographiaJsonContext : JsonSerializerContext
{
}