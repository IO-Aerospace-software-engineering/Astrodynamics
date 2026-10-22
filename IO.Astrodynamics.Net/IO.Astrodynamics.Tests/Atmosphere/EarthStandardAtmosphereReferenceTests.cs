// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// <see cref="EarthStandardAtmosphere"/> against an independent reference (#437): U.S. Standard Atmosphere, 1976, NOAA,
/// NASA and USAF (1976), NASA-TM-X-74335, part 4, table I, geometric altitude, metric units, read from the document; and
/// below 86 km against its defining equations of part 1, coded here independently.
/// </summary>
/// <remarks>
/// Before #437 the model was the curve fit of the NASA Glenn Research Center ("Earth Atmosphere Model - Metric Units"),
/// within 1.0019 to 1.0025 of the tables below 11 km, 0.961 at 28 km, and 2.97, 80.1, 38.7 and 2.28 times the tables at
/// 86, 140, 200 and 400 km.
/// </remarks>
public class EarthStandardAtmosphereReferenceTests
{
    private readonly ITestOutputHelper _output;
    private static readonly EarthStandardAtmosphere Model = new();

    public EarthStandardAtmosphereReferenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static double Density(double altitude) => Model.GetDensity(AtmosphericContext.FromAltitude(altitude));

    // Table I below 86 km, five significant digits: geometric altitude (m), density (kg/m³)
    public static TheoryData<double, double> LowerTable => new()
    {
        { -1050.0, 1.3533 }, { 0.0, 1.2250 }, { 4000.0, 0.81935 }, { 10950.0, 0.36713 }, { 11000.0, 0.36480 },
        { 12000.0, 0.31194 }, { 19000.0, 0.10400 }, { 25000.0, 0.040084 }, { 28000.0, 0.025076 }, { 30000.0, 0.018410 }
    };

    // Table I from 86 to 1000 km, ρ/ρ0 (ρ0 = 1.225 kg/m³), four significant digits (2026-10-22)
    private static readonly (double Km, double Ratio)[] UpperTable =
    {
        (86.0, 5.680e-6),
        (87.0, 4.754e-6),
        (89.5, 3.048e-6),
        (90.0, 2.789e-6),
        (93.0, 1.632e-6),
        (95.0, 1.137e-6),
        (97.5, 7.218e-7),
        (100.0, 4.575e-7),
        (105.0, 1.898e-7),
        (110.0, 7.925e-8),
        (115.0, 3.501e-8),
        (120.0, 1.814e-8),
        (125.0, 1.054e-8),
        (130.0, 6.655e-9),
        (135.0, 4.461e-9),
        (140.0, 3.128e-9),
        (145.0, 2.270e-9),
        (150.0, 1.694e-9),
        (160.0, 1.007e-9),
        (170.0, 6.380e-10),
        (180.0, 4.240e-10),
        (190.0, 2.923e-10),
        (200.0, 2.074e-10),
        (210.0, 1.507e-10),
        (220.0, 1.116e-10),
        (230.0, 8.402e-11),
        (240.0, 6.415e-11),
        (250.0, 4.957e-11),
        (260.0, 3.871e-11),
        (270.0, 3.052e-11),
        (280.0, 2.425e-11),
        (290.0, 1.941e-11),
        (300.0, 1.564e-11),
        (320.0, 1.032e-11),
        (330.0, 8.447e-12),
        (340.0, 6.941e-12),
        (350.0, 5.725e-12),
        (360.0, 4.739e-12),
        (370.0, 3.934e-12),
        (380.0, 3.276e-12),
        (390.0, 2.734e-12),
        (400.0, 2.288e-12),
        (410.0, 1.918e-12),
        (420.0, 1.612e-12),
        (430.0, 1.357e-12),
        (440.0, 1.144e-12),
        (450.0, 9.668e-13),
        (460.0, 8.180e-13),
        (470.0, 6.932e-13),
        (480.0, 5.884e-13),
        (490.0, 5.001e-13),
        (500.0, 4.257e-13),
        (525.0, 2.865e-13),
        (550.0, 1.946e-13),
        (575.0, 1.336e-13),
        (600.0, 9.279e-14),
        (625.0, 6.529e-14),
        (650.0, 4.663e-14),
        (675.0, 3.386e-14),
        (700.0, 2.506e-14),
        (725.0, 1.892e-14),
        (750.0, 1.460e-14),
        (775.0, 1.151e-14),
        (800.0, 9.272e-15),
        (825.0, 7.624e-15),
        (850.0, 6.387e-15),
        (875.0, 5.440e-15),
        (900.0, 4.701e-15),
        (925.0, 4.113e-15),
        (950.0, 3.635e-15),
        (975.0, 3.239e-15),
        (1000.0, 2.907e-15)
    };

    public static TheoryData<double, double> UpperRows()
    {
        var data = new TheoryData<double, double>();
        foreach (var (km, ratio) in UpperTable)
        {
            data.Add(km, ratio);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LowerTable))]
    public void Density_BelowThe86KmOfTheEquations_IsTableI(double altitude, double reference)
    {
        // Act
        double relative = Density(altitude) / reference - 1.0;

        // Assert: within the transitions of ±100 m around the bases of the layers (10.95 and 11 km lie in that of
        // 11.019 km) the density departs from the equations by up to 2.1e-4; elsewhere within the five digits of the table
        _output.WriteLine($"{altitude / 1000.0} km: {relative:E2}");
        Assert.True(System.Math.Abs(relative) < 2.2e-4, $"{relative:E2}");
    }

    [Theory]
    [MemberData(nameof(UpperRows))]
    public void Density_From86To1000Km_IsTableI(double km, double ratio)
    {
        // Act
        double relative = Density(km * 1000.0) / (ratio * 1.225) - 1.0;

        // Assert: within the rounding of four significant digits, 5e-4 of the leading digit; 4.2e-4 at most measured
        // (95 km, 2026-10-22)
        _output.WriteLine($"{km} km: {relative:E2}");
        Assert.True(System.Math.Abs(relative) < 5e-4, $"{relative:E2}");
    }

    // Geometric altitude (m), temperature (K) and pressure (kPa) of table I
    [Theory]
    [InlineData(0.0, 288.150, 101.325)]
    [InlineData(12000.0, 216.650, 19.399)]
    [InlineData(25000.0, 221.552, 2.5492)]
    [InlineData(30000.0, 226.509, 1.1970)]
    [InlineData(200000.0, 854.56, 8.4736e-8)]
    [InlineData(400000.0, 995.83, 1.4518e-9)]
    [InlineData(600000.0, 999.85, 8.2130e-11)]
    public void TemperatureAndPressure_AreTableI(double altitude, double temperature, double pressure)
    {
        // Act
        var context = AtmosphericContext.FromAltitude(altitude);
        double t = Model.GetTemperature(context) + IO.Astrodynamics.Constants.Kelvin;
        double p = Model.GetPressure(context);

        // Assert: to the last digit of the table
        _output.WriteLine($"{altitude / 1000.0} km: T {t:F3} K, p {p:E5} kPa");
        Assert.Equal(temperature, t, 0.01);
        Assert.True(System.Math.Abs(p / pressure - 1.0) < 5e-4, $"{p / pressure - 1.0:E2}");
    }

    [Fact]
    public void UpperTable_DecreasesWithAPhysicalScaleHeight()
    {
        // Arrange: a check of the transcription; between two rows the density scale height, Δh / ln(ρ₁ / ρ₂), lies
        // between 5 km at 86 km and 300 km at 1000 km, and grows with the altitude except where the temperature profile
        // of the model bends (91 to 120 km)
        double previousHeight = 0.0;

        // Act and assert
        for (int k = 1; k < UpperTable.Length; k++)
        {
            double height = (UpperTable[k].Km - UpperTable[k - 1].Km) / System.Math.Log(UpperTable[k - 1].Ratio / UpperTable[k].Ratio);
            Assert.InRange(height, 5.0, 300.0);
            Assert.True(height > previousHeight || UpperTable[k].Km <= 120.0, $"{UpperTable[k].Km} km");
            previousHeight = height;
        }
    }

    // The defining equations of part 1 below 86 km, eq. (23), (33a), (33b) and (42), with the constants of its tables 2
    // and 4, without the transitions
    private static double EquationDensity(double z)
    {
        const double g0 = 9.80665, rStar = 8.31432e3, m0 = 28.9644, r0 = 6356766.0;
        double[] hb = { 0, 11000, 20000, 32000, 47000, 51000, 71000, 84852 };
        double[] lb = { -6.5e-3, 0.0, 1.0e-3, 2.8e-3, 0.0, -2.8e-3, -2.0e-3 };
        double gmr = g0 * m0 / rStar;
        double h = r0 * z / (r0 + z);
        double tb = 288.15, pb = 101325.0;
        int i = 0;
        while (i < 6 && h >= hb[i + 1])
        {
            double top = tb + lb[i] * (hb[i + 1] - hb[i]);
            pb = lb[i] == 0.0 ? pb * System.Math.Exp(-gmr * (hb[i + 1] - hb[i]) / tb) : pb * System.Math.Pow(tb / top, gmr / lb[i]);
            tb = top;
            i++;
        }

        double t = tb + lb[i] * (h - hb[i]);
        double p = lb[i] == 0.0 ? pb * System.Math.Exp(-gmr * (h - hb[i]) / tb) : pb * System.Math.Pow(tb / t, gmr / lb[i]);
        return p * m0 / (rStar * t);
    }

    [Fact]
    public void Density_IsTheEquationsOutsideTheTransitions_AndWithin2Point1EMinus4Inside()
    {
        // Arrange: every 1 m from −5 km to 86 km
        double worstOutside = 0.0, worstInside = 0.0, worstInsideAt = 0.0;
        var bases = EarthStandardAtmosphere.LayerBases.ToArray();

        // Act
        for (int k = -5000; k < 86000; k++)
        {
            double z = k + 0.37;
            double relative = System.Math.Abs(Density(z) / EquationDensity(z) - 1.0);
            bool inside = Array.Exists(bases, b => System.Math.Abs(z - b) < EarthStandardAtmosphere.TransitionHalfWidth);
            if (inside && relative > worstInside)
            {
                worstInside = relative;
                worstInsideAt = z;
            }
            else if (!inside)
            {
                worstOutside = System.Math.Max(worstOutside, relative);
            }
        }

        // Assert: the equations to the rounding outside the transitions; inside, 2.11e-4 at most measured (11.019 km)
        _output.WriteLine($"outside {worstOutside:E2}, inside {worstInside:E2} at {worstInsideAt / 1000.0:F3} km");
        Assert.True(worstOutside < 1e-13, $"{worstOutside:E2}");
        Assert.True(worstInside < 2.2e-4, $"{worstInside:E2}");
    }

    [Fact]
    public void Density_IsPositiveAndDecreasing_FromMinus5To3000Km()
    {
        // Arrange: every 1 m from −5 to 100 km, where the layers, their transitions and the junction at 86 km lie, then
        // every 10 m to 3000 km
        double previous = double.PositiveInfinity;

        // Act and assert
        for (double z = -5000.0; z <= 3.0e6; z += z < 100000.0 ? 1.0 : 10.0)
        {
            double density = Density(z);
            Assert.True(double.IsFinite(density) && density > 0.0 && density < previous, $"{z} m: {density}");
            previous = density;
        }
    }

    [Fact]
    public void Above1000Km_TheScaleHeightIsContinued()
    {
        // Arrange: the standard stops at 1000 km; ln ρ is continued with its slope there
        double top = Density(1.0e6);

        // Act
        double scaleHeight = 100e3 / System.Math.Log(top / Density(1.1e6));
        double far = System.Math.Log(top / Density(2.0e6)) / System.Math.Log(top / Density(1.1e6));

        // Assert: the scale height of the equations at 1000 km, 237.1 km (2026-10-22); the table gives 230.6 km between
        // 975 and 1000 km, and rising
        _output.WriteLine($"scale height {scaleHeight / 1000.0:F2} km");
        Assert.InRange(scaleHeight, 231e3, 245e3);
        Assert.Equal(10.0, far, 9);
    }

    [Theory]
    [InlineData(86000.0)]
    [InlineData(86200.0)]
    [InlineData(1.0e6)]
    public void Density_IsContinuousAtTheJunctions(double altitude)
    {
        // Arrange: the junction of the equations below 86 km with the thermosphere, over 86 to 86.2 km, and the top of
        // the tables
        double below = Density(System.Math.BitDecrement(altitude));

        // Act
        double at = Density(altitude);

        // Assert
        _output.WriteLine($"{altitude / 1000.0} km: {at / below - 1.0:E2}");
        Assert.True(System.Math.Abs(at / below - 1.0) < 1e-14);
    }
}
