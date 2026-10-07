// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The evaluation context: built-in forces give the same <c>double</c> through it, and read the mass and the
/// coefficients from it.
/// </summary>
public class ForceEvaluationContextTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;

    public ForceEvaluationContextTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    public static TheoryData<string> AllForces => new("gravity", "geopotential", "third body", "drag", "srp", "albedo",
        "thermal");

    public static TheoryData<string> ContextForces => new("drag", "srp", "albedo", "thermal");

    [Fact]
    public void FromSpacecraft_ReadsTotalMassAndCoefficients()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        spacecraft.AddFuelTank(new FuelTank("ctxtank", "model", "sn1", 500.0, 300.0));

        // Act
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);

        // Assert
        Assert.Equal(400.0, context.TotalMass);
        Assert.Equal(2.2, context.DragCoefficient);
        Assert.Equal(1.5, context.ReflectivityCoefficient);
    }

    [Fact]
    public void FromSpacecraft_NullSpacecraft_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ForceEvaluationContext.FromSpacecraft(null));
    }

    [Theory]
    [MemberData(nameof(AllForces))]
    public void Apply_WithContextFromSpacecraft_IsBitIdenticalToApply(string forceName)
    {
        foreach (var stateName in PartialsTestCases.StateNames)
        {
            // Arrange
            var (force, state, spacecraft) = Create(forceName, stateName);

            // Act
            var direct = force.Apply(state);
            var throughContext = force.Apply(state, ForceEvaluationContext.FromSpacecraft(spacecraft));

            // Assert
            AssertSameBits(direct, throughContext, $"{forceName}, {stateName}");
        }
    }

    [Theory]
    [MemberData(nameof(ContextForces))]
    public void Apply_DoubledMass_HalvesTheAccelerationExactly(string forceName)
    {
        // Arrange
        var (force, state, spacecraft) = Create(forceName, PartialsTestCases.Leo400);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);

        // Act
        var nominal = force.Apply(state, context);
        var heavier = force.Apply(state, context with { TotalMass = 2.0 * context.TotalMass });

        // Assert
        Assert.NotEqual(Vector3.Zero, nominal);
        AssertSameBits(nominal * 0.5, heavier, forceName);
    }

    [Theory]
    [MemberData(nameof(ContextForces))]
    public void Apply_DoubledCoefficient_DoublesTheAccelerationExactly(string forceName)
    {
        // Arrange
        var (force, state, spacecraft) = Create(forceName, PartialsTestCases.Leo400);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var doubled = forceName == "drag"
            ? context with { DragCoefficient = 2.0 * context.DragCoefficient }
            : context with { ReflectivityCoefficient = 2.0 * context.ReflectivityCoefficient };

        // Act
        var nominal = force.Apply(state, context);
        var scaled = force.Apply(state, doubled);

        // Assert
        AssertSameBits(nominal * 2.0, scaled, forceName);
    }

    [Theory]
    [MemberData(nameof(ContextForces))]
    public void Apply_OtherCoefficient_DoesNotChangeTheAcceleration(string forceName)
    {
        // Arrange
        var (force, state, spacecraft) = Create(forceName, PartialsTestCases.Leo400);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var other = forceName == "drag"
            ? context with { ReflectivityCoefficient = 2.0 }
            : context with { DragCoefficient = 1.0 };

        // Act & Assert
        AssertSameBits(force.Apply(state, context), force.Apply(state, other), forceName);
    }

    [Theory]
    [MemberData(nameof(ContextForces))]
    public void Apply_AfterABurn_ReadsTheNewMassOfTheSpacecraft(string forceName)
    {
        // Arrange: a mass-dependent force on a spacecraft whose tank is burnt, as at an impulsive maneuver
        var (force, state, spacecraft) = Create(forceName, PartialsTestCases.Leo400);
        var tank = new FuelTank("burntank", "model", "sn1", 500.0, 300.0);
        spacecraft.AddFuelTank(tank);
        var beforeBurn = force.Apply(state);
        double massBeforeBurn = spacecraft.GetTotalMass();

        // Act
        tank.Burn(123.456);
        var afterBurn = force.Apply(state);

        // Assert: the live path follows the new mass, and equals the context path at that mass
        AssertSameBits(force.Apply(state, ForceEvaluationContext.FromSpacecraft(spacecraft)), afterBurn, forceName);
        Assert.NotEqual(beforeBurn, afterBurn);
        Assert.Equal(massBeforeBurn - 123.456, ForceEvaluationContext.FromSpacecraft(spacecraft).TotalMass, 1e-9);
    }

    [Fact]
    public void Apply_ForceWithoutContextOverride_IgnoresTheContext()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();

        // Act
        var direct = force.Apply(state);
        var throughContext = force.Apply(state, new ForceEvaluationContext(1.0, 3.0, 2.0));

        // Assert
        AssertSameBits(direct, throughContext, nameof(KnownJacobianForce));
    }

    private (ForceBase Force, StateVector State, Spacecraft Spacecraft) Create(string forceName, string stateName)
    {
        var earth = forceName == "geopotential" ? _cases.EarthWithGeopotential : _cases.Earth;
        var state = PartialsTestCases.State(stateName, earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        ForceBase force = forceName switch
        {
            "gravity" => new GravitationalAcceleration(earth),
            "geopotential" => new GravitationalAcceleration(earth),
            "third body" => new ThirdBodyPerturbation(_cases.Moon, earth),
            "drag" => new AtmosphericDrag(spacecraft, earth),
            "srp" => new SolarRadiationPressure(spacecraft, new CelestialBody[] { earth, _cases.Moon }),
            "albedo" => new AlbedoRadiationPressure(spacecraft, earth),
            "thermal" => new ThermalRadiationPressure(spacecraft, earth),
            _ => throw new ArgumentOutOfRangeException(nameof(forceName))
        };
        return (force, state, spacecraft);
    }

    private static void AssertSameBits(in Vector3 expected, in Vector3 actual, string what)
    {
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected.X) == BitConverter.DoubleToInt64Bits(actual.X) &&
            BitConverter.DoubleToInt64Bits(expected.Y) == BitConverter.DoubleToInt64Bits(actual.Y) &&
            BitConverter.DoubleToInt64Bits(expected.Z) == BitConverter.DoubleToInt64Bits(actual.Z),
            $"{what}: expected {expected}, got {actual}");
    }
}
