// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using IO.Astrodynamics.Tests.SSA;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// An initial covariance made indefinite by rounding (#405): every explicit propagation of the covariance rejects it;
/// <see cref="StateVector.WithRemediatedCovariance"/> remediates it as the conjunction analysis does, against an
/// independent eigenvalue clipping, then every explicit propagation accepts it.
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
    private StateVector Indefinite => WithTrCorrelation(PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth),
        -1.0001);

    private static StateVector WithTrCorrelation(StateVector state, double rho)
    {
        var sigma = new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, rho * sigma[1] * sigma[3]);
        rtn.Set(3, 1, rho * sigma[1] * sigma[3]);
        return WithCovariance(state, state.RotateCovarianceFromRtn(rtn));
    }

    private static StateVector WithCovariance(StateVector state, Matrix covariance) =>
        new(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, covariance);

    // The covariance with the element (i, j) set to value, and (j, i) when symmetric
    private static Matrix With(Matrix covariance, int i, int j, double value, bool symmetric = true)
    {
        var result = new Matrix(6, 6);
        for (int k = 0; k < 36; k++)
        {
            result.Set(k / 6, k % 6, covariance.Get(k / 6, k % 6));
        }

        result.Set(i, j, value);
        if (symmetric)
            result.Set(j, i, value);
        return result;
    }

    // The smallest eigenvalue of the correlation matrix, independently of the library code
    private static double SmallestCorrelationEigenvalue(Matrix p)
    {
        var c = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6,
            (i, j) => 0.5 * (p.Get(i, j) + p.Get(j, i)) / System.Math.Sqrt(p.Get(i, i) * p.Get(j, j)));
        return c.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric).EigenValues.Min(e => e.Real);
    }

    // The relative Frobenius distance of a to b
    private static double Distance(Matrix a, Matrix b)
    {
        double difference = 0.0, norm = 0.0;
        for (int k = 0; k < 36; k++)
        {
            double d = a.Get(k / 6, k % 6) - b.Get(k / 6, k % 6);
            difference += d * d;
            norm += b.Get(k / 6, k % 6) * b.Get(k / 6, k % 6);
        }

        return System.Math.Sqrt(difference / norm);
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

    [Fact]
    public void WithRemediatedCovariance_IndefiniteFromRounding_IsTheIndependentClipping()
    {
        // Arrange
        var state = Indefinite;
        var p0 = state.Covariance.Value;
        var reference = ConjunctionCovarianceEdgeTests.ClipCorrelation(p0);

        // Act
        var remediation = state.WithRemediatedCovariance();

        // Assert: remediated, the clipping of the correlation matrix (Hall et al. 2017) computed independently, to
        // 1e-12 of √(P_ii P_jj); the propagation accepts it; the state is kept, in its frame
        Assert.True(remediation.Remediated);
        var remediated = remediation.State.Covariance.Value;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double scale = System.Math.Sqrt(p0.Get(i, i) * p0.Get(j, j));
                Assert.True(System.Math.Abs(remediated.Get(i, j) - reference.Get(i, j)) < 1e-12 * scale);
                Assert.Equal(remediated.Get(i, j), remediated.Get(j, i));
            }
        }

        Assert.Null(PropagatorBase.InitialCovarianceRejection(remediated, out _));
        Assert.NotSame(state, remediation.State);
        Assert.Equal(state.Position, remediation.State.Position);
        Assert.Equal(state.Velocity, remediation.State.Velocity);
        Assert.Same(state.Observer, remediation.State.Observer);
        Assert.Equal(state.Epoch, remediation.State.Epoch);
        Assert.Equal(state.Frame, remediation.State.Frame);

        // The depth of the defect: the smallest eigenvalue of the correlation matrix given (−1.9e-4)
        double smallest = SmallestCorrelationEigenvalue(p0);
        Assert.True(smallest < -1e-12 * 6.0, $"{smallest:E3}");
        Assert.True(System.Math.Abs(remediation.SmallestCorrelationEigenvalue - smallest) < 1e-14,
            $"{remediation.SmallestCorrelationEigenvalue:E6} {smallest:E6}");
    }

    [Fact]
    public void WithRemediatedCovariance_InAnotherInertialFrame_ClipsInThatFrame()
    {
        // Arrange: the indefinite state in ECLIPJ2000, its covariance rotated by ToFrame
        var state = Indefinite.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector();
        Assert.NotNull(PropagatorBase.InitialCovarianceRejection(state.Covariance.Value, out _));

        // Act
        var remediation = state.WithRemediatedCovariance();

        // Assert: the clipping of the covariance in its frame, the frame kept
        Assert.True(remediation.Remediated);
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, remediation.State.Frame);
        var reference = ConjunctionCovarianceEdgeTests.ClipCorrelation(state.Covariance.Value);
        Assert.True(Distance(remediation.State.Covariance.Value, reference) < 1e-12);
    }

    [Fact]
    public void WithRemediatedCovariance_CovarianceThePropagationAccepts_IsReturnedAsItIs()
    {
        // Arrange: a positive definite covariance; a correlation of −1 − 1e-12, within the tolerance; an asymmetry
        // within the tolerance; a zero variance with zero covariances; a zero covariance
        var leo = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var valid = WithTrCorrelation(leo, -0.5);
        var p = valid.Covariance.Value;
        var states = new[]
        {
            valid,
            WithTrCorrelation(leo, -1.0 - 1e-12),
            WithCovariance(leo, With(p, 0, 1, p.Get(0, 1) + 1e-13 * System.Math.Sqrt(p.Get(0, 0) * p.Get(1, 1)), false)),
            WithCovariance(leo, Enumerable.Range(0, 6).Aggregate(p, (m, j) => With(With(m, 2, j, 0.0), j, 2, 0.0))),
            WithCovariance(leo, new Matrix(6, 6))
        };

        foreach (var state in states)
        {
            // Act
            var remediation = state.WithRemediatedCovariance();

            // Assert: the state itself, its covariance untouched, the smallest eigenvalue within the tolerance
            Assert.False(remediation.Remediated);
            Assert.Same(state, remediation.State);
            Assert.True(remediation.SmallestCorrelationEigenvalue >= -6e-12, $"{remediation.SmallestCorrelationEigenvalue:E3}");
        }

        Assert.True(System.Math.Abs(states[0].WithRemediatedCovariance().SmallestCorrelationEigenvalue -
                                    SmallestCorrelationEigenvalue(p)) < 1e-14);
        Assert.Equal(0.0, states[4].WithRemediatedCovariance().SmallestCorrelationEigenvalue);
    }

    [Fact]
    public void WithRemediatedCovariance_ZeroVarianceWithACovariance_ClearsItsCovariances()
    {
        // Arrange: the radial variance zero (in ICRF, the x variance), its covariances left
        var leo = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var p = WithTrCorrelation(leo, -0.5).Covariance.Value;
        var state = WithCovariance(leo, With(p, 0, 0, 0.0));
        Assert.NotNull(PropagatorBase.InitialCovarianceRejection(state.Covariance.Value, out _));

        // Act
        var remediation = state.WithRemediatedCovariance();

        // Assert: as the analysis, the row and column of the zero variance set to zero; the propagation accepts it
        Assert.True(remediation.Remediated);
        Assert.All(Enumerable.Range(0, 6), j => Assert.Equal(0.0, remediation.State.Covariance.Value.Get(0, j)));
        Assert.All(Enumerable.Range(0, 6), j => Assert.Equal(0.0, remediation.State.Covariance.Value.Get(j, 0)));
        Assert.Null(PropagatorBase.InitialCovarianceRejection(remediation.State.Covariance.Value, out _));
    }

    [Fact]
    public void WithRemediatedCovariance_WhatRoundingDoesNotExplain_IsRefused()
    {
        // Arrange
        var leo = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var p = Indefinite.Covariance.Value;
        double scale = System.Math.Sqrt(p.Get(0, 0) * p.Get(1, 1));
        var cases = new (StateVector State, string Reason)[]
        {
            (leo, "carries no covariance"),
            (WithCovariance(leo, new Matrix(5, 5)), "must be 6×6; it is 5×5"),
            (WithCovariance(leo, With(p, 1, 2, double.NaN)), "must be finite"),
            (WithCovariance(leo, With(p, 3, 3, double.PositiveInfinity)), "must be finite"),
            (WithCovariance(leo, With(p, 4, 4, -p.Get(4, 4))), "its diagonal element (4, 4) is"),
            (WithCovariance(leo, With(p, 0, 1, p.Get(0, 1) + 1e-6 * scale, false)), "must be symmetric")
        };

        foreach (var (state, reason) in cases)
        {
            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => state.WithRemediatedCovariance());

            // Assert: the reason named; the analysis holds such a covariance in RTN
            Assert.Contains(reason, exception.Message);
        }
    }

    [Fact]
    public async Task ExplicitPropagation_StateRemediated_PropagatesTheRemediatedCovariance()
    {
        // Arrange: the remediated state, and the reference: P0 clipped independently
        var remediated = Indefinite.WithRemediatedCovariance().State;
        var reference = WithCovariance(Indefinite, ConjunctionCovarianceEdgeTests.ClipCorrelation(Indefinite.Covariance.Value));
        var end = Window.EndDate;
        var expected = NewSpacecraft(-4060, reference).Propagate(Window, Bodies, new RK78Integrator(), false, false, Step,
            Covariance).GetCovariance(end);
        // An integrator that already has its forces, initialized by its user
        RK78Integrator WithGravity(StateVector start)
        {
            var integrator = new RK78Integrator();
            integrator.AddForce(new GravitationalAcceleration(_cases.Earth));
            integrator.Initialize(start);
            return integrator;
        }

        var expectedWithItsForces = new CentralBodyPropagator(Window, NewSpacecraft(-4061, reference), WithGravity(reference), Step,
            Covariance).Propagate().GetCovariance(end);

        // Act: every explicit propagation of the covariance
        var covariances = new[]
        {
            NewSpacecraft(-4062, remediated).Propagate(Window, Bodies, new RK78Integrator(), false, false, Step, Covariance)
                .GetCovariance(end),
            new CentralBodyPropagator(Window, NewSpacecraft(-4063, remediated), new RK78Integrator(), Bodies, false, false,
                Step, Covariance).Propagate().GetCovariance(end),
            new CentralBodyPropagatorBuilder(Window, NewSpacecraft(-4064, remediated), new RK78Integrator(), Step)
                .WithPerturbingBodies(Bodies).WithCovariancePropagation(Covariance).Build().Propagate().GetCovariance(end),
            BatchPropagator.Propagate(new[]
            {
                new PropagationTask(NewSpacecraft(-4065, remediated), Window, Bodies, false, false, Step,
                    () => new RK78Integrator()) { CovariancePropagation = Covariance }
            }).Results[0].Solution.GetCovariance(end)
        };
        var withItsForces = new CentralBodyPropagator(Window, NewSpacecraft(-4066, remediated), WithGravity(remediated), Step,
            Covariance).Propagate().GetCovariance(end);
        var scenario = new Scenario("remediated", new Astrodynamics.Mission.Mission("remediated"), Window);
        var spacecraft = NewSpacecraft(-4067, remediated);
        scenario.AddSpacecraft(spacecraft);
        await scenario.SimulateAsync(false, false, Step, () => new RK78Integrator(), Covariance);
        var stateTransitionOnly = NewSpacecraft(-4068, remediated).Propagate(Window, Bodies, new RK78Integrator(), false,
            false, Step, new CovariancePropagationOptions());

        // Assert: P at the end of the window from the remediated P0, as from the independent clipping
        foreach (var covariance in covariances)
        {
            Assert.True(Distance(covariance, expected) < 1e-10, $"{Distance(covariance, expected):E2}");
        }

        Assert.True(Distance(withItsForces, expectedWithItsForces) < 1e-10, $"{Distance(withItsForces, expectedWithItsForces):E2}");
        var last = spacecraft.PropagatedStates.Last();
        Assert.Equal(end, last.Epoch);
        Assert.True(Distance(last.Covariance.Value, expected) < 1e-10, $"{Distance(last.Covariance.Value, expected):E2}");
        Assert.Equal(6, stateTransitionOnly.GetStateTransitionMatrix(end).Rows);
    }
}
