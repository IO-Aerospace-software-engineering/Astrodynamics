// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The comparison of the models of two bodies of one NAIF identifier, which decides whether a propagation or a scenario
/// accepts both (#674), and the drag rule of the force builder on inputs the public paths do not form.
/// </summary>
public class ForceModelComparisonTests
{
    public ForceModelComparisonTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static string ModelPath => Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree");

    /// <summary>A custom atmosphere, equal to another by its value.</summary>
    private sealed record ScaleHeightAtmosphere(double ScaleHeight) : IAtmosphericModel
    {
        public double GetTemperature(IAtmosphericContext context) => 0.0;
        public double GetPressure(IAtmosphericContext context) => 0.0;
        public double GetDensity(IAtmosphericContext context) => System.Math.Exp(-context.Altitude / ScaleHeight);

        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context) =>
            new() { Temperature = 0.0, Pressure = 0.0, Density = GetDensity(context) };
    }

    private sealed class DerivedStandardAtmosphere : EarthStandardAtmosphere;

    /// <summary>A gravitational field of a user.</summary>
    private sealed class UserField : GravitationalField;

    /// <summary>An Earth of the kernel GM with other radii.</summary>
    private sealed class WiderEarth : CelestialBody
    {
        internal WiderEarth() : base(399, "EARTH", PlanetsAndMoons.EARTH_BODY.Mass, PlanetsAndMoons.EARTH_BODY.PolarRadius,
            PlanetsAndMoons.EARTH_BODY.EquatorialRadius + 1.0)
        {
        }
    }

    /// <summary>An item of the Earth's identifier and GM that is not a body.</summary>
    private sealed class EarthPoint : CelestialItem
    {
        internal EarthPoint() : base(399, "EARTH", PlanetsAndMoons.EARTH_BODY.Mass, null)
        {
        }
    }

    /// <summary>A subclass of the propagator that builds forces from bodies of its own choice.</summary>
    private sealed class ForceBuilderProbe : CentralBodyPropagator
    {
        private ForceBuilderProbe(Spacecraft spacecraft) : base(default, spacecraft, new RK78Integrator(), TimeSpan.FromSeconds(1.0))
        {
        }

        internal static List<ForceBase> Build(CelestialItem centralBody, CelestialItem[] items, Spacecraft spacecraft) =>
            BuildCentralBodyForces(centralBody, items, spacecraft, true, false);
    }

    [Fact]
    public void Atmospheres_CompareByValue()
    {
        var nominal = new Nrlmsise00Model(SpaceWeather.Nominal);
        var custom = new ScaleHeightAtmosphere(60e3);

        // Same instance, both absent, the stateless standard atmospheres, NRLMSISE-00 by its parameters
        Assert.True(AtmosphericModelComparer.AreEquivalent(nominal, nominal));
        Assert.True(AtmosphericModelComparer.AreEquivalent(null, null));
        Assert.True(AtmosphericModelComparer.AreEquivalent(new EarthStandardAtmosphere(), new EarthStandardAtmosphere()));
        Assert.True(AtmosphericModelComparer.AreEquivalent(new MarsStandardAtmosphere(), new MarsStandardAtmosphere()));
        Assert.True(AtmosphericModelComparer.AreEquivalent(nominal, new Nrlmsise00Model()));
        Assert.True(AtmosphericModelComparer.AreEquivalent(new Nrlmsise00Model(SpaceWeather.Nominal with { ApArray = null }),
            nominal));

        // One absent, another type, a derived standard atmosphere, another space weather or 3-hour Ap
        Assert.False(AtmosphericModelComparer.AreEquivalent(nominal, null));
        Assert.False(AtmosphericModelComparer.AreEquivalent(null, nominal));
        Assert.False(AtmosphericModelComparer.AreEquivalent(new EarthStandardAtmosphere(), nominal));
        Assert.False(AtmosphericModelComparer.AreEquivalent(new DerivedStandardAtmosphere(), new DerivedStandardAtmosphere()));
        Assert.False(AtmosphericModelComparer.AreEquivalent(nominal, new Nrlmsise00Model(SpaceWeather.Moderate)));
        Assert.False(AtmosphericModelComparer.AreEquivalent(nominal,
            new Nrlmsise00Model(SpaceWeather.Nominal with { ApArray = new ApArray { A = new[] { 4.0, 4.0, 4.0, 4.0, 4.0, 4.0, 5.0 } } })));
        Assert.False(AtmosphericModelComparer.AreEquivalent(nominal,
            new Nrlmsise00Model(SpaceWeather.Nominal with { ApArray = new ApArray { A = null } })));

        // A custom model by its Equals
        Assert.True(AtmosphericModelComparer.AreEquivalent(custom, new ScaleHeightAtmosphere(60e3)));
        Assert.False(AtmosphericModelComparer.AreEquivalent(custom, new ScaleHeightAtmosphere(50e3)));
    }

    [Fact]
    public void GravitationalFields_CompareByValue()
    {
        // Arrange
        var shared = new GeopotentialModelParameters(ModelPath, 6);
        var a = new CelestialBody(PlanetsAndMoons.EARTH, geopotentialModelParameters: shared);
        var b = new CelestialBody(PlanetsAndMoons.EARTH, geopotentialModelParameters: shared);
        var fresh = new CelestialBody(PlanetsAndMoons.EARTH,
            geopotentialModelParameters: new GeopotentialModelParameters(ModelPath, 6));
        var lines = File.ReadAllLines(ModelPath);
        var columns = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        columns[2] = "1.0E-3";
        lines[0] = string.Join(' ', columns);
        var modified = new CelestialBody(PlanetsAndMoons.EARTH, geopotentialModelParameters: new GeopotentialModelParameters(
            new MemoryStream(System.Text.Encoding.ASCII.GetBytes(string.Join('\n', lines))), 6));

        // Act, assert: one reader, equal coefficients, then the degree, the constants, the central term, a coefficient
        Assert.True(a.Field.HasSameModel(a.Field));
        Assert.Null(a.ModelDifference(b));
        Assert.Null(a.ModelDifference(fresh));
        Assert.Equal("gravitational field", a.ModelDifference(new CelestialBody(PlanetsAndMoons.EARTH,
            geopotentialModelParameters: new GeopotentialModelParameters(ModelPath, 5))));
        Assert.Equal("gravitational field", a.ModelDifference(new CelestialBody(PlanetsAndMoons.EARTH,
            geopotentialModelParameters: new GeopotentialModelParameters(ModelPath, 6, 3.986004418e14, 6378137.0))));
        Assert.Equal("gravitational field", a.ModelDifference(new CelestialBody(PlanetsAndMoons.EARTH,
            geopotentialModelParameters: new GeopotentialModelParameters(ModelPath, 6)
                { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter })));
        Assert.Equal("gravitational field", a.ModelDifference(modified));
        Assert.Equal("gravitational field", a.ModelDifference(PlanetsAndMoons.EARTH_BODY));
        Assert.Equal("gravitational field", PlanetsAndMoons.EARTH_BODY.ModelDifference(a));

        // A field of a user equals itself only
        var user = new UserField();
        Assert.True(user.HasSameModel(user));
        Assert.False(user.HasSameModel(new UserField()));
        Assert.False(new GravitationalField().HasSameModel(user));
        Assert.False(new GravitationalField().HasSameModel(null));
        Assert.True(new GravitationalField().HasSameModel(new GravitationalField()));
    }

    [Fact]
    public void Bodies_CompareTheirRadiiAndKind()
    {
        Assert.Equal("radii", PlanetsAndMoons.EARTH_BODY.ModelDifference(new WiderEarth()));
        Assert.Equal("kind of body", PlanetsAndMoons.EARTH_BODY.ModelDifference(new EarthPoint()));
        Assert.Equal("kind of body", new EarthPoint().ModelDifference(PlanetsAndMoons.EARTH_BODY));
        Assert.Null(PlanetsAndMoons.EARTH_BODY.ModelDifference(new CelestialBody(PlanetsAndMoons.EARTH)));
        Assert.Equal("atmospheric model", PlanetsAndMoons.EARTH_BODY.ModelDifference(
            new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: new EarthStandardAtmosphere())));
    }

    [Fact]
    public void ListedBodiesOfOneIdentifier_OtherThanTheCentralBody_MustHaveTheSameModels()
    {
        // Arrange: the orbit on the Earth, two Moons of different atmospheres listed
        var earth = PlanetsAndMoons.EARTH_BODY;
        var spacecraft = new Spacecraft(-7401, "FMC", 100.0, 1000.0, new Clock("fmcclk", 65536),
            new StateVector(new Vector3(6700000.0, 0, 0), new Vector3(0, 7713.0, 0), earth, new TimeSystem.Time(2021, 1, 1),
                Frames.Frame.ICRF));
        var moons = new CelestialItem[]
        {
            new CelestialBody(PlanetsAndMoons.MOON), new CelestialBody(PlanetsAndMoons.MOON, atmosphericModel: new ScaleHeightAtmosphere(1e4))
        };
        var window = new TimeSystem.Window(new TimeSystem.Time(2021, 1, 1), new TimeSystem.Time(2021, 1, 1).AddMinutes(10.0));

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            spacecraft.Propagate(window, moons, false, false, TimeSpan.FromSeconds(60.0)));

        // Assert
        Assert.Equal("celestialBodies", exception.ParamName);
        Assert.Contains("MOON", exception.Message);
        Assert.Contains("differ by their atmospheric model", exception.Message);
    }

    [Fact]
    public void BuildCentralBodyForces_TheDragOfTheCentralBody_OnceAndFirstWhenNotListed()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: new EarthStandardAtmosphere());
        var mars = new CelestialBody(PlanetsAndMoons.MARS, atmosphericModel: new MarsStandardAtmosphere());
        var spacecraft = new Spacecraft(-7402, "FMC", 100.0, 1000.0, new Clock("fmcclk2", 65536),
            new StateVector(new Vector3(6700000.0, 0, 0), new Vector3(0, 7713.0, 0), earth, new TimeSystem.Time(2021, 1, 1),
                Frames.Frame.ICRF));

        // Act: the central body unlisted, listed twice, and listed after another atmosphere
        var unlisted = ForceBuilderProbe.Build(earth, new CelestialItem[] { mars }, spacecraft).OfType<AtmosphericDrag>().ToArray();
        var twice = ForceBuilderProbe.Build(earth, new CelestialItem[] { earth, earth }, spacecraft).OfType<AtmosphericDrag>().ToArray();
        var after = ForceBuilderProbe.Build(earth, new CelestialItem[] { mars, earth }, spacecraft).OfType<AtmosphericDrag>().ToArray();

        // Assert
        Assert.Equal(new[] { earth, mars }, unlisted.Select(d => d.CelestialBody));
        Assert.Equal(new[] { earth }, twice.Select(d => d.CelestialBody));
        Assert.Equal(new[] { mars, earth }, after.Select(d => d.CelestialBody));

        // A central item that is not a body has no atmosphere
        var l1 = new LagrangePoint(LagrangePoints.L1);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ForceBuilderProbe.Build(l1, new CelestialItem[] { earth }, spacecraft));
        Assert.Contains("L1", exception.Message);
    }
}
