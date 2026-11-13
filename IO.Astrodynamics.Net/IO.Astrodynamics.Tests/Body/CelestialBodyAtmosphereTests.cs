// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Atmosphere;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// One atmosphere per body (#674, defect 16): <see cref="CelestialBody.GetAtmosphere(AtmosphericContext)"/> and the
/// context getters read the configured model, and a model-less Earth the NRLMSISE-00 at nominal space weather that its
/// drag uses.
/// </summary>
public class CelestialBodyAtmosphereTests
{
    public CelestialBodyAtmosphereTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = new(2024, 6, 21, 12, 0, 0, frame: TimeFrame.UTCFrame);

    private static AtmosphericContext Full(double altitude, double latitude = 0.5, double longitude = 0.3) =>
        AtmosphericContext.FromPlanetodetic(altitude, latitude, longitude, Epoch);

    public static TheoryData<double, double> UpperRows() => EarthStandardAtmosphereReferenceTests.UpperRows();

    [Theory]
    [MemberData(nameof(UpperRows))]
    public void GetAtmosphere_OfAnEarthWithEarthStandardAtmosphere_IsTableI(double km, double ratio)
    {
        // Arrange: U.S. Standard Atmosphere 1976 (NASA-TM-X-74335), table I, ρ/ρ0 with ρ0 = 1.225 kg/m³
        var model = new EarthStandardAtmosphere();
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: model);
        var context = Full(km * 1000.0);

        // Act
        var atmosphere = earth.GetAtmosphere(context);

        // Assert: within the 5e-4 of EarthStandardAtmosphereReferenceTests (4.2e-4 at most, 95 km), and the model's
        double relative = atmosphere.Density / (ratio * 1.225) - 1.0;
        Assert.True(System.Math.Abs(relative) < 5e-4, $"{km} km: {relative:E2}");
        Assert.Equal(model.GetAtmosphere(context), atmosphere);
        Assert.Equal(atmosphere.Density, earth.GetAirDensity(context));
    }

    public static TheoryData<double, double, double> Contexts() => new()
    {
        { 200e3, 0.5, 0.3 }, { 400e3, 0.5, 0.3 }, { 600e3, -0.2, 2.0 }, { 800e3, 0.0, -1.0 },
        { 400e3, System.Math.PI / 2.0, 0.0 }, { 400e3, -System.Math.PI / 2.0, 1.0 }, { 400e3, 0.0, System.Math.PI - 1e-3 },
        { 400e3, 0.1, -System.Math.PI + 1e-3 }
    };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void GetAtmosphere_OfAnEarthWithAConfiguredNrlmsise_IsThatModel(double altitude, double latitude,
        double longitude)
    {
        // Arrange: a storm (F10.7 250, Ap 100)
        var storm = new Nrlmsise00Model(new SpaceWeather { F107 = 250.0, F107A = 250.0, Ap = 100.0 });
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: storm);
        var context = Full(altitude, latitude, longitude);

        // Act
        var atmosphere = earth.GetAtmosphere(context);

        // Assert: the model's values, details included, and not those of the nominal model
        Assert.Equal(storm.GetAtmosphere(context), atmosphere);
        Assert.Equal(atmosphere.Density, earth.GetAirDensity(context));
        Assert.Equal(storm.GetTemperature(context), earth.GetAirTemperature(context));
        Assert.Equal(storm.GetPressure(context), earth.GetAirPressure(context));
        Assert.NotEqual(new Nrlmsise00Model().GetDensity(context), atmosphere.Density);
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void ModellessEarth_OneAtmosphere(double altitude, double latitude, double longitude)
    {
        // Arrange
        var earth = PlanetsAndMoons.EARTH_BODY;
        var nominal = new Nrlmsise00Model(SpaceWeather.Nominal);
        var context = Full(altitude, latitude, longitude);

        // Act, assert: the getters and the density of the drag force are NRLMSISE-00 at nominal space weather
        Assert.Equal(nominal.GetAtmosphere(context), earth.GetAtmosphere(context));
        Assert.Equal(nominal.GetDensity(context), earth.GetAirDensity(context));
        Assert.Equal(nominal.GetPressure(context), earth.GetAirPressure(context));
        Assert.Equal(nominal.GetTemperature(context), earth.GetAirTemperature(context));
        Assert.Equal(nominal.GetDensity(context), earth.GetAirDensity(altitude, latitude, longitude, Epoch));
        Assert.True(earth.GetAirDensity(context) > 0.0);
    }

    [Fact]
    public void ModellessBodies_WithoutAFullContext_HaveNoAtmosphere()
    {
        // An Earth with an altitude only, and another body with a full context: no model, no exception
        foreach (var (body, context) in new[]
                 {
                     (PlanetsAndMoons.EARTH_BODY, AtmosphericContext.FromAltitude(400e3)),
                     (PlanetsAndMoons.MOON_BODY, Full(100e3))
                 })
        {
            var atmosphere = body.GetAtmosphere(context);
            Assert.True(double.IsNaN(atmosphere.Temperature));
            Assert.Equal(0.0, atmosphere.Pressure);
            Assert.Equal(0.0, atmosphere.Density);
            Assert.True(double.IsNaN(body.GetAirTemperature(context)));
            Assert.Equal(0.0, body.GetAirPressure(context));
            Assert.Equal(0.0, body.GetAirDensity(context));
        }

        Assert.Equal(0.0, PlanetsAndMoons.EARTH_BODY.GetAirDensity(400e3));
        Assert.Equal(0.0, PlanetsAndMoons.MOON_BODY.GetAirDensity(100e3, 0.1, 0.2, Epoch));
    }
}
