using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// An encounter given by the states of its two objects at TCA (spec.md sections 16.2 and 16.3) as the CDM that holds
/// them, which <see cref="Cdm.ToEncounter"/> turns into the encounter at that TCA (decision 110 of #668): the states in
/// EME2000 (ICRF) in m and m/s, each covariance in its object's RTN.
/// </summary>
internal static class EncounterMessage
{
    /// <summary>
    /// The CDM of the encounter of <paramref name="objects"/> (object 1, then object 2) at <paramref name="tca"/>.
    /// </summary>
    /// <param name="tca">The TCA.</param>
    /// <param name="objects">The states at TCA in ICRF (km, km/s) and the covariances in each object's RTN (km).</param>
    /// <param name="absentAsZero">Gives an absent covariance as a zero matrix, so that the framework projects the given
    /// one alone, without Frisbee's maximum.</param>
    public static Cdm Of(Time tca, IReadOnlyList<EncounterObject> objects, bool absentAsZero = false)
    {
        var tcaUtc = tca.ToUTC().DateTime;
        var segments = new List<CdmSegment>();
        for (int k = 0; k < 2; k++)
        {
            var o = objects[k];
            var covarianceRtn = Covariance(o.CovarianceRtn, absentAsZero);
            segments.Add(new CdmSegment
            {
                Metadata = new CdmParticipantMetadata
                {
                    Object = k == 0 ? CdmObjectRole.Object1 : CdmObjectRole.Object2,
                    ObjectName = $"Encounter{k + 1}",
                    ReferenceFrame = CdmReferenceFrame.Eme2000
                },
                Data = new CdmData
                {
                    StateVector = new CdmStateVector
                    {
                        PositionMeters = new Vector3(UnitConversion.KmToM(o.PositionKm[0]), UnitConversion.KmToM(o.PositionKm[1]), UnitConversion.KmToM(o.PositionKm[2])),
                        VelocityMetersPerSecond = new Vector3(UnitConversion.KmSToMS(o.VelocityKmS[0]), UnitConversion.KmSToMS(o.VelocityKmS[1]), UnitConversion.KmSToMS(o.VelocityKmS[2]))
                    },
                    CovarianceMatrix = covarianceRtn is { } rtn ? CdmCovarianceMatrix.FromRtnCovariance(rtn) : null
                }
            });
        }

        return new Cdm(new CdmHeader { CreationDateUtc = tcaUtc, Originator = "IO.Astrodynamics.ConformanceRunner", MessageId = "ENCOUNTER" },
            new CdmRelativeMetadataData { TcaUtc = tcaUtc }, segments);
    }

    // The 6x6 RTN covariance of an object in m², m²/s and m²/s² (a 3x3 position covariance in its position block), or null
    private static Matrix? Covariance(double[] lower, bool absentAsZero)
    {
        if (lower == null)
        {
            return absentAsZero ? new Matrix(6, 6) : null;
        }

        var given = LowerTriangle.ToMatrix(lower, 1e6);
        if (given.Rows == 6)
        {
            return given;
        }

        return Matrix.Create(6, 6, (i, j) => i < given.Rows && j < given.Columns ? given[i, j] : 0.0);
    }
}
