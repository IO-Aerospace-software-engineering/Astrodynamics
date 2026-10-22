// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Atmosphere;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The density of <see cref="EarthStandardAtmosphere"/> against an independent reference (#433): U.S. Standard
/// Atmosphere, 1976, NOAA, NASA and USAF (1976), NASA-TM-X-74335, part 4, table I, geometric altitude, metric units.
/// </summary>
/// <remarks>
/// The model is the curve fit of the NASA Glenn Research Center ("Earth Atmosphere Model - Metric Units", Beginner's
/// Guide to Aeronautics) in three branches, troposphere below 11 km, lower stratosphere from 11 to 25 km, upper
/// stratosphere above, with 273.15 where the fit takes 273.1 to convert to kelvins. The tables are continuous; the
/// branches of the fit do not meet, so the density jumped by −0.023 % at 11 km and −1.82 % at 25 km, until the
/// transitions of 500 m centred on both boundaries (#433).
/// Against the tables the fit is within 1.0019 to 1.0025 below 11 km, 1.0007 at 19 km and 0.961 at 28 km, where the
/// temperature of its upper branch rises from 25 km instead of 20 km.
/// </remarks>
public class EarthStandardAtmosphereReferenceTests
{
    private readonly ITestOutputHelper _output;

    public EarthStandardAtmosphereReferenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Geometric altitude (m), density of the tables (kg/m³), bounds of the ratio to it: the departure measured,
    // rounded outward at the fourth decimal (1.0019 to 1.0025 from −1.05 to 4 km, 1.0007 at 19 km, 0.9611 at 28 km)
    [Theory]
    [InlineData(-1050.0, 1.3533, 1.0018, 1.0026)]
    [InlineData(0.0, 1.2250, 1.0018, 1.0026)]
    [InlineData(4000.0, 0.81935, 1.0018, 1.0026)]
    [InlineData(19000.0, 0.10400, 1.0006, 1.0008)]
    [InlineData(28000.0, 0.025076, 0.9610, 0.9612)]
    public void Density_AgainstUsStandardAtmosphere1976(double altitude, double reference, double lower, double upper)
    {
        // Arrange
        var model = new EarthStandardAtmosphere();

        // Act
        double ratio = model.GetDensity(AtmosphericContext.FromAltitude(altitude)) / reference;

        // Assert
        _output.WriteLine($"{altitude / 1000.0} km: {ratio:F5} of the U.S. Standard Atmosphere 1976");
        Assert.InRange(ratio, lower, upper);
    }

    // The branches of the curve fit, as published by the NASA Glenn Research Center, with the code's 273.15
    private static double Troposphere(double h)
    {
        double t = 15.04 - 0.00649 * h + IO.Astrodynamics.Constants.Kelvin;
        return 101.29 * System.Math.Pow(t / 288.08, 5.256) / (0.2869 * t);
    }

    private static double LowerStratosphere(double h) =>
        22.65 * System.Math.Exp(1.73 - 0.000157 * h) / (0.2869 * (-56.46 + IO.Astrodynamics.Constants.Kelvin));

    private static double UpperStratosphere(double h)
    {
        double t = -131.21 + 0.00299 * h + IO.Astrodynamics.Constants.Kelvin;
        return 2.488 * System.Math.Pow(t / 216.6, -11.388) / (0.2869 * t);
    }

    private static double Density(double h) =>
        new EarthStandardAtmosphere().GetDensity(AtmosphericContext.FromAltitude(h));

    // Boundary (m), the branch below, the branch above, the jump of the density measured on main (2026-10-22)
    public static TheoryData<double, string, double> Boundaries => new()
    {
        { 11000.0, "troposphere", -2.342e-4 },
        { 25000.0, "lower stratosphere", -1.824e-2 }
    };

    private static System.Func<double, double> Below(double boundary) =>
        boundary == 11000.0 ? Troposphere : LowerStratosphere;

    private static System.Func<double, double> Above(double boundary) =>
        boundary == 11000.0 ? LowerStratosphere : UpperStratosphere;

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Branches_AreJoinedOver500mCentredOnTheirBoundary(double boundary, string below, double jump)
    {
        // Arrange: the branches of the curve fit do not meet (#433); over 500 m centred on the boundary ln p and T go
        // from one to the next
        double start = boundary - EarthStandardAtmosphere.TransitionHalfWidth;
        double end = boundary + EarthStandardAtmosphere.TransitionHalfWidth;
        var lower = Below(boundary);
        var upper = Above(boundary);

        // Act and assert: the jump of the published branches at the boundary
        Assert.Equal(jump, upper(boundary) / lower(boundary) - 1.0, 5);

        // The published branches outside the transition, joined at both of its ends to the rounding
        foreach (double h in new[] { start - 1000.0, System.Math.BitDecrement(start), start })
        {
            Assert.True(System.Math.Abs(Density(h) / lower(h) - 1.0) < 1e-14, $"{below}, {h} m");
        }

        foreach (double h in new[] { end, end + 1000.0 })
        {
            Assert.True(System.Math.Abs(Density(h) / upper(h) - 1.0) < 1e-14, $"{h} m");
        }

        Assert.True(System.Math.Abs(Density(System.Math.BitDecrement(end)) / upper(end) - 1.0) < 1e-12);

        // Inside, the density lies between the two branches and decreases
        double previous = Density(start);
        for (int k = 1; k < 5000; k++)
        {
            double h = start + 0.1 * k;
            double density = Density(h);
            Assert.InRange(density, System.Math.Min(lower(h), upper(h)), System.Math.Max(lower(h), upper(h)));
            Assert.True(density < previous, $"{h} m");
            previous = density;
        }
    }

    // The rows of the tables in the transitions (#433): the ratio measured, 1.00235, 1.00184 and 1.00289 (2026-10-22),
    // rounded outward at the fourth decimal; on main 1.00195, 1.00172 and 0.99370, the last on the upper branch, 1.82 %
    // below the lower one
    [Theory]
    [InlineData(10950.0, 0.36713, 1.0023, 1.0024)]
    [InlineData(11000.0, 0.36480, 1.0018, 1.0019)]
    [InlineData(25000.0, 0.040084, 1.0028, 1.0029)]
    public void Transitions_AgainstUsStandardAtmosphere1976(double altitude, double reference, double lower,
        double upper)
    {
        // Act
        double ratio = Density(altitude) / reference;

        // Assert
        _output.WriteLine($"MEASURE {altitude / 1000.0} km: {ratio:F5} of the U.S. Standard Atmosphere 1976");
        Assert.InRange(ratio, lower, upper);
    }
}
