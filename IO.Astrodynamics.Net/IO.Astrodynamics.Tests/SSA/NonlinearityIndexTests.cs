// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Variational;
using MathNet.Numerics.LinearAlgebra;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The non-linearity index of a propagated covariance (Junkins and Singla, 2004; step 10, the guard).
/// </summary>
public class NonlinearityIndexTests : IClassFixture<ConjunctionCovarianceCase>
{
    private readonly ConjunctionCovarianceCase _case;
    private readonly ITestOutputHelper _output;

    public NonlinearityIndexTests(ConjunctionCovarianceCase @case, ITestOutputHelper output)
    {
        _case = @case;
        _output = output;
    }

    [Theory]
    [InlineData(6.0, 1.0)]
    [InlineData(24.0, 1.0)]
    [InlineData(24.0, 10.0)]
    public void Index_OfTheTwoBodyPropagation_IsThatOfTheKeplerianStm(double hours, double scale)
    {
        // Arrange: the protected object of the two-body case, P0 scaled by scale²
        var start = Scaled(_case.ProtectedStart, scale);
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6501 - (int)hours - (int)scale * 100, start);
        var solution = _case.Propagate(spacecraft, new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        var epoch = start.Epoch.AddHours(hours);

        // Act
        var watch = Stopwatch.StartNew();
        var index = NonlinearityIndex.Create(solution, spacecraft, epoch);
        double nu = index.At(solution, epoch);
        watch.Stop();

        // Assert: ν of the Keplerian STM (Lagrange f and g in universal variables, F2), on the same definition
        double expected = KeplerianIndex(start, hours * 3600.0);
        double error = System.Math.Abs(nu - expected) / expected;
        _output.WriteLine($"{hours} h, P0 × {scale}²: ν {nu:E4} against {expected:E4} (Keplerian), {error:E2}; {watch.ElapsedMilliseconds} ms");
        Assert.True(error < 1e-8, $"{error:E2}");
    }

    [Fact]
    public void Analyze_ReportsTheIndexOfEachPropagatedCovariance_AndRaisesTheFlagAboveTheThreshold()
    {
        // Arrange
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6511, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6512, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act
        var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow,
            new ConjunctionAnalysisOptions { ComputeNonlinearityIndex = true });
        double nu = encounter.EncounterState.ProtectedCovariance.NonlinearityIndex!.Value;
        var flagged = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow,
            new ConjunctionAnalysisOptions { ComputeNonlinearityIndex = true, NonlinearityIndexThreshold = 0.5 * nu });

        // Off by default
        var off = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow);

        // Assert
        _output.WriteLine($"ν protected {nu:E3}, secondary {encounter.EncounterState.SecondaryCovariance.NonlinearityIndex:E3}");
        Assert.True(nu > 0.0);
        Assert.NotNull(encounter.EncounterState.SecondaryCovariance.NonlinearityIndex);
        Assert.True(flagged.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.NonlinearCovariancePropagation));
        Assert.Equal(nu, flagged.EncounterState.ProtectedCovariance.NonlinearityIndex);
        Assert.Null(off.EncounterState.ProtectedCovariance.NonlinearityIndex);
        Assert.False(off.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.NonlinearCovariancePropagation));
        Assert.Equal(off.CollisionRisk.ProbabilityOfCollision, encounter.CollisionRisk.ProbabilityOfCollision);
    }

    // P0 × scale²
    private static StateVector Scaled(StateVector state, double scale)
    {
        var covariance = state.Covariance.Value;
        var scaled = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                scaled.Set(i, j, covariance.Get(i, j) * scale * scale);
            }
        }

        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, scaled);
    }

    [Theory]
    [InlineData(6.0, 1.0)]
    [InlineData(24.0, 10.0)]
    public void Index_OfAnIsotropicCovariance_DoesNotDependOnTheBasis(double hours, double scale)
    {
        // Arrange: P0 with the same σ on the three RTN axes, of repeated eigenvalues (H3 review of step 10, N1)
        var orbit = ConjunctionCovarianceCase.Pair(_case.Earth, ConjunctionCovarianceCase.Start, ConjunctionCovarianceCase.Tca).Protected;
        var start = ConjunctionCovarianceCase.WithRtnCovariance(orbit,
            new[] { 100.0, 100.0, 100.0, 0.1, 0.1, 0.1 }.Select(s => s * scale).ToArray());
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6521 - (int)hours - (int)scale * 100, start);
        var solution = _case.Propagate(spacecraft, new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        var epoch = start.Epoch.AddHours(hours);
        double nu = NonlinearityIndex.Create(solution, spacecraft, epoch).At(solution, epoch);

        // Act: the Keplerian index along the columns of P0^(1/2) Q, for random orthogonal Q
        var random = new Random(20261018);
        double worst = 0.0;
        for (int trial = 0; trial < 8; trial++)
        {
            var q = Matrix<double>.Build.Random(6, 6, new MathNet.Numerics.Distributions.Normal(0.0, 1.0, random)).QR().Q;
            double expected = KeplerianIndex(start, hours * 3600.0, q);
            worst = System.Math.Max(worst, System.Math.Abs(nu - expected) / expected);
        }

        // Assert: the same ν whatever the basis, to the third-order terms of the central differences, of relative order ν²
        _output.WriteLine($"{hours} h, P0 × {scale}²: ν {nu:E4}, largest departure over 8 bases {worst:E2}");
        Assert.True(worst < nu * nu, $"{worst:E2} against ν² {nu * nu:E2}");
    }

    // ν of the Keplerian STM (Lagrange f and g in universal variables, F2): σ_max of the central differences of Φ along
    // the columns of P0^(1/2) Q, over ‖Φ‖_F; Q the identity by default
    private double KeplerianIndex(StateVector start, double dt, Matrix<double> q = null)
    {
        double mu = _case.Earth.GM;
        var (_, nominal) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, dt);
        var p0 = start.Covariance.Value;
        var evd = Matrix<double>.Build.Dense(6, 6, (i, j) => 0.5 * (p0.Get(i, j) + p0.Get(j, i)))
            .Evd(Symmetricity.Symmetric);
        var root = evd.EigenVectors * Matrix<double>.Build.DiagonalOfDiagonalVector(
            evd.EigenValues.Map(e => System.Math.Sqrt(System.Math.Max(e.Real, 0.0)))) * evd.EigenVectors.Transpose();
        var columns = q == null ? root : root * q;
        double norm = System.Math.Sqrt(nominal.Sum(v => v * v));
        var m = Matrix<double>.Build.Dense(36, 6);
        for (int k = 0; k < 6; k++)
        {
            var dr = new Vector3(columns[0, k], columns[1, k], columns[2, k]);
            var dv = new Vector3(columns[3, k], columns[4, k], columns[5, k]);
            var (_, plus) = KeplerianStm.Propagate(mu, start.Position + dr, start.Velocity + dv, dt);
            var (_, minus) = KeplerianStm.Propagate(mu, start.Position - dr, start.Velocity - dv, dt);
            for (int i = 0; i < 36; i++)
            {
                m[i, k] = 0.5 * (plus[i] - minus[i]);
            }
        }

        return m.Svd(false).S[0] / norm;
    }
}
