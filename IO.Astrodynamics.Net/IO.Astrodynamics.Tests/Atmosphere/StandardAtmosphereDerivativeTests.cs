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
    [InlineData("Earth", -3000.0)] // the first layer below sea level
    [InlineData("Earth", 5000.0)] // the layers of the U.S. Standard Atmosphere 1976 (#437)
    [InlineData("Earth", 18000.0)]
    [InlineData("Earth", 40000.0)]
    [InlineData("Earth", 60000.0)]
    [InlineData("Earth", 80000.0)]
    [InlineData("Earth", 200000.0)] // its thermosphere, the spline of ln ρ
    [InlineData("Earth", 400000.0)]
    [InlineData("Earth", 800000.0)]
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

    // In the transitions between the branches of the models, where their formulas change (lesson 14): for the Earth the
    // bases of the 1976 layers (11.019, 20.063, 47.350 and 71.802 km, ±100 m, #437), the junction from 86 to 86.2 km, the
    // switch of the mean molecular weight at 100 km, the bends of the temperature at 91, 110 and 120 km, the entry of H
    // from 150 to 150.2 km, the last cell of the tables below 1000 km and the continuation above; for Mars 500 m centred on 7 km
    // (#433). Ridders' steps from 1 m, from 10 m to 1000 m in the thermosphere where ln ρ is far from 0 and its rounding
    // dominates smaller steps
    [Theory]
    [InlineData("Earth", 10930.0, 1.0)]
    [InlineData("Earth", 11019.07, 1.0)]
    [InlineData("Earth", 11110.0, 1.0)]
    [InlineData("Earth", 20063.1, 1.0)]
    [InlineData("Earth", 47350.1, 1.0)]
    [InlineData("Earth", 71802.0, 1.0)]
    [InlineData("Earth", 86000.5, 1.0)]
    [InlineData("Earth", 86100.0, 1.0)]
    [InlineData("Earth", 86199.5, 1.0)]
    [InlineData("Earth", 91000.0, 10.0)]
    [InlineData("Earth", 100000.0, 10.0)]
    [InlineData("Earth", 110000.0, 10.0)]
    [InlineData("Earth", 120000.0, 10.0)]
    [InlineData("Earth", 150100.0, 10.0)]
    [InlineData("Earth", 999000.0, 100.0)]
    [InlineData("Earth", 1500000.0, 1000.0)]
    [InlineData("Mars", 6760.0, 1.0)]
    [InlineData("Mars", 7000.0, 1.0)]
    [InlineData("Mars", 7100.0, 1.0)]
    [InlineData("Mars", 7240.0, 1.0)]
    public void Derivative_InTheTransitions_MatchesRidders(string body, double altitude, double step)
    {
        // Arrange
        object model = body == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();
        var density = (IGeodeticDensityModel)model;

        // Act
        double derivative = Derivative((IAltitudeDensityDerivative)model, altitude);

        // Assert
        var (expected, error) = RiddersDerivative.Derivative(h => Density(density, h), altitude, step);
        double relative = System.Math.Abs(derivative - expected) / System.Math.Abs(expected);
        _output.WriteLine($"{body}, {altitude / 1000.0} km: dρ/dh {derivative:E6}, relative error {relative:E2}, " +
                          $"reference {System.Math.Abs(error / expected):E2}");
        Assert.True(System.Math.Abs(error / expected) < 1e-10, $"reference {error / expected:E2}");
        Assert.True(relative < 1e-10, $"{relative:E2}");
    }

    // The ends of the transitions (#433): dρ/dh and its derivative, d²ρ/dh², are continuous, so that RK7(8) with the
    // variational equations in the step control does not stop there
    [Theory]
    [InlineData("Earth", 0)]
    [InlineData("Earth", 1)]
    [InlineData("Earth", 2)]
    [InlineData("Earth", 3)]
    [InlineData("Earth", 4)]
    [InlineData("Earth", 5)]
    [InlineData("Earth", 6)]
    [InlineData("Earth", 7)]
    [InlineData("Earth", 8)]
    [InlineData("Earth", 9)]
    [InlineData("Earth", 10)]
    [InlineData("Earth", 11)]
    [InlineData("Earth", 86000.0)]
    [InlineData("Earth", 86200.0)]
    [InlineData("Mars", 6750.0)]
    [InlineData("Mars", 7250.0)]
    public void Derivative_IsC1AtTheEndsOfTheTransitions(string body, double altitude)
    {
        // Arrange: for the Earth an index below 12 is an end of the transition at a base of the 1976 layers, ±100 m
        if (body == "Earth" && altitude < 12.0)
        {
            int index = (int)altitude;
            altitude = EarthStandardAtmosphere.LayerBases[index / 2] +
                       (index % 2 == 0 ? -1.0 : 1.0) * EarthStandardAtmosphere.TransitionHalfWidth;
        }

        object model = body == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();
        var derivative = (IAltitudeDensityDerivative)model;
        double Second(double h, double step) => (derivative.GetDensityAltitudeDerivative(h + step) -
                                                 derivative.GetDensityAltitudeDerivative(h - step)) / (2.0 * step);

        // Act: dρ/dh at the last double below the end and at the end; d²ρ/dh² by central differences over 0.2 mm centred
        // 0.2 mm below and 0.2 mm above the end, each on one side. The third derivative jumps at the ends (w‴ is not zero
        // there), so d²ρ/dh² moves inside by an amount proportional to the distance, largest for the narrowest transitions
        double below = derivative.GetDensityAltitudeDerivative(System.Math.BitDecrement(altitude));
        double at = derivative.GetDensityAltitudeDerivative(altitude);
        double secondBelow = Second(altitude - 2e-4, 1e-4);
        double secondAbove = Second(altitude + 2e-4, 1e-4);

        // Assert: before #433 and #437 dρ/dh jumped at these altitudes (by 23 % at 11 km); d²ρ/dh² agrees within 3.7e-4
        // at most (the Earth at 10.919 km, 2026-10-22), the change over the 0.4 mm between the two differences
        _output.WriteLine($"{body}, {altitude / 1000.0} km: dρ/dh {below:E9} / {at:E9}, d²ρ/dh² {secondBelow:E6} / " +
                          $"{secondAbove:E6}");
        Assert.True(System.Math.Abs(at / below - 1.0) < 1e-12, $"{at / below - 1.0:E2}");
        Assert.True(System.Math.Abs(secondAbove / secondBelow - 1.0) < 2e-3, $"{secondAbove / secondBelow - 1.0:E2}");
    }

    [Fact]
    public void Earth_Above1000Km_TheLogarithmicSlopeIsConstant()
    {
        // Arrange: the standard stops at 1000 km, where ln ρ is continued with its slope (#437); before, the temperature
        // was held at 2200 °C above 779.67 km and the density did not vary
        var model = new EarthStandardAtmosphere();

        // Act
        double top = Derivative(model, 1.0e6) / Density(model, 1.0e6);
        double far = Derivative(model, 3.0e6) / Density(model, 3.0e6);

        // Assert
        Assert.True(top < 0.0);
        Assert.Equal(top, far, 15);
    }
}
