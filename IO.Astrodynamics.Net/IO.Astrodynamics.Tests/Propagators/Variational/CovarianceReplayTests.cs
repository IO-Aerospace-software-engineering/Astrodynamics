// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The replay of the variational equations along the steps of a propagation that did not integrate them (step 10, E2):
/// with the step controlled on the state only (A2), it gives the values of a propagation run with the same options from
/// the start, bit for bit.
/// </summary>
public class CovarianceReplayTests : IClassFixture<ReferenceCases>
{
    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public CovarianceReplayTests(ReferenceCases cases, ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _cases = cases;
        _output = output;
    }

    private static readonly CovariancePropagationOptions NoisyOptions = new()
    {
        Mode = CovariancePropagationMode.Covariance,
        ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
        ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1, reflectivityCoefficientSigma: 0.05)
    };

    [Fact]
    public void Replay_OfTheEclipsingCaseWithAManeuverAndItsExecutionError_IsThePropagationWithTheCovariance()
    {
        // Arrange: the same spacecraft twice, its burn with the execution error of Gates, once propagated without the
        // variational equations and once with the covariance, process noise and both consider parameters
        var orbit = CovariancePropagationTests.WithCovariance(_cases.Eclipsing.InitialState);
        var execution = new ManeuverExecutionError
            { ResolutionSigma = 0.05, ShutoffSigma = 0.01, AutopilotSigma = 0.02, PointingSigma = 0.005 };
        var plain = EclipsingSpacecraft(-6101, orbit, execution);
        var propagated = EclipsingSpacecraft(-6102, orbit, execution);
        var withoutCovariance = Propagate(plain, null);
        var withCovariance = Propagate(propagated, NoisyOptions);

        // Act
        var replayed = VariationalReplay.Replay(withoutCovariance, plain, NoisyOptions, _cases.Eclipsing.Window.EndDate);

        // Assert: the same steps, the same entry values at the burn, the same Y and Q at every step end, and the same
        // P, extended covariance, Φ and Ψ inside the steps, bit for bit
        Assert.Equal(2, withCovariance.Segments.Count);
        Assert.NotNull(withCovariance.Segments[1].Variational.EntryManeuver?.ExecutionCovariance);
        AssertSameVariationalData(withCovariance, replayed);
        AssertSameEvaluations(withCovariance, replayed, _cases.Eclipsing.Window, TimeSpan.FromSeconds(997.3));
        Assert.Null(withoutCovariance.CovariancePropagation);
        Assert.Throws<InvalidOperationException>(() => withoutCovariance.GetCovariance(_cases.Eclipsing.Window.EndDate));
    }

    [Fact]
    public void Replay_OfR2WithProcessNoise_IsThePropagationWithTheCovariance()
    {
        // Arrange: R2 (EGM2008 10×10, Sun, Moon, drag), Cd as a consider parameter
        var options = NoisyOptions with { ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1) };
        var r2 = _cases.R2;
        var start = CovariancePropagationTests.WithCovariance(r2.InitialState);
        var plain = ReferenceCase.NewSpacecraft(-6103, start);
        var propagated = ReferenceCase.NewSpacecraft(-6104, start);
        var withoutCovariance = plain.Propagate(r2.Window, r2.Bodies, new RK78Integrator(1e-12, 1e-12), true, false,
            TimeSpan.FromSeconds(3600.0), null);
        var withCovariance = propagated.Propagate(r2.Window, r2.Bodies, new RK78Integrator(1e-12, 1e-12), true, false,
            TimeSpan.FromSeconds(3600.0), options);

        // Act
        var replayed = VariationalReplay.Replay(withoutCovariance, plain, options, r2.Window.EndDate);

        // Assert
        AssertSameVariationalData(withCovariance, replayed);
        AssertSameEvaluations(withCovariance, replayed, r2.Window, TimeSpan.FromSeconds(7777.7));
    }

    [Fact]
    public void Replay_StopsAtTheStepThatContainsTheEndEpoch()
    {
        // Arrange
        var orbit = CovariancePropagationTests.WithCovariance(_cases.Eclipsing.InitialState);
        var plain = EclipsingSpacecraft(-6105, orbit, null);
        var solution = Propagate(plain, null);
        var end = _cases.Eclipsing.InitialState.Epoch.AddSeconds(1234.5);
        var options = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };

        // Act
        var replayed = VariationalReplay.Replay(solution, plain, options, end);

        // Assert: one segment (the burn is at 0.82 h), up to the step that contains the end epoch, not further; 13 stage
        // Jacobians per step, and none on the forces of the solution beyond those of the replay
        var segment = solution.Segments[0];
        int k = segment.FindStepIndex(1234.5);
        Assert.Single(replayed.Segments);
        Assert.Equal(k + 1, replayed.Segments[0].Steps.Count);
        Assert.Equal(13L * (k + 1), replayed.Dynamics.StageJacobianEvaluations);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            replayed.GetCovariance(end.AddSeconds(segment.Steps[k + 1].StepSize + segment.Steps[k].StepSize)));
        var p = replayed.GetCovariance(end);
        Assert.True(p.Get(1, 1) > 0.0);
    }

    [Fact]
    public void Replay_RejectsWhatItCannotReplay()
    {
        // Arrange
        var orbit = CovariancePropagationTests.WithCovariance(_cases.Eclipsing.InitialState);
        var plain = EclipsingSpacecraft(-6106, orbit, null);
        var solution = Propagate(plain, null);
        var end = _cases.Eclipsing.Window.EndDate;
        var covariance = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };
        var withStmSpacecraft = EclipsingSpacecraft(-6107, orbit, null);
        var withStm = Propagate(withStmSpacecraft, covariance);
        var vvSpacecraft = EclipsingSpacecraft(-6108, orbit, null);
        var vv = vvSpacecraft.Propagate(_cases.Eclipsing.Window, _cases.Eclipsing.Bodies, true, true,
            TimeSpan.FromSeconds(600.0));

        // Act, assert: the step control on Φ and Ψ would change the steps; a solution with the equations already, or
        // of another integrator, is not replayed; an end outside the propagation is rejected
        Assert.Throws<ArgumentException>(() => VariationalReplay.Replay(solution, plain,
            covariance with { IncludeInStepControl = true }, end));
        Assert.Throws<InvalidOperationException>(() => VariationalReplay.Replay(withStm, withStmSpacecraft, covariance, end));
        Assert.Throws<InvalidOperationException>(() => VariationalReplay.Replay(vv, vvSpacecraft, covariance, end));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VariationalReplay.Replay(solution, plain, covariance, end.AddSeconds(60.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VariationalReplay.Replay(solution, plain, covariance, _cases.Eclipsing.InitialState.Epoch.AddSeconds(-1.0)));
    }

    private Spacecraft EclipsingSpacecraft(int naifId, StateVector orbit, ManeuverExecutionError execution)
    {
        return _cases.EclipsingSpacecraft(naifId, orbit, engine => new ApogeeHeightManeuver(
            (IO.Astrodynamics.Body.CelestialItem)_cases.Eclipsing.InitialState.Observer, ReferenceCases.Epoch, TimeSpan.Zero,
            ReferenceCases.EclipsingApogeeRadius, engine) { ExecutionError = execution });
    }

    private PropagationSolution Propagate(Spacecraft spacecraft, CovariancePropagationOptions options)
    {
        return spacecraft.Propagate(_cases.Eclipsing.Window, _cases.Eclipsing.Bodies, new RK78Integrator(1e-12, 1e-12),
            true, true, TimeSpan.FromSeconds(600.0), options);
    }

    private static void AssertSameVariationalData(PropagationSolution expected, PropagationSolution actual)
    {
        Assert.Equal(expected.Segments.Count, actual.Segments.Count);
        for (int s = 0; s < expected.Segments.Count; s++)
        {
            var e = expected.Segments[s];
            var a = actual.Segments[s];
            Assert.Equal(e.BaseEpoch, a.BaseEpoch);
            Assert.Equal(e.Steps.Count, a.Steps.Count);
            Assert.Equal(e.Variational.EntryY, a.Variational.EntryY);
            Assert.Equal(e.Variational.EntryQ, a.Variational.EntryQ);
            Assert.Equal(e.Variational.EntryManeuver?.ExecutionCovariance, a.Variational.EntryManeuver?.ExecutionCovariance);
            for (int k = 0; k < e.Steps.Count; k++)
            {
                Assert.Equal(e.Steps[k], a.Steps[k]);
                Assert.True(e.Variational.StepEndY(k).SequenceEqual(a.Variational.StepEndY(k)), $"Y, segment {s}, step {k}");
                Assert.True(e.Variational.StepEndQ(k).SequenceEqual(a.Variational.StepEndQ(k)), $"Q, segment {s}, step {k}");
            }
        }
    }

    private void AssertSameEvaluations(PropagationSolution expected, PropagationSolution actual,
        TimeSystem.Window window, TimeSpan spacing)
    {
        int count = 0;
        for (var epoch = window.StartDate; epoch <= window.EndDate; epoch = epoch.Add(spacing))
        {
            Assert.Equal(expected.GetCovariance(epoch).ToArray(), actual.GetCovariance(epoch).ToArray());
            Assert.Equal(expected.GetExtendedCovariance(epoch).ToArray(), actual.GetExtendedCovariance(epoch).ToArray());
            Assert.Equal(expected.GetStateTransitionMatrix(epoch).ToArray(), actual.GetStateTransitionMatrix(epoch).ToArray());
            Assert.Equal(expected.GetParameterSensitivity(epoch).ToArray(), actual.GetParameterSensitivity(epoch).ToArray());
            var state = actual.GetStateVector(epoch);
            Assert.Equal(expected.GetStateVector(epoch).Position, state.Position);
            Assert.Equal(expected.GetStateVector(epoch).Covariance.Value.ToArray(), state.Covariance.Value.ToArray());
            count++;
        }

        Assert.Equal(expected.GetCovariance(window.EndDate).ToArray(), actual.GetCovariance(window.EndDate).ToArray());
        _output.WriteLine($"{count} epochs compared bit for bit");
    }
}
