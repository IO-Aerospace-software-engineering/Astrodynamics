// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Analytic partials of the third-body perturbation (B4), against Ridders' reference, with and without the ephemeris
/// cache.
/// </summary>
public class ThirdBodyPerturbationPartialsTests : IClassFixture<PartialsTestCases>
{
    private static readonly ForceEvaluationContext Context = new(100.0, 2.2, 1.5);

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public ThirdBodyPerturbationPartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string, string, bool> Cases()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var state in PartialsTestCases.StateNames)
        {
            foreach (var body in new[] { "Moon", "Sun" })
            {
                data.Add(state, body, false);
                data.Add(state, body, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnalyticPartials_MatchRiddersWithin1e6(string stateName, string bodyName, bool useCache)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        CelestialItem body = bodyName == "Moon" ? _cases.Moon : _cases.Sun;
        var force = new ThirdBodyPerturbation(body, _cases.Earth);
        if (useCache)
        {
            force.EphemerisCache = PartialsTestCases.Cache(_cases.Earth, body);
        }

        var dadr = new double[9];
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert
        var reference = RiddersDerivative.StatePartials(force, state, Context, 1e-3 * state.Position.Magnitude(), 1.0);
        // The reference resolves the tolerance by two orders of magnitude.
        Assert.True(reference.DadrError < 1e-8, $"reference error {reference.DadrError:E2}");
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"relative error {error:E2}, reference error estimate {reference.DadrError:E2}");
        Assert.True(error < 1e-6, $"relative error {error:E2}");
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    [Fact]
    public void AnalyticPartials_WithTheEphemerisCache_AllocateNothing()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var force = new ThirdBodyPerturbation(_cases.Moon, _cases.Earth)
        {
            EphemerisCache = PartialsTestCases.Cache(_cases.Earth, _cases.Moon)
        };
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
}
