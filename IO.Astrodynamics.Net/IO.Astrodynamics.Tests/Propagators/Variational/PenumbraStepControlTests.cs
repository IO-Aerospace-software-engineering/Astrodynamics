// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// RK7(8) with Φ and Ψ in the step control (<c>IncludeInStepControl</c>) through the SRP penumbra (#423): with the
/// derivative of the shadow function, the step no longer follows the rounding of the central differences there.
/// </summary>
/// <remarks>
/// Before #423 the SRP partials in the penumbra were the central differences of the acceleration, whose noise, 5.0e-7 to
/// 1.6e-3 of their block (<c>SrpPenumbraNoiseTests</c>), held the step of the velocity rows of Φ: R3 took 1 947, 4 379
/// and 23 967 steps a day at 1e-11, 1e-12 and 1e-13, 21 596 of them in its 265 s of penumbra at 1e-13, and stopped on
/// the guard of #420 at 13.3 h at 1e-14; R4 took 2 402 steps at 1e-14. Linux, Release, 2026-10-22.
/// </remarks>
public class PenumbraStepControlTests : IClassFixture<ReferenceCases>
{
    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public PenumbraStepControlTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private static int Steps(PropagationSolution solution) => solution.Segments.Sum(s => s.Steps.Count);

    // Budgets at twice the worst steps measured on Linux, Windows and macOS arm64 (lesson 13 of #364). Linux (2026-10-22):
    // R3 1 692, 2 247, 3 017 and 4 757 steps at 1e-11 to 1e-14 (748, 1 010, 1 344 and 1 821 without the option); R4 252
    // and 458 at 1e-13 and 1e-14 (119 and 171)
    [Theory]
    [InlineData("R3", 1e-11, 3384)]
    [InlineData("R3", 1e-12, 4494)]
    [InlineData("R3", 1e-13, 6034)]
    [InlineData("R3", 1e-14, 9514)]
    [InlineData("R4", 1e-13, 504)]
    [InlineData("R4", 1e-14, 916)]
    public void IncludeInStepControl_ThroughThePenumbra_CompletesWithinItsBudget(string name, double tolerance,
        int budget)
    {
        // Arrange
        var referenceCase = _cases[name];

        // Act
        var solution = referenceCase.Propagate(-1423, tolerance,
            new VariationalOptions(referenceCase.Parameters, IncludeInStepControl: true));
        int steps = Steps(solution);
        int withoutTheOption = Steps(referenceCase.Propagate(-1424, tolerance,
            new VariationalOptions(referenceCase.Parameters)));

        // Assert: the propagation reaches the end of its day within its budget
        _output.WriteLine($"MEASURE #423 {name}, {tolerance:E0}: {steps} steps with the option, {withoutTheOption} " +
                          "without it");
        Assert.True(steps <= budget, $"{steps} steps, budget {budget}");
        Assert.Equal(6, solution.GetStateTransitionMatrix(referenceCase.Window.EndDate).Rows);
    }

    // The eclipsing LEO with its apogee raise, drag and SRP, Ψ_Cd and Ψ_Cr, 6 h, in the shadow at the start. Linux
    // (2026-10-22): 547, 771 and 1 064 steps at 1e-12 to 1e-14 (571, 837 and 1 335 before #423; 213, 286 and 394
    // without the option)
    [Theory]
    [InlineData(1e-12, 1094)]
    [InlineData(1e-13, 1542)]
    [InlineData(1e-14, 2128)]
    public void IncludeInStepControl_EclipsingCaseWithAManeuver_CompletesWithinItsBudget(double tolerance, int budget)
    {
        // Act
        var (solution, _) = _cases.PropagateEclipsing(-1433, tolerance,
            new VariationalOptions(_cases.Eclipsing.Parameters, IncludeInStepControl: true));
        int steps = Steps(solution);

        // Assert
        _output.WriteLine($"MEASURE #423 eclipsing case with a maneuver, {tolerance:E0}: {steps} steps with the option");
        Assert.True(steps <= budget, $"{steps} steps, budget {budget}");
    }

    [Fact]
    public void EveryActivationPathAndBothModes_TakeTheSameSteps()
    {
        // Arrange: R3 at 1e-13, Φ and Ψ_Cr in the step control, through every public path; P0 for the covariance mode,
        // which adds no term to the step control
        var r3 = _cases.R3;
        const double tolerance = 1e-13;
        var stateTransition = new CovariancePropagationOptions { IncludeInStepControl = true };
        var covariance = stateTransition with { Mode = CovariancePropagationMode.Covariance };
        var deltaT = TimeSpan.FromSeconds(3600.0);
        Spacecraft New(int naifId) => ReferenceCase.NewSpacecraft(naifId, r3.InitialState);
        Spacecraft WithP0(int naifId) =>
            ReferenceCase.NewSpacecraft(naifId, CovariancePropagationTests.WithCovariance(r3.InitialState));

        // Act
        var reference = r3.Propagate(-1425, tolerance, new VariationalOptions(r3.Parameters, IncludeInStepControl: true));
        var solutions = new (string Path, PropagationSolution Solution)[]
        {
            ("builder, state transition only", new CentralBodyPropagatorBuilder(r3.Window, New(-1426),
                    new RK78Integrator(tolerance, tolerance), deltaT).WithPerturbingBodies(r3.Bodies)
                .IncludeSolarRadiationPressure().WithCovariancePropagation(stateTransition).Build().Propagate()),
            ("builder, covariance", new CentralBodyPropagatorBuilder(r3.Window, WithP0(-1427),
                    new RK78Integrator(tolerance, tolerance), deltaT).WithPerturbingBodies(r3.Bodies)
                .IncludeSolarRadiationPressure().WithCovariancePropagation(covariance).Build().Propagate()),
            ("constructor", new CentralBodyPropagator(r3.Window, New(-1428), new RK78Integrator(tolerance, tolerance),
                r3.Bodies, false, true, deltaT, stateTransition).Propagate()),
            ("spacecraft", New(-1429).Propagate(r3.Window, r3.Bodies, new RK78Integrator(tolerance, tolerance), false,
                true, deltaT, stateTransition)),
            ("batch", BatchPropagator.Propagate(new[]
            {
                new PropagationTask(New(-1430), r3.Window, r3.Bodies, false, true, deltaT,
                    () => new RK78Integrator(tolerance, tolerance)) { CovariancePropagation = stateTransition }
            }).Results[0].Solution)
        };

        // Assert: the steps and Φ at the end of the internal path, bit for bit
        int steps = Steps(reference);
        var end = r3.Window.EndDate;
        var phi = reference.GetStateTransitionMatrix(end);
        foreach (var (path, solution) in solutions)
        {
            _output.WriteLine($"{path}: {Steps(solution)} steps ({steps} by the internal path)");
            Assert.Equal(steps, Steps(solution));
            var other = solution.GetStateTransitionMatrix(end);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(phi.Get(i, j), other.Get(i, j));
                }
            }
        }
    }

    [Fact]
    public void R3_PsiCrWithTheOption_MatchesTheSameAtATighterTolerance()
    {
        // Arrange: Ψ_Cr, the weakest quantity (lesson 3 of #364), with Φ and Ψ in the step control at 1e-13, against the
        // same propagation at 1e-14, which completes since #423
        var r3 = _cases.R3;
        var options = new VariationalOptions(r3.Parameters, IncludeInStepControl: true);

        // Act
        var solution = r3.Propagate(-1431, ReferenceCases.Tolerance, options);
        var reference = r3.Propagate(-1432, ReferenceCases.ReferenceTolerance, options);

        // Assert: F1 (1e-4) over the arc, every half hour, relative to the largest Ψ_Cr of the arc and to the local value
        double largestPosition = 0.0;
        double largestVelocity = 0.0;
        double worstPosition = 0.0;
        double worstVelocity = 0.0;
        double worstLocal = 0.0;
        for (var epoch = r3.InitialState.Epoch.AddMinutes(30.0); epoch <= r3.Window.EndDate; epoch = epoch.AddMinutes(30.0))
        {
            var psi = solution.GetParameterSensitivity(epoch);
            var expected = reference.GetParameterSensitivity(epoch);
            double position = 0.0;
            double velocity = 0.0;
            double positionNorm = 0.0;
            double velocityNorm = 0.0;
            for (int i = 0; i < 3; i++)
            {
                position += System.Math.Pow(psi.Get(i, 0) - expected.Get(i, 0), 2);
                velocity += System.Math.Pow(psi.Get(i + 3, 0) - expected.Get(i + 3, 0), 2);
                positionNorm += System.Math.Pow(expected.Get(i, 0), 2);
                velocityNorm += System.Math.Pow(expected.Get(i + 3, 0), 2);
            }

            largestPosition = System.Math.Max(largestPosition, System.Math.Sqrt(positionNorm));
            largestVelocity = System.Math.Max(largestVelocity, System.Math.Sqrt(velocityNorm));
            worstPosition = System.Math.Max(worstPosition, System.Math.Sqrt(position));
            worstVelocity = System.Math.Max(worstVelocity, System.Math.Sqrt(velocity));
            worstLocal = System.Math.Max(worstLocal, System.Math.Max(System.Math.Sqrt(position / positionNorm),
                System.Math.Sqrt(velocity / velocityNorm)));
        }

        double overThePosition = worstPosition / largestPosition;
        double overTheVelocity = worstVelocity / largestVelocity;
        _output.WriteLine($"MEASURE #423 R3 Ψ_Cr with the option, 1e-13 against 1e-14: over the arc {overThePosition:E2} " +
                          $"(position), {overTheVelocity:E2} (velocity); local {worstLocal:E2}");
        Assert.True(overThePosition < 1e-4, $"Ψ_Cr, position, over the arc {overThePosition:E2}");
        Assert.True(overTheVelocity < 1e-4, $"Ψ_Cr, velocity, over the arc {overTheVelocity:E2}");
        Assert.True(worstLocal < 1e-4, $"Ψ_Cr, local {worstLocal:E2}");
    }
}
