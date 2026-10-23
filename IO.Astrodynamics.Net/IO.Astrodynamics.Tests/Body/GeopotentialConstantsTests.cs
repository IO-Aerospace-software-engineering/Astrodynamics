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
/// The constants with which the harmonics of a geopotential are evaluated (#410, design of #444): those of the model,
/// EGM2008's for the Earth by default, the kernels' for any other body, the given ones with the constructors that take
/// them; the central term with the GM of the kernels.
/// </summary>
public class GeopotentialConstantsTests
{
    private const double Egm2008Gm = 3.986004415e14;
    private const double Egm2008Radius = 6378136.3;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    private readonly ITestOutputHelper _output;

    public GeopotentialConstantsTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static string Model => Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree");

    private static Stream ModelStream() => new FileStream(Model, FileMode.Open, FileAccess.Read, FileShare.Read);

    private static GeopotentialGravitationalField FieldOf(CelestialItem body) => (GeopotentialGravitationalField)body.Field;

    [Fact]
    public void Earth_WithTheConstructorsWithoutConstants_TakesTheConstantsOfEgm2008()
    {
        // Arrange
        var byPath = new GeopotentialModelParameters(Model, 10);
        var byStream = new GeopotentialModelParameters(ModelStream(), 10);

        // Act
        var earths = new[]
        {
            new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, byPath),
            new CelestialBody(PlanetsAndMoons.EARTH.NaifId, Frames.Frame.ICRF, Epoch, byStream),
            new CelestialBody(PlanetsAndMoons.EARTH, new GeopotentialModelParameters(Model, 2)),
            new CelestialBody(PlanetsAndMoons.EARTH.NaifId, new GeopotentialModelParameters(ModelStream(), 2))
        };

        // Assert
        Assert.Null(byPath.GravitationalParameter);
        Assert.Null(byPath.ReferenceRadius);
        Assert.Null(byStream.GravitationalParameter);
        Assert.Null(byStream.ReferenceRadius);
        foreach (var earth in earths)
        {
            var field = FieldOf(earth);
            Assert.Equal(Egm2008Gm, field.GravitationalParameter);
            Assert.Equal(Egm2008Radius, field.ReferenceRadius);
            Assert.Equal("EGM2008", field.ModelName);
            Assert.NotEqual(Egm2008Gm, earth.GM);
        }
    }

    [Fact]
    public void AnyBody_WithTheConstructorsWithConstants_TakesTheGivenConstants()
    {
        // Arrange
        var byPath = new GeopotentialModelParameters(Model, 10, 3.9e14, 6.4e6);
        var byStream = new GeopotentialModelParameters(ModelStream(), 10, 4.9e12, 1.738e6);

        // Act
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, byPath);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch, byStream);

        // Assert
        Assert.Equal(3.9e14, byPath.GravitationalParameter);
        Assert.Equal(6.4e6, byPath.ReferenceRadius);
        Assert.Equal(4.9e12, byStream.GravitationalParameter);
        Assert.Equal(1.738e6, byStream.ReferenceRadius);
        Assert.Equal(3.9e14, FieldOf(earth).GravitationalParameter);
        Assert.Equal(6.4e6, FieldOf(earth).ReferenceRadius);
        Assert.Null(FieldOf(earth).ModelName);
        Assert.Equal(4.9e12, FieldOf(moon).GravitationalParameter);
        Assert.Equal(1.738e6, FieldOf(moon).ReferenceRadius);
        Assert.Null(FieldOf(moon).ModelName);
    }

    [Theory]
    [InlineData(0.0, 6.4e6)]
    [InlineData(-3.9e14, 6.4e6)]
    [InlineData(double.NaN, 6.4e6)]
    [InlineData(double.PositiveInfinity, 6.4e6)]
    [InlineData(3.9e14, 0.0)]
    [InlineData(3.9e14, -6.4e6)]
    [InlineData(3.9e14, double.NaN)]
    [InlineData(3.9e14, double.PositiveInfinity)]
    public void Constants_NotFiniteAndPositive_Throw(double gravitationalParameter, double referenceRadius)
    {
        string name = gravitationalParameter is > 0.0 and < double.PositiveInfinity ? "referenceRadius" : "gravitationalParameter";
        var byPath = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeopotentialModelParameters(Model, 10, gravitationalParameter, referenceRadius));
        using var stream = ModelStream();
        var byStream = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeopotentialModelParameters(stream, 10, gravitationalParameter, referenceRadius));
        Assert.Equal(name, byPath.ParamName);
        Assert.Equal(name, byStream.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherBody_WithoutConstants_OrWithItsOwn_IsBitIdenticalToAFieldOfTheBodysConstants(bool ownConstants)
    {
        // Arrange: the Moon, which a field without constants evaluates with its kernel GM and equatorial radius, as
        // before #410; the same constants given explicitly make k exactly 1
        var reference = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        var parameters = ownConstants
            ? new GeopotentialModelParameters(Model, 10, reference.GM, reference.EquatorialRadius)
            : new GeopotentialModelParameters(Model, 10);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch, parameters);
        var before = new GeopotentialGravitationalField(new StreamReader(ModelStream()), 10);
        var state = new KeplerianElements(1.938e6, 0.01, 1.0, 0.3, 0.2, 0.1, moon, Epoch, Frames.Frame.ICRF)
            .ToStateVector();

        // Act
        var acceleration = moon.Field.ComputeGravitationalAcceleration(state);
        var expectedAcceleration = before.ComputeGravitationalAcceleration(state);
        var partials = new double[9];
        var expectedPartials = new double[9];
        moon.TryAccumulateGravitationalPositionPartials(state, partials);
        before.TryAccumulatePositionPartials(state, expectedPartials);

        // Assert
        Assert.Equal(ownConstants ? reference.GM : (double?)null, FieldOf(moon).GravitationalParameter);
        Assert.Null(FieldOf(moon).ModelName);
        Assert.Null(before.GravitationalParameter);
        Assert.Null(before.ReferenceRadius);
        Assert.Equal(expectedAcceleration.X, acceleration.X);
        Assert.Equal(expectedAcceleration.Y, acceleration.Y);
        Assert.Equal(expectedAcceleration.Z, acceleration.Z);
        Assert.Equal(expectedPartials, partials);
    }

    /// <summary>
    /// The field of degree 2 of the Earth against the potential of degree 2 written in Cartesian coordinates, with the
    /// GM and radius of EGM2008 for the harmonics and the kernel GM for the central term, independently of the
    /// Legendre functions and Cunningham recursions of the library.
    /// </summary>
    [Theory]
    [InlineData(6778137.0, 30.0, 40.0)]
    [InlineData(7178137.0, -60.0, -120.0)]
    [InlineData(6878137.0, 85.0, 10.0)]
    [InlineData(42164137.0, 0.05, 75.0)]
    public void DegreeTwo_MatchesTheClosedFormPotentialWithTheConstantsOfEgm2008(double radius, double latitude,
        double longitude)
    {
        // Arrange: a body-fixed position, so that no rotation enters
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters(Model, 2));
        double phi = latitude * IO.Astrodynamics.Constants.Deg2Rad, lambda = longitude * IO.Astrodynamics.Constants.Deg2Rad;
        var position = new Vector3(radius * System.Math.Cos(phi) * System.Math.Cos(lambda),
            radius * System.Math.Cos(phi) * System.Math.Sin(lambda), radius * System.Math.Sin(phi));
        var state = new StateVector(position, Vector3.Zero, earth, Epoch, earth.Frame);
        var pointMass = position * (-earth.GM / System.Math.Pow(position.Magnitude(), 3));

        // Act
        var harmonics = earth.Field.ComputeGravitationalAcceleration(state) - pointMass;
        var expected = DegreeTwo(position, Egm2008Gm, Egm2008Radius);
        var withTheKernelConstants = DegreeTwo(position, earth.GM, earth.EquatorialRadius);
        var partials = new double[9];
        var pointMassPartials = new double[9];
        earth.TryAccumulateGravitationalPositionPartials(state, partials);
        new GravitationalField().TryAccumulatePositionPartials(state, pointMassPartials);
        double partialsError = 0.0, partialsNorm = 0.0;
        for (int column = 0; column < 3; column++)
        {
            // Central differences of the closed form, 10 m: truncation (h / r)² of the Hessian, below 3e-12
            const double h = 10.0;
            var step = column == 0 ? new Vector3(h, 0, 0) : column == 1 ? new Vector3(0, h, 0) : new Vector3(0, 0, h);
            var derivative = (DegreeTwo(position + step, Egm2008Gm, Egm2008Radius) -
                              DegreeTwo(position - step, Egm2008Gm, Egm2008Radius)) * (0.5 / h);
            for (int row = 0; row < 3; row++)
            {
                double actual = partials[3 * row + column] - pointMassPartials[3 * row + column];
                double reference = row == 0 ? derivative.X : row == 1 ? derivative.Y : derivative.Z;
                partialsError += (actual - reference) * (actual - reference);
                partialsNorm += reference * reference;
            }
        }

        // Assert
        double error = (harmonics - expected).Magnitude() / expected.Magnitude();
        double before = (withTheKernelConstants - expected).Magnitude() / expected.Magnitude();
        double partialsRelative = System.Math.Sqrt(partialsError / partialsNorm);
        _output.WriteLine(
            $"MEASURE degree 2 at {radius:F0} m, {latitude}°, {longitude}°: harmonics {error:E2}, partials {partialsRelative:E2}; the kernel constants {before:E2}");
        Assert.True(error < 1e-10, $"{error:E2}");
        Assert.True(partialsRelative < 1e-8, $"{partialsRelative:E2}");
        Assert.True(before > 5e-8, $"{before:E2}");
    }

    /// <summary>
    /// The acceleration of the harmonics of degree 2, from the potential
    /// U₂ = GM a² / r⁵ [C₂₀ (3z² − r²) / 2 + 3 C₂₁ x z + 3 S₂₁ y z + 3 C₂₂ (x² − y²) + 6 S₂₂ x y], with the unnormalized
    /// C₂ₘ = N₂ₘ C̄₂ₘ, N₂₀ = √5, N₂₁ = √(5/3), N₂₂ = √(5/12) (Montenbruck and Gill, 2000, §3.2; EGM2008's C̄₂ₘ, S̄₂ₘ):
    /// ∇(f / r⁵) = ∇f / r⁵ − 5 f r / r⁷ for each polynomial f.
    /// </summary>
    private static Vector3 DegreeTwo(in Vector3 p, double gm, double a)
    {
        double c20 = System.Math.Sqrt(5.0) * -0.484165143790815e-03;
        double c21 = System.Math.Sqrt(5.0 / 3.0) * -0.206615509074176e-09;
        double s21 = System.Math.Sqrt(5.0 / 3.0) * 0.138441389137979e-08;
        double c22 = System.Math.Sqrt(5.0 / 12.0) * 0.243938357328313e-05;
        double s22 = System.Math.Sqrt(5.0 / 12.0) * -0.140027370385934e-05;
        double x = p.X, y = p.Y, z = p.Z;
        double r2 = x * x + y * y + z * z;
        double f = 0.5 * c20 * (2.0 * z * z - x * x - y * y) + 3.0 * c21 * x * z + 3.0 * s21 * y * z +
                   3.0 * c22 * (x * x - y * y) + 6.0 * s22 * x * y;
        var gradient = new Vector3(
            -c20 * x + 3.0 * c21 * z + 6.0 * c22 * x + 6.0 * s22 * y,
            -c20 * y + 3.0 * s21 * z - 6.0 * c22 * y + 6.0 * s22 * x,
            2.0 * c20 * z + 3.0 * c21 * x + 3.0 * s21 * y);
        double r5 = r2 * r2 * System.Math.Sqrt(r2);
        return (gradient * (1.0 / r5) - p * (5.0 * f / (r5 * r2))) * (gm * a * a);
    }
}
