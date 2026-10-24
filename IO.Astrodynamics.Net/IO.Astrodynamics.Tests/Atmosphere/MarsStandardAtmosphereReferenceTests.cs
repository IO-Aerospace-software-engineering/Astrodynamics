// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Atmosphere;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The density of <see cref="MarsStandardAtmosphere"/> against an independent reference (#422): the nominal northern
/// summer mid-latitude model of A. Seiff, "Post-Viking models for the structure of the summer atmosphere of Mars", in
/// The Mars Reference Atmosphere, A. J. Kliore (ed.), Advances in Space Research 2(2) (1982), as tabulated in the JPL
/// DESCANSO Mars propagation handbook, section 3, "Martian Atmosphere and Its Effects on Propagation", table 3-1
/// (surface pressure 6.36 mb); confirmed by S. Guillet, 2026-10-24.
/// </summary>
/// <remarks>
/// Below 49.887 km the model is the curve fit of the NASA Glenn Research Center ("Mars Atmosphere Model - Metric
/// Units"), surface pressure 6.99 mb, which states no upper bound. Against Seiff it departs by −3.7 % at the surface
/// (the surface pressure), 11.4 % at 20 km, a factor 1.82 at 40 km and 2.27 at 46 km (2026-10-22). Above, Seiff's
/// isothermal layer at 139 K (#422), reached over a transition of 5 km, keeps it within a factor 2.23 to 2.81 up to
/// 100 km (2.81 at 52 km, in the transition), where the curve fit reached 101.
/// </remarks>
public class MarsStandardAtmosphereReferenceTests
{
    private readonly ITestOutputHelper _output;

    public MarsStandardAtmosphereReferenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Altitude (km), density of Seiff (kg/m³), bounds of the ratio to it: the departure measured over the band, rounded
    // outward at the second significant digit (0.96 to 1.11 up to 20 km; 1.15 to 1.82 from 22 to 40 km; 1.95 to 2.27 to
    // 46 km; 2.23 to 2.81 from 50 to 100 km, the transition and the isothermal layer, where the curve fit gave 2.70 to
    // 101)
    [Theory]
    [InlineData(0.0, 1.56e-2, 0.96, 1.12)]
    [InlineData(4.0, 1.08e-2, 0.96, 1.12)]
    [InlineData(10.0, 6.47e-3, 0.96, 1.12)]
    [InlineData(16.0, 3.81e-3, 0.96, 1.12)]
    [InlineData(20.0, 2.63e-3, 0.96, 1.12)]
    [InlineData(24.0, 1.79e-3, 1.1, 1.9)]
    [InlineData(30.0, 9.81e-4, 1.1, 1.9)]
    [InlineData(36.0, 5.24e-4, 1.1, 1.9)]
    [InlineData(40.0, 3.40e-4, 1.1, 1.9)]
    [InlineData(42.0, 2.72e-4, 1.9, 2.3)]
    [InlineData(46.0, 1.73e-4, 1.9, 2.3)]
    [InlineData(50.0, 1.08e-4, 2.2, 2.9)]
    [InlineData(52.0, 8.54e-5, 2.2, 2.9)]
    [InlineData(54.0, 6.69e-5, 2.2, 2.9)]
    [InlineData(60.0, 3.19e-5, 2.2, 2.9)]
    [InlineData(70.0, 8.75e-6, 2.2, 2.9)]
    [InlineData(80.0, 2.29e-6, 2.2, 2.9)]
    [InlineData(90.0, 6.03e-7, 2.2, 2.9)]
    [InlineData(100.0, 1.60e-7, 2.2, 2.9)]
    public void Density_AgainstSeiff1982(double altitudeKm, double reference, double lower, double upper)
    {
        // Arrange
        var model = new MarsStandardAtmosphere();

        // Act
        double ratio = model.GetDensity(AtmosphericContext.FromAltitude(altitudeKm * 1000.0)) / reference;

        // Assert
        _output.WriteLine($"{altitudeKm} km: {ratio:F4} of Seiff (1982)");
        Assert.InRange(ratio, lower, upper);
    }
}
