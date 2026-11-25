// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// A write with nothing to write, lot L5c of #668 (decision 81 of #668, decided by S. Guillet, 2026-11-23, as narrowed by
/// decisions 87 and 89 for an arc of one state; #799(e)): <see cref="Spacecraft.WriteEphemeris"/> of a spacecraft that
/// stores no state and <see cref="Star.WriteEphemeris"/> of a star that stores fewer than three throw
/// <see cref="InvalidOperationException"/> (the object has nothing to write, the file is not at fault), before any file is
/// written; an arc of one state keeps its <see cref="ArgumentException"/>; and <see cref="Spacecraft.WriteOrientation"/> of
/// a spacecraft without attitude record says how to give it one (#546: a <see cref="TLEPropagator"/> run records none).
/// The reference is the decided rule.
/// </summary>
public class KernelWriteContentTests
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private const string Directory = "Data/User/KernelWriteContent";

    public KernelWriteContentTests()
    {
        TestKernels.EnsureSolarSystem();
        System.IO.Directory.CreateDirectory(Directory);
    }

    private static FileInfo NewFile(string name)
    {
        var file = new FileInfo(Path.Combine(Directory, name));
        if (file.Exists) file.Delete();
        return file;
    }

    private static Spacecraft NewSpacecraft(int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frames.Frame.ICRF);
        return new Spacecraft(naifId, $"WC{-naifId}", 1000.0, orbit);
    }

    private static Star NewStar(int catalogNumber) =>
        new(catalogNumber, $"star{catalogNumber}", 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, Start), 0.1, 0.1, 0, 0, 0,
            0, Start);

    [Fact]
    public void SpacecraftWriteEphemeris_NoStoredState_ThrowsInvalidOperation_AndWritesNothing()
    {
        var spacecraft = NewSpacecraft(-8811);
        var file = NewFile("none.bsp");

        var exception = Assert.Throws<InvalidOperationException>(() => spacecraft.WriteEphemeris(file));

        Assert.Contains(spacecraft.Name, exception.Message);
        Assert.Contains("no stored state", exception.Message);
        file.Refresh();
        Assert.False(file.Exists);
        // The argument keeps its own exception
        Assert.Throws<ArgumentNullException>(() => spacecraft.WriteEphemeris(null));
    }

    [Fact]
    public void SpacecraftWriteEphemeris_AnArcOfOneState_KeepsArgumentException()
    {
        // Decisions 87 and 89 of #668: a lone state given to AddPropagatedStates is an arc an SPK segment cannot hold
        var spacecraft = NewSpacecraft(-8812);
        spacecraft.AddPropagatedStates([spacecraft.InitialOrbitalParameters.ToStateVector()]);
        var file = NewFile("lone.bsp");

        Assert.Throws<ArgumentException>(() => spacecraft.WriteEphemeris(file));
        file.Refresh();
        Assert.False(file.Exists);
    }

    [Fact]
    public void StarWriteEphemeris_NoState_ThrowsInvalidOperation_AndWritesNothing()
    {
        var star = NewStar(881101);
        var file = NewFile("star0.bsp");

        var exception = Assert.Throws<InvalidOperationException>(() => star.WriteEphemeris(file));

        Assert.Contains(star.Name, exception.Message);
        file.Refresh();
        Assert.False(file.Exists);
        Assert.Throws<ArgumentNullException>(() => star.WriteEphemeris(null));
    }

    [Fact]
    public void StarWriteEphemeris_TwoStates_ThrowsInvalidOperation_AndWritesNothing()
    {
        var star = NewStar(881102);
        star.Propagate(new Window(Start, Start.AddHours(1.0)), TimeSpan.FromHours(1.0));
        var file = NewFile("star2.bsp");

        var exception = Assert.Throws<InvalidOperationException>(() => star.WriteEphemeris(file));

        Assert.Contains("2", exception.Message);
        file.Refresh();
        Assert.False(file.Exists);
    }

    [Fact]
    public void StarWriteEphemeris_ThreeStates_Writes()
    {
        var star = NewStar(881103);
        star.Propagate(new Window(Start, Start.AddHours(2.0)), TimeSpan.FromHours(1.0));
        var file = NewFile("star3.bsp");

        star.WriteEphemeris(file);

        file.Refresh();
        Assert.True(file.Exists);
    }

    [Fact]
    public void WriteOrientation_NeverPropagated_NamesTheWaysToAnAttitudeRecord()
    {
        var spacecraft = NewSpacecraft(-8813);

        var exception = Assert.Throws<InvalidOperationException>(() => spacecraft.WriteOrientation(NewFile("never.bc")));

        Assert.Contains(spacecraft.Name, exception.Message);
        Assert.Contains("TLEPropagator", exception.Message);
        Assert.Contains("AddStateOrientationToICRF", exception.Message);
    }

    [Fact]
    public void WriteOrientation_AfterATlePropagatorRun_NamesTheWaysToAnAttitudeRecord()
    {
        // #546: a TLEPropagator run stores SGP4's states and no attitude, so the CK writer has nothing to write
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = new Spacecraft(-8814, "WC8814", 420000.0, tle);
        var window = new Window(tle.Epoch.ToTDB(), tle.Epoch.ToTDB().AddMinutes(10.0));
        new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();

        var exception = Assert.Throws<InvalidOperationException>(() => spacecraft.WriteOrientation(NewFile("tle.bc")));

        Assert.Contains("TLEPropagator", exception.Message);
        Assert.Contains("AddStateOrientationToICRF", exception.Message);
    }
}
