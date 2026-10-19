// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// NRLMSISE-00 without allocation per evaluation: the work arrays kept by the model give the values of fresh ones, and
/// the density path of the forces gives the density of the public API, bit for bit.
/// </summary>
public class Nrlmsise00AllocationTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    // Every branch of the model: troposphere, stratosphere, mesosphere (below and above 72.5 km), lower thermosphere
    // (below the 120 km of the spline nodes and below 300 km) and the upper thermosphere
    private static readonly double[] Altitudes =
        { 0.0, 12e3, 32.5e3, 50e3, 72.5e3, 90e3, 110e3, 150e3, 250e3, 400e3, 1000e3, 2000e3 };

    public Nrlmsise00AllocationTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string> Configurations => new() { "nominal", "solar maximum", "AP array" };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ReusedWorkArrays_GiveTheValuesOfAFreshModel_BitForBit(string configuration)
    {
        // Arrange: one model through the whole grid, high and low altitudes in turn so that every call follows another
        // branch, against a new model for each point
        var model = Model(configuration);
        foreach (var context in Grid())
        {
            // Act
            var reused = model.GetAtmosphere(context);
            var fresh = Model(configuration).GetAtmosphere(context);

            // Assert
            AssertSameBits(fresh.Density, reused.Density);
            AssertSameBits(fresh.Temperature, reused.Temperature);
            AssertSameBits(fresh.Pressure, reused.Pressure);
            var reusedDetails = (NrlmsiseDetails)reused.Details;
            var freshDetails = (NrlmsiseDetails)fresh.Details;
            AssertSameBits(freshDetails.HeliumDensity, reusedDetails.HeliumDensity);
            AssertSameBits(freshDetails.AtomicOxygenDensity, reusedDetails.AtomicOxygenDensity);
            AssertSameBits(freshDetails.NitrogenDensity, reusedDetails.NitrogenDensity);
            AssertSameBits(freshDetails.MolecularOxygenDensity, reusedDetails.MolecularOxygenDensity);
            AssertSameBits(freshDetails.ArgonDensity, reusedDetails.ArgonDensity);
            AssertSameBits(freshDetails.HydrogenDensity, reusedDetails.HydrogenDensity);
            AssertSameBits(freshDetails.AtomicNitrogenDensity, reusedDetails.AtomicNitrogenDensity);
            AssertSameBits(freshDetails.AnomalousOxygenDensity, reusedDetails.AnomalousOxygenDensity);
            AssertSameBits(freshDetails.ExosphericTemperature, reusedDetails.ExosphericTemperature);
        }
    }

    [Fact]
    public void Calculate_ReusedInstance_GivesTheValuesOfAFreshInstance_BitForBit()
    {
        // Arrange
        var model = new NRLMSISE00();
        var flags = NrlmsiseFlags.CreateStandard();
        foreach (var context in Grid())
        {
            var input = new NrlmsiseInput
            {
                Doy = 79, Sec = 43200.0, Alt = context.Altitude, GLat = context.GeodeticLatitude!.Value,
                GLong = context.GeodeticLongitude!.Value, Lst = 13.0, F107A = 150.0, F107 = 150.0, Ap = 4.0
            };

            // Act
            var reused = model.Calculate(input, flags);
            var fresh = new NRLMSISE00().Calculate(input, flags);

            // Assert
            for (int i = 0; i < 9; i++)
            {
                AssertSameBits(fresh.D[i], reused.D[i]);
            }

            AssertSameBits(fresh.T[0], reused.T[0]);
            AssertSameBits(fresh.T[1], reused.T[1]);
        }
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void GeodeticDensity_IsTheDensityOfTheContext_BitForBit(string configuration)
    {
        // Arrange
        var model = Model(configuration);
        IGeodeticDensityModel geodetic = model;
        foreach (var context in Grid())
        {
            // Act
            double density = geodetic.GetDensity(context.Altitude, context.GeodeticLatitude!.Value,
                context.GeodeticLongitude!.Value, context.Epoch!.Value);

            // Assert
            AssertSameBits(model.GetDensity(context), density);
            AssertSameBits(model.GetAtmosphere(context).Density, density);
        }
    }

    [Fact]
    public void Density_AllocatesNothing()
    {
        // Arrange
        var model = new Nrlmsise00Model(SpaceWeather.Nominal);
        IGeodeticDensityModel geodetic = model;
        var context = AtmosphericContext.FromPlanetodetic(400e3, 0.5, 1.0, Epoch);
        _ = model.GetDensity(context);
        _ = geodetic.GetDensity(50e3, 0.5, 1.0, Epoch);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = model.GetDensity(context);
        long fromContext = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = geodetic.GetDensity(400e3, 0.5, 1.0, Epoch);
        _ = geodetic.GetDensity(50e3, 0.5, 1.0, Epoch);
        long fromPosition = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: 4.2 to 5.4 kB per density before
        Assert.Equal(0, fromContext);
        Assert.Equal(0, fromPosition);
    }

    [Fact]
    public void CelestialBody_GeodeticAirDensity_IsTheDensityOfTheContext()
    {
        // Arrange: NRLMSISE-00 through the position, a model without it through a context, and no model
        var nrlmsise = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var contextOnly = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new ContextOnlyModel());
        var none = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);

        foreach (var context in Grid())
        {
            double latitude = context.GeodeticLatitude!.Value;
            double longitude = context.GeodeticLongitude!.Value;
            var epoch = context.Epoch!.Value;

            // Act and assert
            AssertSameBits(nrlmsise.GetAirDensity(context),
                nrlmsise.GetAirDensity(context.Altitude, latitude, longitude, epoch));
            AssertSameBits(contextOnly.GetAirDensity(context),
                contextOnly.GetAirDensity(context.Altitude, latitude, longitude, epoch));

            Assert.Equal(0.0, none.GetAirDensity(context.Altitude, latitude, longitude, epoch));
        }
    }

    private static IEnumerable<AtmosphericContext> Grid()
    {
        foreach (var latitude in new[] { -1.4, 0.0, 0.7 })
        {
            foreach (var longitude in new[] { -3.0, 1.0 })
            {
                for (int k = 0; k < Altitudes.Length; k++)
                {
                    // Alternate the ends of the table: 0, 2000 km, 12 km, 1000 km, ...
                    double altitude = k % 2 == 0 ? Altitudes[k / 2] : Altitudes[Altitudes.Length - 1 - k / 2];
                    yield return AtmosphericContext.FromPlanetodetic(altitude, latitude, longitude,
                        Epoch.AddHours(3.0 * k));
                }
            }
        }
    }

    private static Nrlmsise00Model Model(string configuration)
    {
        switch (configuration)
        {
            case "nominal":
                return new Nrlmsise00Model(SpaceWeather.Nominal);
            case "solar maximum":
                return new Nrlmsise00Model(SpaceWeather.SolarMaximum);
            default:
                var flags = NrlmsiseFlags.CreateStandard();
                flags.Switches[9] = -1;
                return new Nrlmsise00Model(flags, new SpaceWeather
                {
                    F107 = 180.0, F107A = 160.0, Ap = 20.0,
                    ApArray = new ApArray { A = new[] { 20.0, 15.0, 25.0, 30.0, 18.0, 12.0, 10.0 } }
                });
        }
    }

    /// <summary>
    /// A model with the context API only, as a user's model: its density comes from the context the body builds.
    /// </summary>
    private sealed class ContextOnlyModel : IAtmosphericModel
    {
        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context) => new()
        {
            Temperature = GetTemperature(context), Pressure = GetPressure(context), Density = GetDensity(context)
        };

        public double GetTemperature(IAtmosphericContext context) => -50.0;

        public double GetPressure(IAtmosphericContext context) => 0.0;

        // Every field of the context enters the density, so a context built with another field would show
        public double GetDensity(IAtmosphericContext context) =>
            System.Math.Exp(-context.Altitude / 7000.0) * (2.0 + System.Math.Sin(context.GeodeticLatitude!.Value)) *
            (2.0 + System.Math.Cos(context.GeodeticLongitude!.Value)) *
            (1.0 + context.Epoch!.Value.TimeSpanFromJ2000().TotalDays * 1e-6);
    }

    private static void AssertSameBits(double expected, double actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
