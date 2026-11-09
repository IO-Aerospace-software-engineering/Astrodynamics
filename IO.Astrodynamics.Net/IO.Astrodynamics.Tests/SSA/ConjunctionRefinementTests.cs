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
    public void AZeroBeforeTheFirstCandidate_LiesOutsideTheWindow_AndTheMinimumIsDropped()
    {
        // Receding at every candidate: the bracket reaches the window start with a positive range rate (kept unrefined
        // before the window-end rule of the dense search, S1 of the review of lot 6 of #457)
        var candidates = Candidates(6);

        Assert.Empty(ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1] }, candidates, Step(-30.0), Tolerance));
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

    // ---- The window ends (S1 of the review of lot 6 of #457): the first and last candidates are the window ends, and the
    // range rate there, not the minima of the polynomials, decides whether a minimum lies inside the window ----

    // The range rate, refused outside the candidates: the refinement reads nothing outside the window
    private static Func<TimeSystem.Time, double> InsideOnly(TimeSystem.Time[] candidates,
        Func<TimeSystem.Time, double> rangeRate) =>
        epoch =>
        {
            Assert.InRange(epoch, candidates[0], candidates[^1]);
            return rangeRate(epoch);
        };

    private static System.Collections.Generic.List<TimeSystem.Time> RefineInside(TimeSystem.Time[] candidates,
        int minimumIndex, Func<TimeSystem.Time, double> rangeRate) =>
        ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[minimumIndex] }, candidates,
            InsideOnly(candidates, rangeRate), Tolerance);

    [Theory]
    [InlineData(30.0)]
    [InlineData(1.0e-5)] // 10 µs after the start, within the Hermite error of #363 at the default steps
    [InlineData(5.0e-7)] // within the microsecond of the bracket checked around the secant
    public void AMinimumAtTheWindowStart_ClosingThere_IsRefinedToItsZeroInside(double zeroSeconds)
    {
        var candidates = Candidates(4);

        AssertWithinATick(zeroSeconds, Assert.Single(RefineInside(candidates, 0, Rectilinear(zeroSeconds))));
    }

    [Theory]
    [InlineData(270.0)]
    [InlineData(300.0 - 1.0e-5)]
    [InlineData(300.0 - 5.0e-7)]
    public void AMinimumAtTheWindowEnd_RecedingThere_IsRefinedToItsZeroInside(double zeroSeconds)
    {
        // The secant takes the epoch 1 ms before the end, never after it
        var candidates = Candidates(4);

        AssertWithinATick(zeroSeconds, Assert.Single(RefineInside(candidates, 3, Rectilinear(zeroSeconds))));
    }

    [Theory]
    [InlineData(0, -30.0)]
    [InlineData(0, -1.0e-5)]
    [InlineData(3, 330.0)]
    [InlineData(3, 300.0 + 1.0e-5)]
    public void AMinimumAtAWindowEnd_WithItsZeroOutside_IsDropped(int minimumIndex, double zeroSeconds)
    {
        // Receding at the start, or still closing at the end: the distance grows into the window there
        var candidates = Candidates(4);

        Assert.Empty(RefineInside(candidates, minimumIndex, Rectilinear(zeroSeconds)));
    }

    [Theory]
    [InlineData(1, -1.0e-5)]
    [InlineData(1, -5.0e-7)]
    [InlineData(4, 500.0 + 1.0e-5)]
    [InlineData(4, 500.0 + 5.0e-7)]
    [InlineData(4, 500.0 + 30.0)]
    public void AnInteriorMinimum_WhoseZeroLiesJustOutsideTheWindow_IsDropped(int minimumIndex, double zeroSeconds)
    {
        // The minimum of the polynomials inside the window, that of the states just outside it: the bracket reaches the
        // window end with the wrong sign (before, the unrefined minimum was kept)
        var candidates = Candidates(6);

        Assert.Empty(RefineInside(candidates, minimumIndex, Rectilinear(zeroSeconds)));
    }

    [Theory]
    [InlineData(4, 500.0 - 1.0e-5)]
    [InlineData(4, 500.0 - 5.0e-7)]
    [InlineData(1, 1.0e-5)]
    [InlineData(1, 5.0e-7)]
    public void AnInteriorMinimum_WhoseZeroLiesJustInsideTheWindow_IsRefinedToIt(int minimumIndex, double zeroSeconds)
    {
        var candidates = Candidates(6);

        AssertWithinATick(zeroSeconds, Assert.Single(RefineInside(candidates, minimumIndex, Rectilinear(zeroSeconds))));
    }

    [Fact]
    public void AZeroRangeRateAtTheWindowStart_ThenReceding_IsTheMinimum()
    {
        // As the sampled search: g = 0 at the start and positive after
        var candidates = Candidates(4);
        Func<TimeSystem.Time, double> rangeRate = epoch => (epoch - Epoch).TotalSeconds;

        Assert.Equal(candidates[0], Assert.Single(RefineInside(candidates, 0, rangeRate)));
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
