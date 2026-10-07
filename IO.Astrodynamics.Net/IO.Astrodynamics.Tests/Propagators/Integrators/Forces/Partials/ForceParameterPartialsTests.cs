// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Default parameter partials: the built-in forces are linear in their coefficient, so ∂a/∂Cd = a / Cd for drag and
/// ∂a/∂Cr = a / Cr for SRP, albedo and thermal pressure, whose contributions add up in the one Cr column.
/// </summary>
public class ForceParameterPartialsTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;

    public ForceParameterPartialsTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void Drag_DragCoefficientPartial_IsTheAccelerationOverCd(string stateName)
    {
        if (stateName == PartialsTestCases.Geo)
        {
            return;
        }

        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var dadCd = new double[3];
        var dadCr = new double[3];

        // Act
        force.AccumulateParameterPartials(state, context, dadCd, dadCr);

        // Assert
        AssertClose(force.Apply(state, context) / context.DragCoefficient, dadCd);
        Assert.Equal(new double[3], dadCr);
    }

    [Fact]
    public void Drag_ZeroDragCoefficient_GivesTheAccelerationPerUnitCd()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft) with { DragCoefficient = 0.0 };
        var dadCd = new double[3];

        // Act
        force.AccumulateParameterPartials(state, context, dadCd, new double[3]);

        // Assert
        AssertClose(force.Apply(state, context with { DragCoefficient = 1.0 }), dadCd);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void RadiationPressures_ReflectivityPartials_AddUpToTheAccelerationsOverCr(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        ForceBase[] forces =
        {
            new SolarRadiationPressure(spacecraft, new CelestialBody[] { _cases.Earth, _cases.Moon }),
            new AlbedoRadiationPressure(spacecraft, _cases.Earth),
            new ThermalRadiationPressure(spacecraft, _cases.Earth)
        };
        var dadCd = new double[3];
        var dadCr = new double[3];
        var total = Vector3.Zero;

        foreach (var force in forces)
        {
            var alone = new double[3];
            var acceleration = force.Apply(state, context);
            total += acceleration;

            // Act
            force.AccumulateParameterPartials(state, context, new double[3], alone);
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);

            // Assert
            AssertClose(acceleration / context.ReflectivityCoefficient, alone);
        }

        AssertClose(total / context.ReflectivityCoefficient, dadCr);
        Assert.Equal(new double[3], dadCd);
    }

    [Fact]
    public void Gravity_HasNoParameterPartials()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var dadCd = new double[3];
        var dadCr = new double[3];

        // Act
        new GravitationalAcceleration(_cases.Earth).AccumulateParameterPartials(state, context, dadCd, dadCr);
        new ThirdBodyPerturbation(_cases.Moon, _cases.Earth).AccumulateParameterPartials(state, context, dadCd, dadCr);

        // Assert
        Assert.Equal(new double[3], dadCd);
        Assert.Equal(new double[3], dadCr);
    }

    private static void AssertClose(in Vector3 expected, double[] actual)
    {
        var difference = new Vector3(actual[0], actual[1], actual[2]) - expected;
        Assert.True(expected.Magnitude() > 0.0);
        double error = difference.Magnitude() / expected.Magnitude();
        Assert.True(error < 1e-10, $"relative error {error:E2}");
    }
}
