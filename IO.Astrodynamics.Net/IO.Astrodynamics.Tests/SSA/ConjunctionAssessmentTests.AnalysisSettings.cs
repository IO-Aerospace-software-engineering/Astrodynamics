// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Ssa;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The settings of the conjunction analysis in one record (structuring choice 2 of #668, R17, lot L7):
/// <see cref="ConjunctionAnalysisOptions"/> contained in <see cref="ScreeningOptions.Analysis"/> and
/// <see cref="AvoidanceSearchOptions.Analysis"/>, the mirrored subsets removed, a <c>Default</c> on the three records and
/// the checks of a single value at init (D1).
/// </summary>
public partial class ConjunctionAssessmentTests
{
    [Fact]
    public void SettingsRecords_Default_EqualsNew()
    {
        Assert.Equal(new ConjunctionAnalysisOptions(), ConjunctionAnalysisOptions.Default);
        Assert.Equal(new ScreeningOptions(), ScreeningOptions.Default);
        Assert.Equal(new AvoidanceSearchOptions(), AvoidanceSearchOptions.Default);
        Assert.Null(ScreeningOptions.Default.Analysis);
        Assert.Null(AvoidanceSearchOptions.Default.Analysis);
    }

    public static IEnumerable<object[]> InvalidAnalysisValueRows()
    {
        foreach (double seconds in new[] { 0.0, -5.0 })
        {
            yield return new object[] { "SampleStep", seconds };
            yield return new object[] { "MaxEventSearchStep", seconds };
        }

        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 })
        {
            yield return new object[] { "SecondaryHardBodyRadiusMeters", value };
        }

        foreach (double value in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            yield return new object[] { "NonlinearityIndexThreshold", value };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidAnalysisValueRows))]
    public void ConjunctionAnalysisOptions_InvalidValue_ThrowsAtInit(string property, double value)
    {
        // Act: the record built with the value, then copied with it (with goes through the same init)
        Func<ConjunctionAnalysisOptions> build = property switch
        {
            "SampleStep" => () => new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromSeconds(value) },
            "MaxEventSearchStep" => () => new ConjunctionAnalysisOptions { MaxEventSearchStep = TimeSpan.FromSeconds(value) },
            "SecondaryHardBodyRadiusMeters" => () => new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = value },
            _ => () => new ConjunctionAnalysisOptions { NonlinearityIndexThreshold = value }
        };
        Func<ConjunctionAnalysisOptions> copy = property switch
        {
            "SampleStep" => () => ConjunctionAnalysisOptions.Default with { SampleStep = TimeSpan.FromSeconds(value) },
            "MaxEventSearchStep" => () => ConjunctionAnalysisOptions.Default with { MaxEventSearchStep = TimeSpan.FromSeconds(value) },
            "SecondaryHardBodyRadiusMeters" => () => ConjunctionAnalysisOptions.Default with { SecondaryHardBodyRadiusMeters = value },
            _ => () => ConjunctionAnalysisOptions.Default with { NonlinearityIndexThreshold = value }
        };

        // Assert: refused where it is set, naming the property (D1; before 11.0.0, at the start of the analysis, and an
        // avoidance mirror at the start of EvaluateAvoidance)
        Assert.Equal(property, Assert.Throws<ArgumentOutOfRangeException>(() => build()).ParamName);
        Assert.Equal(property, Assert.Throws<ArgumentOutOfRangeException>(() => copy()).ParamName);
    }

    [Fact]
    public void ConjunctionAnalysisOptions_CovarianceOptionsTheAnalysisCannotApply_ThrowAtInit()
    {
        var covariance = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };

        // The state transition matrix alone and the step control on Φ and Ψ, which the replay cannot apply
        var stateTransition = Assert.Throws<ArgumentException>(() => new ConjunctionAnalysisOptions
        {
            CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly }
        });
        var stepControl = Assert.Throws<ArgumentException>(() => ConjunctionAnalysisOptions.Default with
        {
            CovariancePropagation = covariance with { IncludeInStepControl = true }
        });

        Assert.Equal(nameof(ConjunctionAnalysisOptions.CovariancePropagation), stateTransition.ParamName);
        Assert.Equal(nameof(ConjunctionAnalysisOptions.CovariancePropagation), stepControl.ParamName);
        Assert.Equal(CovariancePropagationOptions.None,
            (ConjunctionAnalysisOptions.Default with { CovariancePropagation = CovariancePropagationOptions.None }).CovariancePropagation);
    }

    [Fact]
    public void Screen_AnalysisNull_IsTheScreeningDefaultBitForBit()
    {
        // Arrange: the pair propagated in RK7(8) with its initial covariance, so that a propagated covariance could be read
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        PropagateRk78(protectedCraft, earth);
        PropagateRk78(secondaryCraft, earth);
        var screening = ScreeningOptions.Default with { MaxMissDistanceMeters = double.MaxValue };

        // Act
        var screened = Assert.Single(ConjunctionAssessment.Screen(protectedCraft, new[] { secondaryCraft }, InputRuleWindow, screening));
        var analyzed = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow, ConjunctionAnalysisOptions.Default with
        {
            SampleStep = TimeSpan.FromMinutes(5.0),
            CovariancePropagation = CovariancePropagationOptions.None
        });

        // Assert: the screening's settings of 10.x (a 5 min sample step, the covariance held in RTN), stated by the
        // encounter, the radius of the candidate resolved
        LabelAssert.Equal(analyzed.EncounterState.Epoch, screened.EncounterState.Epoch);
        Assert.Equal(analyzed.EncounterState.MissDistanceMeters, screened.EncounterState.MissDistanceMeters);
        Assert.Equal(analyzed.CollisionRisk.ProbabilityOfCollision, screened.CollisionRisk.ProbabilityOfCollision);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, screened.EncounterState.ProtectedCovariance.Source);
        Assert.Equal(TimeSpan.FromMinutes(5.0), screened.AnalysisOptions.SampleStep);
        Assert.Equal(CovariancePropagationOptions.None, screened.AnalysisOptions.CovariancePropagation);
        Assert.Equal(3.0, screened.AnalysisOptions.SecondaryHardBodyRadiusMeters);
    }

    [Fact]
    public void Screen_AnalysisGiven_IsHonouredAsGiven()
    {
        // Arrange: the pair propagated in RK7(8) with its initial covariance; the screening with the analysis's own
        // defaults, which propagate the covariance and sample every minute
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 0.0);
        PropagateRk78(protectedCraft, earth);
        PropagateRk78(secondaryCraft, earth);
        var analysis = ConjunctionAnalysisOptions.Default with { SecondaryHardBodyRadiusMeters = 2.0 };

        // Act
        var screened = Assert.Single(ConjunctionAssessment.Screen(protectedCraft, new[] { secondaryCraft }, InputRuleWindow,
            ScreeningOptions.Default with { Analysis = analysis, MaxMissDistanceMeters = double.MaxValue }));
        var analyzed = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow, analysis);

        // Assert: the analysis of Analyze with the same settings, its covariance propagated (#668, R17, choice 6 of the
        // annex: one mechanism, no hidden override), the option the fallback radius of a candidate that gives none
        LabelAssert.Equal(analyzed.EncounterState.Epoch, screened.EncounterState.Epoch);
        Assert.Equal(analyzed.CollisionRisk.ProbabilityOfCollision, screened.CollisionRisk.ProbabilityOfCollision);
        Assert.Equal(CovarianceSource.Propagated, screened.EncounterState.ProtectedCovariance.Source);
        Assert.Equal(ProtectedHardBodyRadius + 2.0, screened.CollisionRisk.CombinedHardBodyRadiusMeters);
        Assert.Equal(analysis.SampleStep, screened.AnalysisOptions.SampleStep);
    }

    [Fact]
    public void EvaluateAvoidance_Analysis_NullInheritsTheEncounterAndAGivenOneIsUsed()
    {
        // Arrange: the pair two-body, each initial covariance 30 min older than the TCA: stale at the default 60 s
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var fresh = ConjunctionAnalysisOptions.Default with { StaleCovarianceThreshold = TimeSpan.FromDays(1.0) };
        var staleEncounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);
        var freshEncounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow, fresh);
        Assert.True(staleEncounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        Assert.False(freshEncounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));

        // Act
        var inherited = ConjunctionAssessment.EvaluateAvoidance(freshEncounter, InputRuleAvoidance).Options;
        var given = ConjunctionAssessment.EvaluateAvoidance(staleEncounter, InputRuleAvoidance with { Analysis = fresh }).Options;

        // Assert: null takes the settings of the encounter (#683), a given record replaces them; both the same options
        Assert.Equal(6, inherited.Count);
        Assert.Equal(inherited.Count, given.Count);
        for (int i = 0; i < inherited.Count; i++)
        {
            var a = inherited[i].PostManeuverEncounter;
            var b = given[i].PostManeuverEncounter;
            Assert.False(a.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
            Assert.Equal(a.EncounterState.QualityFlags, b.EncounterState.QualityFlags);
            LabelAssert.Equal(a.EncounterState.Epoch, b.EncounterState.Epoch);
            Assert.Equal(a.CollisionRisk.ProbabilityOfCollision, b.CollisionRisk.ProbabilityOfCollision);
        }

        // A radius of the given record that differs from the secondary's own is refused, naming the options
        var conflict = Assert.Throws<ArgumentException>(() => ConjunctionAssessment.EvaluateAvoidance(staleEncounter,
            InputRuleAvoidance with { Analysis = fresh with { SecondaryHardBodyRadiusMeters = 2.0 } }).Options);
        Assert.Equal("options", conflict.ParamName);
        Assert.True(given.All(o => o.PostManeuverEncounter.CollisionRisk.CombinedHardBodyRadiusMeters == ProtectedHardBodyRadius + 3.0));
    }
}
