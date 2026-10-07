// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Partials of a state in an inertial frame other than ICRF: they are the ICRF partials rotated, R J Rᵀ, since every
/// force input is evaluated in the frame of the state (#357).
/// </summary>
public class NonIcrfFramePartialsTests : IClassFixture<PartialsTestCases>
{
    private static readonly Frames.Frame Ecliptic = Frames.Frame.ECLIPTIC_J2000;

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public NonIcrfFramePartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void PointMassAndThirdBody_StateInEclipticJ2000_AreTheRotatedIcrfPartials(string stateName)
    {
        // Arrange
        var icrfState = PartialsTestCases.State(stateName, _cases.Earth);
        var state = icrfState.ToFrame(Ecliptic).ToStateVector();
        var rotation = Rotation(Ecliptic);
        var context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var cases = new (string Name, ForceBase IcrfForce, ForceBase Force)[]
        {
            ("point mass", new GravitationalAcceleration(_cases.Earth), new GravitationalAcceleration(_cases.Earth)),
            ("Moon, SPICE", new ThirdBodyPerturbation(_cases.Moon, _cases.Earth),
                new ThirdBodyPerturbation(_cases.Moon, _cases.Earth)),
            ("Moon, cache", new ThirdBodyPerturbation(_cases.Moon, _cases.Earth)
                {
                    EphemerisCache = PartialsTestCases.Cache(_cases.Earth, _cases.Moon)
                },
                new ThirdBodyPerturbation(_cases.Moon, _cases.Earth)
                {
                    EphemerisCache = PartialsTestCases.Cache(Ecliptic, _cases.Earth, _cases.Moon)
                }),
            ("Sun, SPICE", new ThirdBodyPerturbation(_cases.Sun, _cases.Earth),
                new ThirdBodyPerturbation(_cases.Sun, _cases.Earth))
        };

        foreach (var (name, icrfForce, force) in cases)
        {
            var icrfDadr = new double[9];
            var dadr = new double[9];

            // Act
            icrfForce.AccumulateStatePartials(icrfState, context, icrfDadr, new double[9]);
            force.AccumulateStatePartials(state, context, dadr, new double[9]);

            // Assert
            double error = RiddersDerivative.RelativeFrobeniusError(dadr, Conjugate(rotation, icrfDadr));
            _output.WriteLine($"{stateName}, {name}: relative error {error:E2}");
            Assert.True(error < 1e-12, $"{stateName}, {name}: relative error {error:E2}");
        }
    }

    [Theory]
    [MemberData(nameof(States))]
    public void Drag_StateInEclipticJ2000_GivesTheRotatedIcrfPartials(string stateName)
    {
        if (stateName == PartialsTestCases.Geo)
        {
            return;
        }

        // Arrange: the default central differences, on both blocks
        var icrfState = PartialsTestCases.State(stateName, _cases.Earth);
        var state = icrfState.ToFrame(Ecliptic).ToStateVector();
        var spacecraft = PartialsTestCases.Spacecraft(icrfState);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var drag = new AtmosphericDrag(spacecraft, _cases.Earth);
        var rotation = Rotation(Ecliptic);
        var icrfDadr = new double[9];
        var icrfDadv = new double[9];
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        drag.AccumulateStatePartials(icrfState, context, icrfDadr, icrfDadv);
        drag.AccumulateStatePartials(state, context, dadr, dadv);

        // Assert: within the error of the default path on drag (1.6e-8 measured by the step study)
        double dadrError = RiddersDerivative.RelativeFrobeniusError(dadr, Conjugate(rotation, icrfDadr));
        double dadvError = RiddersDerivative.RelativeFrobeniusError(dadv, Conjugate(rotation, icrfDadv));
        _output.WriteLine($"{stateName}: dadr {dadrError:E2}, dadv {dadvError:E2}");
        Assert.True(dadrError < 1e-6, $"{stateName}: dadr relative error {dadrError:E2}");
        Assert.True(dadvError < 1e-6, $"{stateName}: dadv relative error {dadvError:E2}");
    }

    /// <summary>
    /// R, row-major, such that x_frame = R x_ICRF: its columns are the ICRF axes expressed in the frame.
    /// </summary>
    private double[] Rotation(Frames.Frame frame)
    {
        var rotation = new double[9];
        Vector3[] axes = { Vector3.VectorX, Vector3.VectorY, Vector3.VectorZ };
        for (int j = 0; j < 3; j++)
        {
            var column = new StateVector(axes[j], Vector3.Zero, _cases.Earth, PartialsTestCases.Epoch,
                Frames.Frame.ICRF).ToFrame(frame).ToStateVector().Position;
            rotation[j] = column.X;
            rotation[3 + j] = column.Y;
            rotation[6 + j] = column.Z;
        }

        return rotation;
    }

    /// <summary>
    /// R J Rᵀ, row-major.
    /// </summary>
    private static double[] Conjugate(double[] rotation, double[] jacobian)
    {
        var result = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                double sum = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    for (int l = 0; l < 3; l++)
                    {
                        sum += rotation[3 * i + j] * jacobian[3 * j + l] * rotation[3 * k + l];
                    }
                }

                result[3 * i + k] = sum;
            }
        }

        return result;
    }
}
