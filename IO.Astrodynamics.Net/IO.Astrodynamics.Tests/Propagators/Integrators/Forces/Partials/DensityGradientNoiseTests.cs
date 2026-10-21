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
/// the variational equations in the step control in dense air (#420): a record of the measurement of its design.
/// </summary>
/// <remarks>
/// The noise is the root mean square of the second difference of each component of ∇ρ along 2000 points 1 cm apart,
/// divided by √6 (the variance of the second difference of a white noise) and by |∇ρ|. The gradient varies on the scale
/// height, tens of kilometres, so over 1 cm its second difference is noise alone. The central difference of the density
/// chain converts each perturbed position to geodetic coordinates, whose rounding (about ε |r|, 1.5e-9 m) is divided by
/// the stencil (2 h ≈ 4 m): a noise of a few 1e-10 of |∇ρ| for every model, falling as 1 / h.
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

    private static double Noise(AtmosphericDrag drag, in Vector3 start, in Vector3 direction,
        IO.Astrodynamics.Math.Quaternion? rotation, double relativeStep)
    {
        const int count = 2000;
        var gradients = new double[count, 3];
        var gradient = new double[3];
        double magnitude = 0.0;
        for (int k = 0; k < count; k++)
        {
            drag.DensityGradient(start + direction * (0.01 * k), rotation, Epoch, gradient, relativeStep);
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

    [Theory]
    [InlineData("US 1976", 200e3)]
    [InlineData("US 1976", 400e3)]
    [InlineData("NRLMSISE-00", 200e3)]
    [InlineData("NRLMSISE-00", 400e3)]
    [InlineData("Mars", 100e3)]
    public void CentralDifferenceOfTheDensityChain_IsNoisyAtTheRoundingOfTheGeodeticConversion(string model,
        double altitude)
    {
        // Arrange: a position at the altitude, off the axes, and a direction along the track
        var body = Body(model);
        double radius = body.EquatorialRadius + altitude;
        var start = new Vector3(0.8 * radius, 0.36 * radius, 0.48 * radius);
        var direction = new Vector3(0.0, 0.8, -0.6);
        var state = new StateVector(start, direction * System.Math.Sqrt(body.GM / radius), body, Epoch,
            Frames.Frame.ICRF);
        var drag = new AtmosphericDrag(new Spacecraft(-1420, "NOISE", 100.0, 1000.0, new Clock("noise", 65536), state,
            10.0, 2.2, null, 1.0), body);
        var rotation = drag.BodyFixedRotation(state);

        // Act
        double noise = Noise(drag, start, direction, rotation, AtmosphericDrag.DensityGradientRelativeStep);
        double noiseTenTimesTheStep = Noise(drag, start, direction, rotation,
            10.0 * AtmosphericDrag.DensityGradientRelativeStep);

        // Assert: 3.6e-10 to 4.0e-10 at the step of B5 for the three models (measured 2026-10-21), ten times less at ten
        // times the step: rounding, not truncation
        _output.WriteLine($"{model}, {altitude / 1000.0} km: noise {noise:E2} of |∇ρ| at δ = 3e-7, " +
                          $"{noiseTenTimesTheStep:E2} at 3e-6");
        Assert.InRange(noise, 1e-10, 1e-9);
        Assert.InRange(noise / noiseTenTimesTheStep, 5.0, 20.0);
    }
}
