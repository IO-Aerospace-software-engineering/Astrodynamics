// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The partials contract of <see cref="ForceBase"/> and its default path, central differences (B1).
/// </summary>
public class ForceBasePartialsTests : IClassFixture<PartialsTestCases>
{
    private static readonly ForceEvaluationContext Context = new(100.0, 2.2, 1.5);

    private readonly PartialsTestCases _cases;

    public ForceBasePartialsTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void DefaultStatePartials_ForceWithKnownJacobian_AgreeWithin1e8(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var force = new KnownJacobianForce();
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        Assert.True(RiddersDerivative.RelativeFrobeniusError(dadr, KnownJacobianForce.PositionJacobian(state.Position))
                    < 1e-8);
        Assert.True(RiddersDerivative.RelativeFrobeniusError(dadv, KnownJacobianForce.VelocityJacobian()) < 1e-8);
    }

    [Fact]
    public void DefaultStatePartials_AreAddedToTheBuffers()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();
        var alone = (Dadr: new double[9], Dadv: new double[9]);
        force.AccumulateStatePartials(state, Context, alone.Dadr, alone.Dadv);
        var dadr = Enumerable.Repeat(1.0, 9).ToArray();
        var dadv = Enumerable.Repeat(-1.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        Assert.Equal(alone.Dadr.Select(x => 1.0 + x), dadr);
        Assert.Equal(alone.Dadv.Select(x => -1.0 + x), dadv);
    }

    [Fact]
    public void DefaultStatePartials_ForceIndependentOfVelocity_LeaveTheVelocityBlockUntouched()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownPositionOnlyForce();
        var dadr = new double[9];
        var dadv = Enumerable.Repeat(7.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        Assert.All(dadv, x => Assert.Equal(7.0, x));
        Assert.True(RiddersDerivative.RelativeFrobeniusError(dadr, KnownJacobianForce.PositionJacobian(state.Position))
                    < 1e-8);
    }

    [Fact]
    public void DefaultStatePartials_LeaveTheStateUnchanged()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.HeoPerigee, _cases.Earth);
        var position = state.Position;
        var velocity = state.Velocity;
        var epoch = state.Epoch;

        // Act
        new KnownJacobianForce().AccumulateStatePartials(state, Context, new double[9], new double[9]);

        // Assert
        Assert.Equal(position, state.Position);
        Assert.Equal(velocity, state.Velocity);
        Assert.Equal(epoch, state.Epoch);
    }

    [Fact]
    public void DefaultStatePartials_StatesOfDifferentObservers_EachUseTheirOwnObserver()
    {
        // Arrange: the work state of the force is created for the first observer, then again for the second
        var earthState = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var moonState = new StateVector(earthState.Position, earthState.Velocity, _cases.Moon, earthState.Epoch,
            Frames.Frame.ICRF);
        var force = new ObserverRecordingForce();

        // Act
        force.AccumulateStatePartials(earthState, Context, new double[9], new double[9]);
        var firstObserver = force.LastObserver;
        force.AccumulateStatePartials(moonState, Context, new double[9], new double[9]);

        // Assert
        Assert.Same(_cases.Earth, firstObserver);
        Assert.Same(_cases.Moon, force.LastObserver);
    }

    [Fact]
    public void DefaultStatePartials_AllocateNothingOnceTheWorkStateExists()
    {
        // Arrange: the first call creates the work state of the force
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();
        var dadr = new double[9];
        var dadv = new double[9];
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        force.AccumulateStatePartials(state, Context, dadr, dadv);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(8, 9)]
    [InlineData(9, 10)]
    [InlineData(0, 0)]
    public void AccumulateStatePartials_BufferOfWrongLength_Throws(int dadrLength, int dadvLength)
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            force.AccumulateStatePartials(state, Context, new double[dadrLength], new double[dadvLength]));
        Assert.Throws<ArgumentException>(() => force.AccumulateStatePartialsByCentralDifferences(state, Context,
            new double[dadrLength], new double[dadvLength], 1e-6, 1e-6));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void AccumulateParameterPartials_BufferOfWrongLength_Throws(int dadCdLength, int dadCrLength)
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            force.AccumulateParameterPartials(state, Context, new double[dadCdLength], new double[dadCrLength]));
    }

    [Theory]
    [InlineData(0.0, 1e-6)]
    [InlineData(1e-6, -1e-6)]
    public void AccumulateStatePartialsByCentralDifferences_NonPositiveStep_Throws(double positionStep,
        double velocityStep)
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => force.AccumulateStatePartialsByCentralDifferences(state,
            Context, new double[9], new double[9], positionStep, velocityStep));
    }

    [Fact]
    public void AccumulateStatePartials_DefaultSteps_EqualCentralDifferencesWithTheDefaultSteps()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Geo, _cases.Earth);
        var force = new KnownJacobianForce();
        var expectedDadr = new double[9];
        var expectedDadv = new double[9];
        force.AccumulateStatePartialsByCentralDifferences(state, Context, expectedDadr, expectedDadv,
            ForceBase.DefaultPositionRelativeStep, ForceBase.DefaultVelocityRelativeStep);
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        Assert.Equal(expectedDadr, dadr);
        Assert.Equal(expectedDadv, dadv);
    }

    [Fact]
    public void DefaultParameterPartials_ForceWithoutParameters_AddNothing()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new KnownJacobianForce();
        var dadCd = new[] { 1.0, 2.0, 3.0 };
        var dadCr = new[] { 4.0, 5.0, 6.0 };

        // Act
        force.AccumulateParameterPartials(state, Context, dadCd, dadCr);

        // Assert
        Assert.Equal(ForceParameters.None, force.Parameters);
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, dadCd);
        Assert.Equal(new[] { 4.0, 5.0, 6.0 }, dadCr);
    }

    [Fact]
    public void BuiltInForces_DeclareTheirVelocityDependenceAndParameters()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);

        // Act & Assert
        AssertDeclaration(new GravitationalAcceleration(_cases.Earth), false, ForceParameters.None);
        AssertDeclaration(new ThirdBodyPerturbation(_cases.Moon, _cases.Earth), false, ForceParameters.None);
        AssertDeclaration(new AtmosphericDrag(spacecraft, _cases.Earth), true, ForceParameters.DragCoefficient);
        AssertDeclaration(new SolarRadiationPressure(spacecraft, new[] { _cases.Earth }), false,
            ForceParameters.ReflectivityCoefficient);
        AssertDeclaration(new AlbedoRadiationPressure(spacecraft, _cases.Earth), false,
            ForceParameters.ReflectivityCoefficient);
        AssertDeclaration(new ThermalRadiationPressure(spacecraft, _cases.Earth), false,
            ForceParameters.ReflectivityCoefficient);
        AssertDeclaration(new KnownJacobianForce(), true, ForceParameters.None);
    }

    private static void AssertDeclaration(ForceBase force, bool dependsOnVelocity, ForceParameters parameters)
    {
        Assert.Equal(dependsOnVelocity, force.DependsOnVelocity);
        Assert.Equal(parameters, force.Parameters);
    }

    private sealed class ObserverRecordingForce : KnownJacobianForce
    {
        internal ILocalizable LastObserver { get; private set; }

        public override IO.Astrodynamics.Math.Vector3 Apply(StateVector stateVector)
        {
            LastObserver = stateVector.Observer;
            return base.Apply(stateVector);
        }
    }
}
