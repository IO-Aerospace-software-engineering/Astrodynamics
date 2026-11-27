// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/time-and-windows.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/time-and-windows.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
// give the inputs of a block that continues an earlier one, as the comment opening the block names them
// ("// Inputs: ..."). DocumentationPagesExamplesTests checks that each block equals its region (the block's using
// directives aside, which are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is
// outside IO.Astrodynamics, as an application's; the file imports every namespace of the library, so
// DocumentationPagesExamplesTests also compiles every block of a page that declares using directives, whether the block
// declares them or not, with its own, the earlier blocks' and a console application's implicit ones alone, as a reader
// who copies it does (#716). Do not edit a region alone: change the page and copy the block.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Converters;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Cosmographia.Model;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Helpers;
using IO.Astrodynamics.Kernels;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Ssa;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Tle = IO.Astrodynamics.OrbitalParameters.Tle;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/time-and-windows.md, one method per block (#705).</summary>
internal static class TimeAndWindowsPage
{
    // docs/reference/time-and-windows.md, "Example"
    internal static void Block01()
    {
        #region docs/reference/time-and-windows.md:1
        var t1 = new Time(2024, 6, 21, 12, 0, 0, TimeScale.UTC);
        var t2 = Time.Parse("2024-06-21T12:00:00.000 UTC");
        var t3 = Time.Parse("2024-173T12:00:00Z");
        var t4 = Time.Parse("JD 2460483.0 UTC");
        var doy = Time.FromDayOfYear(2024, 173, 43200.0, TimeScale.UTC);
        bool sameLabel = t1.EqualsExact(t2) && t1.EqualsExact(t3) && t1.EqualsExact(t4) && t1.EqualsExact(doy); // true
        var tdb = t1.ToTdb();
        bool sameInstant = tdb == t1; // true: one instant in two scales
        string text = $"{tdb:yyyy-MM-dd HH:mm:ss}"; // 2024-06-21 12:01:09 TDB
        var later = t1.AddDays(1.0);
        var duration = later - t1;
        var fromJD = Time.CreateFromJD(2451545.0, TimeScale.TDB);
        #endregion
    }

    // docs/reference/time-and-windows.md, "Example"
    internal static void Block02()
    {
        #region docs/reference/time-and-windows.md:2
        var window = new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 1, 31, TimeScale.TDB));
        Console.WriteLine($"Duration: {window.Length.TotalDays} days");
        #endregion
    }

    // docs/reference/time-and-windows.md, "Window Lists"
    internal static void Block03()
    {
        #region docs/reference/time-and-windows.md:3
        var day = new Window(new Time(2024, 1, 1, TimeScale.TDB), TimeSpan.FromDays(1.0));
        var inView = new[] { new Window(day.StartDate.AddHours(1.0), TimeSpan.FromHours(3.0)) };
        var lit = new[] { new Window(day.StartDate, TimeSpan.FromHours(2.0)), new Window(day.StartDate.AddHours(3.0), TimeSpan.FromHours(6.0)) };
        IReadOnlyList<Window> both = inView.IntersectWindows(lit); // 01:00 to 02:00 and 03:00 to 04:00
        IReadOnlyList<Window> gaps = both.ComplementWithin(day);    // 00:00 to 01:00, 02:00 to 03:00, 04:00 to 24:00
        TimeSpan total = both.TotalDuration();                      // 2 h
        #endregion
    }
}
