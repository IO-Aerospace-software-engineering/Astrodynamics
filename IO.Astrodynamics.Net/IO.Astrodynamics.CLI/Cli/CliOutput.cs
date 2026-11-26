using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>The output format of a command (R29 of #668, choice 4).</summary>
internal enum OutputFormat
{
    /// <summary>The lines of the library's <c>ToString</c>, those of the command before 11.0.0.</summary>
    Text,

    /// <summary>One header line of field names, then one line per row: the tabular commands.</summary>
    Csv,

    /// <summary>One object of named fields: the record commands.</summary>
    Json
}

/// <summary>
/// The one writer of each output format (R29 of #668, choice 4): the field names carry their SI unit (<c>x_m</c>,
/// <c>vx_m_s</c>, <c>i_rad</c>), the reals are written in their shortest round-trip form in invariant culture, read back
/// bit for bit, and an epoch as its label with its scale (<see cref="Time.ToString()"/>).
/// </summary>
internal static class CliOutput
{
    /// <summary>
    /// The format of a <c>--format</c>: text by default; csv for a tabular command, json for a record command, the other
    /// one an input error.
    /// </summary>
    internal static OutputFormat Format(string text, bool tabular)
    {
        var format = CliValues.Choice(text, "--format", "text", "csv", "json");
        switch (format)
        {
            case null or "text":
                return OutputFormat.Text;
            case "csv" when tabular:
                return OutputFormat.Csv;
            case "json" when !tabular:
                return OutputFormat.Json;
            default:
                throw new CliInputException(tabular
                    ? $"--format: this command writes text or csv, not {format}."
                    : $"--format: this command writes text or json, not {format}.");
        }
    }

    /// <summary>A real in its shortest round-trip form, in invariant culture.</summary>
    internal static string Real(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Writes a table in csv: the header, then the rows, a field with a comma, a quote or a line break quoted.</summary>
    internal static void WriteCsv(TextWriter writer, IReadOnlyList<Field> header, IEnumerable<IReadOnlyList<Field>> rows)
    {
        writer.WriteLine(string.Join(",", header.Select(x => Quote(x.Name))));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", row.Select(x => Quote(x.Text))));
        }
    }

    private static string Quote(string text) =>
        text.IndexOfAny([',', '"', '\n', '\r']) < 0 ? text : $"\"{text.Replace("\"", "\"\"")}\"";

    /// <summary>Writes a record in json, one object of its fields in order.</summary>
    internal static void WriteJson(TextWriter writer, IReadOnlyList<Field> fields)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            foreach (var field in fields)
            {
                switch (field.Value)
                {
                    case double d when !double.IsFinite(d):
                        // json has no NaN: a value the body does not have (J2 to J4 of a body without them) is null
                        json.WriteNull(field.Name);
                        break;
                    case double d:
                        // The text of the round-trip form, a valid json number for a finite value
                        json.WritePropertyName(field.Name);
                        json.WriteRawValue(Real(d));
                        break;
                    case int i:
                        json.WriteNumber(field.Name, i);
                        break;
                    default:
                        json.WriteString(field.Name, field.Text);
                        break;
                }
            }

            json.WriteEndObject();
        }

        writer.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>A named value of a row or a record.</summary>
    internal readonly record struct Field(string Name, object Value)
    {
        /// <summary>The value as text: a real in its round-trip form, an epoch as its label.</summary>
        public string Text => Value switch
        {
            double d => Real(d),
            int i => i.ToString(CultureInfo.InvariantCulture),
            null => string.Empty,
            _ => Value.ToString()
        };
    }

    private static Field[] Header(params string[] names) => names.Select(x => new Field(x, null)).ToArray();

    // The fields of each kind of row or record, the names of the header and of the json object

    internal static readonly Field[] StateHeader = Header("epoch", "frame", "x_m", "y_m", "z_m", "vx_m_s", "vy_m_s", "vz_m_s");

    internal static Field[] State(StateVector sv) =>
    [
        new("epoch", sv.Epoch.ToString()), new("frame", sv.Frame.Name),
        new("x_m", sv.Position.X), new("y_m", sv.Position.Y), new("z_m", sv.Position.Z),
        new("vx_m_s", sv.Velocity.X), new("vy_m_s", sv.Velocity.Y), new("vz_m_s", sv.Velocity.Z)
    ];

    internal static readonly Field[] KeplerianHeader = Header("epoch", "frame", "a_m", "e", "i_rad", "raan_rad", "aop_rad", "m_rad");

    internal static Field[] Keplerian(KeplerianElements ke) =>
    [
        new("epoch", ke.Epoch.ToString()), new("frame", ke.Frame.Name),
        new("a_m", ke.SemiMajorAxis()), new("e", ke.Eccentricity()), new("i_rad", ke.Inclination()), new("raan_rad", ke.AscendingNode()), new("aop_rad", ke.ArgumentOfPeriapsis()), new("m_rad", ke.MeanAnomaly())
    ];

    internal static readonly Field[] EquinoctialHeader = Header("epoch", "frame", "p_m", "f", "g", "h", "k", "l0_rad");

    internal static Field[] Equinoctial(EquinoctialElements eq) =>
    [
        new("epoch", eq.Epoch.ToString()), new("frame", eq.Frame.Name),
        new("p_m", eq.P), new("f", eq.F), new("g", eq.G), new("h", eq.H), new("k", eq.K), new("l0_rad", eq.L0)
    ];

    /// <summary>The fields of orbital parameters of one of the three kinds the commands write.</summary>
    internal static Field[] Orbit(OrbitalParameters.OrbitalParameters op) => op switch
    {
        KeplerianElements ke => Keplerian(ke),
        EquinoctialElements eq => Equinoctial(eq),
        StateVector sv => State(sv),
        _ => throw new InvalidOperationException($"No fields for {op.GetType().Name}.")
    };

    /// <summary>The header of the rows of <see cref="Orbit"/>.</summary>
    internal static Field[] OrbitHeader(OrbitalParameters.OrbitalParameters op) => op switch
    {
        KeplerianElements => KeplerianHeader,
        EquinoctialElements => EquinoctialHeader,
        _ => StateHeader
    };

    internal static readonly Field[] OrientationHeader =
        Header("epoch", "frame", "qw", "qx", "qy", "qz", "wx_rad_s", "wy_rad_s", "wz_rad_s");

    /// <summary>
    /// The text line of an orientation: that of <see cref="StateOrientation.ToString"/>, which writes its epoch in TDB, with
    /// the epoch as it is labelled (in the scale of the output); bit for bit the library's line for a TDB label.
    /// </summary>
    internal static string OrientationText(StateOrientation so) =>
        $"Epoch : {so.Epoch} Orientation : {so.Rotation} Angular velocity : {so.AngularVelocity} Frame : {so.ReferenceFrame.Name}";

    internal static Field[] Orientation(StateOrientation so) =>
    [
        new("epoch", so.Epoch.ToString()), new("frame", so.ReferenceFrame.Name),
        new("qw", so.Rotation.W), new("qx", so.Rotation.VectorPart.X), new("qy", so.Rotation.VectorPart.Y),
        new("qz", so.Rotation.VectorPart.Z),
        new("wx_rad_s", so.AngularVelocity.X), new("wy_rad_s", so.AngularVelocity.Y), new("wz_rad_s", so.AngularVelocity.Z)
    ];

    internal static readonly Field[] WindowHeader = Header("start", "end", "length_s");

    internal static Field[] Window(Window window) =>
        [new("start", window.StartDate.ToString()), new("end", window.EndDate.ToString()), new("length_s", window.Length.TotalSeconds)];

    internal static Field[] Body(CelestialBody body) =>
    [
        new("naif_id", body.NaifId), new("name", body.Name), new("mass_kg", body.Mass), new("gm_m3_s2", body.GM),
        new("fixed_frame", body.Frame.Name), new("equatorial_radius_m", body.EquatorialRadius),
        new("polar_radius_m", body.PolarRadius), new("flattening", body.Flattening),
        new("j2", body.J2), new("j3", body.J3), new("j4", body.J4)
    ];

    internal static Field[] Planetocentric(Planetocentric p) =>
        [new("longitude_rad", p.Longitude), new("latitude_rad", p.Latitude), new("radius_m", p.Radius)];

    internal static Field[] Planetodetic(Planetodetic p) =>
        [new("longitude_rad", p.Longitude), new("latitude_rad", p.Latitude), new("altitude_m", p.Altitude)];

    internal static Field[] Cartesian(Vector3 v) => [new("x_m", v.X), new("y_m", v.Y), new("z_m", v.Z)];
}
