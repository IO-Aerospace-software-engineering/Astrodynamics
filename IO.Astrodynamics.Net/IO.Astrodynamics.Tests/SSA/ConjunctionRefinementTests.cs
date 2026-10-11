// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.SSA;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The refinement of the minima of the polynomial search on the range rate of the states
/// (<c>ConjunctionAssessment.RefineOnRangeRate</c>), on constructed range rates whose zero is known.
/// </summary>
public class ConjunctionRefinementTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private const double Tolerance = 1.0e-9;

    // Candidates every 100 s, as the step boundaries of the polynomial search
    private static TimeSystem.Time[] Candidates(int count) =>
        Enumerable.Range(0, count).Select(i => Epoch.AddSeconds(100.0 * i)).ToArray();

    // The range rate of a rectilinear relative motion r(t) = r0 + v t, closest at t*: g = (r0 + v t)·v
    private static Func<TimeSystem.Time, double> Rectilinear(double closestSeconds)
    {
        var velocity = new Vector3(7000.0, -3000.0, 1500.0);
        var closest = new Vector3(120.0, 300.0, 40.0);
        closest -= velocity * (closest * velocity / velocity.MagnitudeSquared());
        var r0 = closest - velocity * closestSeconds;
        return epoch => (r0 + velocity * (epoch - Epoch).TotalSeconds) * velocity;
    }

    // A range rate that only changes sign at t*, flat on each side: no secant is defined, so the refinement bisects
    // between the candidates
    private static Func<TimeSystem.Time, double> Step(double zeroSeconds) =>
        epoch => (epoch - Epoch).TotalSeconds < zeroSeconds ? -1.0 : 1.0;

    private static TimeSystem.Time Refine(TimeSystem.Time[] candidates, int minimumIndex,
        Func<TimeSystem.Time, double> rangeRate) =>
        ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[minimumIndex] }, candidates, rangeRate, Tolerance)
            .Single();

    private static void AssertWithinATick(double expectedSeconds, TimeSystem.Time actual)
    {
        // One tick, 100 ns, the resolution of the epochs, and the rounding of its difference in seconds
        double error = (actual - Epoch).TotalSeconds - expectedSeconds;
        Assert.True(System.Math.Abs(error) <= 1.0e-7 * (1.0 + 1.0e-6), $"TCA {error:E3} s from the zero");
    }

    [Theory]
    [InlineData(303.7)] // next to the minimum
    [InlineData(413.73456789)] // past the right neighbour
    [InlineData(113.2)] // before the left neighbour
    public void ARectilinearEncounter_IsRefinedToItsZero_ByTheSecants(double closestSeconds)
    {
        // Arrange: the polynomial search put the minimum at the candidate of 300 s
        var candidates = Candidates(7);
        var rangeRate = Rectilinear(closestSeconds);

        // Act
        var tca = Refine(candidates, 3, rangeRate);

        // Assert
        AssertWithinATick(closestSeconds, tca);
    }

    [Fact]
    public void WhenTheZeroLiesPastTheRightNeighbour_TheBracketMovesToTheNextCandidate()
    {
        // Arrange: still closing at the right neighbour (200 s), receding at the next candidate (300 s)
        var candidates = Candidates(6);

        // Act
        var tca = Refine(candidates, 1, Step(213.73456789));

        // Assert
        AssertWithinATick(213.73456789, tca);
    }

    [Fact]
    public void WhenTheZeroLiesBeforeTheLeftNeighbour_TheBracketMovesToThePreviousCandidate()
    {
        // Arrange: already receding at the left neighbour (200 s), closing at the previous candidate (100 s)
        var candidates = Candidates(6);

        // Act
        var tca = Refine(candidates, 3, Step(151.25));

        // Assert
        AssertWithinATick(151.25, tca);
    }

    [Fact]
    public void TheBracketMovesTwiceAtMost_ThenTheMinimumIsKept()
    {
        // Arrange: the zero lies three candidates past the minimum
        var candidates = Candidates(7);

        // Act
        var tca = Refine(candidates, 1, Step(450.0));

        // Assert
        Assert.Equal(2, ConjunctionAssessment.MaximumBracketWidenings);
        Assert.Equal(candidates[1], tca);

        // Two candidates past the minimum is within reach
        AssertWithinATick(350.0, Refine(candidates, 1, Step(350.0)));
    }

    [Fact]
    public void AZeroBeforeTheFirstCandidate_CannotBeBracketed_AndTheMinimumIsKept()
    {
        var candidates = Candidates(6);

        Assert.Equal(candidates[1], Refine(candidates, 1, Step(-30.0)));
    }

    [Fact]
    public void AZeroWithinAMicrosecondOfTheFirstCandidate_IsRefinedBetweenTheCandidates()
    {
        // The bracket checked around the secant estimate would cross the first candidate
        var candidates = Candidates(6);

        AssertWithinATick(5.0e-7, Refine(candidates, 1, Rectilinear(5.0e-7)));
    }

    [Fact]
    public void ASecantOutOfReachOfTheCandidates_FallsBackToTheCandidates()
    {
        // Closing slowly at the minimum, so that the secant points a billion seconds away, then receding past 213.7 s
        var candidates = Candidates(6);
        Func<TimeSystem.Time, double> rangeRate = epoch =>
        {
            double t = (epoch - Epoch).TotalSeconds;
            return t < 213.73456789 ? -1.0 + 1.0e-9 * t : 1.0;
        };

        AssertWithinATick(213.73456789, Refine(candidates, 1, rangeRate));
    }

    [Fact]
    public void AMinimumThatIsNotACandidate_IsKept()
    {
        var candidates = Candidates(6);
        var minimum = Epoch.AddSeconds(150.0);

        var refined = ConjunctionAssessment.RefineOnRangeRate(new[] { minimum }, candidates, Rectilinear(170.0),
            Tolerance);

        Assert.Equal(minimum, refined.Single());
    }

    [Fact]
    public void AMinimumAtABoundaryOfTheCandidates_IsKept()
    {
        var candidates = Candidates(4);

        Assert.Equal(candidates[0], Refine(candidates, 0, Rectilinear(30.0)));
        Assert.Equal(candidates[3], Refine(candidates, 3, Rectilinear(270.0)));
    }

    /// <summary>
    /// Range rates far from linear near their zero, so that a secant estimate, or the bracket checked around it, fails:
    /// the refinement falls back to the candidates and still ends on the epoch of the bisection.
    /// </summary>
    [Theory]
    [InlineData("cube root", 213.73456789)]
    [InlineData("cube", 187.12345678)]
    [InlineData("fast", 251.5)]
    public void ARangeRateFarFromLinear_IsRefinedToTheEpochOfTheBisection(string shape, double zeroSeconds)
    {
        // Arrange
        var candidates = Candidates(6);
        Func<TimeSystem.Time, double> rangeRate = shape switch
        {
            // Steep at the zero: the secant from the minimum lands far from it
            "cube root" => epoch => System.Math.Cbrt((epoch - Epoch).TotalSeconds - zeroSeconds),
            // Flat at the zero: the secant overshoots, out of reach of the candidates
            "cube" => epoch => System.Math.Pow((epoch - Epoch).TotalSeconds - zeroSeconds, 3.0),
            // Linear far away, a sharp step within a microsecond of the zero
            _ => epoch =>
            {
                double t = (epoch - Epoch).TotalSeconds - zeroSeconds;
                return System.Math.Abs(t) < 1.0e-6 ? System.Math.Sign(t) * 1.0e6 : t;
            }
        };

        // Act
        var tca = Refine(candidates, 2, rangeRate);

        // Assert
        Assert.Equal(Refine(candidates, 2, Step(zeroSeconds)), tca);
        AssertWithinATick(zeroSeconds, tca);
    }

    [Theory]
    [InlineData(103.7)]
    [InlineData(187.12345678)]
    [InlineData(213.73456789)]
    public void TheSecantsAndTheBisectionBetweenTheCandidates_EndOnTheSameEpoch(double zeroSeconds)
    {
        // Arrange: two range rates with the same sign on every epoch; the first one is refined by the secants, the
        // second one, flat, by the bisection between the candidates
        var candidates = Candidates(6);
        var rectilinear = Rectilinear(zeroSeconds);
        var step = Step(zeroSeconds);
        bool sameSigns = Enumerable.Range(-50, 101)
            .Select(i => Epoch.AddSeconds(zeroSeconds).AddTicks(i))
            .All(epoch => (rectilinear(epoch) < 0.0) == (step(epoch) < 0.0));
        Assert.True(sameSigns);

        // Act
        var bySecants = Refine(candidates, 1, rectilinear);
        var byBisection = Refine(candidates, 1, step);

        // Assert
        Assert.Equal(byBisection, bySecants);
    }
}
