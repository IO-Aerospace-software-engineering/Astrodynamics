// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The TCA and miss distance of the conjunction analysis of two RK7(8) trajectories (#363), against the two-body
/// solution in universal variables.
/// </summary>
public class ConjunctionTcaAccuracyTests
{
    private static readonly TimeSystem.Time Start = PartialsTestCases.Epoch;

    // The secondary is 5 % faster than circular at the meeting, on a 400 × 1900 km orbit: its steps differ from those of
    // the protected spacecraft, so the errors of their interpolations do not cancel in the relative state
    private const double SecondarySpeedFactor = 1.05;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public ConjunctionTcaAccuracyTests(ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _output = output;
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
    }

    /// <summary>
    /// A 400 km LEO and a 400 × 1900 km orbit, point-mass Earth, the default tolerance of RK7(8), crossing at 40° with a
    /// radial offset of 100 m, at several epochs within the steps of the two trajectories. The search on the
    /// polynomials of the trajectories locates the TCA on their cubic Hermite interpolation, off by 2.6e-6 s to 1.3e-4 s
    /// in these cases; the TCA is then refined on the states of the trajectories, which have the accuracy of the
    /// integrator: 8.8e-9 s to 5.1e-8 s. The miss distance is computed on those states in both cases, so the error of
    /// the TCA reaches it at second order only (0.2 mm to 2.8 mm before the refinement, 0.2 mm to 0.26 mm after, the
    /// error of the integration over 2000 s).
    /// </summary>
    [Theory]
    [InlineData(1900.0)]
    [InlineData(1950.0)]
    [InlineData(2000.0)]
    [InlineData(2050.0)]
    [InlineData(2100.0)]
    [InlineData(2150.0)]
    [InlineData(2200.0)]
    [InlineData(2250.0)]
    public void Analyze_OfTwoRk78Trajectories_FindsTheKeplerianTcaAndMissDistance(double meeting)
    {
        // Arrange: the states at the meeting, then back to the start of the window along the Keplerian orbits
        double mu = _earth.GM;
        var protectedInitial = PartialsTestCases.State(PartialsTestCases.Leo400, _earth);
        var (protectedAtMeeting, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity,
            meeting);
        var radial = Position(protectedAtMeeting).Normalize();
        var secondaryPosition = Position(protectedAtMeeting) + radial * 100.0;
        var velocity = Velocity(protectedAtMeeting);
        double angle = 40.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var secondaryVelocity = (velocity * System.Math.Cos(angle) + radial.Cross(velocity) * System.Math.Sin(angle)) *
                                (SecondarySpeedFactor * System.Math.Sqrt(mu / secondaryPosition.Magnitude()) /
                                 velocity.Magnitude());
        var (secondaryStart, _) = KeplerianStm.Propagate(mu, secondaryPosition, secondaryVelocity, -meeting);
        var secondaryInitial = new StateVector(Position(secondaryStart), Velocity(secondaryStart), _earth, Start,
            Frames.Frame.ICRF);

        var window = new TimeSystem.Window(Start, Start.AddHours(1.0));
        var protectedSpacecraft = Spacecraft(-1981, "TCAP", protectedInitial);
        var secondarySpacecraft = Spacecraft(-1982, "TCAS", secondaryInitial);
        var protectedTrajectory = protectedSpacecraft.Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0));
        var secondaryTrajectory = secondarySpacecraft.Propagate(window, new CelestialItem[] { _earth },
            new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0));

        // The reference: the zero of the range rate of the Keplerian states
        double referenceTca = KeplerianTca(mu, protectedInitial, secondaryInitial, meeting - 100.0, meeting + 100.0);
        var (p, _) = KeplerianStm.Propagate(mu, protectedInitial.Position, protectedInitial.Velocity, referenceTca);
        var (s, _) = KeplerianStm.Propagate(mu, secondaryInitial.Position, secondaryInitial.Velocity, referenceTca);
        double referenceMiss = (Position(s) - Position(p)).Magnitude();

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedSpacecraft),
            protectedTrajectory, secondarySpacecraft, secondaryTrajectory, window);

        // Assert
        double tcaError = (encounter.EncounterState.Epoch - Start).TotalSeconds - referenceTca;
        double missError = encounter.EncounterState.MissDistanceMeters - referenceMiss;
        _output.WriteLine($"Keplerian TCA {referenceTca:F6} s, miss {referenceMiss:F6} m; analysis: TCA {tcaError:E3} s, " +
                          $"miss {missError:E3} m from the reference");
        Assert.True(System.Math.Abs(tcaError) < 1e-6, $"TCA {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) < 1e-3, $"miss distance {missError:E3} m");
    }

    private static double KeplerianTca(double mu, StateVector first, StateVector second, double low, double high)
    {
        double RangeRate(double t)
        {
            var (a, _) = KeplerianStm.Propagate(mu, first.Position, first.Velocity, t);
            var (b, _) = KeplerianStm.Propagate(mu, second.Position, second.Velocity, t);
            return (Position(b) - Position(a)) * (Velocity(b) - Velocity(a));
        }

        Assert.True(RangeRate(low) < 0.0 && RangeRate(high) > 0.0);
        while (high - low > 1e-10)
        {
            double middle = 0.5 * (low + high);
            if (RangeRate(middle) < 0.0)
                low = middle;
            else
                high = middle;
        }

        return 0.5 * (low + high);
    }

    private static Spacecraft Spacecraft(int naifId, string name, StateVector state)
    {
        return new Spacecraft(naifId, name, 100.0, 1000.0, new Clock(name, 65536), state, 1.0);
    }

    private static Vector3 Position(double[] state) => new(state[0], state[1], state[2]);

    private static Vector3 Velocity(double[] state) => new(state[3], state[4], state[5]);
}
