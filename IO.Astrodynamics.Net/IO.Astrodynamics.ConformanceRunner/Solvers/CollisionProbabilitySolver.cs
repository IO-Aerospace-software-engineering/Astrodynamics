using System;
using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SSA;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>collision_probability</c> category (spec.md section 16.2): the 2D Pc of an encounter given at TCA, or
/// Frisbee's maximum Pc when one covariance is absent, the sigmas of the encounter plane and whether the covariance was
/// raised to CARA's clip, by the public conjunction analysis on straight-line trajectories
/// (<see cref="StraightLineEncounter"/>).
/// </summary>
/// <remarks>
/// <para>
/// The Pc is <see cref="CollisionRisk.ProbabilityOfCollision"/>: <c>collision_probability</c> with two covariances,
/// <c>maximum_collision_probability</c> when the framework took Frisbee's maximum
/// (<see cref="EncounterQualityFlags.SingleCovarianceMaximumPcUsed"/>); <c>remediated</c> is
/// <see cref="EncounterQualityFlags.CovarianceRemediated"/>.
/// </para>
/// <para>
/// The sigmas are the square roots of the eigenvalues of the framework's encounter-plane covariance
/// (<see cref="CollisionRisk.ProjectedCovariance"/>) of the given covariances alone: an absent covariance is then given
/// as zero, since the projection the framework reports with one covariance absent holds Frisbee's addition. The
/// framework reports that covariance after the clip: an eigenvalue it raised to the clip, (1e-4 HBR)², is not the
/// eigenvalue before the clip that spec.md section 16.2 compares, and its sigma is an output the solver cannot give.
/// </para>
/// </remarks>
public class CollisionProbabilitySolver : ICategorySolver
{
    // The NAIF ids of the objects of the analysis and of the analysis of the sigmas
    private const int NaifId = -1601;
    private const int SigmaNaifId = -1611;

    // CARA's clip of an eigenvalue of the encounter-plane covariance: (1e-4 HBR)² (spec.md section 16.2)
    private const double ClipFactor = 1e-4;

    public string Category => "collision_probability";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseCollisionProbabilityInputs(caseInput.Inputs);
        var tca = EpochParser.Parse(inputs.Tca);
        double hardBodyRadius = UnitConversion.KmToM(inputs.HardBodyRadiusKm);
        var encounter = StraightLineEncounter.Analyze(tca, inputs.Objects, 1e6, hardBodyRadius, NaifId);
        var flags = encounter.EncounterState.QualityFlags;
        var outputs = new Dictionary<string, object>
        {
            [flags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed) ? "maximum_collision_probability" : "collision_probability"] =
                SolverOutput.Of(() => encounter.CollisionRisk.ProbabilityOfCollision ??
                                      throw new InvalidOperationException($"The framework gives no Pc for this encounter (flags {flags}).")),
            ["remediated"] = flags.HasFlag(EncounterQualityFlags.CovarianceRemediated)
        };

        bool oneAbsent = inputs.Objects.Exists(o => o.CovarianceRtn == null);
        var given = oneAbsent
            ? StraightLineEncounter.Analyze(tca, inputs.Objects, 1e6, hardBodyRadius, SigmaNaifId, absentAsZero: true)
            : encounter;
        var (major, minor) = Eigenvalues(given.CollisionRisk.ProjectedCovariance);
        double clip = ClipFactor * hardBodyRadius * (ClipFactor * hardBodyRadius);
        outputs["encounter_plane_sigma_major_km"] = Sigma(major, clip);
        outputs["encounter_plane_sigma_minor_km"] = Sigma(minor, clip);
        return outputs;
    }

    // The sigma of an eigenvalue in km, or the reason it is not the eigenvalue before the clip
    private static object Sigma(double eigenvalue, double clip)
    {
        return SolverOutput.Of(() => eigenvalue > clip * (1.0 + 1e-9)
            ? UnitConversion.MToKm(System.Math.Sqrt(eigenvalue))
            : throw new InvalidOperationException(
                $"The framework raised this eigenvalue of the encounter-plane covariance to the clip ({eigenvalue:R} m², clip {clip:R} m²) and reports it after the clip only."));
    }

    /// <summary>The eigenvalues of a symmetric 2x2 matrix, the larger first.</summary>
    internal static (double Major, double Minor) Eigenvalues(Matrix matrix)
    {
        double a = matrix.Get(0, 0);
        double b = 0.5 * (matrix.Get(0, 1) + matrix.Get(1, 0));
        double c = matrix.Get(1, 1);
        double mean = 0.5 * (a + c);
        double radius = System.Math.Sqrt(0.25 * (a - c) * (a - c) + b * b);
        double major = mean + radius;

        // The minor one from the determinant, without the cancellation of mean − radius
        double minor = major > 0.0 ? (a * c - b * b) / major : mean - radius;
        return (major, minor);
    }
}
