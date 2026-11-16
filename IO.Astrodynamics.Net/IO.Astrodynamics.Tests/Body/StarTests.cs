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
        var at = new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0), TimeFrame.TDBFrame);
        var res = star.GetEquatorialCoordinates(at);
        // The closed form (#709): the declination 10 - 5π + 4 dt = -1.69974 rad is past the south pole, so it crosses it,
        // -π - d, and the right ascension turns by π; before, the declination was reduced modulo π/2 (-0.12895 rad)
        var (declination, rightAscension) = ClosedFormAcrossPoles(
            ClosedFormAngle(10 - (Astrodynamics.Constants.PI2 * 10), 4, star, at), ClosedFormAngle(20 - (Astrodynamics.Constants._2PI * 10), 5, star, at));
        double delta = System.Math.Max(System.Math.Abs(res.Declination - declination), RightAscensionDelta(res.RightAscension, rightAscension));
        _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates2: {delta:E3} rad");
        Assert.True(delta <= LargeAngleTolerance, $"{delta:E3}");
        Assert.InRange(res.Declination, -1.4418430, -1.4418428);
        Assert.Equal(star.Distance, res.Distance);
    }

    // The record of GetEquatorialCoordinates2 against its closed form: twice the worst on Linux x64, rounded up at the
    // second digit (2.132e-14 rad, 2026-11-16), PROVISIONAL, to be set again from the Windows and macOS arm64 CI logs (IEEE
    // 754 arithmetic alone, so the same is expected): its angles are about 40 rad before their reduction, whose ulp is
    // 7.1e-15 rad. Before the fix (#709), 3.142 rad
    internal const double LargeAngleTolerance = 4.3e-14;

    [Fact]
    public void GetDeclinationSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        var at = new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0), TimeFrame.TDBFrame);
        var res = star.GetDeclinationSigma(at);
        // The closed form √(6² + (8 dt)²), 10.0131 rad, not reduced (#709; before, modulo π/2: 0.58837 rad)
        double expected = ClosedFormSigma(6, 8, star, at);
        double delta = System.Math.Abs(res - expected) / expected;
        _output.WriteLine($"MEASURE StarTests.GetDeclinationSigma: relative {delta:E3} ({res:R} rad)");
        Assert.True(delta <= LargeSigmaTolerance, $"{res} {expected}");
    }


    [Fact]
    public void GetRightAscensionSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8, 9, TimeSystem.Time.J2000TDB);
        var at = new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0), TimeFrame.TDBFrame);
        var res = star.GetRightAscensionSigma(at);
        // The closed form √(7² + (9 dt)²), 11.4163 rad, not reduced (#709; before, modulo 2π: 5.13316 rad)
        double expected = ClosedFormSigma(7, 9, star, at);
        double delta = System.Math.Abs(res - expected) / expected;
        _output.WriteLine($"MEASURE StarTests.GetRightAscensionSigma: relative {delta:E3} ({res:R} rad)");
        Assert.True(delta <= LargeSigmaTolerance, $"{res} {expected}");
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
    /// The Julian years of TDB from the catalog epoch of <paramref name="star"/> to <paramref name="epoch"/>, both converted
    /// to TDB (#709), from the exact difference of their ticks, divided in <see cref="decimal"/> (28 digits): the dt of the
    /// catalog motion, before any rounding to a double. A time in TDB is taken as it is.
    /// </summary>
    private static decimal JulianYearsFromCatalogEpoch(Star star, TimeSystem.Time epoch) =>
        (decimal)(epoch.ToTDB().DateTime.Ticks - star.Epoch.ToTDB().DateTime.Ticks) / TimeSpan.TicksPerDay / 365.25m;

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

    // ---- #709: the elapsed time in TDB, the crossing of a pole, the sigmas not reduced ----

    // The leap seconds of the epochs below, TAI − UTC (IERS Bulletin C: 32 s from 1999-01-01, 36 s from 2015-07-01, 37 s
    // from 2017-01-01)
    private static double DeltaAt(DateTime utc) =>
        utc >= new DateTime(2017, 1, 1) ? 37.0 : utc >= new DateTime(2015, 7, 1) ? 36.0 : utc >= new DateTime(1999, 1, 1) ? 32.0 : throw new ArgumentOutOfRangeException(nameof(utc));

    // The TT seconds from J2000 of a UTC calendar of these years: its seconds from 2000-01-01T12:00:00 + ΔAT + 32.184
    private static decimal TtSecondsOfUtc(DateTime utc) =>
        (decimal)(utc - new DateTime(2000, 1, 1, 12, 0, 0)).Ticks / TimeSpan.TicksPerSecond + (decimal)DeltaAt(utc) + 32.184m;

    /// <summary>
    /// TDB − TT of the leapseconds kernel (naif0012: DELTET/K = 1.657e-3 s, DELTET/EB = 1.671e-2, DELTET/M = 6.239996 rad
    /// and 1.99096871e-7 rad/s), as <c>unitim</c> converts TDT to TDB: K sin E, E = M + EB sin M, M = M0 + M1 TT, TT the
    /// seconds from J2000.
    /// </summary>
    private static double TdbMinusTt(double ttSeconds)
    {
        double m = 6.239996 + 1.99096871e-7 * ttSeconds;
        return 1.657e-3 * System.Math.Sin(m + 1.671e-2 * System.Math.Sin(m));
    }

    // The bound of the angles below: the motion of the star of 0.1 rad/yr in 1 µs, 3.17e-15 rad, the resolution the
    // library's epochs are documented to (the TDB − TT of a conversion is rounded to the tick, 100 ns)
    private const double OneMicrosecondOfMotion = 0.1 * 1e-6 / (365.25 * 86400.0);

    /// <summary>
    /// #709: the same instant given in TDB, TT, TAI, GPS and UTC is one position. dt counts the Julian years of TDB between
    /// the catalog epoch and the epoch, whatever their time frames, as SOFA's <c>iauStarpm</c> takes TDB dates: the
    /// position of a time is that of its TDB time bit for bit, and the TDB time of a time converted from TDB is the time
    /// within a tick. Before, dt was the difference of the two calendars, each in its own frame: 2.299e-7 relative between
    /// UTC and TDB (the 64.18 s of TDB − UTC at 0.1 rad/yr), 1.153e-7 for TAI, 4.977e-12 for TT. Catalog epochs in TDB and
    /// in UTC.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_TheSameInstantInEveryFrame_IsOnePosition()
    {
        // Arrange
        var tdb = new TimeSystem.Time(2001, 3, 1, 6, 0, 0);
        var catalogs = new[] { new TimeSystem.Time(2001, 1, 1), new TimeSystem.Time(new DateTime(2001, 1, 1, 0, 0, 0), TimeFrame.UTCFrame) };
        var frames = new ITimeFrame[] { TimeFrame.TDBFrame, TimeFrame.TDTFrame, TimeFrame.TAIFrame, TimeFrame.GPSFrame, TimeFrame.UTCFrame };
        double worst = 0.0;
        int n = 0;
        foreach (var catalog in catalogs)
        {
            var star = NewStar(709001 + n++, catalog);
            var reference = star.GetEquatorialCoordinates(tdb);
            foreach (var frame in frames)
            {
                var at = tdb.ConvertTo(frame);
                Assert.True(System.Math.Abs(at.ToTDB().DateTime.Ticks - tdb.DateTime.Ticks) <= 1, $"{frame}");

                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert: the position of its TDB time, bit for bit, and that of the TDB time given within a tick
                var ofTdb = star.GetEquatorialCoordinates(at.ToTDB());
                Assert.Equal(ofTdb.Declination, coordinates.Declination);
                Assert.Equal(ofTdb.RightAscension, coordinates.RightAscension);
                double delta = System.Math.Max(System.Math.Abs(coordinates.Declination - reference.Declination),
                    System.Math.Abs(coordinates.RightAscension - reference.RightAscension));
                _output.WriteLine($"MEASURE StarTests.TheSameInstantInEveryFrame (catalog {catalog.Frame.GetType().Name}, epoch {frame.GetType().Name}): {delta:E3} rad");
                worst = System.Math.Max(worst, delta);
            }
        }

        Assert.True(worst <= OneMicrosecondOfMotion, $"{worst:E3} > {OneMicrosecondOfMotion:E3}");
    }

    /// <summary>
    /// #709: between two UTC times across the leap second of 2016-12-31, dt counts it: 86 401 s of TT plus the change of
    /// TDB − TT over the day (the closed form of the leapseconds kernel, written here), not the 86 400 s of the calendars.
    /// Before, the star was the position of an epoch 1 s earlier (3.2e-9 rad at 0.1 rad/yr).
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_AcrossALeapSecond_CountsIt()
    {
        // Arrange
        var catalogUtc = new DateTime(2016, 12, 31, 12, 0, 0);
        var epochUtc = new DateTime(2017, 1, 1, 12, 0, 0);
        var star = NewStar(709011, new TimeSystem.Time(catalogUtc, TimeFrame.UTCFrame));
        double ttCatalog = (double)TtSecondsOfUtc(catalogUtc);
        double ttEpoch = (double)TtSecondsOfUtc(epochUtc);
        double seconds = (double)(TtSecondsOfUtc(epochUtc) - TtSecondsOfUtc(catalogUtc)) + (TdbMinusTt(ttEpoch) - TdbMinusTt(ttCatalog));
        Assert.Equal(86401.0, seconds, 3);
        double expected = 1.0 + 0.1 * seconds / 86400.0 / 365.25;

        // Act
        var coordinates = star.GetEquatorialCoordinates(new TimeSystem.Time(epochUtc, TimeFrame.UTCFrame));

        // Assert
        double delta = System.Math.Max(System.Math.Abs(coordinates.Declination - expected), System.Math.Abs(coordinates.RightAscension - expected));
        _output.WriteLine($"MEASURE StarTests.AcrossALeapSecond: {delta:E3} rad (dt {seconds:F9} s)");
        Assert.True(delta <= OneMicrosecondOfMotion, $"{delta:E3} > {OneMicrosecondOfMotion:E3}");
    }

    /// <summary>
    /// #709, the reference: the coordinates of a star whose catalog epoch and epochs are UTC times, against dt from
    /// CSPICE's ET (<c>unitim_c</c> TDT → TDB of the TT of each UTC time, TT from the leap-second table of the IERS):
    /// dt = (ET − ET_catalog) / 86 400 / 365.25. Epochs every 7 h 13 min over 2001 and over 2016-07 to 2017-06, across the
    /// leap second. Validation category: <c>unitim_c</c> through <see cref="CspiceDirect"/>, which the Windows DLL does not
    /// export; the cross-platform tests above assert the frame-independence and the leap second.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void GetEquatorialCoordinates_UtcEpoch_IsTheClosedFormOfSpiceTdb()
    {
        // Arrange
        var cases = new[]
        {
            (Catalog: new DateTime(2001, 1, 1), Start: new DateTime(2001, 1, 1, 0, 30, 0), End: new DateTime(2002, 1, 1)),
            (Catalog: new DateTime(2016, 7, 1), Start: new DateTime(2016, 7, 1, 0, 30, 0), End: new DateTime(2017, 7, 1)),
        };
        double worst = 0.0;
        int n = 0;
        foreach (var (catalogUtc, start, end) in cases)
        {
            var star = NewStar(709021 + n++, new TimeSystem.Time(catalogUtc, TimeFrame.UTCFrame));
            double etCatalog = CspiceDirect.TtToTdb((double)TtSecondsOfUtc(catalogUtc));
            double worstOfCase = 0.0;
            for (var utc = start; utc < end; utc = utc.AddMinutes(433.0))
            {
                // Act
                var coordinates = star.GetEquatorialCoordinates(new TimeSystem.Time(utc, TimeFrame.UTCFrame));

                // Assert
                double et = CspiceDirect.TtToTdb((double)TtSecondsOfUtc(utc));
                double expected = 1.0 + 0.1 * ((et - etCatalog) / 86400.0 / 365.25);
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Max(System.Math.Abs(coordinates.Declination - expected),
                    System.Math.Abs(coordinates.RightAscension - expected)));
            }

            _output.WriteLine($"MEASURE StarTests.UtcEpoch_IsTheClosedFormOfSpiceTdb ({catalogUtc:yyyy-MM}): worst angle {worstOfCase:E3} rad");
            worst = System.Math.Max(worst, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.UtcEpoch_IsTheClosedFormOfSpiceTdb: worst angle {worst:E3} rad");
        Assert.True(worst <= SpiceTdbAngleTolerance, $"{worst:E3}");
    }

    // The record of the angles against dt from CSPICE's ET: twice the worst on Linux x64, rounded up at the second digit
    // (3.553e-15 rad, 2016-07 and 2001-01 alike, 2026-11-16), PROVISIONAL: the Validation test runs on Linux alone, to be
    // confirmed from the CI log of Linux. The ET of unitim_c, a double near 5e8 s (ulp 6e-8 s), and the tick of the
    // library (1e-7 s) bound it near the motion of 1e-7 s, 3.2e-16 rad, plus the rounding of 1 + 0.1 dt. Before the fix
    // (#709), dt skipped the leap second of 2016-12-31 (3.2e-9 rad) and the TDB − TT of each UTC time (up to 1.7 ms)
    internal const double SpiceTdbAngleTolerance = 7.2e-15;

    /// <summary>
    /// The closed form of a declination dec0 + μ dt past a pole, written as the spherical identity: with k = ⌊(d + π/2) / π⌋,
    /// the declination (−1)^k (d − kπ) and the right ascension a + kπ, which point where (cos d cos a, cos d sin a, sin d)
    /// points. In <see cref="decimal"/> but for π.
    /// </summary>
    private static (double Declination, double RightAscension) ClosedFormAcrossPoles(double declination, double rightAscension)
    {
        double k = System.Math.Floor((declination + System.Math.PI / 2.0) / System.Math.PI);
        double dec = (k % 2.0 == 0.0 ? 1.0 : -1.0) * (double)((decimal)declination - (decimal)k * (decimal)System.Math.PI);
        double ra = (double)((decimal)rightAscension + (decimal)k * (decimal)System.Math.PI) % (2.0 * System.Math.PI);
        return (dec, ra < 0.0 ? ra + 2.0 * System.Math.PI : ra);
    }

    // The difference of two right ascensions on the circle
    private static double RightAscensionDelta(double a, double b)
    {
        double d = System.Math.Abs(a - b) % (2.0 * System.Math.PI);
        return System.Math.Min(d, 2.0 * System.Math.PI - d);
    }

    private static Vector3 Direction(double declination, double rightAscension) =>
        new(System.Math.Cos(declination) * System.Math.Cos(rightAscension), System.Math.Cos(declination) * System.Math.Sin(rightAscension),
            System.Math.Sin(declination));

    // The epoch dt Julian years after the catalog epoch of a star, to the tick
    private static TimeSystem.Time AfterCatalogEpoch(Star star, double years) =>
        star.Epoch + TimeSpan.FromTicks((long)System.Math.Round(years * 365.25 * TimeSpan.TicksPerDay));

    /// <summary>
    /// The stars past a pole: north (1.5 rad, +0.1 rad/yr) and south (−1.5 rad, −0.1 rad/yr), a right ascension of 1 rad
    /// moving 0.05 rad/yr, every 0.01 yr over 2 yr, and a declination of 0 moving 1 rad/yr, which passes both poles.
    /// </summary>
    private static IEnumerable<(string Label, Star Star, double Declination, double DeclinationMotion, double RightAscensionMotion, double[] Years)> PoleCases(
        TimeSystem.Time catalog)
    {
        var grid = Enumerable.Range(0, 201).Select(i => i * 0.01).ToArray();
        yield return ("north", new Star(709031, "star709031", 1E+30, "spec", 2, 0.3792, new Equatorial(1.5, 1.0, catalog), 0.1, 0.05, 0, 0, 0, 0, catalog),
            1.5, 0.1, 0.05, grid);
        yield return ("south", new Star(709032, "star709032", 1E+30, "spec", 2, 0.3792, new Equatorial(-1.5, 1.0, catalog), -0.1, 0.05, 0, 0, 0, 0, catalog),
            -1.5, -0.1, 0.05, grid);
        yield return ("both", new Star(709033, "star709033", 1E+30, "spec", 2, 0.3792, new Equatorial(0.0, 1.0, catalog), 1.0, 0.05, 0, 0, 0, 0, catalog),
            0.0, 1.0, 0.05, Enumerable.Range(0, 801).Select(i => i * 0.01).ToArray());
    }

    /// <summary>
    /// #709: a declination that passes a pole crosses it: the declination π − d (−π − d past the south pole) and the right
    /// ascension turned by π, the direction of the linear model's angles, continuous from one epoch to the next. Before, the
    /// declination was reduced modulo π/2, a jump of up to π/2 toward the equator (1.5 rad + 0.1 rad/yr read 0.009 rad
    /// after 0.8 yr), and a star at the exact pole read the equator.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_AcrossAPole_CrossesIt()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        double worstAngle = 0.0;
        double worstDirection = 0.0;
        double worstStep = 0.0;
        foreach (var (label, star, declination, declinationMotion, rightAscensionMotion, years) in PoleCases(catalog))
        {
            double caseAngle = 0.0;
            double caseDirection = 0.0;
            double caseStep = 0.0;
            Vector3? previous = null;
            double previousYears = 0.0;
            foreach (var dt in years)
            {
                var at = AfterCatalogEpoch(star, dt);
                double exactYears = (double)JulianYearsFromCatalogEpoch(star, at);

                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert: the closed form of the crossing, and the direction of the unreduced angles
                double d = ClosedFormAngle(declination, declinationMotion, star, at);
                double a = ClosedFormAngle(1.0, rightAscensionMotion, star, at);
                var (expectedDeclination, expectedRightAscension) = ClosedFormAcrossPoles(d, a);
                Assert.InRange(coordinates.Declination, -System.Math.PI / 2.0, System.Math.PI / 2.0);
                Assert.InRange(coordinates.RightAscension, 0.0, 2.0 * System.Math.PI);
                caseAngle = System.Math.Max(caseAngle, System.Math.Max(System.Math.Abs(coordinates.Declination - expectedDeclination),
                    RightAscensionDelta(coordinates.RightAscension, expectedRightAscension)));
                var direction = Direction(coordinates.Declination, coordinates.RightAscension);
                caseDirection = System.Math.Max(caseDirection, (direction - Direction(d, a)).Magnitude());
                if (previous.HasValue)
                {
                    // No more than the arc the angles move, √(μδ² + μα²) Δt
                    double arc = System.Math.Sqrt(declinationMotion * declinationMotion + rightAscensionMotion * rightAscensionMotion) * (exactYears - previousYears);
                    caseStep = System.Math.Max(caseStep, previous.Value.Angle(direction) - arc);
                }

                previous = direction;
                previousYears = exactYears;
            }

            _output.WriteLine($"MEASURE StarTests.AcrossAPole ({label}): worst angle {caseAngle:E3} rad, direction {caseDirection:E3}, step over the arc {caseStep:E3} rad");
            worstAngle = System.Math.Max(worstAngle, caseAngle);
            worstDirection = System.Math.Max(worstDirection, caseDirection);
            worstStep = System.Math.Max(worstStep, caseStep);
        }

        _output.WriteLine($"MEASURE StarTests.AcrossAPole: worst angle {worstAngle:E3} rad, direction {worstDirection:E3}, step over the arc {worstStep:E3} rad");
        Assert.True(worstAngle <= PoleAngleTolerance, $"{worstAngle:E3}");
        Assert.True(worstDirection <= PoleDirectionTolerance, $"{worstDirection:E3}");
        Assert.True(worstStep <= PoleDirectionTolerance, $"{worstStep:E3}");
    }

    // The records of the crossing against its closed form, twice the worst on Linux x64, rounded up at the second digit
    // (2026-11-16), PROVISIONAL, to be set again from the Windows and macOS arm64 CI logs (the direction goes through the
    // platform's sines and cosines): the angles 9.992e-15 rad (the star of 1 rad/yr, its declination 8 rad after 8 yr,
    // whose rounding the reduction carries; 3.553e-15 rad for the others), the directions 1.539e-15 (3.446e-16 for the
    // others); a step between consecutive epochs longer than the arc of the angles by 0.000e+00 rad. Before the fix (#709),
    // 3.142 rad (the right ascension not turned), a direction 2.0 away and a jump of 1.569 rad over 0.01 yr
    internal const double PoleAngleTolerance = 2.0e-14;
    internal const double PoleDirectionTolerance = 3.1e-15;

    /// <summary>
    /// #709: a star at a pole, with no motion, stays there: its declination ±π/2 (before, (π/2) mod (π/2) = 0, the equator).
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void GetEquatorialCoordinates_AtAPole_StaysThere(double sign)
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        var star = new Star(709041, "star709041", 1E+30, "spec", 2, 0.3792, new Equatorial(sign * Astrodynamics.Constants.PI2, 1.0, catalog), 0, 0, 0, 0, 0, 0,
            catalog);

        // Act
        var coordinates = star.GetEquatorialCoordinates(catalog.AddDays(100.0));

        // Assert
        Assert.Equal(sign * Astrodynamics.Constants.PI2, coordinates.Declination);
        Assert.Equal(1.0, coordinates.RightAscension);
    }

    /// <summary>
    /// #709, the reference: the coordinates past a pole against CSPICE, <c>latrec_c</c> of the unreduced angles (which
    /// accepts any latitude) then <c>reclat_c</c>, whose latitude in [−π/2, π/2] and longitude modulo 2π are the reduced
    /// pair. Validation category: CSPICE through <see cref="CspiceDirect"/>, which the Windows DLL does not export; the
    /// cross-platform test above asserts the closed form of the crossing.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void GetEquatorialCoordinates_AcrossAPole_IsReclatOfLatrec()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        double worst = 0.0;
        foreach (var (label, star, declination, declinationMotion, rightAscensionMotion, years) in PoleCases(catalog))
        {
            double worstOfCase = 0.0;
            foreach (var dt in years)
            {
                var at = AfterCatalogEpoch(star, dt);

                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert
                double d = ClosedFormAngle(declination, declinationMotion, star, at);
                double a = ClosedFormAngle(1.0, rightAscensionMotion, star, at);
                var (_, longitude, latitude) = CspiceDirect.Reclat(CspiceDirect.Radrec(1.0, a, d));
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Max(System.Math.Abs(coordinates.Declination - latitude),
                    RightAscensionDelta(coordinates.RightAscension, longitude)));
            }

            _output.WriteLine($"MEASURE StarTests.AcrossAPole_IsReclatOfLatrec ({label}): worst angle {worstOfCase:E3} rad");
            worst = System.Math.Max(worst, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.AcrossAPole_IsReclatOfLatrec: worst angle {worst:E3} rad");
        Assert.True(worst <= PoleReclatTolerance, $"{worst:E3}");
    }

    // The record of the crossing against latrec_c then reclat_c: twice the worst on Linux x64, rounded up at the second
    // digit (1.533e-15 rad, the star of 1 rad/yr; 8.882e-16 rad for the others; 2026-11-16), PROVISIONAL: the Validation
    // test runs on Linux alone, to be confirmed from the CI log of Linux. Before the fix (#709), 3.142 rad
    internal const double PoleReclatTolerance = 3.1e-15;

    /// <summary>
    /// #709: a sigma is a standard deviation, not an angle: √(σ² + (dt σ_μ)²) as it is, above π/2 for the declination and
    /// 2π for the right ascension. Before, they were reduced modulo π/2 and 2π: a declination sigma of 2 rad read 0.429 rad,
    /// a right ascension sigma of 7 rad 0.717 rad, and a sigma of 2 rad grown by 1e-3 rad/yr over 2000 yr 1.258 rad instead
    /// of 2.828 rad.
    /// </summary>
    [Fact]
    public void Sigmas_AboveAnAngleRange_AreNotReduced()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        var star = new Star(709051, "star709051", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, catalog), 0, 0, 2.0, 7.0, 1e-3, 1e-3, catalog);
        var later = catalog + TimeSpan.FromDays(2000 * 365.25);

        // Act and assert
        foreach (var at in new[] { catalog, later })
        {
            double declination = star.GetDeclinationSigma(at);
            double rightAscension = star.GetRightAscensionSigma(at);
            double expectedDeclination = ClosedFormSigma(2.0, 1e-3, star, at);
            double expectedRightAscension = ClosedFormSigma(7.0, 1e-3, star, at);
            double declinationDelta = System.Math.Abs(declination - expectedDeclination) / expectedDeclination;
            double rightAscensionDelta = System.Math.Abs(rightAscension - expectedRightAscension) / expectedRightAscension;
            _output.WriteLine(
                $"MEASURE StarTests.Sigmas_AboveAnAngleRange ({(at == catalog ? "catalog" : "2000 yr")}): relative declination {declinationDelta:E3} ({declination:R} rad), right ascension {rightAscensionDelta:E3} ({rightAscension:R} rad)");
            Assert.True(declinationDelta <= LargeSigmaTolerance, $"{declination} {expectedDeclination}");
            Assert.True(rightAscensionDelta <= LargeSigmaTolerance, $"{rightAscension} {expectedRightAscension}");
        }

        Assert.Equal(2.0, star.GetDeclinationSigma(catalog));
        Assert.Equal(7.0, star.GetRightAscensionSigma(catalog));
    }

    // The record of the sigmas above π/2 and 2π against their closed form (GetDeclinationSigma, GetRightAscensionSigma,
    // Sigmas_AboveAnAngleRange_AreNotReduced: 2, 2.828, 7, 7.280, 10.013 and 11.416 rad): twice the worst relative
    // difference on Linux x64, rounded up at the second digit (1.269e-16, the right ascension sigma of 7 rad at the catalog
    // epoch, where the library returns 7 exactly: the closed form's conversion of 49 with 28 decimals from decimal to
    // double; 0.000e+00 for the others; 2026-11-16), PROVISIONAL, to be set again from the Windows and macOS arm64 CI logs
    // (System.Math.Pow is the platform's libm). Before the fix (#709), the sigmas were reduced: 0.588, 5.133, 0.429 rad
    internal const double LargeSigmaTolerance = 2.6e-16;

    /// <summary>
    /// The right ascension is reduced to [0, 2π): a right ascension just below 0, a + μ dt = −1e-30 rad, is the direction of
    /// 0, the double of [0, 2π) nearest to it on the circle; 2π − 1e-30 rounds to 2π, the end the interval excludes, which
    /// the reduction returned (`ra % 2π` keeps −1e-30, and −1e-30 + 2π rounds to 2π). A right ascension of −1e-15 rad is
    /// 2π − 1e-15, which rounds to the double below 2π (6.283185307179585), kept. The closed form: dt is one Julian year
    /// exactly (365.25 days of TDB from a catalog epoch of TDB), so a + μ dt is μ exactly.
    /// </summary>
    [Theory]
    [InlineData(-1e-30)]
    [InlineData(-1e-17)]
    [InlineData(-1e-15)]
    [InlineData(-4.0e-16)]
    public void GetEquatorialCoordinates_RightAscensionJustBelowZero_IsInZeroTwoPi(double rightAscensionMotion)
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        var star = new Star(709061, "star709061", 1E+30, "spec", 2, 0.3792, new Equatorial(0.5, 0.0, catalog), 0, rightAscensionMotion, 0, 0, 0, 0, catalog);
        var at = catalog + TimeSpan.FromDays(365.25);
        Assert.Equal(1m, JulianYearsFromCatalogEpoch(star, at));

        // Act
        var coordinates = star.GetEquatorialCoordinates(at);

        // Assert: in [0, 2π), and the double of that interval nearest to μ on the circle (0 when 2π + μ rounds to 2π)
        double shifted = 2.0 * System.Math.PI + rightAscensionMotion;
        double expected = shifted < 2.0 * System.Math.PI ? shifted : 0.0;
        _output.WriteLine($"MEASURE StarTests.RightAscensionJustBelowZero ({rightAscensionMotion:E3}): {coordinates.RightAscension:R} rad");
        Assert.InRange(coordinates.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
        Assert.Equal(expected, coordinates.RightAscension);
        Assert.Equal(0.5, coordinates.Declination);
    }
}
