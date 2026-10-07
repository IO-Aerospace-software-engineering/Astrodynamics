// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Analytic partials of the central-body point mass (B2), against Ridders' reference, and the fallback of the
/// geopotential to the default central differences.
/// </summary>
public class GravitationalAccelerationPartialsTests : IClassFixture<PartialsTestCases>
{
    private static readonly ForceEvaluationContext Context = new(100.0, 2.2, 1.5);

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public GravitationalAccelerationPartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    [Theory]
    [MemberData(nameof(States))]
    public void PointMass_AnalyticPartials_MatchRiddersWithin1e6(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var force = new GravitationalAcceleration(_cases.Earth);
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        AssertMatchesRidders(force, state, state.Position.Magnitude(), dadr);
        Assert.All(dadv, x => Assert.Equal(0.0, x));
    }

    [Fact]
    public void PointMass_AnalyticPartials_AllocateNothing()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new GravitationalAcceleration(_cases.Earth);
        var dadr = new double[9];
        var dadv = new double[9];
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Act
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        force.AccumulateStatePartials(state, Context, dadr, dadv);
        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void PointMass_StateRelativeToTheSun_MatchRiddersWithAndWithoutCache(string stateName)
    {
        // Arrange: the acceleration is evaluated relative to the Earth, from the cache or through SPICE
        var geocentric = PartialsTestCases.State(stateName, _cases.Earth);
        var heliocentric = geocentric.RelativeTo(_cases.Sun, Aberration.None).ToStateVector();
        var withoutCache = new GravitationalAcceleration(_cases.Earth);
        var withCache = new GravitationalAcceleration(_cases.Earth)
        {
            EphemerisCache = PartialsTestCases.Cache(_cases.Sun, _cases.Earth)
        };

        foreach (var force in new[] { withoutCache, withCache })
        {
            var dadr = new double[9];

            // Act
            force.AccumulateStatePartials(heliocentric, Context, dadr, new double[9]);

            // Assert
            AssertMatchesRidders(force, heliocentric, geocentric.Position.Magnitude(), dadr);
        }
    }

    [Theory]
    [MemberData(nameof(States))]
    public void Geopotential_FallsBackToTheDefaultCentralDifferences(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.EarthWithGeopotential);
        var force = new GravitationalAcceleration(_cases.EarthWithGeopotential);
        var expected = new double[9];
        force.AccumulateStatePartialsByCentralDifferences(state, Context, expected, new double[9],
            ForceBase.DefaultPositionRelativeStep, ForceBase.DefaultVelocityRelativeStep);
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        Assert.Equal(expected, dadr);
        Assert.All(dadv, x => Assert.Equal(0.0, x));
    }

    [Fact]
    public void Geopotential_HasNoAnalyticPartialsAndLeavesTheBufferUntouched()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.EarthWithGeopotential);
        var dadr = Enumerable.Repeat(3.0, 9).ToArray();

        // Act
        bool added = _cases.EarthWithGeopotential.TryAccumulateGravitationalPositionPartials(state, dadr);

        // Assert
        Assert.False(added);
        Assert.All(dadr, x => Assert.Equal(3.0, x));
    }

    private void AssertMatchesRidders(ForceBase force, StateVector state, double scale, double[] dadr)
    {
        var reference = RiddersDerivative.StatePartials(force, state, Context, 1e-3 * scale, 1.0);
        // The reference resolves the tolerance by two orders of magnitude.
        Assert.True(reference.DadrError < 1e-8, $"reference error {reference.DadrError:E2}");
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"relative error {error:E2}, reference error estimate {reference.DadrError:E2}");
        Assert.True(error < 1e-6, $"relative error {error:E2}");
    }
}
