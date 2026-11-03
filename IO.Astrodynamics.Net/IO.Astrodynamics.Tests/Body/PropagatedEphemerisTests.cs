using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The stored states in arcs (<see cref="PropagatedEphemeris"/>), the read over sorted states
/// (<see cref="Lagrange.InterpolateSorted"/>) and the managed SPK writer (<see cref="IO.Astrodynamics.Kernels.SpkWriter"/>) on
/// synthetic states (#540, #522): the rules of the arcs, the read of the former sort to the bit, and the writer against
/// <c>spkezr</c> and, on Linux, against CSPICE's own <c>spkw09</c> and <c>dafus</c>.
/// </summary>
public class PropagatedEphemerisTests
{
    private readonly ITestOutputHelper _output;

    public PropagatedEphemerisTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static readonly Time T0 = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
    private static readonly CelestialBody Earth = new(PlanetsAndMoons.EARTH, Frame.ICRF, T0);

    // A motion whose Y velocity is vy
    private static StateVector State(double t, double vy = 0.0) =>
        new(new Vector3(7.0e6 + 100.0 * t, -3.0e5 + vy * t, 2.0e3 * System.Math.Sin(t / 100.0)),
            new Vector3(100.0, vy, 20.0 * System.Math.Cos(t / 100.0)), Earth, T0.AddSeconds(t), Frame.ICRF);

    // An impulse that takes the Y velocity from one value to another
    private static ImpulseBoundary Impulse(double t, double vyBefore, double vyAfter) => new(T0.AddSeconds(t), State(t, vyBefore), State(t, vyAfter));

    [Fact]
    public void Empty_HasNoStateAndNoSpan()
    {
        var ephemeris = new PropagatedEphemeris();
        Assert.Equal(0, ephemeris.Count);
        Assert.Null(ephemeris.First);
        Assert.Null(ephemeris.Span);
        Assert.Empty(ephemeris.All);
        Assert.Empty(ephemeris.Arcs);
        Assert.Throws<InvalidOperationException>(() => ephemeris.Interpolate(T0));
        Assert.Throws<ArgumentNullException>(() => ephemeris.Set(null, Array.Empty<ImpulseBoundary>(), null));
        Assert.Throws<ArgumentNullException>(() => ephemeris.Set(Array.Empty<StateVector>(), null, null));
        Assert.Throws<ArgumentNullException>(() => ephemeris.Add(null));
        ephemeris.Set(Array.Empty<StateVector>(), Array.Empty<ImpulseBoundary>(), null);
        Assert.Equal(0, ephemeris.Count);
    }

    [Fact]
    public void WithoutAnImpulse_OneArc_NeverFilled()
    {
        // Three outputs, fewer than the minimum of an arc: kept as they are, as before 11.0.0
        var outputs = new[] { State(0.0), State(10.0), State(20.0) };
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Set(outputs, Array.Empty<ImpulseBoundary>(), _ => throw new InvalidOperationException("filled"));
        Assert.Single(ephemeris.Arcs);
        Assert.Equal(outputs, ephemeris.Arcs[0]);
        Assert.Equal(new Window(T0, T0.AddSeconds(20.0)), ephemeris.Span);
        Assert.Same(outputs[0], ephemeris.First);
        ephemeris.Clear();
        Assert.Equal(0, ephemeris.Count);
    }

    [Fact]
    public void Impulses_SplitTheArcs_DropTheOutputsAtTheImpulse()
    {
        // Outputs every 10 s; impulses at 50 s (an output there and one 1e-7 s after), at 120.0000015 s (an output 1.5 µs
        // before it) and at 200 s, the end (an arc of zero length)
        static double Vy(double t) => t <= 50.0 ? 0.0 : t <= 120.0000015 ? 1.0 : t <= 200.0 ? 3.0 : 6.0;
        var outputs = Enumerable.Range(0, 21).Select(i => State(10.0 * i, Vy(10.0 * i))).ToList();
        outputs.Insert(6, State(50.0 + 1e-7, 1.0));
        outputs = outputs.OrderBy(x => x.Epoch).ToList();
        var impulses = new[] { Impulse(50.0, 0.0, 1.0), Impulse(120.0000015, 1.0, 3.0), Impulse(200.0, 3.0, 6.0) };
        var filled = new List<Time>();
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Set(outputs, impulses, t =>
        {
            filled.Add(t);
            return State((t - T0).TotalSeconds);
        });

        var arcs = ephemeris.Arcs;
        Assert.Equal(3, arcs.Count);
        // [0, 50): 5 outputs and the state before the first impulse
        Assert.Equal(new[] { 0.0, 10.0, 20.0, 30.0, 40.0, 50.0 }, arcs[0].Select(x => (x.Epoch - T0).TotalSeconds));
        Assert.Same(impulses[0].Before, arcs[0][^1]);
        // (50, 120.0000015): the state after, the outputs 60 to 110 (120 dropped), the state before the second impulse
        Assert.Same(impulses[0].After, arcs[1][0]);
        Assert.Equal(new[] { 60.0, 70.0, 80.0, 90.0, 100.0, 110.0 }, arcs[1].Skip(1).Take(6).Select(x => System.Math.Round((x.Epoch - T0).TotalSeconds, 6)));
        Assert.Same(impulses[1].Before, arcs[1][^1]);
        // (120.0000015, 200]: the state after, the outputs 130 to 190, the state before the last impulse at the end; the arc
        // after it, of zero length, is not stored
        Assert.Same(impulses[1].After, arcs[2][0]);
        Assert.Same(impulses[2].Before, arcs[2][^1]);
        Assert.Equal(9, arcs[2].Length);
        Assert.Empty(filled);
        Assert.Equal(arcs.Sum(a => a.Length), ephemeris.Count);
        Assert.Equal(ephemeris.Count, ephemeris.All.Length);

        // The read at an impulse: the state after it; just before it: the earlier arc
        Assert.Same(impulses[0].After, ephemeris.Interpolate(T0.AddSeconds(50.0)));
        Assert.Equal(0.0, ephemeris.Interpolate(T0.AddSeconds(50.0 - 1e-3)).Velocity.Y);
        Assert.Equal(1.0, ephemeris.Interpolate(T0.AddSeconds(50.0 + 1e-3)).Velocity.Y, 12);
        Assert.Equal(3.0, ephemeris.Interpolate(T0.AddSeconds(120.0000015 + 1e-3)).Velocity.Y, 12);
        // Before every arc, the first; after every arc (past the impulse at the end), the last
        Assert.Equal(0.0, ephemeris.Interpolate(T0.AddSeconds(-5.0)).Velocity.Y);
        Assert.Equal(3.0, ephemeris.Interpolate(T0.AddSeconds(205.0)).Velocity.Y, 9);
    }

    [Fact]
    public void ShortArcs_AreFilled_TwoImpulsesAtOneEpoch_LeaveNoArcBetween()
    {
        var outputs = Enumerable.Range(0, 11).Select(i => State(60.0 * i)).ToArray();
        // An impulse 70 s in: the first arc holds 2 outputs and the state before it; two impulses at 400 s: no arc between
        var impulses = new[] { Impulse(70.0, 0.0, 1.0), Impulse(400.0, 1.0, 2.0), Impulse(400.0, 2.0, 3.0) };
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Set(outputs, impulses, t => State((t - T0).TotalSeconds));
        var arcs = ephemeris.Arcs;
        Assert.Equal(3, arcs.Count);
        Assert.Equal(PropagatedEphemeris.FilledArcStates, arcs[0].Length);
        Assert.Same(outputs[0], arcs[0][0]);
        Assert.Same(impulses[0].Before, arcs[0][^1]);
        for (int i = 1; i < arcs[0].Length; i++) Assert.True(arcs[0][i].Epoch > arcs[0][i - 1].Epoch);
        Assert.Same(impulses[1].Before, arcs[1][^1]);
        Assert.Same(impulses[2].After, arcs[2][0]);
        Assert.Same(impulses[2].After, ephemeris.Interpolate(T0.AddSeconds(400.0)));

        // Without a fill, a short arc keeps its states
        ephemeris.Set(outputs, impulses, null);
        Assert.Equal(3, ephemeris.Arcs[0].Length);

        // Two impulses 1.5 µs apart are one epoch: no arc between them
        var close = new[] { Impulse(400.0, 0.0, 1.0), Impulse(400.0000015, 1.0, 2.0) };
        ephemeris.Set(outputs, close, t => State((t - T0).TotalSeconds));
        Assert.Equal(2, ephemeris.Arcs.Count);
        Assert.Same(close[0].Before, ephemeris.Arcs[0][^1]);
        Assert.Same(close[1].After, ephemeris.Arcs[1][0]);
    }

    [Fact]
    public void Add_ToTheArcOfTheEpoch()
    {
        var ephemeris = new PropagatedEphemeris();
        // Without arcs: one arc, in epoch order, a state at the same epoch replaced
        ephemeris.Add(new[] { State(20.0), State(0.0), State(10.0) });
        Assert.Single(ephemeris.Arcs);
        Assert.Equal(new[] { 0.0, 10.0, 20.0 }, ephemeris.All.Select(x => (x.Epoch - T0).TotalSeconds));
        var replacement = State(10.0, 5.0);
        ephemeris.Add(new[] { replacement });
        Assert.Equal(3, ephemeris.Count);
        Assert.Same(replacement, ephemeris.Arcs[0][1]);
        ephemeris.Add(Array.Empty<StateVector>());
        Assert.Equal(3, ephemeris.Count);

        // With arcs: the later one at an impulse, the first before them, the last after them
        var outputs = Enumerable.Range(0, 21).Select(i => State(10.0 * i)).ToArray();
        var impulses = new[] { Impulse(100.0, 0.0, 1.0) };
        ephemeris.Set(outputs, impulses, null);
        var at = State(100.0, 7.0);
        ephemeris.Add(new[] { State(-5.0), State(55.0), at, State(155.0), State(250.0) });
        var arcs = ephemeris.Arcs;
        Assert.Equal(2, arcs.Count);
        Assert.Equal(-5.0, (arcs[0][0].Epoch - T0).TotalSeconds);
        Assert.Contains(arcs[0], x => x.Epoch == T0.AddSeconds(55.0));
        Assert.Same(at, arcs[1][0]);
        Assert.Contains(arcs[1], x => x.Epoch == T0.AddSeconds(155.0));
        Assert.Equal(250.0, (arcs[1][^1].Epoch - T0).TotalSeconds);
        Assert.Same(impulses[0].Before, arcs[0][^1]);
    }

    /// <summary>
    /// An output closer to an impulse than a tenth of the output step is dropped, on either side, and kept beyond (review of
    /// PR 1 of #554, ARC-1: beside the impulse's state it made the interpolation ill-conditioned, 301 m in the SPK 3 µs from
    /// the impulse); the first and the last output, the ends of the window, are kept in their own arc with the impulse's
    /// state, unfilled when shorter than 15 µs (ARC-2: they were dropped with the arc, the window no longer covered).
    /// </summary>
    [Fact]
    public void OutputsNearAnImpulse_DroppedWithinATenthOfTheStep_TheEndsOfTheWindowKept()
    {
        var outputs = Enumerable.Range(0, 21).Select(i => State(10.0 * i)).ToArray();
        var filled = new List<Time>();
        StateVector Fill(Time t)
        {
            filled.Add(t);
            return State((t - T0).TotalSeconds);
        }

        // 0.9 s after the output at 50 s (dropped, 0.09 step) and 1.1 s before the one at 100 s (kept, 0.11 step); 1.1 s after
        // the output at 150 s (kept) and 0.9 s before the one at 160 s (dropped)
        var ephemeris = new PropagatedEphemeris();
        var impulses = new[] { Impulse(50.9, 0.0, 1.0), Impulse(98.9, 1.0, 2.0), Impulse(151.1, 2.0, 3.0), Impulse(159.1, 3.0, 4.0) };
        ephemeris.Set(outputs, impulses, Fill);
        var arcs = ephemeris.Arcs;
        Assert.Equal(5, arcs.Count);
        Assert.Equal(new[] { 0.0, 10.0, 20.0, 30.0, 40.0, 50.9 }, arcs[0].Select(x => System.Math.Round((x.Epoch - T0).TotalSeconds, 6)));
        Assert.Equal(new[] { 50.9, 60.0, 70.0, 80.0, 90.0, 98.9 }, arcs[1].Select(x => System.Math.Round((x.Epoch - T0).TotalSeconds, 6)));
        Assert.Equal(new[] { 98.9, 100.0, 110.0, 120.0, 130.0, 140.0, 150.0, 151.1 }, arcs[2].Select(x => System.Math.Round((x.Epoch - T0).TotalSeconds, 6)));
        // Between 151.1 and 159.1 s: no output left, filled
        Assert.Equal(PropagatedEphemeris.FilledArcStates, arcs[3].Length);
        Assert.Equal(new[] { 159.1, 170.0, 180.0, 190.0, 200.0 }, arcs[4].Select(x => System.Math.Round((x.Epoch - T0).TotalSeconds, 6)));
        Assert.Equal(PropagatedEphemeris.FilledArcStates - 2, filled.Count);

        // Impulses 1 µs after the start and 1 µs before the end: the first and the last output keep an arc of two states
        filled.Clear();
        var ends = new[] { Impulse(1e-6, 0.0, 1.0), Impulse(200.0 - 1e-6, 1.0, 2.0) };
        ephemeris.Set(outputs, ends, Fill);
        arcs = ephemeris.Arcs;
        Assert.Equal(3, arcs.Count);
        Assert.Equal(new[] { outputs[0], ends[0].Before }, arcs[0]);
        Assert.Equal(new[] { ends[1].After, outputs[^1] }, arcs[2]);
        Assert.Equal(new Window(T0, T0.AddSeconds(200.0)), ephemeris.Span);
        Assert.Empty(filled);
        Assert.Same(outputs[0], ephemeris.Interpolate(T0));
        Assert.Same(outputs[^1], ephemeris.Interpolate(T0.AddSeconds(200.0)));

        // An impulse that the solution does not carry (no state after it): it closes the last arc, the outputs from its epoch
        // on are not stored
        var uncarried = new ImpulseBoundary(T0.AddSeconds(195.0), State(195.0), null);
        ephemeris.Set(outputs, new[] { uncarried }, Fill);
        Assert.Single(ephemeris.Arcs);
        Assert.Same(uncarried.Before, ephemeris.Arcs[0][^1]);
        Assert.Equal(new Window(T0, T0.AddSeconds(195.0)), ephemeris.Span);

        // 20 µs from the end: filled, the states 1.3 µs apart
        ephemeris.Set(outputs, new[] { Impulse(200.0 - 2e-5, 0.0, 1.0) }, Fill);
        Assert.Equal(PropagatedEphemeris.FilledArcStates, ephemeris.Arcs[1].Length);
    }

    /// <summary>
    /// Two impulses 2.1 µs apart in 2026, where the TDB seconds of the SPK are doubles of 1.19e-7 s: the arc between them keeps
    /// its two states, unfilled (16 states 1.4e-7 s apart collided as doubles and the writer threw, review of PR 1 of #554,
    /// spk-F2), and the SPK is written; an arc whose two states are one double (1 tick apart) is left out of the file, which
    /// is then the file without it, byte for byte.
    /// </summary>
    [Fact]
    public void ShortArcs_DistinctEpochsOfTheSpk()
    {
        var t2026 = new Time(2026, 6, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        StateVector At(double t, double vy = 0.0) => new(State(t, vy).Position, State(t, vy).Velocity, Earth, t2026.AddSeconds(t), Frame.ICRF);
        var outputs = Enumerable.Range(0, 11).Select(i => At(10.0 * i)).ToArray();
        var impulses = new[] { new ImpulseBoundary(t2026.AddSeconds(50.0), At(50.0), At(50.0, 1.0)), new ImpulseBoundary(t2026.AddSeconds(50.0000021), At(50.0000021, 1.0), At(50.0000021, 2.0)) };
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Set(outputs, impulses, t => At((t - t2026).TotalSeconds));
        Assert.Equal(3, ephemeris.Arcs.Count);
        Assert.Equal(2, ephemeris.Arcs[1].Length);
        var file = new FileInfo(Path.Combine("Data/User/PropagatedArcs540", "short2026.bsp"));
        SpiceAPI.Instance.WriteEphemerisArcs(file, -5403, ephemeris.Arcs);
        Assert.True(file.Exists);
        file.Delete();

        // Two epochs one tick apart that are one TDB double
        double Seconds(Time t) => t.ToTDB().TimeSpanFromJ2000().TotalSeconds;
        var t0 = t2026.AddSeconds(100.0);
        while (Seconds(t0) != Seconds(t0.AddSeconds(1e-7))) t0 = t0.AddSeconds(1e-7);
        var a0 = new[] { new StateVector(new Vector3(7.0e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), Earth, t0.AddSeconds(-20.0), Frame.ICRF),
            new StateVector(new Vector3(7.0e6, 1.5e5, 0.0), new Vector3(0.0, 7.5e3, 0.0), Earth, t0, Frame.ICRF) };
        var a1 = new[] { new StateVector(new Vector3(7.0e6, 1.5e5, 0.0), new Vector3(0.0, 7.6e3, 0.0), Earth, t0, Frame.ICRF),
            new StateVector(new Vector3(7.0e6, 1.5e5 + 7.6e-4, 0.0), new Vector3(0.0, 7.6e3, 0.0), Earth, t0.AddSeconds(1e-7), Frame.ICRF) };
        Assert.Equal(IO.Astrodynamics.Kernels.SpkWriter.BuildType9(-5403, 1, new[] { a0 }), IO.Astrodynamics.Kernels.SpkWriter.BuildType9(-5403, 1, new[] { a0, a1 }));
        Assert.Throws<ArgumentException>(() => IO.Astrodynamics.Kernels.SpkWriter.BuildType9(-5403, 1, new[] { a1 }));
    }

    /// <summary>
    /// A write the writer refuses (no state, an arc of one state, epochs not increasing) leaves an existing SPK as it was: the
    /// file is built and checked before the existing one is unloaded and deleted (review of PR 1 of #554, spk-F3; before
    /// 11.0.0 the native writer refused these before its delete).
    /// </summary>
    [Fact]
    public void RefusedWrite_KeepsTheExistingFile()
    {
        var directory = Path.Combine("Data/User/PropagatedArcs540", "kept");
        Directory.CreateDirectory(directory);
        var file = new FileInfo(Path.Combine(directory, "kept.bsp"));
        var good = new[] { State(0.0), State(10.0), State(20.0) };
        SpiceAPI.Instance.WriteEphemerisArcs(file, -5404, new[] { good });
        var bytes = File.ReadAllBytes(file.FullName);
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteEphemerisArcs(file, -5404, Array.Empty<StateVector[]>()));
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteEphemerisArcs(file, -5404, new[] { new[] { State(0.0) } }));
        Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.WriteEphemerisArcs(file, -5404, new[] { new[] { State(10.0), State(0.0) } }));
        // A spacecraft with one stored state
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-5404, "PE5404", 1000.0, 3000.0, new Clock("PE5404_CLK", 65536), State(0.0));
        spacecraft.AddPropagatedStates(State(0.0));
        Assert.Throws<ArgumentException>(() => spacecraft.WriteEphemeris(file));
        file.Refresh();
        Assert.True(file.Exists);
        Assert.Equal(bytes, File.ReadAllBytes(file.FullName));
        Directory.Delete(directory, true);
    }

    /// <summary>
    /// A full summary record is followed by an empty one, as the DAF Required Reading lays out ("the final summary record is
    /// full, so new summary and name records will be added") and CSPICE writes it (review of PR 1 of #554, spk-F1: at 25
    /// segments the managed file had none, and CSPICE's append, <c>spkopa</c> (<c>dafopw</c>) then <c>spkw09</c>, failed in <c>dafena</c>
    /// with SPICE(BADSUBSCRIPT)). The file record's backward pointer names the last summary record, empty; on Linux, the files
    /// of 24 segments are byte for byte those of <c>spkw09</c>, those of 25 and 50 the same summaries and data, and CSPICE
    /// appends a segment to each.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(24, 2)]
    [InlineData(25, 4)]
    [InlineData(26, 4)]
    [InlineData(50, 6)]
    public void SpkWriter_FullSummaryRecord_FollowedByAnEmptyOne(int count, int lastSummaryRecord)
    {
        var directory = Path.Combine("Data/User/PropagatedArcs540", $"summaries{count}");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        var file = new FileInfo(Path.Combine(directory, "s.bsp"));
        var arcs = new List<StateVector[]>();
        double t = 0.0;
        for (int k = 0; k < count; k++)
        {
            var arc = new StateVector[5];
            for (int i = 0; i < arc.Length; i++)
            {
                arc[i] = State(t, k);
                if (i < arc.Length - 1) t += 10.0;
            }

            arcs.Add(arc);
        }

        SpiceAPI.Instance.WriteEphemerisArcs(file, -5405, arcs);
        var bytes = File.ReadAllBytes(file.FullName);
        int forward = BitConverter.ToInt32(bytes, 76), backward = BitConverter.ToInt32(bytes, 80);
        Assert.Equal(2, forward);
        Assert.Equal(lastSummaryRecord, backward);
        // The last summary record: no next, its count of summaries count mod 25
        double next = BitConverter.ToDouble(bytes, (backward - 1) * 1024), summaries = BitConverter.ToDouble(bytes, (backward - 1) * 1024 + 16);
        Assert.Equal(0.0, next);
        Assert.Equal(count % 25, (int)summaries);
        if (OperatingSystem.IsLinux())
        {
            var reference = arcs.Select((arc, k) => (Earth.NaifId, System.Math.Min(7, arc.Length - 1), arc.Select(x => x.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds).ToArray(),
                arc.SelectMany(x => new[] { x.Position.X * 1e-3, x.Position.Y * 1e-3, x.Position.Z * 1e-3, x.Velocity.X * 1e-3, x.Velocity.Y * 1e-3, x.Velocity.Z * 1e-3 }).ToArray(),
                $"Seg{k + 1}")).ToList();
            var spkw09 = Path.Combine(directory, "s.spkw09.bsp");
            CspiceDirect.WriteType9(spkw09, -5405, reference);
            var expected = File.ReadAllBytes(spkw09);
            int differing = Enumerable.Range(0, System.Math.Min(bytes.Length, expected.Length)).Count(i => bytes[i] != expected[i]);
            _output.WriteLine($"MEASURE #540 writer, {count} segments: {bytes.Length} and {expected.Length} bytes (spkw09), {differing} differ");
            if (count < 25)
            {
                Assert.Equal(expected, bytes);
            }

            var segments = CspiceDirect.ReadSegments(file.FullName);
            var written = CspiceDirect.ReadSegments(spkw09);
            Assert.Equal(count, segments.Count);
            for (int k = 0; k < count; k++)
            {
                Assert.Equal((written[k].Start, written[k].End, written[k].Body, written[k].Center, written[k].Frame, written[k].Type),
                    (segments[k].Start, segments[k].End, segments[k].Body, segments[k].Center, segments[k].Frame, segments[k].Type));
                Assert.Equal(written[k].Data, segments[k].Data);
            }

            CspiceDirect.AppendType9(file.FullName, -5406, reference[0]);
            Assert.Equal(count + 1, CspiceDirect.ReadSegments(file.FullName).Count);
        }

        Directory.Delete(directory, true);
    }

    /// <summary>The read over sorted states is the former read (the sort of every state by distance) to the bit, at random
    /// epochs, at the states themselves and halfway between two of them, where the distances tie.</summary>
    [Fact]
    public void InterpolateSorted_IsTheFormerSortToTheBit()
    {
        var random = new Random(540);
        double t = 0.0;
        var states = new List<StateVector>();
        for (int i = 0; i < 300; i++)
        {
            t += 0.5 + 9.5 * random.NextDouble();
            states.Add(State(t));
        }

        var sorted = states.ToArray();
        var shuffled = sorted.OrderBy(_ => random.Next()).ToArray();
        var epochs = new List<Time>();
        for (int i = 0; i < 2000; i++) epochs.Add(T0.AddSeconds(-20.0 + (t + 40.0) * random.NextDouble()));
        epochs.AddRange(sorted.Select(x => x.Epoch));
        for (int i = 0; i + 1 < sorted.Length; i++) epochs.Add(sorted[i].Epoch + (sorted[i + 1].Epoch - sorted[i].Epoch) / 2);
        foreach (int k in new[] { 8, 3, 1 })
        {
            foreach (var epoch in epochs)
            {
                var former = Lagrange.Interpolate(shuffled.OrderBy(x => x.Epoch).ToArray(), epoch, k);
                var read = Lagrange.InterpolateSorted(sorted, epoch, k);
                Assert.Equal(former.Position, read.Position);
                Assert.Equal(former.Velocity, read.Velocity);
                Assert.Equal(former.Epoch, read.Epoch);
            }
        }

        // Fewer states than the window
        var few = sorted.Take(5).ToArray();
        Assert.Equal(Lagrange.Interpolate(few, T0.AddSeconds(3.0)).Position, Lagrange.InterpolateSorted(few, T0.AddSeconds(3.0)).Position);
    }

    [Fact]
    public void SpkWriter_RejectsWhatItCannotWrite()
    {
        var file = new FileInfo(Path.Combine("Data/User/PropagatedArcs540", "rejected.bsp"));
        var two = new[] { State(0.0), State(10.0) };
        Assert.Throws<ArgumentNullException>(() => IO.Astrodynamics.Kernels.SpkWriter.WriteType9(null, -540, 1, new[] { two }));
        Assert.Throws<ArgumentException>(() => IO.Astrodynamics.Kernels.SpkWriter.WriteType9(file, -540, 1, null));
        Assert.Throws<ArgumentException>(() => IO.Astrodynamics.Kernels.SpkWriter.WriteType9(file, -540, 1, Array.Empty<StateVector[]>()));
        Assert.Throws<ArgumentException>(() => IO.Astrodynamics.Kernels.SpkWriter.WriteType9(file, -540, 1, new[] { new[] { State(0.0) } }));
        Assert.Throws<ArgumentException>(() => IO.Astrodynamics.Kernels.SpkWriter.WriteType9(file, -540, 1, new[] { new[] { State(10.0), State(0.0) } }));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteEphemerisArcs(null, -540, new[] { two }));
        Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.WriteEphemerisArcs(file, -540, null));
        Assert.False(file.Exists);

        // A spacecraft without stored states has nothing to write
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-5401, "PE5401", 1000.0, 3000.0, new Clock("PE5401_CLK", 65536), State(0.0));
        Assert.Throws<ArgumentException>(() => spacecraft.WriteEphemeris(file));
        Assert.Throws<ArgumentNullException>(() => spacecraft.WriteEphemeris(null));
    }

    /// <summary>
    /// Sixty segments, three summary records, alternately of 37 states (no directory) and 250 (two directory epochs), each a
    /// rectilinear motion of its own, the segments meeting where they abut, written in a directory that does not exist:
    /// <c>spkezr</c> reads the motion of the segment of each epoch, the later one where two meet, to the resolution of the ET
    /// doubles of the nodes; an existing file is replaced, the caller's <see cref="FileInfo"/> refreshed.
    /// </summary>
    [Fact]
    public void SpkWriter_SixtySegments_ReadBySpkezr()
    {
        var directory = new DirectoryInfo(Path.Combine("Data/User/PropagatedArcs540", "sixty"));
        if (directory.Exists) directory.Delete(true);
        var file = new FileInfo(Path.Combine(directory.FullName, "sixty.bsp"));
        var arcs = new List<StateVector[]>();
        var expected = new List<(Time Epoch, Vector3 Position, Vector3 Velocity)>();
        var t0 = T0;
        for (int k = 0; k < 60; k++)
        {
            int n = k % 2 == 0 ? 37 : 250;
            var p0 = new Vector3(7.0e6 + 1000.0 * k, -2.0e6 * k, 3.0e5);
            var v = new Vector3(100.0 + k, -50.0 * k, 7.0);
            var arc = new StateVector[n];
            for (int i = 0; i < n; i++) arc[i] = new StateVector(p0 + v * (i * 3.7), v, Earth, t0.AddSeconds(i * 3.7), Frame.ICRF);
            for (int i = 0; i < n - 1; i++) expected.Add((t0.AddSeconds(i * 3.7 + 1.3), p0 + v * (i * 3.7 + 1.3), v));
            // Where two segments meet, the later one
            if (k > 0) expected.Add((t0, p0, v));
            arcs.Add(arc);
            t0 = arc[^1].Epoch;
        }

        // A file there already: replaced
        Directory.CreateDirectory(directory.FullName);
        File.WriteAllText(file.FullName, "not an SPK");
        Assert.True(file.Exists);
        SpiceAPI.Instance.WriteEphemerisArcs(file, -5402, arcs);
        Assert.True(file.Exists);
        Assert.True(file.Length > 1024);
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            double worst = 0.0, worstVelocity = 0.0;
            foreach (var (epoch, position, velocity) in expected)
            {
                var read = SpiceAPI.Instance.ReadEphemeris(epoch, 399, -5402, Frame.ICRF, Aberration.None).ToStateVector();
                worst = System.Math.Max(worst, (read.Position - position).Magnitude());
                worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - velocity).Magnitude());
            }

            _output.WriteLine($"MEASURE #540 writer, 60 segments: spkezr - motion {worst:E2} m, {worstVelocity:E2} m/s over {expected.Count} epochs");
            Assert.True(worst <= SixtySegmentsPosition, $"{worst:E3} m");
            Assert.True(worstVelocity <= SixtySegmentsVelocity, $"{worstVelocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }

        if (OperatingSystem.IsLinux())
        {
            // On Linux, where the native library exports CSPICE: dafus walks the 60 segments, those spkw09 writes from the same
            // segments. Past 25 segments CSPICE appends each new summary record after the data written so far, where the managed
            // writer puts every summary record first: both are DAFs (a list of summary records), the bytes differ, the summaries
            // and the data of the segments do not
            var segments = CspiceDirect.ReadSegments(file.FullName);
            Assert.Equal(60, segments.Count);
            var reference = arcs.Select((arc, k) => (Earth.NaifId, System.Math.Min(7, arc.Length - 1), arc.Select(x => x.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds).ToArray(),
                arc.SelectMany(x => new[] { x.Position.X * 1e-3, x.Position.Y * 1e-3, x.Position.Z * 1e-3, x.Velocity.X * 1e-3, x.Velocity.Y * 1e-3, x.Velocity.Z * 1e-3 }).ToArray(),
                $"Seg{k + 1}")).ToList();
            var spkw09 = Path.Combine(directory.FullName, "sixty.spkw09.bsp");
            CspiceDirect.WriteType9(spkw09, -5402, reference);
            var written = CspiceDirect.ReadSegments(spkw09);
            Assert.Equal(written.Count, segments.Count);
            for (int k = 0; k < segments.Count; k++)
            {
                Assert.Equal((written[k].Start, written[k].End, written[k].Body, written[k].Center, written[k].Frame, written[k].Type),
                    (segments[k].Start, segments[k].End, segments[k].Body, segments[k].Center, segments[k].Frame, segments[k].Type));
                Assert.Equal(written[k].Data, segments[k].Data);
            }
        }

        directory.Delete(true);
    }

    /// <summary>The motion of the synthetic segments read by <c>spkezr</c>: the ET doubles of the nodes and of the epochs read,
    /// 1.2e-7 s at 7.6e8 s, times up to 3 km/s. PROVISIONAL, Linux worst 6.97e-4 m and 1.37e-12 m/s.</summary>
    internal const double SixtySegmentsPosition = 1.4e-3;

    internal const double SixtySegmentsVelocity = 2.8e-12;
}
