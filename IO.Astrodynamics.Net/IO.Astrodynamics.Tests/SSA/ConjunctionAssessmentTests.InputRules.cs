// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The input rules of the conjunction analysis (#683): the hard-body radius of the secondary (its own radius, the options
/// as fallback, a conflict refused), the protected spacecraft refused as its own secondary, and a trajectory refused when
/// it was propagated for another spacecraft.
/// </summary>
public partial class ConjunctionAssessmentTests
{
    private const double ProtectedHardBodyRadius = 8.0;
    private const double FallbackSecondaryHardBodyRadius = 5.0;
    private const double OptionUnset = -1.0;

    private static int _inputRuleIds = -68_000;

    private static int NextInputRuleId() => Interlocked.Decrement(ref _inputRuleIds);

    private static readonly TimeSystem.Time InputRuleStart = TimeSystem.Time.J2000TDB;

    // The window of the propagations and analyses, the TCA in its middle; the longer window leaves the propagations
    private static readonly Window InputRuleWindow = new(InputRuleStart, InputRuleStart.AddHours(1.0));
    private static readonly Window InputRuleWindowPastTheStates = new(InputRuleStart, InputRuleStart.AddHours(2.0));

    // One lead time, one magnitude: six options
    private static readonly AvoidanceSearchOptions InputRuleAvoidance = new()
    {
        LeadTimes = new[] { TimeSpan.FromMinutes(15.0) },
        DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
        MaxReturnedOptions = 6
    };

    // A pair meeting 200 m apart 30 min after the start in the two-body problem (ConjunctionCovarianceCase.Pair), each
    // with an RTN covariance
    private static (StateVector Protected, StateVector Secondary) InputRulePair(CelestialBody earth)
    {
        var (protectedOrbit, secondaryOrbit) =
            ConjunctionCovarianceCase.Pair(earth, InputRuleStart, InputRuleStart.AddMinutes(30.0));
        return (ConjunctionCovarianceCase.WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 }),
            ConjunctionCovarianceCase.WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 }));
    }

    private static Spacecraft InputRuleSpacecraft(int naifId, StateVector state, double hardBodyRadius) =>
        new(naifId, "IR" + -naifId, 120.0, 150.0, new Clock("ir" + -naifId, 256), state, sectionalArea: 2.0,
            hardBodyRadius: hardBodyRadius);

    private static PropagationSolution PropagateRk78(Spacecraft spacecraft, CelestialBody earth) =>
        spacecraft.Propagate(InputRuleWindow, new CelestialItem[] { earth }, new RK78Integrator(), false, false,
            TimeSpan.FromSeconds(60.0));

    public static IEnumerable<object[]> PrecedenceRows()
    {
        foreach (var entryPoint in new[]
                 {
                     "Analyze", "AnalyzeAll", "AnalyzeDense", "AnalyzeAllDense", "Screen", "AvoidanceTwoBody",
                     "AvoidancePropagated"
                 })
        {
            // Object only, option only, equal, different, neither
            yield return new object[] { entryPoint, "spacecraft", 3.0, OptionUnset };
            yield return new object[] { entryPoint, "spacecraft", 0.0, 2.0 };
            yield return new object[] { entryPoint, "spacecraft", 3.0, 3.0 };
            yield return new object[] { entryPoint, "spacecraft", 3.0, 2.0 };
            yield return new object[] { entryPoint, "spacecraft", 0.0, OptionUnset };
            if (!entryPoint.EndsWith("Dense"))
            {
                // A secondary that is not a spacecraft: the option, or 5 m
                yield return new object[] { entryPoint, "body", 0.0, 2.0 };
                yield return new object[] { entryPoint, "body", 0.0, OptionUnset };
            }
        }
    }

    [Theory]
    [MemberData(nameof(PrecedenceRows))]
    public void SecondaryHardBodyRadius_PrecedenceRule(string entryPoint, string secondaryKind, double secondaryRadius,
        double option)
    {
        // Arrange: the protected spacecraft 8 m, propagated in RK7(8) with its initial covariance (but for the two-body
        // avoidance, not propagated); the secondary a spacecraft of the given radius or the Moon
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        bool propagated = entryPoint != "AvoidanceTwoBody";
        var protectedTrajectory = propagated ? PropagateRk78(protectedCraft, earth) : null;
        ILocalizable secondary;
        PropagationSolution secondaryTrajectory = null;
        if (secondaryKind == "spacecraft")
        {
            var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, secondaryRadius);
            secondaryTrajectory = PropagateRk78(secondaryCraft, earth);
            secondary = secondaryCraft;
        }
        else
        {
            secondary = new CelestialBody(PlanetsAndMoons.MOON);
        }

        double? given = option < 0.0 ? null : option;
        bool secondaryHasRadius = secondaryKind == "spacecraft" && secondaryRadius > 0.0;
        bool conflict = secondaryHasRadius && given.HasValue && given.Value != secondaryRadius;
        double expected = ProtectedHardBodyRadius + (secondaryHasRadius ? secondaryRadius : given ?? FallbackSecondaryHardBodyRadius);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var analysisOptions = given is { } a ? new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = a } : new ConjunctionAnalysisOptions();

        // A conflict is refused before any state is read: the window then leaves the propagated states
        var window = conflict && !entryPoint.StartsWith("Avoidance") ? InputRuleWindowPastTheStates : InputRuleWindow;
        Func<IReadOnlyList<EncounterCase>> act = entryPoint switch
        {
            "Analyze" => () => new[] { ConjunctionAssessment.Analyze(profile, secondary, window, analysisOptions) },
            "AnalyzeAll" => () => ConjunctionAssessment.AnalyzeAll(profile, secondary, window, analysisOptions),
            "AnalyzeDense" => () => new[]
            {
                ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondary, secondaryTrajectory, window,
                    analysisOptions)
            },
            "AnalyzeAllDense" => () =>
                ConjunctionAssessment.AnalyzeAll(profile, protectedTrajectory, secondary, secondaryTrajectory, window,
                    analysisOptions),
            "Screen" => () => ConjunctionAssessment.Screen(profile, new[] { secondary }, window,
                given is { } d
                    ? new ScreeningOptions { DefaultSecondaryHardBodyRadiusMeters = d, MaxMissDistanceMeters = double.MaxValue }
                    : new ScreeningOptions { MaxMissDistanceMeters = double.MaxValue }),
            _ => () =>
            {
                // The encounter analyzed with the default options, the avoidance with the option
                var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);
                var avoidanceOptions = given is { } o
                    ? InputRuleAvoidance with { SecondaryHardBodyRadiusMeters = o }
                    : InputRuleAvoidance;
                var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, avoidanceOptions);
                if (secondaryKind == "spacecraft")
                {
                    Assert.Equal(6, avoidance.Count);
                    Assert.Equal(entryPoint == "AvoidancePropagated" ? CovarianceSource.Propagated : CovarianceSource.InitialStateHeldInRtn,
                        encounter.EncounterState.ProtectedCovariance.Source);
                }

                return avoidance.Select(o => o.PostManeuverEncounter).ToArray();
            }
        };

        // Act and assert
        if (conflict)
        {
            var exception = Assert.Throws<ArgumentException>(() => act());
            string expectedParameter = entryPoint switch
            {
                "Screen" => "candidates",
                "AvoidanceTwoBody" or "AvoidancePropagated" => "options",
                _ => "secondaryObject"
            };
            Assert.Equal(expectedParameter, exception.ParamName);
            Assert.Contains(secondary.Name, exception.Message);
            Assert.Contains(entryPoint == "Screen" ? "DefaultSecondaryHardBodyRadiusMeters" : "SecondaryHardBodyRadiusMeters",
                exception.Message);
            return;
        }

        var encounters = act();
        Assert.NotEmpty(encounters);
        Assert.All(encounters, e => Assert.Equal(expected, e.CollisionRisk.CombinedHardBodyRadiusMeters));
    }

    [Fact]
    public void Screen_RadiusConflictBehindAnotherCandidate_ThrowsBeforeTheFirstAnalysis()
    {
        // Arrange: two candidates propagated over the hour of the pair, the first without a radius of its own (the option
        // gives it 2 m), the second of 3 m beside the option of 2 m; the window leaves the states, so that the analysis of
        // the first candidate throws on its coverage
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var first = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 0.0);
        var second = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        PropagateRk78(protectedCraft, earth);
        PropagateRk78(first, earth);
        PropagateRk78(second, earth);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var options = new ScreeningOptions { DefaultSecondaryHardBodyRadiusMeters = 2.0, MaxMissDistanceMeters = double.MaxValue };
        var firstAlone = Record.Exception(() =>
            ConjunctionAssessment.Screen(profile, new ILocalizable[] { first }, InputRuleWindowPastTheStates, options));
        Assert.NotNull(firstAlone);
        Assert.False(firstAlone is ArgumentException { ParamName: "candidates" });

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.Screen(profile, new ILocalizable[] { first, second }, InputRuleWindowPastTheStates, options));

        // Assert: the radii of every candidate resolved before the first analysis (#683), the conflict named
        Assert.Equal("candidates", exception.ParamName);
        Assert.Contains($"{second.Name} (NAIF {second.NaifId})", exception.Message);
        Assert.Contains("DefaultSecondaryHardBodyRadiusMeters", exception.Message);
    }

    public static IEnumerable<object[]> InvalidRadiusRows()
    {
        foreach (var options in new[] { "analysis", "analysisDense", "screening", "avoidance" })
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 })
            {
                yield return new object[] { options, value };
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRadiusRows))]
    public void SecondaryHardBodyRadiusOption_NotFiniteOrNegative_Throws(string optionsType, double value)
    {
        // Arrange: a secondary without a radius of its own, which the option would then give
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 0.0);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act
        Action act = optionsType switch
        {
            "analysis" => () => ConjunctionAssessment.Analyze(profile, secondaryCraft, InputRuleWindow,
                new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = value }),
            "analysisDense" => () => ConjunctionAssessment.AnalyzeAll(profile, PropagateRk78(protectedCraft, earth),
                secondaryCraft, PropagateRk78(secondaryCraft, earth), InputRuleWindow,
                new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = value }),
            "screening" => () => ConjunctionAssessment.Screen(profile, new ILocalizable[] { secondaryCraft }, InputRuleWindow,
                new ScreeningOptions { DefaultSecondaryHardBodyRadiusMeters = value }),
            _ => () => ConjunctionAssessment.EvaluateAvoidance(
                ConjunctionAssessment.Analyze(profile, secondaryCraft, InputRuleWindow),
                InputRuleAvoidance with { SecondaryHardBodyRadiusMeters = value })
        };

        // Assert: a radius is finite and not negative (before #683, a negative option was subtracted and a NaN or an
        // infinite one gave no Pc and no flag)
        var exception = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal(optionsType == "screening" ? "DefaultSecondaryHardBodyRadiusMeters" : "SecondaryHardBodyRadiusMeters",
            exception.ParamName);
    }

    [Fact]
    public void Analyze_SecondaryHardBodyRadius_DesignedEncounters_MatchExactPc()
    {
        // Arrange: the seven designed encounters of Analyze_DesignedEncounters_MatchExactPcAndFlags, the combined radius
        // of the reference split between the two spacecraft (0.6 R on the protected one, 0.4 R on the secondary), no
        // option: the analysis must add the secondary's own radius (before #683, 5 m in its place)
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        double worst = 0.0;
        int id = 0;
        foreach (var encounter in CollisionProbabilityReference.Value.RootElement.GetProperty("encounters").EnumerateArray())
        {
            var (r1, v1, c1, r2, v2, c2, hardBodyRadius) = ReadEncounter(encounter, 1000.0);
            var exact = encounter.GetProperty("exact");
            double protectedRadius = 0.6 * hardBodyRadius;
            double secondaryRadius = 0.4 * hardBodyRadius;
            var protectedSpacecraft = c1 is null
                ? CreateSpacecraftWithoutCovariance(-6200 - 2 * id, "PC_P", earth, r1, v1, protectedRadius)
                : CreateSpacecraft(-6200 - 2 * id, "PC_P", earth, r1, v1, BuildStateCovarianceFromPosition(c1.Value), protectedRadius);
            var secondarySpacecraft = c2 is null
                ? CreateSpacecraftWithoutCovariance(-6201 - 2 * id, "PC_S", earth, r2, v2, secondaryRadius)
                : CreateSpacecraft(-6201 - 2 * id, "PC_S", earth, r2, v2, BuildStateCovarianceFromPosition(c2.Value), secondaryRadius);
            id++;
            var protectedTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
                seconds => (r1 + v1 * (seconds - 10.0), v1, Vector3.Zero));
            var secondaryTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
                seconds => (r2 + v2 * (seconds - 10.0), v2, Vector3.Zero));

            // Act
            var result = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedSpacecraft),
                protectedTrajectory, secondarySpacecraft, secondaryTrajectory, window,
                new ConjunctionAnalysisOptions
                {
                    SampleStep = TimeSpan.FromDays(1.0),
                    MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                    LowRelativeSpeedThresholdMetersPerSecond = 0.0
                });

            // Assert: the combined radius of the two, and the exact Pc at the reference's radius within the bound of the
            // public Analyze (the split rounds the sum by an ulp at most, a relative 4e-16 of the Pc)
            var name = encounter.GetProperty("name").GetString();
            Assert.Equal(protectedRadius + secondaryRadius, result.CollisionRisk.CombinedHardBodyRadiusMeters);
            Assert.True(System.Math.Abs(result.CollisionRisk.CombinedHardBodyRadiusMeters - hardBodyRadius) <= 1e-12 * hardBodyRadius,
                $"{name}: combined {result.CollisionRisk.CombinedHardBodyRadiusMeters:R} m against {hardBodyRadius:R} m");
            Assert.NotNull(result.CollisionRisk.ProbabilityOfCollision);
            double error = RelativeError(result.CollisionRisk.ProbabilityOfCollision.Value, exact.GetProperty("pc"));
            worst = System.Math.Max(worst, error);
            _output.WriteLine($"MEASURE #683 split radius {name}: relative error {error:E2}");
            Assert.True(error <= DesignedEncounterAnalyzePcBound, $"{name}: {error:E2}");
        }

        _output.WriteLine($"MEASURE #683 split radius worst {worst:E2}");
    }

    public static IEnumerable<object[]> SelfSecondaryRows()
    {
        foreach (var overload in new[] { "Analyze", "AnalyzeAll", "AnalyzeDense", "AnalyzeAllDense" })
        {
            yield return new object[] { overload, false };
            yield return new object[] { overload, true };
        }
    }

    [Theory]
    [MemberData(nameof(SelfSecondaryRows))]
    public void Analyze_ProtectedAsItsOwnSecondary_Throws(string overload, bool distinctInstance)
    {
        // Arrange: the protected spacecraft propagated; the secondary is that instance, or another spacecraft with its
        // NAIF id, propagated too; the window leaves the states, so the check runs before the coverage is read
        var earth = CreateEarth();
        var (protectedStart, _) = InputRulePair(earth);
        int naifId = NextInputRuleId();
        var protectedCraft = InputRuleSpacecraft(naifId, protectedStart, ProtectedHardBodyRadius);
        var protectedTrajectory = PropagateRk78(protectedCraft, earth);
        var secondary = distinctInstance ? InputRuleSpacecraft(naifId, protectedStart, 3.0) : protectedCraft;
        var secondaryTrajectory = distinctInstance ? PropagateRk78(secondary, earth) : protectedTrajectory;
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = InputRuleWindowPastTheStates;

        // Act
        Action act = overload switch
        {
            "Analyze" => () => ConjunctionAssessment.Analyze(profile, secondary, window),
            "AnalyzeAll" => () => ConjunctionAssessment.AnalyzeAll(profile, secondary, window),
            "AnalyzeDense" => () => ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondary, secondaryTrajectory, window),
            _ => () => ConjunctionAssessment.AnalyzeAll(profile, protectedTrajectory, secondary, secondaryTrajectory, window)
        };

        // Assert: Screen's NAIF-id rule (before #683: a miss of 0 and a Pc of 0.815, or an empty list on the dense
        // AnalyzeAll)
        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Equal("secondaryObject", exception.ParamName);
    }

    [Fact]
    public void Screen_ProtectedAmongTheCandidates_IsSkippedAndTheOthersAnalyzed()
    {
        // Arrange
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act
        var screened = ConjunctionAssessment.Screen(profile, new ILocalizable[] { protectedCraft, secondaryCraft }, InputRuleWindow);
        var alone = ConjunctionAssessment.Screen(profile, new ILocalizable[] { secondaryCraft }, InputRuleWindow);

        // Assert: the same encounter as without the protected spacecraft among the candidates
        var encounter = Assert.Single(screened);
        Assert.Same(secondaryCraft, encounter.SecondaryObject);
        Assert.Equal(alone[0].EncounterState.Epoch, encounter.EncounterState.Epoch);
        Assert.Equal(alone[0].EncounterState.MissDistanceMeters, encounter.EncounterState.MissDistanceMeters);
    }

    // The producers of a solution, each propagating the spacecraft it is given over the window
    private static PropagationSolution Produce(string producer, Spacecraft spacecraft, CelestialBody earth, Window window)
    {
        switch (producer)
        {
            case "Rk78":
                return spacecraft.Propagate(window, new CelestialItem[] { earth }, new RK78Integrator(), false, false,
                    TimeSpan.FromSeconds(60.0));
            case "VelocityVerlet":
                return spacecraft.Propagate(window, new CelestialItem[] { earth }, false, false, TimeSpan.FromSeconds(10.0));
            case "CentralBodyPropagator":
            {
                using var propagator = new CentralBodyPropagator(window, spacecraft, new RK78Integrator(),
                    new CelestialItem[] { earth }, false, false, TimeSpan.FromSeconds(60.0));
                return propagator.Propagate();
            }
            case "Builder":
            {
                using var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(),
                    TimeSpan.FromSeconds(60.0)).Build();
                return propagator.Propagate();
            }
            case "Batch":
            {
                var result = BatchPropagator.Propagate(new[]
                {
                    new PropagationTask(spacecraft, window, new CelestialItem[] { earth }, false, false,
                        TimeSpan.FromSeconds(60.0), () => new RK78Integrator())
                });
                Assert.True(result.AllSucceeded);
                return result.Results[0].Solution;
            }
            default:
            {
                using var propagator = new TLEPropagator(window, spacecraft, TimeSpan.FromSeconds(60.0));
                return propagator.Propagate();
            }
        }
    }

    private static readonly TLE InputRuleTle = new("ISS",
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    public static IEnumerable<object[]> ForeignTrajectoryRows()
    {
        foreach (var producer in new[] { "Rk78", "VelocityVerlet", "CentralBodyPropagator", "Builder", "Batch", "Tle" })
        {
            foreach (var pairing in new[] { "foreignProtected", "foreignSecondary", "swapped", "sameNaifIdProtected", "sameNaifIdSecondary" })
            {
                yield return new object[] { producer, pairing, false };
                yield return new object[] { producer, pairing, true };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ForeignTrajectoryRows))]
    public void Analyze_TrajectoryOfAnotherSpacecraft_Throws(string producer, string pairing, bool all)
    {
        // Arrange: each spacecraft propagated by the producer; a third spacecraft with the NAIF id of one of them
        var earth = CreateEarth();
        Spacecraft NewCraft(int naifId, StateVector state, double radius) => producer == "Tle"
            ? new Spacecraft(naifId, "IRTLE" + -naifId, 100.0, 1000.0, new Clock("irtle" + -naifId, 256), InputRuleTle,
                hardBodyRadius: radius)
            : InputRuleSpacecraft(naifId, state, radius);

        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var window = producer == "Tle"
            ? new Window(InputRuleTle.Epoch.ToTDB(), InputRuleTle.Epoch.ToTDB().AddMinutes(30.0))
            : InputRuleWindow;
        int protectedId = NextInputRuleId();
        int secondaryId = NextInputRuleId();
        var protectedCraft = NewCraft(protectedId, protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = NewCraft(secondaryId, secondaryStart, 3.0);
        var protectedTrajectory = Produce(producer, protectedCraft, earth, window);
        var secondaryTrajectory = Produce(producer, secondaryCraft, earth, window);
        var twinOfProtected = NewCraft(protectedId, protectedStart, ProtectedHardBodyRadius);
        var twinOfSecondary = NewCraft(secondaryId, secondaryStart, 3.0);
        var (first, second, parameter) = pairing switch
        {
            "foreignProtected" => (secondaryTrajectory, secondaryTrajectory, "protectedTrajectory"),
            "foreignSecondary" => (protectedTrajectory, protectedTrajectory, "secondaryTrajectory"),
            "swapped" => (secondaryTrajectory, protectedTrajectory, "protectedTrajectory"),
            "sameNaifIdProtected" => (Produce(producer, twinOfProtected, earth, window), secondaryTrajectory, "protectedTrajectory"),
            _ => (protectedTrajectory, Produce(producer, twinOfSecondary, earth, window), "secondaryTrajectory")
        };
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act
        Action act = all
            ? () => ConjunctionAssessment.AnalyzeAll(profile, first, secondaryCraft, second, window)
            : () => ConjunctionAssessment.Analyze(profile, first, secondaryCraft, second, window);

        // Assert: a trajectory describes the spacecraft it was propagated for, compared by reference (before #683, a miss
        // of 0 and a Pc of 0.676, or the covariances of the other objects)
        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Equal(parameter, exception.ParamName);

        // The own trajectories are accepted
        Assert.NotNull(all
            ? ConjunctionAssessment.AnalyzeAll(profile, protectedTrajectory, secondaryCraft, secondaryTrajectory, window)
            : new[] { ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondaryCraft, secondaryTrajectory, window) });
    }

    // From the MEASURE line below (Linux, 2026-11-14; provisional, to be set from the three platforms): the TCAs the same
    // tick (0 s), bounded by one tick, the resolution of a label; the misses 3.45e-4 m apart, bounded by twice that
    private const double OwnerlessTcaBound = 1e-7;
    private const double OwnerlessMissBound = 7.0e-4;

    [Fact]
    public void Analyze_OwnerlessTrajectory_IsAccepted()
    {
        // Arrange: the protected spacecraft's own RK7(8) solution, the secondary's built by hand (its two-body states at
        // the output epochs of a straight line would do as well: a solution built by hand states no spacecraft)
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var protectedTrajectory = PropagateRk78(protectedCraft, earth);
        var secondaryOwn = PropagateRk78(secondaryCraft, earth);
        double gm = earth.GM;
        var secondaryByHand = CreatePropagationSolution(earth, InputRuleStart, 3600.0, 60.0, seconds =>
        {
            var state = secondaryStart.ToStateVector(InputRuleStart.AddSeconds(seconds));
            return (state.Position, state.Velocity, state.Position * (-gm / System.Math.Pow(state.Position.Magnitude(), 3)));
        });
        var profile = new ProtectedSpacecraftProfile(protectedCraft);

        // Act
        var byHand = ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondaryCraft, secondaryByHand, InputRuleWindow,
            new ConjunctionAnalysisOptions { CovariancePropagation = null });
        var own = ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondaryCraft, secondaryOwn, InputRuleWindow,
            new ConjunctionAnalysisOptions { CovariancePropagation = null });
        var all = ConjunctionAssessment.AnalyzeAll(profile, protectedTrajectory, secondaryCraft, secondaryByHand, InputRuleWindow);

        // Assert: accepted, and the same close approach within the Hermite of the solution built by hand (its steps of 60 s
        // carry the two-body states and accelerations)
        Assert.NotEmpty(all);
        double tcaDifference = System.Math.Abs((byHand.EncounterState.Epoch - own.EncounterState.Epoch).TotalSeconds);
        double missDifference = System.Math.Abs(byHand.EncounterState.MissDistanceMeters - own.EncounterState.MissDistanceMeters);
        _output.WriteLine($"MEASURE #683 owner-less trajectory: TCA {tcaDifference:E3} s, miss {missDifference:E3} m from the own solution");
        Assert.True(tcaDifference <= OwnerlessTcaBound, $"TCA {tcaDifference:E3} s");
        Assert.True(missDifference < OwnerlessMissBound, $"miss {missDifference:E3} m");
    }

    [Fact]
    public void EvaluateAvoidance_EncounterBuiltByHand_TakesTheDefaultSettings()
    {
        // Arrange: the pair not propagated (two-body), the encounter analyzed with the default options, and the same
        // encounter built by hand from its public parts, which carries no settings
        var earth = CreateEarth();
        var (protectedStart, secondaryStart) = InputRulePair(earth);
        var protectedCraft = InputRuleSpacecraft(NextInputRuleId(), protectedStart, ProtectedHardBodyRadius);
        var secondaryCraft = InputRuleSpacecraft(NextInputRuleId(), secondaryStart, 3.0);
        var analyzed = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft, InputRuleWindow);
        var byHand = new EncounterCase(analyzed.ProtectedAsset, analyzed.SecondaryObject, analyzed.ScreeningWindow,
            analyzed.EncounterState, analyzed.CollisionRisk, analyzed.ProtectedState, analyzed.SecondaryState);

        // Act
        var fromAnalysis = ConjunctionAssessment.EvaluateAvoidance(analyzed, InputRuleAvoidance);
        var fromHand = ConjunctionAssessment.EvaluateAvoidance(byHand, InputRuleAvoidance);

        // Assert: the defaults of ConjunctionAnalysisOptions, those the analysis used, and the radius of the secondary
        // resolved from the secondary itself (#683)
        Assert.Equal(6, fromHand.Count);
        Assert.Equal(fromAnalysis.Count, fromHand.Count);
        for (int i = 0; i < fromHand.Count; i++)
        {
            var a = fromAnalysis[i].PostManeuverEncounter;
            var b = fromHand[i].PostManeuverEncounter;
            Assert.Equal(a.EncounterState.Epoch, b.EncounterState.Epoch);
            Assert.Equal(a.EncounterState.MissDistanceMeters, b.EncounterState.MissDistanceMeters);
            Assert.Equal(a.CollisionRisk.ProbabilityOfCollision, b.CollisionRisk.ProbabilityOfCollision);
            Assert.Equal(ProtectedHardBodyRadius + 3.0, b.CollisionRisk.CombinedHardBodyRadiusMeters);
            Assert.Equal(a.EncounterState.QualityFlags, b.EncounterState.QualityFlags);
        }
    }
}
