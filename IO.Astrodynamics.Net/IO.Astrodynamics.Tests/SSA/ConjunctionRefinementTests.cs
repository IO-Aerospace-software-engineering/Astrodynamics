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

    [Theory]
    [InlineData(1, 350.0)] // two candidates past the minimum, within reach of the widenings
    [InlineData(1, 450.0)] // three candidates past it: before round 4 of the lot-6 review, the minimum was kept unrefined
    [InlineData(1, 550.0)]
    [InlineData(5, 150.0)] // three candidates before it
    [InlineData(5, 50.0)]
    public void PastTheWidenings_TheRangeRateIsFollowedToItsChangeOfSign(int minimumIndex, double zeroSeconds)
    {
        // Arrange: the zero lies two to four candidates from the minimum
        var candidates = Candidates(7);
        Assert.Equal(2, ConjunctionAssessment.MaximumBracketWidenings);

        // Act
        var tca = RefineInside(candidates, minimumIndex, Step(zeroSeconds));

        // Assert
        AssertWithinATick(zeroSeconds, Assert.Single(tca));
    }

    // A range rate that changes sign at each of the given epochs, negative before the first: the minima of the distance at
    // the first, third ... and its maxima at the second, fourth ...
    private static Func<TimeSystem.Time, double> Steps(params double[] zeroSeconds) =>
        epoch => zeroSeconds.Count(zero => (epoch - Epoch).TotalSeconds >= zero) % 2 == 0 ? -1.0 : 1.0;

    [Fact]
    public void AMaximumOfTheDistance_WithoutAMinimumInside_IsDropped()
    {
        // Receding from the start up to 520 s, closing from there to the end: a maximum, which the Hermite of a slow pass
        // may seed (round 4 of the lot-6 review: kept unrefined before, a close approach of +0.0012 m/s at t0 + 4246.66 s)
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, epoch => (epoch - Epoch).TotalSeconds < 520.0 ? 1.0 : -1.0);

        Assert.Empty(ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[5] }, candidates, rangeRate, Tolerance));
    }

    [Fact]
    public void AMaximumOfTheDistance_BetweenTwoMinima_GivesThemOnce()
    {
        // Minima at 150 s and 850 s, a maximum at 520 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(150.0, 520.0, 850.0));

        // Seeded at the maximum alone: the range rate followed on both sides reaches both minima
        var fromTheMaximum = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[5] }, candidates, rangeRate, Tolerance);
        Assert.Equal(2, fromTheMaximum.Count);
        AssertWithinATick(150.0, fromTheMaximum[0]);
        AssertWithinATick(850.0, fromTheMaximum[1]);

        // Seeded at the three: each minimum once, in order, the epochs of their own seeds
        var fromTheThree = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1], candidates[5], candidates[9] },
            candidates, rangeRate, Tolerance);
        Assert.Equal(new[] { Refine(candidates, 1, rangeRate), Refine(candidates, 9, rangeRate) }, fromTheThree);
    }

    [Fact]
    public void AFollowedZeroAtTheWindowEnd_ClosingBefore_IsTheEndItself()
    {
        // Closing up to the window end, zero exactly there: the end, not the tick before it (#624)
        var candidates = Candidates(11);
        var end = candidates[^1];
        var rangeRate = InsideOnly(candidates, epoch => epoch == end ? 0.0 : -1.0);

        Assert.Equal(end, Assert.Single(
            ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance)));
    }

    [Theory]
    [InlineData(498.4)] // 1.6 s before the seed, as on the pass with two minima per orbit (round 5 of the lot-6 review)
    [InlineData(499.9995)] // within the first read, 1 ms before the seed
    [InlineData(467.0)] // 33 s before the seed, between its sixth and seventh reads
    public void ASeedPastItsZero_RecedingAtTheCandidatesOnBothSides_IsSearchedOnItsOwnRangeRate(double zeroSeconds)
    {
        // Receding from the start up to a maximum at 420 s, closing up to the zero, receding after it: the seed, at 500 s,
        // and the candidates on both sides recede, the zero lies between the seed and the candidate before it. Before
        // round 5 of the lot-6 review, the candidates were followed to the left up to the window start, receding, and the
        // minimum was dropped
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(-1.0, 420.0, zeroSeconds));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[5] }, candidates, rangeRate, Tolerance);

        AssertWithinATick(zeroSeconds, Assert.Single(tca));
    }

    [Fact]
    public void ASeedBeforeItsZero_ClosingAtTheCandidatesAfterIt_IsSearchedForward()
    {
        // Closing at the seed (200 s) and at the candidates after it up to 400 s: a minimum 2.5 s after the seed, a
        // maximum at 205 s, the next minimum at 450 s. The seed is refined to the minimum next to it, between it and the
        // next candidate (before round 5 of the lot-6 review, the widened bracket of the candidates gave the one at 450 s)
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(202.5, 205.0, 450.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance);

        AssertWithinATick(202.5, Assert.Single(tca));
    }

    [Theory]
    [InlineData(204.85, 206.47)] // the fold of round 5 of the lot-6 review: 4.85 s from the seed, positive over 1.62 s
    [InlineData(204.10, 205.10)] // one second wide, starting just past the read at 4.096 s
    [InlineData(250.0241, 251.0241)] // one second wide, deep in the reads 1 s apart
    [InlineData(299.5, 300.5)] // nearer the neighbouring candidate than the spacing: the last read, on the candidate
    public void ASeedClosing_ItsZeroInANarrowPositiveInterval_IsFoundForward(double zeroSeconds, double maximumSeconds)
    {
        // Closing at the seed (200 s) and at the candidates after it: positive only between the minimum and the maximum,
        // then closing up to a minimum at 450 s. Reads at distances doubling from the seed (4.096 and 8.192 s, ...) stepped
        // over the narrow interval, and the widened bracket then gave the minimum at 450 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(zeroSeconds, maximumSeconds, 450.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance);

        AssertWithinATick(zeroSeconds, Assert.Single(tca));
    }

    [Fact]
    public void ASeedReceding_ItsZeroAfterANarrowNegativeInterval_IsFoundBackward()
    {
        // Receding at the seed (300 s): a minimum at 150 s, a maximum at 293.53 s, the minimum at 295.15 s. Reads at
        // 4.096 and 8.192 s before the seed both receding stepped over the 1.62 s where it closes
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(150.0, 293.53, 295.15));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[3] }, candidates, rangeRate, Tolerance);

        AssertWithinATick(295.15, Assert.Single(tca));
    }

    [Fact]
    public void TheSearchFromTheSeed_ReadsAtMostOneSecondApart_ElevenPlusOnePerSecond()
    {
        // Closing from the seed (200 s) to the neighbouring candidate, 100 s away, and past it: the search reads at 1 ms to
        // 1.024 s from the seed (11 reads), then every second up to the candidate, 11 + ceil(100 - 1.024) = 110 reads, the
        // last on the candidate, against 18 when the distances doubled all the way
        var candidates = Candidates(11);
        var reads = new System.Collections.Generic.SortedSet<double>();
        var rangeRate = InsideOnly(candidates, epoch =>
        {
            double t = (epoch - Epoch).TotalSeconds;
            reads.Add(t);
            return Steps(450.0)(epoch);
        });

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance);

        AssertWithinATick(450.0, Assert.Single(tca));
        var search = reads.Where(t => t > 200.0 && t <= 300.0).ToArray();
        Assert.Equal(110, search.Length);
        Assert.Equal(300.0, search[^1]);
        Assert.True(search.Zip(search.Skip(1), (a, b) => b - a).Max() <= ConjunctionAssessment.SeedSearchMaximumSpacingSeconds + 1.0e-9);
        Assert.Equal(1.0, ConjunctionAssessment.SeedSearchMaximumSpacingSeconds);
    }

    [Fact]
    public void TwoSeedsReachingTwoZerosInOnePairOfCandidates_GiveOneMinimum()
    {
        // A range rate changing sign three times between the candidates of 200 s and 300 s (a minimum at 240 s, a
        // maximum at 250 s and a minimum at 260 s, a range rate at the noise level): the seed at 200 s, closing, reaches
        // the first zero forward, the seed at 300 s, receding, the last one backward. They are one minimum, the first
        var candidates = Candidates(6);
        var rangeRate = InsideOnly(candidates, Steps(240.0, 250.0, 260.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2], candidates[3] }, candidates, rangeRate,
            Tolerance);

        AssertWithinATick(240.0, Assert.Single(tca));
        AssertWithinATick(260.0, Assert.Single(
            ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[3] }, candidates, rangeRate, Tolerance)));
    }

    [Fact]
    public void ARangeRateExactlyZeroAtTheSeed_TheSeedIsTheZero()
    {
        // Zero over [300, 300.5] s, so that the secant from the seed (300 s) has no slope
        var candidates = Candidates(7);
        var rangeRate = InsideOnly(candidates, epoch =>
        {
            double t = (epoch - Epoch).TotalSeconds;
            return t < 300.0 ? -1.0 : t <= 300.5 ? 0.0 : 1.0;
        });

        Assert.Equal(candidates[3], Assert.Single(
            ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[3] }, candidates, rangeRate, Tolerance)));
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
    public void AMinimumThatIsNotACandidate_IsDropped()
    {
        // No bracket of candidates to refine it in: not a minimum on the range rate (kept unrefined before round 4 of the
        // lot-6 review)
        var candidates = Candidates(6);
        var minimum = Epoch.AddSeconds(150.0);

        Assert.Empty(ConjunctionAssessment.RefineOnRangeRate(new[] { minimum }, candidates, Rectilinear(170.0), Tolerance));
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

    [Theory]
    [InlineData(3)] // the window end itself
    [InlineData(2)] // an interior minimum of the polynomials
    public void AZeroRangeRateAtTheWindowEnd_ClosingBefore_IsTheEndItself(int minimumIndex)
    {
        // g = 0 exactly at the end and negative before: the zero is the end, not the tick before it (the bisection on
        // [left, end] ends on [end − 1 tick, end] and returns its lower bound, #624)
        var candidates = Candidates(4);
        Func<TimeSystem.Time, double> rangeRate = epoch => (epoch - Epoch).TotalSeconds - 300.0;

        Assert.Equal(candidates[^1], Assert.Single(RefineInside(candidates, minimumIndex, rangeRate)));
    }

    [Fact]
    public void AZeroRangeRateAtTheWindowStart_ThenReceding_IsTheMinimum_FromAnInteriorMinimum()
    {
        // The minimum of the polynomials at the next candidate, g = 0 exactly at the start and positive after: the start,
        // as from a minimum at the start (it was dropped as receding at the start)
        var candidates = Candidates(4);
        Func<TimeSystem.Time, double> rangeRate = epoch => (epoch - Epoch).TotalSeconds;

        Assert.Equal(candidates[0], Assert.Single(RefineInside(candidates, 1, rangeRate)));
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
