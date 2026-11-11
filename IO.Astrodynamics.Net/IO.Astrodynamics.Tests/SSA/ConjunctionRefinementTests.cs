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

    // The spacing of the reads over the window: MaximumEventSearchStep with ReadRangeRateOverWindow, none without it (the
    // default, decision 54 of #457)
    private static double? Reads(bool overTheWindow) => overTheWindow ? ReadSpacing : null;

    // The contract of the refinement, with or without the reads over the window: every epoch returned is within a tick of a
    // zero of the reference (a change of the range rate from negative to positive), in order, no two at the same zero.
    // Without the reads, a minimum that no seed reaches may be missing (#628), so this alone asserts no completeness
    private static void AssertMinimaOf(double[] referenceSeconds, System.Collections.Generic.IReadOnlyList<TimeSystem.Time> tca)
    {
        var zeros = tca.Select(epoch => referenceSeconds.Single(zero =>
            System.Math.Abs((epoch - Epoch).TotalSeconds - zero) <= 1.0e-7 * (1.0 + 1.0e-6))).ToArray();
        Assert.Equal(zeros.OrderBy(zero => zero).Distinct(), zeros);
        Assert.Equal(tca.OrderBy(epoch => epoch), tca);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMaximumOfTheDistance_BetweenTwoMinima_GivesThemOnce(bool overTheWindow)
    {
        // Minima at 150 s and 850 s, a maximum at 520 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(150.0, 520.0, 850.0));

        // Seeded at the maximum alone: the range rate followed on both sides reaches both minima
        var fromTheMaximum = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[5] }, candidates, rangeRate, Tolerance,
            Reads(overTheWindow));
        Assert.Equal(2, fromTheMaximum.Count);
        AssertWithinATick(150.0, fromTheMaximum[0]);
        AssertWithinATick(850.0, fromTheMaximum[1]);

        // Seeded at the three: each minimum once, in order, the epochs of their own seeds
        var fromTheThree = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1], candidates[5], candidates[9] },
            candidates, rangeRate, Tolerance, Reads(overTheWindow));
        Assert.Equal(new[] { Refine(candidates, 1, rangeRate), Refine(candidates, 9, rangeRate) }, fromTheThree);

        // Seeded at one of them: its own minimum; the other one, which no seed reaches, with the reads over the window
        // only (#628: the range rate closes at 800 s and recedes at 900 s); without them, the default, it is not guaranteed
        foreach (int seed in new[] { 1, 9 })
        {
            var fromOne = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[seed] }, candidates, rangeRate, Tolerance,
                Reads(overTheWindow));
            AssertMinimaOf(new[] { 150.0, 850.0 }, fromOne);
            Assert.Contains(Refine(candidates, seed, rangeRate), fromOne);
            if (overTheWindow)
            {
                Assert.Equal(fromTheThree, fromOne);
            }
        }
    }

    [Fact]
    public void AFollowedZeroAtTheWindowEnd_ClosingBefore_IsTheEndItself()
    {
        // Closing up to the window end, zero exactly there: the end, not the tick before it (before #624, the bisection
        // returned the lower tick of its last bracket)
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASeedBeforeItsZero_ClosingAtTheCandidatesAfterIt_IsSearchedForward(bool overTheWindow)
    {
        // Closing at the seed (200 s) and at the candidates after it up to 400 s: a minimum 2.5 s after the seed, a
        // maximum at 205 s, the next minimum at 450 s. The seed is refined to the minimum next to it, between it and the
        // next candidate (before round 5 of the lot-6 review, the widened bracket of the candidates gave the one at 450 s).
        // The one at 450 s, which no seed reaches, is read over the window with ReadRangeRateOverWindow (#628); without
        // it, the default, it is not guaranteed
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(202.5, 205.0, 450.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance,
            Reads(overTheWindow));

        AssertMinimaOf(new[] { 202.5, 450.0 }, tca);
        AssertWithinATick(202.5, tca[0]);
        if (overTheWindow)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(450.0, tca[1]);
        }
    }

    [Theory]
    [InlineData(204.85, 206.47, false)] // the fold of round 5 of the lot-6 review: 4.85 s from the seed, positive over 1.62 s
    [InlineData(204.10, 205.10, false)] // one second wide, starting just past the read at 4.096 s
    [InlineData(250.0241, 251.0241, false)] // one second wide, deep in the reads 1 s apart
    [InlineData(299.5, 300.5, false)] // nearer the neighbouring candidate than the spacing: the last read, on the candidate
    [InlineData(204.85, 206.47, true)]
    [InlineData(204.10, 205.10, true)]
    [InlineData(250.0241, 251.0241, true)]
    [InlineData(299.5, 300.5, true)]
    public void ASeedClosing_ItsZeroInANarrowPositiveInterval_IsFoundForward(double zeroSeconds, double maximumSeconds,
        bool overTheWindow)
    {
        // Closing at the seed (200 s) and at the candidates after it: positive only between the minimum and the maximum,
        // then closing up to a minimum at 450 s. Reads at distances doubling from the seed (4.096 and 8.192 s, ...) stepped
        // over the narrow interval, and the widened bracket then gave the minimum at 450 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(zeroSeconds, maximumSeconds, 450.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[2] }, candidates, rangeRate, Tolerance,
            Reads(overTheWindow));

        // The minimum at 450 s, which no seed reaches, read over the window with ReadRangeRateOverWindow (#628), not
        // guaranteed without it
        AssertMinimaOf(new[] { zeroSeconds, 450.0 }, tca);
        AssertWithinATick(zeroSeconds, tca[0]);
        if (overTheWindow)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(450.0, tca[1]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASeedReceding_ItsZeroAfterANarrowNegativeInterval_IsFoundBackward(bool overTheWindow)
    {
        // Receding at the seed (300 s): a minimum at 150 s, a maximum at 293.53 s, the minimum at 295.15 s. Reads at
        // 4.096 and 8.192 s before the seed both receding stepped over the 1.62 s where it closes
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(150.0, 293.53, 295.15));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[3] }, candidates, rangeRate, Tolerance,
            Reads(overTheWindow));

        // The minimum at 150 s, which no seed reaches, read over the window with ReadRangeRateOverWindow (#628), not
        // guaranteed without it
        AssertMinimaOf(new[] { 150.0, 295.15 }, tca);
        AssertWithinATick(295.15, tca[^1]);
        if (overTheWindow)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(150.0, tca[0]);
        }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMinimumThatIsNotACandidate_IsDropped(bool overTheWindow)
    {
        // No bracket of candidates to refine it in: not a minimum on the range rate (kept unrefined before round 4 of the
        // lot-6 review). The seed adds nothing: the result is that of no seed, empty without the reads over the window,
        // and with them the zero at 170 s that they give without a seed (#628)
        var candidates = Candidates(6);
        var minimum = Epoch.AddSeconds(150.0);

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { minimum }, candidates, Rectilinear(170.0), Tolerance,
            Reads(overTheWindow));

        Assert.Equal(ConjunctionAssessment.RefineOnRangeRate(Array.Empty<TimeSystem.Time>(), candidates, Rectilinear(170.0),
            Tolerance, Reads(overTheWindow)), tca);
        if (overTheWindow)
        {
            AssertWithinATick(170.0, Assert.Single(tca));
        }
        else
        {
            Assert.Empty(tca);
        }
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
        // g = 0 exactly at the end and negative before: the zero is the end, not the tick before it (before #624, the
        // bisection on [left, end] ended on [end − 1 tick, end] and returned its lower bound)
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
    /// the refinement falls back to the candidates and still ends on the epoch of the bisection, the tick nearer the zero
    /// (the three shapes are odd about it, so the smaller |g| of the last bracket is the nearer tick).
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

        // Assert: the epoch of the bisection between the candidates on the same values near the zero, the nearest tick
        Assert.Equal(Refine(candidates, 2, FlatAwayFromTheZero(rangeRate, zeroSeconds)), tca);
        Assert.Equal(NearestTick(zeroSeconds), tca);
    }

    // The range rate within ten microseconds of a zero at zeroSeconds, and its sign alone (±1) farther: no secant from a
    // minimum is defined, so the refinement bisects, and its last bracket, one tick wide, holds the values of rangeRate
    private static Func<TimeSystem.Time, double> FlatAwayFromTheZero(Func<TimeSystem.Time, double> rangeRate,
        double zeroSeconds) =>
        epoch => System.Math.Abs((epoch - Epoch).TotalSeconds - zeroSeconds) < 1.0e-5
            ? rangeRate(epoch)
            : System.Math.Sign(rangeRate(epoch));

    // The tick nearest to zeroSeconds from the epoch, for a zero that is not half-way between two ticks
    private static TimeSystem.Time NearestTick(double zeroSeconds) =>
        Epoch.AddTicks((long)System.Math.Round(zeroSeconds * TimeSpan.TicksPerSecond));

    // ---- The tick returned (#624): the bisection ends on a bracket one tick wide and returns the tick nearer the zero of
    // the chord of the range rate, the end where |g| is smaller, the lower tick on a tie. Before, it always returned the
    // lower tick: up to one tick before the zero, a zero exactly on a tick one tick early ----

    // The slope of a linear range rate at LEO relative speeds, (7.6 km/s)², per tick: 5.8 m²/s per 100 ns
    private const double SlopePerTick = 5.8;

    // A linear range rate whose zero lies fraction of a tick after the tick zeroTicks from the epoch, evaluated in exact
    // tick arithmetic: g = s ((n − k) − f) at the tick n, so the zero is known exactly and |g| at the two ticks around it
    // compares as their distances to it
    private static Func<TimeSystem.Time, double> LinearInTicks(long zeroTicks, double fraction) =>
        epoch => SlopePerTick * ((double)((epoch - Epoch).Ticks - zeroTicks) - fraction);

    // The nearest tick to the zero, the lower one on a tie (f = 0.5 exactly)
    private static TimeSystem.Time NearestTick(long zeroTicks, double fraction) =>
        Epoch.AddTicks(zeroTicks + (fraction > 0.5 ? 1 : 0));

    public static TheoryData<string, double> RoutesAndFractions()
    {
        var data = new TheoryData<string, double>();
        foreach (var route in new[] { "secants", "seed forward", "seed backward", "widened", "followed" })
        {
            foreach (double fraction in new[] { 0.0, 1.0e-6, 0.25, 0.5 - 1.0e-6, 0.5, 0.5 + 1.0e-6, 0.75, 1.0 - 1.0e-6 })
            {
                data.Add(route, fraction);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RoutesAndFractions))]
    public void ALinearRangeRate_IsRefinedToTheNearestTick_OnEveryRoute(string route, double fraction)
    {
        // Arrange: candidates every 100 s; the zero f ticks after 250 s, or after 450 s for the followed route. Linear,
        // the secants refine it; flat (±1) farther than ten microseconds from the zero, no secant is defined and the
        // range rate is searched from the seed (forward from 200 s, backward from 300 s), between the widened candidates
        // (seed at 100 s, the zero past its right neighbour) or followed over the candidates (seed at 100 s, the zero
        // three candidates away)
        var candidates = Candidates(7);
        long zeroTicks = (route == "followed" ? 450L : 250L) * TimeSpan.TicksPerSecond;
        var linear = LinearInTicks(zeroTicks, fraction);
        double zeroSeconds = (zeroTicks + fraction) / TimeSpan.TicksPerSecond;
        var rangeRate = route == "secants" ? linear : FlatAwayFromTheZero(linear, zeroSeconds);
        int minimumIndex = route switch
        {
            "secants" or "seed forward" => 2,
            "seed backward" => 3,
            _ => 1
        };

        // Act
        var tca = RefineInside(candidates, minimumIndex, rangeRate);

        // Assert: the nearest tick, half a tick from the zero at most; a zero exactly on a tick is that tick
        Assert.Equal(NearestTick(zeroTicks, fraction), Assert.Single(tca));
    }

    [Theory]
    [InlineData(0.8, 3)] // the zero 0.2 tick before the end: the end, the nearer tick
    [InlineData(0.6, 3)] // 0.4 tick before it: the end
    [InlineData(0.6, 2)] // the same from an interior minimum of the polynomials
    [InlineData(0.4, 3)] // 0.6 tick before it: the tick before the end
    [InlineData(0.2, 2)]
    public void AZeroInTheLastTickOfTheCandidates_IsTheNearerTick_TheEndIncluded(double fraction, int minimumIndex)
    {
        // The zero f ticks after the tick before the last candidate (300 s): RefineOnRangeRate returns the nearer tick,
        // the last candidate itself when the zero lies within half a tick of it. The range rate is positive there, so the
        // dense search takes it as the tick before the end (FindDenseCloseApproaches, ConjunctionAssessmentTests)
        var candidates = Candidates(4);
        long zeroTicks = 300L * TimeSpan.TicksPerSecond - 1;

        var tca = RefineInside(candidates, minimumIndex, LinearInTicks(zeroTicks, fraction));

        Assert.Equal(NearestTick(zeroTicks, fraction), Assert.Single(tca));
    }

    [Theory]
    [InlineData(103.7)]
    [InlineData(187.12345678)]
    [InlineData(213.73456789)]
    public void TheSecantsAndTheBisectionBetweenTheCandidates_EndOnTheSameEpoch(double zeroSeconds)
    {
        // Arrange: two range rates with the same values within ten microseconds of the zero and the same sign on every
        // epoch; the first one is refined by the secants, the second one, flat farther from the zero, by the bisection
        // between the candidates. Since #624 the epoch returned depends on the values of the last bracket, one tick wide,
        // not only on their signs (a range rate flat up to the zero, ±1, gave the same epoch before)
        var candidates = Candidates(6);
        var rectilinear = Rectilinear(zeroSeconds);
        var flat = FlatAwayFromTheZero(rectilinear, zeroSeconds);
        bool sameValues = Enumerable.Range(-50, 101)
            .Select(i => Epoch.AddSeconds(zeroSeconds).AddTicks(i))
            .All(epoch => rectilinear(epoch) == flat(epoch));
        Assert.True(sameValues);

        // Act
        var bySecants = Refine(candidates, 1, rectilinear);
        var byBisection = Refine(candidates, 1, flat);

        // Assert: the same epoch, the tick nearest the zero
        Assert.Equal(byBisection, bySecants);
        Assert.Equal(NearestTick(zeroSeconds), bySecants);
    }
    // ---- #628: the minima that no seed reaches. With ReadRangeRateOverWindow (opt-in, decision 54 of #457), the range
    // rate is read at every candidate and, between two candidates farther apart than the spacing of the reads
    // (MaximumEventSearchStep, 60 s by default, on the dense path), at epochs evenly spaced between them; each change from
    // negative to positive between two reads that holds no zero yet is bisected. Without it, the default, the seeds alone
    // are refined, as before #628 ----

    // The spacing of the reads of the dense path, MaximumEventSearchStep by default
    private const double ReadSpacing = 60.0;

    [Theory]
    [InlineData("A", false, false)]
    [InlineData("A", false, true)]
    [InlineData("A", true, false)]
    [InlineData("A", true, true)]
    [InlineData("B", false, false)]
    [InlineData("B", false, true)]
    [InlineData("B", true, false)]
    [InlineData("B", true, true)]
    public void TheReadsOverTheWindow_FindTheMinimaNoSeedReaches_OnlyWhenAskedFor(string form, bool overTheWindow,
        bool windowEndsWithoutInteriorMinimum)
    {
        // The dense search after its seeds (CloseApproachesOnRangeRate), AnalyzeAll (no window end) and Analyze (window ends
        // without a minimum inside), on the two forms of #628. Form A: minima at 150 s and 850 s, maxima at 520 s and
        // 930 s, no seed, the range rate closing at both window ends, so that the bisection over the window does not apply.
        // Form B: one step of 600 s holding minima at 117.45 s and 326.28 s and a maximum at 250 s, the seed at 302.10 s
        // (CW600's step [1200, 1800] s). The reference: the zeros of the range rate from negative to positive. With the
        // reads, every one; without them (the documented default, decision 54 of #457), the seed's alone, and for Analyze
        // the window end where the distance decreases into the window when no seed gives one
        bool formA = form == "A";
        var candidates = formA
            ? Candidates(11)
            : new[] { Epoch, Epoch.AddSeconds(302.10), Epoch.AddSeconds(600.0) };
        var rangeRate = InsideOnly(candidates, formA ? Steps(150.0, 520.0, 850.0, 930.0) : Steps(117.45, 250.0, 326.28));
        var seeds = formA ? Array.Empty<TimeSystem.Time>() : new[] { candidates[1] };
        double[] reference = formA ? new[] { 150.0, 850.0 } : new[] { 117.45, 326.28 };

        var tca = ConjunctionAssessment.CloseApproachesOnRangeRate(seeds, candidates, rangeRate,
            epoch => (epoch - Epoch).TotalSeconds, Tolerance, Reads(overTheWindow), windowEndsWithoutInteriorMinimum);

        if (overTheWindow)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(reference[0], tca[0]);
            AssertWithinATick(reference[1], tca[1]);
        }
        else if (formA)
        {
            // No minimum inside: nothing for AnalyzeAll, for Analyze the end, closing there (the start recedes into the
            // window nowhere: closing there too)
            Assert.Equal(windowEndsWithoutInteriorMinimum ? new[] { candidates[^1] } : Array.Empty<TimeSystem.Time>(), tca);
        }
        else
        {
            AssertWithinATick(reference[1], Assert.Single(tca));
        }
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 9 })]
    [InlineData(new[] { 1 })]
    public void AMinimumWithoutASeed_ClosingAtOneCandidateAndRecedingAtTheNext_IsRead(int[] seeds)
    {
        // Minima at 150 s and 850 s, maxima at 520 s and 930 s: the range rate closes at both window ends, so that the
        // bisection over the window without a seed does not apply either (form A of #628: before, a minimum without a seed
        // near it was not listed)
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(150.0, 520.0, 850.0, 930.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(seeds.Select(i => candidates[i]).ToArray(), candidates, rangeRate,
            Tolerance, ReadSpacing);

        Assert.Equal(2, tca.Count);
        AssertWithinATick(150.0, tca[0]);
        AssertWithinATick(850.0, tca[1]);
    }

    [Fact]
    public void TwoMinimaBetweenTwoCandidates_AreSeparatedByTheReads()
    {
        // One step of 600 s holding a minimum at 117.45 s, a maximum at 250 s and a minimum at 326.28 s, the Hermite
        // seeding one minimum at 302.10 s, where the range rate still closes, as at the start (form B of #628, the 600 s
        // steps of the pass with two minima per orbit, y0 = 1490 m, at t0 + 1200 s): the seed reaches the second minimum;
        // the first one lies between two candidates where the range rate closes, and the reads 50.35 s apart between them
        // find it. Read at the candidates alone (an infinite spacing), it is lost
        var candidates = new[] { Epoch, Epoch.AddSeconds(302.10), Epoch.AddSeconds(600.0) };
        var reads = new System.Collections.Generic.SortedSet<double>();
        var rangeRate = InsideOnly(candidates, epoch =>
        {
            reads.Add((epoch - Epoch).TotalSeconds);
            return Steps(117.45, 250.0, 326.28)(epoch);
        });

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1] }, candidates, rangeRate, Tolerance,
            ReadSpacing);

        Assert.Equal(2, tca.Count);
        AssertWithinATick(117.45, tca[0]);
        AssertWithinATick(326.28, tca[1]);
        var all = reads.ToArray();
        Assert.True(all.Zip(all.Skip(1), (a, b) => b - a).Max() <= ReadSpacing);
        AssertWithinATick(326.28, Assert.Single(
            ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1] }, candidates, rangeRate, Tolerance,
                double.PositiveInfinity)));
    }

    [Fact]
    public void TheReads_AreEvenlySpacedInTicks_NoGapWiderThanTheSpacingByMoreThanATick()
    {
        // 100 s, 60 s, 61 s, 300 s and one tick, 300 s and two ticks, 60 s and one tick and 60 s and two ticks between the
        // candidates: two, one, two, five, six, one and two intervals. A gap one tick wider than a multiple of the spacing
        // is not subdivided once more: about the Earth a step of 60 TT seconds spans 60 s and one tick of its TDB labels
        // when TDB - TT increases, which a strict comparison read at its middle (review round 3 of #628). The reference:
        // the fewest intervals of at most the spacing and one tick, floor(gap k / n) ticks after the left candidate
        var gaps = new[] { 1000000000L, 600000000L, 610000000L, 3000000001L, 3000000002L, 600000001L, 600000002L };
        var candidates = new TimeSystem.Time[gaps.Length + 1];
        candidates[0] = Epoch;
        for (int index = 0; index < gaps.Length; index++)
        {
            candidates[index + 1] = candidates[index].AddTicks(gaps[index]);
        }

        var reads = new System.Collections.Generic.SortedSet<long>();
        Func<TimeSystem.Time, double> rangeRate = epoch =>
        {
            reads.Add((epoch - Epoch).Ticks);
            return -1.0;
        };

        Assert.Empty(ConjunctionAssessment.RefineOnRangeRate(Array.Empty<TimeSystem.Time>(), candidates, rangeRate, Tolerance,
            ReadSpacing));

        var intervals = new[] { 2, 1, 2, 5, 6, 1, 2 };
        var expected = new System.Collections.Generic.List<long> { 0 };
        long left = 0;
        for (int index = 0; index < gaps.Length; index++)
        {
            for (int k = 1; k <= intervals[index]; k++)
            {
                expected.Add(left + gaps[index] * k / intervals[index]);
            }

            left += gaps[index];
        }

        Assert.Equal(expected, reads.ToArray());
        long widest = expected.Zip(expected.Skip(1), (a, b) => b - a).Max();
        Assert.Equal(600000001L, widest);
    }

    [Theory]
    [InlineData(420.0, 5.0)]
    [InlineData(500.0, 1.5)]
    [InlineData(700.0, 20.0)]
    public void TheSearchFromTheSeed_ReachesItsNeighbouringCandidate_WhateverTheSpacingOfTheReads(double foldSeconds,
        double widthSeconds)
    {
        // Candidates at 0, 300, 790 and 900 s, the seeds at 300 and 790 s, both closing; positive over a fold of 1.5 to 20 s
        // more than 60 s after the first seed, then closing up to the minimum at 800 s. The reads over the window, 54.44 s
        // apart between 300 and 790 s, all close around the fold; the reads 1 s apart from the seed at 300 s reach it, up to
        // the neighbouring candidate (round 1 of the review of #628: the search from a seed stopped 60 s from it, and the
        // minimum of the fold was lost where main b237eec2 found it, at fixed 600 s steps a positive interval of 2.3 to 27 s
        // 61 to 90 s past the seed). The reference: the zeros of the range rate from negative to positive
        var candidates = new[] { 0.0, 300.0, 790.0, 900.0 }.Select(t => Epoch.AddSeconds(t)).ToArray();
        var rangeRate = InsideOnly(candidates, Steps(foldSeconds, foldSeconds + widthSeconds, 800.0));

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1], candidates[2] }, candidates, rangeRate,
            Tolerance, ReadSpacing);

        Assert.Equal(2, tca.Count);
        AssertWithinATick(foldSeconds, tca[0]);
        AssertWithinATick(800.0, tca[1]);
    }

    [Fact]
    public void TheSearchFromTheSeed_Backward_ReachesItsNeighbouringCandidate_WhateverTheSpacingOfTheReads()
    {
        // Receding at the seed (500 s) back to 432 s, closing over [430, 432) s, receding from 150 s to 430 s: the search
        // reads backward 1 s apart to the neighbouring candidate (400 s) and finds the minimum at 432 s, 68 s from the seed,
        // which the reads at 400 and 450 s, both receding, step over; the minimum at 150 s is found by the candidates. The
        // reference: the zeros of the range rate from negative to positive
        var candidates = Candidates(11);
        var reads = new System.Collections.Generic.SortedSet<double>();
        var rangeRate = InsideOnly(candidates, epoch =>
        {
            reads.Add((epoch - Epoch).TotalSeconds);
            return Steps(150.0, 430.0, 432.0)(epoch);
        });

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[5] }, candidates, rangeRate, Tolerance,
            ReadSpacing);

        Assert.Equal(2, tca.Count);
        AssertWithinATick(150.0, tca[0]);
        AssertWithinATick(432.0, tca[1]);
        Assert.Contains(reads, t => t < 440.0 && t > 431.0);
    }

    [Theory]
    [InlineData(1.0e-9)]
    [InlineData(1.0e-3)]
    public void AZeroOfTheReadsAtTheWindowEnd_ClosingBefore_IsTheEndItself(double tolerance)
    {
        // No seed, closing up to the window end, zero exactly there: the reads give the end itself, whose range rate is
        // exactly zero, even with a tolerance of a tick or more, where the bisection of the last pair of reads ends on the
        // middle of its last bracket, inside the window. This is RefineOnRangeRate only: the dense search drops that end
        // (Inside), then its fallback bisects the whole window again, which with a tolerance coarser than a tick ends on
        // the middle of its last bracket and lists it, the range rate negative there (as before #628; out of its scope)
        var candidates = Candidates(11);
        var end = candidates[^1];
        var rangeRate = InsideOnly(candidates, epoch => epoch == end ? 0.0 : -1.0);

        Assert.Equal(end, Assert.Single(ConjunctionAssessment.RefineOnRangeRate(Array.Empty<TimeSystem.Time>(), candidates,
            rangeRate, tolerance, ReadSpacing)));
    }

    [Theory]
    [InlineData("steps")]
    [InlineData("cubic")]
    public void AMinimumInTheLastPairOfReads_ZeroAtTheWindowEnd_IsBisected(string shape)
    {
        // Closing at the read before the end (950 s), receding over (970, 990) s, closing again and exactly zero at the
        // window end (1000 s): the minimum at 970 s lies in the last pair of reads, whose bisection finds it (round 1 of the
        // review of #628: the end was returned without bisecting the pair). The reference: the zero of the range rate from
        // negative to positive inside the window, 970 s
        var candidates = Candidates(11);
        var end = candidates[^1];
        Func<TimeSystem.Time, double> shapeRate = shape == "steps"
            ? epoch => epoch == end ? 0.0 : Steps(970.0, 990.0)(epoch)
            : epoch =>
            {
                double s = (epoch - Epoch).TotalSeconds;
                return (s - 970.0) * (s - 990.0) * (s - 1000.0);
            };
        var rangeRate = InsideOnly(candidates, shapeRate);

        var tca = ConjunctionAssessment.RefineOnRangeRate(Array.Empty<TimeSystem.Time>(), candidates, rangeRate, Tolerance,
            ReadSpacing);

        AssertWithinATick(970.0, Assert.Single(tca));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMinimumInTheLastPairOfReads_ThatItsBisectionStepsOver_IsFoundByTheBisectionOverTheWindow(
        bool windowEndsWithoutInteriorMinimum)
    {
        // No seed, closing up to the window end, exactly zero there, but receding over [960, 970) s: a minimum at 960 s in
        // the last pair of reads (950 and 1000 s, 50 s apart, both closing), narrower than the reads. That pair's bisection
        // (middles 975, 987.5 ... s) ends on the end itself, which the dense search drops; its fallback then bisects the
        // window (middles 500, 750, 875, 937.5, 968.75 ... s), whose bracket reaches the receding interval, and lists the
        // minimum (review round 3 of #628: no test reached this branch). The reference: the zero of the range rate from
        // negative to positive inside the window, 960 s
        var candidates = Candidates(11);
        var end = candidates[^1];
        var rangeRate = InsideOnly(candidates, epoch => epoch == end ? 0.0 : Steps(960.0, 970.0)(epoch));

        var tca = ConjunctionAssessment.CloseApproachesOnRangeRate(Array.Empty<TimeSystem.Time>(), candidates, rangeRate,
            _ => 1.0, Tolerance, ReadSpacing, windowEndsWithoutInteriorMinimum);

        AssertWithinATick(960.0, Assert.Single(tca));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANarrowFoldBetweenTwoReads_IsFoundFromASeed(bool seeded)
    {
        // Positive over [230.2, 231.2] s only, then closing up to the minimum at 450 s: the reads at 200 s and 250 s both
        // close. From the seed at 200 s, whose reads are 1 s apart, the minimum at 230.2 s is found; without a seed before
        // it, it is not guaranteed to be found (the documented limit: an interval narrower than the spacing of the reads),
        // and whatever is listed is a zero of the reference. The reference: the zeros of the range rate from negative to
        // positive, 230.2 s and 450 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(230.2, 231.2, 450.0));
        var seeds = seeded ? new[] { candidates[2] } : Array.Empty<TimeSystem.Time>();

        var tca = ConjunctionAssessment.RefineOnRangeRate(seeds, candidates, rangeRate, Tolerance, ReadSpacing);

        var reference = new[] { 230.2, 450.0 };
        Assert.All(tca, epoch => Assert.Contains(reference,
            zero => System.Math.Abs((epoch - Epoch.AddSeconds(zero)).TotalSeconds) <= 1.0e-7));
        Assert.Equal(tca.Count, tca.Distinct().Count());
        if (seeded)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(230.2, tca[0]);
        }

        AssertWithinATick(450.0, tca[^1]);
    }

    [Theory]
    [InlineData(true, 60.0)]
    [InlineData(false, 60.0)]
    [InlineData(true, 4.0)]
    [InlineData(false, 4.0)]
    [InlineData(true, 1.0)]
    [InlineData(false, 1.0)]
    public void TwoMinimaInOnePairOfReads_AreSeparatedByReadsNarrowerThanTheIntervalOfEachSign(bool seeded,
        double spacing)
    {
        // Minima at 210 s and 230 s, the range rate positive over [210, 215) s, negative over [215, 230) s (15 s) and
        // positive over [230, 300) s: at a spacing of 60 s the reads at 200 s and 250 s hold both, and the pair holding the
        // zero the seed at 200 s finds is not bisected again, so the second minimum is not guaranteed (the documented
        // limit: an interval of either sign narrower than the spacing of the reads; review of #628, round 2). Reads
        // narrower than every interval of each sign (4 s and 1 s) separate them, with a seed or without. The reference:
        // the zeros of the range rate from negative to positive, 210 s and 230 s
        var candidates = Candidates(11);
        var rangeRate = InsideOnly(candidates, Steps(210.0, 215.0, 230.0, 300.0));
        var seeds = seeded ? new[] { candidates[2] } : Array.Empty<TimeSystem.Time>();

        var tca = ConjunctionAssessment.RefineOnRangeRate(seeds, candidates, rangeRate, Tolerance, spacing);

        var reference = new[] { 210.0, 230.0 };
        Assert.All(tca, epoch => Assert.Contains(reference,
            zero => System.Math.Abs((epoch - Epoch.AddSeconds(zero)).TotalSeconds) <= 1.0e-7));
        Assert.Equal(tca.Count, tca.Distinct().Count());
        Assert.NotEmpty(tca);
        if (spacing < 5.0)
        {
            Assert.Equal(2, tca.Count);
            AssertWithinATick(210.0, tca[0]);
            AssertWithinATick(230.0, tca[1]);
        }
        else if (seeded)
        {
            AssertWithinATick(210.0, tca[0]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachEpoch_IsReadOnce(bool overTheWindow)
    {
        // The reads of the candidates come back to those the seeds have read, and the seeds come back to epochs they have
        // read: each epoch is read once, with the reads over the window or without them
        var candidates = Candidates(11);
        var counts = new System.Collections.Generic.Dictionary<TimeSystem.Time, int>();
        var rangeRate = InsideOnly(candidates, epoch =>
        {
            counts[epoch] = counts.TryGetValue(epoch, out int count) ? count + 1 : 1;
            return Steps(150.0, 520.0, 850.0)(epoch);
        });

        var tca = ConjunctionAssessment.RefineOnRangeRate(new[] { candidates[1], candidates[5], candidates[9] }, candidates,
            rangeRate, Tolerance, Reads(overTheWindow));

        Assert.Equal(2, tca.Count);
        Assert.All(counts.Values, count => Assert.Equal(1, count));
        if (overTheWindow)
        {
            Assert.All(candidates, candidate => Assert.True(counts.ContainsKey(candidate)));
        }
    }
}
