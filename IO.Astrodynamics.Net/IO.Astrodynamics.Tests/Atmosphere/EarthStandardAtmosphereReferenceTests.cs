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
/// branches of the fit do not meet, so the density jumps by −0.023 % at 11 km and −1.82 % at 25 km (2026-10-22).
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
}
