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

    // In the transitions between the branches of the curve fits (#433): 500 m centred on 11 and 25 km for the Earth, on
    // 7 km for Mars; Ridders' steps from 10 m, within the transition
    [Theory]
    [InlineData("Earth", 10760.0)]
    [InlineData("Earth", 10900.0)]
    [InlineData("Earth", 11000.0)]
    [InlineData("Earth", 11100.0)]
    [InlineData("Earth", 11240.0)]
    [InlineData("Earth", 24760.0)]
    [InlineData("Earth", 25000.0)]
    [InlineData("Earth", 25240.0)]
    [InlineData("Mars", 6760.0)]
    [InlineData("Mars", 7000.0)]
    [InlineData("Mars", 7100.0)]
    [InlineData("Mars", 7240.0)]
    public void Derivative_InTheTransitions_MatchesRidders(string body, double altitude)
    {
        // Arrange
        object model = body == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();
        var density = (IGeodeticDensityModel)model;

        // Act
        double derivative = Derivative((IAltitudeDensityDerivative)model, altitude);

        // Assert
        var (expected, error) = RiddersDerivative.Derivative(h => Density(density, h), altitude, 10.0);
        double relative = System.Math.Abs(derivative - expected) / System.Math.Abs(expected);
        _output.WriteLine($"{body}, {altitude / 1000.0} km: dρ/dh {derivative:E6}, relative error {relative:E2}, " +
                          $"reference {System.Math.Abs(error / expected):E2}");
        Assert.True(System.Math.Abs(error / expected) < 1e-10, $"reference {error / expected:E2}");
        Assert.True(relative < 1e-10, $"{relative:E2}");
    }

    // The ends of the transitions (#433): dρ/dh and its derivative, d²ρ/dh², are continuous, so that RK7(8) with the
    // variational equations in the step control does not stop there
    [Theory]
    [InlineData("Earth", 10750.0)]
    [InlineData("Earth", 11250.0)]
    [InlineData("Earth", 24750.0)]
    [InlineData("Earth", 25250.0)]
    [InlineData("Mars", 6750.0)]
    [InlineData("Mars", 7250.0)]
    public void Derivative_IsC1AtTheEndsOfTheTransitions(string body, double altitude)
    {
        // Arrange
        object model = body == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();
        var derivative = (IAltitudeDensityDerivative)model;
        double Second(double h, double step) => (derivative.GetDensityAltitudeDerivative(h + step) -
                                                 derivative.GetDensityAltitudeDerivative(h - step)) / (2.0 * step);

        // Act: dρ/dh at the last double below the end and at the end; d²ρ/dh² by central differences over 2 mm centred
        // 2 mm below and 2 mm above the end, each on one side. The third derivative jumps at the ends (w‴ is not zero
        // there), so d²ρ/dh² moves by up to 3.5e-10 kg/m⁵ per metre inside, against about 1e-9 for d²ρ/dh² itself
        double below = derivative.GetDensityAltitudeDerivative(System.Math.BitDecrement(altitude));
        double at = derivative.GetDensityAltitudeDerivative(altitude);
        double secondBelow = Second(altitude - 2e-3, 1e-3);
        double secondAbove = Second(altitude + 2e-3, 1e-3);

        // Assert: before #433 dρ/dh jumped by 23 % at 11 km, 9 % at 25 km and 6 % at 7 km; d²ρ/dh² agrees within 2.9e-4
        // to 7.9e-4 (2026-10-22), the change over the 4 mm between the two differences, bounded at 2e-3
        _output.WriteLine($"{body}, {altitude / 1000.0} km: dρ/dh {below:E9} / {at:E9}, d²ρ/dh² {secondBelow:E6} / " +
                          $"{secondAbove:E6}");
        Assert.True(System.Math.Abs(at / below - 1.0) < 1e-12, $"{at / below - 1.0:E2}");
        Assert.True(System.Math.Abs(secondAbove / secondBelow - 1.0) < 2e-3, $"{secondAbove / secondBelow - 1.0:E2}");
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
