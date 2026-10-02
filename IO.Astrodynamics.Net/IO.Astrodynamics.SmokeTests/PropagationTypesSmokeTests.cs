using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SSA;
using Xunit;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// Smoke tests that verify the IO.Astrodynamics NuGet package exposes the whole public
/// surface a consumer compiles against: adaptive propagation, the fluent builder, the
/// CIO-based frames, batch and Monte Carlo propagation, conjunction assessment and CDM.
/// </summary>
public class PropagationTypesSmokeTests
{
    [Fact]
    public void RK78Integrator_TypeIsAccessible()
    {
        var type = typeof(RK78Integrator);
        Assert.NotNull(type);
        Assert.Equal("RK78Integrator", type.Name);
    }

    [Fact]
    public void RK78Integrator_CanBeInstantiated()
    {
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-9,
            relativeTolerance: 1e-12,
            initialStepSize: 60.0);
        Assert.NotNull(integrator);
    }

    [Fact]
    public void BisectionEventFinder_TypeIsAccessible()
    {
        var type = typeof(BisectionEventFinder);
        Assert.NotNull(type);
        Assert.True(type.IsAbstract && type.IsSealed, "BisectionEventFinder should be a static class");
    }

    [Fact]
    public void CioBasedFrames_AreExposedOnFrame()
    {
        Assert.IsType<GcrfFrame>(Frame.GCRF);
        Assert.IsType<CirsFrame>(Frame.CIRS);
        Assert.IsType<TirsFrame>(Frame.TIRS);

        // Singletons, so a cached orientation is shared across call sites.
        Assert.Same(Frame.GCRF, Frame.GCRF);
        Assert.Same(Frame.TIRS, Frame.TIRS);
    }

    [Fact]
    public void PropagationAndAnalysisTypes_AreAccessible()
    {
        Assert.NotNull(typeof(CentralBodyPropagatorBuilder));
        Assert.NotNull(typeof(BatchPropagator));
        Assert.NotNull(typeof(MonteCarloPropagator));
        Assert.NotNull(typeof(ConjunctionAssessment));
        Assert.NotNull(typeof(Cdm));
        Assert.NotNull(typeof(CelestialBody));
    }
}
