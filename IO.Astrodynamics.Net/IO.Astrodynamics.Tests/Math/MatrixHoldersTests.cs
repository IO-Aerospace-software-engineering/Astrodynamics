// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

/// <summary>
/// The public holders of a <see cref="Matrix"/> copy it in and out (#722, decided by S. Guillet, 2026-11-17): the public
/// <c>Set</c> of the versions before 11.0.0 wrote the array a matrix wraps, which a copy of the struct shares, so a term
/// set on the matrix given to a holder, or on the one it returned, reached the holder. <see cref="Matrix"/> is immutable
/// since 11.0.0 (#668, R26); the copies stay, and these tests write through the internal setter. The holders: <see cref="StateVector.Covariance"/>, <see cref="EncounterState.CombinedCovarianceRtn"/>,
/// <see cref="CollisionRisk.ProjectedCovariance"/>, <see cref="EpochStatistics.SampleCovariance"/> and
/// <see cref="Iau2006Model.FrameBias"/> (<c>CdmCovarianceMatrix</c> since #719). The expected terms are those given.
/// </summary>
public class MatrixHoldersTests
{
    private const double Term = 4.0;
    private const double Changed = 99.0;

    private static readonly TimeSystem.Time Epoch = new(new DateTime(2025, 3, 28), TimeScale.UTC);

    public MatrixHoldersTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static Matrix Diagonal(int size)
    {
        var matrixTerms = new double[size, size];
        for (int i = 0; i < size; i++) matrixTerms[i, i] = Term;
        var matrix = new Matrix(matrixTerms);
        return matrix;
    }

    /// <summary>
    /// Sets a term of the matrix given and of the matrix returned, then reads the holder again.
    /// </summary>
    private static void AssertHolderIsolated(Matrix given, Func<Matrix> read)
    {
        given.SetTerm(0, 0, Changed);
        Assert.Equal(Term, read().Get(0, 0));

        var returned = read();
        returned.SetTerm(0, 0, Changed);
        Assert.Equal(Term, read().Get(0, 0));
    }

    [Fact]
    public void StateVectorCovariance()
    {
        var covariance = Diagonal(6);
        var state = new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0), TestHelpers.EarthAtJ2000, Epoch,
            Frames.Frame.ICRF, covariance);

        AssertHolderIsolated(covariance, () => state.Covariance!.Value);
    }

    /// <summary>
    /// One matrix reused to build the covariances of several states, the loop a caller wrote with the public <c>Set</c>
    /// before 11.0.0 (#722: all held the last one): each state keeps its own. <see cref="Matrix"/> is immutable since
    /// 11.0.0 (#668, R26); the internal setter checks that the holder still copies.
    /// </summary>
    [Fact]
    public void OneMatrixReusedForSeveralStates_EachKeepsItsOwn()
    {
        var builder = new Matrix(6, 6);
        var states = new StateVector[3];
        for (int k = 0; k < states.Length; k++)
        {
            for (int i = 0; i < 6; i++) builder.SetTerm(i, i, k + 1.0);
            states[k] = new StateVector(new Vector3(6800e3 + k, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0), TestHelpers.EarthAtJ2000, Epoch,
                Frames.Frame.ICRF, builder);
        }

        for (int k = 0; k < states.Length; k++)
        {
            Assert.Equal(k + 1.0, states[k].Covariance!.Value.Get(5, 5));
        }
    }

    [Fact]
    public void EncounterStateCombinedCovariance()
    {
        var covariance = Diagonal(6);
        var relative = new RelativeState(Epoch, Vector3.VectorX, Vector3.VectorY, Vector3.VectorX, Vector3.VectorY);
        var state = new EncounterState(Epoch, relative, 1.0, covariance, EncounterQualityFlags.None);

        AssertHolderIsolated(covariance, () => state.CombinedCovarianceRtn);
    }

    [Fact]
    public void CollisionRiskProjectedCovariance()
    {
        var covariance = Diagonal(2);
        var risk = new CollisionRisk(10.0, 1e-5, covariance, 1.0, 2.0, 3.0);

        AssertHolderIsolated(covariance, () => risk.ProjectedCovariance);
    }

    [Fact]
    public void EpochStatisticsSampleCovariance()
    {
        var covariance = Diagonal(6);
        var components = new StateComponents(Vector3.Zero, Vector3.Zero);
        var percentiles = new RssPercentiles(1.0, 2.0, 3.0);
        var statistics = new EpochStatistics(Epoch, Vector3.Zero, Vector3.Zero, covariance, components, components, components,
            percentiles, percentiles);

        AssertHolderIsolated(covariance, () => statistics.SampleCovariance);
    }

    /// <summary>
    /// The frame bias is a static of the process: a term set on the matrix returned must not reach the next caller. The
    /// term is put back in <c>finally</c>, so that a failure of the test does not corrupt the others of the process.
    /// </summary>
    [Fact]
    public void FrameBias()
    {
        var returned = Iau2006Model.FrameBias();
        double original = returned.Get(0, 0);
        try
        {
            returned.SetTerm(0, 0, Changed);
            Assert.Equal(original, Iau2006Model.FrameBias().Get(0, 0));
        }
        finally
        {
            returned.SetTerm(0, 0, original);
        }
    }

    /// <summary>
    /// A matrix without terms (the default one, whose array is null) is held and returned as it is.
    /// </summary>
    [Fact]
    public void ADefaultMatrix_IsHeldAsItIs()
    {
        var risk = new CollisionRisk(10.0, null, default, 1.0, 2.0, 3.0);

        Assert.Equal(0, risk.ProjectedCovariance.Rows);
    }
}
