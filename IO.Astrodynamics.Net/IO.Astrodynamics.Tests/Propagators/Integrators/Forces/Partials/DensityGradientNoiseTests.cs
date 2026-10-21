// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The rounding noise of the density gradient of the drag partials, the cause of the collapse of the step of RK7(8) with
/// the variational equations in the step control in dense air (#420): that of the Cartesian central differences, and
/// that of the gradient in geodetic coordinates that replaced them.
/// </summary>
/// <remarks>
/// The noise is the root mean square of the second difference of each component of ∇ρ along 2000 points 1 cm apart,
/// divided by √6 (the variance of the second difference of a white noise) and by |∇ρ|. The gradient varies on the scale
/// height, tens of kilometres, so over 1 cm its second difference is noise alone. The central difference of the density
/// chain converts each perturbed position to geodetic coordinates, whose rounding (about ε |r|, 1.5e-9 m) is divided by
/// the stencil (2 h ≈ 4 m): a noise of a few 1e-10 of |∇ρ| for every model, falling as 1 / h. The gradient in geodetic
/// coordinates converts the position once and differentiates the model in its own inputs, or in closed form.
/// </remarks>
public class DensityGradientNoiseTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private readonly ITestOutputHelper _output;

    public DensityGradientNoiseTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static CelestialBody Body(string model)
    {
        return model switch
        {
            "US 1976" => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
                new EarthStandardAtmosphere()),
            "NRLMSISE-00" => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
                new Nrlmsise00Model(SpaceWeather.Nominal)),
            _ => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
                new MarsStandardAtmosphere())
        };
    }

    private delegate void Gradient(in Vector3 position, double[] gradient);

    private static double Noise(Gradient method, in Vector3 start, in Vector3 direction)
    {
        const int count = 2000;
        var gradients = new double[count, 3];
        var gradient = new double[3];
        double magnitude = 0.0;
        for (int k = 0; k < count; k++)
        {
            method(start + direction * (0.01 * k), gradient);
            for (int j = 0; j < 3; j++)
            {
                gradients[k, j] = gradient[j];
            }

            magnitude = System.Math.Sqrt(gradient[0] * gradient[0] + gradient[1] * gradient[1] +
                                         gradient[2] * gradient[2]);
        }

        double sum = 0.0;
        for (int k = 1; k < count - 1; k++)
        {
            for (int j = 0; j < 3; j++)
            {
                double second = gradients[k + 1, j] - 2.0 * gradients[k, j] + gradients[k - 1, j];
                sum += second * second;
            }
        }

        return System.Math.Sqrt(sum / (3.0 * (count - 2)) / 6.0) / magnitude;
    }

    private static (AtmosphericDrag Drag, Vector3 Start, Vector3 Direction, IO.Astrodynamics.Math.Quaternion? Rotation)
        Geometry(string model, double altitude)
    {
        // A position at the altitude, off the axes, and a direction along the track
        var body = Body(model);
        double radius = body.EquatorialRadius + altitude;
        var start = new Vector3(0.8 * radius, 0.36 * radius, 0.48 * radius);
        var direction = new Vector3(0.0, 0.8, -0.6);
        var state = new StateVector(start, direction * System.Math.Sqrt(body.GM / radius), body, Epoch,
            Frames.Frame.ICRF);
        var drag = new AtmosphericDrag(new Spacecraft(-1420, "NOISE", 100.0, 1000.0, new Clock("noise", 65536), state,
            10.0, 2.2, null, 1.0), body);
        return (drag, start, direction, drag.BodyFixedRotation(state));
    }

    [Theory]
    [InlineData("US 1976", 200e3)]
    [InlineData("US 1976", 400e3)]
    [InlineData("NRLMSISE-00", 200e3)]
    [InlineData("NRLMSISE-00", 400e3)]
    [InlineData("Mars", 100e3)]
    public void CentralDifferenceOfTheDensityChain_IsNoisyAtTheRoundingOfTheGeodeticConversion(string model,
        double altitude)
    {
        // Arrange
        var (drag, start, direction, rotation) = Geometry(model, altitude);

        // Act
        double noise = Noise((in Vector3 p, double[] g) => drag.CartesianDensityGradient(p, rotation, Epoch, g),
            start, direction);
        double noiseTenTimesTheStep = Noise((in Vector3 p, double[] g) => drag.CartesianDensityGradient(p, rotation,
            Epoch, g, 10.0 * AtmosphericDrag.DensityGradientRelativeStep), start, direction);

        // Assert: 3.6e-10 to 4.0e-10 at the step of B5 for the three models (measured 2026-10-21), ten times less at ten
        // times the step: rounding, not truncation
        _output.WriteLine($"{model}, {altitude / 1000.0} km: noise {noise:E2} of |∇ρ| at δ = 3e-7, " +
                          $"{noiseTenTimesTheStep:E2} at 3e-6");
        Assert.InRange(noise, 1e-10, 1e-9);
        Assert.InRange(noise / noiseTenTimesTheStep, 5.0, 20.0);
    }

    [Theory]
    [InlineData("US 1976", 200e3)]
    [InlineData("US 1976", 400e3)]
    [InlineData("NRLMSISE-00", 200e3)]
    [InlineData("NRLMSISE-00", 400e3)]
    [InlineData("Mars", 100e3)]
    public void GradientInGeodeticCoordinates_IsSmooth(string model, double altitude)
    {
        // Arrange
        var (drag, start, direction, rotation) = Geometry(model, altitude);

        // Act
        double noise = Noise((in Vector3 p, double[] g) => drag.DensityGradient(p, rotation, Epoch, g), start,
            direction);
        double former = Noise((in Vector3 p, double[] g) => drag.CartesianDensityGradient(p, rotation, Epoch, g),
            start, direction);

        _output.WriteLine($"{model}, {altitude / 1000.0} km: noise {noise:E2} of |∇ρ|, {former / noise:F0} times " +
                          "less than the Cartesian differences");

        // Assert: 1.8e-14 to 3.5e-14 for US 1976 (dρ/dh in closed form), 1.5e-13 for Mars, 4.0e-13 to 9.8e-13 for
        // NRLMSISE-00 (differences in its inputs), 365 to 20 000 times less than the Cartesian differences (measured
        // 2026-10-21, Linux); bound at twice the worst
        Assert.True(noise < 2e-12, $"noise {noise:E2}");
    }

    [Theory]
    [InlineData(1000.0)]
    [InlineData(10000.0)]
    [InlineData(100000.0)]
    public void GradientNearThePolarAxis_IsSmooth(double axisDistance)
    {
        // Arrange: NRLMSISE-00 400 km above the north pole, at a distance from the polar axis, longitude 40°, along the
        // parallel
        var (drag, _, _, rotation) = Geometry("NRLMSISE-00", 400e3);
        double longitude = 40.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var bodyFixed = new Vector3(axisDistance * System.Math.Cos(longitude),
            axisDistance * System.Math.Sin(longitude), 6356752.0 + 400e3);
        var bodyFixedDirection = new Vector3(-System.Math.Sin(longitude), System.Math.Cos(longitude), 0.0);
        var start = rotation.HasValue ? bodyFixed.Rotate(rotation.Value.Conjugate()) : bodyFixed;
        var direction = rotation.HasValue
            ? bodyFixedDirection.Rotate(rotation.Value.Conjugate())
            : bodyFixedDirection;

        // Act
        double noise = Noise((in Vector3 p, double[] g) => drag.DensityGradient(p, rotation, Epoch, g), start,
            direction);

        _output.WriteLine($"{axisDistance / 1000.0} km from the polar axis: noise {noise:E2} of |∇ρ|");

        // Assert: 1.6e-11 at 1 km, the longitude step capped to 1e-3 rad, 2.0e-12 at 10 km and 1.0e-12 at 100 km
        // (measured 2026-10-21, Linux), 25 times less than the Cartesian differences at 1 km; bound at twice the worst
        Assert.True(noise < 3.2e-11, $"noise {noise:E2}");
    }
}
