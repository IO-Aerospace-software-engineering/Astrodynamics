// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The results of the conjunction analysis read at a glance (R17 of #668, lot L7): the four shortcuts of
/// <see cref="EncounterCase"/>, the <c>ToString</c> of the results, the options the analysis used, the trajectory of each
/// participant (<see cref="TrajectorySource"/>); the search made visible (R18): <see cref="EncounterCase.Search"/> and
/// <see cref="ConjunctionAnalysisOptions.ReadRangeRateOverWindow"/> refused where the sampled search runs.
/// </summary>
public partial class ConjunctionAssessmentTests
{
    [Fact]
    public void EncounterCase_Shortcuts_AreTheNestedValues_AndToStringReadsThem()
    {
        // Arrange: the pair two-body, both with a covariance
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);

        // Assert: each shortcut is the value it stands for, the relative speed the norm of the relative velocity, as the CDM
        // writes it
        Assert.Equal(encounter.EncounterState.Epoch, encounter.Tca);
        Assert.Equal(encounter.EncounterState.MissDistanceMeters, encounter.MissDistanceMeters);
        Assert.Equal(encounter.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude(),
            encounter.RelativeSpeedMetersPerSecond);
        Assert.NotNull(encounter.ProbabilityOfCollision);
        Assert.Equal(encounter.CollisionRisk.ProbabilityOfCollision, encounter.ProbabilityOfCollision);

        // The summaries name the epoch, the miss, the probability, the radius and the flags
        var text = encounter.ToString();
        Assert.StartsWith($"TCA {encounter.Tca}, miss ", text);
        Assert.Contains($"Pc {encounter.ProbabilityOfCollision!.Value.ToString("G4", System.Globalization.CultureInfo.InvariantCulture)}", text);
        Assert.Contains($"HBR {ProtectedHardBodyRadius + 3.0} m", text);
        Assert.EndsWith(encounter.EncounterState.QualityFlags.ToString(), text);
        Assert.StartsWith($"{encounter.Tca}, miss ", encounter.EncounterState.ToString());
        Assert.StartsWith("Pc ", encounter.CollisionRisk.ToString());
    }

    [Fact]
    public void EncounterCase_WithoutProbability_PrintsPcNotAvailable()
    {
        // Arrange: neither participant has a covariance, so the analysis computes no probability
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) =
            ConjunctionCovarianceCase.Pair(earth, InputRuleStart, InputRuleStart.AddMinutes(30.0));
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);

        // Assert
        Assert.Null(encounter.ProbabilityOfCollision);
        Assert.Contains("Pc n/a", encounter.ToString());
        Assert.StartsWith("Pc n/a", encounter.CollisionRisk.ToString());
    }

    [Fact]
    public void EncounterCase_AnalysisOptions_AreTheResolvedOptions()
    {
        // Arrange
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var given = ConjunctionAnalysisOptions.Default with { StaleCovarianceThreshold = TimeSpan.FromDays(1.0) };

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow, given);

        // Assert: the options given, the radius of the secondary resolved; reusable as the avoidance's
        Assert.Equal(given with { SecondaryHardBodyRadiusMeters = 3.0 }, encounter.AnalysisOptions);
        var study = ConjunctionAssessment.EvaluateAvoidance(encounter,
            InputRuleAvoidance with { Analysis = encounter.AnalysisOptions with { SampleStep = TimeSpan.FromSeconds(30.0) } });
        Assert.Equal(6, study.Options.Count);
    }

    [Fact]
    public void Search_ReportsTheSearchThatRan_AndTheTrajectoryOfEachParticipant()
    {
        // Arrange: the pair two-body, then propagated in RK7(8)
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);

        // Act: two-body (no stored state)
        var twoBody = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);

        // Assert
        Assert.Equal(CloseApproachSearch.Sampled, twoBody.Search);
        Assert.Equal(TrajectorySource.TwoBody, twoBody.EncounterState.ProtectedTrajectory);
        Assert.Equal(TrajectorySource.TwoBody, twoBody.EncounterState.SecondaryTrajectory);

        // Act: propagated, through the stored states and through the solutions
        var protectedTrajectory = PropagateRk78(protectedCraft, earth);
        var secondaryTrajectory = PropagateRk78(secondaryCraft, earth);
        var stored = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);
        var dense = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondaryCraft, secondaryTrajectory,
            InputRuleWindow);
        var denseAll = ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondaryCraft, secondaryTrajectory,
            InputRuleWindow);

        // Assert
        Assert.Equal(CloseApproachSearch.Sampled, stored.Search);
        Assert.Equal(TrajectorySource.Propagated, stored.EncounterState.ProtectedTrajectory);
        Assert.Equal(TrajectorySource.Propagated, stored.EncounterState.SecondaryTrajectory);
        Assert.Equal(CloseApproachSearch.Polynomial, dense.Search);
        Assert.Equal(TrajectorySource.Propagated, dense.EncounterState.ProtectedTrajectory);
        Assert.Equal(TrajectorySource.Propagated, dense.EncounterState.SecondaryTrajectory);
        Assert.Equal(CloseApproachSearch.Polynomial, Assert.Single(denseAll).Search);
    }

    [Fact]
    public void TrajectorySource_OfAnSgp4Participant_AndOfABodyReadFromTheKernels()
    {
        // Arrange: a TLE spacecraft that no numerical propagation stored states for, against the Moon
        var tle = InputRuleTle;
        var protectedCraft = new Spacecraft(NextInputRuleId(), "SGP4", 120.0, tle, 150.0,
            new Clock("sgp4" + -_inputRuleIds, 256), hardBodyRadius: ProtectedHardBodyRadius);
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var window = new Window(tle.Epoch, tle.Epoch.AddMinutes(10.0));

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, moon, window);

        // Assert
        Assert.Equal(TrajectorySource.Sgp4, encounter.EncounterState.ProtectedTrajectory);
        Assert.Equal(TrajectorySource.Kernel, encounter.EncounterState.SecondaryTrajectory);
    }

    [Fact]
    public void ReadRangeRateOverWindow_WhereTheSampledSearchRuns_Throws()
    {
        // Arrange
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var reads = ConjunctionAnalysisOptions.Default with { ReadRangeRateOverWindow = true };

        // Act and assert: the ILocalizable overloads and the screening, before any search
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow, reads)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.AnalyzeAll(protectedCraft, secondaryCraft, InputRuleWindow, reads)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.Screen(protectedCraft, new ILocalizable[] { secondaryCraft }, InputRuleWindow,
                new ScreeningOptions { Analysis = reads })).ParamName);

        // The avoidance given the options, whose re-analysis is sampled; inherited from the encounter, it is cleared
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.EvaluateAvoidance(encounter, InputRuleAvoidance with { Analysis = reads })).ParamName);

        // Two trajectories in different frames, which the polynomial search cannot take
        var eclipticCraft = InputRuleSpacecraft(NextInputRuleId(),
            secondaryStart.ToFrame(Frames.Frame.ECLIPJ2000).ToStateVector(), 3.0);
        var protectedTrajectory = PropagateRk78(protectedCraft, earth);
        var eclipticTrajectory = PropagateRk78(eclipticCraft, earth);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, eclipticCraft, eclipticTrajectory,
                InputRuleWindow, reads)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, eclipticCraft, eclipticTrajectory,
                InputRuleWindow, reads)).ParamName);
        var sampled = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, eclipticCraft, eclipticTrajectory,
            InputRuleWindow);
        Assert.Equal(CloseApproachSearch.Sampled, sampled.Search);

        // Accepted where the polynomial search runs
        var secondaryTrajectory = PropagateRk78(secondaryCraft, earth);
        var dense = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondaryCraft, secondaryTrajectory,
            InputRuleWindow, reads);
        Assert.Equal(CloseApproachSearch.Polynomial, dense.Search);
        Assert.True(dense.AnalysisOptions.ReadRangeRateOverWindow);
    }

    /// <summary>
    /// The results are the library's (R17 of #668, choice 4, lot L7): no public constructor on the five result types, and
    /// the trajectory of an avoidance option set by the library only.
    /// </summary>
    [Theory]
    [InlineData(typeof(EncounterCase))]
    [InlineData(typeof(EncounterState))]
    [InlineData(typeof(CollisionRisk))]
    [InlineData(typeof(AvoidanceOption))]
    [InlineData(typeof(RelativeState))]
    public void ResultTypes_HaveNoPublicConstructor(Type type)
    {
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, p.Name));
    }
}
