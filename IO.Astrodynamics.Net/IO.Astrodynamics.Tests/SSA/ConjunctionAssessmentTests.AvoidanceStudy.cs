// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Ssa;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The protected participant is the <see cref="Spacecraft"/> itself and the avoidance grid is the constraint (R17 of #668,
/// lot L7): <c>ProtectedSpacecraftProfile</c> and <c>ManeuverConstraints</c> removed, <see cref="AvoidanceStudy"/> with
/// the lead times skipped and why, and MANEUVERABLE of object 1 always YES (decision 109 of #668).
/// </summary>
public partial class ConjunctionAssessmentTests
{
    [Fact]
    public void EntryPoints_TakeTheSpacecraft_NullThrows()
    {
        // Arrange: the pair two-body
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);
        var screened = ConjunctionAssessment.Screen(protectedCraft, new ILocalizable[] { secondaryCraft }, InputRuleWindow);

        // Assert: the encounter holds the spacecraft analyzed; null is refused naming it
        Assert.Same(protectedCraft, encounter.ProtectedSpacecraft);
        Assert.Same(protectedCraft, Assert.Single(screened).ProtectedSpacecraft);
        Assert.Equal("protectedSpacecraft", Assert.Throws<ArgumentNullException>(() =>
            ConjunctionAssessment.Analyze(null, secondaryCraft, InputRuleWindow)).ParamName);
        Assert.Equal("protectedSpacecraft", Assert.Throws<ArgumentNullException>(() =>
            ConjunctionAssessment.AnalyzeAll(null, secondaryCraft, InputRuleWindow)).ParamName);
        Assert.Equal("protectedSpacecraft", Assert.Throws<ArgumentNullException>(() =>
            ConjunctionAssessment.Screen(null, new ILocalizable[] { secondaryCraft }, InputRuleWindow)).ParamName);
    }

    [Fact]
    public void EvaluateAvoidance_TheGridIsTheConstraint_TheLeadsBeforeTheWindowSkipped()
    {
        // Arrange: the pair two-body, its TCA 30 min after the start of the window; a lead of 5 min and a magnitude of 6 m/s,
        // outside the default ManeuverConstraints of 10.x ([10 min, 24 h], 5 m/s), which dropped them; a lead of 45 min puts
        // the burn before the window
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, secondaryCraft, InputRuleWindow);
        var leads = new[] { TimeSpan.FromMinutes(5.0), TimeSpan.FromMinutes(15.0), TimeSpan.FromMinutes(45.0) };

        // Act
        AvoidanceStudy study = ConjunctionAssessment.EvaluateAvoidance(encounter, AvoidanceSearchOptions.Default with
        {
            LeadTimes = leads,
            DeltaVMagnitudesMetersPerSecond = new[] { 0.1, 6.0 },
            MaxReturnedOptions = 100
        });

        // Assert: every value of the grid evaluated, six directions each, but the lead whose burn falls before the window,
        // listed with its reason (before 11.0.0, the options were dropped without a word)
        Assert.Equal(2 * 2 * 6, study.Options.Count);
        Assert.Equal(new[] { leads[0], leads[1] }, study.Options.Select(o => encounter.EncounterState.Epoch - o.BurnEpoch)
            .Distinct().OrderBy(l => l));
        Assert.Contains(study.Options, o => System.Math.Abs(o.DeltaVInertial.Magnitude() - 6.0) < 1e-9);
        var skipped = Assert.Single(study.Skipped);
        Assert.Equal(leads[2], skipped.LeadTime);
        Assert.Equal(AvoidanceSkipReason.BeforeWindow, skipped.Reason);
        Assert.Equal($"{leads[2]}: before the window", skipped.ToString());

        // MANEUVERABLE of object 1 is YES, the post-maneuver trial without engines included (decision 109 of #668), and
        // ProtectedParticipantMetadata overrides it
        // (the initial covariances held 30 min in RTN: stale, exported on request)
        var export = new CdmExportOptions { AllowStaleCovariance = true };
        Assert.Equal(CdmManeuverable.Yes, encounter.ToCdm(export).Segments[0].Metadata!.Maneuverable);
        Assert.Equal(CdmManeuverable.Yes, study.Options[0].PostManeuverEncounter.ToCdm(export).Segments[0].Metadata!.Maneuverable);
        Assert.Equal(CdmManeuverable.No, encounter.ToCdm(export with
        {
            ProtectedParticipantMetadata = new CdmParticipantMetadata { Maneuverable = CdmManeuverable.No }
        }).Segments[0].Metadata!.Maneuverable);
    }

    public static IEnumerable<object[]> InvalidGridRows()
    {
        yield return new object[] { "LeadTimes", Array.Empty<double>() };
        yield return new object[] { "LeadTimes", new[] { 600.0, 0.0 } };
        yield return new object[] { "LeadTimes", new[] { -600.0 } };
        yield return new object[] { "DeltaVMagnitudesMetersPerSecond", Array.Empty<double>() };
        yield return new object[] { "DeltaVMagnitudesMetersPerSecond", new[] { 0.1, 0.0 } };
        yield return new object[] { "DeltaVMagnitudesMetersPerSecond", new[] { -0.1 } };
        yield return new object[] { "DeltaVMagnitudesMetersPerSecond", new[] { double.NaN } };
        yield return new object[] { "DeltaVMagnitudesMetersPerSecond", new[] { double.PositiveInfinity } };
    }

    [Theory]
    [MemberData(nameof(InvalidGridRows))]
    public void AvoidanceSearchOptions_InvalidGrid_ThrowsAtInit(string property, double[] values)
    {
        Func<AvoidanceSearchOptions> build = property == "LeadTimes"
            ? () => AvoidanceSearchOptions.Default with { LeadTimes = values.Select(TimeSpan.FromSeconds).ToArray() }
            : () => AvoidanceSearchOptions.Default with { DeltaVMagnitudesMetersPerSecond = values };

        // The grid is the constraint: empty, not positive or not finite is refused where it is set (before 11.0.0, the
        // ManeuverConstraints of the profile dropped the values outside them, a magnitude of 0 included)
        Assert.Equal(property, Assert.Throws<ArgumentOutOfRangeException>(() => build()).ParamName);
        Assert.Equal("LeadTimes", Assert.Throws<ArgumentNullException>(() =>
            new AvoidanceSearchOptions { LeadTimes = null }).ParamName);
        Assert.Equal("DeltaVMagnitudesMetersPerSecond", Assert.Throws<ArgumentNullException>(() =>
            new AvoidanceSearchOptions { DeltaVMagnitudesMetersPerSecond = null }).ParamName);
    }
}
