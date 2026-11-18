// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The universal variables of the near-parabolic band (#753), <see cref="UniversalKepler"/>, against their closed forms
/// at 60 digits (<c>tools/near_parabolic/near_parabolic_reference.py</c>, section 11, mpmath 1.4.1): the Stumpff functions
/// on both sides of z = 0 and of their series bound, the universal anomaly of Barker's anomaly and its time from the
/// periapsis on ellipses and hyperbolas of the band and beyond it (z up to 9.8), and the root of the universal Kepler
/// equation. A value is a pair (hi, lo) of doubles whose sum is the 60-digit value.
/// </summary>
public class UniversalKeplerTests
{
    private const double Mu = 398600435507022.6;
    private const double Q = 7.0e6;

    // Twice the worst on Linux x64 (2026-11-18), rounded up to two digits; provisional, to be set again from the Windows
    // x64 and macOS arm64 CI logs (#753): the Stumpff functions 4.9e-16 (z = −50), χ of D 1.8e-16, its time 2.1e-16, the
    // root 5.9e-16 (e = 1 − 1e-12, D = 300), D and ν of the root 2.1e-14 (e = 0.5 at D = 300, ν within 0.4° of the
    // apoapsis, where D = tan(ν/2) carries the rounding of the root's time divided by 1 + cos ν)
    private const double StumpffBound = 1.0e-15;
    private const double UniversalBound = 3.6e-16;
    private const double TimeBound = 4.3e-16;
    private const double RootBound = 1.2e-15;
    private const double BarkerBound = 4.3e-14;

    private readonly ITestOutputHelper _output;

    public UniversalKeplerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private void Measure(string what, double value) => _output.WriteLine($"MEASURE #753 {what}: {value:E3}");

    // z, C(z), S(z), 1 − z S(z), 2 − z C(z) at 60 digits
    private static readonly (double Z, (double Hi, double Lo) C, (double Hi, double Lo) S, (double Hi, double Lo) SineFactor,
        (double Hi, double Lo) CosineFactor)[] Stumpff =
    {
        (-50.0, (11.754054591751744, 7.805881820813775e-16), (1.6451003665217923, 6.945590258953625e-17), (83.25501832608961, 4.360973549176938e-15), (589.7027295875872, 4.968755014047038e-14)),
        (-4.0, (0.6905489227709078, 1.7866462272033598e-17), (0.20335755098087735, -3.64157989268682e-18), (1.8134302039235093, 9.645598289176837e-17), (4.762195691083631, 7.146584908813439e-17)),
        (-1.0, (0.5430806348152438, -4.495433471244733e-17), (0.17520119364380146, -4.770005424030052e-18), (1.1752011936438014, 7.849672142285669e-17), (2.5430806348152437, 6.606796775006833e-17)),
        (-0.999, (0.5430361163181794, -4.8316427431641207e-17), (0.17519245532368852, -1.3466184012690433e-17), (1.1750172628683648, -1.6023055065350237e-17), (2.542493080201861, 1.124539592117316e-16)),
        (-0.5, (0.5211836730427123, -5.3168674351119857e-17), (0.170883282545214, -6.5447387001718184e-18), (1.085441641272607, 1.0774993311242975e-16), (2.260591836521356, 1.3994911651821354e-16)),
        (-1e-06, (0.500000041666668, 5.07972002973462e-17), (0.1666666750000002, -9.99374686352067e-18), (1.000000166666675, 9.438681414810617e-17), (2.000000500000042, -1.4760667646561686e-16)),
        (0.0, (0.5, 0.0), (0.16666666666666666, 9.25185853854297e-18), (1.0, 0.0), (2.0, 0.0)),
        (1e-06, (0.4999999583333347, 6.934167148798357e-18), (0.16666665833333352, 8.98922653156975e-18), (0.9999998333333416, 2.995678560373205e-17), (1.9999995000000417, -7.83144201277957e-18)),
        (0.5, (0.4795108058487397, 2.3441887802160053e-17), (0.16254926026886313, -2.7452491155692256e-18), (0.9187253698655684, 2.9128200173413526e-17), (1.7602445970756302, -3.947651951670894e-17)),
        (0.999, (0.45973665764972876, 1.1517205979315569e-17), (0.1585369600581226, 1.776120694122742e-18), (0.8416215769019355, 1.7712100737313218e-17), (1.540723079007921, 1.6824748474055987e-17)),
        (1.0, (0.4596976941318603, -7.901605105213652e-18), (0.1585290151921035, -1.776845092935536e-18), (0.8414709848078965, 1.776845092935536e-18), (1.5403023058681398, -4.760954612604417e-17)),
        (4.0, (0.3540367091367856, 8.901296810420718e-18), (0.1363378216467898, 1.752613319727032e-18), (0.45464871341284085, -7.010453278908128e-18), (0.5838531634528576, -3.560518724168287e-17)),
        (30.0, (0.010252696280208405, -1.3563671526481513e-19), (0.037724215189836374, 7.607423533528317e-20), (-0.13172645569509123, -2.2822270600584952e-18), (1.692419111593748, -7.572817843698868e-17)),
        (100.0, (0.018390715290764525, -9.687518247356224e-19), (0.01054402111088937, 3.026278670166982e-19), (-0.05440211108893698, -2.50721108604091e-18), (0.16092847092354753, 1.3608455626675496e-17)),
    };

    // e, Barker's D, the universal anomaly χ of D, its time from the periapsis t, the root χ of the universal Kepler
    // equation for t rounded to a double, and z = (1 − e) χ²/q, at 60 digits, q = 7000 km
    private static readonly (double E, double D, (double Hi, double Lo) Chi, (double Hi, double Lo) Time,
        (double Hi, double Lo) Root, (double Hi, double Lo) Z)[] Universal =
    {
        (0.9999995, 0.3, (1122.4973479256284, 6.590884338676638e-14), (405.3700891543762, 8.425192891252968e-15), (1122.4973479256284, 4.3863127361052404e-14), (9.000002114260097e-08, -3.0318568357214154e-24)),
        (0.9999995, -2.0, (-7483.313214524708, -1.8125277001272856e-13), (-6122.088265634865, -7.574810557478236e-14), (-7483.313214524708, -1.3804388594347967e-13), (3.999998333005228e-06, -1.145650754634128e-22)),
        (0.9999995, 30.0, (112241.31803850601, 3.933367656290008e-12), (11843059.30514, 2.1051705867495585e-12), (112241.31803850601, 3.926702182083004e-12), (0.0008998652481417608, -2.288659152980541e-20)),
        (0.9999995, 300.0, (1114190.4818042193, -6.041895960806357e-11), (11496007232.348885, 2.729692785491606e-07), (1114190.4818042193, -6.926401588577659e-11), (0.08867288783149933, 5.8439006393500524e-18)),
        (1.0000005, 0.3, (1122.4970841387828, -1.0156543609667142e-13), (405.3700002563319, -2.1963625448798105e-14), (1122.4970841387828, -4.40944874786433e-14), (-8.999997886258499e-08, 3.172857146161517e-24)),
        (1.0000005, -2.0, (-7483.316332572531, 9.82354053513783e-14), (-6122.098629464487, -3.9307435557900724e-15), (-7483.316332572531, 1.0047760931982815e-13), (-4.000001667226741e-06, 2.3752201608715e-22)),
        (1.0000005, 30.0, (112258.12743468047, 3.932397117489523e-12), (11849443.873863399, 6.009106514164823e-10), (112258.12743468047, 2.0306260834081204e-12), (-0.0009001347983501734, -5.978023722531424e-21)),
        (1.0000005, 300.0, (1131031.3135832225, 5.714570804290555e-11), (12133947209.336, -6.269599078862029e-07), (1131031.3135832225, 7.656708635521152e-11), (-0.09137370232032845, -5.3894348271885454e-18)),
        (0.999999999999, 0.3, (1122.4972160324462, -4.8681380050109976e-14), (405.37004470543565, 7.629045925297719e-16), (1122.4972160324462, -5.067762842570074e-14), (1.7999601809046272e-13, 2.1227495474268873e-30)),
        (0.999999999999, -2.0, (-7483.314773544765, -1.440719315426644e-13), (-6122.093447535327, 4.504704296176262e-13), (-7483.314773544765, -4.0103288070443e-13), (7.999823026232362e-12, -4.5380037614914523e-29)),
        (0.999999999999, 30.0, (112249.72158640921, 3.545577861008277e-12), (11846250.814616483, 5.315740503515992e-10), (112249.72158640921, 1.8628657300747523e-12), (1.7999601803647052e-09, 3.141252353058253e-26)),
        (0.999999999999, 300.0, (1122497.1991953778, -4.009221891276676e-11), (11807287431.56415, 2.5938894047278376e-08), (1122497.1991953778, -4.091422490650238e-11), (1.7999601269070723e-07, 3.1888616210934287e-24)),
        (0.5, 0.3, (1283.4150793822266, -8.768024679128531e-16), (458.7551423492038, -2.6045532246563777e-14), (1283.4150793822266, 6.931959234493577e-14), (0.11765387614183478, -2.903971538945722e-18)),
        (0.5, -2.0, (-6413.739169340346, 2.8273931456123854e-13), (-3199.070815374602, 1.2279718650602583e-13), (-6413.739169340346, 1.192964211842516e-13), (2.9382892951664705, -1.2751422671153728e-16)),
        (0.5, 30.0, (11323.193075682022, -8.166699462924034e-13), (7789.158981569479, 1.9012253287868288e-13), (11323.19307568202, 8.211662028441317e-13), (9.158192959226662, -6.3120736242209805e-16)),
        (0.5, 300.0, (11711.558900594879, -8.494525088094647e-13), (8197.32342600378, 7.359204375659739e-14), (11711.558900594877, 8.995704019513923e-13), (9.797186563007363, 4.373613113816226e-16)),
        (0.9, 0.3, (1149.844587485443, -8.711763019629292e-14), (414.5626757612875, -1.0131307648683924e-15), (1149.844587485443, -8.445407207872511e-14), (0.018887751076708117, 1.2972443022062253e-18)),
        (0.9, -2.0, (-7198.185447726123, 2.893023900348041e-13), (-5224.03630695595, -3.152611368748855e-13), (-7198.185447726122, -4.0249878635340217e-13), (0.7401981962836588, -4.195169832619007e-17)),
        (0.9, 30.0, (23870.067750897506, 1.7040522174439237e-13), (76178.43733011244, -4.628346925111178e-12), (23870.067750897506, 8.790586110924318e-13), (8.139716206177672, -7.340612420353388e-16)),
        (0.9, 300.0, (26041.339268890333, -1.7946065398395089e-12), (90537.52679774722, -3.681101914523927e-13), (26041.339268890333, -1.7393374981830924e-12), (9.68787644167785, 5.513008848772993e-16)),
        (1.5, 0.3, (1010.0818910700854, -2.8325420919731874e-14), (367.10036646935936, -1.9806948771031753e-14), (1010.0818910700854, 2.2569379140725326e-14), (-0.07287610190483712, 1.953107530697992e-18)),
        (1.5, -2.0, (-10803.17867902341, -1.259095004931488e-13), (-27625.868620043926, -9.283284478562508e-13), (-10803.17867902341, -2.000059131257605e-14), (-8.336333540778998, 7.09200429084997e-16)),
        (3.0, 0.3, (805.9634410909282, -3.978691184307667e-14), (295.8155522769489, -1.2247579086498037e-14), (805.9634410909282, -9.181518139284697e-15), (-0.18559344810717998, -1.2860907897178078e-17)),
    };

    private static double Relative(double actual, (double Hi, double Lo) expected) =>
        System.Math.Abs((actual - expected.Hi - expected.Lo) / expected.Hi);

    [Fact]
    public void Stumpff_AreTheClosedForms()
    {
        double worst = 0.0;
        foreach (var r in Stumpff)
        {
            var (c, s, sineFactor, cosineFactor) = UniversalKepler.Stumpff(r.Z);
            double error = System.Math.Max(System.Math.Max(Relative(c, r.C), Relative(s, r.S)),
                System.Math.Max(Relative(sineFactor, r.SineFactor), Relative(cosineFactor, r.CosineFactor)));
            Measure($"Stumpff z = {r.Z:R}: worst relative error of C, S, 1 − zS, 2 − zC", error);
            worst = System.Math.Max(worst, error);
        }

        Measure("Stumpff, worst", worst);
        Assert.Equal(14, Stumpff.Length);
        Assert.InRange(worst, 0.0, StumpffBound);
    }

    [Fact]
    public void UniversalAnomaly_Time_AndRoot_AreTheClosedForms()
    {
        double worstChi = 0.0, worstTime = 0.0, worstRoot = 0.0, worstBarker = 0.0;
        foreach (var r in Universal)
        {
            // Act: χ of D, its time, the root for the double time, and D and ν back from the root
            double chi = UniversalKepler.FromBarkerAnomaly(r.D, Q, r.E);
            double time = UniversalKepler.TimeFromPeriapsis(r.Chi.Hi, Q, r.E, Mu);
            double root = UniversalKepler.Solve(r.Time.Hi, Q, r.E, Mu);
            var (barker, nu) = UniversalKepler.Anomalies(r.Root.Hi, Q, r.E);
            double chiError = Relative(chi, r.Chi), timeError = Relative(time, r.Time), rootError = Relative(root, r.Root);

            // D and ν of the 60-digit root are those of the double D given, to the rounding of the root's time
            double barkerError = System.Math.Max(System.Math.Abs(barker - r.D) / System.Math.Abs(r.D),
                System.Math.Abs(nu - 2.0 * System.Math.Atan(r.D)) / System.Math.Abs(2.0 * System.Math.Atan(r.D)));
            _output.WriteLine($"MEASURE #753 e = {r.E:R}, D = {r.D:R}, z = {r.Z.Hi:E3}: χ {chiError:E3}, t {timeError:E3}, root " +
                              $"{rootError:E3}, D and ν {barkerError:E3}");
            worstChi = System.Math.Max(worstChi, chiError);
            worstTime = System.Math.Max(worstTime, timeError);
            worstRoot = System.Math.Max(worstRoot, rootError);
            worstBarker = System.Math.Max(worstBarker, barkerError);
        }

        Measure("χ of D, worst relative", worstChi);
        Measure("time from the periapsis, worst relative", worstTime);
        Measure("root of the universal Kepler equation, worst relative", worstRoot);
        Measure("D and ν of the root, worst relative", worstBarker);
        Assert.Equal(23, Universal.Length);
        Assert.InRange(worstChi, 0.0, UniversalBound);
        Assert.InRange(worstTime, 0.0, TimeBound);
        Assert.InRange(worstRoot, 0.0, RootBound);
        Assert.InRange(worstBarker, 0.0, BarkerBound);
    }

    [Fact]
    public void AtEOne_IsBarkersEquation()
    {
        // χ = √(2q) D and √μ t = q χ + χ³/6: Barker's equation, z = 0
        const double d = 0.7;
        double chi = UniversalKepler.FromBarkerAnomaly(d, Q, 1.0);
        Assert.Equal(System.Math.Sqrt(2.0 * Q) * d, chi, 2e-16 * chi);
        double barker = System.Math.Sqrt(2.0 * Q * Q * Q / Mu) * (d + d * d * d / 3.0);
        Assert.Equal(barker, UniversalKepler.TimeFromPeriapsis(chi, Q, 1.0, Mu), 4e-16 * barker);
    }

    [Fact]
    public void Solve_AtThePeriapsis_IsZero_AndANonFiniteTime_Throws()
    {
        Assert.Equal(0.0, UniversalKepler.Solve(0.0, Q, 0.9999995, Mu));
        Assert.Throws<InvalidOperationException>(() => UniversalKepler.Solve(double.NaN, Q, 0.9999995, Mu));
        Assert.Throws<InvalidOperationException>(() => UniversalKepler.Solve(double.PositiveInfinity, Q, 0.9999995, Mu));
        Assert.Throws<InvalidOperationException>(() => UniversalKepler.Solve(100.0, Q, double.NaN, Mu));
    }
}
