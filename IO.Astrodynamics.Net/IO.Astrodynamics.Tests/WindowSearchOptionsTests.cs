// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The options records of the window searches (R15 of #668, lot L6, D1): <see cref="WindowSearchOptions"/>,
/// <see cref="FieldOfViewSearchOptions"/> and <see cref="OccultationSearchOptions"/>, their defaults and the checks of
/// their init accessors, which name the property and run again through <c>with</c>.
/// </summary>
public class WindowSearchOptionsTests
{
    [Fact]
    public void Defaults_AreNewAndUnset()
    {
        Assert.Equal(new WindowSearchOptions(), WindowSearchOptions.Default);
        Assert.Null(WindowSearchOptions.Default.Step);
        Assert.Null(WindowSearchOptions.Default.Aberration);
        Assert.Null(WindowSearchOptions.Default.ConvergenceTolerance);
        Assert.Null(WindowSearchOptions.Default.AdjustValue);

        Assert.Equal(new FieldOfViewSearchOptions(), FieldOfViewSearchOptions.Default);
        Assert.Null(FieldOfViewSearchOptions.Default.Step);
        Assert.Null(FieldOfViewSearchOptions.Default.Aberration);
        Assert.Null(FieldOfViewSearchOptions.Default.ConvergenceTolerance);
        Assert.Null(FieldOfViewSearchOptions.Default.TargetShape);
        Assert.Null(FieldOfViewSearchOptions.Default.TargetFrame);

        Assert.Equal(new OccultationSearchOptions(), OccultationSearchOptions.Default);
        Assert.Null(OccultationSearchOptions.Default.Step);
        Assert.Null(OccultationSearchOptions.Default.Aberration);
        Assert.Null(OccultationSearchOptions.Default.ConvergenceTolerance);
        Assert.Null(OccultationSearchOptions.Default.TargetShape);
        Assert.Null(OccultationSearchOptions.Default.FrontShape);
        Assert.Equal(OccultationType.Any, OccultationSearchOptions.Default.OccultationType);
        Assert.Null(OccultationSearchOptions.Default.FrontFrame);
        Assert.Null(OccultationSearchOptions.Default.TargetFrame);
    }

    [Fact]
    public void Settings_AreKept()
    {
        var o = WindowSearchOptions.Default with
        {
            Step = TimeSpan.FromMinutes(1), Aberration = IO.Astrodynamics.Aberration.LT, ConvergenceTolerance = TimeSpan.FromTicks(1), AdjustValue = 0.0
        };
        Assert.Equal(TimeSpan.FromMinutes(1), o.Step);
        Assert.Equal(IO.Astrodynamics.Aberration.LT, o.Aberration);
        Assert.Equal(TimeSpan.FromTicks(1), o.ConvergenceTolerance);
        Assert.Equal(0.0, o.AdjustValue);

        var f = FieldOfViewSearchOptions.Default with { TargetShape = ShapeType.Point, TargetFrame = Frames.Frame.ICRF };
        Assert.Equal(ShapeType.Point, f.TargetShape);
        Assert.Equal(Frames.Frame.ICRF, f.TargetFrame);

        var c = OccultationSearchOptions.Default with { FrontShape = ShapeType.Point, OccultationType = OccultationType.Full };
        Assert.Equal(ShapeType.Point, c.FrontShape);
        Assert.Equal(OccultationType.Full, c.OccultationType);
    }

    public static TheoryData<long> NonPositiveTicks => new() { 0L, -1L, -TimeSpan.TicksPerHour };

    [Theory]
    [MemberData(nameof(NonPositiveTicks))]
    public void Step_NotPositive_ThrowsNamingStep(long ticks)
    {
        var step = TimeSpan.FromTicks(ticks);
        Assert.Equal("Step", Assert.Throws<ArgumentOutOfRangeException>(() => new WindowSearchOptions { Step = step }).ParamName);
        Assert.Equal("Step", Assert.Throws<ArgumentOutOfRangeException>(() => FieldOfViewSearchOptions.Default with { Step = step }).ParamName);
        Assert.Equal("Step", Assert.Throws<ArgumentOutOfRangeException>(() => OccultationSearchOptions.Default with { Step = step }).ParamName);
    }

    [Theory]
    [MemberData(nameof(NonPositiveTicks))]
    public void ConvergenceTolerance_NotPositive_ThrowsNamingConvergenceTolerance(long ticks)
    {
        var tolerance = TimeSpan.FromTicks(ticks);
        Assert.Equal("ConvergenceTolerance",
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowSearchOptions.Default with { ConvergenceTolerance = tolerance }).ParamName);
        Assert.Equal("ConvergenceTolerance",
            Assert.Throws<ArgumentOutOfRangeException>(() => new FieldOfViewSearchOptions { ConvergenceTolerance = tolerance }).ParamName);
        Assert.Equal("ConvergenceTolerance",
            Assert.Throws<ArgumentOutOfRangeException>(() => new OccultationSearchOptions { ConvergenceTolerance = tolerance }).ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1e-300)]
    public void AdjustValue_NotFiniteOrNegative_ThrowsNamingAdjustValue(double adjust)
    {
        Assert.Equal("AdjustValue",
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowSearchOptions.Default with { AdjustValue = adjust }).ParamName);
    }

    [Fact]
    public void NullSettings_AreAccepted()
    {
        var o = WindowSearchOptions.Default with { Step = TimeSpan.FromSeconds(1), AdjustValue = 1.0 };
        var back = o with { Step = null, AdjustValue = null, ConvergenceTolerance = null };
        Assert.Equal(WindowSearchOptions.Default, back);
    }
}
