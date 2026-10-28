using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// A field file of the suite's <c>data/</c> and its constants (spec.md section 12.1): the body it belongs to, the degree
/// it reaches, and the GM and reference radius of the model, which apply to the whole field, central term included.
/// </summary>
/// <param name="Model">The name of the file, the case's <c>geopotential_model</c>.</param>
/// <param name="BodyNaifId">The NAIF id of the body of the field.</param>
/// <param name="MaximumDegree">The largest degree of the file.</param>
/// <param name="GravitationalParameter">The GM of the model, m³/s².</param>
/// <param name="ReferenceRadius">The reference radius of the model, m.</param>
public sealed record GeopotentialField(
    string Model,
    int BodyNaifId,
    int MaximumDegree,
    double GravitationalParameter,
    double ReferenceRadius)
{
    /// <summary>
    /// The field to <paramref name="degree"/> with order = degree, read from <paramref name="dataPath"/>: the model's
    /// GM and radius on the harmonics and on the central term (<see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>),
    /// as GMAT applies the header of a <c>.cof</c> (decision 18 of #457). At degree 0 or 1 the field is the point mass
    /// of the model's GM (degrees 0 and 1 are absent from the file).
    /// </summary>
    /// <exception cref="InvalidOperationException">The degree is not one of the file, or the file is missing or is a
    /// Git LFS pointer.</exception>
    public GeopotentialModelParameters Parameters(string dataPath, int degree)
    {
        if (degree < 0 || degree > MaximumDegree)
        {
            throw new InvalidOperationException(
                $"The field {Model} goes to degree {MaximumDegree}: degree {degree} is not one of its degrees (spec.md section 12.1).");
        }

        var path = Path.Combine(dataPath, Model);
        SuiteDataFile.EnsureContent(path);
        return new GeopotentialModelParameters(path, (ushort)degree, GravitationalParameter, ReferenceRadius)
        {
            CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
        };
    }
}

/// <summary>
/// The field files of the <c>propagator</c> category, by their names in the suite's <c>data/</c>: the table of spec.md
/// section 12.1. Every other name is rejected: the constants of a field come from this table, never from another
/// field's.
/// </summary>
public static class GeopotentialFieldCatalog
{
    /// <summary>The name of a case without a field: the central body is a point mass with its kernel GM.</summary>
    public const string None = "none";

    /// <summary>The fields of spec.md section 12.1.</summary>
    public static IReadOnlyList<GeopotentialField> Fields { get; } = new[]
    {
        // EGM2008, Pavlis et al. 2012: the constants of the model
        new GeopotentialField("EGM2008_to70_TideFree", PlanetsAndMoons.EARTH.NaifId, 70, 3.986004415e14, 6378136.3),
        // LP165P, Konopliv et al. 2001, oriented by MOON_PA
        new GeopotentialField("LP165P_to20", PlanetsAndMoons.MOON.NaifId, 20, 4.902801056e12, 1738000.0),
        // GMM-2B, Lemoine et al. 2001, oriented by IAU_MARS
        new GeopotentialField("GMM2B_to20", PlanetsAndMoons.MARS.NaifId, 20, 4.28283719012840e13, 3397000.0)
    };

    /// <summary>
    /// The field of <paramref name="model"/> for the central body <paramref name="bodyNaifId"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The model is not a field of spec.md section 12.1, or is the field of
    /// another body.</exception>
    public static GeopotentialField Resolve(string model, int bodyNaifId)
    {
        var field = Fields.SingleOrDefault(f => f.Model == model) ?? throw new InvalidOperationException(
            $"The geopotential model '{model}' is not a field of spec.md section 12.1 ({string.Join(", ", Fields.Select(f => f.Model))}): its constants are unknown.");
        return field.BodyNaifId == bodyNaifId
            ? field
            : throw new InvalidOperationException(
                $"The geopotential model {model} is the field of the body {field.BodyNaifId}, not of the central body {bodyNaifId} (spec.md section 12.1).");
    }
}
