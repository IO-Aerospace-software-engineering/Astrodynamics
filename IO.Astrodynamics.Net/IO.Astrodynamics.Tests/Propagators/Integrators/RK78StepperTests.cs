// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators;

/// <summary>
/// The RK7(8) step shared by the integrator and the evaluations made after a propagation.
/// </summary>
public class RK78StepperTests : IClassFixture<PartialsTestCases>
{
    private const double StepSize = 120.0;
    private const double StepOffset = 30.0;

    private readonly PartialsTestCases _cases;

    public RK78StepperTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void Step_WithTheContextOfTheSpacecraft_IsBitIdenticalToTheLiveForces(string name)
    {
        // Arrange
        var state = PartialsTestCases.State(name, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var forces = AllBuiltInForces(spacecraft);
        var live = NewStepper(forces, state);
        var withContext = NewStepper(forces, state);
        withContext.Context = ForceEvaluationContext.FromSpacecraft(spacecraft);

        // Act
        live.Step(state.Position, state.Velocity, state.Epoch, StepOffset, StepSize, out var livePos,
            out var liveVel, out var liveErrPos, out var liveErrVel);
        withContext.Step(state.Position, state.Velocity, state.Epoch, StepOffset, StepSize, out var pos,
            out var vel, out var errPos, out var errVel);

        // Assert
        AssertSameBits(livePos, pos);
        AssertSameBits(liveVel, vel);
        AssertSameBits(liveErrPos, errPos);
        AssertSameBits(liveErrVel, errVel);
        AssertSameBits(live.StartAcceleration, withContext.StartAcceleration);
        AssertSameBits(live.EndAcceleration, withContext.EndAcceleration);
        for (int s = 0; s < RK78ButcherTableau.Stages; s++)
        {
            AssertSameBits(live.StageStates[s].Position, withContext.StageStates[s].Position);
            AssertSameBits(live.StageStates[s].Velocity, withContext.StageStates[s].Velocity);
        }
    }

    [Fact]
    public void Step_WithTheMassDoubledInTheContext_HalvesTheDragAcceleration()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var forces = new List<ForceBase> { new AtmosphericDrag(spacecraft, _cases.Earth) };
        var live = NewStepper(forces, state);
        var heavier = NewStepper(forces, state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        heavier.Context = context with { TotalMass = 2.0 * context.TotalMass };

        // Act
        live.Step(state.Position, state.Velocity, state.Epoch, 0.0, StepSize, out _, out _, out _, out _);
        heavier.Step(state.Position, state.Velocity, state.Epoch, 0.0, StepSize, out _, out _, out _, out _);

        // Assert
        AssertSameBits(live.StartAcceleration * 0.5, heavier.StartAcceleration);
    }

    [Fact]
    public void Step_LeavesTheStageStatesAtTheStageEpochs()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var stepper = NewStepper(new List<ForceBase> { new GravitationalAcceleration(_cases.Earth) }, state);

        // Act
        stepper.Step(state.Position, state.Velocity, state.Epoch, StepOffset, StepSize, out _, out _, out _, out _);

        // Assert
        Assert.Equal(RK78ButcherTableau.Stages, stepper.StageStates.Count);
        AssertSameBits(state.Position, stepper.StageStates[0].Position);
        AssertSameBits(state.Velocity, stepper.StageStates[0].Velocity);
        for (int s = 0; s < RK78ButcherTableau.Stages; s++)
        {
            var expectedEpoch = state.Epoch.AddSeconds(StepOffset + RK78ButcherTableau.C[s] * StepSize);
            Assert.Equal(expectedEpoch, stepper.StageStates[s].Epoch);
            Assert.Same(state.Observer, stepper.StageStates[s].Observer);
            Assert.Same(state.Frame, stepper.StageStates[s].Frame);
        }
    }

    [Fact]
    public void Step_IncludesAForceAddedToTheListAfterConstruction()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var forces = new List<ForceBase>();
        var stepper = NewStepper(forces, state);
        var gravity = new GravitationalAcceleration(_cases.Earth);
        forces.Add(gravity);

        // Act
        stepper.Step(state.Position, state.Velocity, state.Epoch, 0.0, StepSize, out _, out _, out _, out _);

        // Assert
        AssertSameBits(gravity.Apply(state), stepper.StartAcceleration);
    }

    [Fact]
    public void Constructor_WithoutForces_Throws()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new RK78Stepper(null));

        // Assert
        Assert.Equal("forces", exception.ParamName);
    }

    private List<ForceBase> AllBuiltInForces(IO.Astrodynamics.Body.Spacecraft.Spacecraft spacecraft)
    {
        var forces = new List<ForceBase>
        {
            new GravitationalAcceleration(_cases.Earth),
            new ThirdBodyPerturbation(_cases.Moon, _cases.Earth),
            new ThirdBodyPerturbation(_cases.Sun, _cases.Earth),
            new AtmosphericDrag(spacecraft, _cases.Earth),
            new SolarRadiationPressure(spacecraft, new CelestialBody[] { _cases.Earth, _cases.Moon }),
            new AlbedoRadiationPressure(spacecraft, _cases.Earth),
            new ThermalRadiationPressure(spacecraft, _cases.Earth)
        };
        var cache = PartialsTestCases.Cache(_cases.Earth, _cases.Earth, _cases.Moon, _cases.Sun);
        foreach (var force in forces)
        {
            force.EphemerisCache = cache;
        }

        return forces;
    }

    private static RK78Stepper NewStepper(IReadOnlyList<ForceBase> forces, StateVector state)
    {
        var stepper = new RK78Stepper(forces);
        stepper.Reset(state.Observer, state.Frame, state.Epoch);
        return stepper;
    }

    private static void AssertSameBits(in Vector3 expected, in Vector3 actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(actual.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(actual.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Z), BitConverter.DoubleToInt64Bits(actual.Z));
    }
}
