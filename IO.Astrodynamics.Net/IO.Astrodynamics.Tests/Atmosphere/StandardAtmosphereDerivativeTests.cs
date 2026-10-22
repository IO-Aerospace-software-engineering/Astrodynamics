// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The closed-form dρ/dh of the US Standard Atmosphere 1976 and of the Mars atmosphere, which the drag partials take
/// instead of finite differences (#420), against Ridders' derivative of the density, in every branch of each model.
/// </summary>
public class StandardAtmosphereDerivativeTests
{
    private readonly ITestOutputHelper _output;

    public StandardAtmosphereDerivativeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static double Derivative(IAltitudeDensityDerivative model, double altitude) =>
        model.GetDensityAltitudeDerivative(altitude);

    private static double Density(IGeodeticDensityModel model, double altitude) =>
        model.GetDensity(altitude, 0.0, 0.0, TimeSystem.Time.J2000TDB);

    [Theory]
    [InlineData("Earth", 5000.0)] // below 11 km
    [InlineData("Earth", 18000.0)] // isothermal, 11 to 25 km
    [InlineData("Earth", 100000.0)] // above 25 km
    [InlineData("Earth", 200000.0)]
    [InlineData("Earth", 400000.0)]
    [InlineData("Mars", 3000.0)] // below 7 km
    [InlineData("Mars", 30000.0)] // above 7 km
    [InlineData("Mars", 49700.0)] // below the isothermal layer (49.887 km), Ridders' steps within the branch
    [InlineData("Mars", 50100.0)] // the isothermal layer of Seiff (1982), #422
    [InlineData("Mars", 100000.0)]
    [InlineData("Mars", 150000.0)]
    [InlineData("Mars", 200000.0)]
    [InlineData("Mars", 400000.0)]
    [InlineData("Mars", 1000000.0)]
    public void Derivative_MatchesRidders(string body, double altitude)
    {
        // Arrange
        object model = body == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();
        var density = (IGeodeticDensityModel)model;

        // Act
        double derivative = Derivative((IAltitudeDensityDerivative)model, altitude);

        // Assert: Ridders' steps from 100 m, within the branch of the altitude
        var (expected, error) = RiddersDerivative.Derivative(h => Density(density, h), altitude, 100.0);
        double relative = System.Math.Abs(derivative - expected) / System.Math.Abs(expected);
        _output.WriteLine($"{body}, {altitude / 1000.0} km: dρ/dh {derivative:E6}, relative error {relative:E2}, " +
                          $"reference {System.Math.Abs(error / expected):E2}");
        Assert.True(System.Math.Abs(error / expected) < 1e-10, $"reference {error / expected:E2}");
        Assert.True(relative < 1e-10, $"{relative:E2}");
    }

    [Fact]
    public void Earth_AboveTheTemperatureClamp_TheDensityIsConstant()
    {
        // Arrange: the temperature is held at 2200 °C above 779.67 km, so the density no longer varies
        var model = new EarthStandardAtmosphere();

        // Act
        double derivative = Derivative(model, 900000.0);

        // Assert
        Assert.Equal(0.0, derivative);
        Assert.Equal(Density(model, 890000.0), Density(model, 910000.0));
    }
}
