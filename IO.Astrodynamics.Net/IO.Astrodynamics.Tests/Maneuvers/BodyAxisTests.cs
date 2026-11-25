using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

/// <summary>
/// The body axes of a maneuver law (decisions 48, 69 and 93 of #668, decided by S. Guillet, 2026-11-23): the protected
/// <c>GetBodyAxis(BodyAxis)</c> reads the instance axes of the spacecraft whose plan last held the maneuver, the static
/// axes before any plan, and keeps them when the plan releases it (decision 46: "the axes stay those of the last plan's
/// spacecraft").
/// </summary>
public class BodyAxisTests
{
    public BodyAxisTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    // A user law that exposes the protected axis reader
    private sealed class AxisProbe : Attitude
    {
        public AxisProbe() : base(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, TestHelpers.EarthAtJ2000)
        {
        }

        public Vector3 Axis(BodyAxis axis) => GetBodyAxis(axis);

        public override Quaternion ComputeOrientation(StateVector stateVector) => Quaternion.Identity;
    }

    private static Spacecraft CustomAxesSpacecraft(int naifId, string name, Vector3 front, Vector3 right, Vector3 up)
    {
        var orbit = new KeplerianElements(7000000.0, 0.0, 0.5, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(naifId, name, 1000.0, orbit, bodyFront: front, bodyRight: right, bodyUp: up);
        var tank = spacecraft.AddFuelTank(new FuelTank("ft", 1000.0, 900.0));
        spacecraft.AddEngine(new Engine("eng", 450.0, 50.0, tank));
        return spacecraft;
    }

    private static Engine FreeEngine() => new Engine("free", 450.0, 50.0, new FuelTank("freeTank", 1000.0, 900.0));

    [Fact]
    public void TheSixAxes()
    {
        Assert.Equal(new[] { "Front", "Back", "Right", "Left", "Up", "Down" }, Enum.GetNames<BodyAxis>());
    }

    [Fact]
    public void GetBodyFront_IsGone_GetBodyAxis_IsProtected()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.Null(typeof(Maneuver.Maneuver).GetMethod("GetBodyFront", flags));
        var methods = typeof(Maneuver.Maneuver).GetMethods(flags).Where(m => m.Name == "GetBodyAxis").ToArray();
        var method = Assert.Single(methods);
        Assert.True(method.IsFamily, "GetBodyAxis is protected");
        Assert.Equal(typeof(BodyAxis), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(Vector3), method.ReturnType);
    }

    [Fact]
    public void BeforeAnyPlan_TheStaticAxes()
    {
        // An engine on a spacecraft of custom axes: before any plan, the static axes all the same (before lot L5b, the
        // axes of the engine's spacecraft)
        var spc = CustomAxesSpacecraft(-6900, "AxisNoPlanSc", Vector3.VectorX, Vector3.VectorY.Inverse(), Vector3.VectorZ);
        var probe = new AxisProbe();
        Assert.Equal(Spacecraft.Front, probe.Axis(BodyAxis.Front));
        Assert.Equal(Spacecraft.Back, probe.Axis(BodyAxis.Back));
        Assert.Equal(Spacecraft.Right, probe.Axis(BodyAxis.Right));
        Assert.Equal(Spacecraft.Left, probe.Axis(BodyAxis.Left));
        Assert.Equal(Spacecraft.Up, probe.Axis(BodyAxis.Up));
        Assert.Equal(Spacecraft.Down, probe.Axis(BodyAxis.Down));
    }

    [Fact]
    public void InAPlan_TheInstanceAxesOfItsSpacecraft()
    {
        var spc = CustomAxesSpacecraft(-6901, "AxisPlanSc", Vector3.VectorX, Vector3.VectorY.Inverse(), Vector3.VectorZ);
        var probe = new AxisProbe();
        spc.SetManeuvers(probe);

        Assert.Equal(spc.BodyFront, probe.Axis(BodyAxis.Front));
        Assert.Equal(spc.BodyBack, probe.Axis(BodyAxis.Back));
        Assert.Equal(spc.BodyRight, probe.Axis(BodyAxis.Right));
        Assert.Equal(spc.BodyLeft, probe.Axis(BodyAxis.Left));
        Assert.Equal(spc.BodyUp, probe.Axis(BodyAxis.Up));
        Assert.Equal(spc.BodyDown, probe.Axis(BodyAxis.Down));
        Assert.Equal(Vector3.VectorX, probe.Axis(BodyAxis.Front));
    }

    [Fact]
    public void ReleasedFromItsPlan_KeepsTheAxesOfTheLastPlansSpacecraft()
    {
        var first = CustomAxesSpacecraft(-6902, "AxisFirstSc", Vector3.VectorX, Vector3.VectorY.Inverse(), Vector3.VectorZ);
        var probe = new AxisProbe();
        first.SetManeuvers(probe);
        first.SetManeuvers();

        Assert.Equal(first.BodyFront, probe.Axis(BodyAxis.Front));
        Assert.Equal(first.BodyUp, probe.Axis(BodyAxis.Up));
    }

    [Fact]
    public void AnUndefinedAxis_Throws()
    {
        var probe = new AxisProbe();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => probe.Axis((BodyAxis)6));
        Assert.Equal("axis", ex.ParamName);
    }

    [Fact]
    public void ASingleVectorAttitude_PreviewsOnTheStaticAxesBeforeAnyPlan_AndOnTheInstanceAxesInOne()
    {
        var spc = CustomAxesSpacecraft(-6904, "AxisPreviewSc", Vector3.VectorX, Vector3.VectorY.Inverse(), Vector3.VectorZ);
        var prograde = new ProgradeAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, TestHelpers.EarthAtJ2000);
        var state = spc.InitialOrbitalParameters.ToStateVector();
        var velocity = state.Velocity.Normalize();

        var before = prograde.ComputeStateOrientation(state);
        Assert.True(Spacecraft.Front.Rotate(before.Rotation).Angle(velocity) < 1e-12);

        spc.SetManeuvers(prograde);
        var inPlan = prograde.ComputeStateOrientation(state);
        Assert.True(spc.BodyFront.Rotate(inPlan.Rotation).Angle(velocity) < 1e-12);
        Assert.True(Spacecraft.Front.Rotate(inPlan.Rotation).Angle(velocity) > 1.0);
    }
}
