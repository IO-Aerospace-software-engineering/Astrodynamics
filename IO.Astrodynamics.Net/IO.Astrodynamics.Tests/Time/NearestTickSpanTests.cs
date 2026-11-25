using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The spans the library computes outside <see cref="TimeSystem.Time"/>, and <see cref="TimeSystem.Time.AddDays"/>,
/// <see cref="TimeSystem.Time.AddHours"/>, <see cref="TimeSystem.Time.AddMinutes"/> and
/// <see cref="TimeSystem.Time.CreateFromJD"/>, are the nearest tick of their value, ties to even, as the seconds of
/// <see cref="TimeSystem.Time"/> since #625 (#637, decided by S. Guillet, 2026-11-17). Before 11.0.0 they went through
/// <see cref="TimeSpan.FromSeconds(double)"/> and <see cref="System.DateTime.AddDays"/>, which truncate toward zero:
/// one tick early for about half of the values. The references are exact: the integer arithmetic of
/// <see cref="ExactTicks"/> on the double the formula gives, or the closed form evaluated in <see cref="decimal"/> (28
/// digits) from the double inputs (the period of an ellipse, the sidereal period from SPICE's angular velocity, the
/// rocket equation), a value counted only where it lies farther than <see cref="Margin"/> from a half tick, which the
/// rounding of the double formula (a few 1e-16 relative, 3e-4 tick on a sidereal day) cannot cross.
/// </summary>
public class NearestTickSpanTests
{
    // In ticks: the distance to a half tick below which a value of a closed form is not counted
    private const decimal Margin = 0.01m;

    private const decimal Pi = 3.1415926535897932384626433833m;

    private readonly ITestOutputHelper _output;

    public NearestTickSpanTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    #region Extended precision

    // The decimal of a double: its shortest round-trip string, within half an ulp of the double (1.1e-16 relative)
    private static decimal D(double value) =>
        decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static decimal Sqrt(decimal value)
    {
        decimal x = (decimal)System.Math.Sqrt((double)value);
        for (int i = 0; i < 8; i++)
        {
            x = (x + value / x) / 2m;
        }

        return x;
    }

    private static decimal Exp(decimal value)
    {
        decimal sum = 1m, term = 1m;
        for (int k = 1; k < 200 && term != 0m; k++)
        {
            term = term * value / k;
            sum += term;
        }

        return sum;
    }

    // The magnitude of a vector of doubles, in decimal
    private static decimal Norm(Vector3 v) => Sqrt(D(v.X) * D(v.X) + D(v.Y) * D(v.Y) + D(v.Z) * D(v.Z));

    // The nearest tick of seconds held in decimal, ties to even, or null within the margin of a half tick
    private static long? NearestTick(decimal seconds)
    {
        decimal ticks = seconds * TimeSpan.TicksPerSecond;
        decimal floor = decimal.Floor(ticks);
        if (System.Math.Abs(ticks - floor - 0.5m) < Margin)
        {
            return null;
        }

        return (long)decimal.Round(ticks, MidpointRounding.ToEven);
    }

    // The tick TimeSpan.FromSeconds gives, the truncation of 11.0.0's predecessors
    private static long Truncated(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    #endregion

    #region Time

    /// <summary>
    /// AddDays, AddHours and AddMinutes add the nearest tick of their span, ties to even, symmetric in sign: the exact
    /// integer reference of <see cref="ExactTicks"/> with the ticks per day, hour and minute. Red on fe72f8e, whose
    /// <see cref="System.DateTime"/> methods truncate (1/3 day: 287 999 999 999 ticks, not 288 000 000 000).
    /// </summary>
    [Fact]
    public void AddDaysHoursMinutes_AreTheNearestTick()
    {
        var values = new[] { 1.0 / 3.0, -1.0 / 3.0, 0.1, 2.0 / 3.0, 1.0 / 7.0, -1.0 / 7.0, 1e-9, 12345.678901234, 0.5e-7 / 3600.0 };
        var start = new TimeSystem.Time(2021, 6, 2, 3, 4, 5, TimeScale.TDB);
        int moved = 0;
        foreach (double value in values)
        {
            foreach (var (name, ticksPerUnit, add, truncate) in new (string Name, long TicksPerUnit, Func<double, TimeSystem.Time> Add, Func<double, DateTime> Truncate)[]
                     {
                         ("days", TimeSpan.TicksPerDay, start.AddDays, start.DateTime.AddDays),
                         ("hours", TimeSpan.TicksPerHour, start.AddHours, start.DateTime.AddHours),
                         ("minutes", TimeSpan.TicksPerMinute, start.AddMinutes, start.DateTime.AddMinutes)
                     })
            {
                long expected = ExactTicks.Nearest(value, ticksPerUnit);
                long actual = add(value).DateTime.Ticks - start.DateTime.Ticks;
                Assert.True(expected == actual, $"{name} {value:R}: {actual} ticks, the nearest {expected}");
                Assert.Equal(start.Scale, add(value).Scale);
                if (truncate(value).Ticks - start.DateTime.Ticks != expected)
                {
                    moved++;
                }
            }
        }

        _output.WriteLine($"MEASURE AddDays/AddHours/AddMinutes: {moved} of {values.Length * 3} spans one tick off by truncation");
        Assert.True(moved > 0);
    }

    /// <summary>
    /// CreateFromJD is the nearest tick of its days from J2000, exact by Sterbenz's lemma for these dates (JD within a
    /// factor 2 of J2000's): the exact integer reference of <see cref="ExactTicks"/> with the ticks per day. Red on
    /// fe72f8e, which truncates.
    /// </summary>
    [Fact]
    public void CreateFromJD_IsTheNearestTick()
    {
        var julianDates = new[] { 2459367.6, 2451545.0 + 1.0 / 3.0, 2460000.3, 2440587.5 + 1.0 / 7.0, 2415020.1, 2488069.9 };
        int moved = 0;
        foreach (double jd in julianDates)
        {
            double days = jd - 2451545.0;
            Assert.Equal(jd, days + 2451545.0);
            long expected = ExactTicks.Nearest(days, TimeSpan.TicksPerDay);
            foreach (TimeScale frame in new TimeScale[] { TimeScale.UTC, TimeScale.TDB })
            {
                var time = TimeSystem.Time.CreateFromJD(jd, frame);
                Assert.True(expected == time.DateTime.Ticks - TimeSystem.Time.J2000Calendar.Ticks, $"JD {jd:R}");
                Assert.Equal(frame, time.Scale);
            }

            if (TimeSystem.Time.J2000Calendar.AddDays(days).Ticks - TimeSystem.Time.J2000Calendar.Ticks != expected)
            {
                moved++;
            }
        }

        _output.WriteLine($"MEASURE CreateFromJD: {moved} of {julianDates.Length} dates one tick off by truncation");
        Assert.True(moved > 0);
    }

    #endregion

    /// <summary>
    /// The span of seconds the library computes is the exact nearest tick (<see cref="ExactTicks"/>), with the exceptions
    /// of <see cref="TimeSpan.FromSeconds(double)"/>, which it replaces: <see cref="ArgumentException"/> for NaN,
    /// <see cref="OverflowException"/> for an infinity or a span outside a <see cref="TimeSpan"/>.
    /// </summary>
    [Fact]
    public void SecondsToSpan_IsTheExactNearestTick()
    {
        var random = new Random(637);
        int moved = 0, count = 0;
        for (int i = 0; i < 20000; i++)
        {
            double seconds = (random.NextDouble() - 0.5) * System.Math.Pow(10.0, random.Next(-8, 11));
            long expected = ExactTicks.Nearest(seconds);
            Assert.True(expected == TimeSystem.Time.SecondsToSpan(seconds).Ticks, $"{seconds:R} s");
            count++;
            if (TimeSpan.FromSeconds(seconds).Ticks != expected)
            {
                moved++;
            }
        }

        Assert.Equal(0L, TimeSystem.Time.SecondsToSpan(0.0).Ticks);
        Assert.Equal(2L, TimeSystem.Time.SecondsToSpan(2.5e-7).Ticks);
        Assert.Equal(-2L, TimeSystem.Time.SecondsToSpan(-2.5e-7).Ticks);
        Assert.Equal(ExactTicks.Nearest(922337203684.0), TimeSystem.Time.SecondsToSpan(922337203684.0).Ticks);
        Assert.Throws<ArgumentException>(() => TimeSystem.Time.SecondsToSpan(double.NaN));
        Assert.Throws<ArgumentException>(() => TimeSpan.FromSeconds(double.NaN));
        foreach (double outside in new[] { double.PositiveInfinity, double.NegativeInfinity, 1e12, -1e12, 922337203685.0 })
        {
            Assert.Throws<OverflowException>(() => TimeSystem.Time.SecondsToSpan(outside));
        }

        Assert.Throws<OverflowException>(() => TimeSpan.FromSeconds(1e12));
        Assert.Throws<OverflowException>(() => TimeSpan.FromSeconds(double.PositiveInfinity));
        _output.WriteLine($"MEASURE SecondsToSpan: {moved} of {count} spans one tick off by TimeSpan.FromSeconds");
        Assert.True(moved > 0);
    }

    /// <summary>
    /// The uniform step of the sampled close-approach search, a sixteenth of the sample step here, is its nearest tick:
    /// for a sample step of 16 k ticks, exactly k ticks (integer reference). Red on fe72f8e (a private method, which
    /// truncated (16 k / 1e7 / 16) × 1e7, one tick short for some k).
    /// </summary>
    [Fact]
    public void CloseApproachSearchStep_IsTheNearestTick()
    {
        int moved = 0;
        for (long k = 1; k <= 5000; k++)
        {
            long sampleTicks = 16 * (37_500_000 + 7919 * k);
            var sampleStep = TimeSpan.FromTicks(sampleTicks);
            var step = IO.Astrodynamics.SSA.ConjunctionAssessment.ComputeCloseApproachSearchStep(sampleStep, TimeSpan.FromDays(1.0), 1e-6);
            Assert.Equal(sampleTicks / 16, step.Ticks);
            if (TimeSpan.FromSeconds(sampleStep.TotalSeconds / 16.0).Ticks != sampleTicks / 16)
            {
                moved++;
            }
        }

        _output.WriteLine($"MEASURE ComputeCloseApproachSearchStep: {moved} of 5000 steps one tick short by truncation");
        Assert.True(moved > 0);
    }

    #region Periods

    /// <summary>
    /// The period of an ellipse is the nearest tick of 2π √(a³/μ), the closed form in decimal from the double a and μ.
    /// Red on fe72f8e, which truncates.
    /// </summary>
    [Fact]
    public void Period_IsTheNearestTickOfTheClosedForm()
    {
        var earth = TestHelpers.EarthAtJ2000;
        decimal mu = D(earth.GM);
        int counted = 0, moved = 0;
        for (int k = 0; k < 24; k++)
        {
            double a = 6800000.0 + 12345.0 * k;
            var orbit = new KeplerianElements(a, 0.01, 0.2, 0.1, 0.3, 0.4, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            decimal am = D(a);
            long? expected = NearestTick(2m * Pi * Sqrt(am * am * am / mu));
            if (expected is null)
            {
                continue;
            }

            counted++;
            Assert.True(expected == orbit.Period().Ticks, $"a = {a:R} m: {orbit.Period().Ticks} ticks, the nearest {expected}");
            if (Truncated(IO.Astrodynamics.Constants.TwoPi * System.Math.Sqrt(a * a * a / earth.GM)) != expected)
            {
                moved++;
            }
        }

        _output.WriteLine($"MEASURE Period: {moved} of {counted} periods one tick off by truncation");
        Assert.True(counted >= 20);
        Assert.True(moved > 0);
    }

    /// <summary>
    /// The sidereal rotation period is the nearest tick of 2π/|ω|, ω the angular velocity of the body-fixed frame in
    /// ICRF from SPICE, the closed form in decimal; the true solar day adds the nearest tick of the angle/|ω| of its
    /// formula. Red on fe72f8e, which truncates both.
    /// </summary>
    [Fact]
    public void SiderealPeriodAndTrueSolarDay_AreTheNearestTick()
    {
        var earth = TestHelpers.Earth;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        int counted = 0, moved = 0, solarCounted = 0, solarMoved = 0;
        for (int day = 0; day < 40; day++)
        {
            var epoch = new TimeSystem.Time(2021, 1, 1, TimeScale.TDB).AddSeconds(day * 9.1e5 + 1234.5);
            foreach (var body in new[] { earth, moon })
            {
                var omega = body.GetOrientation(Frames.Frame.ICRF, epoch).AngularVelocity;
                decimal w = Sqrt(D(omega.X) * D(omega.X) + D(omega.Y) * D(omega.Y) + D(omega.Z) * D(omega.Z));
                long? expected = NearestTick(2m * Pi / w);
                var sidereal = body.SideralRotationPeriod(epoch);
                if (expected is not null)
                {
                    counted++;
                    Assert.True(expected == sidereal.Ticks, $"{body.Name} at {epoch}: {sidereal.Ticks} ticks, the nearest {expected}");
                    if (Truncated(IO.Astrodynamics.Constants.TwoPi / omega.Magnitude()) != expected)
                    {
                        moved++;
                    }
                }

                if (body != earth)
                {
                    continue;
                }

                // The true solar day: the sidereal period plus the angle the Sun moves over it, at the rate |ω|
                var eph0 = body.GetEphemeris(epoch, Stars.SUN_BODY, Frames.Frame.ECLIPTIC_J2000, Aberration.LT).Position;
                var eph1 = body.GetEphemeris(epoch + sidereal, Stars.SUN_BODY, Frames.Frame.ECLIPTIC_J2000, Aberration.LT).Position;
                double angle = eph0.Angle(eph1);
                long? extra = NearestTick(D(angle) / w);
                if (extra is null)
                {
                    continue;
                }

                solarCounted++;
                var solarDay = body.TrueSolarDay(epoch);
                Assert.True(sidereal.Ticks + extra == solarDay.Ticks, $"true solar day at {epoch}: {solarDay.Ticks} ticks, the nearest {sidereal.Ticks + extra}");
                if (Truncated(angle / omega.Magnitude()) != extra)
                {
                    solarMoved++;
                }
            }
        }

        _output.WriteLine($"MEASURE SideralRotationPeriod: {moved} of {counted} periods one tick off by truncation");
        _output.WriteLine($"MEASURE TrueSolarDay: {solarMoved} of {solarCounted} angle spans one tick off by truncation");
        Assert.True(counted >= 70 && solarCounted >= 35);
        Assert.True(moved > 0 && solarMoved > 0);
    }

    /// <summary>
    /// The period of the elements SPICE converts a state to is the nearest tick of OSCLTX's period, the closed form
    /// 2π √(a³/μ) in decimal from their semi-major axis and the GM of the observer within the margin. Red on fe72f8e.
    /// </summary>
    [Fact]
    public void SpiceElementsPeriod_IsTheNearestTick()
    {
        var earth = TestHelpers.EarthAtJ2000;
        decimal mu = D(earth.GM);
        int counted = 0, moved = 0;
        for (int k = 0; k < 24; k++)
        {
            var orbit = new KeplerianElements(6900000.0 + 23456.0 * k, 0.02, 0.5, 0.1, 0.3, 0.4 + 0.1 * k, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var elements = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(orbit.ToStateVector());
            decimal a = D(elements.SemiMajorAxis());
            long? expected = NearestTick(2m * Pi * Sqrt(a * a * a / mu));
            if (expected is null)
            {
                continue;
            }

            counted++;
            Assert.True(expected == elements.Period().Ticks, $"a = {elements.SemiMajorAxis():R} m: {elements.Period().Ticks} ticks, the nearest {expected}");
            if (Truncated((double)(2m * Pi * Sqrt(a * a * a / mu))) != expected)
            {
                moved++;
            }
        }

        _output.WriteLine($"MEASURE SPICE elements period: {moved} of {counted} periods one tick off by truncation");
        Assert.True(counted >= 20);
        Assert.True(moved > 0);
    }

    #endregion

    #region Maneuvers

    /// <summary>
    /// The thrust duration of the rocket equation is the nearest tick of m0/q (1 − exp(−Δv/(Isp g0))), the closed form
    /// in decimal (its exponential by its series). Red on fe72f8e, which truncates.
    /// </summary>
    [Fact]
    public void ThrustDuration_IsTheNearestTickOfTheRocketEquation()
    {
        double isp = 450.0, initialMass = 1900.0, fuelFlow = 50.0;
        int counted = 0, moved = 0;
        for (int k = 1; k <= 30; k++)
        {
            double deltaV = 7.3 * k * k;
            decimal exact = D(initialMass) / D(fuelFlow) * (1m - Exp(-D(deltaV) / (D(isp) * D(IO.Astrodynamics.Constants.g0))));
            long? expected = NearestTick(exact);
            if (expected is null)
            {
                continue;
            }

            counted++;
            var duration = Tsiolkovski.DeltaT(isp, initialMass, fuelFlow, deltaV);
            Assert.True(expected == duration.Ticks, $"Δv = {deltaV:R} m/s: {duration.Ticks} ticks, the nearest {expected}");
            if (Truncated(initialMass / fuelFlow * (1 - System.Math.Exp(-deltaV / (isp * IO.Astrodynamics.Constants.g0)))) != expected)
            {
                moved++;
            }
        }

        _output.WriteLine($"MEASURE Tsiolkovski.DeltaT: {moved} of {counted} durations one tick off by truncation");
        Assert.True(counted >= 28);
        Assert.True(moved > 0);
    }

    /// <summary>
    /// The hold of a phasing maneuver is the nearest tick of 0.9 n t2, t2 the period of the phasing orbit of its formula
    /// from the state at execution (<c>PhasingManeuver.Execute</c>), by the exact integer reference of
    /// <see cref="ExactTicks"/>. Red on fe72f8e, which truncates.
    /// </summary>
    [Fact]
    public void PhasingHold_IsTheNearestTick()
    {
        var earth = TestHelpers.EarthAtJ2000;
        int moved = 0, count = 0;
        for (int k = 0; k < 12; k++)
        {
            for (uint revolutions = 1; revolutions <= 3; revolutions++)
            {
                var orbit = new KeplerianElements(7000000.0 + 1111.0 * k, 0.01, 0.1, 0.0, 0.0, 0.0, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
                var spacecraft = new Spacecraft(-1637, "Phasing1637", 1000.0, orbit, 3000.0, new Clock("Clk1637", 65536));
                spacecraft.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
                spacecraft.AddEngine(new Engine("eng", 450, 50, spacecraft.FuelTanks.First(), "engmk1", "12345"));
                var target = new KeplerianElements(7000000.0 + 1111.0 * k, 0.01, 0.1, 0.0, 0.0, 0.3 + 0.05 * k, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
                var maneuver = new PhasingManeuver(target,
                    revolutions,
                    spacecraft.Engines.First(),
                    new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB));
                var state = orbit.ToStateVector();
                maneuver.TryExecute(state);

                // The formula of PhasingManeuver.Execute on the state relative to its centre
                var local = state.RelativeTo(maneuver.ManeuverCenter, Aberration.None).ToStateVector();
                double targetTrueLongitude = target.ToStateVector(local.Epoch).TrueLongitude();
                double deltaTrueAnomaly = targetTrueLongitude - local.TrueLongitude();
                if (deltaTrueAnomaly < 0.0)
                {
                    deltaTrueAnomaly += IO.Astrodynamics.Constants.TwoPi;
                }

                double e = local.Eccentricity();
                double eccentricAnomaly = IO.Astrodynamics.OrbitalParameters.OrbitalParameters.EllipticAnomaly(deltaTrueAnomaly, e);
                double t1 = local.Period().TotalSeconds;
                double t = t1 / IO.Astrodynamics.Constants.TwoPi * (eccentricAnomaly - e * System.Math.Sin(eccentricAnomaly));
                double t2 = t1 - t / revolutions;
                double hold = t2 * revolutions * 0.9;

                long expected = ExactTicks.Nearest(hold);
                Assert.True(expected == maneuver.HoldDuration.Ticks,
                    $"a = {orbit.SemiMajorAxis():R} m, {revolutions} revolutions: {maneuver.HoldDuration.Ticks} ticks, the nearest {expected}");
                count++;
                if (Truncated(hold) != expected)
                {
                    moved++;
                }
            }
        }

        _output.WriteLine($"MEASURE PhasingManeuver hold: {moved} of {count} holds one tick off by truncation");
        Assert.True(moved > 0);
    }

    /// <summary>
    /// After the first launch window, each window is the nearest tick of π/|ω₀| ± 2 δL/ω after the previous one
    /// (<c>Launch.FindLaunchWindows</c>): half the sidereal period at the first window, ω₀ SPICE's angular velocity of the
    /// body there, plus the arc of the site's latitude, δL = asin(tan φ/tan i), at the rate ω = 2π/P of the body at the
    /// previous window (<see cref="CelestialBody.AngularVelocity"/>, P its sidereal period to the tick), the whole offset
    /// rounded once. The reference evaluates π/|ω₀| and δL P/π in decimal from SPICE's angular velocities (read through
    /// <see cref="CelestialBody.GetOrientation"/>, independently of the code's periods).
    /// Red on ea398b3, which halved the nearest tick of the sidereal period by integer division (a tick short of the
    /// nearest tick of half the period for about a quarter of the periods) and rounded the two terms apart.
    /// </summary>
    [Fact]
    public void FollowingLaunchWindows_AreTheNearestTick()
    {
        var epoch = new TimeSystem.Time(2021, 6, 2, TimeScale.TDB);
        var earth = TestHelpers.Earth;
        var planetodetic = new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0);
        var site = new LaunchSite(33, "l1", earth, planetodetic, default, new AzimuthRange(0.0, 6.0));
        var recoverySite = new Site(34, "l2", earth, planetodetic);
        var target = new StateVector(new Vector3(-1.144243683349977E+06, 4.905086030671815E+06, 4.553416875193589E+06),
            new Vector3(-5.588288926819989E+03, -4.213222250603758E+03, 3.126518392859475E+03), earth, epoch, Frames.Frame.ICRF);
        var launch = new Launch(site, recoverySite, target, IO.Astrodynamics.Constants.CivilTwilight, null);
        double deltaL = System.Math.Asin(System.Math.Tan(site.Planetodetic.Latitude) / System.Math.Tan(target.Inclination()));
        int moved = 0, count = 0;
        for (int day = 0; day < 90; day++)
        {
            // A search of one day, as LaunchTests: its first window by bisection, the next ones by the offsets
            var windows = launch.FindLaunchWindows(new Window(epoch.AddSeconds(day * 86400.0 + day * 1234.567), TimeSpan.FromDays(1.0))).ToArray();
            if (windows.Length < 2)
            {
                continue;
            }

            var omega0 = earth.GetOrientation(Frames.Frame.ICRF, windows[0].Window.StartDate).AngularVelocity;
            decimal halfRotation = Pi / Norm(omega0);
            long halfTicks = (long)decimal.Round(halfRotation * TimeSpan.TicksPerSecond, MidpointRounding.ToEven);
            for (int i = 1; i < windows.Length; i++)
            {
                var previous = windows[i - 1].Window.StartDate;
                long actual = (windows[i].Window.StartDate - previous).Ticks;

                // The arc at the rate of the body at the previous window, 2π over its sidereal period (a whole number of
                // ticks, the nearest of 2π/|ω| from SPICE): 2 δL / (2π / P) = δL P / π
                var omega = earth.GetOrientation(Frames.Frame.ICRF, previous).AngularVelocity;
                long? periodTicks = NearestTick(2m * Pi / Norm(omega));
                if (periodTicks is null)
                {
                    continue;
                }

                decimal arc = D(deltaL) * periodTicks.Value / TimeSpan.TicksPerSecond / Pi;

                // The sign alternates with the node of the window; the arc (about 3.3 h here) tells it apart
                long sign = actual > halfTicks ? 1 : -1;
                long? expected = NearestTick(halfRotation + sign * arc);
                if (expected is null)
                {
                    continue;
                }

                Assert.True(expected == actual, $"day {day}, window {i}: an offset of {actual} ticks, the nearest {expected}");
                count++;

                // Before 11.0.0: half the truncated period by integer division, plus the truncated arc at the rate of the
                // truncated period
                double formerRate = IO.Astrodynamics.Constants.TwoPi / TimeSpan.FromSeconds(IO.Astrodynamics.Constants.TwoPi / omega.Magnitude()).TotalSeconds;
                long former = Truncated(IO.Astrodynamics.Constants.TwoPi / omega0.Magnitude()) / 2 + sign * Truncated(deltaL * 2.0 / formerRate);
                if (former != expected)
                {
                    moved++;
                }
            }
        }

        _output.WriteLine($"MEASURE Launch: {moved} of {count} offsets one tick off by the former truncations");
        Assert.True(count >= 40);
        Assert.True(moved > 0);
    }

    #endregion
}
