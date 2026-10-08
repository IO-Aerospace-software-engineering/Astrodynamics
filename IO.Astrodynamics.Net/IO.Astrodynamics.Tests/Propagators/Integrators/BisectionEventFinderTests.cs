// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators;

public class BisectionEventFinderTests
{
    private static readonly TimeSystem.Time Epoch = TimeSystem.Time.J2000TDB;

    // Uniform motion along x at 2 m/s from x = -10 m over a step of 10 s starting 100 s after the segment base epoch:
    // the cubic Hermite interpolation of a straight line is exact, so x crosses zero at 105 s
    private static readonly AcceptedStep Step = new(100.0, 10.0, new Vector3(-10.0, 0.0, 0.0), new Vector3(2.0, 0.0, 0.0),
        new Vector3(10.0, 0.0, 0.0), new Vector3(2.0, 0.0, 0.0), Vector3.Zero, Vector3.Zero);

    [Fact]
    public void FindRoot_OnTheHermiteInterpolationOfAStep_FindsTheCrossing()
    {
        double t = BisectionEventFinder.FindRoot(Step, (p, _, _) => p.X - 1.0, -11.0, 9.0, Epoch);
        Assert.Equal(105.5, t, 1e-9);
    }

    [Fact]
    public void FindRoot_PassesTheEpochOfEachEvaluation()
    {
        double t = BisectionEventFinder.FindRoot(Step, (_, _, epoch) => (epoch - Epoch).TotalSeconds - 102.5, -2.5,
            7.5, Epoch, 1e-6);
        Assert.Equal(102.5, t, 1e-6);
    }

    [Fact]
    public void FindRoot_AtAnExactZero_ReturnsIt()
    {
        // The first midpoint, 105 s, is the root
        double t = BisectionEventFinder.FindRoot(Step, (p, _, _) => p.X, -10.0, 10.0, Epoch);
        Assert.Equal(105.0, t);
    }

    [Fact]
    public void FindRoot_WithoutASignChange_Throws()
    {
        Assert.Throws<ArgumentException>(() => BisectionEventFinder.FindRoot(Step, (p, _, _) => p.X, 1.0, 2.0, Epoch));
    }
}
