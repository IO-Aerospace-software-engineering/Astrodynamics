// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

/// <summary>
/// A negative hold throws in the maneuver constructor (R11 of #668): before 11.0.0 it was accepted, and the window it
/// gave an attitude or an impulse mid-propagation ended before it started. A hold of zero is accepted.
/// </summary>
public class ManeuverHoldTests
{
    public ManeuverHoldTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static Engine NewEngine() =>
        new("eng", "model", "sn", 350.0, 50.0, new FuelTank("tank", "model", "sn", 4000.0, 3000.0));

    private static readonly TimeSystem.Time Epoch = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    [Fact]
    public void Attitude_NegativeHold_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProgradeAttitude(TestHelpers.EarthAtJ2000, Epoch, TimeSpan.FromTicks(-1), NewEngine()));
        Assert.Equal("maneuverHoldDuration", ex.ParamName);
    }

    [Fact]
    public void Triad_NegativeHold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TriadAttitude.CreateLVLH(TestHelpers.EarthAtJ2000, Epoch, TimeSpan.FromSeconds(-1), NewEngine()));
    }

    [Fact]
    public void Impulse_NegativeHold_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, Epoch, TimeSpan.FromSeconds(-1), 8000000.0, NewEngine()));
        Assert.Equal("maneuverHoldDuration", ex.ParamName);
    }

    [Fact]
    public void ZeroHold_IsAccepted()
    {
        var attitude = new ProgradeAttitude(TestHelpers.EarthAtJ2000, Epoch, TimeSpan.Zero, NewEngine());
        Assert.Equal(TimeSpan.Zero, attitude.ManeuverHoldDuration);
    }

    /// <summary>
    /// The hold is set through its checked setter only, which no type outside the library reaches (decision 11 of lot L4
    /// of #668: a private setter with the constructor's check; before 11.0.0 a protected setter that a subclass could give
    /// a negative hold).
    /// </summary>
    [Fact]
    public void TheHoldSetter_IsNotPartOfThePublicOrProtectedSurface()
    {
        var setter = typeof(IO.Astrodynamics.Maneuver.Maneuver).GetProperty(nameof(IO.Astrodynamics.Maneuver.Maneuver.ManeuverHoldDuration))!
            .GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        Assert.False(setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly, setter.Attributes.ToString());
    }
}
