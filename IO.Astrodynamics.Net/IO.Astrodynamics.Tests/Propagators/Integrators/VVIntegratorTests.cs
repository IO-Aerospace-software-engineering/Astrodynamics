using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Integrators;

public class VVIntegratorTests
{
    private readonly ITestOutputHelper _output;

    public VVIntegratorTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>
    /// Velocity-Verlet is second order: halving the step divides the global error by about 4.
    /// Two-body propagation of the LEO state of conformance case propagator_24h_leo_grav10_001 over 24 h,
    /// against the analytic Keplerian solution, at 0.5, 1 and 2 s. The errors are printed: they are the
    /// truncation errors published in docs/reference/integrators.md.
    /// </summary>
    [Fact]
    public void GlobalErrorIsSecondOrderInTheStep()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var epoch = TimeSystem.Time.J2000TDB;
        var initialState = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            earth, epoch, Frames.Frame.ICRF);

        const double duration = 86400.0; // divisible by every step below
        var expected = initialState.ToStateVector(epoch.AddSeconds(duration));

        double[] steps = { 0.5, 1.0, 2.0 };
        var errors = new double[steps.Length];
        for (int k = 0; k < steps.Length; k++)
        {
            var integrator = new VVIntegrator(TimeSpan.FromSeconds(steps[k]));
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.Initialize(initialState);

            var result = integrator.IntegrateSegment(initialState.Position, initialState.Velocity, epoch, duration);
            var (finalPosition, _) = result.Segment.InterpolateAt(duration);
            errors[k] = (finalPosition - expected.Position).Magnitude();
            _output.WriteLine($"Velocity-Verlet, step {steps[k]} s: two-body position error after 24 h = {errors[k]:F1} m");
        }

        double ratioOneToHalf = errors[1] / errors[0];
        double ratioTwoToOne = errors[2] / errors[1];
        _output.WriteLine($"Error ratios per doubling of the step: {ratioOneToHalf:F2}, {ratioTwoToOne:F2}");

        Assert.InRange(ratioOneToHalf, 3.5, 4.5);
        Assert.InRange(ratioTwoToOne, 3.5, 4.5);
    }
}
