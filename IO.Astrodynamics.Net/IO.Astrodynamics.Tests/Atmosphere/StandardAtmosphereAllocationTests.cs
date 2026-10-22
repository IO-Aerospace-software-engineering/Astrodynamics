// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The U.S. Standard Atmosphere 1976 and the Mars standard atmosphere without allocation per density (#404): the
/// density path of the forces gives the density of the public API, bit for bit, and drag with either model allocates
/// nothing per stage.
/// </summary>
public class StandardAtmosphereAllocationTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    // Every branch and boundary of the Earth model: below 11 km, 11 to 25 km, above 25 km, and the temperature clamp
    // of 2200 °C from (2200 + 131.21) / 0.00299 m, about 779.67 km; a negative altitude
    private static readonly double[] EarthAltitudes =
    {
        -1000.0, 0.0, 5000.0, 10999.999, 11000.0, 18000.0, 24999.999, 25000.0, 86e3, 350e3, 779e3, 779668.0,
        779669.0, 780e3, 2000e3
    };

    // Every branch and boundary of the Mars model: below and above 7 km, the base of the isothermal layer (#422), about
    // 49.887 km, and 112.5 km, where the curve fit reached 0 K; a negative altitude
    private static readonly double[] MarsAltitudes =
    {
        -500.0, 0.0, 6999.999, 7000.0, 30e3, 49887.0, MarsStandardAtmosphere.IsothermalLayerBase, 49888.0, 112.5e3,
        200e3, 1000e3
    };

    private readonly ITestOutputHelper _output;

    public StandardAtmosphereAllocationTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string> Models => new() { "Earth", "Mars" };

    [Theory]
    [MemberData(nameof(Models))]
    public void GeodeticDensity_IsTheDensityOfTheContext_BitForBit(string name)
    {
        // Arrange
        var model = Model(name);
        IGeodeticDensityModel geodetic = (IGeodeticDensityModel)model;
        foreach (var altitude in Altitudes(name))
        {
            var context = AtmosphericContext.FromPlanetodetic(altitude, 0.5, -1.0, Epoch);

            // Act
            double density = geodetic.GetDensity(altitude, 0.5, -1.0, Epoch);

            // Assert
            AssertSameBits(model.GetDensity(context), density);
            AssertSameBits(model.GetAtmosphere(context).Density, density);
            AssertSameBits(model.GetDensity(AtmosphericContext.FromAltitude(altitude)), density);
        }
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Density_AllocatesNothing(string name)
    {
        // Arrange
        var model = Model(name);
        IGeodeticDensityModel geodetic = (IGeodeticDensityModel)model;
        var context = AtmosphericContext.FromPlanetodetic(30e3, 0.5, 1.0, Epoch);
        _ = model.GetDensity(context);
        _ = geodetic.GetDensity(30e3, 0.5, 1.0, Epoch);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = model.GetDensity(context);
        long fromContext = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = geodetic.GetDensity(30e3, 0.5, 1.0, Epoch);
        _ = geodetic.GetDensity(5e3, 0.5, 1.0, Epoch);
        long fromPosition = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, fromContext);
        Assert.Equal(0, fromPosition);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void CelestialBody_GeodeticAirDensity_IsTheDensityOfTheContext(string name)
    {
        // Arrange
        var body = Body(name);
        foreach (var altitude in Altitudes(name))
        {
            var context = AtmosphericContext.FromPlanetodetic(altitude, -0.3, 2.0, Epoch);

            // Act and assert
            AssertSameBits(body.GetAirDensity(context), body.GetAirDensity(altitude, -0.3, 2.0, Epoch));
        }
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void DragAccelerationAndPartials_WithTheOrientationCacheOfAPropagation_AllocateNothing(string name)
    {
        // Arrange: the orientation cache of the body-fixed frame active on this thread, as in a propagation
        var body = Body(name);
        var state = State(name, body);
        var spacecraft = new Spacecraft(-1961, "DRAG", 100.0, 1000.0, new Clock("drag", 65536), state, 10.0, 2.2,
            null, 1.5);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, body);
        var cache = new PropagationFrameOrientationCache(
            new TimeSystem.Window(state.Epoch.AddHours(-1.0), state.Epoch.AddHours(1.0)), body.Frame,
            TimeSpan.FromSeconds(60.0));
        var dadr = new double[9];
        var dadv = new double[9];
        var dadCd = new double[3];
        var dadCr = new double[3];

        using (Frames.Frame.UseOrientationCache(body.Frame, cache))
        {
            var acceleration0 = force.Apply(state, context);
            force.AccumulateStatePartials(state, context, dadr, dadv);
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = force.Apply(state, context);
            long acceleration = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            force.AccumulateStatePartials(state, context, dadr, dadv);
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);
            long partials = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert: 80 bytes per density before (the atmospheric context), 80 for the acceleration and 640 for the
            // partials; the drag is not zero, so the density was evaluated
            _output.WriteLine($"{name}: acceleration {acceleration} bytes, partials {partials} bytes");
            Assert.True(acceleration0.Magnitude() > 0.0);
            Assert.Equal(0, acceleration);
            Assert.Equal(0, partials);
        }
    }

    private static double[] Altitudes(string name) => name == "Earth" ? EarthAltitudes : MarsAltitudes;

    private static IAtmosphericModel Model(string name) =>
        name == "Earth" ? new EarthStandardAtmosphere() : new MarsStandardAtmosphere();

    private static CelestialBody Body(string name) =>
        new(name == "Earth" ? PlanetsAndMoons.EARTH : PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
            Model(name));

    // LEO 350 km for the Earth, 200 km for Mars, both in ICRF
    private static StateVector State(string name, CelestialBody body)
    {
        const double deg = IO.Astrodynamics.Constants.Deg2Rad;
        double altitude = name == "Earth" ? 350e3 : 200e3;
        return new KeplerianElements(body.EquatorialRadius + altitude, 0.0005, 51.6 * deg, 20.0 * deg, 15.0 * deg,
            0.0, body, Epoch, Frames.Frame.ICRF).ToStateVector();
    }

    private static void AssertSameBits(double expected, double actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
