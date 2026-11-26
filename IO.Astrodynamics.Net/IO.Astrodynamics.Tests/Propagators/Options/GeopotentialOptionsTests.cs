// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// <see cref="GeopotentialModelParameters"/> as a value and <see cref="PropagationOptions.Geopotential"/> (R06 and R12 of
/// #668, lot L4): value equality, EGM2008 embedded equal to the file of the tests at degrees 2, 10 and 70, the five rows
/// of D1's fallback rule, the TLE's point-mass Earth filled by the option (R12), one field per run (section A of the
/// re-baseline: a geopotential body shared by parallel propagations), and the CDM naming the field and atmosphere a run
/// resolved.
/// </summary>
public class GeopotentialOptionsTests
{
    private static readonly TimeSystem.Time Start = new(2021, 1, 1, 0, 0, 0, TimeScale.TDB);
    private static readonly Window OneHour = new(Start, Start + TimeSpan.FromHours(1));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60);
    private const string ModelPath = GeopotentialTestModels.To70Path;

    public GeopotentialOptionsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody Earth(GeopotentialModelParameters model = null) =>
        new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start, model);

    [Fact]
    public void Parameters_AreValues()
    {
        var byPath = new GeopotentialModelParameters(ModelPath, 10);
        Assert.Equal(byPath, new GeopotentialModelParameters(Path.GetFullPath(ModelPath), 10));
        Assert.Equal(byPath.GetHashCode(), new GeopotentialModelParameters(Path.GetFullPath(ModelPath), 10).GetHashCode());
        Assert.NotEqual(byPath, new GeopotentialModelParameters(ModelPath, 11));
        Assert.NotEqual(byPath, new GeopotentialModelParameters(ModelPath, 10, 3.9e14, 6.4e6));
        Assert.NotEqual(byPath, byPath with { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter });
        Assert.Equal(GeopotentialModelParameters.Egm2008(10), GeopotentialModelParameters.Egm2008(10));
        Assert.NotEqual(GeopotentialModelParameters.Egm2008(10), byPath);

        // A stream is read here and compared by its bytes
        using var first = File.OpenRead(ModelPath);
        using var second = File.OpenRead(ModelPath);
        var fromFirst = new GeopotentialModelParameters(first, 10);
        Assert.Equal(fromFirst, new GeopotentialModelParameters(second, 10));
        Assert.NotEqual(fromFirst, new GeopotentialModelParameters(new MemoryStream(new byte[] { 0x20 }), 10));
        Assert.Equal(10, fromFirst.GeopotentialDegree);
    }

    [Fact]
    public void Parameters_CheckTheirInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeopotentialModelParameters(ModelPath, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeopotentialModelParameters.Egm2008(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeopotentialModelParameters.Egm2008(71));
        Assert.Throws<FileNotFoundException>(() => new GeopotentialModelParameters("Data/SolarSystem/NoSuchModel", 10));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(70)]
    public void Egm2008_IsTheFileOfTheTests_BitForBit(int degree)
    {
        var embedded = Earth(GeopotentialModelParameters.Egm2008(degree));
        var file = Earth(new GeopotentialModelParameters(ModelPath, degree));
        foreach (var position in new[] { new Vector3(6778137.0, 0.0, 0.0), new Vector3(1.0e6, -4.0e6, 5.5e6),
                     new Vector3(0.0, 0.0, 6.9e6) })
        {
            var state = new StateVector(position, Vector3.Zero, embedded, Start, Frames.Frame.ICRF);
            Assert.Equal(file.EvaluateGravitationalAcceleration(state.RelativeTo(file, Aberration.None)),
                embedded.EvaluateGravitationalAcceleration(state));
        }
    }

    private static Spacecraft NewSpacecraft(ILocalizable earth) =>
        PropagationOptionsPathTests.NewSpacecraft(PropagationOptionsPathTests.Leo(earth));

    [Fact]
    public void BodyOnly_RunsWithTheBodysModel()
    {
        var earth = Earth(GeopotentialModelParameters.Egm2008(10));
        var viaOptions = NewSpacecraft(earth);
        var reference = NewSpacecraft(earth);

        var solution = viaOptions.Propagate(OneHour);
        reference.Propagate(OneHour, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>(), CovariancePropagation = CovariancePropagationOptions.None });

        PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
        Assert.Equal(GeopotentialModelParameters.Egm2008(10), solution.Options.Geopotential);
    }

    [Fact]
    public void OptionOnly_FillsAPointMassBody_AsABodyBuiltWithTheModel()
    {
        var viaOption = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var reference = NewSpacecraft(Earth(GeopotentialModelParameters.Egm2008(10)));

        var solution = viaOption.Propagate(OneHour, PropagationOptions.Default with
            { Geopotential = GeopotentialModelParameters.Egm2008(10) });
        reference.Propagate(OneHour, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>(), CovariancePropagation = CovariancePropagationOptions.None });

        PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, viaOption.PropagatedStates);
        Assert.Equal(GeopotentialModelParameters.Egm2008(10), solution.Options.Geopotential);
    }

    [Fact]
    public void BothEqual_RunWithThatModel_TheSameCoefficientsFromAnotherSourceIncluded()
    {
        var earth = Earth(new GeopotentialModelParameters(ModelPath, 10));
        var reference = NewSpacecraft(earth);
        reference.Propagate(OneHour);
        foreach (var option in new[] { new GeopotentialModelParameters(ModelPath, 10), GeopotentialModelParameters.Egm2008(10) })
        {
            var spacecraft = NewSpacecraft(earth);
            spacecraft.Propagate(OneHour, PropagationOptions.Default with { Geopotential = option });
            PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, spacecraft.PropagatedStates);
        }
    }

    [Fact]
    public void BothDifferent_Throw_NamingBoth()
    {
        var spacecraft = NewSpacecraft(Earth(GeopotentialModelParameters.Egm2008(10)));
        var exception = Assert.Throws<ArgumentException>(() => spacecraft.Propagate(OneHour, PropagationOptions.Default with
            { Geopotential = GeopotentialModelParameters.Egm2008(4) }));
        Assert.Equal("options", exception.ParamName);
        Assert.Contains("degree 10", exception.Message);
        Assert.Contains("degree 4", exception.Message);
        Assert.False(spacecraft.IsPropagated);
    }

    [Fact]
    public void Neither_IsThePointMass()
    {
        var viaOptions = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var reference = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var solution = viaOptions.Propagate(OneHour);
        reference.Propagate(OneHour, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>(), CovariancePropagation = CovariancePropagationOptions.None });
        PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
        Assert.Null(solution.Options.Geopotential);
    }

    [Fact]
    public void TleSpacecraft_OptionFillsThePointMassEarthOfTheTle()
    {
        // R12: the TLE's Earth is a point mass, which counts as not given; the reference is the twin of the TLE start (SGP4
        // at the window start rotated to ICRF, TlePropagationTests) on an Earth built with the model
        var tle = new Tle("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        var t0 = window.StartDate.ToTDB();
        var start = tle.ToOsculating(t0).ToFrame(Frames.Frame.ICRF).ToStateVector();
        var earth = Earth(GeopotentialModelParameters.Egm2008(10));
        var twin = PropagationOptionsPathTests.NewSpacecraft(new StateVector(start.Position, start.Velocity, earth, t0,
            Frames.Frame.ICRF));
        var fromTle = PropagationOptionsPathTests.NewSpacecraft(tle);

        fromTle.Propagate(window, PropagationOptions.Default with { Geopotential = GeopotentialModelParameters.Egm2008(10) });
        twin.Propagate(window, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>(), CovariancePropagation = CovariancePropagationOptions.None });

        PropagationOptionsPathTests.AssertSameStates(twin.PropagatedStates, fromTle.PropagatedStates);
    }

    [Fact]
    public void OneFieldPerRun_ParallelPropagationsSharingAGeopotentialEarth_EqualTheSequentialOnes()
    {
        // Section A of the re-baseline: each run builds its own field over the shared coefficients, so a body with a
        // geopotential is shared by concurrent propagations (before, its field's buffers were overwritten across threads)
        var earth = Earth(GeopotentialModelParameters.Egm2008(20));
        var options = PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY } };
        var sequential = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(earth)).ToArray();
        foreach (var spacecraft in sequential)
        {
            spacecraft.Propagate(OneHour, options);
        }

        var parallel = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(earth)).ToArray();
        Parallel.ForEach(parallel, spacecraft => spacecraft.Propagate(OneHour, options));

        for (int i = 0; i < parallel.Length; i++)
        {
            PropagationOptionsPathTests.AssertSameStates(sequential[0].PropagatedStates, parallel[i].PropagatedStates);
        }
    }

    [Fact]
    public void TheCdm_NamesTheFieldAndTheAtmosphereTheRunResolved()
    {
        var spacecraft = PropagationOptionsPathTests.NewSpacecraft(
            PropagationOptionsPathTests.Leo(PlanetsAndMoons.EARTH_BODY, PropagationOptionsPathTests.Covariance()));
        var solution = spacecraft.Propagate(OneHour, PropagationOptions.Default with
        {
            Geopotential = GeopotentialModelParameters.Egm2008(10), AtmosphericDrag = true,
            Atmosphere = new EarthStandardAtmosphere()
        });

        var forceModel = CdmForceModel.From(solution.Dynamics);
        Assert.Equal("EGM2008: 10D 10O", forceModel.GravityModel);
        Assert.Equal("US STANDARD ATMOSPHERE 1976", forceModel.AtmosphericModel);
    }
}
