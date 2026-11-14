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
/// drag uses with a full context, the U.S. Standard Atmosphere 1976 otherwise (decided by S. Guillet, 2026-11-14).
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

    // Geometric altitude (m) and density of table I of the U.S. Standard Atmosphere 1976 (NASA-TM-X-74335), the rows of
    // EarthStandardAtmosphereReferenceTests: kg/m³ below 86 km, ρ/ρ0 times ρ0 = 1.225 kg/m³ from 86 km
    public static TheoryData<double, double> TableIRows() => new()
    {
        { 0.0, 1.2250 }, { 11000.0, 0.36480 }, { 86000.0, 5.680e-6 * 1.225 }, { 400000.0, 2.288e-12 * 1.225 },
        { 1000000.0, 2.907e-15 * 1.225 }
    };

    private static void AssertIsStandard1976(CelestialBody earth, AtmosphericContext context, double tableDensity)
    {
        // The model's own values, bit for bit
        var standard = new EarthStandardAtmosphere();
        Assert.Equal(standard.GetAtmosphere(context), earth.GetAtmosphere(context));
        Assert.Equal(standard.GetDensity(context), earth.GetAirDensity(context));
        Assert.Equal(standard.GetPressure(context), earth.GetAirPressure(context));
        Assert.Equal(standard.GetTemperature(context), earth.GetAirTemperature(context));
        Assert.Null(earth.GetAtmosphere(context).Details);

        // Table I within the 4.2e-4 the model states from −1.05 to 1000 km
        double relative = earth.GetAirDensity(context) / tableDensity - 1.0;
        Assert.True(System.Math.Abs(relative) <= 4.2e-4, $"{context.Altitude / 1000.0} km: {relative:E2}");
    }

    [Theory]
    [MemberData(nameof(TableIRows))]
    public void ModellessEarth_WithAnAltitudeOnly_IsTheStandardAtmosphere1976(double altitude, double tableDensity)
    {
        // Decision of S. Guillet, 2026-11-14 (#674): an Earth always has at least the base atmosphere, so an Earth
        // without a model answers an altitude-only read with the U.S. Standard Atmosphere 1976
        foreach (var earth in new[] { PlanetsAndMoons.EARTH_BODY, new CelestialBody(399) })
        {
            var context = AtmosphericContext.FromAltitude(altitude);
            AssertIsStandard1976(earth, context, tableDensity);

            // The altitude overloads read the same atmosphere
            var standard = new EarthStandardAtmosphere();
            Assert.Equal(standard.GetAtmosphere(context), earth.GetAtmosphere(altitude));
            Assert.Equal(standard.GetDensity(context), earth.GetAirDensity(altitude));
            Assert.Equal(standard.GetPressure(context), earth.GetAirPressure(altitude));
            Assert.Equal(standard.GetTemperature(context), earth.GetAirTemperature(altitude));
        }
    }

    // Geometric altitude (m), temperature (K) and pressure (kPa) of table I
    [Theory]
    [InlineData(0.0, 288.150, 101.325)]
    [InlineData(400000.0, 995.83, 1.4518e-9)]
    public void ModellessEarth_WithAnAltitudeOnly_HasTheTemperatureAndPressureOfTableI(double altitude,
        double temperature, double pressure)
    {
        // Act
        var earth = PlanetsAndMoons.EARTH_BODY;
        double t = earth.GetAirTemperature(altitude) + IO.Astrodynamics.Constants.Kelvin;
        double p = earth.GetAirPressure(altitude);

        // Assert: to the last digit of the table, as EarthStandardAtmosphereReferenceTests
        Assert.Equal(temperature, t, 0.01);
        Assert.True(System.Math.Abs(p / pressure - 1.0) < 5e-4, $"{p / pressure - 1.0:E2}");
    }

    public static TheoryData<AtmosphericContext> PartialContexts() => new()
    {
        new AtmosphericContext { Altitude = 400e3, GeodeticLatitude = 0.5 },
        new AtmosphericContext { Altitude = 400e3, GeodeticLatitude = 0.5, GeodeticLongitude = 0.3 },
        new AtmosphericContext { Altitude = 400e3, GeodeticLongitude = 0.3, Epoch = Epoch },
        new AtmosphericContext { Altitude = 400e3, GeodeticLatitude = 0.5, Epoch = Epoch },
        new AtmosphericContext { Altitude = 400e3, Epoch = Epoch }
    };

    [Theory]
    [MemberData(nameof(PartialContexts))]
    public void ModellessEarth_WithAPartialContext_IsTheStandardAtmosphere1976(AtmosphericContext context)
    {
        // A context without the geodetic position or the epoch NRLMSISE-00 needs: the base atmosphere
        foreach (var earth in new[] { PlanetsAndMoons.EARTH_BODY, new CelestialBody(399) })
        {
            AssertIsStandard1976(earth, context, 2.288e-12 * 1.225);
        }
    }

    [Fact]
    public void ModellessEarth_WithAFullContext_IsStillNrlmsiseNominal()
    {
        // The full context keeps NRLMSISE-00 at nominal space weather, that of the drag, and not the 1976 standard
        var context = Full(400e3);
        var nominal = new Nrlmsise00Model(SpaceWeather.Nominal);
        foreach (var earth in new[] { PlanetsAndMoons.EARTH_BODY, new CelestialBody(399) })
        {
            Assert.Equal(nominal.GetAtmosphere(context), earth.GetAtmosphere(context));
            Assert.Equal(nominal.GetDensity(context), earth.GetAirDensity(400e3, 0.5, 0.3, Epoch));
            Assert.NotEqual(new EarthStandardAtmosphere().GetDensity(context), earth.GetAirDensity(context));
            Assert.IsType<NrlmsiseDetails>(earth.GetAtmosphere(context).Details);

            // The atmosphere of the drag stays NRLMSISE-00: it never reads the 1976 standard
            Assert.IsType<Nrlmsise00Model>(earth.Atmosphere);
        }
    }

    [Fact]
    public void ModellessEarth_AltitudeOnlyReads_FromManyThreads_AreTheStandardAtmosphere1976()
    {
        // The base atmosphere is one stateless instance shared by every thread
        var altitudes = new double[64];
        for (int i = 0; i < altitudes.Length; i++)
        {
            altitudes[i] = i * 15e3;
        }

        var densities = new double[altitudes.Length];
        System.Threading.Tasks.Parallel.For(0, altitudes.Length * 100,
            k => densities[k % altitudes.Length] = PlanetsAndMoons.EARTH_BODY.GetAirDensity(altitudes[k % altitudes.Length]));

        var standard = new EarthStandardAtmosphere();
        for (int i = 0; i < altitudes.Length; i++)
        {
            Assert.Equal(standard.GetDensity(AtmosphericContext.FromAltitude(altitudes[i])), densities[i]);
        }
    }

    [Fact]
    public void ModellessOtherBody_HasNoAtmosphere()
    {
        // Another body without a model, with an altitude only or a full context: no atmosphere, no exception
        foreach (var context in new[] { AtmosphericContext.FromAltitude(100e3), Full(100e3) })
        {
            var moon = PlanetsAndMoons.MOON_BODY;
            var atmosphere = moon.GetAtmosphere(context);
            Assert.True(double.IsNaN(atmosphere.Temperature));
            Assert.Equal(0.0, atmosphere.Pressure);
            Assert.Equal(0.0, atmosphere.Density);
            Assert.True(double.IsNaN(moon.GetAirTemperature(context)));
            Assert.Equal(0.0, moon.GetAirPressure(context));
            Assert.Equal(0.0, moon.GetAirDensity(context));
        }

        Assert.True(double.IsNaN(PlanetsAndMoons.MOON_BODY.GetAirTemperature(100e3)));
        Assert.Equal(0.0, PlanetsAndMoons.MOON_BODY.GetAirPressure(100e3));
        Assert.Equal(0.0, PlanetsAndMoons.MOON_BODY.GetAirDensity(100e3));
        Assert.Equal(0.0, PlanetsAndMoons.MOON_BODY.GetAtmosphere(100e3).Density);
        Assert.Equal(0.0, PlanetsAndMoons.MOON_BODY.GetAirDensity(100e3, 0.1, 0.2, Epoch));
    }

    [Fact]
    public void EarthWithAConfiguredModel_WithAnAltitudeOnly_IsThatModel()
    {
        // A configured model answers every context, as before: the Mars model given to an Earth, and NRLMSISE-00,
        // which needs the full context and throws without it
        var mars = new MarsStandardAtmosphere();
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: mars);
        var context = AtmosphericContext.FromAltitude(10e3);
        Assert.Equal(mars.GetAtmosphere(context), earth.GetAtmosphere(10e3));
        Assert.Equal(mars.GetDensity(context), earth.GetAirDensity(10e3));
        Assert.NotEqual(new EarthStandardAtmosphere().GetDensity(context), earth.GetAirDensity(10e3));

        var msis = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: new Nrlmsise00Model());
        Assert.Throws<ArgumentException>(() => msis.GetAirDensity(400e3));
    }

    [Fact]
    public void ModellessBodies_WithANullContext_HaveNoAtmosphere()
    {
        // A null context holds no altitude: NaN, 0 and 0, as the getters of a model-less body returned before #674
        foreach (var body in new[] { PlanetsAndMoons.EARTH_BODY, PlanetsAndMoons.MOON_BODY })
        {
            Assert.True(double.IsNaN(body.GetAirTemperature((IAtmosphericContext)null)));
            Assert.Equal(0.0, body.GetAirPressure((IAtmosphericContext)null));
            Assert.Equal(0.0, body.GetAirDensity((IAtmosphericContext)null));
            var atmosphere = body.GetAtmosphere((AtmosphericContext)null);
            Assert.True(double.IsNaN(atmosphere.Temperature));
            Assert.Equal(0.0, atmosphere.Pressure);
            Assert.Equal(0.0, atmosphere.Density);
        }
    }
}
