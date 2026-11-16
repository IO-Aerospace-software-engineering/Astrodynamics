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
        var at = new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0), TimeFrame.TDBFrame);
        var res = star.GetEquatorialCoordinates(at);
        // The straight line written here (#718), at dt = 366/365.25 yr: before, the linear angles 1.4418429380022246 and
        // 6.160711018912988 rad (the declination 10 + 4 dt reduced across the poles) at the catalog distance
        AssertIsTheStraightLine(star, at, res, "StarTests.GetEquatorialCoordinates");
        Assert.InRange(res.Declination, -System.Math.PI / 2.0, System.Math.PI / 2.0);
        Assert.InRange(res.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
    }

    /// <summary>
    /// Asserts that <paramref name="coordinates"/> are the straight line of <paramref name="star"/> at
    /// <paramref name="epoch"/> (<see cref="CatalogPosition"/>): its position within <see cref="StraightLinePositionTolerance"/>
    /// of its length.
    /// </summary>
    private void AssertIsTheStraightLine(Star star, TimeSystem.Time epoch, Equatorial coordinates, string label)
    {
        var expected = CatalogPosition(star, epoch);
        double relative = (coordinates.ToCartesian() - expected).Magnitude() / expected.Magnitude();
        _output.WriteLine($"MEASURE {label}: relative position {relative:E3}");
        Assert.True(relative <= StraightLinePositionTolerance, $"{relative:E3}");
    }

    // The record of a star against its straight line written here (StraightLinePosition, the sines and cosines of the
    // runtime on both sides): twice the worst on Linux x64, rounded up at the second digit, provisional (to be set again
    // from the Windows x64 and macOS arm64 CI logs): 4.384e-16 relative, GetEquatorialCoordinates_OffHalfDays_IsTheClosedForm
    // (2.775e-16 for GetEquatorialCoordinates, 4.319e-16 across a pole, 1.033e-16 for Propagate; 2026-11-16). Before the fix
    // (#718), the linear angles
    internal const double StraightLinePositionTolerance = 8.8e-16;

    [Fact]
    public void GetEquatorialCoordinates2()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792,
            new Equatorial(10 - (Astrodynamics.Constants.PI2 * 10), 20 - (Astrodynamics.Constants._2PI * 10), TimeSystem.Time.J2000TDB), 4, 5, 6, 7, 8,
            9, TimeSystem.Time.J2000TDB);
        var at = new TimeSystem.Time(new DateTime(2001, 1, 1, 12, 0, 0), TimeFrame.TDBFrame);
        var res = star.GetEquatorialCoordinates(at);
        // The straight line (#718) from catalog angles outside [−π/2, π/2] and [0, 2π): u0, e_α and e_δ of their sines and
        // cosines; before, the linear angles, the declination 10 - 5π + 4 dt = -1.69974 rad crossing the south pole
        AssertIsTheStraightLine(star, at, res, "StarTests.GetEquatorialCoordinates2");
        Assert.InRange(res.Declination, -System.Math.PI / 2.0, System.Math.PI / 2.0);
        Assert.InRange(res.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
    }

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
        // The straight line (#718) at 0, 365 and 730 days: before, the angles 1, 1.1 and 1.2 rad (to 3 decimals) at the
        // catalog distance
        foreach (var at in new[] { epoch, epoch.Add(TimeSpan.FromDays(365)), epoch.Add(TimeSpan.FromDays(365 + 365)) })
        {
            var eph = star.GetEphemeris(at, observer, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var expected = CatalogPosition(star, at);
            double relative = (eph.Position - expected).Magnitude() / expected.Magnitude();
            _output.WriteLine($"MEASURE StarTests.Propagate ({(at - epoch).TotalDays} d): relative position {relative:E3}");
            Assert.True(relative <= StraightLinePositionTolerance, $"{relative:E3}");
        }
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
    /// The straight line of a star (SOFA's <c>iauStarpm</c>, Note 5, rv = 0), written here as a vector: its position from
    /// the solar system barycenter in ICRF <paramref name="years"/> Julian years after its catalog epoch, r0 (u0 + x e_α +
    /// y e_δ), r0 its catalog distance, u0 its catalog direction, e_α = (−sin α0, cos α0, 0) and e_δ = (−sin δ0 cos α0,
    /// −sin δ0 sin α0, cos δ0), x = μα* dt and y = μδ dt from the exact decimals of the proper motions and of dt.
    /// </summary>
    internal static Vector3 StraightLinePosition(Star star, decimal years)
    {
        double a = star.EquatorialCoordinatesAtEpoch.RightAscension;
        double d = star.EquatorialCoordinatesAtEpoch.Declination;
        double x = (double)(ExactDecimal(star.RightAscensionProperMotion) * years);
        double y = (double)(ExactDecimal(star.DeclinationProperMotion) * years);
        var u0 = new Vector3(System.Math.Cos(d) * System.Math.Cos(a), System.Math.Cos(d) * System.Math.Sin(a), System.Math.Sin(d));
        var east = new Vector3(-System.Math.Sin(a), System.Math.Cos(a), 0.0);
        var north = new Vector3(-System.Math.Sin(d) * System.Math.Cos(a), -System.Math.Sin(d) * System.Math.Sin(a), System.Math.Cos(d));
        return (u0 + east * x + north * y) * star.Distance;
    }

    /// <summary>The straight line of <see cref="StraightLinePosition(Star, decimal)"/>, dt given as a double.</summary>
    internal static Vector3 StraightLinePosition(Star star, double years) => StraightLinePosition(star, ExactDecimal(years));

    /// <summary>
    /// The position of a star from the solar system barycenter in ICRF at <paramref name="epoch"/>, written here: its straight
    /// line (<see cref="StraightLinePosition(Star, decimal)"/>) at dt, the Julian years of TDB from its catalog epoch.
    /// </summary>
    internal static Vector3 CatalogPosition(Star star, TimeSystem.Time epoch) => StraightLinePosition(star, JulianYearsFromCatalogEpoch(star, epoch));

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

    // The noise-level record of the SPK read against the catalog position written in the test (its straight line, #718):
    // twice the worst on Linux x64, rounded up at the second digit, provisional (2.780e-16,
    // StarTests.Propagate_Twice_TheSpkHoldsTheSecondGridAlone, Validation category, Linux only, 2026-11-16; StarTests.Propagate_Twice
    // and ScenarioTests.SimulateAsync_OfAStarPropagatedBefore the same; 1.390e-16 for the linear angles before #718, CI run
    // 43773937161), asserted by the Validation test alone (the epochs are whole half
    // days from the catalog epoch, whose Julian dates, which the star differences, are exact). The tests of every platform
    // assert CatalogMotionInOneMicrosecond
    internal const double CatalogPositionTolerance = 5.6e-16;

    /// <summary>
    /// The bound of the tests that run on every platform, where the rounding of the sines and cosines of each runtime is not
    /// measured: the distance the star moves in 1 µs, the resolution of the library's epochs, relative to its distance from
    /// the barycenter, the least over <paramref name="epochs"/>. Along its straight line the star moves r0 μ per Julian
    /// year, μ = √(μα*² + μδ²), at the distance r0 √(1 + (μ dt)²): the bound is μ / √(1 + (μ dt)²) · 1e-6 / (365.25 · 86400),
    /// 4.5e-15 for the star of <see cref="NewStar"/> near its catalog epoch, so that a state of another epoch, or of another
    /// star, fails it.
    /// </summary>
    internal static double CatalogMotionInOneMicrosecond(Star star, IEnumerable<TimeSystem.Time> epochs) =>
        epochs.Min(epoch =>
        {
            double mu = System.Math.Sqrt(star.RightAscensionProperMotion * star.RightAscensionProperMotion +
                                         star.DeclinationProperMotion * star.DeclinationProperMotion);
            double dt = (double)JulianYearsFromCatalogEpoch(star, epoch);
            return mu / System.Math.Sqrt(1.0 + mu * dt * mu * dt) * 1e-6 / (365.25 * 86400.0);
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

    /// <summary>
    /// #709 and #718: off whole and half days, the position is the straight line (<see cref="CatalogPosition"/>) at dt from
    /// the exact difference of the ticks: the fast star within the distance it moves in 1 µs, the resolution of the
    /// library's epochs, and every star within <see cref="StraightLinePositionTolerance"/>. Before #709, dt was the
    /// difference of the two Julian dates, each rounded to a double: the fast star 6.328e-14 rad away, the position of an
    /// epoch up to about 20 µs away (half the 2^-31 day, 40.2 µs, a Julian date near 2.45e6 resolves).
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_OffHalfDays_IsTheClosedForm()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var fast = NewStar(668021, epoch);
        var slow = SlowStar(668022, epoch);
        double worstPosition = 0.0;
        double worstFast = 0.0;
        double bound = double.PositiveInfinity;

        foreach (var (label, star, _, epochs) in OffHalfDayCases(fast, slow))
        {
            double worstOfCase = 0.0;
            foreach (var at in epochs)
            {
                // Act
                var position = star.GetEquatorialCoordinates(at).ToCartesian();

                // Assert: the straight line, dt from the exact difference of the ticks
                var expected = CatalogPosition(star, at);
                worstOfCase = System.Math.Max(worstOfCase, (position - expected).Magnitude() / expected.Magnitude());
            }

            if (ReferenceEquals(star, fast))
            {
                bound = System.Math.Min(bound, CatalogMotionInOneMicrosecond(star, epochs));
                worstFast = System.Math.Max(worstFast, worstOfCase);
            }

            _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays ({label}): worst relative position {worstOfCase:E3}");
            worstPosition = System.Math.Max(worstPosition, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.GetEquatorialCoordinates_OffHalfDays: worst relative position {worstPosition:E3}, fast star {worstFast:E3} (bound {bound:E3})");
        Assert.True(worstPosition <= StraightLinePositionTolerance, $"{worstPosition:E3}");
        Assert.True(worstFast <= bound, $"{worstFast:E3} > {bound:E3}");
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

    // The record of the position off half days against the straight line whose catalog direction is CSPICE's latrec_c (the
    // radrec_c of SPICE): twice the worst on Linux x64, rounded up at the second digit, provisional (the Validation test runs
    // on Linux alone): 4.199e-16 relative (2026-11-16). Before #709, 7.287e-14; a record of its own: CatalogPositionTolerance, the SPK
    // read at half-day epochs, does not move
    internal const double ClosedFormPositionTolerance = 8.4e-16;

    /// <summary>
    /// The straight line of <paramref name="star"/> <paramref name="years"/> Julian years after its catalog epoch, its
    /// catalog position r0 u0 from CSPICE's <c>latrec_c</c> (<see cref="CspiceDirect.Radrec"/>) and its motion
    /// r0 (x e_α + y e_δ) written here, as <see cref="StraightLinePosition(Star, decimal)"/>.
    /// </summary>
    internal static Vector3 SpiceStraightLinePosition(Star star, decimal years)
    {
        double a = star.EquatorialCoordinatesAtEpoch.RightAscension;
        double d = star.EquatorialCoordinatesAtEpoch.Declination;
        var catalog = CspiceDirect.Radrec(star.Distance, a, d);
        double x = (double)(ExactDecimal(star.RightAscensionProperMotion) * years);
        double y = (double)(ExactDecimal(star.DeclinationProperMotion) * years);
        var east = new Vector3(-System.Math.Sin(a), System.Math.Cos(a), 0.0);
        var north = new Vector3(-System.Math.Sin(d) * System.Math.Cos(a), -System.Math.Sin(d) * System.Math.Sin(a), System.Math.Cos(d));
        return new Vector3(catalog[0], catalog[1], catalog[2]) + (east * x + north * y) * star.Distance;
    }

    // latrec_c through CspiceDirect, which the Windows DLL does not export (Linux, Validation); the cross-platform test
    // above asserts the straight line written here and the 1 µs bound of the position
    [Fact]
    [Trait("Category", "Validation")]
    public void GetEquatorialCoordinates_OffHalfDays_IsLatrecOfTheClosedForm()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2001, 1, 1);
        var fast = NewStar(668025, epoch);
        var slow = SlowStar(668026, epoch);
        double worst = 0.0;

        foreach (var (label, star, _, epochs) in OffHalfDayCases(fast, slow))
        {
            double worstOfCase = 0.0;
            foreach (var at in epochs)
            {
                // Act
                var position = star.GetEquatorialCoordinates(at).ToCartesian();

                // Assert
                var reference = SpiceStraightLinePosition(star, JulianYearsFromCatalogEpoch(star, at));
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

    /// <summary>
    /// #709: the same instant given in TDB, TT, TAI, GPS and UTC is one position. dt counts the Julian years of TDB between
    /// the catalog epoch and the epoch, whatever their time frames, as SOFA's <c>iauStarpm</c> takes TDB dates: the
    /// position of a time is that of its TDB time bit for bit, and the TDB time of a time converted from TDB is the time
    /// within a tick: the position within the distance the star moves in 1 µs. Before, dt was the difference of the two calendars, each in its own frame: 2.299e-7 relative between
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
        double bound = double.PositiveInfinity;
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
                double delta = (coordinates.ToCartesian() - reference.ToCartesian()).Magnitude() / reference.Distance;
                _output.WriteLine($"MEASURE StarTests.TheSameInstantInEveryFrame (catalog {catalog.Frame.GetType().Name}, epoch {frame.GetType().Name}): relative position {delta:E3}");
                worst = System.Math.Max(worst, delta);
                bound = System.Math.Min(bound, CatalogMotionInOneMicrosecond(star, new[] { tdb }));
            }
        }

        Assert.True(worst <= bound, $"{worst:E3} > {bound:E3}");
    }

    /// <summary>
    /// #709: between two UTC times across the leap second of 2016-12-31, dt counts it: 86 401 s of TT plus the change of
    /// TDB − TT over the day (the closed form of the leapseconds kernel, written here), not the 86 400 s of the calendars:
    /// the straight line at that dt. Before, the star was the position of an epoch 1 s earlier (3.2e-9 rad at 0.1 rad/yr).
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
        var at = new TimeSystem.Time(epochUtc, TimeFrame.UTCFrame);
        var expected = StraightLinePosition(star, seconds / 86400.0 / 365.25);

        // Act
        var coordinates = star.GetEquatorialCoordinates(at);

        // Assert: the straight line at that dt (#718), within the distance the star moves in 1 µs
        double delta = (coordinates.ToCartesian() - expected).Magnitude() / expected.Magnitude();
        double bound = CatalogMotionInOneMicrosecond(star, new[] { at });
        _output.WriteLine($"MEASURE StarTests.AcrossALeapSecond: relative position {delta:E3} (dt {seconds:F9} s, bound {bound:E3})");
        Assert.True(delta <= bound, $"{delta:E3} > {bound:E3}");
    }

    /// <summary>
    /// #709, the reference: the position of a star whose catalog epoch and epochs are UTC times, against its straight line
    /// (#718) at dt from CSPICE's ET (<c>unitim_c</c> TDT → TDB of the TT of each UTC time, TT from the leap-second table of the IERS):
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
                var expected = StraightLinePosition(star, (et - etCatalog) / 86400.0 / 365.25);
                worstOfCase = System.Math.Max(worstOfCase, (coordinates.ToCartesian() - expected).Magnitude() / expected.Magnitude());
            }

            _output.WriteLine($"MEASURE StarTests.UtcEpoch_IsTheClosedFormOfSpiceTdb ({catalogUtc:yyyy-MM}): worst relative position {worstOfCase:E3}");
            worst = System.Math.Max(worst, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.UtcEpoch_IsTheClosedFormOfSpiceTdb: worst relative position {worst:E3}");
        Assert.True(worst <= SpiceTdbAngleTolerance, $"{worst:E3}");
    }

    // The record of the position against the straight line at dt from CSPICE's ET: twice the worst on Linux x64, rounded up
    // at the second digit, provisional (the Validation test runs on Linux alone): 4.963e-15 relative, 2016-07 (4.941e-15 for 2001-01;
    // 3.553e-15 rad in the angles of the linear model before #718, CI run 43906476725; 2026-11-16). The ET of unitim_c, a
    // double near 5e8 s (ulp 6e-8 s), and the tick of the library (1e-7 s) bound it near the motion of 1e-7 s, 4.5e-16
    // relative, plus the rounding of the sines and cosines. Before the fix (#709), dt skipped the leap second of 2016-12-31
    // (3.2e-9 rad) and the TDB − TT of each UTC time (up to 1.7 ms)
    internal const double SpiceTdbAngleTolerance = 1.0e-14;

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
    /// The stars whose straight line passes near or over a pole (#718): north (1.5 rad, μδ 0.1 rad/yr, μα* 0.05 rad/yr)
    /// and south (−1.5 rad, −0.1 and 0.05 rad/yr), which pass near it, and a star of no motion in right ascension
    /// (1.5 rad, 0.1 rad/yr) that passes over the north pole after cot(1.5)/0.1 = 0.709 yr, every 0.01 yr over 2 yr; a
    /// star at 0 moving 1 rad/yr in declination and 0.05 rad/yr in right ascension, every 0.01 yr over 8 yr, whose
    /// direction tends to atan(0.05) from the north pole and never reaches it: a straight line never passes both poles
    /// (#709's case of 1 rad/yr through both poles was an artefact of the linear angles).
    /// </summary>
    private static IEnumerable<(string Label, Star Star, double[] Years)> PoleCases(TimeSystem.Time catalog)
    {
        var grid = Enumerable.Range(0, 201).Select(i => i * 0.01).ToArray();
        yield return ("north", new Star(709031, "star709031", 1E+30, "spec", 2, 0.3792, new Equatorial(1.5, 1.0, catalog), 0.1, 0.05, 0, 0, 0, 0, catalog),
            grid);
        yield return ("south", new Star(709032, "star709032", 1E+30, "spec", 2, 0.3792, new Equatorial(-1.5, 1.0, catalog), -0.1, 0.05, 0, 0, 0, 0, catalog),
            grid);
        yield return ("over the pole", new Star(709034, "star709034", 1E+30, "spec", 2, 0.3792, new Equatorial(1.5, 1.0, catalog), 0.1, 0.0, 0, 0, 0, 0, catalog),
            grid);
        yield return ("fast", new Star(709033, "star709033", 1E+30, "spec", 2, 0.3792, new Equatorial(0.0, 1.0, catalog), 1.0, 0.05, 0, 0, 0, 0, catalog),
            Enumerable.Range(0, 801).Select(i => i * 0.01).ToArray());
    }

    /// <summary>
    /// #709 and #718: a star whose straight line passes near or over a pole crosses it continuously: its declination in
    /// [−π/2, π/2], its right ascension in [0, 2π), its position the straight line written here, and the angle between two
    /// consecutive directions no more than μ Δt, the angular rate of a straight line at constant speed seen from the
    /// barycenter being at most its speed over its least distance, μ. Before #709 the declination was reduced modulo π/2, a
    /// jump of up to π/2 toward the equator, and a star at the exact pole read the equator; before #718 the angles moved
    /// linearly.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_AcrossAPole_CrossesIt()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        double worstPosition = 0.0;
        double worstStep = 0.0;
        foreach (var (label, star, years) in PoleCases(catalog))
        {
            double casePosition = 0.0;
            double caseStep = 0.0;
            Vector3? previous = null;
            decimal previousYears = 0m;
            double mu = System.Math.Sqrt(star.RightAscensionProperMotion * star.RightAscensionProperMotion +
                                         star.DeclinationProperMotion * star.DeclinationProperMotion);
            foreach (var dt in years)
            {
                var at = AfterCatalogEpoch(star, dt);
                decimal exactYears = JulianYearsFromCatalogEpoch(star, at);

                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert
                Assert.InRange(coordinates.Declination, -System.Math.PI / 2.0, System.Math.PI / 2.0);
                Assert.InRange(coordinates.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
                var position = coordinates.ToCartesian();
                var expected = StraightLinePosition(star, exactYears);
                casePosition = System.Math.Max(casePosition, (position - expected).Magnitude() / expected.Magnitude());
                var direction = Direction(coordinates.Declination, coordinates.RightAscension);
                if (previous.HasValue)
                {
                    caseStep = System.Math.Max(caseStep, Separation(previous.Value, direction) - mu * (double)(exactYears - previousYears));
                }

                previous = direction;
                previousYears = exactYears;
            }

            _output.WriteLine($"MEASURE StarTests.AcrossAPole ({label}): worst relative position {casePosition:E3}, step over μ Δt {caseStep:E3} rad");
            worstPosition = System.Math.Max(worstPosition, casePosition);
            worstStep = System.Math.Max(worstStep, caseStep);
        }

        _output.WriteLine($"MEASURE StarTests.AcrossAPole: worst relative position {worstPosition:E3}, step over μ Δt {worstStep:E3} rad");
        Assert.True(worstPosition <= StraightLinePositionTolerance, $"{worstPosition:E3}");
        Assert.True(worstStep <= 0.0, $"{worstStep:E3}");
    }

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
    /// #709 and #718, the reference: the coordinates near and over a pole against CSPICE, <c>reclat_c</c> of the straight
    /// line whose catalog position is <c>latrec_c</c>'s (<see cref="SpiceStraightLinePosition"/>): its latitude in
    /// [−π/2, π/2] and its longitude modulo 2π. Validation category: CSPICE through <see cref="CspiceDirect"/>, which the
    /// Windows DLL does not export; the cross-platform test above asserts the straight line written here.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void GetEquatorialCoordinates_AcrossAPole_IsReclatOfLatrec()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        double worst = 0.0;
        foreach (var (label, star, years) in PoleCases(catalog))
        {
            double worstOfCase = 0.0;
            foreach (var dt in years)
            {
                var at = AfterCatalogEpoch(star, dt);

                // Act
                var coordinates = star.GetEquatorialCoordinates(at);

                // Assert: the angles, the right ascension scaled by cos δ (its arc on the sky, which a pole leaves undefined)
                var reference = SpiceStraightLinePosition(star, JulianYearsFromCatalogEpoch(star, at));
                var (_, longitude, latitude) = CspiceDirect.Reclat(new[] { reference.X, reference.Y, reference.Z });
                worstOfCase = System.Math.Max(worstOfCase, System.Math.Max(System.Math.Abs(coordinates.Declination - latitude),
                    RightAscensionDelta(coordinates.RightAscension, longitude) * System.Math.Cos(latitude)));
            }

            _output.WriteLine($"MEASURE StarTests.AcrossAPole_IsReclatOfLatrec ({label}): worst angle {worstOfCase:E3} rad");
            worst = System.Math.Max(worst, worstOfCase);
        }

        _output.WriteLine($"MEASURE StarTests.AcrossAPole_IsReclatOfLatrec: worst angle {worst:E3} rad");
        Assert.True(worst <= PoleReclatTolerance, $"{worst:E3}");
    }

    // The record of the crossing against reclat_c of the straight line: twice the worst on Linux x64, rounded up at the
    // second digit, provisional (the Validation test runs on Linux alone): 2.220e-16 rad, every case (2026-11-16). Before
    // #709, 3.142 rad
    internal const double PoleReclatTolerance = 4.5e-16;

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
    // difference over Linux x64, Windows x64 and macOS arm64, rounded up at the second digit (1.269e-16 on all three,
    // CI runs 43906476725 and 43926830045: the right ascension sigma of 7 rad at the catalog epoch, where the library
    // returns 7 exactly, the closed form's conversion of 49 with 28 decimals from decimal to double; 0.000e+00 for the
    // others; System.Math.Pow is the platform's libm). Before the fix (#709), the sigmas were reduced: 0.588, 5.133,
    // 0.429 rad
    internal const double LargeSigmaTolerance = 2.6e-16;

    /// <summary>
    /// The right ascension is reduced to [0, 2π): a right ascension just below 0, a + μ dt = −1e-30 rad, is the direction of
    /// 0, the double of [0, 2π) nearest to it on the circle; 2π − 1e-30 rounds to 2π, the end the interval excludes, which
    /// the reduction returned (`ra % 2π` keeps −1e-30, and −1e-30 + 2π rounds to 2π). A right ascension of −1e-15 rad is
    /// 2π − 1e-15, which rounds to the double below 2π (6.283185307179585), kept. The closed form: dt is one Julian year
    /// exactly (365.25 days of TDB from a catalog epoch of TDB), so the straight line turns the right ascension by
    /// atan2(μ, cos δ0) (#718).
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

        // Assert: in [0, 2π), and the double of that interval nearest to α0 + atan2(μ, cos δ0) on the circle (0 when its sum
        // with 2π rounds to 2π), atan2(μ, cos δ0) = μ / cos δ0 to the rounding for these μ (#718: μ is a true angle); the
        // declination 0.5 rad within (μ / cos δ0)² / 2 (a second order of 1e-30 at most) and its rounding
        double shifted = 2.0 * System.Math.PI + rightAscensionMotion / System.Math.Cos(0.5);
        double expected = shifted < 2.0 * System.Math.PI ? shifted : 0.0;
        _output.WriteLine($"MEASURE StarTests.RightAscensionJustBelowZero ({rightAscensionMotion:E3}): {coordinates.RightAscension:R} rad, declination − 0.5 {coordinates.Declination - 0.5:E3} rad");
        Assert.InRange(coordinates.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
        Assert.Equal(expected, coordinates.RightAscension);
        Assert.True(System.Math.Abs(coordinates.Declination - 0.5) <= System.Math.BitIncrement(0.5) - 0.5, $"{coordinates.Declination:R}");
    }

    // ---- #718: the straight line of iauStarpm, the proper motion in right ascension a true angle ----

    /// <summary>
    /// A row of <c>tools/sofa_reference/starpm_ref.c</c>: a star (its right ascension and declination in rad, μα* =
    /// (dα/dt) cos δ and μδ in rad per Julian year, its parallax in arcsec, its catalog epoch in TDB days from J2000) at
    /// <see cref="Years"/> Julian years of TDB from its catalog epoch; SOFA's <c>iauStarpm</c> (rv = 0, fed dα/dt =
    /// μα*/cos δ; its <see cref="Status"/>, −9 where it cannot be fed, NaN then and for a status other than 0): its unit
    /// direction and the ratio of the catalog parallax to its parallax, the distance in catalog distances; the closed form
    /// of the straight line in long double: its unit direction and its length in catalog distances.
    /// </summary>
    internal sealed record StarpmCase(string Label, double RightAscension, double Declination, double RightAscensionProperMotion,
        double DeclinationProperMotion, double Parallax, double EpochDays, double Years, int Status, Vector3 Starpm, double StarpmDistance,
        Vector3 StraightLine, double StraightLineLength);

    // Printed by tools/sofa_reference/starpm_ref.c (SOFA 2023-10-11, gcc 13.3, x86-64 Linux), never edited by hand
    internal static readonly StarpmCase[] StarpmCases =
    {
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-0.0095011796364996431, -0.9966332215401521, 0.081436781051396262), 0.9999999937595595,
            new Vector3(-0.0095011796364996865, -0.9966332215401521, 0.081436781051396262), 1.0000000012611043),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-0.0095356719968014113, -0.99659608981391223, 0.081885925086000247), 1.000000051094968,
            new Vector3(-0.0095356719968006289, -0.99659608981391223, 0.081885925086000219), 1.0000001261104201),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-0.0098804841055939348, -0.99621357926272025, 0.086376388708946455), 1.0000118607999076,
            new Vector3(-0.0098804841055915704, -0.99621357926272225, 0.086376388708922849), 1.000012610963287),
        new("Barnard I/311", 4.7028598776368105, 0.081476992715215468, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-0.013313185071146394, -0.99127609698294716, 0.13112764259879037), 1.0012527991588618,
            new Vector3(-0.01331318505322117, -0.99127609701412955, 0.13112764236488336), 1.0012603100897464),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 1.0, 0,
            new Vector3(-0.0095962329535933151, -0.99653042121451263, 0.082674251778449157), 0.99999999369962467,
            new Vector3(-0.0095962329535935077, -0.99653042121451263, 0.082674251778449157), 1.0000000012694943),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 10.0, 0,
            new Vector3(-0.0096308448004147357, -0.99649260297802988, 0.083124840081054183), 1.000000051250711,
            new Vector3(-0.0096308448004144113, -0.99649260297802988, 0.083124840081054183), 1.0000001269494114),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 100.0, 0,
            new Vector3(-0.0099768499410058456, -0.99610315411649819, 0.0876297256895089), 1.0000119378653636,
            new Vector3(-0.0099768499410037986, -0.9961031541165003, 0.087629725689484864), 1.0000126948613683),
        new("Barnard DR3", 4.7027635336371842, 0.082718484526907429, -3.8860289090702973e-06, 5.0238303802473693e-05, 0.54697590000000007, 5844, 1000.0, 0,
            new Vector3(-0.01342125493689973, -0.99108911546991763, 0.13252258340741371), 1.0012611100044226,
            new Vector3(-0.013421254918629902, -0.99108911550203527, 0.13252258316906818), 1.0012686894084084),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 1.0, 0,
            new Vector3(0.010127656653116421, 0.0078975464977979939, 0.99991752626405728), 0.99999999998925326,
            new Vector3(0.010127656653116492, 0.0078975464977980494, 0.99991752626405728), 1.0000000000000249),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 10.0, 0,
            new Vector3(0.010126870921005155, 0.0078993949171149106, 0.99991751962113995), 0.99999999989477351,
            new Vector3(0.01012687092100509, 0.0078993949171148586, 0.99991751962113995), 1.0000000000024902),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 100.0, 0,
            new Vector3(0.010119013597646115, 0.0079178791085256859, 0.99991745297011059), 0.99999999917184945,
            new Vector3(0.010119013597646115, 0.0079178791085256859, 0.99991745297011059), 1.0000000002490168),
        new("Polaris I/311", 0.66228513378259868, 1.5579531082148617, 2.156451253575216e-07, -5.745042121148002e-08, 0.0075399999999999998, -3195.9375, 1000.0, 0,
            new Vector3(0.010040440141498139, 0.0081027208423543502, 0.9999167642742649), 1.0000000141300092,
            new Vector3(0.01004044014149817, 0.0081027208423543745, 0.9999167642742649), 1.0000000249016852),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-8.6549126963275804e-05, -0.0086760344376858155, 0.99996235875941086), 0.9999999937595595,
            new Vector3(-8.6549126963274666e-05, -0.0086760344376857531, 0.99996235875941086), 1.0000000012611043),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-0.00011709810166067247, -0.0082250897482506365, 0.9999661665209818), 1.000000051094968,
            new Vector3(-0.00011709810166067056, -0.0082250897482505948, 0.9999661665209818), 1.0000001261104201),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-0.00042258268081029243, -0.0037156072970390788, 0.99999300781870071), 1.0000118607999076,
            new Vector3(-0.00042258268080868017, -0.0037156072970627139, 0.9999930078187006), 1.000012610963287),
        new("Barnard I/311 at +89.5 deg", 4.7028598776368105, 1.562069680534925, -3.8716250946045326e-06, 5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-0.0034731097662499951, 0.041326628026185155, 0.9991396535642737), 1.0012527991588618,
            new Vector3(-0.0034731097502762051, 0.04132662779027272, 0.99913965357408707), 1.0012603100897464),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1.0, 0,
            new Vector3(-8.6549126963275804e-05, -0.0086760344376858155, -0.99996235875941086), 0.9999999937595595,
            new Vector3(-8.6549126963274666e-05, -0.0086760344376857531, -0.99996235875941086), 1.0000000012611043),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 10.0, 0,
            new Vector3(-0.00011709810166067247, -0.0082250897482506365, -0.9999661665209818), 1.000000051094968,
            new Vector3(-0.00011709810166067056, -0.0082250897482505948, -0.9999661665209818), 1.0000001261104201),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 100.0, 0,
            new Vector3(-0.00042258268081029243, -0.0037156072970390788, -0.99999300781870071), 1.0000118607999076,
            new Vector3(-0.00042258268080868017, -0.0037156072970627139, -0.9999930078187006), 1.000012610963287),
        new("Barnard I/311 at -89.5 deg", 4.7028598776368105, -1.562069680534925, -3.8716250946045326e-06, -5.0072138761410217e-05, 0.54830999999999996, -3195.9375, 1000.0, 0,
            new Vector3(-0.0034731097662499951, 0.041326628026185155, -0.9991396535642737), 1.0012527991588618,
            new Vector3(-0.0034731097502762051, 0.04132662779027272, -0.99913965357408707), 1.0012603100897464),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 1.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-1.9220755960603488e-05, -1.1426396634568409e-05, 0.99999999974999998), 1.00000000025),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 10.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.00019220755484919571, -0.00011426396351811477, 0.99999997500000093), 1.0000000249999996),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 100.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.0019220707913731711, -0.0011426368071591547, 0.99999750000937493), 1.0000024999968751),
        new("North pole", 1, 1.5707963267948966, 1.0000000000000001e-05, 2.0000000000000002e-05, 0.37919999999999998, 366.5, 1000.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.019215952577645756, -0.01142354110909565, 0.99975009371095458), 1.0002499687578101),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 1.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-1.9220755960603488e-05, -1.1426396634568409e-05, -0.99999999974999998), 1.00000000025),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 10.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.00019220755484919571, -0.00011426396351811477, -0.99999997500000093), 1.0000000249999996),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 100.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.0019220707913731711, -0.0011426368071591547, -0.99999750000937493), 1.0000024999968751),
        new("South pole", 1, -1.5707963267948966, 1.0000000000000001e-05, -2.0000000000000002e-05, 0.37919999999999998, 366.5, 1000.0, -9,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.019215952577645756, -0.01142354110909565, -0.99975009371095458), 1.0002499687578101),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(0.16071541416630961, 0.43355749103016039, 0.88667832815569814), 1.0099504938362078),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.57977116612635826, 0.16562886016610184, 0.79776718133084445), 1.7320508075688774),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.89362143331063415, -0.086268170926741231, 0.44045264968303044), 14.177446878757825),
        new("1 rad, 0.1 rad/yr", 1, 1, 0.10000000000000001, 0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.91440793527323483, -0.11541435406318602, 0.38799182309105074), 141.42489172702238),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.05739137806500462, 0.0025859968854076505, 0.99834840729271879), 1.0062305898749053),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.61430990705753896, -0.33979242202313775, 0.7121547921811715), 1.5),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.8515498949918564, -0.50179153895865591, 0.15188162419747189), 11.224972160321824),
        new("1.5 rad, 0.1 and 0.05 rad/yr", 1, 1.5, 0.050000000000000003, 0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.85799159855193774, -0.50856589797357077, 0.072188255503413873), 111.80787092150535),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 1.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.05739137806500462, 0.0025859968854076505, -0.99834840729271879), 1.0062305898749053),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 10.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.61430990705753896, -0.33979242202313775, -0.7121547921811715), 1.5),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 100.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.8515498949918564, -0.50179153895865591, -0.15188162419747189), 11.224972160321824),
        new("-1.5 rad, -0.1 and 0.05 rad/yr", 1, -1.5, 0.050000000000000003, -0.10000000000000001, 0.37919999999999998, 366.5, 1000.0, 2,
            new Vector3(double.NaN, double.NaN, double.NaN), double.NaN,
            new Vector3(-0.85799159855193774, -0.50856589797357077, -0.072188255503413873), 111.80787092150535)
    };

    /// <summary>The star of a row, built as a caller builds one from its catalog values, and the epoch of its horizon.</summary>
    private static (Star Star, TimeSystem.Time At) StarOf(StarpmCase row, int index)
    {
        var epoch = TimeSystem.Time.CreateTDB(row.EpochDays * 86400.0);
        var star = new Star(718000 + index, $"star{718000 + index}", 1E+30, "spec", 2, row.Parallax, new Equatorial(row.Declination, row.RightAscension, epoch),
            row.DeclinationProperMotion, row.RightAscensionProperMotion, 0, 0, 0, 0, epoch);
        var at = epoch + TimeSpan.FromTicks((long)System.Math.Round(row.Years * 365.25 * TimeSpan.TicksPerDay));
        Assert.Equal((decimal)row.Years, JulianYearsFromCatalogEpoch(star, at));
        return (star, at);
    }

    /// <summary>
    /// The angle between two directions, atan2(|a × b|, a · b), which resolves an angle near zero to the rounding of its
    /// components; <see cref="Vector3.Angle"/>, the acos of the dot product, cannot (1.49e-8 rad at least).
    /// </summary>
    private static double Separation(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    // The records against iauStarpm, per horizon: twice the worst on Linux x64, rounded up at the second digit, provisional
    // (to be set again from the Windows x64 and macOS arm64 CI logs): the direction 1.780e-17, 1.777e-17, 2.419e-14 and
    // 2.412e-10 rad after 1, 10, 100 and 1000 years, the distance 7.570e-9, 7.570e-8, 7.570e-7 and 7.570e-6 relative
    // (2026-11-16). After 1 and 10 years the library and iauStarpm round the right ascension to the same double (a right
    // ascension near 4.7 rad resolves 8.9e-16 rad, which a one-ulp difference of a platform's atan2 would show); after 100
    // and 1000 years the direction is iauStarpm's light-time and relativistic terms of the observed place at rv = 0 (the
    // straight line of its Note 5 is the same), growing as dt⁴, the distance
    // iauStarpv's inertial radial velocity −v_t²/(2c) for an observed rv of 0, 7.5e-8 relative per decade for Barnard's star.
    // Before the fix (#718), Barnard's star fed its catalog μα* was 26.8 mas (1.3e-7 rad) away after 10 years, and fed
    // dα/dt 0.33 mas (1.6e-9 rad), 34 mas after 100 years and 9.4″ after 1000; the distance the catalog's
    internal static double StarpmDirectionTolerance(double years) => years switch
    {
        1.0 => 3.6e-17,
        10.0 => 3.6e-17,
        100.0 => 4.9e-14,
        _ => 4.9e-10
    };

    internal static double StarpmDistanceTolerance(double years) => years switch
    {
        1.0 => 1.6e-8,
        10.0 => 1.6e-7,
        100.0 => 1.6e-6,
        _ => 1.6e-5
    };

    /// <summary>
    /// #718, the reference: the catalog stars (Barnard's star from Hipparcos I/311 and Gaia DR3, Polaris from I/311) and
    /// stars at ±89.5° that cross a pole, 1, 10, 100 and 1000 Julian years of TDB from their catalog epoch, given their μα*
    /// as the catalogs give it, against SOFA's <c>iauStarpm</c> (rv = 0): the direction, and the distance in catalog
    /// distances. Before, the angles moved linearly and μα* was read as dα/dt.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_CatalogStars_AreIauStarpm()
    {
        var worstDirection = new Dictionary<double, double>();
        var worstDistance = new Dictionary<double, double>();
        int compared = 0;
        for (int i = 0; i < StarpmCases.Length; i++)
        {
            var row = StarpmCases[i];
            if (row.Status != 0)
            {
                continue;
            }

            // Arrange
            var (star, at) = StarOf(row, i);

            // Act
            var coordinates = star.GetEquatorialCoordinates(at);

            // Assert
            double direction = Separation(coordinates.ToCartesian(), row.Starpm);
            double distance = System.Math.Abs(coordinates.Distance / star.Distance / row.StarpmDistance - 1.0);
            _output.WriteLine($"MEASURE StarTests.CatalogStars_AreIauStarpm ({row.Label}, {row.Years:F0} yr): direction {direction:E3} rad, relative distance {distance:E3}");
            worstDirection[row.Years] = System.Math.Max(worstDirection.GetValueOrDefault(row.Years), direction);
            worstDistance[row.Years] = System.Math.Max(worstDistance.GetValueOrDefault(row.Years), distance);
            compared++;
        }

        Assert.Equal(20, compared);
        foreach (var years in worstDirection.Keys)
        {
            _output.WriteLine($"MEASURE StarTests.CatalogStars_AreIauStarpm ({years:F0} yr): worst direction {worstDirection[years]:E3} rad, relative distance {worstDistance[years]:E3}");
            Assert.True(worstDirection[years] <= StarpmDirectionTolerance(years), $"{years} yr: {worstDirection[years]:E3}");
            Assert.True(worstDistance[years] <= StarpmDistanceTolerance(years), $"{years} yr: {worstDistance[years]:E3}");
        }
    }

    // The record against the closed form of the straight line in long double: twice the worst on Linux x64, rounded up at
    // the second digit, provisional (to be set again from the Windows x64 and macOS arm64 CI logs): the direction 7.825e-16
    // rad, Barnard's star after 10 years, whose right ascension near 4.7 rad resolves 8.9e-16 rad and is reduced to
    // [0, 2π) by the double of 2π, 2.4e-16 below it; the length 2.220e-16 relative (2026-11-16). Before the fix (#718),
    // 2.213 rad and 0.993
    internal const double StraightLineDirectionTolerance = 1.6e-15;
    internal const double StraightLineLengthTolerance = 4.5e-16;

    /// <summary>
    /// #718: every star of <see cref="StarpmCases"/>, the stars at ±π/2 exactly (where <c>iauStarpm</c> cannot take
    /// μα*/cos δ) and the synthetic stars of the suite (faster than <c>iauStarpv</c>'s 0.5c, which it would freeze) among
    /// them, against the closed form of the straight line evaluated in long double: the direction, and the distance in
    /// catalog distances, √(1 + (μ dt)²).
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_IsTheStraightLine_OfALongDoubleClosedForm()
    {
        double worstDirection = 0.0;
        double worstLength = 0.0;
        for (int i = 0; i < StarpmCases.Length; i++)
        {
            // Arrange
            var row = StarpmCases[i];
            var (star, at) = StarOf(row, i);

            // Act
            var coordinates = star.GetEquatorialCoordinates(at);

            // Assert
            double direction = Separation(coordinates.ToCartesian(), row.StraightLine);
            double length = System.Math.Abs(coordinates.Distance / star.Distance / row.StraightLineLength - 1.0);
            _output.WriteLine($"MEASURE StarTests.IsTheStraightLine ({row.Label}, {row.Years:F0} yr): direction {direction:E3} rad, relative length {length:E3}");
            worstDirection = System.Math.Max(worstDirection, direction);
            worstLength = System.Math.Max(worstLength, length);
            Assert.InRange(coordinates.Declination, -System.Math.PI / 2.0, System.Math.PI / 2.0);
            Assert.InRange(coordinates.RightAscension, 0.0, System.Math.BitDecrement(2.0 * System.Math.PI));
        }

        _output.WriteLine($"MEASURE StarTests.IsTheStraightLine: worst direction {worstDirection:E3} rad, relative length {worstLength:E3}");
        Assert.True(worstDirection <= StraightLineDirectionTolerance, $"{worstDirection:E3}");
        Assert.True(worstLength <= StraightLineLengthTolerance, $"{worstLength:E3}");
    }

    // The record of the second difference of the positions at 0, dt and 2 dt, relative to the position at dt: twice the
    // worst on Linux x64, rounded up at the second digit, provisional (to be set again from the Windows x64 and macOS arm64
    // CI logs): 1.170e-15 (2026-11-16). Before the fix (#718), 3.725, the curvature of the linear angles
    internal const double OneLineTolerance = 2.4e-15;

    /// <summary>
    /// #718, the behaviour: a star moves along one line at constant speed, so its positions at t0, t0 + dt and t0 + 2 dt
    /// (t0 the catalog epoch and later) satisfy p(2) − p(1) = p(1) − p(0), relative to |p(1)|. Every star of
    /// <see cref="StarpmCases"/>, dt its horizon, from its catalog epoch and from one dt after it.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_PositionsLieOnOneLineAtConstantSpeed()
    {
        double worst = 0.0;
        for (int i = 0; i < StarpmCases.Length; i++)
        {
            // Arrange
            var row = StarpmCases[i];
            var (star, at) = StarOf(row, i);
            var step = at - star.Epoch;
            foreach (var start in new[] { star.Epoch, at })
            {
                // Act
                var p0 = star.GetEquatorialCoordinates(start).ToCartesian();
                var p1 = star.GetEquatorialCoordinates(start + step).ToCartesian();
                var p2 = star.GetEquatorialCoordinates(start + step + step).ToCartesian();

                // Assert
                double secondDifference = (p2 - p1 - (p1 - p0)).Magnitude() / p1.Magnitude();
                worst = System.Math.Max(worst, secondDifference);
                _output.WriteLine($"MEASURE StarTests.OneLineAtConstantSpeed ({row.Label}, {row.Years:F0} yr, from {(start == star.Epoch ? "the catalog epoch" : "dt")}): {secondDifference:E3}");
            }
        }

        _output.WriteLine($"MEASURE StarTests.OneLineAtConstantSpeed: worst {worst:E3}");
        Assert.True(worst <= OneLineTolerance, $"{worst:E3}");
    }

    /// <summary>
    /// #718: a star without proper motion at any epoch, and every star at its catalog epoch, is its catalog: its angles and
    /// its distance, bit for bit.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_WithoutProperMotion_IsTheCatalog_BitForBit()
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        var still = new Star(718100, "star718100", 1E+30, "spec", 2, 0.3792, new Equatorial(1.2, 4.5, catalog), 0, 0, 0, 0, 0, 0, catalog);
        foreach (var at in new[] { catalog, catalog.AddDays(-1000.0), catalog.AddDays(0.5), catalog + TimeSpan.FromDays(365250.0) })
        {
            // Act
            var coordinates = still.GetEquatorialCoordinates(at);

            // Assert
            Assert.Equal(1.2, coordinates.Declination);
            Assert.Equal(4.5, coordinates.RightAscension);
            Assert.Equal(still.Distance, coordinates.Distance);
        }

        for (int i = 0; i < StarpmCases.Length; i++)
        {
            var (star, _) = StarOf(StarpmCases[i], i);

            // Act
            var coordinates = star.GetEquatorialCoordinates(star.Epoch);

            // Assert
            Assert.Equal(StarpmCases[i].Declination, coordinates.Declination);
            Assert.Equal(StarpmCases[i].RightAscension, coordinates.RightAscension);
            Assert.Equal(star.Distance, coordinates.Distance);
        }
    }

    /// <summary>
    /// #718, the convention: Barnard's star built from its Gaia DR3 record (4472832130942575872, VizieR I/355, read on
    /// 2026-11-16, to be verified by S. Guillet) with the units converted and nothing else: ra and dec from degrees to
    /// radians, pmra (μα* = μα cos δ, as Gaia gives it) and pmdec from mas/yr to rad/yr, the parallax from mas to arcsec,
    /// the epoch J2016.0 TDB; against <c>iauStarpm</c> fed pmra/cos δ (its Note 4) and rv = 0. Before, pmra was read as
    /// dα/dt: 27.7 mas away after 10 years.
    /// </summary>
    [Fact]
    public void GetEquatorialCoordinates_FromAGaiaDr3Record_IsIauStarpm()
    {
        // Arrange: the record, converted as a caller converts it
        const double deg = System.Math.PI / 180.0;
        const double mas = System.Math.PI / 180.0 / 3600.0 / 1000.0;
        var epoch = TimeSystem.Time.CreateTDB(5844.0 * 86400.0);
        var star = new Star(718200, "Barnard", 1E+30, "M4V", 9.5, 546.9759 / 1000.0, new Equatorial(4.73942005111 * deg, 269.44850252544 * deg, epoch),
            10362.394 * mas, -801.551 * mas, 0, 0, 0, 0, epoch);
        var rows = StarpmCases.Where(x => x.Label == "Barnard DR3").ToArray();
        Assert.Equal(4, rows.Length);
        foreach (var row in rows)
        {
            // The doubles of the reference are those of the record
            Assert.Equal(row.RightAscension, star.EquatorialCoordinatesAtEpoch.RightAscension);
            Assert.Equal(row.Declination, star.EquatorialCoordinatesAtEpoch.Declination);
            Assert.Equal(row.RightAscensionProperMotion, star.RightAscensionProperMotion);
            Assert.Equal(row.DeclinationProperMotion, star.DeclinationProperMotion);
            Assert.Equal(row.Parallax, star.Parallax);
            Assert.Equal(row.EpochDays * 86400.0, star.Epoch.TimeSpanFromJ2000().TotalSeconds);
            var at = epoch + TimeSpan.FromTicks((long)System.Math.Round(row.Years * 365.25 * TimeSpan.TicksPerDay));

            // Act
            var coordinates = star.GetEquatorialCoordinates(at);

            // Assert
            double direction = Separation(coordinates.ToCartesian(), row.Starpm);
            double distance = System.Math.Abs(coordinates.Distance / star.Distance / row.StarpmDistance - 1.0);
            _output.WriteLine($"MEASURE StarTests.FromAGaiaDr3Record ({row.Years:F0} yr): direction {direction:E3} rad ({direction / mas:E3} mas), relative distance {distance:E3}");
            Assert.True(direction <= StarpmDirectionTolerance(row.Years), $"{row.Years} yr: {direction:E3}");
            Assert.True(distance <= StarpmDistanceTolerance(row.Years), $"{row.Years} yr: {distance:E3}");
        }
    }

    /// <summary>
    /// #718, the convention: the proper motion in right ascension is a true angle, μα* = (dα/dt) cos δ, so the same value
    /// moves a star on the equator, at 1 rad and at a pole (where dα/dt is undefined) by the same angle atan(μα* dt) along
    /// e_α = (−sin α0, cos α0, 0), the direction of increasing right ascension. The position is r0 (u0 + μα* dt e_α),
    /// written here. Before, the motion was cos δ0 times larger at 1 rad and none at a pole.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(System.Math.PI / 2.0)]
    [InlineData(-System.Math.PI / 2.0)]
    public void RightAscensionProperMotion_IsTheTrueAngleRate(double declination)
    {
        // Arrange
        var catalog = new TimeSystem.Time(2001, 1, 1);
        const double rightAscension = 2.0;
        const double motion = 1.0e-3;
        var star = new Star(718300, "star718300", 1E+30, "spec", 2, 0.3792, new Equatorial(declination, rightAscension, catalog), 0, motion, 0, 0, 0, 0,
            catalog);
        var at = catalog + TimeSpan.FromDays(365.25);
        var u0 = new Vector3(System.Math.Cos(declination) * System.Math.Cos(rightAscension), System.Math.Cos(declination) * System.Math.Sin(rightAscension),
            System.Math.Sin(declination));
        var east = new Vector3(-System.Math.Sin(rightAscension), System.Math.Cos(rightAscension), 0.0);
        var expected = (u0 + east * motion) * star.Distance;

        // Act
        var position = star.GetEquatorialCoordinates(at).ToCartesian();

        // Assert: the angle atan(μα* dt), and the position
        double angle = Separation(position, u0) - System.Math.Atan(motion);
        double relative = (position - expected).Magnitude() / expected.Magnitude();
        _output.WriteLine($"MEASURE StarTests.RightAscensionProperMotion_IsTheTrueAngleRate ({declination:R}): angle − atan(μα* dt) {angle:E3} rad, relative position {relative:E3}");
        Assert.True(System.Math.Abs(angle) <= TrueAngleTolerance, $"{angle:E3}");
        Assert.True(relative <= TrueAngleTolerance, $"{relative:E3}");
    }

    // The record of the true-angle motion against r0 (u0 + μα* dt e_α): twice the worst on Linux x64, rounded up at the
    // second digit, provisional (to be set again from the Windows x64 and macOS arm64 CI logs): the position 1.475e-16
    // relative, the angle 1.364e-16 rad (2026-11-16). Before the fix (#718), the motion was dα/dt: the position 4.597e-4 relative
    // at 1 rad and 1.000e-3 at a pole, where the star did not move
    internal const double TrueAngleTolerance = 3.0e-16;
}
