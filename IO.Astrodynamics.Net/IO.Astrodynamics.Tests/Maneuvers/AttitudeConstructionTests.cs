// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The constructors of the attitudes, lot L5b of #668 (D1 rule 8, decisions 62, 67, 69 and 70, decided by S. Guillet,
/// 2026-11-23): every parameter after the targets optional (<c>minEpoch</c>, <c>holdDuration</c>, <c>maneuverCenter</c>,
/// then <c>minVectorSeparation</c>); the single-vector attitudes point an optional <see cref="BodyAxis"/>; TRIAD of
/// <see cref="IAttitudeTarget"/> targets in four shapes (two axes, two vectors, an instrument, two instruments), its
/// targets never null, its factories <c>CreateLvlh</c> and <c>CreateProgradeWithSunTracking</c> the axis shape;
/// <see cref="InstrumentPointingToAttitude"/> of an <see cref="IAttitudeTarget"/>, which carries the correction.
/// </summary>
public class AttitudeConstructionTests
{
    private readonly ITestOutputHelper _output;

    public AttitudeConstructionTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    private static Spacecraft CustomAxes(int id)
    {
        var orbit = new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, S, Frames.Frame.ICRF);
        return new Spacecraft(id, "AC" + -id, 1000.0, orbit, bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(),
            bodyUp: Vector3.VectorZ);
    }

    [Fact]
    public void ASingleVectorAttitude_OfNoArgument_IsTheFrontOfNoMinimumHoldOrCentre()
    {
        Attitude[] attitudes = [new NadirAttitude(), new ZenithAttitude(), new ProgradeAttitude(), new RetrogradeAttitude(), new NormalAttitude(), new AntiNormalAttitude()];
        foreach (var attitude in attitudes)
        {
            Assert.True(attitude.MinEpoch.EqualsExact(default(Time)));
            Assert.Equal(TimeSpan.Zero, attitude.HoldDuration);
            Assert.Null(attitude.ManeuverCenter);
            Assert.Equal(BodyAxis.Front, (BodyAxis)attitude.GetType().GetProperty("BodyAxis")!.GetValue(attitude)!);
        }
    }

    [Theory]
    [InlineData(BodyAxis.Front)]
    [InlineData(BodyAxis.Back)]
    [InlineData(BodyAxis.Right)]
    [InlineData(BodyAxis.Left)]
    [InlineData(BodyAxis.Up)]
    [InlineData(BodyAxis.Down)]
    public void ASingleVectorAttitude_PointsTheAxisItIsGiven_OnTheSpacecraftOfThePlan(BodyAxis axis)
    {
        var spc = CustomAxes(-899950 - (int)axis);
        var nadir = new NadirAttitude(S, maneuverCenter: TestHelpers.EarthAtJ2000, bodyAxis: axis);
        spc.SetManeuvers(nadir);
        var state = spc.InitialOrbitalParameters.ToStateVector();

        var q = nadir.ComputeStateOrientation(state).Rotation;
        var axisVector = axis switch
        {
            BodyAxis.Front => spc.BodyFront, BodyAxis.Back => spc.BodyBack, BodyAxis.Right => spc.BodyRight,
            BodyAxis.Left => spc.BodyLeft, BodyAxis.Up => spc.BodyUp, _ => spc.BodyDown
        };
        var pointed = axisVector.Rotate(q);
        var nadirDirection = state.Position.Inverse();
        var angle = System.Math.Atan2(pointed.Cross(nadirDirection).Magnitude(), pointed.Dot(nadirDirection));
        _output.WriteLine($"MEASURE AttitudeConstruction nadir {axis}: axis to nadir {angle:E3} rad");
        Assert.True(angle < 1e-15, $"{angle:E3} rad");
        Assert.Equal(axis, nadir.BodyAxis);
    }

    [Fact]
    public void AnUndefinedAxis_Throws()
    {
        Assert.Equal("bodyAxis", Assert.Throws<ArgumentOutOfRangeException>(() => new ProgradeAttitude(bodyAxis: (BodyAxis)9)).ParamName);
        Assert.Equal("primaryBodyAxis", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TriadAttitude((BodyAxis)9, OrbitalDirectionTarget.Nadir, BodyAxis.Front, OrbitalDirectionTarget.Prograde)).ParamName);
        Assert.Equal("secondaryBodyAxis", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TriadAttitude(BodyAxis.Down, OrbitalDirectionTarget.Nadir, (BodyAxis)(-1), OrbitalDirectionTarget.Prograde)).ParamName);
    }

    [Theory]
    [InlineData(BodyAxis.Front, BodyAxis.Front)]
    [InlineData(BodyAxis.Front, BodyAxis.Back)]
    [InlineData(BodyAxis.Up, BodyAxis.Down)]
    public void ATriadOfTheSameOrOppositeAxes_Throws(BodyAxis primary, BodyAxis secondary)
    {
        Assert.Throws<ArgumentException>(() => new TriadAttitude(primary, OrbitalDirectionTarget.Nadir, secondary, OrbitalDirectionTarget.Prograde));
    }

    [Fact]
    public void TheAxisTriad_ReadsTheAxesOfThePlansSpacecraft_TheFactoriesAreIt()
    {
        var spc = CustomAxes(-899960);
        var triad = new TriadAttitude(BodyAxis.Down, OrbitalDirectionTarget.Nadir, BodyAxis.Front, OrbitalDirectionTarget.Prograde, S,
            maneuverCenter: TestHelpers.EarthAtJ2000);
        var lvlh = TriadAttitude.CreateLvlh(S, maneuverCenter: TestHelpers.EarthAtJ2000);
        Assert.Equal(Spacecraft.Down, triad.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Front, lvlh.SecondaryBodyVector);

        spc.SetManeuvers(triad, lvlh);
        Assert.Equal(spc.BodyDown, triad.PrimaryBodyVector);
        Assert.Equal(spc.BodyFront, triad.SecondaryBodyVector);
        var state = spc.InitialOrbitalParameters.ToStateVector();
        Assert.Equal(triad.ComputeStateOrientation(state).Rotation, lvlh.ComputeStateOrientation(state).Rotation);
        Assert.Same(OrbitalDirectionTarget.Nadir, lvlh.PrimaryTarget);
        Assert.Same(OrbitalDirectionTarget.Prograde, lvlh.SecondaryTarget);

        var theSun = TestHelpers.Sun;
        var sun = TriadAttitude.CreateProgradeWithSunTracking(theSun);
        var target = Assert.IsType<CelestialAttitudeTarget>(sun.SecondaryTarget);
        Assert.Same(theSun, target.Target);
        Assert.Equal(Aberration.None, target.Aberration);
        Assert.Equal("sun", Assert.Throws<ArgumentNullException>(() => TriadAttitude.CreateProgradeWithSunTracking(null)).ParamName);
    }

    [Fact]
    public void TheSurface_OfTheAttitudes()
    {
        var triad = typeof(TriadAttitude);
        Assert.Null(triad.GetProperty("PrimaryAttitudeTarget"));
        Assert.Null(triad.GetProperty("SecondaryAttitudeTarget"));
        Assert.Null(triad.GetProperty("MinimumVectorSeparation"));
        Assert.NotNull(triad.GetProperty("MinVectorSeparation"));
        Assert.Equal(typeof(IAttitudeTarget), triad.GetProperty("PrimaryTarget")!.PropertyType);
        Assert.Equal(typeof(IAttitudeTarget), triad.GetProperty("SecondaryTarget")!.PropertyType);
        Assert.Null(triad.GetMethod("CreateLVLH"));
        Assert.NotNull(triad.GetMethod("CreateLvlh"));
        Assert.DoesNotContain(triad.GetConstructors(), c => c.GetParameters().Any(p => p.ParameterType == typeof(ILocalizable)));
        Assert.Equal(4, triad.GetConstructors().Length);
        foreach (var constructor in triad.GetConstructors())
        {
            var names = constructor.GetParameters().Select(p => p.Name).TakeLast(4).ToArray();
            Assert.Equal(["minEpoch", "holdDuration", "maneuverCenter", "minVectorSeparation"], names);
        }

        var pointing = typeof(InstrumentPointingToAttitude);
        Assert.Null(pointing.GetProperty("Aberration"));
        Assert.Equal(typeof(IAttitudeTarget), pointing.GetProperty("Target")!.PropertyType);
        Assert.Equal(["instrument", "target", "minEpoch", "holdDuration", "maneuverCenter"],
            Assert.Single(pointing.GetConstructors()).GetParameters().Select(p => p.Name));
        Assert.Equal(["minEpoch", "holdDuration", "maneuverCenter", "bodyAxis"],
            Assert.Single(typeof(NadirAttitude).GetConstructors()).GetParameters().Select(p => p.Name));
        Assert.Equal("minVectorSeparation", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TriadAttitude(BodyAxis.Down, OrbitalDirectionTarget.Nadir, BodyAxis.Front, OrbitalDirectionTarget.Prograde, minVectorSeparation: 0.0)).ParamName);
        Assert.Equal("primaryTarget", Assert.Throws<ArgumentNullException>(() =>
            new TriadAttitude(BodyAxis.Down, null, BodyAxis.Front, OrbitalDirectionTarget.Prograde)).ParamName);
        Assert.Equal("target", Assert.Throws<ArgumentNullException>(() =>
            new InstrumentPointingToAttitude(TestHelpersInstrument(), null)).ParamName);
    }

    private static Instrument TestHelpersInstrument()
    {
        var spc = CustomAxes(-899970);
        return spc.AddCircularInstrument("CAM", 0.1, Vector3.VectorZ, Vector3.VectorY);
    }

    /// <summary>
    /// The pointing direction is its target's (<see cref="IAttitudeTarget.GetDirection"/>): geometric by default, with the
    /// light time when the target asks for it (the attitude's own LT default is gone, decision 67 of #668), and an orbital
    /// direction may be a target.
    /// </summary>
    [Fact]
    public void APointing_TakesTheCorrectionOfItsTarget()
    {
        var spc = CustomAxes(-899980);
        var camera = spc.AddCircularInstrument("CAM", 0.1, Vector3.VectorZ, Vector3.VectorY);
        var state = spc.InitialOrbitalParameters.ToStateVector();
        var moon = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MOON);
        foreach (var target in new IAttitudeTarget[] { new CelestialAttitudeTarget(moon), new CelestialAttitudeTarget(moon, Aberration.LT), OrbitalDirectionTarget.Prograde })
        {
            var pointing = new InstrumentPointingToAttitude(camera, target, maneuverCenter: TestHelpers.EarthAtJ2000);
            var boresight = camera.GetBoresightInSpacecraftFrame().Rotate(pointing.ComputeOrientation(state));
            var direction = target.GetDirection(state);
            var angle = System.Math.Atan2(boresight.Cross(direction).Magnitude(), boresight.Dot(direction));
            _output.WriteLine($"MEASURE AttitudeConstruction pointing {(target is CelestialAttitudeTarget c ? "Moon " + c.Aberration : "prograde")}: boresight to target {angle:E3} rad");
            Assert.True(angle < 1e-15, $"{angle:E3} rad");
        }
    }
}
