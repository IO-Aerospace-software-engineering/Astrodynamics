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
        // Arrange: every 10 m from −1 km to 10 000 km, the base of the layer within 1 m, and 101.39 km and 112.5 km,
        // where the curve fit stopped decreasing and became infinite
        var altitudes = new System.Collections.Generic.List<double>();
        for (int k = -100; k <= 1_000_000; k++)
        {
            altitudes.Add(10.0 * k);
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
            // The branches of the curve fit do not meet at 7 km (−37.99 °C below, −38.94 °C above): the density rises by
            // 0.41 % there, on main too, outside the scope of #422
            Assert.True(atmosphere.Density < previous || altitude == 7000.0,
                $"{altitude} m: {atmosphere.Density} after {previous}");
            previous = atmosphere.Density;
        }

        _output.WriteLine($"10 000 km: {Density(1e7):E3} kg/m³");
    }

    [Fact]
    public void CurveFit_JumpsAt7Km()
    {
        // Arrange: the jump of the curve fit at the boundary of its branches, 4.07e-3 measured (2026-10-22), kept as it is
        double below = Density(System.Math.BitDecrement(7000.0));

        // Act
        double above = Density(7000.0);

        // Assert
        _output.WriteLine($"7 km: {above / below - 1.0:E3}");
        Assert.InRange(above / below - 1.0, 4.0e-3, 4.1e-3);
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
