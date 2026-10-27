// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The gravitational parameter of the central term of a geopotential (decision 18 of #457): the GM of the body by
/// default (D2-A of #444), or the GM of the model with <see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>,
/// in the acceleration and in its partials alike.
/// </summary>
public class GeopotentialCentralTermTests
{
    private const double Egm2008Gm = 3.986004415e14;
    private const double Egm2008Radius = 6378136.3;
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    private readonly ITestOutputHelper _output;

    public GeopotentialCentralTermTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static string Model => Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree");

    private static Stream ModelStream() => new FileStream(Model, FileMode.Open, FileAccess.Read, FileShare.Read);

    private static GeopotentialGravitationalField FieldOf(CelestialItem body) => (GeopotentialGravitationalField)body.Field;

    private static CelestialBody Earth(ushort degree, GeopotentialCentralTerm centralTerm) =>
        new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters(Model, degree) { CentralTerm = centralTerm });

    [Fact]
    public void ByDefault_TheCentralTermTakesTheGmOfTheBody()
    {
        // Arrange
        var parameters = new[]
        {
            new GeopotentialModelParameters(Model, 10), new GeopotentialModelParameters(ModelStream(), 10),
            new GeopotentialModelParameters(Model, 10, 3.9e14, 6.4e6),
            new GeopotentialModelParameters(ModelStream(), 10, 3.9e14, 6.4e6)
        };

        // Act
        var fields = Array.ConvertAll(parameters,
            p => FieldOf(new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, p)));
        var withoutBody = new GeopotentialGravitationalField(new StreamReader(ModelStream()), 10);

        // Assert
        Assert.Equal(GeopotentialCentralTerm.BodyGravitationalParameter, default(GeopotentialCentralTerm));
        Assert.All(parameters, p => Assert.Equal(GeopotentialCentralTerm.BodyGravitationalParameter, p.CentralTerm));
        Assert.All(fields, f => Assert.Equal(GeopotentialCentralTerm.BodyGravitationalParameter, f.CentralTerm));
        Assert.Equal(GeopotentialCentralTerm.BodyGravitationalParameter, withoutBody.CentralTerm);
    }

    [Fact]
    public void WithTheModelCentralTerm_TheFieldTakesIt_AndTheBodyKeepsItsGm()
    {
        // Arrange
        var byPath = new GeopotentialModelParameters(Model, 10)
            { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter };
        var byStream = new GeopotentialModelParameters(ModelStream(), 10, 3.9e14, 6.4e6)
            { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter };
        var reference = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);

        // Act
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, byPath);
        var other = new CelestialBody(PlanetsAndMoons.EARTH.NaifId, Frames.Frame.ICRF, Epoch, byStream);

        // Assert: the field takes the option and the constants of the model; the GM of the body, which every other
        // point-mass computation reads, and the name of the model in the CDM do not change
        Assert.Equal(GeopotentialCentralTerm.ModelGravitationalParameter, byPath.CentralTerm);
        Assert.Equal(GeopotentialCentralTerm.ModelGravitationalParameter, FieldOf(earth).CentralTerm);
        Assert.Equal(Egm2008Gm, FieldOf(earth).GravitationalParameter);
        Assert.Equal(Egm2008Radius, FieldOf(earth).ReferenceRadius);
        Assert.Equal("EGM2008", FieldOf(earth).ModelName);
        Assert.Equal(reference.GM, earth.GM);
        Assert.Equal(GeopotentialCentralTerm.ModelGravitationalParameter, FieldOf(other).CentralTerm);
        Assert.Equal(3.9e14, FieldOf(other).GravitationalParameter);
        Assert.Equal(reference.GM, other.GM);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void CentralTerm_NotAMember_Throws(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeopotentialModelParameters(Model, 10) { CentralTerm = (GeopotentialCentralTerm)value });
        Assert.Equal("CentralTerm", exception.ParamName);
    }

    public static TheoryData<GeopotentialCentralTerm, string, bool> DegreeZeroCases
    {
        get
        {
            var data = new TheoryData<GeopotentialCentralTerm, string, bool>();
            foreach (var centralTerm in new[]
                     {
                         GeopotentialCentralTerm.BodyGravitationalParameter,
                         GeopotentialCentralTerm.ModelGravitationalParameter
                     })
            {
                foreach (var state in new[] { "LEO", "GEO", "polar" })
                {
                    data.Add(centralTerm, state, false);
                    data.Add(centralTerm, state, true);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// A field of degree 0 is the point mass of the GM of its central term: the GM of the body by default, the GM of the
    /// model (EGM2008's for the Earth) with the option, in the acceleration and in its partials, for a state in ICRF and
    /// in the Earth-fixed frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(DegreeZeroCases))]
    public void DegreeZero_IsThePointMassOfTheGmOfTheCentralTerm(GeopotentialCentralTerm centralTerm, string stateName,
        bool earthFixed)
    {
        // Arrange
        var earth = Earth(0, centralTerm);
        var state = State(stateName, earth);
        if (earthFixed)
        {
            state = state.ToFrame(earth.Frame).ToStateVector();
        }

        double gm = centralTerm == GeopotentialCentralTerm.ModelGravitationalParameter ? Egm2008Gm : earth.GM;
        var position = state.Position;
        double r = position.Magnitude();
        var expected = position * (-gm / (r * r * r));
        var expectedPartials = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                expectedPartials[3 * i + j] = gm * (3.0 * Component(position, i) * Component(position, j) / (r * r) -
                                                    (i == j ? 1.0 : 0.0)) / (r * r * r);
            }
        }

        // Act
        var acceleration = earth.Field.ComputeGravitationalAcceleration(state);
        var partials = new double[9];
        earth.TryAccumulateGravitationalPositionPartials(state, partials);
        var samePartials = new double[9];
        PointMassPartials.Accumulate(position, gm, samePartials);

        // Assert
        double error = (acceleration - expected).Magnitude() / expected.Magnitude();
        double partialsError = RelativeFrobenius(partials, expectedPartials);
        double fromTheOther = (acceleration - position * (-(gm == earth.GM ? Egm2008Gm : earth.GM) / (r * r * r)))
            .Magnitude() / expected.Magnitude();
        _output.WriteLine($"MEASURE central term with the model GM (decision 18 of #457), degree 0, {centralTerm}, " +
                          $"{stateName}, {(earthFixed ? "Earth-fixed" : "ICRF")}: acceleration {error:E2}, partials " +
                          $"{partialsError:E2} from the point mass; {fromTheOther:E3} from the point mass of the other GM");
        // Bounds, twice the worst of the twelve cases over Linux, Windows and macOS arm64 (CI of 77b509c, 2026-10-27, the worst on Linux:
        // acceleration 8.2e-16, partials 5.9e-16, the rounding of the rotation to the Earth-fixed frame and back)
        Assert.True(error < 1.7e-15, $"acceleration {error:E2}");
        Assert.True(partialsError < 1.2e-15, $"partials {partialsError:E2}");
        Assert.Equal(samePartials, partials);
        Assert.True(System.Math.Abs(fromTheOther - System.Math.Abs(Egm2008Gm / earth.GM - 1.0)) < 1e-12,
            $"{fromTheOther:E3}");
    }

    /// <summary>
    /// The field of degree 2 of the Earth with the option against the whole field of EGM2008 written in Cartesian
    /// coordinates, central term included, with its GM and radius (<see cref="GeopotentialConstantsTests.DegreeTwo"/>),
    /// independently of the Legendre functions and Cunningham recursions of the library.
    /// </summary>
    [Theory]
    [InlineData(6778137.0, 30.0, 40.0)]
    [InlineData(7178137.0, -60.0, -120.0)]
    [InlineData(6878137.0, 85.0, 10.0)]
    [InlineData(42164137.0, 0.05, 75.0)]
    public void DegreeTwo_WithTheModelCentralTerm_IsTheClosedFormFieldOfEgm2008(double radius, double latitude,
        double longitude)
    {
        // Arrange: a body-fixed position, so that no rotation enters
        var earth = Earth(2, GeopotentialCentralTerm.ModelGravitationalParameter);
        double phi = latitude * Deg, lambda = longitude * Deg;
        var position = new Vector3(radius * System.Math.Cos(phi) * System.Math.Cos(lambda),
            radius * System.Math.Cos(phi) * System.Math.Sin(lambda), radius * System.Math.Sin(phi));
        var state = new StateVector(position, Vector3.Zero, earth, Epoch, earth.Frame);
        Vector3 Field(in Vector3 p) => p * (-Egm2008Gm / System.Math.Pow(p.Magnitude(), 3)) +
                                       GeopotentialConstantsTests.DegreeTwo(p, Egm2008Gm, Egm2008Radius);

        // Act
        var acceleration = earth.Field.ComputeGravitationalAcceleration(state);
        var partials = new double[9];
        earth.TryAccumulateGravitationalPositionPartials(state, partials);

        // Assert: the acceleration, and the partials against central differences of the closed form (10 m: truncation
        // (h / r)² of the Hessian, below 3e-12)
        var expected = Field(position);
        var expectedPartials = new double[9];
        for (int column = 0; column < 3; column++)
        {
            const double h = 10.0;
            var step = column == 0 ? new Vector3(h, 0, 0) : column == 1 ? new Vector3(0, h, 0) : new Vector3(0, 0, h);
            var derivative = (Field(position + step) - Field(position - step)) * (0.5 / h);
            for (int row = 0; row < 3; row++)
            {
                expectedPartials[3 * row + column] = Component(derivative, row);
            }
        }

        double error = (acceleration - expected).Magnitude() / expected.Magnitude();
        double partialsError = RelativeFrobenius(partials, expectedPartials);
        _output.WriteLine($"MEASURE central term with the model GM (decision 18 of #457), degree 2 at {radius:F0} m, " +
                          $"{latitude}°, {longitude}°: acceleration {error:E2}, partials {partialsError:E2} from the " +
                          "closed form of EGM2008");
        // Bounds, twice the worst of the four positions over Linux, Windows and macOS arm64 (CI of 77b509c, 2026-10-27, the worst on Linux:
        // acceleration 3.8e-16, partials 2.9e-10 at GEO, where the central differences of the closed form lose ε r / h,
        // 4.7e-10); the GM of the body in the central term would leave 1.5e-8 in both
        Assert.True(error < 7.7e-16, $"acceleration {error:E2}");
        Assert.True(partialsError < 5.9e-10, $"partials {partialsError:E2}");
    }

    /// <summary>
    /// With the option, the field of the Earth for a state relative to the solar system barycentre, as a propagation in
    /// SSB mode evaluates it, is the field for the same state relative to the Earth: the option is the field's, whatever
    /// the observer of the state.
    /// </summary>
    [Theory]
    [InlineData("LEO")]
    [InlineData("GEO")]
    [InlineData("polar")]
    public void WithTheModelCentralTerm_AStateRelativeToTheBarycentre_TakesTheSameField(string stateName)
    {
        // Arrange
        var earth = Earth(10, GeopotentialCentralTerm.ModelGravitationalParameter);
        var state = State(stateName, earth);
        var relativeToSsb = state.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();

        // Act
        var expected = earth.EvaluateGravitationalAcceleration(state);
        var acceleration = earth.EvaluateGravitationalAcceleration(relativeToSsb);
        var expectedPartials = new double[9];
        var partials = new double[9];
        earth.TryAccumulateGravitationalPositionPartials(state, expectedPartials);
        earth.TryAccumulateGravitationalPositionPartials(relativeToSsb, partials);

        // Assert: the translation to the barycentre and back rounds the position to its last bits at 1.5e11 m
        double error = (acceleration - expected).Magnitude() / expected.Magnitude();
        double partialsError = RelativeFrobenius(partials, expectedPartials);
        _output.WriteLine($"MEASURE central term with the model GM (decision 18 of #457), {stateName} relative to the " +
                          $"SSB: acceleration {error:E2}, partials {partialsError:E2} from the same state relative to the Earth");
        // Bounds, twice the worst of the three states over Linux, Windows and macOS arm64 (CI of 77b509c, 2026-10-27, the worst on Linux:
        // acceleration 1.7e-12, partials 2.6e-12, LEO); the other GM would leave 1.5e-8
        Assert.True(error < 3.5e-12, $"acceleration {error:E2}");
        Assert.True(partialsError < 5.3e-12, $"partials {partialsError:E2}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherBody_WithTheModelCentralTerm_IsBitIdenticalToTheGmOfTheBody(bool ownConstants)
    {
        // Arrange: the Moon, whose field takes its kernel GM without constants, or the same given explicitly: its model
        // GM is then the GM of the body, and the option changes nothing
        var reference = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        GeopotentialModelParameters Parameters(GeopotentialCentralTerm centralTerm) => ownConstants
            ? new GeopotentialModelParameters(Model, 10, reference.GM, reference.EquatorialRadius)
                { CentralTerm = centralTerm }
            : new GeopotentialModelParameters(Model, 10) { CentralTerm = centralTerm };
        var withBody = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch,
            Parameters(GeopotentialCentralTerm.BodyGravitationalParameter));
        var withModel = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch,
            Parameters(GeopotentialCentralTerm.ModelGravitationalParameter));
        var state = new KeplerianElements(1.938e6, 0.01, 1.0, 0.3, 0.2, 0.1, withModel, Epoch, Frames.Frame.ICRF)
            .ToStateVector();

        // Act and assert
        AssertBitIdentical(withBody, withModel, state);
    }

    [Fact]
    public void Earth_WithTheGmOfTheBodyGivenExplicitly_IsBitIdenticalToTheDefault()
    {
        // Arrange
        var byDefault = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters(Model, 10));
        var explicitly = Earth(10, GeopotentialCentralTerm.BodyGravitationalParameter);
        var state = State("LEO", explicitly);

        // Act and assert
        AssertBitIdentical(byDefault, explicitly, state);
    }

    private static void AssertBitIdentical(CelestialBody expected, CelestialBody actual, StateVector state)
    {
        var acceleration = actual.Field.ComputeGravitationalAcceleration(state);
        var expectedAcceleration = expected.Field.ComputeGravitationalAcceleration(
            new StateVector(state.Position, state.Velocity, expected, state.Epoch, state.Frame));
        var partials = new double[9];
        var expectedPartials = new double[9];
        actual.TryAccumulateGravitationalPositionPartials(state, partials);
        expected.TryAccumulateGravitationalPositionPartials(
            new StateVector(state.Position, state.Velocity, expected, state.Epoch, state.Frame), expectedPartials);
        Assert.Equal(expectedAcceleration.X, acceleration.X);
        Assert.Equal(expectedAcceleration.Y, acceleration.Y);
        Assert.Equal(expectedAcceleration.Z, acceleration.Z);
        Assert.Equal(expectedPartials, partials);
    }

    /// <summary>LEO 400 km at 51.6°, GEO, or a polar LEO at 89.9° of latitude, in ICRF.</summary>
    private static StateVector State(string name, CelestialBody earth)
    {
        return name switch
        {
            "LEO" => new KeplerianElements(6778137.0, 0.0005, 51.6 * Deg, 20.0 * Deg, 15.0 * Deg, 0.0, earth, Epoch,
                Frames.Frame.ICRF).ToStateVector(),
            "GEO" => new KeplerianElements(42164137.0, 0.0002, 0.05 * Deg, 0.0, 30.0 * Deg, 0.0, earth, Epoch,
                Frames.Frame.ICRF).ToStateVector(),
            _ => new KeplerianElements(6778137.0, 0.0005, 90.0 * Deg, 20.0 * Deg, 89.9 * Deg, 0.0, earth, Epoch,
                Frames.Frame.ICRF).ToStateVector()
        };
    }

    private static double Component(in Vector3 vector, int index) =>
        index == 0 ? vector.X : index == 1 ? vector.Y : vector.Z;

    private static double RelativeFrobenius(double[] actual, double[] expected)
    {
        double difference = 0.0, norm = 0.0;
        for (int k = 0; k < actual.Length; k++)
        {
            difference += (actual[k] - expected[k]) * (actual[k] - expected[k]);
            norm += expected[k] * expected[k];
        }

        return System.Math.Sqrt(difference / norm);
    }
}
