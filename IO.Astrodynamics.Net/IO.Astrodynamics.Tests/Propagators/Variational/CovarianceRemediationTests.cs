// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// An initial covariance made indefinite by rounding (#405): every explicit propagation of the covariance rejects it.
/// </summary>
public class CovarianceRemediationTests : IClassFixture<PartialsTestCases>
{
    private static readonly CovariancePropagationOptions Covariance = new() { Mode = CovariancePropagationMode.Covariance };

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(300.0);

    private readonly PartialsTestCases _cases;

    public CovarianceRemediationTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    private static TimeSystem.Window Window => new(PartialsTestCases.Epoch, PartialsTestCases.Epoch.AddHours(1.0));

    private CelestialItem[] Bodies => new CelestialItem[] { _cases.Earth };

    // The LEO state with an RTN covariance (σ 50, 200, 30 m and 0.2, 0.05, 0.03 m/s) and a T–Ṙ correlation of −1.0001,
    // as from rounded values: symmetric, finite, non-negative variances, not positive semi-definite
    private StateVector Indefinite
    {
        get
        {
            var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
            var sigma = new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
            var rtn = new Matrix(6, 6);
            for (int i = 0; i < 6; i++)
            {
                rtn.Set(i, i, sigma[i] * sigma[i]);
            }

            rtn.Set(1, 3, -1.0001 * sigma[1] * sigma[3]);
            rtn.Set(3, 1, -1.0001 * sigma[1] * sigma[3]);
            return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
                state.RotateCovarianceFromRtn(rtn));
        }
    }

    private Spacecraft NewSpacecraft(int naifId, StateVector state) =>
        new(naifId, $"REMEDIATION{-naifId}", 100.0, 1000.0, new Clock("remediation", 65536), state, 10.0, 2.2, null, 1.5);

    [Fact]
    public async Task ExplicitPropagation_InitialCovarianceIndefiniteFromRounding_IsRejected()
    {
        // Arrange
        var state = Indefinite;
        const string reason = "positive semi-definite; the smallest eigenvalue of its correlation matrix";

        // Act, assert: Spacecraft.Propagate, both CentralBodyPropagator constructors, the builder, a batch task and
        // Scenario.SimulateAsync reject it; the propagator does not remediate it (#405)
        var exception = Assert.Throws<InvalidOperationException>(() => NewSpacecraft(-4051, state).Propagate(Window, Bodies,
            new RK78Integrator(), false, false, Step, Covariance));
        Assert.Contains(reason, exception.Message);
        exception = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(Window,
            NewSpacecraft(-4052, state), new RK78Integrator(), Bodies, false, false, Step, Covariance));
        Assert.Contains(reason, exception.Message);
        exception = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(Window,
            NewSpacecraft(-4053, state), new RK78Integrator(), Step, Covariance));
        Assert.Contains(reason, exception.Message);
        exception = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagatorBuilder(Window,
            NewSpacecraft(-4054, state), new RK78Integrator(), Step).WithCovariancePropagation(Covariance).Build());
        Assert.Contains(reason, exception.Message);
        var batch = BatchPropagator.Propagate(new[]
        {
            new PropagationTask(NewSpacecraft(-4055, state), Window, Bodies, false, false, Step, () => new RK78Integrator())
                { CovariancePropagation = Covariance }
        });
        Assert.IsType<InvalidOperationException>(batch.Results[0].Error);
        Assert.Contains(reason, batch.Results[0].Error.Message);
        var scenario = new Scenario("remediation", new Astrodynamics.Mission.Mission("remediation"), Window);
        scenario.AddSpacecraft(NewSpacecraft(-4056, state));
        exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scenario.SimulateAsync(false, false, Step, () => new RK78Integrator(), Covariance));
        Assert.Contains(reason, exception.Message);
    }
}
