using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // The obsolete member is read on purpose: it stays null
#pragma warning disable CS0618
        Assert.Null(bySync.PropagationOutput);
        Assert.Null(byAsync.PropagationOutput);
#pragma warning restore CS0618
    }

    [Fact]
    public void PropagationOutput_IsObsoleteAsAWarning()
    {
        // Arrange: the output directory of an older design, always null, which no code sets
        // (nameof of an obsolete member raises CS0618 too)
#pragma warning disable CS0618
        var property = typeof(Star).GetProperty(nameof(Star.PropagationOutput));
#pragma warning restore CS0618

        // Act
        var obsolete = property!.GetCustomAttribute<ObsoleteAttribute>();

        // Assert: a warning (CS0618), not an error, whose message points to the members that replace it
        Assert.NotNull(obsolete);
        Assert.False(obsolete.IsError);
        Assert.Contains(nameof(Star.IsPropagated), obsolete.Message);
        Assert.Contains(nameof(Star.WriteEphemeris), obsolete.Message);
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

    // The noise-level record of the SPK read against the catalog position written in the test: twice the worst, rounded up
    // at the second digit (1.390e-16, StarTests.Propagate_Twice_TheSpkHoldsTheSecondGridAlone, Validation category, Linux
    // only, CI run 43773937161, 2026-11-15; StarTests.Propagate_Twice and ScenarioTests.SimulateAsync_OfAStarPropagatedBefore
    // the same on Linux x64, Windows x64 and macOS arm64), asserted by the Validation test alone (the epochs are whole half
    // days from the catalog epoch, whose Julian dates, which the star differences, are exact). The tests of every platform
    // assert CatalogMotionInOneMicrosecond
    internal const double CatalogPositionTolerance = 2.8e-16;

    /// <summary>
    /// The bound of the tests that run on every platform, where the rounding of the sines and cosines of each runtime is not
    /// measured: the distance the star of <see cref="NewStar"/> moves in 1 µs, the resolution of the library's epochs,
    /// relative to its distance, the least over <paramref name="epochs"/>. Its direction is (cos²a, cos a sin a, sin a), a =
    /// 1 + 0.1 dt rad, dt in Julian years (<see cref="CatalogPosition"/>), whose derivative has the norm √(1 + cos²a): the
    /// bound is √(1 + cos²a) · 0.1 · 1e-6 / (365.25 · 86400), 3.6e-15 near a = 1, so that a state of another epoch, or of
    /// another star, fails it.
    /// </summary>
    internal static double CatalogMotionInOneMicrosecond(Star star, IEnumerable<TimeSystem.Time> epochs) =>
        epochs.Min(epoch =>
        {
            double angle = 1.0 + 0.1 * (epoch - star.Epoch).TotalDays / 365.25;
            return System.Math.Sqrt(1.0 + System.Math.Cos(angle) * System.Math.Cos(angle)) * 0.1 * 1e-6 / (365.25 * 86400.0);
        });

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
        var kept = Grid(second, TimeSpan.FromHours(12.0));
        double worst = CheckSpkHoldsOnly(star, kept, replaced, "StarTests.Propagate_Twice", _output);
        double bound = CatalogMotionInOneMicrosecond(star, kept);
        Assert.True(worst <= bound, $"{worst:E3} > {bound:E3}");
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
        var kept = Grid(second, TimeSpan.FromHours(12.0));
        double worst = CheckSpkHoldsOnly(star, kept, Grid(first, TimeSpan.FromHours(1.0)).Append(new TimeSystem.Time(2001, 2, 1)),
            "StarTests.PropagateAsync_AfterPropagate", _output);
        double bound = CatalogMotionInOneMicrosecond(star, kept);
        Assert.True(worst <= bound, $"{worst:E3} > {bound:E3}");
    }

    /// <summary>
    /// An exact decimal of <paramref name="value"/> (its 28 significant digits), so that the closed forms below take the
    /// proper motions of the star as the doubles it holds.
    /// </summary>
    private static decimal ExactDecimal(double value) =>
        decimal.Parse(value.ToString("E27", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// The Julian years from the catalog epoch of <paramref name="star"/> to <paramref name="epoch"/>, from the exact
    /// difference of their ticks, divided in <see cref="decimal"/> (28 digits): the dt of the catalog motion, before any
    /// rounding to a double.
    /// </summary>
    private static decimal JulianYearsFromCatalogEpoch(Star star, TimeSystem.Time epoch) =>
        (decimal)(epoch.DateTime.Ticks - star.Epoch.DateTime.Ticks) / TimeSpan.TicksPerDay / 365.25m;

    /// <summary>The closed form of the catalog motion, a + μ dt, in <see cref="decimal"/> and rounded once to a double.</summary>
    private static double ClosedFormAngle(double angleAtEpoch, double properMotion, Star star, TimeSystem.Time epoch) =>
        (double)(ExactDecimal(angleAtEpoch) + ExactDecimal(properMotion) * JulianYearsFromCatalogEpoch(star, epoch));

    /// <summary>
    /// The closed form of a sigma at an epoch, √(σ² + (dt σ_μ)²): its square in <see cref="decimal"/>, rounded once to a
    /// double, and the correctly rounded square root of IEEE 754.
    /// </summary>
    private static double ClosedFormSigma(double sigma, double sigmaProperMotion, Star star, TimeSystem.Time epoch)
    {
        decimal s = ExactDecimal(sigma);
        decimal motion = ExactDecimal(sigmaProperMotion) * JulianYearsFromCatalogEpoch(star, epoch);
        return System.Math.Sqrt((double)(s * s + motion * motion));
    }

    /// <summary>
    /// Epochs off whole and half days from the catalog epoch, whose Julian dates are not exact doubles (a Julian date near
    /// 2.45e6 resolves 4.66e-10 day, about 40 µs): each star with its epochs. The star of <see cref="NewStar"/> (1 rad
    /// and 0.1 rad/yr, catalog epoch 2001-01-01 TDB) at the 744 hourly epochs of March 2001, at 199 epochs 1234567 ticks
    /// apart from 2001-03-01 and at 99 epochs 7654321 ticks apart from 2000-06-01, before its catalog epoch; a star of
    /// 0.01 rad/yr at 99 epochs 1234567 ticks apart 40 years after its catalog epoch, a tick difference above 2^53.
    /// </summary>
    private static IEnumerable<(string Label, Star Star, double ProperMotion, List<TimeSystem.Time> Epochs)> OffHalfDayCases(Star fast, Star slow)
    {
        var march = new TimeSystem.Time(2001, 3, 1);
        yield return ("hourly", fast, 0.1, Enumerable.Range(0, 744).Select(i => march + TimeSpan.FromHours(i)).ToList());
        yield return ("ticks", fast, 0.1, Enumerable.Range(1, 199).Select(i => march + TimeSpan.FromTicks(i * 1234567L)).ToList());
        var june = new TimeSystem.Time(2000, 6, 1);
        yield return ("before", fast, 0.1, Enumerable.Range(1, 99).Select(i => june + TimeSpan.FromTicks(i * 7654321L)).ToList());
        var later = new TimeSystem.Time(2041, 1, 1);
        yield return ("40 years", slow, 0.01, Enumerable.Range(1, 99).Select(i => later + TimeSpan.FromTicks(i * 1234567L)).ToList());
    }

    private static Star SlowStar(int catalogNumber, TimeSystem.Time epoch) =>
        new(catalogNumber, $"star{catalogNumber}", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.01, 0.01, 0, 0, 0, 0, epoch);

    // The record of the right ascension and the declination off half days against their closed form, IEEE 754 arithmetic
    // alone (no sine nor cosine), so the same on every platform: twice the worst, rounded up at the second digit (4.441e-16
    // rad, 2 ulp of a + μ dt near 1.4 rad, the slow star 40 years after its epoch; 2.220e-16 for the fast star; the same on
    // Linux x64, Windows x64 and macOS arm64, CI run 43773937161, 2026-11-15).
    // Before the fix, the Julian dates rounded before their difference put the fast star 6.328e-14 rad away, the position
    // of an epoch up to about 20 µs away (half the 2^-31 day, 40.2 µs, a Julian date near 2.45e6 resolves)
    internal const double ClosedFormAngleTolerance = 8.9e-16;

    [Fact]
    public void GetEquatorialCoordinates_OffHalfDays_IsTheClosedForm()
    {
        // Arrange: before, dt was the difference of the two Julian dates, each rounded to a double
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var fast = NewStar(668021, epoch);
        var slow = SlowStar(668022, epoch);
        double worstAngle = 0.0;
        double worstPosition = 0.0;
        double bound = double.PositiveInfinity;

        foreach (var (label, star, properMotion, epochs) in OffHalfDayCases(fast, slow))
        {
            double worstOfCase = 0.0;
            foreach (var at in epochs)
            {
                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert: the closed form, dt from the exact difference of the ticks
                double expected = ClosedFormAngle(1.0, properMotion, star, at);
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Abs(coordinates.RightAscension - expected));
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Abs(coordinates.Declination - expected));
                if (ReferenceEquals(star, fast))
                {
                    var catalog = CatalogPosition(star, at);
                    worstPosition = System.Math.Max(worstPosition, (coordinates.ToCartesian() - catalog).Magnitude() / catalog.Magnitude());
                }
            }

            if (ReferenceEquals(star, fast))
            {
                bound = System.Math.Min(bound, CatalogMotionInOneMicrosecond(star, epochs));
            }

            _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays ({label}): worst angle {worstOfCase:E3} rad");
            worstAngle = System.Math.Max(worstAngle, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays: worst angle {worstAngle:E3} rad, worst relative position {worstPosition:E3} (bound {bound:E3})");
        Assert.True(worstAngle <= ClosedFormAngleTolerance, $"{worstAngle:E3}");
        // The position of the fast star within the distance it moves in 1 µs, the resolution of the library's epochs
        Assert.True(worstPosition <= bound, $"{worstPosition:E3} > {bound:E3}");
    }

    // The record of the right ascension and declination sigmas off half days against their closed form: twice the worst
    // relative difference, rounded up at the second digit (3.469e-16, the same on Linux x64, Windows x64 and macOS arm64,
    // CI run 43773937161, 2026-11-15). The sigmas square through System.Math.Pow, the platform's libm, which IEEE 754 does
    // not require to round correctly, so the figure could differ between platforms; it does not on these three. Before
    // the fix, the rounded Julian dates moved them by 3.930e-12 relative
    internal const double ClosedFormSigmaTolerance = 7.0e-16;

    [Fact]
    public void GetRightAscensionSigma_And_GetDeclinationSigma_OffHalfDays_AreTheClosedForm()
    {
        // Arrange: stars whose sigmas grow with their sigma proper motion, which dominates past a few hours
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var fast = new Star(668023, "star668023", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.1, 0.1, 1e-7, 2e-7, 1e-3, 2e-3, epoch);
        var slow = new Star(668024, "star668024", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.01, 0.01, 1e-7, 2e-7, 1e-3, 2e-3, epoch);
        double worstRightAscension = 0.0;
        double worstDeclination = 0.0;

        foreach (var (label, star, _, epochs) in OffHalfDayCases(fast, slow))
        {
            double worstOfCase = 0.0;
            foreach (var at in epochs)
            {
                // Act
                double rightAscension = star.GetRightAscensionSigma(at);
                double declination = star.GetDeclinationSigma(at);

                // Assert
                double expectedRightAscension = ClosedFormSigma(2e-7, 2e-3, star, at);
                double expectedDeclination = ClosedFormSigma(1e-7, 1e-3, star, at);
                double rightAscensionDelta = System.Math.Abs(rightAscension - expectedRightAscension) / expectedRightAscension;
                double declinationDelta = System.Math.Abs(declination - expectedDeclination) / expectedDeclination;
                worstRightAscension = System.Math.Max(worstRightAscension, rightAscensionDelta);
                worstDeclination = System.Math.Max(worstDeclination, declinationDelta);
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Max(rightAscensionDelta, declinationDelta));
            }

            _output.WriteLine($"MEASURE StarTests.GetSigmas_OffHalfDays ({label}): worst relative sigma {worstOfCase:E3}");
        }

        _output.WriteLine(
            $"MEASURE StarTests.GetSigmas_OffHalfDays: worst relative right ascension sigma {worstRightAscension:E3}, declination sigma {worstDeclination:E3}");
        Assert.True(worstRightAscension <= ClosedFormSigmaTolerance, $"{worstRightAscension:E3}");
        Assert.True(worstDeclination <= ClosedFormSigmaTolerance, $"{worstDeclination:E3}");
    }

    // The record of the position off half days against CSPICE's latrec_c (the radrec_c of SPICE) of the closed form of
    // the right ascension and the declination: twice the worst, rounded up at the second digit (4.657e-16, the slow star 40
    // years after its epoch; 3.711e-16 for the fast star; Validation category, Linux only, CI run 43773937161, 2026-11-15),
    // asserted by the Validation test (7.287e-14 before the fix). A record of its own: CatalogPositionTolerance, the SPK read at half-day epochs,
    // does not move
    internal const double ClosedFormPositionTolerance = 9.4e-16;

    // latrec_c through CspiceDirect, which the Windows DLL does not export (Linux, Validation); the cross-platform test
    // above asserts the angles and the 1 µs bound of the position
    [Fact]
    [Trait("Category", "Validation")]
    public void GetEquatorialCoordinates_OffHalfDays_IsLatrecOfTheClosedForm()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var fast = NewStar(668025, epoch);
        var slow = SlowStar(668026, epoch);
        double worst = 0.0;

        foreach (var (label, star, properMotion, epochs) in OffHalfDayCases(fast, slow))
        {
            double worstOfCase = 0.0;
            foreach (var at in epochs)
            {
                // Act
                var position = star.GetEquatorialCoordinates(at).ToCartesian();

                // Assert
                double angle = ClosedFormAngle(1.0, properMotion, star, at);
                var expected = CspiceDirect.Radrec(star.Distance, angle, angle);
                var reference = new Vector3(expected[0], expected[1], expected[2]);
                worstOfCase = System.Math.Max(worstOfCase, (position - reference).Magnitude() / reference.Magnitude());
            }

            _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays_IsLatrecOfTheClosedForm ({label}): worst relative position {worstOfCase:E3}");
            worst = System.Math.Max(worst, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays_IsLatrecOfTheClosedForm: worst relative position {worst:E3}");
        Assert.True(worst <= ClosedFormPositionTolerance, $"{worst:E3}");
    }

    // The segment of the SPK is read through CspiceDirect (dafus, dafgda), which the Windows DLL does not export
    // (Linux, Validation); the cross-platform tests above read the SPK through SPICE. The noise-level record of the
    // position, measured on Linux, is asserted here, where the test runs on Linux alone
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
        double worst = CheckSpkHoldsOnly(star, Grid(second, TimeSpan.FromHours(12.0)),
            Grid(first, TimeSpan.FromDays(1.0)).Append(new TimeSystem.Time(2001, 2, 1)), "StarTests.Propagate_Twice_TheSpkHoldsTheSecondGridAlone",
            _output);
        Assert.True(worst <= CatalogPositionTolerance, $"{worst:E3}");
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
