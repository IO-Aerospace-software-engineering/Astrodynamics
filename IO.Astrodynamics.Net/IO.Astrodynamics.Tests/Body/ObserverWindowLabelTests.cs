// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The labels of the windows of the searches on the observer (R15 of #668, lot L6, IC-7 and IC-8): in the scale of the
/// search window's start, the same instants as the former searches, whose CSPICE and occultation paths labelled them TDB;
/// an end at a bound of the search window that bound itself; an end inside a UTC leap second, which no UTC label holds,
/// TDB.
/// </summary>
public class ObserverWindowLabelTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static readonly Window UtcDistanceWindow = new(TimeSystem.Time.CreateTDB(755870700.0).ToUTC(), TimeSystem.Time.CreateTDB(755913900.0).ToUTC());
    private static readonly Window UtcEclipse2005 = new(TimeSystem.Time.Parse("2005-10-03T00:00:00Z"), TimeSystem.Time.Parse("2005-10-04T00:00:00Z"));
    private static readonly Window UtcEclipse001 = new(TimeSystem.Time.Parse("2026-03-20T00:00:00Z"), TimeSystem.Time.Parse("2026-03-20T12:00:00Z"));

    private readonly ITestOutputHelper _output;

    public ObserverWindowLabelTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureLro();
        TestKernels.EnsureOccultationReference();
    }

    // The same instants as the former search, each end at a bound that bound, every other end a UTC label
    private void AssertInTheWindowScale(IReadOnlyList<Window> former, IReadOnlyList<Window> actual, in Window search, string label)
    {
        Assert.NotEmpty(former);
        Assert.Equal(former, actual);
        var start = search.StartDate;
        var end = search.EndDate;
        double lengthChange = 0.0;
        for (int i = 0; i < actual.Count; i++)
        {
            foreach (var t in new[] { actual[i].StartDate, actual[i].EndDate })
            {
                if (t == start) LabelAssert.Equal(start, t);
                else if (t == end) LabelAssert.Equal(end, t);
                else Assert.Equal(TimeScale.UTC, t.Scale);
            }

            // The TT seconds of the UTC labels against the TDB seconds (#508): the rate of TDB − TT, at most 3.3e-10, and the
            // rounding of each converted end to the 100 ns of a label
            double change = System.Math.Abs((actual[i].Length - former[i].Length).TotalSeconds);
            Assert.True(change <= 3.3e-10 * former[i].Length.TotalSeconds + 2.0e-7, $"{label}: {change:E3} s on {former[i].Length}");
            lengthChange = System.Math.Max(lengthChange, change / former[i].Length.TotalSeconds);
        }

        _output.WriteLine($"MEASURE {label}: {actual.Count} windows, largest relative change of Window.Length {lengthChange:E3}");
    }

    [Fact]
    public void UtcSearchWindow_UtcLabels_TheSameInstants()
    {
        var lro = new Spacecraft(-85, "LRO", epoch: UtcDistanceWindow.StartDate);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = new CelestialBody(Stars.Sun);
        var dss13 = new Site(13, "DSS-13", earth);
        var options = WindowSearchOptions.Default with { Step = Step };

        // gfdist_c and gfposc_c, which labelled TDB
        AssertInTheWindowScale(moon.FindDistanceWindows(UtcDistanceWindow, lro, RelationnalOperator.Greater, 1815148.0, new WindowSearchOptions { Step = Step }),
            moon.FindDistanceWindows(UtcDistanceWindow, lro, RelationnalOperator.Greater, 1815148.0, options), UtcDistanceWindow, "gfdist_c");
        AssertInTheWindowScale(moon.FindCoordinateWindows(UtcDistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater, 500000.0, new WindowSearchOptions { Step = Step }),
            moon.FindCoordinateWindows(UtcDistanceWindow, lro, Frames.Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z, RelationnalOperator.Greater,
                500000.0, options), UtcDistanceWindow, "gfposc_c");

        // gfilum_c and the day windows, which forward to it
        AssertInTheWindowScale(dss13.FindIlluminationWindows(UtcDistanceWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, 1.5, sun, new WindowSearchOptions { Step = Step }),
            dss13.FindIlluminationWindows(UtcDistanceWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, 1.5, sun, options), UtcDistanceWindow,
            "gfilum_c");
        AssertInTheWindowScale(dss13.FindIlluminationWindows(UtcDistanceWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, IO.Astrodynamics.Constants.HalfPi + 0.1, sun, new WindowSearchOptions { Step = TimeSpan.FromMinutes(1.0), Aberration = Aberration.LTS }),
            dss13.FindDayWindows(UtcDistanceWindow, 0.1), UtcDistanceWindow, "FindDayWindows");

        // gfoclt_c and the library's occultation search, which labelled TDB
        var kernelSpacecraft = new Spacecraft(-470001, "eclipse_001", epoch: UtcEclipse001.StartDate);
        AssertInTheWindowScale(kernelSpacecraft.FindOccultationWindows(UtcEclipse001, sun, earth, new OccultationSearchOptions { Step = Step, TargetShape = ShapeType.Ellipsoid, FrontShape = ShapeType.Ellipsoid, OccultationType = OccultationType.None }),
            kernelSpacecraft.FindOccultationWindows(UtcEclipse001, sun, earth, new OccultationSearchOptions { Step = Step, OccultationType = OccultationType.None }),
            UtcEclipse001, "gfoclt_c, None");
        var dss65 = new Site(65, "DSS-65", earth);
        AssertInTheWindowScale(dss65.FindOccultationWindows(UtcEclipse2005, sun, moon, new OccultationSearchOptions { Step = TimeSpan.FromSeconds(360.0), TargetShape = ShapeType.Ellipsoid, FrontShape = ShapeType.Ellipsoid }),
            dss65.FindOccultationWindows(UtcEclipse2005, sun, moon, new OccultationSearchOptions { Step = TimeSpan.FromSeconds(360.0) }), UtcEclipse2005,
            "managed occultation");
    }

    [Fact]
    public void Relabel_BoundsTheirOwn_InteriorEndsInTheStartScale_ALeapSecondKeptTdb()
    {
        // The search window from 2016-12-31T23:00Z to 2017-01-01T01:00Z TDB, across the leap second of 2016-12-31
        var start = TimeSystem.Time.Parse("2016-12-31T23:00:00Z");
        var end = TimeSystem.Time.Parse("2017-01-01T01:00:00Z").ToTDB();
        var search = new Window(start, end);
        // 2017-01-01T00:00:36.5 TAI is 23:59:60.5 UTC, inside the leap second: its UTC label reads back 1 s earlier
        var inLeapSecond = TimeSystem.Time.Parse("2017-01-01T00:00:36.5 TAI").ToTDB();
        var ordinary = TimeSystem.Time.Parse("2016-12-31T23:30:00.1234567Z").ToTDB();
        var windows = new[] { new Window(start.ToTDB(), ordinary), new Window(inLeapSecond, end) };

        var relabelled = WindowSearchSettings.InScaleOf(windows, search);

        LabelAssert.Equal(start, relabelled[0].StartDate);
        Assert.Equal(TimeScale.UTC, relabelled[0].EndDate.Scale);
        Assert.Equal(ordinary, relabelled[0].EndDate);
        LabelAssert.Equal(inLeapSecond, relabelled[1].StartDate);
        LabelAssert.Equal(end, relabelled[1].EndDate);
        Assert.Equal(windows, relabelled);
    }
}
