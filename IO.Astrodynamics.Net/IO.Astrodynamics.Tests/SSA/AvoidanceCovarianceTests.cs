// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.CCSDS.CDM;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The avoidance study with the propagated covariance (step 10, E4): each option propagated numerically from the state
/// and the covariance at its burn, with the execution error of Gates.
/// </summary>
public class AvoidanceCovarianceTests : IClassFixture<ConjunctionCovarianceCase>, IClassFixture<CdmPropagatedCovarianceCase>
{
    private readonly ConjunctionCovarianceCase _twoBody;
    private readonly CdmPropagatedCovarianceCase _full;
    private readonly ITestOutputHelper _output;

    public AvoidanceCovarianceTests(ConjunctionCovarianceCase twoBody, CdmPropagatedCovarianceCase full,
        ITestOutputHelper output)
    {
        _twoBody = twoBody;
        _full = full;
        _output = output;
    }

    private static readonly ManeuverExecutionError Gates = new()
        { ResolutionSigma = 0.05, ShutoffSigma = 0.01, AutopilotSigma = 0.02, PointingSigma = 0.005 };

    [Fact]
    public void EvaluateAvoidance_TheExecutionErrorAddsPhiBLeBtPhiTAtTheTcaOfEachOption()
    {
        // Arrange: the two-body conjunction, both participants propagated in RK7(8); one lead time, one magnitude
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6401, _twoBody.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6402, _twoBody.SecondaryStart);
        _twoBody.Propagate(protectedCraft);
        _twoBody.Propagate(secondaryCraft);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            AvoidanceWindow);
        var options = new AvoidanceSearchOptions
        {
            LeadTimes = new[] { TimeSpan.FromHours(3.0) },
            DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
            MaxReturnedOptions = 6
        };

        // Act
        var without = ConjunctionAssessment.EvaluateAvoidance(encounter, options);
        var with = ConjunctionAssessment.EvaluateAvoidance(encounter, options with { ExecutionError = Gates });

        // Assert: six options each; for each, the same trajectory and TCA, and P(with) − P(without) = Φ B L_e Bᵀ Φᵀ
        // (C5 propagated through Φ(tca, t_b) of the option, the A5 threshold); the probability changes
        Assert.Equal(6, without.Count);
        foreach (var option in with)
        {
            var reference = without.Single(o => o.DeltaVInertial == option.DeltaVInertial);
            var encounterWith = option.PostManeuverEncounter;
            var encounterWithout = reference.PostManeuverEncounter;
            Assert.Equal(encounterWithout.EncounterState.Epoch, encounterWith.EncounterState.Epoch);
            Assert.Equal(encounterWithout.ProtectedState.Position, encounterWith.ProtectedState.Position);
            Assert.Equal(CovarianceSource.Propagated, encounterWith.EncounterState.ProtectedCovariance.Source);
            Assert.Equal(CovarianceSource.Propagated, encounterWith.EncounterState.SecondaryCovariance.Source);

            var tca = encounterWith.EncounterState.Epoch;
            var phi = encounterWith.ProtectedCovariance.Solution.GetStateTransitionMatrix(tca);
            var le = new double[9];
            Gates.Covariance(option.DeltaVInertial, le);
            var jump = new Matrix(6, 6);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    jump.Set(i + 3, j + 3, le[3 * i + j]);
                }
            }

            var expected = phi * jump * phi.Transpose();
            var actual = Difference(encounterWith.ProtectedState.Covariance.Value, encounterWithout.ProtectedState.Covariance.Value);
            double error = RelativeError(expected, actual);
            _output.WriteLine($"option {option.DeltaVInertial}: P(Gates) − P against Φ B L_e Bᵀ Φᵀ {error:E2}; Pc " +
                              $"{reference.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision:E3} → " +
                              $"{encounterWith.CollisionRisk.ProbabilityOfCollision:E3}");
            Assert.True(error < 1e-10, $"{error:E2}");
            Assert.NotEqual(encounterWithout.CollisionRisk.ProbabilityOfCollision,
                encounterWith.CollisionRisk.ProbabilityOfCollision);
        }
    }

    [Fact]
    public void EvaluateAvoidance_AnOption_IsThePropagationFromTheStartWithAFixedBurn()
    {
        // Arrange: the LEO with drag and SRP (point-mass Earth, Moon, Sun), Cd and Cr as consider parameters and process noise; the protected spacecraft
        // with an engine, so that its burn spends fuel
        var protectedCraft = WithEngine(-6411, _full.PointMassProtectedStart, out _);
        var secondaryCraft = ReferenceCase.NewSpacecraft(-6412, _full.PointMassSecondaryStart);
        _full.Propagate(protectedCraft, pointMass: true);
        _full.Propagate(secondaryCraft, pointMass: true);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            new Window(CdmPropagatedCovarianceCase.Start.AddMinutes(30.0), CdmPropagatedCovarianceCase.Start.AddHours(2.5)),
            new ConjunctionAnalysisOptions { CovariancePropagation = CdmPropagatedCovarianceCase.Options });
        var lead = TimeSpan.FromHours(1.0);
        var options = new AvoidanceSearchOptions
        {
            LeadTimes = new[] { lead },
            DeltaVMagnitudesMetersPerSecond = new[] { 0.25 },
            MaxReturnedOptions = 6,
            ExecutionError = Gates
        };


        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, options);

        // Assert: each option against the same spacecraft propagated from the start with a burn fixed in epoch and ΔV and
        // its execution error, the open-loop model (FixedImpulse): the state and the covariance at the TCA of the option,
        // the correlation of the state and the consider parameters at the burn carried over
        Assert.Equal(6, avoidance.Count);
        double worstState = 0.0;
        double worstCovariance = 0.0;
        foreach (var option in avoidance.Take(3))
        {
            var reference = WithEngine(-6413, _full.PointMassProtectedStart, out var engine);
            reference.SetStandbyManeuver(new FixedImpulse((CelestialItem)_full.PointMassProtectedStart.Observer,
                CdmPropagatedCovarianceCase.Start, option.BurnEpoch, option.DeltaVInertial, engine) { ExecutionError = Gates });
            var solution = _full.Propagate(reference, CdmPropagatedCovarianceCase.Options, pointMass: true);

            var post = option.PostManeuverEncounter;
            var tca = post.EncounterState.Epoch;
            var expected = solution.GetStateVector(tca);
            double stateError = (post.ProtectedState.Position - expected.Position).Magnitude();
            double covarianceError = RelativeError(expected.Covariance.Value, post.ProtectedState.Covariance.Value);
            worstState = System.Math.Max(worstState, stateError);
            worstCovariance = System.Math.Max(worstCovariance, covarianceError);
            _output.WriteLine($"option {option.DeltaVInertial}: state {stateError:E2} m, P {covarianceError:E2}");
        }

        Assert.True(worstState < 1e-3, $"{worstState:E2} m");
        Assert.True(worstCovariance < 1e-8, $"{worstCovariance:E2}");
    }

    [Fact]
    public void EvaluateAvoidance_ProtectedCovarianceNotPropagated_KeepsTheTwoBodyTrial()
    {
        // Arrange: the covariance held in RTN
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6421, _twoBody.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6422, _twoBody.SecondaryStart);
        _twoBody.Propagate(protectedCraft);
        _twoBody.Propagate(secondaryCraft);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            AvoidanceWindow, new ConjunctionAnalysisOptions { CovariancePropagation = null });
        var options = new AvoidanceSearchOptions
            { LeadTimes = new[] { TimeSpan.FromHours(1.0) }, DeltaVMagnitudesMetersPerSecond = new[] { 0.1 } };

        // Act
        var without = ConjunctionAssessment.EvaluateAvoidance(encounter, options);
        var with = ConjunctionAssessment.EvaluateAvoidance(encounter, options with { ExecutionError = Gates });

        // Assert: the execution error is not used; the trial is not propagated, its covariance not either
        Assert.Equal(without.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision),
            with.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision));
        Assert.All(with, o => Assert.NotEqual(CovarianceSource.Propagated,
            o.PostManeuverEncounter.EncounterState.ProtectedCovariance.Source));
    }

    // The screening window of the two-body encounter, which must hold the burns: four hours before the TCA, one after
    private static Window AvoidanceWindow =>
        new(ConjunctionCovarianceCase.Tca.AddHours(-4.0), ConjunctionCovarianceCase.Tca.AddHours(1.0));

    private static Spacecraft WithEngine(int naifId, StateVector start, out Engine engine)
    {
        var spacecraft = ReferenceCase.NewSpacecraft(naifId, start);
        var tank = new FuelTank("tank" + -naifId, "model", "sn1", 100.0, 100.0);
        engine = new Engine("engine" + -naifId, "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    private static Matrix Difference(Matrix a, Matrix b)
    {
        var d = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                d.Set(i, j, a.Get(i, j) - b.Get(i, j));
            }
        }

        return d;
    }

    private static double RelativeError(Matrix expected, Matrix actual)
    {
        double difference = 0.0;
        double norm = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double e = actual.Get(i, j) - expected.Get(i, j);
                difference += e * e;
                norm += expected.Get(i, j) * expected.Get(i, j);
            }
        }

        return System.Math.Sqrt(difference / norm);
    }
}
