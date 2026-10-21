// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// H2: the partials of each force at the four states of B7 against GMAT's A matrix, computed by the official GMAT API of
/// the R2026a build (<c>tools/gmat_reference/gmat_partials.py</c>; design of #408, decision 9).
/// </summary>
/// <remarks>
/// GMAT's derivative array with the A matrix on is the derivative of the state, then A = [0, I; ∂a/∂r, ∂a/∂v], 6×6
/// row-major, in km and s: ∂a/∂r in 1/s² and ∂a/∂v in 1/s, the same in SI. For drag, the densities of the two tools
/// agree to 5e-6 since #411 and #409 (<c>GmatReferenceTests.Density_AlongTheDragCase_AgainstGmat</c>), so GMAT's
/// partials are compared as they are, without the scaling by the ratio of the accelerations that took the density ratio
/// out before (decision 6 of #408).
/// </remarks>
[Trait("Category", "Validation")]
public class GmatPartialsTests : IClassFixture<PartialsTestCases>
{
    private static readonly ForceEvaluationContext Context = new(100.0, ReferenceCases.DragCoefficient,
        ReferenceCases.ReflectivityCoefficient);

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public GmatPartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var state in PartialsTestCases.StateNames)
        {
            foreach (var force in GmatCases.ForcesAt(state))
            {
                data.Add(state, force);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Partials_AgainstGmatAMatrix(string stateName, string forceName)
    {
        // Arrange
        var earth = forceName switch
        {
            "earth_geopotential" => new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, PartialsTestCases.Epoch,
                new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree",
                    GmatCases.PartialsGeopotentialDegree)),
            "drag" => new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, PartialsTestCases.Epoch, null,
                new Nrlmsise00Model(SpaceWeather.Nominal)),
            _ => new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, PartialsTestCases.Epoch)
        };
        var state = PartialsTestCases.State(stateName, earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        ForceBase force = forceName switch
        {
            "earth_point_mass" or "earth_geopotential" => new GravitationalAcceleration(earth),
            "moon" => new ThirdBodyPerturbation(_cases.Moon, earth),
            "sun" => new ThirdBodyPerturbation(_cases.Sun, earth),
            "srp" => new SolarRadiationPressure(spacecraft, new[] { earth }),
            "drag" => new AtmosphericDrag(spacecraft, earth),
            _ => throw new System.ArgumentOutOfRangeException(nameof(forceName))
        };
        var gmat = Read(stateName, forceName);

        // Act
        var dadr = new double[9];
        var dadv = new double[9];
        force.AccumulateStatePartials(state, Context, dadr, dadv);
        var acceleration = force.Apply(state, Context);

        // Assert
        var gmatAcceleration = new[] { gmat[3], gmat[4], gmat[5] }.Select(x => x * 1000.0).ToArray();
        double accelerationError = RiddersDerivative.RelativeFrobeniusError(
            new[] { acceleration.X, acceleration.Y, acceleration.Z }, gmatAcceleration);
        var gmatDadr = new double[9];
        var gmatDadv = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                gmatDadr[3 * i + j] = gmat[6 + 6 * (3 + i) + j];
                gmatDadv[3 * i + j] = gmat[6 + 6 * (3 + i) + 3 + j];
            }
        }

        double dadrError = RiddersDerivative.RelativeFrobeniusError(dadr, gmatDadr);
        string dadvText = gmatDadv.All(x => x == 0.0) && dadv.All(x => x == 0.0)
            ? "both zero"
            : RiddersDerivative.RelativeFrobeniusError(dadv, gmatDadv).ToString("E2");
        _output.WriteLine($"{stateName}, {forceName}: acceleration {accelerationError:E2}, ∂a/∂r {dadrError:E2}, " +
                          $"∂a/∂v {dadvText}");

        // Assert: the analytic threshold of B (1e-6) wherever the models agree, drag included since #411 and #409
        // (acceleration 6.7e-8 to 4.0e-7, ∂a/∂v 6.9e-8 to 3.6e-7 on 2026-10-20). The drag ∂a/∂r keeps the departure of
        // GMAT's density gradient, the library's agreeing with Ridders' reference to 5e-10 (B5): a record at twice the
        // measurement of 2026-10-20, rounded up at the second significant digit
        bool drag = forceName == "drag";
        Assert.True(accelerationError < 1e-6, $"acceleration {accelerationError:E2}");
        Assert.True(dadrError < (drag ? 7.3e-3 : 1e-6), $"∂a/∂r {dadrError:E2}");
        if (dadvText != "both zero")
        {
            Assert.True(RiddersDerivative.RelativeFrobeniusError(dadv, gmatDadv) < 1e-6, $"∂a/∂v {dadvText}");
        }
        else
        {
            Assert.False(drag, "drag has a velocity partial");
        }
    }

    private static double[] Read(string stateName, string forceName)
    {
        var path = Path.Combine(GmatCasesFileTests.DataDirectory, "partials",
            $"{GmatCases.StateId(stateName)}_{forceName}.txt");
        return File.ReadAllText(path).Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
    }
}
