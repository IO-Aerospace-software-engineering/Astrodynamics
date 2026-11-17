using System;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
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
        TestKernels.EnsureSolarSystem();
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

    /// <summary>
    /// Velocity-Verlet is second order with drag too (#439): the acceleration at the end of a step is evaluated with the
    /// predicted end velocity v + h a, not the half-step velocity, which made the drag at t + h wrong by O(h) and the
    /// integrator first order (267 m, 27.2 m and 2.72 m at 1, 0.1 and 0.01 s on the dense pass below). The case of #439: a
    /// point-mass Earth with the U.S. Standard Atmosphere 1976, perigee 20 km and apogee 400 km above the equatorial radius,
    /// 45° in ICRF, 10 min from 300 s before the perigee, 1 000 kg, 1e-4 m², Cd 2.2; and a thin LEO, perigee 300 km and
    /// apogee 400 km, 0.01 m². The reference is RK7(8) at 1e-13 of the same forces: the error must fall by about 100 per
    /// decade of the step.
    /// </summary>
    [Theory]
    [InlineData(20000.0, 1.0e-4)]
    [InlineData(300000.0, 1.0e-2)]
    public void GlobalErrorIsSecondOrderWithDrag(double perigeeHeight, double area)
    {
        var epoch = new TimeSystem.Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch, atmosphericModel: new EarthStandardAtmosphere());
        double rp = earth.EquatorialRadius + perigeeHeight, ra = earth.EquatorialRadius + 400000.0;
        double a = (rp + ra) / 2.0, e = (ra - rp) / (ra + rp);
        double n = System.Math.Sqrt(earth.GM / (a * a * a));
        var orbit = new KeplerianElements(a, e, System.Math.PI / 4.0, 0.0, 0.0, -300.0 * n, earth, epoch, Frames.Frame.ICRF);
        var initialState = orbit.ToStateVector();
        var spacecraft = new Spacecraft(-7439, "VV439", 1000.0, 1000.0, new Clock("VV439_CLK", 65536), initialState, sectionalArea: area, dragCoeff: 2.2);
        const double duration = 600.0;

        Vector3 FinalPosition(Integrator integrator)
        {
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.AddForce(new AtmosphericDrag(spacecraft, earth));
            integrator.Initialize(initialState);
            var result = integrator.IntegrateSegment(initialState.Position, initialState.Velocity, epoch, duration);
            return result.Segment.InterpolateAt(duration).position;
        }

        var reference = FinalPosition(new RK78Integrator(1e-13, 1e-13));
        double[] steps = { 1.0, 0.1, 0.01 };
        var errors = new double[steps.Length];
        for (int k = 0; k < steps.Length; k++)
        {
            errors[k] = (FinalPosition(new VVIntegrator(TimeSpan.FromSeconds(steps[k]))) - reference).Magnitude();
        }

        double first = errors[0] / errors[1], second = errors[1] / errors[2];
        _output.WriteLine($"MEASURE #439 VV with drag, perigee {perigeeHeight / 1000.0} km, A {area} m²: errors {errors[0]:E3} / {errors[1]:E3} / {errors[2]:E3} m at 1 / 0.1 / 0.01 s, ratios per decade {first:F1}, {second:F1}");
        Assert.InRange(first, 80.0, 125.0);
        Assert.InRange(second, 80.0, 125.0);
    }
}
