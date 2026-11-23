// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Maneuvers;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// Pure previews and the run state of the definitions, lot L5a of #668 (decisions 50 to 52, decided by S. Guillet,
/// 2026-11-23; #802, #784): <see cref="ImpulseManeuver.ComputeDeltaV"/> and <see cref="Attitude.ComputeStateOrientation"/>
/// change nothing and equal the execution at the same state; <see cref="ImpulseManeuver.ComputeEventValue"/> is pure, the
/// node of a <see cref="PlaneAlignmentManeuver"/> locked by the detector only; the re-arm clears the crossing direction
/// (#784) and the hold of a <see cref="PhasingManeuver"/>, which takes none.
/// </summary>
/// <remarks>
/// The references are the decided rules: a preview equals the internal execution (<c>TryExecute</c>, which a run calls) of a
/// fresh twin at the same state, bit for bit; what the preview leaves is compared with what was there before it; a
/// re-armed maneuver with a fresh one. The case: a 7 000 km orbit, e = 0.01, i = 0.5 rad, about a point-mass Earth from
/// 2024-03-01 12:00 TDB; 1 000 kg dry, 1 000 kg of fuel, Isp 300 s, 10 kg/s.
/// </remarks>
public class ManeuverPreviewTests
{
    private readonly ITestOutputHelper _output;

    public ManeuverPreviewTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static int _nextId = -850000;

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, S);

    private static Spacecraft NewSpacecraft(CelestialBody earth, out Engine engine)
    {
        int id = Interlocked.Decrement(ref _nextId);
        var spacecraft = new Spacecraft(id, "PREV" + -id, 1000.0, 3000.0, new Clock("PREV" + -id, 65536),
            new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF));
        var tank = new FuelTank("ft" + -id, "m", "sn" + -id, 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("eng" + -id, "m", "sn" + -id, 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator = "VV") => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
    };

    // The library impulses, each built twice on the engine, at a state where each has a delta-V
    private static ImpulseManeuver Impulse(string kind, CelestialBody earth, Engine engine)
    {
        var target = new KeplerianElements(7200e3, 0.05, 0.6, 0.3, 0.4, 0.0, earth, S, Frames.Frame.ICRF);
        return kind switch
        {
            "Apogee" => new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine),
            "Perigee" => new PerigeeHeightManeuver(earth, S, TimeSpan.Zero, 7100e3, engine),
            "Combined" => new CombinedManeuver(earth, S, TimeSpan.Zero, 7100e3, 0.55, engine),
            "Plane" => new PlaneAlignmentManeuver(S, TimeSpan.Zero, target, engine),
            "Apsidal" => new ApsidalAlignmentManeuver(S, TimeSpan.Zero, new KeplerianElements(7010e3, 0.012, 0.5, 0.0, 0.2, 0.0, earth, S, Frames.Frame.ICRF), engine),
            "PhasingOrbit" => new PhasingManeuver(S, target, 2, engine),
            _ => new PhasingManeuver(earth, S, 1.0, 2, engine)
        };
    }

    public static IEnumerable<object[]> Kinds() =>
        new[] { "Apogee", "Perigee", "Combined", "Plane", "Apsidal", "PhasingOrbit", "PhasingLongitude" }.Select(k => new object[] { k });

    // ---------------------------------------------------------------------------------------------------------------------
    // ComputeDeltaV
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The preview of each library impulse, at its own state, is the delta-V the execution applies at that state, bit for
    /// bit, and leaves the maneuver, its tank and its spacecraft as they were.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void ComputeDeltaV_EqualsTheExecution_AndChangesNothing(string kind)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var state = spacecraft.InitialOrbitalParameters.ToStateVector().AtEpoch(S.AddSeconds(900.0)).ToStateVector();
        var previewed = Impulse(kind, earth, engine);
        if (previewed is ApsidalAlignmentManeuver intersecting)
        {
            // The apsidal alignment burns at an intersection of the two orbits
            state = state.ToStateVector(intersecting.GetPTrueAnomaly(state));
        }

        spacecraft.SetManeuvers(previewed);
        double fuel = spacecraft.FuelTanks.Single().Quantity;
        var standby = spacecraft.StandbyManeuver;
        var crossing = previewed.EventCrossingDirection;

        var deltaV = previewed.ComputeDeltaV(state);

        Assert.Equal(fuel, spacecraft.FuelTanks.Single().Quantity);
        Assert.Equal(0.0, previewed.FuelBurned);
        Assert.Equal(Vector3.Zero, previewed.DeltaV);
        Assert.Null(previewed.ThrustWindow);
        Assert.Null(previewed.ManeuverWindow);
        Assert.Same(standby, spacecraft.StandbyManeuver);
        Assert.Equal(crossing, previewed.EventCrossingDirection);
        Assert.Equal(TimeSpan.Zero, previewed.ManeuverHoldDuration);
        if (previewed is PlaneAlignmentManeuver plane) Assert.Null(plane.IsAscendingNode);
        if (previewed is ApsidalAlignmentManeuver apsidal) Assert.False(apsidal.IntersectsP || apsidal.IntersectsQ);

        // The execution of a twin on another spacecraft at the same state
        var other = NewSpacecraft(earth, out var otherEngine);
        var executed = Impulse(kind, earth, otherEngine);
        other.SetManeuvers(executed);
        executed.TryExecute(state);
        _output.WriteLine($"MEASURE {kind}: preview {deltaV} m/s, execution {executed.DeltaV} m/s");
        Assert.NotEqual(Vector3.Zero, deltaV);
        Assert.Equal(executed.DeltaV, deltaV);
    }

    /// <summary>A preview after a finished run changes nothing the run left (#802: the public TryExecute took the fuel).</summary>
    [Theory]
    [InlineData("VV")]
    [InlineData("RK")]
    public void ComputeDeltaV_AfterARun_ChangesNothing(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var apogee = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);
        var perigee = new PerigeeHeightManeuver(earth, S, TimeSpan.Zero, 7100e3, engine);
        spacecraft.SetManeuvers(apogee, perigee);
        spacecraft.Propagate(new Window(S, S.AddHours(1.0)), Options(earth, integrator));
        Assert.NotNull(apogee.ThrustWindow);
        double fuel = spacecraft.FuelTanks.Single().Quantity;
        var deltaV = apogee.DeltaV;
        var window = apogee.ThrustWindow;
        var standby = spacecraft.StandbyManeuver;
        var executed = spacecraft.ExecutedManeuvers.ToArray();

        apogee.ComputeDeltaV(spacecraft.InitialOrbitalParameters.ToStateVector());
        perigee.ComputeDeltaV(spacecraft.InitialOrbitalParameters.ToStateVector().AtEpoch(S.AddSeconds(2900.0)).ToStateVector());

        Assert.Equal(fuel, spacecraft.FuelTanks.Single().Quantity);
        Assert.Equal(deltaV, apogee.DeltaV);
        LabelAssert.Equal(window, apogee.ThrustWindow);
        Assert.Same(standby, spacecraft.StandbyManeuver);
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
    }

    /// <summary>
    /// The preview takes the state about its own observer, in its frame (decision 50; the given centre comes with lot L5b):
    /// a state in ECLIPJ2000 gives the delta-V of the execution at that state, in ECLIPJ2000; a state about the Moon gives
    /// the delta-V of a twin maneuver whose centre is the Moon.
    /// </summary>
    [Fact]
    public void ComputeDeltaV_TheStatesObserverAndFrame()
    {
        var earth = Earth();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, S);
        var spacecraft = NewSpacecraft(earth, out var engine);
        var apogee = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);

        var ecliptic = spacecraft.InitialOrbitalParameters.ToStateVector().ToFrame(Frames.Frame.ECLIPTIC_J2000);
        var twin = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);
        twin.TryExecute(ecliptic);
        Assert.Equal(twin.DeltaV, apogee.ComputeDeltaV(ecliptic));

        var aboutTheMoon = new StateVector(new Vector3(2000e3, 0.0, 0.0), new Vector3(0.0, 1600.0, 100.0), moon, S, Frames.Frame.ICRF);
        var lunar = new ApogeeHeightManeuver(moon, S, TimeSpan.Zero, 8000e3, engine);
        lunar.TryExecute(aboutTheMoon);
        Assert.Equal(lunar.DeltaV, apogee.ComputeDeltaV(aboutTheMoon));
    }

    [Fact]
    public void ComputeDeltaV_Null_Throws()
    {
        var earth = Earth();
        NewSpacecraft(earth, out var engine);
        var apogee = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);
        Assert.Equal("stateVector", Assert.Throws<ArgumentNullException>(() => apogee.ComputeDeltaV(null)).ParamName);
    }

    /// <summary>
    /// The plane change at both nodes of PlaneAlignmentManeuverTests' case, as a preview: the values of the execution, the
    /// node by the rule of the state, and no node locked.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlaneAlignment_ComputeDeltaV_TakesTheNodeOfTheState(bool ascending)
    {
        var orbit = new KeplerianElements(11480000.0, 0.0, 60.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var target = new KeplerianElements(11480000.0, 0.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 55.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(Interlocked.Decrement(ref _nextId), "PLANEPREV", 1000.0, 3000.0, new Clock("PLANEPREV", 65536), orbit);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 2000.0, 1900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        var plane = new PlaneAlignmentManeuver(new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, target, spc.Engines.First());
        var dn = 2.197937654;
        var state = orbit.ToStateVector(ascending ? dn + IO.Astrodynamics.Constants.PI : dn);

        var deltaV = plane.ComputeDeltaV(state);

        var expected = ascending
            ? new Vector3(-1485.9760225904602, 2563.0404801133755, -2458.4753263817515)
            : new Vector3(1485.9760225904588, -2563.040480113374, 2458.47532638175);
        Assert.Equal(expected, deltaV, TestHelpers.VectorComparer);
        Assert.Null(plane.IsAscendingNode);
        Assert.Equal(CrossingDirection.Any, plane.EventCrossingDirection);
        Assert.Equal(1900.0, spc.FuelTanks.First().Quantity);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // ComputeEventValue
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The public g-function locks nothing (#802: it locked the node of a plane alignment and wrote the intersection of an
    /// apsidal alignment); the detector of a run locks the node, by the same rule.
    /// </summary>
    [Fact]
    public void ComputeEventValue_IsPure_TheDetectorLocksTheNode()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var state = spacecraft.InitialOrbitalParameters.ToStateVector();
        var plane = (PlaneAlignmentManeuver)Impulse("Plane", earth, engine);
        var apsidal = (ApsidalAlignmentManeuver)Impulse("Apsidal", earth, engine);

        double g = plane.ComputeEventValue(state);
        apsidal.ComputeEventValue(state);

        Assert.Null(plane.IsAscendingNode);
        Assert.Equal(CrossingDirection.Any, plane.EventCrossingDirection);
        Assert.False(apsidal.IntersectsP);
        Assert.False(apsidal.IntersectsQ);

        var detector = new ManeuverEventDetector(plane);
        Assert.Equal(g, detector.Evaluate(state));
        Assert.NotNull(plane.IsAscendingNode);
        Assert.NotEqual(CrossingDirection.Any, plane.EventCrossingDirection);
        Assert.Equal(plane.IsAscendingNode == true ? CrossingDirection.NegativeToPositive : CrossingDirection.PositiveToNegative, detector.Direction);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The re-arm (#784) and the hold of the phasing maneuver
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// #784: after a run that locks the node of a plane alignment, the re-arm of the next run, which does not reach it,
    /// clears the node and the crossing direction, as a fresh maneuver's (before, the direction of the last node stayed).
    /// </summary>
    [Theory]
    [InlineData("VV")]
    [InlineData("RK")]
    public void PlaneAlignment_TheReArm_ClearsTheCrossingDirection(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var target = new KeplerianElements(7000e3, 0.0, 0.6, 0.3, 0.0, 0.0, earth, S, Frames.Frame.ICRF);
        var plane = new PlaneAlignmentManeuver(S.AddSeconds(1800.0), TimeSpan.Zero, target, engine);
        var fresh = new PlaneAlignmentManeuver(S.AddSeconds(1800.0), TimeSpan.Zero, target, engine);
        spacecraft.SetManeuvers(plane);
        spacecraft.Propagate(new Window(S, S.AddHours(3.0)), Options(earth, integrator));
        Assert.NotNull(plane.ThrustWindow);
        Assert.NotEqual(CrossingDirection.Any, plane.EventCrossingDirection);

        spacecraft.Propagate(new Window(S, S.AddSeconds(600.0)), Options(earth, integrator));

        Assert.Null(plane.ThrustWindow);
        Assert.Equal(fresh.IsAscendingNode, plane.IsAscendingNode);
        Assert.Equal(fresh.EventCrossingDirection, plane.EventCrossingDirection);
        Assert.Equal(CrossingDirection.Any, plane.EventCrossingDirection);
        Assert.Equal(0.0, plane.RelativeInclination);
    }

    /// <summary>
    /// The phasing maneuver takes no hold: 0 before the burn, 0.9 N T₂ after it, 0 again after the re-arm of a run that
    /// does not reach it (decision 51; before, the hold the burn computed was left for good).
    /// </summary>
    [Theory]
    [InlineData("VV")]
    [InlineData("RK")]
    public void Phasing_TheHold_IsTheLastRunsValue(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var phasing = new PhasingManeuver(earth, S.AddSeconds(1800.0), 1.0, 2, engine);
        Assert.Equal(TimeSpan.Zero, phasing.ManeuverHoldDuration);
        spacecraft.SetManeuvers(phasing);

        spacecraft.Propagate(new Window(S, S.AddHours(3.0)), Options(earth, integrator));
        Assert.NotNull(phasing.ThrustWindow);
        var hold = phasing.ManeuverHoldDuration;
        Assert.True(hold > TimeSpan.Zero);

        spacecraft.Propagate(new Window(S, S.AddSeconds(600.0)), Options(earth, integrator));
        Assert.Null(phasing.ThrustWindow);
        Assert.Equal(TimeSpan.Zero, phasing.ManeuverHoldDuration);

        // The preview computes no hold
        phasing.ComputeDeltaV(spacecraft.InitialOrbitalParameters.ToStateVector());
        Assert.Equal(TimeSpan.Zero, phasing.ManeuverHoldDuration);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // ComputeStateOrientation
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The attitude preview is the orientation of the internal execution at the same state, sets no window and moves no
    /// chain; in another frame, the state rotated to ICRF first.
    /// </summary>
    [Fact]
    public void ComputeStateOrientation_EqualsTheExecution_AndChangesNothing()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var nadir = new NadirAttitude(earth, S, TimeSpan.FromSeconds(10.0), engine);
        var prograde = new ProgradeAttitude(earth, S, TimeSpan.Zero, engine);
        spacecraft.SetManeuvers(nadir, prograde);
        var state = spacecraft.InitialOrbitalParameters.ToStateVector();

        var orientation = nadir.ComputeStateOrientation(state);
        var later = nadir.ComputeStateOrientation(state.AtEpoch(S.AddSeconds(60.0)).ToStateVector());

        Assert.Null(nadir.ThrustWindow);
        Assert.Null(nadir.ManeuverWindow);
        Assert.Same(nadir, spacecraft.StandbyManeuver);
        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.NotNull(later);

        var twin = new NadirAttitude(earth, S, TimeSpan.FromSeconds(10.0), engine);
        var (_, executed) = twin.TryExecute(state);
        Assert.Equal(executed.Rotation, orientation.Rotation);
        LabelAssert.Equal(state.Epoch, orientation.Epoch);
        Assert.Equal(Frames.Frame.ICRF, orientation.ReferenceFrame);

        var ecliptic = nadir.ComputeStateOrientation(state.ToFrame(Frames.Frame.ECLIPTIC_J2000));
        double angle = Body.AttitudeRecordTests.RotationAngle(orientation.Rotation, ecliptic.Rotation);
        _output.WriteLine($"MEASURE ComputeStateOrientation ECLIPJ2000 against ICRF: {angle:E3} rad");
        Assert.True(angle < 1e-12, $"{angle:E3} rad");
        Assert.Equal("stateVector", Assert.Throws<ArgumentNullException>(() => nadir.ComputeStateOrientation(null)).ParamName);
    }

    /// <summary>
    /// After a run, the preview at the state of the solution at the activation of an attitude is the attitude the record
    /// holds there (the migration of <c>Attitude.StateOrientation</c>, removed by decision 52).
    /// </summary>
    [Theory]
    [InlineData("VV")]
    [InlineData("RK")]
    public void ComputeStateOrientation_IsTheRecordAtTheActivation(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var nadir = new NadirAttitude(earth, S.AddSeconds(300.0), TimeSpan.Zero, engine);
        spacecraft.SetManeuvers(nadir);
        var solution = spacecraft.Propagate(new Window(S, S.AddHours(1.0)), Options(earth, integrator));
        var activation = nadir.ThrustWindow!.Value.StartDate;
        var (position, velocity) = solution.InterpolateAt(activation);

        var preview = nadir.ComputeStateOrientation(new StateVector(position, velocity, earth, activation, Frames.Frame.ICRF));

        double angle = Body.AttitudeRecordTests.RotationAngle(preview.Rotation, spacecraft.Frame.GetStateOrientationToICRF(activation).Rotation);
        _output.WriteLine($"MEASURE ComputeStateOrientation {integrator} against the record at the activation: {angle:E3} rad");
        Assert.Equal(0.0, angle);
    }
}
