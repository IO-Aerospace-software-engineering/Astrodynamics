// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The engine and the centre a run gives each maneuver, lot L5b of #668 (decisions 48, 61, 63, 64 and 93, decided by
/// S. Guillet, 2026-11-23, and decision 99, decided by S. Guillet, 2026-11-24): <see cref="ImpulseManeuver.Engine"/> is on
/// the impulse; a maneuver given no engine takes the only engine of the spacecraft and one given no centre the observer of
/// its initial orbit when it is a celestial body other than a star; where the centre cannot be inferred (SSB mode), the
/// library's <see cref="TriadAttitude"/> or <see cref="InstrumentPointingToAttitude"/> whose targets are all library
/// <see cref="CelestialAttitudeTarget"/>s takes the central body of the run and any other law (a user subclass of either)
/// throws <see cref="InvalidOperationException"/> from the propagator constructor and from <c>Propagate</c>. The resolved
/// values are written right after the re-arm, restored by the next re-arm, and every check before the write and every
/// preview reads the given values (IC-1 of the gap analysis of L5b).
/// </summary>
/// <remarks>
/// The case: a 7 000 km orbit, e = 0.01, i = 0.5 rad, about a point-mass Earth from 2024-03-01 12:00 TDB, over 2 h with
/// outputs every 60 s; in SSB mode the same orbit about the solar system barycentre, with the Earth and the Sun as third
/// bodies. 1 000 kg dry, a tank of 1 000 kg, an engine of Isp 300 s at 10 kg/s.
/// </remarks>
public class ManeuverInferenceTests
{
    private readonly ITestOutputHelper _output;

    public ManeuverInferenceTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly Window TwoHours = new(S, S.AddHours(2.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static int _nextId = -899000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth() => new(PlanetsAndMoons.Earth, Frame.ICRF, S);

    private static IO.Astrodynamics.OrbitalParameters.OrbitalParameters Orbit(CelestialBody earth, bool ssb)
    {
        var icrf = new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frame.ICRF);
        if (!ssb) return icrf;
        var sv = icrf.ToStateVector();
        var fromSsb = earth.GetGeometricStateRelativeTo(S, Barycenters.SolarSystemBarycenter);
        return new StateVector(fromSsb.Position + sv.Position, fromSsb.Velocity + sv.Velocity, Barycenters.SolarSystemBarycenter, S,
            Frame.ICRF);
    }

    private static Spacecraft NewSpacecraft(CelestialBody earth, bool ssb = false, int engines = 1)
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "MI" + -id, 1000.0, Orbit(earth, ssb));
        for (int i = 0; i < engines; i++)
        {
            var tank = spacecraft.AddFuelTank(new FuelTank("tank" + i, 1000.0, 1000.0));
            spacecraft.AddEngine(new Engine("engine" + i, 300.0, 10.0, tank));
        }

        return spacecraft;
    }

    private static PropagationOptions Options(CelestialBody earth, string integrator, bool ssb = false) => new()
    {
        Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
        OutputStep = Output,
        ThirdBodies = ssb ? [earth, new CelestialBody(Stars.Sun, Frame.ICRF, S)] : [earth],
        CovariancePropagation = CovariancePropagationOptions.None
    };

    // A user target: the direction of the central body's pole as the state's observer reads it, which the library cannot
    // tell from a celestial one
    private sealed class UserTarget : IAttitudeTarget
    {
        public string Name => "user";
        public Vector3 GetDirection(StateVector observerState) => observerState.Position.Cross(observerState.Velocity).Normalize();
    }

    // A user subclass of the library's celestial target, which the library cannot tell from a user target either
    private sealed class DerivedCelestialTarget : CelestialAttitudeTarget
    {
        public DerivedCelestialTarget(ILocalizable target) : base(target)
        {
        }
    }

    // A user attitude law: its front along the position from its centre
    private sealed class UserAttitude : Attitude
    {
        public UserAttitude(CelestialItem maneuverCenter) : base(S, TimeSpan.Zero, maneuverCenter)
        {
        }

        public override Quaternion ComputeOrientation(StateVector stateVector) =>
            GetBodyAxis(BodyAxis.Front).To(stateVector.Position);
    }

    // A user subclass of the library's TRIAD law, which replaces the law: the library cannot know whether it reads its
    // centre, whatever its targets
    private sealed class UserTriad : TriadAttitude
    {
        public UserTriad(CelestialItem maneuverCenter) : base(BodyAxis.Front,
            new CelestialAttitudeTarget(new CelestialBody(PlanetsAndMoons.Moon)),
            BodyAxis.Up,
            new CelestialAttitudeTarget(new CelestialBody(Stars.Sun, Frame.ICRF, S)),
            S,
            TimeSpan.Zero,
            maneuverCenter)
        {
        }

        public override Quaternion ComputeOrientation(StateVector stateVector) =>
            GetBodyAxis(BodyAxis.Front).To(stateVector.Position);
    }

    // A user subclass of the library's pointing law, the same
    private sealed class UserPointing : InstrumentPointingToAttitude
    {
        public UserPointing(Instrument instrument, CelestialItem maneuverCenter) : base(instrument,
            new CelestialAttitudeTarget(new CelestialBody(PlanetsAndMoons.Moon)), S, TimeSpan.Zero, maneuverCenter)
        {
        }

        public override Quaternion ComputeOrientation(StateVector stateVector) =>
            Instrument.GetBoresightInSpacecraftFrame().To(stateVector.Position);
    }

    // The largest angle between the axes two rotations give (the sign of a quaternion is free)
    // Measured by the CI of #820 at 5d97c750 (2026-11-25), identical on Linux x64, Windows x64 and macOS arm64, each threshold twice the worst rounded up at the second digit:
    // - the front along the velocity of a plan of attitudes only, read on the attitude record at an output epoch,
    //   3.004e-11 rad (RK7(8)) and 3.005e-11 rad (Velocity-Verlet);
    // - the SSB pointing, the boresight to the Moon, 2.058e-16 rad on both integrators;
    // - the SSB user attitude, the front to the position from the Earth, 1.684e-16 rad (RK7(8)), 6.603e-17 rad
    //   (Velocity-Verlet);
    // - the SSB TRIAD, the recorded attitude to the law, 0 rad on both, held at the pointing's bound, the rounding of one
    //   direction.
    private const double AttitudesOnlyThreshold = 6.1e-11;
    private const double SsbPointingThreshold = 4.2e-16;
    private const double SsbUserAttitudeThreshold = 3.4e-16;
    private const double SsbTriadThreshold = SsbPointingThreshold;

    private static double RotationAngle(Quaternion a, Quaternion b) =>
        new[] { Vector3.VectorX, Vector3.VectorY, Vector3.VectorZ }.Max(v => Separation(v.Rotate(a), v.Rotate(b)));

    // The angle between two vectors, atan2(|a × b|, a · b), free of the resolution of acos near 0 (2.1e-8 rad)
    private static double Separation(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a.Dot(b));

    // ---------------------------------------------------------------------------------------------------------------------
    // The engine moves to ImpulseManeuver
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Engine_IsOnImpulseManeuver_NotOnManeuverNorAttitude()
    {
        Assert.Null(typeof(Maneuver.Maneuver).GetProperty("Engine"));
        Assert.Null(typeof(Attitude).GetProperty("Engine"));
        var engine = typeof(ImpulseManeuver).GetProperty("Engine");
        Assert.NotNull(engine);
        Assert.Equal(typeof(Engine), engine.PropertyType);
        Assert.False(engine.SetMethod?.IsPublic ?? false);
        Assert.DoesNotContain(typeof(Attitude).GetConstructors(), c => c.GetParameters().Any(p => p.ParameterType == typeof(Engine)));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Inference, write and restore (decisions 61 and 63)
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void NullBeforeARun_ResolvedAfterIt_AndTheRunIsThatOfTheExplicitValues(string integrator)
    {
        var earth = Earth();
        var inferred = NewSpacecraft(earth);
        var nadir = new NadirAttitude(S, TimeSpan.FromSeconds(600.0));
        var raise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        inferred.SetManeuvers(nadir, raise);
        Assert.Null(nadir.ManeuverCenter);
        Assert.Null(raise.ManeuverCenter);
        Assert.Null(raise.Engine);

        var solution = inferred.Propagate(TwoHours, Options(earth, integrator));

        Assert.Same(earth, nadir.ManeuverCenter);
        Assert.Same(earth, raise.ManeuverCenter);
        Assert.Same(inferred.Engines.Single(), raise.Engine);
        Assert.NotNull(raise.ThrustWindow);

        // The same plan given its engine and centre: the same run, bit for bit
        var explicitSc = NewSpacecraft(earth);
        explicitSc.SetManeuvers(new NadirAttitude(S, TimeSpan.FromSeconds(600.0), earth),
            new ApogeeHeightManeuver(8000e3, explicitSc.Engines.Single(), S, TimeSpan.Zero, earth));
        var reference = explicitSc.Propagate(TwoHours, Options(earth, integrator));
        var end = TwoHours.EndDate;
        Assert.Equal(reference.GetStateVector(end).Position, solution.GetStateVector(end).Position);
        Assert.Equal(reference.GetStateVector(end).Velocity, solution.GetStateVector(end).Velocity);
        Assert.Equal(explicitSc.GetRemainingFuel(), inferred.GetRemainingFuel());
        var q = inferred.Frame.GetStateOrientationToIcrf(S.AddSeconds(300.0)).Rotation;
        var qRef = explicitSc.Frame.GetStateOrientationToIcrf(S.AddSeconds(300.0)).Rotation;
        Assert.Equal(qRef, q);
    }

    [Fact]
    public void TheGivenValues_AreKept_AndTheReArmRestoresTheGivenOnes()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth);
        var given = spacecraft.Engines.Single();
        var explicitRaise = new ApogeeHeightManeuver(8000e3, given, S, TimeSpan.Zero, earth);
        var inferredRaise = new PerigeeHeightManeuver(7100e3, null, S, TimeSpan.Zero, null);
        spacecraft.SetManeuvers(explicitRaise, inferredRaise);
        spacecraft.Propagate(TwoHours, Options(earth, "RK"));
        Assert.Same(given, explicitRaise.Engine);
        Assert.Same(earth, explicitRaise.ManeuverCenter);
        Assert.Same(given, inferredRaise.Engine);
        Assert.Same(earth, inferredRaise.ManeuverCenter);

        // The re-arm restores the given values (null for the inferred ones)
        Maneuver.Maneuver.ResetChain(spacecraft.Maneuvers[0]);
        Assert.Same(given, explicitRaise.Engine);
        Assert.Same(earth, explicitRaise.ManeuverCenter);
        Assert.Null(inferredRaise.Engine);
        Assert.Null(inferredRaise.ManeuverCenter);

        // And the next run writes the resolved ones again
        spacecraft.Propagate(TwoHours, Options(earth, "RK"));
        Assert.Same(given, inferredRaise.Engine);
        Assert.Same(earth, inferredRaise.ManeuverCenter);
    }

    [Fact]
    public void ACancelledToken_OrAnInitializeThatThrows_ChangesNothing()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth);
        var raise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        spacecraft.SetManeuvers(raise);
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, Options(earth, "RK"));

        // Before any run: a cancelled token leaves them null
        Assert.Throws<OperationCanceledException>(() => propagator.Propagate(new CancellationToken(true)));
        Assert.Null(raise.Engine);
        Assert.Null(raise.ManeuverCenter);

        // After a run: a cancelled token leaves the last run's values
        propagator.Propagate();
        Assert.Same(spacecraft.Engines.Single(), raise.Engine);
        Assert.Throws<OperationCanceledException>(() => propagator.Propagate(new CancellationToken(true)));
        Assert.Same(spacecraft.Engines.Single(), raise.Engine);
        Assert.Same(earth, raise.ManeuverCenter);

        // An Initialize that throws, before any run: they stay null
        var other = NewSpacecraft(earth);
        var otherRaise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        other.SetManeuvers(otherRaise);
        var throwing = new ThrowingIntegrator();
        var throwingPropagator = new CentralBodyPropagator(TwoHours, other, throwing, Output);
        throwing.Throw = true;
        Assert.Throws<InvalidOperationException>(() => throwingPropagator.Propagate());
        Assert.Null(otherRaise.Engine);
        Assert.Null(otherRaise.ManeuverCenter);
    }

    private sealed class ThrowingIntegrator : IIntegrator
    {
        internal bool Throw { get; set; }

        public void Initialize(StateVector initialState)
        {
            if (Throw) throw new InvalidOperationException("Initialize refused by the test.");
        }

        public IntegrationResult IntegrateSegment(Vector3 startPosition, Vector3 startVelocity, Time baseEpoch, double duration,
            IReadOnlyList<IEventDetector> eventDetectors = null) => throw new NotSupportedException();
    }

    [Fact]
    public void ASecondEngineAddedBetweenRuns_MakesRunTwoThrow()
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth);
        var raise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        spacecraft.SetManeuvers(raise);
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, Options(earth, "RK"));
        propagator.Propagate();
        var first = raise.Engine;

        var tank = spacecraft.AddFuelTank(new FuelTank("second tank", 100.0, 100.0));
        spacecraft.AddEngine(new Engine("second engine", 200.0, 1.0, tank));

        var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.Contains("engine0", exception.Message);
        Assert.Contains("second engine", exception.Message);
        Assert.Contains("engine:", exception.Message);
        Assert.Same(first, raise.Engine);
        Assert.Throws<InvalidOperationException>(() => spacecraft.Propagate(TwoHours, Options(earth, "RK")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void AnImpulseGivenNoEngine_OnASpacecraftOfNoEngineOrOfSeveral_Throws(int engines)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, engines: engines);
        spacecraft.SetManeuvers(new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, earth));

        var constructor = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(TwoHours, spacecraft, Options(earth, "RK")));
        Assert.Contains("engine:", constructor.Message);
        Assert.Contains(spacecraft.Name, constructor.Message);
        Assert.False(spacecraft.IsPropagated);
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void APlanOfAttitudesOnly_RunsOnASpacecraftWithoutTankOrEngine(string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, engines: 0);
        Assert.Empty(spacecraft.FuelTanks);
        var nadir = new NadirAttitude(S, TimeSpan.FromMinutes(30.0));
        var prograde = new ProgradeAttitude(S, TimeSpan.Zero);
        spacecraft.SetManeuvers(nadir, prograde);

        spacecraft.Propagate(TwoHours, Options(earth, integrator));

        Assert.Equal([nadir, prograde], spacecraft.ExecutedManeuvers);
        var state = spacecraft.GetEphemeris(S.AddHours(1.0), earth, Frame.ICRF, Aberration.None);
        var front = spacecraft.BodyFront.Rotate(spacecraft.Frame.GetStateOrientationToIcrf(S.AddHours(1.0)).Rotation);
        double angle = Separation(front, state.Velocity);
        _output.WriteLine($"MEASURE ManeuverInference attitudes only {integrator}: front to velocity {angle:E3} rad");
        Assert.True(angle < AttitudesOnlyThreshold, $"{angle:E3} rad");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // IC-1: the checks before the write and the previews read the given values
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AReleasedManeuver_AfterARunThatInferredItsEngine_GoesToAnotherSpacecraft()
    {
        var earth = Earth();
        var first = NewSpacecraft(earth);
        var raise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        first.SetManeuvers(raise);
        first.Propagate(TwoHours, Options(earth, "RK"));
        Assert.Same(first.Engines.Single(), raise.Engine);
        first.SetManeuvers();

        var second = NewSpacecraft(earth);
        second.SetManeuvers(raise);
        second.Propagate(TwoHours, Options(earth, "RK"));

        Assert.Same(second.Engines.Single(), raise.Engine);
        Assert.True(second.GetRemainingFuel() < 1000.0);
    }

    [Fact]
    public void ThePreviewOfAnInferredCentre_IsTheSameBeforeAndAfterARun()
    {
        var earth = Earth();
        var sun = new CelestialBody(Stars.Sun, Frame.ICRF, S);
        var spacecraft = NewSpacecraft(earth);
        var nadir = new NadirAttitude(S, TimeSpan.Zero);
        var raise = new ApogeeHeightManeuver(8000e3, null, S, TimeSpan.Zero, null);
        spacecraft.SetManeuvers(nadir, raise);
        var heliocentric = spacecraft.InitialOrbitalParameters.ToStateVector().RelativeTo(sun, Aberration.None);

        var orientationBefore = nadir.ComputeStateOrientation(heliocentric).Rotation;
        var deltaVBefore = raise.ComputeDeltaV(heliocentric);
        spacecraft.Propagate(TwoHours, Options(earth, "RK"));
        Assert.Same(earth, nadir.ManeuverCenter);

        Assert.Equal(orientationBefore, nadir.ComputeStateOrientation(heliocentric).Rotation);
        Assert.Equal(deltaVBefore, raise.ComputeDeltaV(heliocentric));
        // The run's centre would have given another preview
        var aboutEarth = new NadirAttitude(S, TimeSpan.Zero, earth).ComputeStateOrientation(heliocentric).Rotation;
        Assert.True(RotationAngle(orientationBefore, aboutEarth) > 1e-3);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 99: where the centre cannot be inferred (SSB mode)
    // ---------------------------------------------------------------------------------------------------------------------

    // The pointing of an instrument of the spacecraft of the plan
    private static Instrument Camera(Spacecraft spacecraft) =>
        spacecraft.Instruments.FirstOrDefault() ?? spacecraft.AddCircularInstrument("CAM", 0.1, Vector3.VectorZ, Vector3.VectorY);

    private static Attitude LawReadingTheCentre(string law, CelestialItem center, Spacecraft spacecraft) => law switch
    {
        "PointingAtAnOrbitalDirection" => new InstrumentPointingToAttitude(Camera(spacecraft), OrbitalDirectionTarget.Prograde, S,
            maneuverCenter: center),
        "UserTarget" => new TriadAttitude(Spacecraft.Front,
            new UserTarget(),
            Spacecraft.Up,
            new CelestialAttitudeTarget(new CelestialBody(Stars.Sun, Frame.ICRF, S)),
            S,
            TimeSpan.Zero,
            center),
        "DerivedCelestialTarget" => new TriadAttitude(Spacecraft.Front,
            new DerivedCelestialTarget(new CelestialBody(PlanetsAndMoons.Moon)),
            Spacecraft.Up,
            new CelestialAttitudeTarget(new CelestialBody(Stars.Sun, Frame.ICRF, S)),
            S,
            TimeSpan.Zero,
            center),
        "UserAttitude" => new UserAttitude(center),
        "UserTriadSubclass" => new UserTriad(center),
        "UserPointingSubclass" => new UserPointing(Camera(spacecraft), center),
        "OrbitalDirection" => new TriadAttitude(Spacecraft.Down,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            S,
            TimeSpan.Zero,
            center),
        "Nadir" => new NadirAttitude(S, TimeSpan.Zero, center),
        _ => throw new ArgumentOutOfRangeException(nameof(law))
    };

    [Theory]
    [InlineData("UserTarget", "RK")]
    [InlineData("UserTarget", "VV")]
    [InlineData("DerivedCelestialTarget", "RK")]
    [InlineData("UserAttitude", "RK")]
    [InlineData("UserAttitude", "VV")]
    [InlineData("UserTriadSubclass", "RK")]
    [InlineData("UserTriadSubclass", "VV")]
    [InlineData("UserPointingSubclass", "RK")]
    [InlineData("UserPointingSubclass", "VV")]
    [InlineData("OrbitalDirection", "RK")]
    [InlineData("Nadir", "VV")]
    [InlineData("PointingAtAnOrbitalDirection", "RK")]
    public void SsbMode_ALawThatReadsItsCentre_GivenNone_ThrowsInTheConstructorAndInPropagate(string law, string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, ssb: true);
        spacecraft.SetManeuvers(LawReadingTheCentre(law, null, spacecraft));

        var constructor = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(TwoHours, spacecraft, Options(earth, integrator, true)));
        Assert.Contains("maneuverCenter", constructor.Message);
        Assert.Contains(spacecraft.Name, constructor.Message);

        // A propagator built before the plan: its Propagate throws, nothing changed
        var other = NewSpacecraft(earth, ssb: true);
        var propagator = new CentralBodyPropagator(TwoHours, other, Options(earth, integrator, true));
        other.SetManeuvers(LawReadingTheCentre(law, null, other));
        var run = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.Contains("maneuverCenter", run.Message);
        Assert.False(other.IsPropagated);
        Assert.Empty(other.ExecutedManeuvers);
    }

    [Theory]
    [InlineData("UserTarget", "RK")]
    [InlineData("UserTarget", "VV")]
    [InlineData("UserAttitude", "RK")]
    [InlineData("UserAttitude", "VV")]
    [InlineData("PointingAtAnOrbitalDirection", "VV")]
    public void SsbMode_ALawThatReadsItsCentre_GivenIt_Runs(string law, string integrator)
    {
        var earth = Earth();
        var spacecraft = NewSpacecraft(earth, ssb: true);
        var attitude = LawReadingTheCentre(law, earth, spacecraft);
        spacecraft.SetManeuvers(attitude);

        spacecraft.Propagate(TwoHours, Options(earth, integrator, true));

        Assert.Equal([attitude], spacecraft.ExecutedManeuvers);
        Assert.Same(earth, attitude.ManeuverCenter);
        if (law == "UserAttitude")
        {
            // Its front along the position from the Earth, the centre it was given
            var epoch = S.AddHours(1.0);
            var fromEarth = spacecraft.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None).Position;
            var front = spacecraft.BodyFront.Rotate(spacecraft.Frame.GetStateOrientationToIcrf(epoch).Rotation);
            var angle = Separation(front, fromEarth);
            _output.WriteLine($"MEASURE ManeuverInference SSB user attitude {integrator}: front to position from the Earth {angle:E3} rad");
            Assert.True(angle < SsbUserAttitudeThreshold, $"{angle:E3} rad");
        }
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void SsbMode_ATriadOfCelestialTargetsOnly_GivenNoCentre_TakesTheCentralBodyOfTheRun(string integrator)
    {
        var earth = Earth();
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var sun = new CelestialBody(Stars.Sun, Frame.ICRF, S);
        var spacecraft = NewSpacecraft(earth, ssb: true);
        var triad = new TriadAttitude(Spacecraft.Front,
            new CelestialAttitudeTarget(moon),
            Spacecraft.Up,
            new CelestialAttitudeTarget(sun),
            S,
            TimeSpan.Zero);
        spacecraft.SetManeuvers(triad);

        spacecraft.Propagate(TwoHours, Options(earth, integrator, true));

        Assert.Equal([triad], spacecraft.ExecutedManeuvers);
        Assert.Equal(Stars.Sun.NaifId, ((CelestialItem)triad.ManeuverCenter).NaifId);
        var epoch = S.AddHours(1.0);
        var state = spacecraft.GetEphemeris(epoch, sun, Frame.ICRF, Aberration.None);
        var expected = triad.ComputeOrientation(state);
        var recorded = spacecraft.Frame.GetStateOrientationToIcrf(epoch).Rotation;
        var angle = RotationAngle(expected, recorded);
        _output.WriteLine($"MEASURE ManeuverInference SSB TRIAD {integrator}: recorded to law {angle:E3} rad");
        Assert.True(angle < SsbTriadThreshold, $"{angle:E3} rad");
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void SsbMode_APointingAtACelestialTarget_GivenNoCentre_TakesTheCentralBodyOfTheRun(string integrator)
    {
        var earth = Earth();
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var spacecraft = NewSpacecraft(earth, ssb: true);
        var pointing = new InstrumentPointingToAttitude(Camera(spacecraft), new CelestialAttitudeTarget(moon), S);
        spacecraft.SetManeuvers(pointing);

        spacecraft.Propagate(TwoHours, Options(earth, integrator, true));

        Assert.Equal([pointing], spacecraft.ExecutedManeuvers);
        Assert.Equal(Stars.Sun.NaifId, ((CelestialItem)pointing.ManeuverCenter).NaifId);
        var epoch = S.AddHours(1.0);
        var boresight = Camera(spacecraft).GetBoresightInSpacecraftFrame().Rotate(spacecraft.Frame.GetStateOrientationToIcrf(epoch).Rotation);
        var toMoon = moon.GetEphemeris(epoch, spacecraft, Frame.ICRF, Aberration.None).Position;
        var angle = Separation(boresight, toMoon);
        _output.WriteLine($"MEASURE ManeuverInference SSB pointing {integrator}: boresight to the Moon {angle:E3} rad");
        Assert.True(angle < SsbPointingThreshold, $"{angle:E3} rad");
    }
}
