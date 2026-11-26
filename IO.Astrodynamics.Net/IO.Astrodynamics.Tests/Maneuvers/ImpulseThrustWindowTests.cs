// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Maneuvers;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The thrust window of an impulse is the equivalent constant-thrust burn whose acceleration has its centroid at the
/// impulse epoch t (#335, derived from S. Guillet's remark of 2026-11-18, decided by S. Guillet, 2026-11-18): [t − f dt, t + (1 − f) dt], f = 1/x + 1/ln(1 − x) for the
/// mass fraction burned x. The reference is the centroid of the acceleration F / (m0 − q τ) over [0, dt], by quadrature
/// (<see cref="TimelineRules.ThrustLead"/>), and the 40-digit values of f by mpmath 1.4.1 for the closed form. On
/// f70ee32 the window opened a fixed 0.666 dt before t: 1.88 s early for the 16.1 s burn of an apogee raise from
/// 6 678 km to 42 164 km (x = 0.42, f = 0.549).
/// </summary>
/// <remarks>
/// Thresholds: the epochs to the tick of a <see cref="Time"/>, 1e-7 s, the resolution of the windows (measured: one
/// tick at most, the rounding of the burn duration and of f dt to their ticks), and the rounding of the burn duration to
/// its tick, 0.5e-7 s; f to <see cref="LeadBound"/>.
/// </remarks>
public class ImpulseThrustWindowTests
{
    private const double Isp = 300.0;
    private const double FuelFlow = 10.0;

    /// <summary>f against mpmath and the quadrature: worst 5.551e-15 at u = 0.1 on Linux x64, Windows x64 and macOS arm64
    /// (the MEASURE lines, CI run 44395076184, 2026-11-18), the cancellation of the closed form where the library leaves
    /// its series, about ε/u; twice that, rounded up.</summary>
    private const double LeadBound = 1.2e-14;

    private readonly ITestOutputHelper _output;

    public ImpulseThrustWindowTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    private static Spacecraft NewSpacecraft(int naifId, StateVector initial, double dry, double fuel, out Engine engine)
    {
        var spacecraft = new Spacecraft(naifId, "TW" + -naifId, dry, initial, dry + fuel + 1.0, new Clock("tw" + -naifId, 65536));
        var tank = new FuelTank("tank" + -naifId, fuel, fuel, "model", "sn");
        spacecraft.AddFuelTank(tank);
        engine = new Engine("engine" + -naifId, Isp, FuelFlow, tank, "model", "sn");
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    /// <summary>
    /// f = 1/(1 − e^(−u)) − 1/u, u = Δv / (Isp g0), against mpmath 1.4.1 at 40 digits (the values below, at the u of the
    /// doubles the library computes), on both sides of u = 0.1, where the library leaves its series for the closed form,
    /// and against the quadrature of <see cref="TimelineRules.ThrustLead"/>.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(2.94199500e-6, 0.50000000008333333333)]
    [InlineData(0.00294199500, 0.50000008333333333333)]
    [InlineData(14.7099750, 0.5004166664930556589)]
    [InlineData(294.1994, 0.5083319419439202994)]
    [InlineData(294.1995, 0.50833194477504962451)]
    [InlineData(294.19953, 0.50833194562438842197)]
    [InlineData(1000.0, 0.52827105663092169483)]
    [InlineData(2039.20, 0.55730397616858711006)]
    [InlineData(6494.0, 0.67055287688250408496)]
    [InlineData(29419.95, 0.90004540199100968777)]
    public void ThrustLeadFraction_IsTheClosedForm(double deltaV, double expected)
    {
        // Act
        double lead = ImpulseManeuver.ThrustLeadFraction(deltaV, Isp);

        // Assert
        double error = lead - expected;
        double quadrature = deltaV > 0.0 ? lead - TimelineRules.ThrustLead(deltaV, Isp) : 0.0;
        _output.WriteLine($"MEASURE #335 f at {deltaV} m/s: {lead:R}, {error:E3} from mpmath, {quadrature:E3} from the quadrature");
        Assert.True(System.Math.Abs(error) <= LeadBound, $"{error:E3}");
        Assert.True(System.Math.Abs(quadrature) <= LeadBound, $"{quadrature:E3}");
    }

    /// <summary>
    /// <c>TryExecute</c> at t: the thrust window opens at t less the centroid of the acceleration of the burn, from its
    /// mass before the burn, Isp and fuel flow, and lasts the burn duration; small (x = 3.4e-4), medium (x = 0.1),
    /// large (x = 0.5) and near-total (x = 0.89, where f = 2/3) burns. On f70ee32 it opened 0.666 dt before t.
    /// </summary>
    [Theory]
    [InlineData(1.0, -7501)]
    [InlineData(310.0, -7502)]
    [InlineData(2039.0, -7503)]
    [InlineData(6494.0, -7504)]
    public void TryExecute_ThrustWindowOpensAtTheCentroidOfTheAcceleration(double deltaV, int naifId)
    {
        // Arrange: 100 kg dry, 900 kg of fuel
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, Start);
        var state = new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 7612.6, 0.0), earth, Start, Frame.ICRF);
        var spacecraft = NewSpacecraft(naifId, state, 100.0, 900.0, out var engine);
        var impulse = new FixedImpulse(earth, Start, Start, new Vector3(0.0, deltaV, 0.0), engine);
        double massBefore = spacecraft.GetTotalMass();

        // Act
        impulse.TryExecute(state);

        // Assert: the reference centroid, by quadrature, of a burn of the rocket equation's duration
        double dt = TimelineRules.BurnDuration(massBefore, deltaV, Isp, FuelFlow);
        double lead = TimelineRules.ThrustLead(deltaV, Isp);
        double x = TimelineRules.FuelBurned(massBefore, deltaV, Isp) / massBefore;
        var thrust = impulse.ThrustWindow!.Value;
        double startError = (thrust.StartDate - Start.AddSeconds(-lead * dt)).TotalSeconds;
        double lengthError = thrust.Length.TotalSeconds - dt;
        _output.WriteLine($"MEASURE #335 x = {x:E3}: f = {lead:R}, dt = {dt:R} s, start {startError:E3} s, length {lengthError:E3} s " +
                          $"from the reference; 0.666 dt would open {(0.666 - lead) * dt:E3} s earlier");
        Assert.True(System.Math.Abs(startError) <= 1.0e-7, $"{startError:E3} s");
        Assert.True(System.Math.Abs(lengthError) <= 0.5e-7, $"{lengthError:E3} s");
        LabelAssert.Equal(thrust, impulse.ManeuverWindow!.Value);
    }

    /// <summary>
    /// In a propagation, about the Earth (TT seconds) and the Moon (TDB seconds), in RK7(8) and Velocity-Verlet: the
    /// thrust window of a burn of 2 039 m/s (x = 0.5) opens f dt before its event in seconds of the time argument, and
    /// the attitude chained after it activates at the end of its window, its gate; the impulse's attitude is recorded from
    /// the opening of its thrust window.
    /// </summary>
    [Theory]
    [InlineData("RK", "Earth", -7511)]
    [InlineData("VV", "Earth", -7512)]
    [InlineData("RK", "Moon", -7513)]
    [InlineData("VV", "Moon", -7514)]
    public void Propagation_ThrustWindowOpensAtTheCentroid_AndGatesTheNextManeuver(string integrator, string central, int naifId)
    {
        // Arrange
        bool earthCentred = central == "Earth";
        CelestialBody body = earthCentred
            ? new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, Start)
            : new CelestialBody(PlanetsAndMoons.Moon, Frame.ICRF, Start);
        var initial = earthCentred
            ? new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 7612.6, 0.0), body, Start, Frame.ICRF)
            : new StateVector(new Vector3(1.9e6, 0.0, 0.0), new Vector3(0.0, 1606.0, 0.0), body, Start, Frame.ICRF);
        var spacecraft = NewSpacecraft(naifId, initial, 100.0, 900.0, out var engine);
        var burn = Start.AddSeconds(600.0);
        const double deltaV = 2039.0;
        var impulse = new FixedImpulse(body, Start, burn, new Vector3(0.0, deltaV, 0.0), engine);
        var nadir = new NadirAttitude(Start, TimeSpan.FromSeconds(30.0), body);
        spacecraft.SetManeuvers(impulse, nadir);
        double massBefore = spacecraft.GetTotalMass();
        var window = new Window(Start, Start.AddSeconds(1800.0));

        // Act
        var solution = integrator == "RK"
            ? spacecraft.Propagate(window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = new CelestialItem[] { body }, CovariancePropagation = CovariancePropagationOptions.None })
            : spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = new CelestialItem[] { body } });

        // Assert: the event, the base of the segment after the impulse; the windows in seconds of the time argument
        var argument = solution.TimeArgument;
        Assert.Same(earthCentred ? PropagationTimeArgument.Tt : PropagationTimeArgument.Tdb, argument);
        var tc = solution.Segments[1].BaseEpoch;
        double dt = TimelineRules.BurnDuration(massBefore, deltaV, Isp, FuelFlow);
        double lead = TimelineRules.ThrustLead(deltaV, Isp);
        var thrust = impulse.ThrustWindow!.Value;
        double startError = argument.SecondsBetween(thrust.StartDate, tc) - lead * dt;
        double endError = argument.SecondsBetween(tc, thrust.EndDate) - (1.0 - lead) * dt;
        double gateError = (nadir.ManeuverWindow!.Value.StartDate - impulse.ManeuverWindow!.Value.EndDate).TotalSeconds;
        _output.WriteLine($"MEASURE #335 {integrator} {central}: f = {lead:R}, dt = {dt:R} s, event {(tc - burn).TotalSeconds:E3} s from " +
                          $"the burn epoch, start {startError:E3} s, end {endError:E3} s, gate {gateError:E3} s");
        Assert.True(System.Math.Abs(startError) <= 1.0e-7, $"start {startError:E3} s");
        Assert.True(System.Math.Abs(endError) <= 1.0e-7, $"end {endError:E3} s");
        Assert.Equal(0.0, gateError);
        LabelAssert.Equal(thrust.EndDate, impulse.ManeuverWindow.Value.EndDate);

        // The impulse's attitude from the opening of its thrust window, its front along the delta-V: 1 ms after it the
        // record holds it, 1 ms before it the identity of no attitude
        var expectedFront = new Vector3(0.0, 1.0, 0.0);
        var after = spacecraft.Frame.GetStateOrientationToIcrf(thrust.StartDate.AddSeconds(1.0e-3));
        var before = spacecraft.Frame.GetStateOrientationToIcrf(thrust.StartDate.AddSeconds(-1.0e-3));
        double frontAfter = Spacecraft.Front.Rotate(after.Rotation).Angle(expectedFront);
        double frontBefore = Spacecraft.Front.Rotate(before.Rotation).Angle(Spacecraft.Front);
        Assert.True(frontAfter < 1.0e-9, $"{frontAfter:E3} rad");
        Assert.True(frontBefore < 1.0e-9, $"{frontBefore:E3} rad");
    }
}
