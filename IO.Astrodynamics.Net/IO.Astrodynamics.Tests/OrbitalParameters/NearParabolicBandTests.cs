// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The near-parabolic band, |e − 1| &lt; 1e-6, where the library takes the anomalies of a parabola (Barker's equation,
/// #368): the semi-latus rectum of <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToEquinoctial"/>
/// (#462), the mean anomaly of a true anomaly (#478), parabolic equinoctial elements without a perigee radius (#477) and
/// the conic's semi-major axis and mean motion with Barker's motion (#459). The references are closed forms at 60
/// digits on the doubles given to the library (<c>tools/near_parabolic/near_parabolic_reference.py</c>, mpmath 1.4.1
/// and spiceypy 8.4.0 on gm_de440.tpc), a value as a pair (hi, lo) of doubles whose sum is the 60-digit value.
/// </summary>
public class NearParabolicBandTests
{
    // An epoch tagged TT: about the Earth the two-body motion counts TT seconds (#501, #508), so +600 s here is 600 s of
    // the motion, the time of the references
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeFrame.TDTFrame);
    private const double Mu = 398600435507022.6;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public NearParabolicBandTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        Assert.Equal(Mu, _earth.GM);
    }

    private void Measure(string what, double value) => _output.WriteLine($"MEASURE {what}: {value:E3}");

    private StateVector State(double[] r, double[] v) =>
        new(new Vector3(r[0], r[1], r[2]), new Vector3(v[0], v[1], v[2]), _earth, Epoch, Frames.Frame.ICRF);

    // h²/μ of a state, from its components: the closed form, not the library's conversions
    private static double SemiLatusRectumOf(StateVector sv)
    {
        var r = sv.Position;
        var v = sv.Velocity;
        double hx = r.Y * v.Z - r.Z * v.Y;
        double hy = r.Z * v.X - r.X * v.Z;
        double hz = r.X * v.Y - r.Y * v.X;
        return (hx * hx + hy * hy + hz * hz) / Mu;
    }

    #region The states of the band (#478, #459)

    /// <summary>
    /// A state of the band, q = 7000 km, i = 30°, Ω = 40°, ω = 0, ν0 = 0.5 rad, built at 60 digits and rounded to doubles,
    /// with the references of its exact conic: e and q, ν0, Barker's time to ν = 1 rad, to the double radius at ν = 1 rad
    /// and to 2e7 m (√(2q³/μ) ΔM, the band model), the conic's a = −μ/(2ξ) and n = √(μ/|a|³), its exact two-body state
    /// 600 s later, and the position of the band model 600 s later (Barker's anomaly at Barker's rate, the conic's p).
    /// </summary>
    private sealed record BandState(double EccentricityMinusOne, double[] R, double[] V, double E, double Q, double Nu0Hi,
        double Nu0Lo, double TimeToNu1Hi, double TimeToNu1Lo, double RadiusAtNu1, double TimeToR1Hi, double TimeToR1Lo,
        double TimeTo2e7Hi, double TimeTo2e7Lo, double AHi, double ALo, double NHi, double NLo, double[] ConicR600,
        double[] ConicV600, double[] BandModelR600);

    private static readonly BandState[] BandStates =
    {
        new(-9e-07, new[] { 3022712.978827644, 6577710.563477488, 1787393.3961055127 }, new[] { -7536.673624729877, 5002.081834552712, 5009.262649628231 }, 0.9999990999999998, 6999999.999999999, 0.5000000000000001, -7.90058854244684e-18,
            445.7214440077845, 2.3121511062130555e-14, 9089123.652188776, 445.7214440077845, 1.6552751579726227e-14, 2552.2585813216256, -2.7638394189592477e-14,
            7777777775849.168, 0.00025715800022454055, 9.204190288298098e-13, 1.4232657918025233e-29, new[] { -1726868.3535889704, 8577588.819387313, 4434525.706828881 }, new[] { -7935.00818791347, 1951.116951134403, 3807.7215838147417 }, new[] { -1726868.4936726545, 8577588.853832098, 4434525.774049941 }),
        new(-5e-07, new[] { 3022713.0182435443, 6577710.649250231, 1787393.4194129587 }, new[] { -7536.674059189769, 5002.082750299571, 5009.263215875031 }, 0.9999994999999999, 7000000.0, 0.4999999999999999, -1.3392355868807667e-17,
            445.7214440077847, 2.3353488701011568e-14, 9089124.194712382, 445.7214440077847, 1.0930319806456158e-15, 2552.257121899323, 1.443134678622322e-13,
            13999999997605.965, 0.0007617428192711686, 3.811332437055924e-13, -1.4155614888313326e-29, new[] { -1726868.554456251, 8577589.523180204, 4434526.092644141 }, new[] { -7935.008576817969, 1951.1181435562714, 3807.7222555220237 }, new[] { -1726868.6322805488, 8577589.542316213, 4434526.129989194 }),
        new(-1e-12, new[] { 3022713.0675132996, 6577710.7564659, 1787393.4485471956 }, new[] { -7536.67460226353, 5002.083894980668, 5009.263923682028 }, 0.999999999999, 7000000.0, 0.5000000000000001, 2.2893671923504517e-17,
            445.72144400778456, -3.772433353186757e-15, 9089124.872865317, 445.7214440077845, -1.3656728867605846e-14, 2552.2552976284355, 2.1579064317604538e-13,
            7.000446983145129e+18, 438.2739591184537, 1.0779043587689419e-21, 5.797399423910541e-38, new[] { -1726868.8055398676, 8577590.402919369, 4434526.574912175 }, new[] { -7935.009062947617, 1951.1196340802992, 3807.7230951543015 }, new[] { -1726868.8055400234, 8577590.402919406, 4434526.574912249 }),
        new(1e-12, new[] { 3022713.067513497, 6577710.756466328, 1787393.448547312 }, new[] { -7536.674602265703, 5002.083894985247, 5009.263923684859 }, 1.000000000001, 7000000.0, 0.4999999999999999, 6.698638793340091e-18,
            445.7214440077847, 1.8161489767963067e-14, 9089124.872868031, 445.7214440077849, 1.4634123457112462e-14, 2552.2552976211387, 1.7108613163523074e-13,
            -6.999543461347844e+18, 121.10533646075723, 1.0781130741338164e-21, -4.27116638403931e-38, new[] { -1726868.805540872, 8577590.402922887, 4434526.574914103 }, new[] { -7935.009062949563, 1951.1196340862605, 3807.72309515766 }, new[] { -1726868.8055407163, 8577590.402922848, 4434526.574914029 }),
        new(5e-07, new[] { 3022713.1167832296, 6577710.863681947, 1787393.4776815353 }, new[] { -7536.675145339446, 5002.085039666136, 5009.264631491756 }, 1.0000005, 7000000.0, 0.49999999999999994, 6.5562118814838666e-18,
            445.7214440077847, -2.374936784225188e-16, 9089125.551020728, 445.72144400778467, 9.676300271325106e-15, 2552.2534733539655, 1.4221236428348514e-13,
            -13999999997301.438, 0.00041118925915454923, 3.8113324371802805e-13, -6.131900396934986e-30, new[] { -1726869.05662451, 8577591.282661838, 4434527.057182049 }, new[] { -7935.009549079209, 1951.1211246099224, 3807.723934789774 }, new[] { -1726868.9788001392, 8577591.263525784, 4434527.01983695 }),
        new(9e-07, new[] { 3022713.156199078, 6577710.949454577, 1787393.5009889507 }, new[] { -7536.675579799297, 5002.085955412529, 5009.265197738335 }, 1.0000009, 7000000.0, 0.4999999999999999, -7.234819915321706e-18,
            445.7214440077848, -8.11882090880411e-15, 9089126.0935438, 445.72144400778456, 1.8603449180690334e-14, 2552.2520139399826, 3.658378749392438e-14,
            -7777777777532.525, 0.00023342897249723345, 9.20419028530998e-13, 8.82357147199345e-29, new[] { -1726869.2574918373, 8577591.986454254, 4434527.442997117 }, new[] { -7935.009937983703, 1951.122317030973, 3807.7246064966916 }, new[] { -1726869.1174079173, 8577591.952009322, 4434527.375775903 })
    };

    public static TheoryData<int> BandStateIndices => new() { 0, 1, 2, 3, 4, 5 };

    private StateVector BandStateVector(int index) => State(BandStates[index].R, BandStates[index].V);

    // Barker's mean anomaly D + D³/3 of a true anomaly, D = tan(ν/2), at 60 digits (hi, lo)
    private static readonly (double Nu, double Hi, double Lo)[] BarkerMeanAnomalies =
    {
        (-1.0, -0.6006498288743456, 2.0239346816021216e-17),
        (0.5, 0.2608913094738398, -2.714079837584985e-17),
        (1.0, 0.6006498288743456, -2.0239346816021216e-17),
        (1.5, 1.2010986097824399, 8.941625657435429e-19),
    };

    private static double Distance(Vector3 a, Vector3 b) => (a - b).Magnitude();

    #endregion

    #region #478: the mean anomaly of a true anomaly in the band

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue478_MeanAnomalyOfATrueAnomaly_IsBarkers(int index)
    {
        // Arrange: the state, its Keplerian and its equinoctial elements
        var sv = BandStateVector(index);
        Assert.True(sv.IsParabolic());
        var forms = new (string Name, IO.Astrodynamics.OrbitalParameters.OrbitalParameters Orbit)[]
            { ("state", sv), ("Keplerian", sv.ToKeplerianElements()), ("equinoctial", sv.ToEquinoctial()) };

        foreach (var (name, orbit) in forms)
        {
            foreach (var (nu, hi, lo) in BarkerMeanAnomalies)
            {
                // Act
                double m = orbit.MeanAnomaly(nu);

                // Assert: Barker's D + D³/3, whatever the side of e = 1 (before: D − e sin D or e sinh D − D, 0.0028 rad
                // for 0.26 at ν = 0.5)
                double error = m - hi - lo;
                Measure($"#478 e − 1 = {BandStates[index].EccentricityMinusOne:R} {name} ν = {nu:R}, M error (rad)", error);
                // Measured at most 2.8e-17 rad on Linux (the rounding of tan(ν/2) and of the cube); provisional, twice,
                // until the Windows x64 and macOS arm64 CI logs
                Assert.InRange(error, -5.5e-17, 5.5e-17);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue478_EquinoctialMeanAnomaly_IsBarkersAtItsTrueAnomaly(int index)
    {
        // Arrange
        var sv = BandStateVector(index);
        var eq = sv.ToEquinoctial();
        double d = System.Math.Tan(0.5 * BandStates[index].Nu0Hi);

        // Act
        double m = eq.MeanAnomaly();

        // Assert: the mean anomaly of its own true anomaly, Barker's of the state's ν0 (before: Kepler's equation on
        // Barker's D, 3565 km off on the way back to a state)
        Assert.Equal(eq.MeanAnomaly(eq.TrueAnomaly()), m);
        double error = m - (d + d * d * d / 3.0);
        Measure($"#478 e − 1 = {BandStates[index].EccentricityMinusOne:R} equinoctial M error (rad)", error);
        // Measured at most 2.3e-16 rad on Linux (one ulp of M near 0.26 rad, through ν0 of the elements); provisional,
        // twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(error, -4.5e-16, 4.5e-16);
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue478_TheStateAtItsOwnTrueAnomaly_IsTheState(int index)
    {
        // Arrange
        var sv = BandStateVector(index);
        var c = BandStates[index];

        // Act
        var atNu0 = sv.ToStateVector(sv.TrueAnomaly());
        var atNu1 = sv.ToStateVector(1.0);

        // Assert: at the epoch of the state, and at Barker's time to ν = 1 rad (before: 338.6 s in the past, 3565 km off)
        double offset = (atNu0.Epoch - Epoch).TotalSeconds;
        double toNu1 = (atNu1.Epoch - Epoch).TotalSeconds - c.TimeToNu1Hi - c.TimeToNu1Lo;
        double distance = Distance(atNu0.Position, sv.Position);
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} ToStateVector(ν0): epoch offset (s)", offset);
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} ToStateVector(ν0): |Δr| (m)", distance);
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} ToStateVector(1 rad): epoch − Barker's (s)", toNu1);
        // At its own epoch the state is itself (StateVector.ToStateVector(Time), #551)
        Assert.Equal(0.0, offset);
        Assert.Equal(0.0, distance);
        // The epoch is a tick of Time: measured at most 7.8e-9 s on Linux, Barker's time rounded to its tick; provisional,
        // twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(toNu1, -1.6e-8, 1.6e-8);
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue478_TimeToRadius_IsBarkersTime(int index)
    {
        // Arrange
        var sv = BandStateVector(index);
        var c = BandStates[index];

        // Act
        double toR1 = (sv.TimeToRadius(c.RadiusAtNu1) - Epoch).TotalSeconds - c.TimeToR1Hi - c.TimeToR1Lo;
        double to2e7 = (sv.TimeToRadius(2e7) - Epoch).TotalSeconds - c.TimeTo2e7Hi - c.TimeTo2e7Lo;

        // Sphere of influence: Barker's closed form in doubles from the 60-digit e and q of the state
        double soi = _earth.SphereOfInfluence;
        double p = c.Q * (1.0 + c.E);
        double nuSoi = System.Math.Acos((p / soi - 1.0) / c.E);
        double dSoi = System.Math.Tan(0.5 * nuSoi), d0 = System.Math.Tan(0.5 * c.Nu0Hi);
        double barkerSoi = System.Math.Sqrt(2.0 * c.Q * c.Q * c.Q / Mu) *
                           (dSoi + dSoi * dSoi * dSoi / 3.0 - (d0 + d0 * d0 * d0 / 3.0));
        double toSoi = (sv.TimeToSphereOfInfluence() - Epoch).TotalSeconds - barkerSoi;

        // Assert: Barker's time, the band model (before: −307 s for a radius reached 446 s later; 7616 s for the sphere
        // of influence reached in 671 491 s)
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} TimeToRadius(r at 1 rad) − Barker's (s)", toR1);
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} TimeToRadius(2e7 m) − Barker's (s)", to2e7);
        Measure($"#478 e − 1 = {c.EccentricityMinusOne:R} TimeToSphereOfInfluence − Barker's (s)", toSoi);
        // The epochs are ticks of Time. Measured on Linux at most 7.8e-9 s to the radius at 1 rad (Barker's time rounded to
        // its tick), 4.6e-8 s to 2e7 m and 4.6e-8 s to the sphere of influence (with the rounding of the anomalies, and of
        // the closed form in doubles for the sphere of influence); provisional, twice, until the Windows x64 and macOS
        // arm64 CI logs
        Assert.InRange(toR1, -1.6e-8, 1.6e-8);
        Assert.InRange(to2e7, -9.3e-8, 9.3e-8);
        Assert.InRange(toSoi, -9.2e-8, 9.2e-8);
    }

    [Theory]
    [InlineData(-0.3, new[] { 7179482.60497222, 1407113.6027229016, -2042071.1048295994 }, new[] { -3276.908251108003, 8368.7016299537, 4917.380809165369 })]
    [InlineData(0.3, new[] { 2632440.456275306, 6826067.419109745, 2042071.1048295994 }, new[] { -7672.533101637674, 4680.334439080567, 4917.380809165369 })]
    public void Issue478_AnExactParabolaThroughEquinoctialElements_KeepsItsMeanAnomaly(double m, double[] r, double[] v)
    {
        // Arrange: an exact parabola, whose equinoctial e = √(F² + G²) rounds one ulp off 1
        var ke = new KeplerianElements(double.PositiveInfinity, 1.0, 0.5235987755982988, 0.6981317007977318, 0.0, m, _earth,
            Epoch, Frames.Frame.ICRF, perigeeRadius: 7000000.0);
        var eq = ke.ToEquinoctial();

        // Act
        var back = eq.ToKeplerianElements();
        var state = back.ToStateVector();
        var atEpoch = eq.ToStateVector(Epoch);

        // Assert: M back, and Barker's state at 60 digits (before: 5.1e7 m off at M = −0.3, 4.1e6 m at +0.3)
        double mError = back.M - m;
        double rError = Distance(state.Position, new Vector3(r[0], r[1], r[2]));
        double rErrorAtEpoch = Distance(atEpoch.Position, new Vector3(r[0], r[1], r[2]));
        Measure($"#478 exact parabola M = {m:R}: e − 1 of the equinoctial elements", eq.Eccentricity() - 1.0);
        Measure($"#478 exact parabola M = {m:R}: M error (rad)", mError);
        Measure($"#478 exact parabola M = {m:R}: |Δr| (m), ToKeplerianElements and ToStateVector(Epoch)", System.Math.Max(rError, rErrorAtEpoch));
        // Measured at most 1.7e-16 rad and 2.3e-9 m on Linux; provisional, twice, until the Windows x64 and macOS arm64
        // CI logs
        Assert.InRange(mError, -3.4e-16, 3.4e-16);
        Assert.InRange(System.Math.Max(rError, rErrorAtEpoch), 0.0, 4.5e-9);
    }

    [Fact]
    public void Issue478_ExactParabolas_RandomSweep()
    {
        // Arrange: exact parabolas built in doubles, the seed fixed. Rounding moves their e off 1, into the band
        var random = new Random(478);
        double worstOwnNu = 0.0, worstRoundTrip = 0.0, worstKeplerian = 0.0, worstM = 0.0;
        for (int n = 0; n < 2000; n++)
        {
            double q = 6600e3 + random.NextDouble() * (50000e3 - 6600e3);
            double nu = -2.5 + 5.0 * random.NextDouble();
            double i = 0.01 + random.NextDouble() * 3.1, node = random.NextDouble() * 6.28, w = random.NextDouble() * 6.28;
            var sv = ParabolaState(q, i, node, w, nu);

            // Act, assert: the state at its own true anomaly, at its epoch; the equinoctial round trip
            var own = sv.ToStateVector(sv.TrueAnomaly());
            Assert.Equal(Epoch, own.Epoch);
            worstOwnNu = System.Math.Max(worstOwnNu, Distance(own.Position, sv.Position) / sv.Position.Magnitude());
            var back = sv.ToEquinoctial().ToKeplerianElements().ToStateVector();
            worstRoundTrip = System.Math.Max(worstRoundTrip, Distance(back.Position, sv.Position) / sv.Position.Magnitude());

            // An exact parabola given as Keplerian elements, through its equinoctial elements
            double m = -3.0 + 6.0 * random.NextDouble();
            var ke = new KeplerianElements(double.PositiveInfinity, 1.0, i, node, w, m, _earth, Epoch, Frames.Frame.ICRF,
                perigeeRadius: q);
            var keBack = ke.ToEquinoctial().ToKeplerianElements();
            var expected = ke.ToStateVector();
            worstKeplerian = System.Math.Max(worstKeplerian,
                Distance(keBack.ToStateVector().Position, expected.Position) / expected.Position.Magnitude());
            worstM = System.Math.Max(worstM, System.Math.Abs(keBack.M - m));
        }

        Measure("#478 sweep, ToStateVector(own ν), worst |Δr| / |r|", worstOwnNu);
        Measure("#478 sweep, equinoctial round trip, worst |Δr| / |r|", worstRoundTrip);
        Measure("#478 sweep, Keplerian through equinoctial, worst |Δr| / |r|", worstKeplerian);
        Measure("#478 sweep, Keplerian through equinoctial, worst |ΔM| (rad)", worstM);
        // At its own epoch a state is itself (#551). Measured on Linux 5.9e-15 for the round trip, 4.0e-15 and 1.3e-14 rad
        // for the Keplerian elements (before: up to 5.1e8 m, more than 1 km off for every state whose e rounding moved off
        // 1); provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.Equal(0.0, worstOwnNu);
        Assert.InRange(worstRoundTrip, 0.0, 1.2e-14);
        Assert.InRange(worstKeplerian, 0.0, 7.9e-15);
        Assert.InRange(worstM, 0.0, 2.5e-14);
    }

    // The state of an exact parabola, p = 2q, at the true anomaly ν, built in doubles
    private StateVector ParabolaState(double q, double i, double node, double w, double nu)
    {
        double p = 2.0 * q;
        double r = p / (1.0 + System.Math.Cos(nu));
        double speed = System.Math.Sqrt(Mu / p);
        double x = r * System.Math.Cos(nu), y = r * System.Math.Sin(nu);
        double vx = -speed * System.Math.Sin(nu), vy = speed * (1.0 + System.Math.Cos(nu));
        double co = System.Math.Cos(node), so = System.Math.Sin(node), ci = System.Math.Cos(i), si = System.Math.Sin(i);
        double cw = System.Math.Cos(w), sw = System.Math.Sin(w);
        double[] px = [co * cw - so * sw * ci, so * cw + co * sw * ci, sw * si];
        double[] py = [-co * sw - so * cw * ci, -so * sw + co * cw * ci, cw * si];
        return State([x * px[0] + y * py[0], x * px[1] + y * py[1], x * px[2] + y * py[2]],
            [vx * px[0] + vy * py[0], vx * px[1] + vy * py[1], vx * px[2] + vy * py[2]]);
    }

    [Theory]
    [InlineData(-2e-6)]
    [InlineData(2e-6)]
    public void Issue478_OutsideTheBand_TheMeanAnomalyIsUnchanged(double eccentricityMinusOne)
    {
        // Kepler's equation of the side of e = 1, bit for bit
        var ke = new KeplerianElements(7000000.0 / -eccentricityMinusOne, 1.0 + eccentricityMinusOne, 0.5, 0.7, 0.0, 0.2,
            _earth, Epoch, Frames.Frame.ICRF);
        Assert.False(ke.IsParabolic());
        foreach (double nu in new[] { -1.0, 0.5, 1.0, 1.5 })
        {
            Assert.Equal(IO.Astrodynamics.OrbitalParameters.OrbitalParameters.TrueAnomalyToMeanAnomaly(nu, ke.E, ke.EccentricAnomaly(nu)),
                ke.MeanAnomaly(nu));
        }
    }

    #endregion

    #region #459: the conic's semi-major axis and mean motion, Barker's motion

    // The periapsis states of #462 with their conic: a = −μ/(2ξ) and n = √(μ/|a|³) at 60 digits (hi, lo), and the exact
    // two-body position 600 s later
    private static readonly (double Vy, double EccentricityMinusOne, double AHi, double ALo, double NHi, double NLo, double[] ConicR600)[]
        PeriapsisConics =
    {
        (10671.729487052813, -5.0e-7, 13999999998506.668, 0.00011686965500757134, 3.811332436688116e-13, -2.4544534186066316e-29,
            [5701340.518665373, 6030128.912406198, 0.0]),
        (10671.7308, -7.87848e-9, 888496454486998.6, 0.05359163239942072, 7.538506884282065e-16, -4.441791851760456e-32,
            [5701340.566816242, 6030129.680142079, 0.0]),
        (10671.732154985518, 5.0e-7, -13999999997212.982, -0.0007535038149700652, 3.811332437216401e-13, 2.282128073184565e-29,
            [5701340.61650882, 6030130.472459559, 0.0]),
    };

    public static TheoryData<int> PeriapsisConicIndices => new() { 0, 1, 2 };

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue459_StatesOfTheBand_HaveTheConicsSemiMajorAxisAndMeanMotion(int index)
    {
        var c = BandStates[index];
        AssertConic(BandStateVector(index), c.EccentricityMinusOne.ToString("R"), System.Math.Abs(c.EccentricityMinusOne), c.AHi,
            c.ALo, c.NHi, c.NLo);
    }

    [Theory]
    [MemberData(nameof(PeriapsisConicIndices))]
    public void Issue459_PeriapsisStatesOfTheBand_HaveTheConicsSemiMajorAxisAndMeanMotion(int index)
    {
        var c = PeriapsisConics[index];
        AssertConic(State([7000000.0, 0.0, 0.0], [0.0, c.Vy, 0.0]), $"periapsis vy = {c.Vy:R}",
            System.Math.Abs(c.EccentricityMinusOne), c.AHi, c.ALo, c.NHi, c.NLo);
    }

    private void AssertConic(StateVector sv, string name, double distanceToOne, double aHi, double aLo, double nHi, double nLo)
    {
        // Act
        Assert.True(sv.IsParabolic());
        double a = sv.SemiMajorAxis();
        double n = sv.MeanMotion();

        // Assert: −μ/(2ξ) and √(μ/|a|³), the conic of every e ≠ 1 (before: +Infinity and Barker's √(μ/(2q³)), 2e9 times n
        // at e − 1 = ±5e-7)
        double aError = (a - aHi - aLo) / aHi;
        double nError = (n - nHi - nLo) / nHi;
        Measure($"#459 {name}: a relative error", aError);
        Measure($"#459 {name}: n relative error", nError);
        // The specific energy ξ = v²/2 − μ/r cancels near e = 1: its rounding is about ε v²/(2|ξ|) ∝ ε / |e − 1| relative,
        // 6e-10 at |e − 1| = 5e-7 and 6e-4 at 1e-12, so the errors are bounded scaled by |e − 1|. Measured at most 6.2e-16
        // for a and 9.3e-16 for n, times |e − 1|, on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Measure($"#459 {name}: a relative error × |e − 1|", aError * distanceToOne);
        Measure($"#459 {name}: n relative error × |e − 1|", nError * distanceToOne);
        Assert.InRange(aError * distanceToOne, -1.3e-15, 1.3e-15);
        Assert.InRange(nError * distanceToOne, -1.9e-15, 1.9e-15);
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue459_RoundTripsOfTheBand_ReturnTheState(int index)
    {
        // Arrange
        var sv = BandStateVector(index);
        var c = BandStates[index];

        // Act: the paths through the Keplerian elements, which took p = 2q in the band
        var paths = new (string Name, StateVector State)[]
        {
            ("ToKeplerianElements().ToStateVector()", sv.ToKeplerianElements().ToStateVector()),
            ("ToEquinoctial().ToKeplerianElements().ToStateVector()", sv.ToEquinoctial().ToKeplerianElements().ToStateVector()),
            ("AtEpoch(epoch).ToStateVector()", sv.AtEpoch(Epoch).ToStateVector()),
            ("ToEquinoctial().ToStateVector(epoch + 0)", sv.ToEquinoctial().ToStateVector(Epoch)),
        };

        // Assert: the state, to rounding (before: |r| |e − 1| / (1 + e), 1.9 m at ±5e-7 and 3.4 m at ±9e-7)
        foreach (var (name, state) in paths)
        {
            double error = Distance(state.Position, sv.Position) / sv.Position.Magnitude();
            Measure($"#459 e − 1 = {c.EccentricityMinusOne:R} {name}: |Δr| / |r|", error);
            // Measured at most 6.0e-16 on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
            Assert.InRange(error, 0.0, 1.2e-15);
        }
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue459_TheMotionOfTheBand_IsBarkersAnomalyOnTheConic(int index)
    {
        // Arrange
        var sv = BandStateVector(index);
        var c = BandStates[index];

        // Act: 600 s of TT later
        var later = sv.ToStateVector(Epoch.AddSeconds(600.0));

        // Assert: the band model at 60 digits, Barker's anomaly at Barker's rate on the conic of p = q (1 + e), to rounding;
        // and its distance to the exact conic, the model error of the band (the issue's comment, item 3 (c))
        var model = new Vector3(c.BandModelR600[0], c.BandModelR600[1], c.BandModelR600[2]);
        var conic = new Vector3(c.ConicR600[0], c.ConicR600[1], c.ConicR600[2]);
        double modelError = Distance(later.Position, model) / model.Magnitude();
        double conicError = Distance(later.Position, conic);
        Measure($"#459 e − 1 = {c.EccentricityMinusOne:R} +600 s: |Δr| / |r| to the band model", modelError);
        Measure($"#459 e − 1 = {c.EccentricityMinusOne:R} +600 s: |Δr| to the exact conic (m)", conicError);
        // Measured at most 6.1e-16 to the band model and 0.16 m to the conic on Linux (1.8e-7 m at |e − 1| = 1e-12;
        // before: 2.0 to 5.1 m, p = 2q); provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(modelError, 0.0, 1.3e-15);
        Assert.InRange(conicError, 0.0, 0.32);
    }

    [Theory]
    [MemberData(nameof(PeriapsisConicIndices))]
    public void Issue459_TheMotionFromThePeriapsis_IsNearTheConic(int index)
    {
        var c = PeriapsisConics[index];
        var sv = State([7000000.0, 0.0, 0.0], [0.0, c.Vy, 0.0]);
        double error = Distance(sv.ToStateVector(Epoch.AddSeconds(600.0)).Position,
            new Vector3(c.ConicR600[0], c.ConicR600[1], c.ConicR600[2]));
        Measure($"#459 periapsis vy = {c.Vy:R} +600 s: |Δr| to the exact conic (m)", error);
        // The model error of the band, 0.54 m at |e − 1| = 5e-7 from the periapsis (the reference script), 8.6e-3 m at
        // 7.9e-9: measured at most 0.55 m on Linux (before: 2.3 m, p = 2q); provisional, twice, until the Windows x64 and
        // macOS arm64 CI logs
        Assert.InRange(error, 0.0, 1.1);
    }

    [Theory]
    [InlineData(0.999999, 6999999999798.71, null, false, new[] { -1243914014995.374, -3939707795523.814, -2409833141200.765 })]
    [InlineData(0.9999990000000001, 7000000000575.866, 7000000.0, true, new[] { -2084400.6272309453, 5446055.565423589, 4867313.5769885015 })]
    [InlineData(1.000001, -7000000000575.866, 7000000.0, true, new[] { -2084400.804620014, 5446056.028900098, 4867313.991212241 })]
    public void Issue459_TheEdgesOfTheBand_AsKeplerianElements(double e, double a, double? q, bool inBand, double[] r)
    {
        // Arrange: q = 7000 km, a = q / (1 − e) in doubles, i = 0.7, Ω = 0.5, ω = 0.9, M = 0.3. In doubles the band starts at
        // the double after 0.999999, which is outside it, and the double nearest 1.000001 is inside it
        Assert.Equal(System.Math.Abs(e - 1.0) < 1e-6, inBand);

        // Act: a finite a with the perigee radius in the band (before: ArgumentException, a = +Infinity required)
        var ke = new KeplerianElements(a, e, 0.7, 0.5, 0.9, 0.3, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius: q);
        var state = ke.ToStateVector();

        // Assert: the state of the elements at 60 digits: Kepler's elliptic equation outside the band, Barker's anomaly and
        // the conic's p = q (1 + e) inside it
        Assert.Equal(inBand, ke.IsParabolic());
        Assert.Equal(a, ke.SemiMajorAxis());
        double error = Distance(state.Position, new Vector3(r[0], r[1], r[2])) / new Vector3(r[0], r[1], r[2]).Magnitude();
        Measure($"#459 edge e = {e:R}: |Δr| / |r|", error);
        // Measured on Linux 1.7e-12 at e = 0.999999, outside the band, near the apoapsis (#472: the cancellations of
        // p = a(1 − e²) and 1 + e cos ν, its own issue), and at most 2.7e-16 inside; provisional, twice, until the Windows
        // x64 and macOS arm64 CI logs
        Assert.InRange(error, 0.0, inBand ? 5.3e-16 : 3.3e-12);

        // In the band the perigee radius is still required
        if (inBand)
        {
            Assert.Throws<ArgumentException>(() => new KeplerianElements(a, e, 0.7, 0.5, 0.9, 0.3, _earth, Epoch, Frames.Frame.ICRF));
        }
    }

    [Fact]
    public void Issue459_AParabola_KeepsItsInfiniteSemiMajorAxisAndBarkersMeanMotion()
    {
        // e = 1 exactly: a = +Infinity, n = √(μ/(2q³)), as before, bit for bit
        var ke = new KeplerianElements(double.PositiveInfinity, 1.0, 0.2, 0.3, 0.4, 0.5, _earth, Epoch, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        Assert.True(double.IsPositiveInfinity(ke.SemiMajorAxis()));
        Assert.Equal(System.Math.Sqrt(IO.Astrodynamics.Constants.G * _earth.Mass / (2.0 * System.Math.Pow(6800000.0, 3))), ke.MeanMotion());

        // A finite a at e = 1 exactly has no meaning: refused
        Assert.Throws<ArgumentException>(() => new KeplerianElements(7e12, 1.0, 0.2, 0.3, 0.4, 0.5, _earth, Epoch,
            Frames.Frame.ICRF, perigeeRadius: 6800000.0));
    }

    // The distance of a and q to one conic, |q/a − (1 − e)| in units of ε (1 + e): a conic has a (1 − e) = q exactly
    private static double ConicMismatch(double a, double e, double q) =>
        System.Math.Abs(q / a - (1.0 - e)) / (2.220446049250313e-16 * (1.0 + e));

    [Theory]
    [InlineData(7000000.0)]
    [InlineData(-7000000.0)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-14000000000000.0)]
    [InlineData(1.000001)]
    public void Issue459_InTheBand_ASemiMajorAxisOfAnotherConic_IsRefused(double a)
    {
        // Arrange: e = 1 − 5e-7 and q = 7000 km, whose conic has a = q / (1 − e) = 1.4e13 m: the rows give the a of a 7000 km
        // circle, its opposite, the parabola's +Infinity (the only a the band took before #459), the a of the other side of
        // e = 1, and (1.000001 as a factor) the conic's a in doubles off by 1e-6 relative, 5.0e-13 on q/a against a rounding
        // of about 1e-16 (the review of #459: a85098d refused them, the branch built them with the values of the circle)
        const double e = 1.0 - 5e-7;
        const double q = 7000000.0;
        double semiMajorAxis = a == 1.000001 ? q / (1.0 - e) * a : a;

        // Act, assert: a and q of two conics, refused, naming the semi-major axis
        var exception = Assert.Throws<ArgumentException>(() => new KeplerianElements(semiMajorAxis, e, 0.5, 0.3, 0.2, 0.3,
            _earth, Epoch, Frames.Frame.ICRF, perigeeRadius: q));
        Assert.Equal("semiMajorAxis", exception.ParamName);
    }

    [Fact]
    public void Issue459_InTheBand_TheElementsOfTheLibraryAreOneConic()
    {
        // Arrange: the states of the band, the periapsis states, and exact parabolas whose state comes back one ulp off e = 1
        var states = new System.Collections.Generic.List<(string Name, StateVector State)>();
        for (int i = 0; i < BandStates.Length; i++)
        {
            states.Add(($"e − 1 = {BandStates[i].EccentricityMinusOne:R}", BandStateVector(i)));
        }

        foreach (var c in PeriapsisConics)
        {
            states.Add(($"periapsis vy = {c.Vy:R}", State([7000000.0, 0.0, 0.0], [0.0, c.Vy, 0.0])));
        }

        foreach (double m in new[] { -2.0, 0.0, 0.3, 5.0 })
        {
            states.Add(($"parabola M = {m:R}", new KeplerianElements(double.PositiveInfinity, 1.0, 0.5, 0.7, 0.9, m, _earth, Epoch,
                Frames.Frame.ICRF, perigeeRadius: 6800000.0).ToStateVector()));
        }

        foreach (var (name, sv) in states)
        {
            // Act: every path that builds Keplerian elements of the band from a state (the constructor checks a against q)
            var paths = new (string Path, KeplerianElements Elements)[]
            {
                ("ToKeplerianElements()", sv.ToKeplerianElements()),
                ("ToEquinoctial().ToKeplerianElements()", sv.ToEquinoctial().ToKeplerianElements()),
                ("AtEpoch(+600 s)", (KeplerianElements)sv.AtEpoch(Epoch.AddSeconds(600.0))),
                ("SpiceAPI.ConvertStateVectorToConicOrbitalElement", SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv)),
            };

            // Assert: a (1 − e) = q to rounding, the rule of the constructor (|q/a − (1 − e)| ≤ 6 ε (1 + e))
            foreach (var (path, ke) in paths)
            {
                Assert.True(ke.IsParabolic(), $"{name} {path}");
                double mismatch = ConicMismatch(ke.SemiMajorAxis(), ke.E, ke.PerigeeRadius());
                Measure($"#459 {name} {path}: |q/a − (1 − e)| / (ε (1 + e))", mismatch);
                Assert.InRange(mismatch, 0.0, 6.0);
            }
        }
    }

    [Fact]
    public void Issue459_InTheBand_TheElementsOfASweepOfStatesAreOneConic()
    {
        // Arrange: 6400 states of the band, from elements of |e − 1| from 0 to 9.99e-7 either side, q from 6600 km to 1 AU,
        // |M| up to 2e6 rad and random orientations (a fixed seed), the measurement behind the tolerance of the constructor
        var random = new Random(12345);
        double[] distances = [0.0, 1e-16, 2.2e-16, 4.4e-16, 1e-15, 1e-14, 1e-13, 1e-12, 1e-11, 1e-10, 1e-9, 1e-8, 1e-7, 5e-7, 9e-7, 9.99e-7];
        double[] perigeeRadii = [6.6e6, 7e6, 4.2e7, 3.8e8, 1.5e11];
        double worstState = 0.0, worstSpice = 0.0;
        int count = 0;
        foreach (double distance in distances)
        foreach (double sign in new[] { -1.0, 1.0 })
        foreach (double q in perigeeRadii)
        {
            for (int k = 0; k < 40; k++)
            {
                double e = 1.0 + sign * distance;
                double m = (random.NextDouble() - 0.5) * 2.0 * System.Math.Pow(10.0, random.Next(-2, 7));
                var elements = new KeplerianElements(e == 1.0 ? double.PositiveInfinity : q / (1.0 - e), e, random.NextDouble() * 3.0,
                    random.NextDouble() * 6.0, random.NextDouble() * 6.0, m, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius: q);
                var sv = elements.ToStateVector();
                sv = new StateVector(sv.Position, sv.Velocity, _earth, Epoch, Frames.Frame.ICRF);
                if (!sv.IsParabolic())
                {
                    continue;
                }

                // Act: the elements of the state and SPICE's (oscltx: −μ/(2ξ) and its perifocal distance)
                count++;
                var fromState = sv.ToKeplerianElements();
                var fromSpice = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv);
                worstState = System.Math.Max(worstState, ConicMismatch(fromState.SemiMajorAxis(), fromState.E, fromState.PerigeeRadius()));
                worstSpice = System.Math.Max(worstSpice, ConicMismatch(fromSpice.SemiMajorAxis(), fromSpice.E, fromSpice.PerigeeRadius()));
            }
        }

        // Assert: every state of the band is built (the constructor checks the same rule); measured at most 2.8 and 3.0 ε (1 + e)
        // on Linux, the tolerance of the constructor twice that, provisional until the Windows x64 and macOS arm64 CI logs
        Measure($"#459 sweep of {count} states: ToKeplerianElements() |q/a − (1 − e)| / (ε (1 + e))", worstState);
        Measure($"#459 sweep of {count} states: SPICE |q/a − (1 − e)| / (ε (1 + e))", worstSpice);
        Assert.Equal(6400, count);
        Assert.InRange(worstState, 0.0, 6.0);
        Assert.InRange(worstSpice, 0.0, 6.0);
    }

    [Theory]
    [MemberData(nameof(PeriapsisConicIndices))]
    public void Issue459_SpiceConversionInTheBand_HasTheConicsSemiMajorAxisAndMeanMotion(int index)
    {
        // The public SpiceAPI.ConvertStateVectorToConicOrbitalElement forced a = +Infinity in the band, so Barker's n,
        // 2e9 times the conic's at ±5e-7 (the review of #459); SPICE's oscltx gives −μ/(2ξ)
        var c = PeriapsisConics[index];
        var sv = State([7000000.0, 0.0, 0.0], [0.0, c.Vy, 0.0]);
        var ke = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv);
        double aError = (ke.SemiMajorAxis() - c.AHi - c.ALo) / c.AHi;
        double nError = (ke.MeanMotion() - c.NHi - c.NLo) / c.NHi;
        double distanceToOne = System.Math.Abs(c.EccentricityMinusOne);
        Measure($"#459 SPICE periapsis vy = {c.Vy:R}: a relative error × |e − 1|", aError * distanceToOne);
        Measure($"#459 SPICE periapsis vy = {c.Vy:R}: n relative error × |e − 1|", nError * distanceToOne);
        // The bound of the state's own a and n (AssertConic): the cancellation of ξ, scaled by |e − 1|
        Assert.InRange(aError * distanceToOne, -1.3e-15, 1.3e-15);
        Assert.InRange(nError * distanceToOne, -1.9e-15, 1.9e-15);
    }

    [Theory]
    [MemberData(nameof(BandStateIndices))]
    public void Issue459_KeplersEquationForTheConic_AgainstBarkersLaw(int index)
    {
        // The measurement the decision of #459 asked for (#467's solvers inside the band): Kepler's equation for the conic,
        // solved in doubles by KeplerEquation from the state's a, e and ν, against the band's Barker's anomaly at Barker's
        // rate, both 600 s later, to the exact conic at 60 digits (tools/near_parabolic)
        var c = BandStates[index];
        var sv = BandStateVector(index);
        var conic = new Vector3(c.ConicR600[0], c.ConicR600[1], c.ConicR600[2]);

        // Act: the conic's elliptic or hyperbolic anomaly, advanced at √(μ/|a|³)
        double a = sv.SemiMajorAxis();
        double e = sv.Eccentricity();
        double nu = sv.TrueAnomaly();
        double n = System.Math.Sqrt(Mu / System.Math.Pow(System.Math.Abs(a), 3));
        var pHat = sv.EccentricityVector().Normalize();
        var qHat = sv.SpecificAngularMomentum().Normalize().Cross(pHat);
        Vector3 solved;
        if (e < 1.0)
        {
            double e0 = 2.0 * System.Math.Atan2(System.Math.Sqrt(1.0 - e) * System.Math.Sin(nu / 2.0), System.Math.Sqrt(1.0 + e) * System.Math.Cos(nu / 2.0));
            double e1 = KeplerEquation.EllipticAnomaly(e0 - e * System.Math.Sin(e0) + n * 600.0, e);
            solved = pHat * (a * (System.Math.Cos(e1) - e)) + qHat * (a * System.Math.Sqrt(1.0 - e * e) * System.Math.Sin(e1));
        }
        else
        {
            double h0 = 2.0 * System.Math.Atanh(System.Math.Sqrt((e - 1.0) / (e + 1.0)) * System.Math.Tan(nu / 2.0));
            double h1 = KeplerEquation.HyperbolicAnomaly(e * System.Math.Sinh(h0) - h0 + n * 600.0, e);
            solved = pHat * (-a * (e - System.Math.Cosh(h1))) + qHat * (-a * System.Math.Sqrt(e * e - 1.0) * System.Math.Sinh(h1));
        }

        double keplerError = Distance(solved, conic);
        double barkerError = Distance(sv.ToStateVector(Epoch.AddSeconds(600.0)).Position, conic);
        Measure($"#459 e − 1 = {c.EccentricityMinusOne:R} +600 s: Kepler's equation for the conic, |Δr| (m)", keplerError);
        Measure($"#459 e − 1 = {c.EccentricityMinusOne:R} +600 s: Barker's law, |Δr| (m)", barkerError);

        // Assert: neither holds over the whole band, the reason Barker's law is kept (decided by S. Guillet, 2026-11-17):
        // Kepler's equation is the better at the edges (|e − 1| = 5e-7 and 9e-7), Barker's law near e = 1 (1e-12), where
        // the conic's a(cos E − e) cancels
        if (System.Math.Abs(c.EccentricityMinusOne) >= 5e-7)
        {
            Assert.True(keplerError < barkerError, $"{keplerError} m, {barkerError} m");
        }
        else
        {
            Assert.True(barkerError < keplerError, $"{barkerError} m, {keplerError} m");
        }
    }

    #endregion

    #region #477: parabolic equinoctial elements without a perigee radius

    // Case 1 of the issue (the input of #469, section 9 (b)): e = 1, q = P / 2 = 7000 km
    private EquinoctialElements Case1(double? perigeeRadius = null) =>
        new(14e6, 1.0, 0.0, 0.1, 0.05, 0.5, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius);

    // Case 2: F² + G² = 1 in doubles, q = P / 2 = 6 878 137 m
    private EquinoctialElements Case2(double? perigeeRadius = null) =>
        new(13756274.0, 0.6, 0.8, -0.2, 0.3, 2.0, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius);

    private static double RelativeDistance(StateVector state, double[] r) =>
        Distance(state.Position, new Vector3(r[0], r[1], r[2])) / new Vector3(r[0], r[1], r[2]).Magnitude();

    [Fact]
    public void Issue477_AFreshInstance_HasItsPerigeeRadiusAndKeplerianElements()
    {
        // Arrange: no perigee radius, a fresh instance for each call
        foreach (var (name, build, q, nHi, nLo, angles) in new (string, Func<EquinoctialElements>, double, double, double, double[])[]
                 {
                     ("case 1", () => Case1(), 7000000.0, 0.0007622664872156607, -4.563587792784953e-21,
                         [0.2226820286819278, 0.4636476090008061, 5.81953769817878, 0.2608913094738398]),
                     ("case 2", () => Case2(), 6878137.0, 0.0007826140740306223, 3.212510836361077e-20,
                         [0.6920938617784603, 2.1587989303424644, 5.051681594838734, 0.6645139575840776]),
                 })
        {
            // Act
            double perigeeRadius = build().PerigeeRadius();
            double meanMotion = build().MeanMotion();
            var ke = build().ToKeplerianElements();

            // Assert: q = P / (1 + e) exactly (before: the cached h²/μ/(1 + e) of the state, 2 ulp above, or nothing on a
            // fresh instance, where ToKeplerianElements threw), Barker's n = √(μ/(2q³)), and the Keplerian elements
            Assert.Equal(q, perigeeRadius);
            Measure($"#477 {name} MeanMotion relative error", (meanMotion - nHi - nLo) / nHi);
            // Measured at most 4.1e-17 on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
            Assert.InRange((meanMotion - nHi - nLo) / nHi, -8.3e-17, 8.3e-17);
            Assert.True(double.IsPositiveInfinity(ke.A));
            Assert.Equal(q, ke.PerigeeRadius());
            double[] actual = [ke.I, ke.RAAN, ke.AOP, ke.M];
            for (int k = 0; k < 4; k++)
            {
                Measure($"#477 {name} angle {k} error (rad)", actual[k] - angles[k]);
                // Measured at most 1.2e-16 rad on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
                Assert.InRange(actual[k] - angles[k], -2.3e-16, 2.3e-16);
            }
        }
    }

    [Fact]
    public void Issue477_AFreshInstance_MovesInTime()
    {
        // Arrange: the 60-digit motion of the parabola (Barker's equation at q = P / (1 + e))
        double[] r0 = [6546595.968654342, 3568802.005525925, 59848.915685823675];
        double[] r600 = [4257611.299089934, 8735251.710614737, 1338014.3921153967];
        double[] r3600 = [-10891598.240740746, 21999559.196883645, 5558553.58324132];
        double[] r2At0 = [-4155698.622213448, 8276711.556639796, -939385.5739400573];
        double[] r2At5400 = [-29871818.463815868, 3209062.704096721, 19125822.98465613];

        // Act: each call on a fresh instance (before: ArgumentException from the KeplerianElements constructor)
        var errors = new (string Name, double Error)[]
        {
            ("case 1 AtEpoch(t0)", RelativeDistance(Case1().AtEpoch(Epoch).ToStateVector(), r0)),
            ("case 1 AtEpoch(t0 + 600 s)", RelativeDistance(Case1().AtEpoch(Epoch.AddSeconds(600.0)).ToStateVector(), r600)),
            ("case 1 ToStateVector(t0 + 3600 s)", RelativeDistance(Case1().ToStateVector(Epoch.AddSeconds(3600.0)), r3600)),
            ("case 2 AtEpoch(t0)", RelativeDistance(Case2().AtEpoch(Epoch).ToStateVector(), r2At0)),
            ("case 2 ToStateVector(t0 + 5400 s)", RelativeDistance(Case2().ToStateVector(Epoch.AddSeconds(5400.0)), r2At5400)),
        };
        double nuError = Case1().TrueAnomaly(Epoch.AddSeconds(600.0)) - 1.129382094161676 + 2.903015728754372e-17;

        // Assert
        foreach (var (name, error) in errors)
        {
            Measure($"#477 {name}, |Δr| / |r|", error);
            // Measured at most 8.8e-16 on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
            Assert.InRange(error, 0.0, 1.8e-15);
        }

        Measure("#477 case 1 TrueAnomaly(t0 + 600 s) error (rad)", nuError);
        // Measured 2.9e-17 rad on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(nuError, -5.9e-17, 5.9e-17);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Issue477_APropagation_StartsFromTheElements(bool rk78)
    {
        // Arrange: a spacecraft whose initial orbit is case 1 without a perigee radius
        var spacecraft = new Spacecraft(-1477, "BAND477", 100.0, 1000.0, new Clock("band477", 65536), Case1());
        var window = new Window(Epoch, Epoch.AddSeconds(60.0));
        var bodies = new CelestialItem[] { _earth };

        // Act (before: ArgumentException from the constructor of the propagator)
        using var propagator = rk78
            ? new IO.Astrodynamics.Propagator.CentralBodyPropagator(window, spacecraft,
                new IO.Astrodynamics.Propagator.Integrators.RK78Integrator(1e-12, 1e-12), bodies, false, false,
                TimeSpan.FromSeconds(60.0))
            : new IO.Astrodynamics.Propagator.CentralBodyPropagator(window, spacecraft, bodies, false, false,
                TimeSpan.FromSeconds(10.0));
        var first = propagator.Propagate().Segments[0].Steps[0];

        // Assert: the state of the elements at the start
        double[] r0 = [6546595.968654342, 3568802.005525925, 59848.915685823675];
        double error = Distance(first.StartPosition, new Vector3(r0[0], r0[1], r0[2])) / new Vector3(r0[0], r0[1], r0[2]).Magnitude();
        Measure($"#477 propagation start ({(rk78 ? "RK7(8)" : "Velocity-Verlet")}), |Δr| / |r|", error);
        // Measured 2.9e-16 on Linux, both integrators; provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(error, 0.0, 5.8e-16);
    }

    [Fact]
    public void Issue477_TheResults_DoNotDependOnTheOrderOfTheCalls()
    {
        // Arrange: the calls that cached a perigee radius on #469 before ToKeplerianElements could run
        var later = Epoch.AddSeconds(600.0);
        var firstCalls = new (string Name, Action<EquinoctialElements> Call)[]
        {
            ("none", _ => { }), ("PerigeeRadius", e => e.PerigeeRadius()), ("MeanMotion", e => e.MeanMotion()),
            ("ToStateVector()", e => e.ToStateVector()), ("ToKeplerianElements(Time)", e => e.ToKeplerianElements(later)),
            ("ToStateVector(ν)", e => e.ToStateVector(1.0)), ("EpochAtMeanAnomaly", e => e.EpochAtMeanAnomaly(0.4)),
        };
        var fresh = Case1();
        var withRadius = Case1(14e6 / 2.0);
        var expectedElements = fresh.ToKeplerianElements();
        var expectedLater = (KeplerianElements)Case1().AtEpoch(later);

        foreach (var (name, call) in firstCalls)
        {
            // Act
            var elements = Case1();
            call(elements);
            var ke = elements.ToKeplerianElements();
            var atLater = (KeplerianElements)elements.AtEpoch(later);

            // Assert: bit for bit those of a fresh instance (before: the order chose between an exception and two radii
            // 2 ulp apart)
            Assert.True(expectedElements.Equals(ke) && ke.PerigeeRadius() == expectedElements.PerigeeRadius(), name);
            Assert.True(expectedLater.Equals(atLater) && atLater.PerigeeRadius() == expectedLater.PerigeeRadius(), name);
        }

        // And those of the same elements given P / (1 + e)
        Assert.Equal(expectedElements, withRadius.ToKeplerianElements());
        Assert.Equal(expectedLater, (KeplerianElements)withRadius.AtEpoch(later));
    }

    [Theory]
    [InlineData(2e7, 1.0000004, new[] { 9352278.206717614, 5098287.626146383, 85498.4349949521 })]
    [InlineData(1.6e7, 0.9999996, new[] { 7481825.362979214, 4078631.6259995797, 68398.77357164008 })]
    public void Issue477_ElementsOfTheBand_MoveFromTheirState(double p, double f, double[] r)
    {
        // Arrange: e − 1 = ±4e-7, P finite, no perigee radius
        var elements = new EquinoctialElements(p, f, 0.0, 0.1, 0.05, 0.5, _earth, Epoch, Frames.Frame.ICRF);
        Assert.True(elements.IsParabolic());

        // Act
        var direct = elements.ToStateVector();
        var atEpoch = elements.AtEpoch(Epoch).ToStateVector();

        // Assert: the state of the elements, and the Keplerian path to it (before: ArgumentException; 7.26e6 and 1.19e7 m
        // off with a perigee radius given, the mean anomaly of the band, #478)
        double directError = RelativeDistance(direct, r);
        double atEpochError = RelativeDistance(atEpoch, r);
        Measure($"#477 band row e − 1 = {f - 1.0:E1}: direct |Δr| / |r|", directError);
        Measure($"#477 band row e − 1 = {f - 1.0:E1}: AtEpoch(t0) |Δr| / |r|", atEpochError);
        // Measured on Linux 2.3e-16 and 3.6e-16 (2.0e-7 before #459, the semi-latus rectum 2q of the Keplerian state in
        // the band, |e − 1| / (1 + e)); provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(directError, 0.0, 4.6e-16);
        Assert.InRange(atEpochError, 0.0, 7.2e-16);
    }

    #endregion

    #region #462: the semi-latus rectum of ToEquinoctial

    [Theory]
    [InlineData(1.0, 13600000.0, 0.0)]
    [InlineData(0.9999995, 13599996.6, 6.522355988636264e-10)]
    [InlineData(1.0000005, 13600003.4, 1.0271605788148008e-10)]
    public void Issue462_KeplerianElementsOfTheBand_HaveTheConicsSemiLatusRectum(double e, double hi, double lo)
    {
        // Arrange: the inputs of KeplerianElementsTests.ParabolicToEquinoctial, q = 6800 km, with the semi-major axis of the
        // conic, q / (1 − e) in doubles (+Infinity only at e = 1 since the review of #459)
        double a = e == 1.0 ? double.PositiveInfinity : 6800000.0 / (1.0 - e);
        var ke = new KeplerianElements(a, e, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius: 6800000.0);

        // Act
        var eq = ke.ToEquinoctial();

        // Assert: p = q (1 + e), the conic of the elements with its periapsis at q, 2q at e = 1 (before: NaN at e = 1, ±∞
        // in the band, of a sign set by rounding)
        double error = (eq.P - hi - lo) / hi;
        Measure($"#462 Keplerian e = {e:R}, P relative error", error);
        Assert.True(double.IsFinite(eq.P));
        if (e == 1.0)
        {
            Assert.Equal(13600000.0, eq.P);
        }

        // Measured at most 4.8e-17 relative on Linux (the rounding of q (1 + e)); provisional, twice, until the Windows x64
        // and macOS arm64 CI logs
        Assert.InRange(error, -9.6e-17, 9.6e-17);
        Assert.DoesNotContain("NaN", eq.ToString());
        Assert.DoesNotContain("Infinity", eq.ToString());
    }

    public static TheoryData<double, double, double> PeriapsisStates => new()
    {
        // vy (m/s), h²/μ at 60 digits (hi, lo): e − 1 = −5e-7, −7.9e-9 and +5e-7 at r = (7000 km, 0, 0)
        { 10671.729487052813, 13999996.5, -3.733329786349084e-10 },
        { 10671.7308, 13999999.944850652, 6.386977430653186e-10 },
        { 10671.732154985518, 14000003.5, 6.967542062940009e-10 },
    };

    [Theory]
    [MemberData(nameof(PeriapsisStates))]
    public void Issue462_StatesOfTheBand_HaveTheirSemiLatusRectum(double vy, double hi, double lo)
    {
        // Arrange
        var sv = State([7000000.0, 0.0, 0.0], [0.0, vy, 0.0]);
        Assert.True(sv.IsParabolic());

        // Act: the paths of the issue, each a P of the same orbit
        var direct = sv.ToEquinoctial().P;
        var throughKeplerian = sv.ToKeplerianElements().ToEquinoctial().P;
        var later = sv.AtEpoch(Epoch.AddSeconds(3600.0)).ToEquinoctial().P;
        var ecliptic = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquinoctial().P;

        // Assert: h²/μ (before: +∞ below e = 1, −∞ above)
        foreach (var (path, p) in new[] { ("direct", direct), ("Keplerian", throughKeplerian), ("+1 h", later), ("ecliptic", ecliptic) })
        {
            double error = (p - hi - lo) / hi;
            Measure($"#462 vy = {vy:R} {path}, P relative error", error);
            Assert.True(double.IsFinite(p));
            // Measured at most 4.9e-16 relative on Linux (the rounding of e, q and the rotation to the ecliptic);
            // provisional, twice, until the Windows x64 and macOS arm64 CI logs
            Assert.InRange(error, -9.7e-16, 9.7e-16);
        }
    }

    [Fact]
    public void Issue462_TheStateOfAnExactParabola_HasItsSemiLatusRectumInAnyFrame()
    {
        // Arrange: the input of StateVectorTests.ParabolicToEquinoctial, whose e is off 1 by its rounding
        var ke = new KeplerianElements(double.PositiveInfinity, 1.0, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        var sv = ke.ToStateVector();
        var ecliptic = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector();

        foreach (var (name, state) in new[] { ("ICRF", sv), ("ECLIPTIC_J2000", ecliptic) })
        {
            // Act
            double p = state.ToEquinoctial().P;

            // Assert: h²/μ of the state, and 2q of the elements within the rounding of the state
            Measure($"#462 exact parabola in {name}, P − h²/μ relative", (p - SemiLatusRectumOf(state)) / p);
            Measure($"#462 exact parabola in {name}, P − 2q relative", (p - 13600000.0) / 13600000.0);
            Assert.True(double.IsFinite(p));
            // Measured at most 2.8e-16 relative to h²/μ and 5.5e-16 to 2q on Linux; provisional, twice, until the Windows
            // x64 and macOS arm64 CI logs
            Assert.InRange((p - SemiLatusRectumOf(state)) / p, -5.5e-16, 5.5e-16);
            Assert.InRange((p - 13600000.0) / 13600000.0, -1.1e-15, 1.1e-15);
        }
    }

    [Fact]
    public void Issue462_ExactParabolas_HaveAFinitePositiveSemiLatusRectum()
    {
        // Arrange: exact parabolas built in doubles, p = 2q, i = 0.7 rad, ν from −170° to 170° every 0.5°
        double worst = 0.0;
        int count = 0;
        foreach (double q in new[] { 6600e3, 8000e3, 42164e3, 1e9 })
        {
            for (int step = -340; step <= 340; step++)
            {
                double nu = step * 0.5 * IO.Astrodynamics.Constants.Deg2Rad;
                double p = 2.0 * q;
                double r = p / (1.0 + System.Math.Cos(nu));
                double w = System.Math.Sqrt(Mu / p);
                double ci = System.Math.Cos(0.7), si = System.Math.Sin(0.7);
                var sv = State([r * System.Math.Cos(nu), r * System.Math.Sin(nu) * ci, r * System.Math.Sin(nu) * si],
                    [-w * System.Math.Sin(nu), w * (1.0 + System.Math.Cos(nu)) * ci, w * (1.0 + System.Math.Cos(nu)) * si]);

                // Act
                double actual = sv.ToEquinoctial().P;

                // Assert: finite, positive, h²/μ of the state (before: NaN, +∞ or −∞ on every one)
                Assert.True(double.IsFinite(actual) && actual > 0.0, $"q = {q}, ν = {nu}: P = {actual}");
                worst = System.Math.Max(worst, System.Math.Abs(actual - SemiLatusRectumOf(sv)) / actual);
                count++;
            }
        }

        Measure($"#462 {count} exact parabolas, worst |P − h²/μ| relative", worst);
        // Measured 2.9e-16 relative on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(worst, 0.0, 5.7e-16);
    }

    [Fact]
    public void Issue462_OutsideTheBand_PIsUnchanged()
    {
        // a (1 − e²) bit for bit outside the band, an ellipse and a hyperbola
        foreach (var (a, e) in new[] { (6800000.0, 0.2), (-7954545.45, 1.88), (7e12, 0.999998) })
        {
            var ke = new KeplerianElements(a, e, 0.2, 0.3, 0.4, 0.5, _earth, Epoch, Frames.Frame.ICRF);
            Assert.Equal(a * (1 - e * e), ke.ToEquinoctial().P);
        }
    }

    #endregion
}
