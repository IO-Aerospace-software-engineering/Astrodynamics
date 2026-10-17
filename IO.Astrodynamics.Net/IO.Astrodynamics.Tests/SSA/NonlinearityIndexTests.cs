// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
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

        // Assert: ν of the Keplerian STM (Lagrange f and g in universal variables, F2) at the same twelve points
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
        var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow);
        double nu = encounter.EncounterState.ProtectedCovariance.NonlinearityIndex!.Value;
        var flagged = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow,
            new ConjunctionAnalysisOptions { NonlinearityIndexThreshold = 0.5 * nu });
        var off = ConjunctionAssessment.Analyze(profile, secondaryCraft, ConjunctionCovarianceCase.AnalysisWindow,
            new ConjunctionAnalysisOptions { ComputeNonlinearityIndex = false });

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

    // ν with Φ of the Keplerian STM, at the twelve points ±√λ_k e_k of P0
    private double KeplerianIndex(StateVector start, double dt)
    {
        double mu = _case.Earth.GM;
        var (_, nominal) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, dt);
        var p0 = start.Covariance.Value;
        var evd = Matrix<double>.Build.Dense(6, 6, (i, j) => 0.5 * (p0.Get(i, j) + p0.Get(j, i)))
            .Evd(Symmetricity.Symmetric);
        double norm = 0.0;
        foreach (var value in nominal)
        {
            norm += value * value;
        }

        norm = System.Math.Sqrt(norm);
        double largest = 0.0;
        for (int k = 0; k < 6; k++)
        {
            double s = System.Math.Sqrt(evd.EigenValues[k].Real);
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                var dr = new Vector3(evd.EigenVectors[0, k], evd.EigenVectors[1, k], evd.EigenVectors[2, k]) * (sign * s);
                var dv = new Vector3(evd.EigenVectors[3, k], evd.EigenVectors[4, k], evd.EigenVectors[5, k]) * (sign * s);
                var (_, phi) = KeplerianStm.Propagate(mu, start.Position + dr, start.Velocity + dv, dt);
                double difference = 0.0;
                for (int i = 0; i < 36; i++)
                {
                    difference += (phi[i] - nominal[i]) * (phi[i] - nominal[i]);
                }

                largest = System.Math.Max(largest, System.Math.Sqrt(difference) / norm);
            }
        }

        return largest;
    }
}
