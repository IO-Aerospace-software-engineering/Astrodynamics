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
    /// The preview takes the state about the centre the maneuver is given, as the execution does (decision 50: the given
    /// centre, or the state's observer when none is given), and returns the delta-V in the frame of the state: the test
    /// orbit about the Earth re-expressed about the Sun (an SSB-mode solution's states are about the Sun) and about the
    /// Moon, in ICRF and in ECLIPJ2000, gives the delta-V of the execution at that state, bit for bit, and the delta-V of
    /// the same orbit given about the Earth, to the rounding of the change of centre.
    /// </summary>
    [Theory]
    [InlineData("Sun", "ICRF")]
    [InlineData("Sun", "ECLIPJ2000")]
    [InlineData("Moon", "ICRF")]
    [InlineData("Earth", "ECLIPJ2000")]
    public void ComputeDeltaV_TakesTheStateAboutTheGivenCentre(string observer, string frame)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var apogee = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);
        var targetFrame = frame == "ICRF" ? Frames.Frame.ICRF : Frames.Frame.ECLIPTIC_J2000;
        var aboutTheEarth = spacecraft.InitialOrbitalParameters.ToStateVector().ToFrame(targetFrame);
        CelestialItem other = observer switch
        {
            "Sun" => Stars.SUN_BODY,
            "Moon" => new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, S),
            _ => earth
        };
        var state = aboutTheEarth.RelativeTo(other, Aberration.None);
        Assert.Equal(other.NaifId, state.Observer.NaifId);

        var deltaV = apogee.ComputeDeltaV(state);

        var twin = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, 8000e3, engine);
        twin.TryExecute(state);
        Assert.Equal(twin.DeltaV, deltaV);
        var reference = apogee.ComputeDeltaV(aboutTheEarth);
        double difference = (deltaV - reference).Magnitude();
        _output.WriteLine($"MEASURE ComputeDeltaV about {observer} in {frame} against about the Earth: {difference:E3} m/s of {reference.Magnitude():F6} m/s");
        Assert.True(difference < 1e-9, $"{difference:E3} m/s");
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
    /// The plane change at both nodes of PlaneAlignmentManeuverTests' case, as a preview, against the closed form of a
    /// plane change of a circular orbit at a node (review of L5a, #668): the velocity v = √(μ/a) turned from the plane of
    /// normal ĥ₁ into the plane of normal ĥ₂ about the node direction r̂, ΔV = v (ĥ₂ − ĥ₁) × r̂, of magnitude 2 v sin(θ/2),
    /// θ the angle between the normals; ĥ = (sin Ω sin i, −cos Ω sin i, cos i), the node ±(ĥ₁ × ĥ₂)/|ĥ₁ × ĥ₂|, μ the kernel
    /// GM of the Earth (BODY399_GM). The state is that of the orbit at the node, its argument of latitude from the same
    /// closed form; the node by the rule of the state, and no node locked.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlaneAlignment_ComputeDeltaV_TakesTheNodeOfTheState(bool ascending)
    {
        const double a = 11480000.0;
        double i1 = 60.0 * IO.Astrodynamics.Constants.Deg2Rad, o1 = 10.0 * IO.Astrodynamics.Constants.Deg2Rad;
        double i2 = 45.0 * IO.Astrodynamics.Constants.Deg2Rad, o2 = 55.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var earth = TestHelpers.EarthAtJ2000;
        var orbit = new KeplerianElements(a, 0.0, i1, o1, 0.0, 0.0, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var target = new KeplerianElements(a, 0.0, i2, o2, 0.0, 0.0, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(Interlocked.Decrement(ref _nextId), "PLANEPREV", 1000.0, 3000.0, new Clock("PLANEPREV", 65536), orbit);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 2000.0, 1900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        var plane = new PlaneAlignmentManeuver(new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, target, spc.Engines.First());

        // The closed form: the normals, the node of the case's descending pass (u near 2.198 rad) and its opposite
        static Vector3 Normal(double i, double o) => new(System.Math.Sin(o) * System.Math.Sin(i), -System.Math.Cos(o) * System.Math.Sin(i), System.Math.Cos(i));
        var h1 = Normal(i1, o1);
        var h2 = Normal(i2, o2);
        var line = h1.Cross(h2);
        var node = line * (1.0 / line.Magnitude());
        var ascendingNodeOfTheOrbit = new Vector3(System.Math.Cos(o1), System.Math.Sin(o1), 0.0);
        double u = System.Math.Atan2(node.Dot(h1.Cross(ascendingNodeOfTheOrbit)), node.Dot(ascendingNodeOfTheOrbit));
        Assert.True(System.Math.Abs(u - 2.197937654) < 1e-6, $"{u}");
        if (ascending)
        {
            node = -node;
            u += IO.Astrodynamics.Constants.PI;
        }

        double v = System.Math.Sqrt(earth.GM / a);
        var expected = v * (h2 - h1).Cross(node);
        double theta = System.Math.Acos(h1.Dot(h2));
        Assert.True(System.Math.Abs(expected.Magnitude() - 2.0 * v * System.Math.Sin(theta / 2.0)) < 1e-9);

        var deltaV = plane.ComputeDeltaV(orbit.ToStateVector(u));

        double difference = (deltaV - expected).Magnitude();
        _output.WriteLine($"MEASURE PlaneAlignment ComputeDeltaV ascending={ascending} against the closed form: {difference:E3} m/s of {expected.Magnitude():F6} m/s");
        Assert.True(difference < PlaneChangeTolerance, $"{difference:E3} m/s");
        Assert.Null(plane.IsAscendingNode);
        Assert.Equal(CrossingDirection.Any, plane.EventCrossingDirection);
        Assert.Equal(1900.0, spc.FuelTanks.First().Quantity);
    }

    // Distance of the plane change from its closed form, m/s, of about 3.85 km/s: the rounding of the state conversion and
    // of the elements the maneuver reads from the state. Measured 1.98e-8 m/s (worst of the two nodes, Linux x64,
    // 2026-11-23); twice the worst. PROVISIONAL: a Linux-only figure, to be set from the MEASURE lines of the CI logs,
    // twice the worst of Linux, Windows and macOS arm64.
    private const double PlaneChangeTolerance = 4.0e-8;

    /// <summary>
    /// The apogee raise previewed at the periapsis of the test orbit against vis-viva (review of L5a, #668): from
    /// rp = a (1 − e), ΔV = √(μ (2/rp − 2/(rp + ra))) − √(μ (2/rp − 1/a)) along the velocity at periapsis, which for
    /// Ω = ω = 0 is (0, cos i, sin i); μ the kernel GM of the Earth (BODY399_GM), ra the target apogee radius.
    /// </summary>
    [Fact]
    public void ApogeeHeight_ComputeDeltaV_IsTheVisVivaRaise()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var apogee = (ApogeeHeightManeuver)Impulse("Apogee", earth, engine);
        const double a = 7000e3, e = 0.01, i = 0.5, ra = 8000e3;
        double rp = a * (1.0 - e);
        double expectedMagnitude = System.Math.Sqrt(earth.GM * (2.0 / rp - 2.0 / (rp + ra))) - System.Math.Sqrt(earth.GM * (2.0 / rp - 1.0 / a));
        var expected = expectedMagnitude * new Vector3(0.0, System.Math.Cos(i), System.Math.Sin(i));

        var deltaV = apogee.ComputeDeltaV(spacecraft.InitialOrbitalParameters.ToStateVector());

        double difference = (deltaV - expected).Magnitude();
        _output.WriteLine($"MEASURE ApogeeHeight ComputeDeltaV against vis-viva: {difference:E3} m/s of {expectedMagnitude:F6} m/s");
        Assert.True(difference < ApogeeRaiseTolerance, $"{difference:E3} m/s");
    }

    // Distance of the apogee raise from vis-viva, m/s, of about 229 m/s. Measured 0 (Linux x64, 2026-11-23), so the bound
    // is that of the rounding, about 30 ulps of the delta-V. PROVISIONAL: a Linux-only figure, to be set from the MEASURE
    // lines of the CI logs, twice the worst of Linux, Windows and macOS arm64 (this bound while the worst stays 0).
    private const double ApogeeRaiseTolerance = 1.0e-12;

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
    /// The attitude preview takes the state about the centre the maneuver is given, as the execution does (decision 50),
    /// then in ICRF: a nadir attitude about the Earth at the test orbit re-expressed about the Sun or the Moon, in ICRF or
    /// ECLIPJ2000, is the orientation of the execution at that state, bit for bit, and the nadir of the orbit given about the
    /// Earth, to the rounding of the change of centre.
    /// </summary>
    [Theory]
    [InlineData("Sun", "ICRF")]
    [InlineData("Sun", "ECLIPJ2000")]
    [InlineData("Moon", "ICRF")]
    public void ComputeStateOrientation_TakesTheStateAboutTheGivenCentre(string observer, string frame)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, out var engine);
        var nadir = new NadirAttitude(earth, S, TimeSpan.Zero, engine);
        spacecraft.SetManeuvers(nadir);
        var targetFrame = frame == "ICRF" ? Frames.Frame.ICRF : Frames.Frame.ECLIPTIC_J2000;
        var aboutTheEarth = spacecraft.InitialOrbitalParameters.ToStateVector().ToFrame(targetFrame);
        CelestialItem other = observer == "Sun" ? Stars.SUN_BODY : new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, S);
        var state = aboutTheEarth.RelativeTo(other, Aberration.None);

        var orientation = nadir.ComputeStateOrientation(state);

        var twin = new NadirAttitude(earth, S, TimeSpan.Zero, engine);
        var (_, executed) = twin.TryExecute(state);
        Assert.Equal(executed.Rotation, orientation.Rotation);
        Assert.Equal(Frames.Frame.ICRF, orientation.ReferenceFrame);
        double angle = Body.AttitudeRecordTests.RotationAngle(orientation.Rotation, nadir.ComputeStateOrientation(aboutTheEarth).Rotation);
        _output.WriteLine($"MEASURE ComputeStateOrientation about {observer} in {frame} against about the Earth: {angle:E3} rad");
        Assert.True(angle < 1e-9, $"{angle:E3} rad");
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
