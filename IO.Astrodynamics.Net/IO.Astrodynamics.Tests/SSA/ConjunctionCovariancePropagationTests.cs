// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// A two-body conjunction 24 h after the initial states of both participants: a sun-synchronous-like LEO at 700 km and a
/// crossing orbit through a point 200 m away from it at the TCA, both built in closed form (two-body
/// <see cref="OrbitalParameters.OrbitalParameters.AtEpoch"/>), each with a covariance at the start.
/// </summary>
public sealed class ConjunctionCovarianceCase
{
    internal static readonly TimeSystem.Time Start = new(2024, 3, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
    internal static readonly TimeSystem.Time Tca = Start.AddHours(24.0);

    public ConjunctionCovarianceCase()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        Earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var protectedOrbit = new KeplerianElements(7078137.0, 0.001, 98.2 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1,
            0.3, Earth, Start, Frames.Frame.ICRF).ToStateVector();
        var protectedAtTca = protectedOrbit.ToStateVector(Tca);

        // At the TCA: 200 m from the protected object, mostly radial and across, with a velocity turned by 70° about the
        // radial direction
        var radial = protectedAtTca.Position.Normalize();
        var normal = protectedAtTca.Position.Cross(protectedAtTca.Velocity).Normalize();
        var position = protectedAtTca.Position + radial * 120.0 + normal * 160.0;
        double angle = 70.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var v = protectedAtTca.Velocity;
        var velocity = v * System.Math.Cos(angle) + radial.Cross(v) * System.Math.Sin(angle) +
                       radial * (radial * v) * (1.0 - System.Math.Cos(angle));
        var secondaryAtTca = new StateVector(position, velocity, Earth, Tca, Frames.Frame.ICRF);
        var secondaryOrbit = secondaryAtTca.ToStateVector(Start);

        ProtectedStart = WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 });
        SecondaryStart = WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 });
    }

    internal CelestialBody Earth { get; }

    internal StateVector ProtectedStart { get; }

    internal StateVector SecondaryStart { get; }

    /// <summary>The window of the propagations, 26 h from the start.</summary>
    internal static Window PropagationWindow => new(Start, Start.AddHours(26.0));

    /// <summary>The window of the analysis, an hour on each side of the TCA.</summary>
    internal static Window AnalysisWindow => new(Tca.AddHours(-1.0), Tca.AddHours(1.0));

    internal static StateVector WithRtnCovariance(StateVector state, double[] sigma)
    {
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);
        rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);
        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            state.RotateCovarianceFromRtn(rtn));
    }

    internal static Spacecraft NewSpacecraft(int naifId, StateVector start) => ReferenceCase.NewSpacecraft(naifId, start);

    /// <summary>The participant propagated by RK7(8), two-body, with the covariance options given (none when null).</summary>
    internal PropagationSolution Propagate(Spacecraft spacecraft, CovariancePropagationOptions options = null)
    {
        return spacecraft.Propagate(PropagationWindow, new CelestialItem[] { Earth }, new RK78Integrator(1e-12, 1e-12),
            false, false, TimeSpan.FromSeconds(60.0), options);
    }
}

/// <summary>
/// The conjunction analysis with the propagated covariance (step 10, E1 to E3).
/// </summary>
public class ConjunctionCovariancePropagationTests : IClassFixture<ConjunctionCovarianceCase>
{
    private static readonly CovariancePropagationOptions Covariance = new() { Mode = CovariancePropagationMode.Covariance };

    private readonly ConjunctionCovarianceCase _case;
    private readonly ITestOutputHelper _output;

    public ConjunctionCovariancePropagationTests(ConjunctionCovarianceCase @case, ITestOutputHelper output)
    {
        _case = @case;
        _output = output;
    }

    [Fact]
    public void Analyze_ParticipantsPropagatedInRk78_UseTheirPropagatedCovarianceAtTheTca()
    {
        // Arrange: both participants propagated in RK7(8) without the covariance, and the same participants propagated
        // with it, the reference
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6201, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6202, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);
        var protectedReference = _case.Propagate(ConjunctionCovarianceCase.NewSpacecraft(-6203, _case.ProtectedStart), Covariance);
        var secondaryReference = _case.Propagate(ConjunctionCovarianceCase.NewSpacecraft(-6204, _case.SecondaryStart), Covariance);

        // Act: the defaults, then the former behaviour
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            ConjunctionCovarianceCase.AnalysisWindow);
        var held = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            ConjunctionCovarianceCase.AnalysisWindow, new ConjunctionAnalysisOptions { CovariancePropagation = null });

        // Assert: the same TCA and miss distance; the covariance used is that of the propagation with the covariance at
        // the TCA (E1, 1e-12), propagated for both participants, age zero, no process noise, never stale
        var state = encounter.EncounterState;
        Assert.Equal(held.EncounterState.Epoch, state.Epoch);
        Assert.Equal(held.EncounterState.MissDistanceMeters, state.MissDistanceMeters);
        Assert.InRange(state.MissDistanceMeters, 150.0, 250.0);
        Assert.True(System.Math.Abs((state.Epoch - ConjunctionCovarianceCase.Tca).TotalSeconds) < 1.0);
        Assert.Equal(CovarianceSource.Propagated, state.ProtectedCovariance.Source);
        Assert.Equal(CovarianceSource.Propagated, state.SecondaryCovariance.Source);
        Assert.Equal(TimeSpan.Zero, state.ProtectedCovarianceAge);
        Assert.False(state.ProtectedCovariance.ProcessNoiseApplied);
        Assert.False(state.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));

        double protectedError = RelativeError(protectedReference.GetCovariance(state.Epoch), encounter.ProtectedState.Covariance.Value);
        double secondaryError = RelativeError(secondaryReference.GetCovariance(state.Epoch), encounter.SecondaryState.Covariance.Value);
        _output.WriteLine($"covariance used against GetCovariance(tca): protected {protectedError:E2}, secondary {secondaryError:E2}");
        Assert.True(protectedError < 1e-12, $"{protectedError:E2}");
        Assert.True(secondaryError < 1e-12, $"{secondaryError:E2}");

        // The former behaviour: the initial covariance held in RTN, 24 h old, flagged; the probability differs
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, held.EncounterState.ProtectedCovariance.Source);
        Assert.True(held.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        Assert.Null(held.ProtectedState.Covariance);
        double propagatedPc = encounter.CollisionRisk.ProbabilityOfCollision!.Value;
        double heldPc = held.CollisionRisk.ProbabilityOfCollision!.Value;
        _output.WriteLine($"Pc propagated {propagatedPc:E4}, held in RTN {heldPc:E4}; in-track σ {encounter.CollisionRisk.InTrackSigmaMeters:F1} m against {held.CollisionRisk.InTrackSigmaMeters:F1} m");
        Assert.NotEqual(heldPc, propagatedPc);
        Assert.True(encounter.CollisionRisk.InTrackSigmaMeters > 2.0 * held.CollisionRisk.InTrackSigmaMeters);
    }

    [Fact]
    public void Analyze_TwoBodyPropagatedCovariance_IsPhiP0PhiTOfTheKeplerianStm()
    {
        // Arrange
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6211, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6212, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            ConjunctionCovarianceCase.AnalysisWindow);

        // Assert: P = Φ P0 Φᵀ with Φ the Keplerian STM of Lagrange's f and g (F2), at the C2 threshold
        foreach (var (start, state) in new[]
                 {
                     (_case.ProtectedStart, encounter.ProtectedState), (_case.SecondaryStart, encounter.SecondaryState)
                 })
        {
            double dt = (state.Epoch - start.Epoch).TotalSeconds;
            var (_, phi) = KeplerianStm.Propagate(_case.Earth.GM, start.Position, start.Velocity, dt);
            var phiMatrix = new Matrix(6, 6, phi);
            var expected = phiMatrix * start.Covariance.Value * phiMatrix.Transpose();
            double error = RelativeError(expected, state.Covariance.Value);
            _output.WriteLine($"P against the Keplerian Φ P0 Φᵀ: {error:E2}");
            Assert.True(error < 1e-8, $"{error:E2}");
        }
    }

    [Fact]
    public void Analyze_WithPropagationSolutions_UsesTheirPropagatedCovariance()
    {
        // Arrange: solutions passed to the analysis; the protected one already propagated the covariance, with process
        // noise, and is read as it is; the secondary one is replayed
        var noisy = Covariance with { ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12) };
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6221, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6222, _case.SecondaryStart);
        var protectedSolution = _case.Propagate(protectedCraft, noisy);
        var secondarySolution = _case.Propagate(secondaryCraft);
        var secondaryReference = _case.Propagate(ConjunctionCovarianceCase.NewSpacecraft(-6223, _case.SecondaryStart), Covariance);

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), protectedSolution,
            secondaryCraft, secondarySolution, ConjunctionCovarianceCase.AnalysisWindow);

        // Assert
        var state = encounter.EncounterState;
        Assert.Equal(CovarianceSource.Propagated, state.ProtectedCovariance.Source);
        Assert.True(state.ProtectedCovariance.ProcessNoiseApplied);
        Assert.Equal(CovarianceSource.Propagated, state.SecondaryCovariance.Source);
        Assert.False(state.SecondaryCovariance.ProcessNoiseApplied);
        Assert.True(RelativeError(protectedSolution.GetCovariance(state.Epoch), encounter.ProtectedState.Covariance.Value) < 1e-12);
        Assert.True(RelativeError(secondaryReference.GetCovariance(state.Epoch), encounter.SecondaryState.Covariance.Value) < 1e-12);
        Assert.Same(protectedSolution, encounter.ProtectedCovariance.Solution);
        Assert.NotSame(secondarySolution, encounter.SecondaryCovariance.Solution);
        Assert.Null(secondarySolution.CovariancePropagation);
    }

    [Fact]
    public void Screen_NeverEvaluatesAPartialDerivative_AndAnalyzeReplaysUpToTheLastTcaOnly()
    {
        // Arrange
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6231, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6232, _case.SecondaryStart);
        var protectedSolution = _case.Propagate(protectedCraft);
        var secondarySolution = _case.Propagate(secondaryCraft);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act: a screening, which ranks by probability
        var ranked = ConjunctionAssessment.Screen(profile, new ILocalizable[] { secondaryCraft },
            ConjunctionCovarianceCase.AnalysisWindow);

        // Assert: no partial evaluated on the forces of either propagation; the covariance held in RTN
        Assert.Single(ranked);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, ranked[0].EncounterState.ProtectedCovariance.Source);
        Assert.All(protectedSolution.Dynamics.Forces.Concat(secondarySolution.Dynamics.Forces),
            force => Assert.Equal(0, force.PartialsEvaluations));

        // Act: the resolver of an analysis whose last TCA is the TCA found
        var tca = ranked[0].EncounterState.Epoch;
        var resolver = new EncounterCovarianceResolver(protectedCraft, protectedSolution, Covariance, tca);
        var covariance = resolver.Resolve(ranked[0].ProtectedState);

        // Assert: the replay holds the steps up to the one that contains the TCA, not further
        Assert.Equal(CovarianceSource.Propagated, covariance.Source);
        var segment = protectedSolution.Segments.Single();
        int k = segment.FindStepIndex((tca - segment.BaseEpoch).TotalSeconds);
        Assert.Equal(k + 1, resolver.Replayed.Segments.Single().Steps.Count);
        Assert.True(k + 1 < segment.Steps.Count);
        Assert.Contains(protectedSolution.Dynamics.Forces, force => force.PartialsEvaluations > 0);
    }

    [Fact]
    public void AnalyzeAll_ReplaysOncePerParticipant_UpToTheLastTca()
    {
        // Arrange: a window of three revolutions, so that several minima are found
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6241, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6242, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);
        var window = new Window(ConjunctionCovarianceCase.Tca.AddHours(-3.0), ConjunctionCovarianceCase.Tca.AddHours(1.0));

        // Act
        var encounters = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            window);

        // Assert: every encounter with its covariance propagated, the same replay for all of them
        Assert.True(encounters.Count > 1, $"{encounters.Count}");
        Assert.All(encounters, e => Assert.Equal(CovarianceSource.Propagated, e.EncounterState.ProtectedCovariance.Source));
        Assert.Single(encounters.Select(e => e.ProtectedCovariance.Solution).Distinct());
        var replayed = encounters[0].ProtectedCovariance.Solution;
        var last = replayed.Segments[^1];
        var lastTca = encounters.Max(e => e.EncounterState.Epoch);
        Assert.True(last.BaseEpoch.AddSeconds(last.Duration) >= lastTca);
        Assert.True(last.BaseEpoch.AddSeconds(last.Steps[^1].CumulativeTime) <= lastTca);
    }

    [Fact]
    public void Analyze_ParticipantsNotPropagatedInRk78_KeepTheCovarianceHeldInRtn()
    {
        // Arrange: Velocity-Verlet, not propagated (two-body ephemeris), and an RK7(8) propagation whose window starts
        // after the epoch of the initial covariance
        var vv = ConjunctionCovarianceCase.NewSpacecraft(-6251, _case.ProtectedStart);
        vv.Propagate(ConjunctionCovarianceCase.PropagationWindow, new CelestialItem[] { _case.Earth }, false, false,
            TimeSpan.FromSeconds(60.0));
        var notPropagated = ConjunctionCovarianceCase.NewSpacecraft(-6252, _case.SecondaryStart);
        var later = ConjunctionCovarianceCase.NewSpacecraft(-6253, _case.SecondaryStart);
        later.Propagate(new Window(ConjunctionCovarianceCase.Start.AddHours(1.0), ConjunctionCovarianceCase.Start.AddHours(26.0)),
            new CelestialItem[] { _case.Earth }, new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(60.0), null);

        foreach (var secondary in new[] { notPropagated, later })
        {
            // Act
            var withDefaults = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(vv), secondary,
                ConjunctionCovarianceCase.AnalysisWindow);
            var withoutPropagation = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(vv), secondary,
                ConjunctionCovarianceCase.AnalysisWindow, new ConjunctionAnalysisOptions { CovariancePropagation = null });

            // Assert: the fallback, the same results bit for bit as without the propagation of the covariance
            Assert.Equal(CovarianceSource.InitialStateHeldInRtn, withDefaults.EncounterState.ProtectedCovariance.Source);
            Assert.Equal(CovarianceSource.InitialStateHeldInRtn, withDefaults.EncounterState.SecondaryCovariance.Source);
            Assert.Equal(withoutPropagation.EncounterState.Epoch, withDefaults.EncounterState.Epoch);
            Assert.Equal(withoutPropagation.CollisionRisk.ProbabilityOfCollision, withDefaults.CollisionRisk.ProbabilityOfCollision);
            Assert.Equal(withoutPropagation.EncounterState.CombinedCovarianceRtn.ToArray(),
                withDefaults.EncounterState.CombinedCovarianceRtn.ToArray());
            Assert.Equal(withoutPropagation.EncounterState.QualityFlags, withDefaults.EncounterState.QualityFlags);
        }
    }

    [Fact]
    public void Analyze_RejectsCovarianceOptionsItCannotApply()
    {
        // Arrange
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6261, _case.ProtectedStart);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6262, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = ConjunctionCovarianceCase.AnalysisWindow;

        // Act, assert: the state transition matrix alone, the step control on Φ and Ψ, a consider parameter whose force
        // the propagation lacks (it names the participant), a threshold that is not positive
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { CovariancePropagation = new CovariancePropagationOptions() }));
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { CovariancePropagation = Covariance with { IncludeInStepControl = true } }));
        var error = Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions
            {
                CovariancePropagation = Covariance with { ConsiderParameters = new ConsiderParameters(0.1) }
            }));
        Assert.Contains(protectedCraft.Name, error.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { NonlinearityIndexThreshold = 0.0 }));
    }

    [Fact]
    public void EncounterState_ConstructorsWithTheAges_InferTheSource()
    {
        // Arrange
        var relative = new RelativeState(ConjunctionCovarianceCase.Tca, Vector3.VectorX, Vector3.VectorY, Vector3.VectorX,
            Vector3.VectorY);

        // Act
        var none = new EncounterState(ConjunctionCovarianceCase.Tca, relative, 1.0, new Matrix(6, 6),
            EncounterQualityFlags.None);
        var aged = new EncounterState(ConjunctionCovarianceCase.Tca, relative, 1.0, new Matrix(6, 6),
            EncounterQualityFlags.None, TimeSpan.Zero, TimeSpan.FromHours(3.0));
        var full = new EncounterState(ConjunctionCovarianceCase.Tca, relative, 1.0, new Matrix(6, 6),
            new ParticipantCovariance(CovarianceSource.Propagated, TimeSpan.Zero, true, 0.02), ParticipantCovariance.None,
            EncounterQualityFlags.None);

        // Assert
        Assert.Same(ParticipantCovariance.None, none.ProtectedCovariance);
        Assert.Null(none.SecondaryCovarianceAge);
        Assert.Equal(CovarianceSource.ProvidedAtEncounter, aged.ProtectedCovariance.Source);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, aged.SecondaryCovariance.Source);
        Assert.Equal(TimeSpan.FromHours(3.0), aged.SecondaryCovarianceAge);
        Assert.True(full.ProtectedCovariance.ProcessNoiseApplied);
        Assert.Equal(0.02, full.ProtectedCovariance.NonlinearityIndex);
        Assert.Equal(TimeSpan.Zero, full.ProtectedCovarianceAge);
        Assert.Throws<ArgumentNullException>(() => new EncounterState(ConjunctionCovarianceCase.Tca, relative, 1.0,
            new Matrix(6, 6), null, ParticipantCovariance.None, EncounterQualityFlags.None));
    }

    private static double RelativeError(Matrix expected, Matrix actual)
    {
        double difference = 0.0;
        double norm = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double d = actual.Get(i, j) - expected.Get(i, j);
                difference += d * d;
                norm += expected.Get(i, j) * expected.Get(i, j);
            }
        }

        return System.Math.Sqrt(difference / norm);
    }
}
