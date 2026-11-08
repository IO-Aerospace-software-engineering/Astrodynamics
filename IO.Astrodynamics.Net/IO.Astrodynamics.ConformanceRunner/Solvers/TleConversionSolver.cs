using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.OrbitalParameters.TLE;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>tle_conversion</c> category (spec.md section 15.2): a TLE read into its fields under the OMM keywords
/// (<c>tle_to_omm</c>), or OMM mean elements written as a TLE (<c>omm_to_tle</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>tle_to_omm</c>: <see cref="TLE.ToOmm"/> of the framework's <see cref="TLE"/>, its mean elements, TLE parameters
/// and epoch, the UTC calendar date of the TLE epoch (#603).
/// </para>
/// <para>
/// <c>omm_to_tle</c>: an <see cref="Omm"/> of the item (Earth, TEME, SGP4, its <c>TIME_SYSTEM</c>), then
/// <see cref="Omm.ToTle"/>, which reads the epoch in that time system and writes its UTC (#485, #479); the lines are
/// compared by the column table (<see cref="TleLineComparer"/>).
/// </para>
/// </remarks>
public class TleConversionSolver : ICategorySolver
{
    public string Category => "tle_conversion";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseTleConversionInputs(caseInput.Inputs);
        var outputs = new Dictionary<string, object>();
        if (inputs.Operation == TleConversionInputs.TleToOmm)
        {
            outputs["items"] = inputs.Tles.Count;
            for (int k = 0; k < inputs.Tles.Count; k++)
            {
                var input = inputs.Tles[k];
                AddFields(outputs, k, new TLE(string.IsNullOrEmpty(input.Name) ? "TLE" : input.Name, input.Line1, input.Line2).ToOmm());
            }
        }
        else
        {
            outputs["items"] = inputs.Omms.Count;
            for (int k = 0; k < inputs.Omms.Count; k++)
            {
                var tle = ToOmm(inputs.Omms[k]).ToTle();
                outputs[OutputComparer.IndexedOutput("items", k, "tle")] = new TleText(tle.Line1, tle.Line2);
            }
        }

        return outputs;
    }

    /// <summary>The OMM of an item: the Earth, TEME and SGP4, the epoch in the item's <c>TIME_SYSTEM</c>.</summary>
    internal static Omm ToOmm(OmmInput item)
    {
        var metadata = new OmmMetadata(item.ObjectName, item.ObjectId, "EARTH", "TEME", item.TimeSystem, "SGP4");
        var elements = MeanElements.CreateWithMeanMotion(item.Epoch, item.MeanMotion, item.Eccentricity, item.Inclination,
            item.RaOfAscNode, item.ArgOfPericenter, item.MeanAnomaly);
        var parameters = TleParameters.CreateWithBStarAndDDot(item.BStar, item.MeanMotionDot, item.MeanMotionDdot,
            item.EphemerisType, item.ClassificationType, item.NoradCatId, item.ElementSetNo, item.RevAtEpoch);
        return new Omm(CcsdsHeader.CreateDefault(), metadata, OmmData.CreateForTle(elements, parameters));
    }

    private static void AddFields(Dictionary<string, object> outputs, int k, Omm omm)
    {
        var elements = omm.Data.MeanElements;
        var parameters = omm.Data.TleParameters;
        void Add(string field, object value) => outputs[OutputComparer.IndexedOutput("items", k, field)] = value;

        Add("NORAD_CAT_ID", parameters.NoradCatalogId);
        Add("OBJECT_ID", omm.ObjectId);
        Add("CLASSIFICATION_TYPE", parameters.ClassificationType);
        Add("EPOCH", elements.Epoch);
        Add("MEAN_MOTION_DOT", parameters.MeanMotionDot);
        Add("MEAN_MOTION_DDOT", parameters.MeanMotionDDot);
        Add("BSTAR", parameters.BStar);
        Add("EPHEMERIS_TYPE", parameters.EphemerisType);
        Add("ELEMENT_SET_NO", parameters.ElementSetNumber);
        Add("INCLINATION", elements.Inclination);
        Add("RA_OF_ASC_NODE", elements.RightAscensionOfAscendingNode);
        Add("ECCENTRICITY", elements.Eccentricity);
        Add("ARG_OF_PERICENTER", elements.ArgumentOfPericenter);
        Add("MEAN_ANOMALY", elements.MeanAnomaly);
        Add("MEAN_MOTION", elements.MeanMotion);
        Add("REV_AT_EPOCH", parameters.RevolutionNumberAtEpoch);
    }
}
