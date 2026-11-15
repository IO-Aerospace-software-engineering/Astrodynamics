using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

public class StarTests
{
    private readonly ITestOutputHelper _output;

    public StarTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    [Fact]
    public void Create()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        Assert.Equal(1, star.CatalogNumber);
        Assert.Equal(1000000001, star.NaifId);
        Assert.Equal("star1", star.Name);
        Assert.Equal(1E+30, star.Mass);
        Assert.Equal(6.674299999999999E+19, star.GM);
        Assert.Equal("spec", star.SpectralType);
        Assert.Equal(2, star.VisualMagnitude);
        Assert.Equal(TimeSystem.Time.J2000TDB, star.Epoch);
        Assert.Equal(new Equatorial(10, 20, TimeSystem.Time.J2000TDB), star.EquatorialCoordinatesAtEpoch);
        Assert.Equal(4, star.DeclinationProperMotion);
        Assert.Equal(5, star.RightAscensionProperMotion);
        Assert.Equal(6, star.DeclinationSigma);
        Assert.Equal(7, star.RightAscensionSigma);
        Assert.Equal(8, star.DeclinationSigmaProperMotion);
        Assert.Equal(9, star.RightAscensionSigmaProperMotion);
        Assert.Equal(0.3792, star.Parallax);
        Assert.Equal(8.1373353929324900E+16, star.Distance);
        Assert.Null(star.InitialOrbitalParameters);
        Assert.Equal(0.0, star.Flattening);
        Assert.True(star.IsStar);
    }

    [Fact]
    public void GetEquatorialCoordinates()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        var res = star.GetEquatorialCoordinates(new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame));
        Assert.Equal(new Equatorial(1.4418429380022246, 6.160711018912988, 8.1373353929324900E+16, new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame)), res);
    }

    [Fact]
    public void GetEquatorialCoordinates2()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792,
            new Equatorial(10 - (Astrodynamics.Constants.PI2 * 10), 20 - (Astrodynamics.Constants._2PI * 10), TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8,
            9, TimeSystem.Time.J2000TDB);
        var res = star.GetEquatorialCoordinates(new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame));
        Assert.Equal(new Equatorial(-0.12895338879267282, 6.160711018912984, 81373353929324896, new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame)), res);
    }

    [Fact]
    public void GetDeclinationSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        var res = star.GetDeclinationSigma(new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame));
        Assert.Equal(0.5883685739286051, res);
    }


    [Fact]
    public void GetRightAscensionSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        var res = star.GetRightAscensionSigma(new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0),TimeFrame.TDBFrame));
        Assert.Equal(5.1331621997619905, res);
    }

    [Fact]
    public async Task Propagate()
    {
        var epoch = new TimeSystem.Time(new DateTime(2001, 1, 1), TimeFrame.TDBFrame);
        var observer = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.1, 0.1, 0, 0, 0, 0, epoch);
        await star.PropagateAsync(new Window(epoch, epoch + TimeSpan.FromDays(365 * 4)), TimeSpan.FromDays(365));
        var eph0 = star.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None);
        var eph1 = star.GetEphemeris(epoch.Add(TimeSpan.FromDays(365)), observer, Frames.Frame.ICRF, Aberration.None);
        var eph2 = star.GetEphemeris(epoch.Add(TimeSpan.FromDays(365 + 365)), observer, Frames.Frame.ICRF, Aberration.None);
        Assert.Equal(1.0, eph0.ToEquatorial().RightAscension, 12);
        Assert.Equal(1.0, eph0.ToEquatorial().Declination, 12);
        Assert.Equal(8.1373353929324900E+16, eph0.ToEquatorial().Distance);

        Assert.Equal(1.1, eph1.ToEquatorial().RightAscension, 3);
        Assert.Equal(1.1, eph1.ToEquatorial().Declination, 3);
        Assert.Equal(8.1373353929324896E+16, eph1.ToEquatorial().Distance);

        Assert.Equal(1.2, eph2.ToEquatorial().RightAscension, 3);
        Assert.Equal(1.2, eph2.ToEquatorial().Declination, 3);
        Assert.Equal(8.1373353929324910E+16, eph2.ToEquatorial().Distance);
    }

    internal static Star NewStar(int catalogNumber, TimeSystem.Time epoch) =>
        new(catalogNumber, $"star{catalogNumber}", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.1, 0.1, 0, 0, 0, 0,
            epoch);

    public static TheoryData<long> NonPositiveSteps() => new() { 0L, -1L, -TimeSpan.TicksPerSecond };

    [Theory]
    [MemberData(nameof(NonPositiveSteps))]
    public void Propagate_AStepOfZeroOrLess_Throws(long stepTicks)
    {
        // Arrange: before, the loop epoch += step never passed the end of the window (a negative step also stored a state
        // at each turn, without bound)
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var star = NewStar(668001, epoch);
        var window = new Window(epoch, epoch.AddHours(1.0));

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => star.Propagate(window, TimeSpan.FromTicks(stepTicks)));

        // Assert: the rule of every step of the library (#674), and nothing stored
        Assert.Equal("stepSize", exception.ParamName);
        Assert.False(star.IsPropagated);
        Assert.Throws<ArgumentException>(() => star.WriteEphemeris(new System.IO.FileInfo("star668001.bsp")));
    }

    [Theory]
    [MemberData(nameof(NonPositiveSteps))]
    public void PropagateAsync_AStepOfZeroOrLess_ThrowsOnTheCall(long stepTicks)
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var star = NewStar(668002, epoch);
        var window = new Window(epoch, epoch.AddHours(1.0));

        // Act: thrown by the call itself, before any task is created
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = star.PropagateAsync(window, TimeSpan.FromTicks(stepTicks));
        });

        // Assert
        Assert.Equal("stepSize", exception.ParamName);
        Assert.False(star.IsPropagated);
    }

    [Fact]
    public async Task IsPropagated_IsTrueOnceTheStatesAreStored()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var window = new Window(epoch, epoch.AddHours(1.0));
        var bySync = NewStar(668003, epoch);
        var byAsync = NewStar(668004, epoch);

        // Act and assert: false before, true after either path; the output directory of an older design stays null
        Assert.False(bySync.IsPropagated);
        Assert.False(byAsync.IsPropagated);
        bySync.Propagate(window, TimeSpan.FromMinutes(10.0));
        await byAsync.PropagateAsync(window, TimeSpan.FromMinutes(10.0));
        Assert.True(bySync.IsPropagated);
        Assert.True(byAsync.IsPropagated);
        Assert.Null(bySync.PropagationOutput);
        Assert.Null(byAsync.PropagationOutput);
    }

    /// <summary>The epochs of a propagation of a star: from the start of the window, every step, up to its end.</summary>
    internal static List<TimeSystem.Time> Grid(Window window, TimeSpan step)
    {
        var epochs = new List<TimeSystem.Time>();
        for (var epoch = window.StartDate; epoch <= window.EndDate; epoch += step)
        {
            epochs.Add(epoch);
        }

        return epochs;
    }

    /// <summary>
    /// The position of the stars of <see cref="NewStar"/> from the solar system barycenter in ICRF at <paramref name="epoch"/>,
    /// written here: the right ascension and the declination 1 + 0.1 dt rad, dt the Julian years from the catalog epoch, at
    /// the star's distance.
    /// </summary>
    internal static Vector3 CatalogPosition(Star star, TimeSystem.Time epoch)
    {
        double dt = (epoch - star.Epoch).TotalDays / 365.25;
        double angle = 1.0 + 0.1 * dt;
        return new Vector3(System.Math.Cos(angle) * System.Math.Cos(angle), System.Math.Cos(angle) * System.Math.Sin(angle),
            System.Math.Sin(angle)) * star.Distance;
    }

    /// <summary>
    /// Writes the SPK of <paramref name="star"/>, loads it and checks through SPICE (<c>spkezr</c>, <see cref="SpiceAPI"/>)
    /// that it holds the states of <paramref name="kept"/> alone: each at the catalog position written here, and no state
    /// at the epochs of <paramref name="replaced"/> nor between the two windows, where SPICE has no data (the read throws).
    /// Returns the worst relative position.
    /// </summary>
    internal static double CheckSpkHoldsOnly(Star star, IReadOnlyList<TimeSystem.Time> kept, IEnumerable<TimeSystem.Time> replaced,
        string label, ITestOutputHelper output)
    {
        var directory = Directory.CreateTempSubdirectory("star668");
        var spk = new FileInfo(Path.Combine(directory.FullName, $"star{star.CatalogNumber}.bsp"));
        star.WriteEphemeris(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            double worst = 0.0;
            foreach (var epoch in kept)
            {
                var read = SpiceAPI.Instance.ReadEphemeris(epoch, 0, star.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                var catalog = CatalogPosition(star, epoch);
                double relative = (read.Position - catalog).Magnitude() / catalog.Magnitude();
                worst = System.Math.Max(worst, relative);
            }

            output.WriteLine($"MEASURE {label}: worst relative position {worst:E3}");
            foreach (var epoch in replaced)
            {
                Assert.Throws<InvalidOperationException>(() =>
                    SpiceAPI.Instance.ReadEphemeris(epoch, 0, star.NaifId, Frames.Frame.ICRF, Aberration.None));
            }

            return worst;
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
            directory.Delete(true);
        }
    }

    // Provisional: twice the Linux worst against the catalog position written in the test (1.390e-16, StarTests.Propagate_Twice
    // and ScenarioTests.SimulateAsync_OfAStarPropagatedBefore, 2026-11-15), to be read again on Windows and macOS arm64. The
    // epochs are whole half days from the catalog epoch, whose Julian dates, which the star differences, are exact
    internal const double CatalogPositionTolerance = 2.8e-16;

    [Fact]
    public void Propagate_Twice_ReplacesTheStates()
    {
        // Arrange: two propagations over different windows and steps; before, the second added its states to the first's
        // (TryAdd), so that the SPK of the star covered both windows and the gap between them
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var star = NewStar(668011, epoch);
        var first = new Window(epoch, epoch.AddDays(4.0));
        var second = new Window(new TimeSystem.Time(2001, 3, 1), new TimeSystem.Time(2001, 3, 3));

        // Act
        star.Propagate(first, TimeSpan.FromDays(1.0));
        star.Propagate(second, TimeSpan.FromHours(12.0));

        // Assert: the second propagation's states alone, as Spacecraft.Propagate replaces its states
        var replaced = Grid(first, TimeSpan.FromDays(1.0)).Append(new TimeSystem.Time(2001, 2, 1)).ToList();
        double worst = CheckSpkHoldsOnly(star, Grid(second, TimeSpan.FromHours(12.0)), replaced, "StarTests.Propagate_Twice", _output);
        Assert.True(worst <= CatalogPositionTolerance, $"{worst:E3}");
        Assert.True(star.IsPropagated);
    }

    [Fact]
    public async Task PropagateAsync_AfterPropagate_ReplacesTheStates()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var star = NewStar(668012, epoch);
        var first = new Window(new TimeSystem.Time(2001, 3, 1), new TimeSystem.Time(2001, 3, 1, 6, 0, 0));
        var second = new Window(epoch, epoch.AddDays(2.0));

        // Act: the later window first, then an earlier one at another step, through the asynchronous path
        star.Propagate(first, TimeSpan.FromHours(1.0));
        await star.PropagateAsync(second, TimeSpan.FromHours(12.0));

        // Assert
        double worst = CheckSpkHoldsOnly(star, Grid(second, TimeSpan.FromHours(12.0)),
            Grid(first, TimeSpan.FromHours(1.0)).Append(new TimeSystem.Time(2001, 2, 1)), "StarTests.PropagateAsync_AfterPropagate", _output);
        Assert.True(worst <= CatalogPositionTolerance, $"{worst:E3}");
    }

    // The segment of the SPK is read through CspiceDirect (dafus, dafgda), which the Windows DLL does not export
    // (Linux, Validation); the cross-platform tests above read the SPK through SPICE
    [Fact]
    [Trait("Category", "Validation")]
    public void Propagate_Twice_TheSpkHoldsTheSecondGridAlone()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var star = NewStar(668013, epoch);
        var first = new Window(epoch, epoch.AddDays(4.0));
        var second = new Window(new TimeSystem.Time(2001, 3, 1), new TimeSystem.Time(2001, 3, 3));
        star.Propagate(first, TimeSpan.FromDays(1.0));
        star.Propagate(second, TimeSpan.FromHours(12.0));
        var directory = Directory.CreateTempSubdirectory("star668");
        var spk = new FileInfo(Path.Combine(directory.FullName, "star668013.bsp"));
        try
        {
            // Act
            star.WriteEphemeris(spk);
            var segments = CspiceDirect.ReadSegments(spk.FullName);

            // Assert: one segment over the second window, of type 8 (the states are evenly spaced): its N states, its first
            // epoch and its step (the words 6N, 6N + 1, the last word N, SPK Required Reading, type 8) are those of the
            // second grid, in ET (TDB seconds from J2000)
            var grid = Grid(second, TimeSpan.FromHours(12.0)).Select(x => x.TimeSpanFromJ2000().TotalSeconds).ToArray();
            var segment = Assert.Single(segments);
            Assert.Equal(8, segment.Type);
            Assert.Equal(grid[0], segment.Start);
            Assert.Equal(grid[^1], segment.End);
            int n = (int)segment.Data[^1];
            Assert.Equal(grid.Length, n);
            Assert.Equal(grid[0], segment.Data[6 * n]);
            Assert.Equal(12.0 * 3600.0, segment.Data[6 * n + 1]);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
