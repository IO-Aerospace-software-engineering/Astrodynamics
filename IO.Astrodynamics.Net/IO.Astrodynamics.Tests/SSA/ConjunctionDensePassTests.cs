using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The CSPICE references and the assertions of the dense search on the slow passes and the passes with two minima per orbit
/// (round 4 of the lot-6 review to #628), shared by <see cref="ConjunctionKernelAndWindowTests"/> and the five classes of
/// those passes, each of which owns some of their fixtures and declares the theories of their rows. No test of its own.
/// </summary>
/// <remarks>
/// <para>
/// xUnit runs the tests of one class one after the other, and its classes in parallel. These rows were all in
/// <see cref="ConjunctionKernelAndWindowTests"/>, which ran from 211 to 795 s of the Linux x64 job of run 43256478324, from
/// 59 to 508 s of the Windows x64 job and from 115 to 466 s of the macOS arm64 job, alone on one of the four threads from
/// 311 s on Windows and 278 s on macOS: its tests took 571, 435 and 348 s, of which the 29 rows of
/// <c>Dense_EveryMinimumOfThePass_IsListedWithinTheBounds</c> and
/// <c>Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds</c> 564, 431 and 344 s. Each fixture is a class
/// fixture of exactly one class, so that no NAIF id is written or loaded twice, and every row moves with its fixture: the
/// same rows, assertions, windows, options and bounds, spread over six classes.
/// </para>
/// <para>
/// A theory whose rows use fixtures of several classes is declared under its name in each of them with the rows of its
/// fixtures, its body here under the same name without <c>Dense_</c>; <see cref="Pass"/> finds a fixture by name in the
/// fixtures of the class only, and throws on another name. The local minima of CSPICE on a window are computed once per
/// fixture and window (<see cref="CrossingEncounterCase.LocalMinima"/>).
/// </para>
/// </remarks>
public abstract class ConjunctionDensePassTestBase
{
    protected static readonly ConjunctionAnalysisOptions Options = new() { CovariancePropagation = null };

    // The reads of the range rate over the window of the dense search (#628), opt-in (decision 54 of #457)
    protected static readonly ConjunctionAnalysisOptions ReadOverWindowOptions = Options with { ReadRangeRateOverWindow = true };

    private readonly IReadOnlyDictionary<string, CrossingEncounterCase> _passes;
    protected readonly ITestOutputHelper _output;

    /// <param name="output">The output of the test.</param>
    /// <param name="passes">The fixtures of the class, by the names of the rows of its theories.</param>
    protected ConjunctionDensePassTestBase(ITestOutputHelper output, params (string Name, CrossingEncounterCase Pass)[] passes)
    {
        _output = output;
        _passes = passes.ToDictionary(p => p.Name, p => p.Pass);
    }

    /// <summary>The fixture of the class named <paramref name="fixture"/>; throws on a name the class does not own.</summary>
    protected CrossingEncounterCase Pass(string fixture) => _passes.TryGetValue(fixture, out var pass)
        ? pass
        : throw new ArgumentOutOfRangeException(nameof(fixture), fixture, $"{GetType().Name} owns no fixture {fixture}");

    // The local minima of the distance on the two SPKs: those of CSPICE's gfdist_c (LOCMIN, step 60 s, its convergence
    // tolerance of 1e-6 s), each refined to the zero of the range rate of CSPICE's states by secants (1 ms base) to an
    // epoch on the grid of ticks (Time.AddSeconds gives the nearest tick, #625), then below the tick
    // (CspiceZeroBelowTheTick): the zero is Epoch + Offset, Offset in s, and Distance the distance there; the secants
    // read the SPKs within their coverage only. The passes with two minima per orbit take the step of their case (1 s, 0.2 s with the fold): their
    // maxima lie less than 60 s from a minimum, which a step of 60 s steps over
    protected (TimeSystem.Time Epoch, double Offset, double Distance)[] CspiceLocalMinima(Window window,
        CrossingEncounterCase crossing)
    {
        return crossing.LocalMinima.GetOrAdd(window, w => CspiceLocalMinimaOf(w, crossing)).ToArray();
    }

    // The body of CspiceLocalMinima, which serves each window of a fixture once: a function of the fixture (its ids, the
    // step of its gfdist_c, the bounds of its window) and of the window, whose SPKs stay loaded until it is disposed
    private static (TimeSystem.Time Epoch, double Offset, double Distance)[] CspiceLocalMinimaOf(Window window,
        CrossingEncounterCase crossing)
    {
        var step = TimeSpan.FromSeconds(crossing is CwPassEncounterCase cw ? cw.GfStepSeconds : 60.0);
        var first = crossing.Window.StartDate;
        var last = crossing.Window.EndDate.AddSeconds(-1.0e-3);
        return SpiceAPI.Instance.FindWindowsOnDistanceConstraint(window, crossing.ProtectedId, crossing.SecondaryId,
                RelationnalOperator.LocalMin, 0.0, Aberration.None, step)
            .Select(w =>
            {
                var epoch = w.StartDate;
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    double f0 = CspiceRangeRate(epoch, crossing);
                    double f1 = CspiceRangeRate(epoch.AddSeconds(1.0e-3), crossing);
                    if (f1 == f0) break;
                    epoch = epoch.AddSeconds(-f0 * 1.0e-3 / (f1 - f0));
                    epoch = epoch < first ? first : epoch > last ? last : epoch;
                }

                var (offset, distance) = CspiceZeroBelowTheTick(epoch, crossing);
                return (epoch, offset, distance);
            })
            .ToArray();
    }

    // CSPICE's minimum within the TCA bound of a listed epoch near a window end, searched over [epoch − w, epoch + w],
    // w = max(2 × bound, 1 s), within the propagation; none when CSPICE has no minimum there
    protected (TimeSystem.Time Epoch, double Offset, double Distance)? CspiceMinimumNear(TimeSystem.Time epoch, Window window, double tcaBound,
        CrossingEncounterCase crossing)
    {
        double w = System.Math.Max(2.0 * tcaBound, 1.0);
        var start = epoch.AddSeconds(-w);
        var end = epoch.AddSeconds(w);
        start = start < crossing.Window.StartDate ? crossing.Window.StartDate : start;
        end = end > crossing.Window.EndDate ? crossing.Window.EndDate : end;
        var near = CspiceLocalMinima(new Window(start, end), crossing)
            .Where(m => System.Math.Abs((m.Epoch - epoch).TotalSeconds) <= tcaBound)
            .ToArray();
        return near.Length == 0 ? null : near.MinBy(m => System.Math.Abs((m.Epoch - epoch).TotalSeconds));
    }

    protected static (Vector3 Position, Vector3 Velocity) CspiceRelative(TimeSystem.Time epoch, CrossingEncounterCase crossing)
    {
        var s1 = CspiceState(epoch, crossing.ProtectedId);
        var s2 = CspiceState(epoch, crossing.SecondaryId);
        return (s2.Position - s1.Position, s2.Velocity - s1.Velocity);
    }

    protected static double CspiceRangeRate(TimeSystem.Time epoch, CrossingEncounterCase crossing)
    {
        var (position, velocity) = CspiceRelative(epoch, crossing);
        return position * velocity;
    }

    protected static StateVector CspiceState(TimeSystem.Time epoch, int id, int observer = 399)
    {
        return SpiceAPI.Instance.ReadEphemeris(epoch, observer, id, Frames.Frame.ICRF, Aberration.None).ToStateVector();
    }

    // ---- Round 4 of the lot-6 review: a maximum of the distance is no close approach ----

    // The bounds of the TCA and of the miss distance of the slow passes against CSPICE on their SPKs, twice the worst
    // over every minimum of each propagation, over Linux x64, Windows x64 and macOS arm64 (CI of #622 at 7f5e6dfb,
    // 2026-11-10) and the reviewers' probes (Linux x64), rounded up to two digits. Measured by
    // Dense_EveryMinimumOfThePass_IsListedWithinTheBounds (1222 to 1527 windows, those that end or start at the zero
    // included, a listed minimum within the TCA bound of an end measured against CSPICE's minimum just outside the
    // window, against CSPICE's zero below the tick) and by the reviewers' probes on the same propagations (their 634 to
    // 1571 windows, gfdist_c refined by bisection of spkezr_c's range rate to 1e-10 s), the larger of the two. This
    // test gives the same figures on the three platforms but CWE (TCA 3.068e-6 s on Linux x64, 3.205e-6 s on Windows
    // x64, 3.282e-6 s on macOS arm64; miss 9.269e-8, 9.212e-8 and 9.206e-8 m) and CWF (miss 9.685e-7 m on Linux x64 and
    // macOS arm64, 9.684e-7 m on Windows x64); the worst over the three:
    //                                         this test                 the probe                 bound
    //   SLOW (60 s steps)                     1.272e-7 s  3.136e-10 m   1.480e-7 s  2.294e-10 m   3.0e-7 s  6.3e-10 m
    //   SLOWD (default steps)                 2.907e-5 s  1.147e-6 m    2.903e-5 s  1.147e-6 m    5.9e-5 s  2.3e-6 m
    //   SLOWX (default and 30 s steps, 1 m)   5.047e-2 s  2.074e-7 m    5.041e-2 s  2.072e-7 m    1.1e-1 s  4.2e-7 m
    //   CWD (two minima per orbit, default)   6.498e-7 s  4.732e-8 m    7.190e-7 s  4.807e-8 m    1.5e-6 s  9.7e-8 m
    //   CW60 (60 s steps)                     7.096e-7 s  4.702e-8 m    7.110e-7 s  4.753e-8 m    1.5e-6 s  9.6e-8 m
    //   CWE (y0 = 1500 m, default)            3.282e-6 s  9.269e-8 m    3.025e-6 s  9.163e-8 m    6.6e-6 s  1.9e-7 m
    //   CWE60 (y0 = 1500 m, 60 s steps)       1.233e-6 s  4.893e-8 m    1.270e-6 s  4.905e-8 m    2.6e-6 s  9.9e-8 m
    //   CWF (the fold, default)               2.094e-3 s  9.685e-7 m    2.083e-3 s  9.684e-7 m    4.2e-3 s  2.0e-6 m
    //   CWF60 (the fold, 60 s steps)          3.338e-5 s  3.864e-8 m    3.232e-5 s  3.848e-8 m    6.7e-5 s  7.8e-8 m
    // At the tick nearer the zero (#624), this test over Linux x64, Windows x64 and macOS arm64 (CI of #629 at
    // 8c864b23, 2026-11-10; 1187 to 1524 windows: the windows about the listed minima now coincide more often with
    // those about CSPICE's), the same on the three but CWE (TCA 3.068e-6 s on Linux x64, 3.205e-6 s on Windows x64,
    // 3.282e-6 s on macOS arm64; miss 9.269e-8, 9.212e-8 and 9.206e-8 m, as on lot 6), and the probes, run again on
    // #624 by its verifier (Linux x64, 634 to 911 windows, gfdist_c refined by bisection of spkezr_c's range rate to
    // 1e-10 s), give, the worst over the three:
    //                                         this test                 the probe                 bound
    //   SLOW (60 s steps)                     1.272e-7 s  3.136e-10 m   1.480e-7 s  2.294e-10 m   3.0e-7 s  6.3e-10 m
    //   SLOWD (default steps)                 2.907e-5 s  1.147e-6 m    2.903e-5 s  1.147e-6 m    5.9e-5 s  2.3e-6 m
    //   SLOWX (default and 30 s steps, 1 m)   5.048e-2 s  2.076e-7 m    5.041e-2 s  2.072e-7 m    1.1e-1 s  4.2e-7 m
    //   CWD (two minima per orbit, default)   6.498e-7 s  4.732e-8 m    6.190e-7 s  4.779e-8 m    1.3e-6 s  9.7e-8 m
    //   CW60 (60 s steps)                     7.096e-7 s  4.702e-8 m    6.110e-7 s  4.753e-8 m    1.5e-6 s  9.6e-8 m
    //   CWE (y0 = 1500 m, default)            3.282e-6 s  9.269e-8 m    3.025e-6 s  9.163e-8 m    6.6e-6 s  1.9e-7 m
    //   CWE60 (y0 = 1500 m, 60 s steps)       1.233e-6 s  4.893e-8 m    1.170e-6 s  4.892e-8 m    2.5e-6 s  9.9e-8 m
    //   CWF (the fold, default)               2.094e-3 s  9.677e-7 m    2.082e-3 s  9.676e-7 m    4.2e-3 s  2.0e-6 m
    //   CWF60 (the fold, 60 s steps)          3.328e-5 s  3.864e-8 m    3.222e-5 s  3.848e-8 m    6.7e-5 s  7.8e-8 m
    // Every bound is twice the worst of the two, rounded up to two digits (CWE's TCA bound from 3.282e-6 s on macOS
    // arm64, as on lot 6: this test gives CWE the same figures on each platform before and after #624). CWD's and
    // CWE60's move from 1.5e-6 s and 2.6e-6 s to 1.3e-6 s and 2.5e-6 s: the verifier's probe gives 6.190e-7 s and
    // 1.170e-6 s on lot 6 before #624 (a9f2a371) as on #624, and the earlier probes, 7.190e-7 s and 1.270e-6 s, lay
    // exactly one tick above it, as a reference on the grid of ticks would. The miss bounds are unchanged (the figures
    // within 1 % of the earlier ones).
    // The TCA error is the difference of the range rate of the states and of their SPK (the degree-7 interpolation of
    // their 60 s outputs) over its slope, 2.6e-4 m/s per second at a drift of 100 m and 2.6e-6 at 1 m; at the fold, 2.9e-7 m
    // deep, the states and their SPK of the default steps place the zero 2.1 ms apart (the library within 7.4e-6 s of the
    // zero of the states, from a scan of their range rate). A CSPICE minimum or a listed one within the TCA bound of a
    // window end may be on either side of it for the states and their SPK.
    // The passes of #628, whose minima no seed reached before (mixed steps, fixed 600 s steps), measured by this test on
    // Linux x64 (2026-11-10) and by the probe of #628 (843 to 1084 windows per propagation, gfdist_c refined by bisection
    // of spkezr_c's range rate to 1e-10 s); the CI of Linux x64, Windows x64 and macOS arm64 (run 43000949510, 2026-11-11,
    // by default and with the reads over the window) gives this test's figures on the three platforms, but CW600's miss
    // on Windows x64, 1.278e-5 m; with the nearest tick of #625 (merged 2026-11-11) they move in their fourth digit,
    // the windows ending a tick apart (this test's column, Linux x64; before #625: CWM 3.543e-5 s 8.430e-7 m, CWM90
    // 6.057e-6 s 6.567e-7 m, CWN 6.519e-5 s, CW600 3.107e-5 s, CWE600 1.016e-4 s, CWFM 3.223e-3 s 2.279e-6 m):
    //                                         this test                 the probe                 bound
    //   CWM (y0 = 1500 m, default and 60 s)   3.542e-5 s  8.427e-7 m    3.546e-5 s  8.433e-7 m    7.1e-5 s  1.7e-6 m
    //   CWM90 (y0 = 1490 m, default and 60 s) 6.067e-6 s  6.560e-7 m    6.066e-6 s  6.564e-7 m    1.3e-5 s  1.4e-6 m
    //   CWN (y0 = 1495 m, 60 s and default)   6.501e-5 s  1.960e-6 m    6.507e-5 s  1.960e-6 m    1.4e-4 s  4.0e-6 m
    //   CW600 (y0 = 1490 m, fixed 600 s)      3.106e-5 s  1.277e-5 m    3.105e-5 s  1.277e-5 m    6.3e-5 s  2.6e-5 m
    //   CWE600 (y0 = 1500 m, fixed 600 s)     1.015e-4 s  2.090e-5 m    1.016e-4 s  2.090e-5 m    2.1e-4 s  4.2e-5 m
    //   CWFM (the fold, default and 60 s, read every 1 s, round 1 of the review of #628)
    //                                         3.224e-3 s  2.278e-6 m    3.219e-3 s  2.280e-6 m    6.5e-3 s  4.6e-6 m
    // twice the worst of the two, rounded up to two digits: the states of mixed or 600 s steps against the degree-7
    // interpolation of their 60 s outputs in the SPK
    protected static (double Tca, double Miss) SlowPassBounds(CrossingEncounterCase crossing) => crossing switch
    {
        SlowPassEncounterCase => (3.0e-7, 6.3e-10),
        DefaultStepSlowPassEncounterCase => (5.9e-5, 2.3e-6),
        MixedStepSlowPassEncounterCase => (1.1e-1, 4.2e-7),
        CwPassDefaultStepCase => (1.3e-6, 9.7e-8),
        CwPass60StepCase => (1.5e-6, 9.6e-8),
        CwPass1500DefaultStepCase => (6.6e-6, 1.9e-7),
        CwPass1500Step60Case => (2.5e-6, 9.9e-8),
        CwPassFoldDefaultStepCase => (4.2e-3, 2.0e-6),
        CwPassFoldStep60Case => (6.7e-5, 7.8e-8),
        CwPassMixedStep1500Case => (7.1e-5, 1.7e-6),
        CwPassMixedStep1490Case => (1.3e-5, 1.4e-6),
        CwPassMixedStep1495Case => (1.4e-4, 4.0e-6),
        CwPassStep600Case => (6.3e-5, 2.6e-5),
        CwPass1500Step600Case => (2.1e-4, 4.2e-5),
        CwPassFoldMixedStepCase => (6.5e-3, 4.6e-6),
        _ => throw new ArgumentOutOfRangeException(nameof(crossing))
    };

    protected void SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(string steps, double start, double end, int count)
    {
        // Before, at the default steps, the Hermite of the steps had minima near the maxima of the distance (about 112 m,
        // t0 + 1398.7, 4246.7, 7096.0 and 9945.6 s), which the refinement could not bracket on the range rate of the
        // states and kept unrefined: AnalyzeAll listed them, a range rate of +0.0012 m/s at t0 + 4246.66 s, and the dense
        // Analyze returned that maximum on [3000, 5500] s instead of the closer end (53.06 m against 111.79 m). The count is
        // that of the minima away from the window ends: on [0, 10800] s a fourth one lies at the start, its range rate
        // -2e-13 m²/s there, which CSPICE lists at 6e-8 s and the states one tick after the start
        var crossing = Pass(steps == "60 s" ? "SLOW" : "SLOWD");
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    protected void TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(string steps, double start, double end, int count)
    {
        // At the default steps the Hermite seeds the minimum at t0 + 1526.28 s 1.6 s past it, where the range rate of the
        // states is already positive (+0.086 m²/s), as at the candidates on either side, the maximum between the two minima
        // lying before the one on the left: the zero lies between that candidate and the seed. Before round 5 of the lot-6
        // review, the refinement followed the candidates to the left, reached the pair that holds the minimum at
        // t0 + 1317.43 s and dropped the seed, so that AnalyzeAll listed the minimum at 1317.43 s alone and Analyze returned
        // it (509.744 m against 509.695 m); before round 4 it kept the seed unrefined, 1.6 s off, and on [1400, 1700] s it
        // listed nothing, Analyze returning the start. It is now searched from the seed on its own range rate
        CrossingEncounterCase crossing = Pass(steps == "60 s" ? "CW60" : "CWD");
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    protected void TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(string steps)
    {
        // y0 = 1500 m: the window ends 60 s after the minimum at t0 + 7029.75 s and starts 1400 s before it, where the
        // fourth reviewer found it dropped in round 4 of the lot-6 review, as in 134 other windows
        CrossingEncounterCase crossing = Pass(steps == "60 s" ? "CWE60" : "CWE");
        var minimum = CspiceLocalMinima(crossing.Seconds(6500.0, 7100.0), crossing).Single().Epoch;
        AssertSlowPassMatchesCspice(crossing, new Window(minimum.AddSeconds(-1400.0), minimum.AddSeconds(60.0)), 1);
    }

    protected void ShallowMinimumNextToAFold_IsListed(string steps, double start, double end, int count)
    {
        // The shallow minimum at t0 + 7065.55 s, its range rate positive over 1.6 s only: the Hermite seeds it 4.85 s
        // before at 60 s steps, closing, and the reads of the search from the seed, at distances doubling, read the range rate negative
        // 4.096 and 8.192 s after the seed, on either side of the 1.6 s. In round 5 of the lot-6 review it was lost on these
        // windows (204 to 229 windows of the fifth reviewer per propagation), except on the short [7060, 7067] s; before,
        // the seed was listed unrefined, 4.85 s off at 60 s steps. The reads are now 1 s apart at most
        CrossingEncounterCase crossing = Pass(steps == "60 s" ? "CWF60" : "CWF");
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    protected void ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(string steps, double startOffset, double endOffset,
        int count)
    {
        // Windows of the fifth reviewer about the shallow minimum (CSPICE's on the SPKs of each propagation), the next
        // minimum 91 s after it
        CrossingEncounterCase crossing = Pass(steps == "60 s" ? "CWF60" : "CWF");
        var minimum = CspiceLocalMinima(crossing.Seconds(7000.0, 7100.0), crossing).Single().Epoch;
        AssertSlowPassMatchesCspice(crossing, new Window(minimum.AddSeconds(startOffset), minimum.AddSeconds(endOffset)), count);
    }

    protected void MinimumWithoutASeedNearIt_IsListed(string fixture, double start, double end, int count)
    {
        // #628, with ReadRangeRateOverWindow (opt-in, decision 54 of #457; without it, Dense_ByDefault_* below): the
        // windows the review of lot 6 traced, where the Hermite of mixed or long steps places no minimum near a
        // minimum of the states (form A: CWM's 504.6428 m at t0 + 7029.70 s, the range rate -3.18, -0.22, +0.062 and
        // -0.044 m²/s at the candidates of the window, the only seed its end; CWM90's minimum 1 s after the start; CWN's at
        // t0 + 1497.86 s, closing at t0 + 1440 s and receding at 1500 s), or one 600 s step holds two minima (form B:
        // CW600's at t0 + 1317.45 and 1526.28 s, a maximum between them, the Hermite one minimum at 1502.10 s; CWE600's at
        // t0 + 7029.94 s). Before, AnalyzeAll did not list them on the traced windows and on the whole windows of CW600
        // and CWE600, and Analyze returned a window end (504.6578 m on CWM's window) or another minimum (CWN, CW600); the
        // windows of 3000 s every 97 s of the sweeps lost them in 163 to 164 windows at 600 s steps. The whole windows of
        // CWM, CWM90 and CWN are regression rows: main b237eec2 already lists every minimum there, the seeds of the whole
        // window reaching them (round 1 of the review of #628)
        AssertSlowPassMatchesCspice(Pass(fixture), Pass(fixture).Seconds(start, end), count, options: ReadOverWindowOptions);
    }

    protected void ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end)
    {
        // The windows of #628 with the default options (decision 54 of #457: the reads of the range rate over the window
        // opt-in): a minimum that no minimum of the Hermite seeds may be lost (on CWM's window, AnalyzeAll lists nothing and
        // Analyze returns the window end, 504.658 m, for the minimum of 504.643 m; on CW600's [1200, 1800] s, AnalyzeAll
        // lists only the minimum at t0 + 1526.28 s, 509.692 m, losing the farther one at 1317.45 s, 509.741 m, and Analyze
        // returns 1526.28 s either way), but every TCA listed is a minimum of CSPICE within the bounds, inside the window, in
        // order, none twice, and Analyze returns the closest one listed, or without one the end where the distance grows
        // into the window
        Assert.False(new ConjunctionAnalysisOptions().ReadRangeRateOverWindow);
        AssertSlowPassListsOnlyMinima(Pass(fixture), Pass(fixture).Seconds(start, end), Options, log: true);
    }

    protected void ByDefault_TheRangeRateIsNotReadOverTheWindow(string fixture, double start, double end)
    {
        // Decision 54 of #457, through the public Analyze and AnalyzeAll: by default the dense search reads the range rate
        // the seeds need only, so MaximumEventSearchStep changes neither its reads nor its results, and at a spacing of 1 s
        // it reads fewer states than the reads over the window alone need (one per second of the window); with
        // ReadRangeRateOverWindow it reads at least those (the review of decision 54: a search that always read over the
        // window passed every other test)
        var crossing = Pass(fixture);
        var window = crossing.Seconds(start, end);
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        double length = (window.EndDate - window.StartDate).TotalSeconds;

        (long Reads, string Result) All(ConjunctionAnalysisOptions options)
        {
            ConjunctionAssessment.DenseRangeRateReads = 0;
            var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window,
                options);
            return (ConjunctionAssessment.DenseRangeRateReads, string.Join("; ",
                all.Select(e => $"{e.EncounterState.Epoch.TimeSpanFromJ2000().Ticks} {e.EncounterState.MissDistanceMeters:R}")));
        }

        (long Reads, string Result) One(ConjunctionAnalysisOptions options)
        {
            ConjunctionAssessment.DenseRangeRateReads = 0;
            var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2,
                window, options);
            return (ConjunctionAssessment.DenseRangeRateReads,
                $"{encounter.EncounterState.Epoch.TimeSpanFromJ2000().Ticks} {encounter.EncounterState.MissDistanceMeters:R}");
        }

        var fine = Options with { MaximumEventSearchStep = TimeSpan.FromSeconds(1.0) };
        foreach (var search in new Func<ConjunctionAnalysisOptions, (long Reads, string Result)>[] { All, One })
        {
            var byDefault = search(Options);
            var byDefaultFine = search(fine);
            var withTheReads = search(fine with { ReadRangeRateOverWindow = true });
            _output.WriteLine($"reads by default {byDefault.Reads} (at 1 s {byDefaultFine.Reads}), with the option at 1 s " +
                              $"{withTheReads.Reads}, window {length:F2} s");
            Assert.Equal(byDefault, byDefaultFine);
            Assert.True(byDefaultFine.Reads < length, $"{byDefaultFine.Reads} reads by default at 1 s");
            Assert.True(withTheReads.Reads >= length, $"{withTheReads.Reads} reads with the option at 1 s");
        }
    }

    protected void EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false)
    {
        // The windows of the reviewers' sweeps over the whole propagation: the start and the end of each window at 1 to
        // 1400 s on either side of each local minimum of CSPICE on the whole window (within the propagation), windows of
        // 300, 700, 1500 and 3000 s every 97 s, and the windows of the fifth reviewer (round 5 of the lot-6 review): about
        // each minimum of CSPICE, 0.1 ms to 5 s on both sides and 0.1 ms to 600 s on either side, and the windows that end
        // or start at the zero, at -10 to 10 ticks and at 1 µs to 10 ms on either side of each minimum of CSPICE and of each
        // minimum the dense AnalyzeAll lists on the whole window, from 300 s and from 20 ms on the other side. Every minimum
        // of the propagation is measured, from many windows, against CSPICE's zero below the tick: the bounds of
        // SlowPassBounds are twice the worst of this test
        // The passes of #628 (CWM to CWE600) only with ReadRangeRateOverWindow (decision 54 of #457): by default a minimum
        // that no minimum of the Hermite seeds may be lost there (Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimum)
        var crossing = Pass(fixture);
        var options = readRangeRateOverWindow ? ReadOverWindowOptions : Options;
        var (minima, windows) = SweepWindows(crossing, options);

        _worstSlowTcaError = 0.0;
        _worstSlowMissError = 0.0;
        foreach (var (start, end) in windows)
        {
            AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), null, log: false, options: options);
        }

        _output.WriteLine($"MEASURE {fixture}{(readRangeRateOverWindow ? " (reads over the window)" : "")} every minimum " +
                          $"({minima.Length} on the whole window, {windows.Count} windows): " +
                          $"TCA {_worstSlowTcaError:E3} s, miss {_worstSlowMissError:E3} m");
    }

    protected void ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture)
    {
        // The windows of the sweep above, on the passes of #628, with the default options (decision 54 of #457): a minimum
        // that no minimum of the Hermite seeds may be missing, but every TCA listed is a minimum of CSPICE within the bounds
        // of the pass, inside the window, in order, none twice, and Analyze returns the closest one listed, or without one
        // the end where the distance grows into the window (the contract that holds by default)
        var crossing = Pass(fixture);
        var (minima, windows) = SweepWindows(crossing, Options);
        _worstSlowTcaError = 0.0;
        _worstSlowMissError = 0.0;
        foreach (var (start, end) in windows)
        {
            AssertSlowPassListsOnlyMinima(crossing, crossing.Seconds(start, end), Options, log: false);
        }

        _output.WriteLine($"MEASURE {fixture} by default, every listed minimum ({minima.Length} on the whole window, " +
                          $"{windows.Count} windows): TCA {_worstSlowTcaError:E3} s, miss {_worstSlowMissError:E3} m");
    }

    // The windows of the sweeps: the start and the end of each window at 1 to 1400 s on either side of each local minimum of
    // CSPICE on the whole window (within the propagation), windows of 300, 700, 1500 and 3000 s every 97 s, and the windows
    // of the fifth reviewer (round 5 of the lot-6 review), about each minimum of CSPICE and of each minimum AnalyzeAll lists
    // on the whole window with the given options
    protected (double[] Minima, System.Collections.Generic.SortedSet<(double Start, double End)> Windows) SweepWindows(
        CrossingEncounterCase crossing, ConjunctionAnalysisOptions options)
    {
        double duration = (crossing.Window.EndDate - crossing.Window.StartDate).TotalSeconds;
        var minima = CspiceLocalMinima(crossing.Window, crossing)
            .Select(m => (m.Epoch - crossing.T0).TotalSeconds)
            .ToArray();
        var listed = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(crossing.Sat1), crossing.Solution1,
                crossing.Sat2, crossing.Solution2, crossing.Window, options)
            .Select(e => (e.EncounterState.Epoch - crossing.T0).TotalSeconds)
            .ToArray();
        double[] offsets = { 1.0, 10.0, 30.0, 60.0, 120.0, 200.0, 400.0, 800.0, 1400.0 };
        var windows = new System.Collections.Generic.SortedSet<(double Start, double End)>();
        foreach (double minimum in minima)
        {
            foreach (double before in offsets)
            {
                foreach (double after in offsets)
                {
                    double start = System.Math.Round(System.Math.Max(0.0, minimum - before), 7);
                    double end = System.Math.Round(System.Math.Min(duration, minimum + after), 7);
                    if (end > start)
                    {
                        windows.Add((start, end));
                    }
                }
            }
        }

        foreach (double length in new[] { 300.0, 700.0, 1500.0, 3000.0 })
        {
            for (double start = 0.0; start + length <= duration; start += 97.0)
            {
                windows.Add((start, start + length));
            }
        }

        foreach (double minimum in minima)
        {
            foreach (double half in new[] { 1.0e-4, 1.0e-3, 1.0e-2, 0.1, 1.0, 5.0 })
            {
                AddWindow(minimum - half, minimum + half);
            }

            double[] sides = { 1.0e-4, 1.0e-2, 1.0, 60.0, 600.0 };
            foreach (double before in sides)
            {
                foreach (double after in sides)
                {
                    AddWindow(minimum - before, minimum + after);
                }
            }
        }

        var splits = Enumerable.Range(-10, 21).Select(k => k * 1.0e-7)
            .Concat(new[] { 1.0e-6, 1.0e-5, 1.0e-4, 1.0e-3, 1.0e-2 }.SelectMany(d => new[] { -d, d }))
            .ToArray();
        foreach (double centre in minima.Concat(listed))
        {
            foreach (double split in splits)
            {
                double at = centre + split;
                foreach (double side in new[] { 300.0, 0.02 })
                {
                    AddWindow(at - side, at);
                    AddWindow(at, at + side);
                }
            }
        }

        void AddWindow(double start, double end)
        {
            double s0 = System.Math.Round(System.Math.Max(0.0, start), 7);
            double e0 = System.Math.Round(System.Math.Min(duration, end), 7);
            if (e0 > s0)
            {
                windows.Add((s0, e0));
            }
        }

        return (minima, windows);
    }

    // The contract of the dense search by default (decision 54 of #457): AnalyzeAll lists only local minima of CSPICE's
    // gfdist_c, each within the bounds of the slow passes (or, within the TCA bound of a window end, one CSPICE places just
    // outside the window, or none, the states and their SPK placing the zero on either side of that end), strictly inside
    // the window, in order, none twice; Analyze returns the closest one listed, or without one the window end where the
    // distance grows into the window (AssertTheEndWhereTheDistanceGrows)
    protected void AssertSlowPassListsOnlyMinima(CrossingEncounterCase crossing, Window window,
        ConjunctionAnalysisOptions options, bool log)
    {
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        var (tcaBound, missBound) = SlowPassBounds(crossing);
        var locmin = CspiceLocalMinima(window, crossing);
        var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        string listed = $"[{(window.StartDate - crossing.T0).TotalSeconds:F7}, {(window.EndDate - crossing.T0).TotalSeconds:F7}] s: " +
                        $"LOCMIN [{string.Join("; ", locmin.Select(m => $"{(m.Epoch - crossing.T0).TotalSeconds:F7} s {m.Distance:R} m"))}], " +
                        $"AnalyzeAll [{string.Join("; ", all.Select(e => $"{(e.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {e.EncounterState.MissDistanceMeters:R} m"))}], " +
                        $"Analyze {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {encounter.EncounterState.MissDistanceMeters:R} m";
        if (log)
        {
            _output.WriteLine(listed);
        }

        try
        {
            bool NearAnEnd(TimeSystem.Time epoch) => System.Math.Abs((epoch - window.StartDate).TotalSeconds) <= tcaBound ||
                                                     System.Math.Abs((window.EndDate - epoch).TotalSeconds) <= tcaBound;

            var epochs = all.Select(e => e.EncounterState.Epoch).ToArray();
            Assert.All(epochs, epoch => Assert.True(epoch > window.StartDate && epoch < window.EndDate));
            Assert.True(epochs.Zip(epochs.Skip(1), (a, b) => a < b).All(ordered => ordered), "ordered, none twice");
            var matched = new System.Collections.Generic.List<int>();
            foreach (var e in all)
            {
                int index = locmin.Select((m, i) => (m, i))
                    .Where(x => System.Math.Abs((e.EncounterState.Epoch - x.m.Epoch).TotalSeconds) <= tcaBound)
                    .Select(x => x.i).DefaultIfEmpty(-1).First();
                if (index >= 0)
                {
                    AssertSlowPassMinimum(e, locmin[index], tcaBound, missBound, crossing, log);
                    matched.Add(index);
                    continue;
                }

                Assert.True(NearAnEnd(e.EncounterState.Epoch), "a listed TCA that is no minimum of CSPICE");
                if (CspiceMinimumNear(e.EncounterState.Epoch, window, tcaBound, crossing) is { } outside)
                {
                    AssertSlowPassMinimum(e, outside, tcaBound, missBound, crossing, log);
                }
            }

            Assert.Equal(matched.Count, matched.Distinct().Count());
            if (all.Count > 0)
            {
                Assert.Equal(all.MinBy(e => e.EncounterState.MissDistanceMeters).EncounterState.Epoch,
                    encounter.EncounterState.Epoch);
            }
            else if (locmin.Length > 0 && locmin.All(m => NearAnEnd(m.Epoch)))
            {
                // Only minima within the band of an end, none listed: an end, as AssertSlowPassLists
                Assert.True(encounter.EncounterState.Epoch == window.StartDate ||
                            encounter.EncounterState.Epoch == window.EndDate);
            }
            else
            {
                // None listed, a minimum inside lost by default or none: the end where the distance grows into the window
                AssertTheEndWhereTheDistanceGrows(crossing, window, encounter, tcaBound, missBound);
            }
        }
        catch (Xunit.Sdk.XunitException e) when (!log)
        {
            throw new Xunit.Sdk.XunitException($"{listed}{Environment.NewLine}{e.Message}");
        }
    }

    // AnalyzeAll lists the local minima of CSPICE's gfdist_c in the window, each within the bounds of the slow passes, and
    // nothing else; a CSPICE minimum within the TCA bound of a window end may be listed or not, and a listed one there may
    // have no CSPICE minimum, the states and their SPK placing the zero of the range rate on either side of that end.
    // Analyze returns the closest listed minimum, or without one the window end where the distance grows into the window by
    // CSPICE's range rate (the closer one when both do)
    protected void AssertSlowPassMatchesCspice(CrossingEncounterCase crossing, Window window, int? expectedCount,
        bool log = true, ConjunctionAnalysisOptions options = null)
    {
        options ??= Options;
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        var (tcaBound, missBound) = SlowPassBounds(crossing);
        var locmin = CspiceLocalMinima(window, crossing);

        var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        string listed = $"[{(window.StartDate - crossing.T0).TotalSeconds:F7}, {(window.EndDate - crossing.T0).TotalSeconds:F7}] s: " +
                          $"LOCMIN [{string.Join("; ", locmin.Select(m => $"{(m.Epoch - crossing.T0).TotalSeconds:F7} s {m.Distance:R} m"))}], " +
                          $"AnalyzeAll [{string.Join("; ", all.Select(e => $"{(e.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {e.EncounterState.MissDistanceMeters:R} m"))}], " +
                          $"Analyze {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s";
        if (log)
        {
            _output.WriteLine(listed);
        }

        try
        {
            AssertSlowPassLists(crossing, window, expectedCount, locmin, all, encounter, tcaBound, missBound, log);
        }
        catch (Xunit.Sdk.XunitException e) when (!log)
        {
            throw new Xunit.Sdk.XunitException($"{listed}{Environment.NewLine}{e.Message}");
        }
    }

    private void AssertSlowPassLists(CrossingEncounterCase crossing, Window window, int? expectedCount,
        (TimeSystem.Time Epoch, double Offset, double Distance)[] locmin, System.Collections.Generic.IReadOnlyList<EncounterCase> all,
        EncounterCase encounter, double tcaBound, double missBound, bool log)
    {
        bool NearAnEnd(TimeSystem.Time epoch) => System.Math.Abs((epoch - window.StartDate).TotalSeconds) <= tcaBound ||
                                                 System.Math.Abs((window.EndDate - epoch).TotalSeconds) <= tcaBound;

        if (expectedCount.HasValue)
        {
            Assert.Equal(expectedCount.Value, locmin.Count(m => !NearAnEnd(m.Epoch)));
        }

        Assert.All(all, e => Assert.True(e.EncounterState.Epoch > window.StartDate && e.EncounterState.Epoch < window.EndDate));
        var matched = all.Select(e => locmin.Select((m, i) => (m, i))
                .Where(x => System.Math.Abs((e.EncounterState.Epoch - x.m.Epoch).TotalSeconds) <= tcaBound)
                .Select(x => x.i).DefaultIfEmpty(-1).First())
            .ToArray();
        Assert.Equal(matched.Where(i => i >= 0).Count(), matched.Where(i => i >= 0).Distinct().Count());
        Assert.All(locmin.Where((m, i) => !matched.Contains(i)), m => Assert.True(NearAnEnd(m.Epoch)));
        var outsideReferences = new System.Collections.Generic.List<(TimeSystem.Time Epoch, double Offset, double Distance)>();
        for (int i = 0; i < all.Count; i++)
        {
            if (matched[i] < 0)
            {
                Assert.True(NearAnEnd(all[i].EncounterState.Epoch));
                // A listed minimum within the TCA bound of an end whose CSPICE minimum lies just outside the window (a window
                // starting or ending at the zero, the states placing it inside): measured against that minimum, from
                // CSPICE's minima of the window widened by the bound (the round-6 verifier's windows)
                var outside = CspiceMinimumNear(all[i].EncounterState.Epoch, window, tcaBound, crossing);
                if (outside.HasValue)
                {
                    AssertSlowPassMinimum(all[i], outside.Value, tcaBound, missBound, crossing, log);
                    outsideReferences.Add(outside.Value);
                }

                continue;
            }

            AssertSlowPassMinimum(all[i], locmin[matched[i]], tcaBound, missBound, crossing, log);
        }

        if (all.Count > 0)
        {
            // Analyze returns the closest listed minimum, one within the TCA bound of an end included, measured against
            // its CSPICE minimum just outside the window (#628: a window ending at a minimum, the states placing it one
            // tick inside, with another minimum inside that no seed reached before)
            var references = matched.Where(i => i >= 0).Select(i => locmin[i]).Concat(outsideReferences).ToArray();
            if (references.Length > 0)
            {
                AssertSlowPassMinimum(encounter, references.MinBy(m => m.Distance), tcaBound, missBound, crossing, log);
            }
            else
            {
                Assert.Contains(encounter.EncounterState.Epoch, all.Select(e => e.EncounterState.Epoch));
            }

            return;
        }

        if (locmin.Length > 0)
        {
            // Only minima within the band of an end, none listed: an end
            Assert.True(encounter.EncounterState.Epoch == window.StartDate || encounter.EncounterState.Epoch == window.EndDate);
            return;
        }

        AssertTheEndWhereTheDistanceGrows(crossing, window, encounter, tcaBound, missBound);
    }

    // Analyze without a listed minimum: the window end where the distance grows into the window by CSPICE's range rate, the
    // closer one when both do (either one when their distances lie within the miss bound of each other), or an end within
    // the TCA bound of a zero of CSPICE's range rate
    private static void AssertTheEndWhereTheDistanceGrows(CrossingEncounterCase crossing, Window window,
        EncounterCase encounter, double tcaBound, double missBound)
    {
        // A zero of CSPICE's range rate, from negative to positive, within the TCA bound of a window end (inside the window
        // or outside it, too near an end for gfdist_c): the states may place it anywhere within that bound, so that the
        // whole window recedes (the start the closest point) or closes (the end) on the states. A window narrower than the
        // bound about a minimum, or starting or ending at one, as in the fifth reviewer's windows (round 5 of the lot-6 review)
        bool ZeroNear(TimeSystem.Time end)
        {
            var before = end.AddSeconds(-tcaBound);
            var after = end.AddSeconds(tcaBound);
            before = before < crossing.Window.StartDate ? crossing.Window.StartDate : before;
            after = after > crossing.Window.EndDate ? crossing.Window.EndDate : after;
            return CspiceRangeRate(before, crossing) < 0.0 && CspiceRangeRate(after, crossing) >= 0.0;
        }

        if ((encounter.EncounterState.Epoch == window.StartDate && ZeroNear(window.StartDate)) ||
            (encounter.EncounterState.Epoch == window.EndDate && ZeroNear(window.EndDate)))
        {
            return;
        }

        var ends = new[] { window.StartDate, window.EndDate }
            .Where(epoch => epoch == window.StartDate
                ? CspiceRangeRate(epoch, crossing) >= 0.0
                : CspiceRangeRate(epoch, crossing) <= 0.0)
            .ToArray();
        Assert.NotEmpty(ends);

        // Both ends nearer to each other than the miss bound of the states against their SPK: either one
        var distances = ends.Select(epoch => CspiceRelative(epoch, crossing).Position.Magnitude()).ToArray();
        if (distances.Length == 2 && System.Math.Abs(distances[0] - distances[1]) <= 2.0 * missBound)
        {
            Assert.Contains(encounter.EncounterState.Epoch, ends);
            return;
        }

        Assert.Equal(ends.MinBy(epoch => CspiceRelative(epoch, crossing).Position.Magnitude()), encounter.EncounterState.Epoch);
    }

    private double _worstSlowTcaError;
    private double _worstSlowMissError;

    private void AssertSlowPassMinimum(EncounterCase encounter,
        (TimeSystem.Time Epoch, double Offset, double Distance) reference, double tcaBound, double missBound,
        CrossingEncounterCase crossing, bool log)
    {
        double tcaError = (encounter.EncounterState.Epoch - reference.Epoch).TotalSeconds - reference.Offset;
        double missError = encounter.EncounterState.MissDistanceMeters - reference.Distance;
        _worstSlowTcaError = System.Math.Max(_worstSlowTcaError, System.Math.Abs(tcaError));
        _worstSlowMissError = System.Math.Max(_worstSlowMissError, System.Math.Abs(missError));
        if (log)
        {
            _output.WriteLine($"MEASURE slow pass TCA t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: " +
                              $"{tcaError:E3} s, miss {missError:E3} m");
        }

        Assert.True(System.Math.Abs(tcaError) <= tcaBound,
            $"TCA t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) <= missBound,
            $"miss at t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: {missError:E3} m");
    }

    // The zero of CSPICE's range rate below the tick, from a reference minimum (an epoch, on the grid of ticks): the secant of
    // the range rate g at the epoch and 1 ms after it (before it at the end of the SPKs), in double precision, gives the
    // offset of the zero from the epoch, δ = -g/g', and the distance there, sqrt(d² - g²/g'), d² being quadratic over δ
    protected static (double Offset, double Distance) CspiceZeroBelowTheTick(TimeSystem.Time epoch, CrossingEncounterCase crossing)
    {
        double h = epoch.AddSeconds(1.0e-3) > crossing.Window.EndDate ? -1.0e-3 : 1.0e-3;
        var (position, velocity) = CspiceRelative(epoch, crossing);
        double g = position * velocity;
        double slope = (CspiceRangeRate(epoch.AddSeconds(h), crossing) - g) / h;
        double squared = position.MagnitudeSquared();
        if (slope <= 0.0)
        {
            return (0.0, System.Math.Sqrt(squared));
        }

        return (-g / slope, System.Math.Sqrt(squared - g * g / slope));
    }
}

/// <summary>
/// The rows of the dense search on the fold of the range rate at the default steps (CWF), the pass of <c>y0</c> = 1500 m at
/// fixed 600 s steps (CWE600) and the slow pass at the default steps (SLOWD), against CSPICE on their SPKs.
/// Their references and assertions are those of <see cref="ConjunctionDensePassTestBase"/>.
/// </summary>
public class ConjunctionDensePassATests : ConjunctionDensePassTestBase, IClassFixture<CwPassFoldDefaultStepCase>,
    IClassFixture<CwPass1500Step600Case>, IClassFixture<DefaultStepSlowPassEncounterCase>
{
    public ConjunctionDensePassATests(CwPassFoldDefaultStepCase cwFoldDefaultCase, CwPass1500Step600Case cw1500Step600Case,
        DefaultStepSlowPassEncounterCase defaultStepSlowCase, ITestOutputHelper output)
        : base(output, ("CWF", cwFoldDefaultCase), ("CWE600", cw1500Step600Case), ("SLOWD", defaultStepSlowCase))
    {
    }

    [Theory]
    [InlineData("default", 3000.0, 5500.0, 0)]
    [InlineData("default", 4000.0, 4500.0, 0)]
    [InlineData("default", 0.0, 10800.0, 3)]
    [InlineData("default", 2800.0, 5700.0, 2)]
    public void Dense_SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(string steps, double start, double end, int count) =>
        SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(steps, start, end, count);

    [Theory]
    [InlineData("default", 0.0, 10800.0, 3)]
    [InlineData("default", 7000.0, 7100.0, 1)]
    [InlineData("default", 7060.0, 7067.0, 1)]
    [InlineData("default", 4171.0, 7171.0, 2)]
    [InlineData("default", 5141.0, 8141.0, 2)]
    [InlineData("default", 6499.0, 7199.0, 2)]
    [InlineData("default", 6887.0, 7187.0, 2)]
    [InlineData("default", 6984.0, 9984.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_IsListed(string steps, double start, double end, int count) =>
        ShallowMinimumNextToAFold_IsListed(steps, start, end, count);

    [Theory]
    [InlineData("default", -800.0, 10.0, 1)]
    [InlineData("default", -400.0, 30.0, 1)]
    [InlineData("default", -30.0, 60.0, 1)]
    [InlineData("default", -10.0, 200.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(string steps, double startOffset, double endOffset,
        int count) =>
        ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(steps, startOffset, endOffset, count);

    [Theory]
    [InlineData("CWE600", 0.0, 10800.0, 3)]
    [InlineData("CWE600", 4268.0, 7268.0, 2)]
    public void Dense_MinimumWithoutASeedNearIt_IsListed(string fixture, double start, double end, int count) =>
        MinimumWithoutASeedNearIt_IsListed(fixture, start, end, count);

    [Theory]
    [InlineData("CWE600", 0.0, 10800.0)]
    [InlineData("CWE600", 4268.0, 7268.0)]
    public void Dense_ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end) =>
        ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(fixture, start, end);

    [Theory]
    [InlineData("SLOWD")]
    [InlineData("CWF")]
    [InlineData("SLOWD", true)]
    [InlineData("CWF", true)]
    [InlineData("CWE600", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false) =>
        EveryMinimumOfThePass_IsListedWithinTheBounds(fixture, readRangeRateOverWindow);

    [Theory]
    [InlineData("CWE600")]
    public void Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture) =>
        ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(fixture);
}

/// <summary>
/// The rows of the dense search on the fold of the range rate at 60 s steps (CWF60), the pass of <c>y0</c> = 1500 m at 60 s
/// steps (CWE60) and the slow pass at 60 s steps (SLOW), against CSPICE on their SPKs.
/// Their references and assertions are those of <see cref="ConjunctionDensePassTestBase"/>.
/// </summary>
public class ConjunctionDensePassBTests : ConjunctionDensePassTestBase, IClassFixture<CwPassFoldStep60Case>,
    IClassFixture<CwPass1500Step60Case>, IClassFixture<SlowPassEncounterCase>
{
    public ConjunctionDensePassBTests(CwPassFoldStep60Case cwFoldStep60Case, CwPass1500Step60Case cw1500Step60Case,
        SlowPassEncounterCase slowCase, ITestOutputHelper output)
        : base(output, ("CWF60", cwFoldStep60Case), ("CWE60", cw1500Step60Case), ("SLOW", slowCase))
    {
    }

    [Theory]
    [InlineData("60 s", 3000.0, 5500.0, 0)]
    [InlineData("60 s", 4000.0, 4500.0, 0)]
    [InlineData("60 s", 0.0, 10800.0, 3)]
    [InlineData("60 s", 2800.0, 5700.0, 2)]
    public void Dense_SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(string steps, double start, double end, int count) =>
        SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(steps, start, end, count);

    [Theory]
    [InlineData("60 s")]
    public void Dense_TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(string steps) =>
        TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(steps);

    [Theory]
    [InlineData("60 s", 0.0, 10800.0, 3)]
    [InlineData("60 s", 7000.0, 7100.0, 1)]
    [InlineData("60 s", 7060.0, 7067.0, 1)]
    [InlineData("60 s", 4171.0, 7171.0, 2)]
    [InlineData("60 s", 5141.0, 8141.0, 2)]
    [InlineData("60 s", 6499.0, 7199.0, 2)]
    [InlineData("60 s", 6887.0, 7187.0, 2)]
    [InlineData("60 s", 6984.0, 9984.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_IsListed(string steps, double start, double end, int count) =>
        ShallowMinimumNextToAFold_IsListed(steps, start, end, count);

    [Theory]
    [InlineData("60 s", -800.0, 10.0, 1)]
    [InlineData("60 s", -400.0, 30.0, 1)]
    [InlineData("60 s", -30.0, 60.0, 1)]
    [InlineData("60 s", -10.0, 200.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(string steps, double startOffset, double endOffset,
        int count) =>
        ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(steps, startOffset, endOffset, count);

    [Theory]
    [InlineData("SLOW")]
    [InlineData("CWE60")]
    [InlineData("CWF60")]
    [InlineData("SLOW", true)]
    [InlineData("CWE60", true)]
    [InlineData("CWF60", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false) =>
        EveryMinimumOfThePass_IsListedWithinTheBounds(fixture, readRangeRateOverWindow);
}

/// <summary>
/// The rows of the dense search on the fold of the range rate at mixed steps (CWFM) and the pass of <c>y0</c> = 1490 m at
/// 60 s steps (CW60), against CSPICE on their SPKs.
/// Their references and assertions are those of <see cref="ConjunctionDensePassTestBase"/>.
/// </summary>
public class ConjunctionDensePassCTests : ConjunctionDensePassTestBase, IClassFixture<CwPassFoldMixedStepCase>,
    IClassFixture<CwPass60StepCase>
{
    private readonly CwPassFoldMixedStepCase _cwFoldMixedCase;

    public ConjunctionDensePassCTests(CwPassFoldMixedStepCase cwFoldMixedCase, CwPass60StepCase cw60Case,
        ITestOutputHelper output)
        : base(output, ("CWFM", cwFoldMixedCase), ("CW60", cw60Case))
    {
        _cwFoldMixedCase = cwFoldMixedCase;
    }

    [Theory]
    [InlineData("60 s", 0.0, 10800.0, 4)]
    [InlineData("60 s", 1000.0, 2000.0, 2)]
    [InlineData("60 s", 1200.0, 1800.0, 2)]
    [InlineData("60 s", 1300.0, 1600.0, 2)]
    [InlineData("60 s", 1400.0, 1700.0, 1)]
    [InlineData("60 s", 1450.0, 1600.0, 1)]
    public void Dense_TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(string steps, double start, double end, int count) =>
        TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(steps, start, end, count);

    [Theory]
    [InlineData("CWFM", 7000.0, 7100.0)]
    [InlineData("CWFM", 0.0, 10800.0)]
    public void Dense_ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end) =>
        ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(fixture, start, end);

    [Theory]
    [InlineData(7000.0, 7100.0, 1)]
    [InlineData(6900.0, 7300.0, 2)]
    [InlineData(0.0, 10800.0, 3)]
    public void Dense_TheReadsOfTheRangeRate_AreMaximumEventSearchStepApart(double start, double end, int count)
    {
        // The fold of CWFM, positive over [7065.14, 7067.49] s with no minimum of the Hermite before it: at a
        // MaximumEventSearchStep of 1 s the reads over the window land in it, and AnalyzeAll lists every minimum of CSPICE
        // (the round-1 review of #628: the option reaches the dense search; the 60 s reads step over this fold, the
        // documented limit, which this test does not assert)
        var options = ReadOverWindowOptions with { MaximumEventSearchStep = TimeSpan.FromSeconds(1.0) };
        AssertSlowPassMatchesCspice(_cwFoldMixedCase, _cwFoldMixedCase.Seconds(start, end), count, options: options);
    }

    [Theory]
    [InlineData("CW60")]
    [InlineData("CW60", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false) =>
        EveryMinimumOfThePass_IsListedWithinTheBounds(fixture, readRangeRateOverWindow);

    [Theory]
    [InlineData("CWFM")]
    public void Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture) =>
        ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(fixture);
}

/// <summary>
/// The rows of the dense search on the passes of <c>y0</c> = 1490, 1495 and 1500 m at mixed steps (CWM90, CWN, CWM),
/// against CSPICE on their SPKs.
/// Their references and assertions are those of <see cref="ConjunctionDensePassTestBase"/>.
/// </summary>
public class ConjunctionDensePassDTests : ConjunctionDensePassTestBase, IClassFixture<CwPassMixedStep1490Case>,
    IClassFixture<CwPassMixedStep1495Case>, IClassFixture<CwPassMixedStep1500Case>
{
    public ConjunctionDensePassDTests(CwPassMixedStep1490Case cwMixed1490Case, CwPassMixedStep1495Case cwMixed1495Case,
        CwPassMixedStep1500Case cwMixed1500Case, ITestOutputHelper output)
        : base(output, ("CWM90", cwMixed1490Case), ("CWN", cwMixed1495Case), ("CWM", cwMixed1500Case))
    {
    }

    [Theory]
    [InlineData("CWM", 6969.70, 7089.70, 1)]
    [InlineData("CWM", 0.0, 10800.0, 3)]
    [InlineData("CWM90", 6970.31, 7171.31, 1)]
    [InlineData("CWM90", 0.0, 10800.0, 4)]
    [InlineData("CWN", 97.86, 1507.86, 2)]
    [InlineData("CWN", 0.0, 10800.0, 4)]
    public void Dense_MinimumWithoutASeedNearIt_IsListed(string fixture, double start, double end, int count) =>
        MinimumWithoutASeedNearIt_IsListed(fixture, start, end, count);

    [Theory]
    [InlineData("CWM", 6969.70, 7089.70)]
    [InlineData("CWM", 0.0, 10800.0)]
    [InlineData("CWM90", 6970.31, 7171.31)]
    [InlineData("CWM90", 0.0, 10800.0)]
    [InlineData("CWN", 97.86, 1507.86)]
    [InlineData("CWN", 0.0, 10800.0)]
    public void Dense_ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end) =>
        ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(fixture, start, end);

    [Theory]
    [InlineData("CWM", 6969.70, 7089.70)]
    [InlineData("CWM", 0.0, 10800.0)]
    public void Dense_ByDefault_TheRangeRateIsNotReadOverTheWindow(string fixture, double start, double end) =>
        ByDefault_TheRangeRateIsNotReadOverTheWindow(fixture, start, end);

    [Theory]
    [InlineData("CWM", true)]
    [InlineData("CWM90", true)]
    [InlineData("CWN", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false) =>
        EveryMinimumOfThePass_IsListedWithinTheBounds(fixture, readRangeRateOverWindow);

    [Theory]
    [InlineData("CWM")]
    [InlineData("CWM90")]
    [InlineData("CWN")]
    public void Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture) =>
        ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(fixture);
}

/// <summary>
/// The rows of the dense search on the slow pass at mixed steps (SLOWX), the passes of <c>y0</c> = 1490 and 1500 m at the
/// default steps (CWD, CWE) and of <c>y0</c> = 1490 m at fixed 600 s steps (CW600), against CSPICE on their SPKs.
/// Their references and assertions are those of <see cref="ConjunctionDensePassTestBase"/>.
/// </summary>
public class ConjunctionDensePassETests : ConjunctionDensePassTestBase, IClassFixture<MixedStepSlowPassEncounterCase>,
    IClassFixture<CwPassDefaultStepCase>, IClassFixture<CwPass1500DefaultStepCase>, IClassFixture<CwPassStep600Case>
{
    private readonly MixedStepSlowPassEncounterCase _mixedStepSlowCase;

    public ConjunctionDensePassETests(MixedStepSlowPassEncounterCase mixedStepSlowCase, CwPassDefaultStepCase cwDefaultCase,
        CwPass1500DefaultStepCase cw1500DefaultCase, CwPassStep600Case cw600Case, ITestOutputHelper output)
        : base(output, ("SLOWX", mixedStepSlowCase), ("CWD", cwDefaultCase), ("CWE", cw1500DefaultCase),
            ("CW600", cw600Case))
    {
        _mixedStepSlowCase = mixedStepSlowCase;
    }

    [Theory]
    [InlineData(-400.0, 1400.0)]
    [InlineData(-120.0, 1400.0)]
    [InlineData(-232.0, 1268.0)]
    public void Dense_MinimumPastTheWideningsOfItsSeeds_IsListed(double startOffset, double endOffset)
    {
        // The windows about the third minimum of the mixed-step slow pass (t0 + 5761.03 s) where the seeds of the polynomial search lie
        // three candidates or more from the zero of the range rate of the states: the minimum is no longer kept unrefined
        // at a seed, it is reached by following the range rate over the candidates (before, AnalyzeAll listed the seeds,
        // range rates of up to 1.2e-5 m/s; dropping them without following the range rate listed nothing)
        var crossing = _mixedStepSlowCase;
        var minimum = CspiceLocalMinima(crossing.Seconds(4000.0, 7000.0), crossing).Single().Epoch;
        var window = new Window(minimum.AddSeconds(startOffset), minimum.AddSeconds(endOffset));
        AssertSlowPassMatchesCspice(crossing, window, 1);
    }

    [Theory]
    [InlineData("default", 0.0, 10800.0, 4)]
    [InlineData("default", 1000.0, 2000.0, 2)]
    [InlineData("default", 1200.0, 1800.0, 2)]
    [InlineData("default", 1300.0, 1600.0, 2)]
    [InlineData("default", 1400.0, 1700.0, 1)]
    [InlineData("default", 1450.0, 1600.0, 1)]
    public void Dense_TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(string steps, double start, double end, int count) =>
        TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(steps, start, end, count);

    [Theory]
    [InlineData("default")]
    public void Dense_TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(string steps) =>
        TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(steps);

    [Theory]
    [InlineData("CW600", 0.0, 10800.0, 4)]
    [InlineData("CW600", 1200.0, 1800.0, 2)]
    public void Dense_MinimumWithoutASeedNearIt_IsListed(string fixture, double start, double end, int count) =>
        MinimumWithoutASeedNearIt_IsListed(fixture, start, end, count);

    [Theory]
    [InlineData("CW600", 0.0, 10800.0)]
    [InlineData("CW600", 1200.0, 1800.0)]
    public void Dense_ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end) =>
        ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(fixture, start, end);

    [Theory]
    [InlineData("CW600", 1200.0, 1800.0)]
    [InlineData("CW600", 0.0, 10800.0)]
    public void Dense_ByDefault_TheRangeRateIsNotReadOverTheWindow(string fixture, double start, double end) =>
        ByDefault_TheRangeRateIsNotReadOverTheWindow(fixture, start, end);

    [Theory]
    [InlineData("SLOWX")]
    [InlineData("CWD")]
    [InlineData("CWE")]
    [InlineData("SLOWX", true)]
    [InlineData("CWD", true)]
    [InlineData("CWE", true)]
    [InlineData("CW600", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false) =>
        EveryMinimumOfThePass_IsListedWithinTheBounds(fixture, readRangeRateOverWindow);

    [Theory]
    [InlineData("CW600")]
    public void Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture) =>
        ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(fixture);
}
