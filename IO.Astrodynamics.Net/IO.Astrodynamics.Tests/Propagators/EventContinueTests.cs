// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// An event whose handler returns <see cref="Astrodynamics.Propagator.Events.EventAction.Continue"/>, here a maneuver
/// whose precondition fails, is skipped and the propagation goes on to the next occurrence (#355). It used to integrate
/// the same segment again without end: in 20 s, 431 MB with RK7(8), and an <see cref="OutOfMemoryException"/> in about
/// 1 s with Velocity-Verlet.
/// </summary>
/// <remarks>
/// The orbit is Keplerian (point-mass Earth), a = 11,480 km and e = 0.5, starting 0.15 rad of mean anomaly before
/// apogee, with the apogee 90° from the ascending node: the precondition of <see cref="CombinedManeuver"/> fails at every
/// apogee. The apogees are at t0 + (π − M0 + 2πk) / n, the reference of the event epochs.
/// </remarks>
public class EventContinueTests
{
    private static readonly TimeSystem.Time Start = new(2021, 3, 20, 12, 0, 0);
    private const double SemiMajorAxis = 11480000.0;
    private const double MeanAnomaly = System.Math.PI - 0.15;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public EventContinueTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreconditionThatNeverHolds_SkipsEveryApogee_AndTheManeuverIsNotExecuted(bool rk78)
    {
        // Arrange
        var (spacecraft, engine) = CreateSpacecraft(-1355);
        var maneuver = new CombinedManeuver(_earth, Start, TimeSpan.Zero, 9000000.0, 0.1, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        var window = new TimeSystem.Window(Start, Start.AddHours(12.0));

        // Act: bounded, so that a regression fails instead of hanging
        var propagation = Task.Run(() => spacecraft.Propagate(window, new CelestialItem[] { _earth },
            rk78 ? new RK78Integrator(1e-11, 1e-11) : new VVIntegrator(TimeSpan.FromSeconds(10.0)), false, false,
            TimeSpan.FromSeconds(60.0)));
        Assert.True(propagation.Wait(TimeSpan.FromMinutes(2)), "The propagation did not end.");
        var solution = propagation.Result;

        // Assert: one segment per skipped apogee, each starting at that apogee
        var apogees = ApogeesWithin(window);
        Assert.Equal(4, apogees.Length);
        Assert.Equal(apogees.Length + 1, solution.Segments.Count);
        double tolerance = rk78 ? Rk78EventTolerance : VelocityVerletStep;
        for (int k = 0; k < apogees.Length; k++)
        {
            double error = (solution.Segments[k + 1].BaseEpoch - apogees[k]).TotalSeconds;
            _output.WriteLine($"rk78 = {rk78}, apogee {k}: {error:E3} s from the Keplerian apogee");
            Assert.InRange(error, rk78 ? -tolerance : 0.0, tolerance);
        }

        Assert.Null(maneuver.ThrustWindow);
        Assert.Same(maneuver, spacecraft.StandbyManeuver);
        Assert.Equal(window.EndDate, solution.StateVectors[^1].Epoch);
    }

    [Fact]
    public void PreconditionThatHoldsAtTheThirdApogee_FiresThere_AsAManeuverArmedJustBeforeIt()
    {
        // Arrange: skips the first two apogees; the reference is the same maneuver armed between the second and the
        // third apogee, on the ascending half of the orbit, where its g-function starts positive
        var window = new TimeSystem.Window(Start, Start.AddHours(12.0));
        var (skipping, skippingEngine) = CreateSpacecraft(-1356);
        var skippingManeuver = new SkippingPerigeeHeightManeuver(_earth, Start, skippingEngine, 2);
        skipping.SetStandbyManeuver(skippingManeuver);
        var (armed, armedEngine) = CreateSpacecraft(-1357);
        var armedManeuver = new PerigeeHeightManeuver(_earth, Start.AddHours(6.0), TimeSpan.Zero, 9000000.0,
            armedEngine);
        armed.SetStandbyManeuver(armedManeuver);

        // Act
        var skipped = skipping.Propagate(window, new CelestialItem[] { _earth }, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(60.0));
        var reference = armed.Propagate(window, new CelestialItem[] { _earth }, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(60.0));

        // Assert
        var thirdApogee = ApogeesWithin(window)[2];
        var burn = skipped.Segments[^1].BaseEpoch;
        _output.WriteLine($"burn {(burn - thirdApogee).TotalSeconds:E3} s from the Keplerian apogee, " +
                          $"{(burn - reference.Segments[^1].BaseEpoch).TotalSeconds:E3} s from the reference");
        Assert.Equal(3, skippingManeuver.Checks);
        Assert.Equal(4, skipped.Segments.Count);
        Assert.InRange((burn - thirdApogee).TotalSeconds, -Rk78EventTolerance, Rk78EventTolerance);
        Assert.InRange((burn - reference.Segments[^1].BaseEpoch).TotalSeconds, -Rk78EventTolerance,
            Rk78EventTolerance);
        Assert.NotNull(skippingManeuver.ThrustWindow);

        double deltaV = (skippingManeuver.DeltaV - armedManeuver.DeltaV).Magnitude();
        double position = (skipped.StateVectors[^1].Position - reference.StateVectors[^1].Position).Magnitude();
        double velocity = (skipped.StateVectors[^1].Velocity - reference.StateVectors[^1].Velocity).Magnitude();
        _output.WriteLine($"ΔV {deltaV:E3} m/s apart; final states {position:E3} m and {velocity:E3} m/s apart");
        Assert.True(deltaV < DeltaVTolerance, $"ΔV {deltaV:E3} m/s apart");
        Assert.True(position < PositionTolerance, $"final positions {position:E3} m apart");
        Assert.True(velocity < VelocityTolerance, $"final velocities {velocity:E3} m/s apart");
    }

    [Fact]
    public void SkippedApogees_LeaveTheStateTransitionMatrixContinuous()
    {
        // Arrange: Φ over 12 h across the four skipped apogees, against the same propagation without maneuver
        var window = new TimeSystem.Window(Start, Start.AddHours(12.0));
        var (skipping, engine) = CreateSpacecraft(-1358);
        skipping.SetStandbyManeuver(new CombinedManeuver(_earth, Start, TimeSpan.Zero, 9000000.0, 0.1, engine));
        var (free, _) = CreateSpacecraft(-1359);
        var phi = new double[36];
        var expected = new double[36];

        // Act
        var skipped = skipping.PropagateWithVariationalEquations(window, new CelestialItem[] { _earth }, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(60.0), new VariationalOptions());
        var reference = free.PropagateWithVariationalEquations(window, new CelestialItem[] { _earth }, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(60.0), new VariationalOptions());
        skipped.EvaluateVariational(window.EndDate, phi, Array.Empty<double>());
        reference.EvaluateVariational(window.EndDate, expected, Array.Empty<double>());

        // Assert
        Assert.Equal(5, skipped.Segments.Count);
        Assert.All(skipped.Segments.Skip(1), segment => Assert.Null(segment.Variational.EntryManeuver));
        double error = System.Math.Sqrt(phi.Zip(expected, (a, b) => (a - b) * (a - b)).Sum()) /
                       System.Math.Sqrt(expected.Sum(b => b * b));
        _output.WriteLine($"Φ(t, t0) relative error {error:E3}");
        Assert.True(error < PhiTolerance, $"Φ relative error {error:E3}");
    }

    // RK7(8) event epochs, on the 100 ns grid of the epochs: measured 0 and one tick (1e-7 s) from the Keplerian apogees
    // and from the reference; two ticks. With Velocity-Verlet the event is the end of the step that holds the root, up to
    // one step (10 s) after it.
    private const double Rk78EventTolerance = 2e-7;
    private const double VelocityVerletStep = 10.0;

    // The skipped apogees split the propagation into segments, whose steps differ from those of the reference: measured
    // ΔV 2.0e-9 m/s, final state 1.3e-4 m and 4.7e-8 m/s apart (RK7(8) at 1e-11, 12 h), Φ 4.5e-11 relative. The
    // tolerances are about four to five times these.
    private const double DeltaVTolerance = 1e-8;
    private const double PositionTolerance = 5e-4;
    private const double VelocityTolerance = 2e-7;
    private const double PhiTolerance = 2e-10;

    private (Spacecraft Spacecraft, Engine Engine) CreateSpacecraft(int id)
    {
        var orbit = new KeplerianElements(SemiMajorAxis, 0.5, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0,
            System.Math.PI * 0.5, MeanAnomaly, _earth, Start, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(id, "CONTINUE" + -id, 1000.0, 3000.0, new Clock("continue" + -id, 65536),
            orbit);
        var tank = new FuelTank("tank" + -id, "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("engine" + -id, "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        return (spacecraft, engine);
    }

    // The Keplerian apogees within the window
    private TimeSystem.Time[] ApogeesWithin(TimeSystem.Window window)
    {
        double n = System.Math.Sqrt(_earth.GM / (SemiMajorAxis * SemiMajorAxis * SemiMajorAxis));
        return Enumerable.Range(0, 100)
            .Select(k => Start.AddSeconds((System.Math.PI - MeanAnomaly + 2.0 * System.Math.PI * k) / n))
            .TakeWhile(epoch => epoch < window.EndDate)
            .ToArray();
    }

    /// <summary>
    /// A perigee height maneuver whose precondition fails at its first <c>skips</c> apogees.
    /// </summary>
    private sealed class SkippingPerigeeHeightManeuver : PerigeeHeightManeuver
    {
        private readonly int _skips;

        public SkippingPerigeeHeightManeuver(CelestialItem center, TimeSystem.Time minimumEpoch, Engine engine,
            int skips) : base(center, minimumEpoch, TimeSpan.Zero, 9000000.0, engine)
        {
            _skips = skips;
        }

        public int Checks { get; private set; }

        public override bool CheckPreconditions(StateVector state) => ++Checks > _skips;
    }
}
