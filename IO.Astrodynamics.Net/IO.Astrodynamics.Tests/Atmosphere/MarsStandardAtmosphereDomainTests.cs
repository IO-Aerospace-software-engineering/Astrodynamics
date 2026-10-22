// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Linq;
using IO.Astrodynamics.Atmosphere;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The domain of <see cref="MarsStandardAtmosphere"/> (#422): the absolute temperature of the curve fit reached 0 K at
/// 112.5 km, so the density was infinite there and negative above; the isothermal layer of Seiff (1982) above
/// 49.887 km keeps it positive, decreasing and continuous at every altitude.
/// </summary>
public class MarsStandardAtmosphereDomainTests
{
    private const double Base = MarsStandardAtmosphere.IsothermalLayerBase;
    private readonly ITestOutputHelper _output;

    public MarsStandardAtmosphereDomainTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly MarsStandardAtmosphere Model = new();

    private static double Density(double altitude) => Model.GetDensity(AtmosphericContext.FromAltitude(altitude));

    [Fact]
    public void Density_IsPositiveFiniteAndDecreasing_AtEveryAltitude()
    {
        // Arrange: every 10 m from −1 km to 10 000 km, every 0.1 m from 6.7 to 7.3 km, where the branches of the curve fit
        // jumped (#433), the base of the layer within 1 m, and 101.39 km and 112.5 km, where the curve fit stopped
        // decreasing and became infinite
        var altitudes = new System.Collections.Generic.List<double>();
        for (int k = -100; k <= 1_000_000; k++)
        {
            altitudes.Add(10.0 * k);
        }

        for (int k = 67_000; k <= 73_000; k++)
        {
            altitudes.Add(0.1 * k);
        }

        altitudes.AddRange(new[] { Base - 1.0, Base - 1e-3, Base, Base + 1e-3, Base + 1.0, 101390.0, 112500.0 });
        altitudes = altitudes.Distinct().OrderBy(h => h).ToList();

        // Act and assert
        double previous = double.PositiveInfinity;
        foreach (double altitude in altitudes)
        {
            var atmosphere = Model.GetAtmosphere(AtmosphericContext.FromAltitude(altitude));
            Assert.True(double.IsFinite(atmosphere.Density) && atmosphere.Density > 0.0,
                $"{altitude} m: density {atmosphere.Density}");
            Assert.True(atmosphere.Temperature + IO.Astrodynamics.Constants.Kelvin > 0.0,
                $"{altitude} m: temperature {atmosphere.Temperature} °C");
            Assert.True(atmosphere.Pressure > 0.0, $"{altitude} m: pressure {atmosphere.Pressure}");
            Assert.True(atmosphere.Density < previous, $"{altitude} m: {atmosphere.Density} after {previous}");
            previous = atmosphere.Density;
        }

        _output.WriteLine($"10 000 km: {Density(1e7):E3} kg/m³");
    }

    // The branches of the curve fit, as published by the NASA Glenn Research Center, with the code's 273.15
    private static double LowerBranch(double h) => 0.699 * System.Math.Exp(-0.00009 * h) /
                                                    (0.1921 * (-31.0 - 0.000998 * h + IO.Astrodynamics.Constants.Kelvin));

    private static double UpperBranch(double h) => 0.699 * System.Math.Exp(-0.00009 * h) /
                                                    (0.1921 * (-23.4 - 0.00222 * h + IO.Astrodynamics.Constants.Kelvin));

    [Fact]
    public void CurveFit_IsContinuousAcross7Km()
    {
        // Arrange: the branches of the curve fit do not meet at 7 km (−37.99 °C below, −38.94 °C above), so the density
        // rose there by 4.07e-3 (#433); over 500 m centred on 7 km the temperature goes from one to the other
        const double start = 7000.0 - MarsStandardAtmosphere.CurveFitTransitionHalfWidth;
        const double end = 7000.0 + MarsStandardAtmosphere.CurveFitTransitionHalfWidth;

        // Act and assert: the published branches outside the transition, joined at both of its ends to the rounding
        Assert.Equal(4.07e-3, UpperBranch(7000.0) / LowerBranch(7000.0) - 1.0, 5);
        foreach (double h in new[] { 0.0, 3000.0, System.Math.BitDecrement(start) })
        {
            Assert.True(System.Math.Abs(Density(h) / LowerBranch(h) - 1.0) < 1e-15, $"{h} m");
        }

        foreach (double h in new[] { end, 20000.0, 40000.0 })
        {
            Assert.True(System.Math.Abs(Density(h) / UpperBranch(h) - 1.0) < 1e-15, $"{h} m");
        }

        Assert.True(System.Math.Abs(Density(start) / LowerBranch(start) - 1.0) < 1e-15);
        Assert.True(System.Math.Abs(Density(System.Math.BitDecrement(end)) / UpperBranch(end) - 1.0) < 1e-12);

        // Inside, the density lies between the two branches, within 0.20 % of the published model, which takes the lower
        // branch below 7 km and the upper one above (measured 2.03e-3 and 2.02e-3, 2026-10-22)
        double worst = 0.0;
        for (int k = 1; k < 5000; k++)
        {
            double h = start + 0.1 * k;
            double density = Density(h);
            Assert.InRange(density, LowerBranch(h), UpperBranch(h));
            double published = h < 7000.0 ? LowerBranch(h) : UpperBranch(h);
            worst = System.Math.Max(worst, System.Math.Abs(density / published - 1.0));
        }

        _output.WriteLine($"7 km: the transition within {worst:E3} of the curve fit");
        Assert.True(worst < 2.1e-3, $"{worst:E3}");
    }

    [Fact]
    public void Layer_IsContinuousAtItsBase()
    {
        // Arrange: the last double below the base, on the branch of the curve fit, and the base, on the layer
        double below = System.Math.BitDecrement(Base);

        // Act
        var lower = Model.GetAtmosphere(AtmosphericContext.FromAltitude(below));
        var upper = Model.GetAtmosphere(AtmosphericContext.FromAltitude(Base));

        // Assert: the temperature, the pressure and the density of both branches agree to the rounding
        _output.WriteLine($"base {Base} m: T {lower.Temperature} / {upper.Temperature} °C, p {lower.Pressure:E17} / " +
                          $"{upper.Pressure:E17} kPa, ρ {lower.Density:E17} / {upper.Density:E17}");
        Assert.Equal(139.0, upper.Temperature + IO.Astrodynamics.Constants.Kelvin, 12);
        Assert.Equal(lower.Temperature, upper.Temperature, 12);
        Assert.True(System.Math.Abs(lower.Pressure / upper.Pressure - 1.0) < 1e-14);
        Assert.True(System.Math.Abs(lower.Density / upper.Density - 1.0) < 1e-13);
    }

    [Theory]
    [InlineData(60e3)]
    [InlineData(100e3)]
    [InlineData(150e3)]
    [InlineData(200e3)]
    [InlineData(400e3)]
    [InlineData(1000e3)]
    public void LayerPressure_IsHydrostatic(double altitude)
    {
        // Arrange: dp/dh = −p g / (R T) with g = GM / r², T = 139 K, R = 192.1 J/(kg K), the equation of Seiff at
        // constant temperature, integrated in ln p by the classical fourth-order Runge-Kutta from the base, independently
        // of the closed form
        const double gm = 4.2828e13;
        const double radius = 3389.5e3;
        const double gasConstant = 192.1;
        const int steps = 20000;
        double Slope(double h) => -gm / ((radius + h) * (radius + h)) / (gasConstant * 139.0);
        double logPressure = System.Math.Log(Model.GetPressure(AtmosphericContext.FromAltitude(Base)));
        double step = (altitude - Base) / steps;
        for (int k = 0; k < steps; k++)
        {
            double h = Base + k * step;
            logPressure += step / 6.0 * (Slope(h) + 4.0 * Slope(h + 0.5 * step) + Slope(h + step));
        }

        // Act
        double pressure = Model.GetPressure(AtmosphericContext.FromAltitude(altitude));

        // Assert
        double relative = System.Math.Abs(pressure / System.Math.Exp(logPressure) - 1.0);
        _output.WriteLine($"{altitude / 1000.0} km: p {pressure:E6} kPa, relative to the integration {relative:E2}");
        Assert.True(relative < 1e-11, $"{relative:E2}");
    }
}
