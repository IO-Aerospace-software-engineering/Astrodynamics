// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Tests.Body;
using Xunit;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The window reads of <see cref="SpiceAPI.ReadEphemeris(Window, int, int, Frames.Frame, Aberration, TimeSpan)"/> and
/// <see cref="SpiceAPI.ReadOrientation"/> (#563): before 11.0.0 the native proxies stepped their epoch by epoch += step in
/// double TDB seconds, so that sample i drifted from start + i·step (1.43e-4 s after 6000 steps of 0.1 s) and the last
/// epoch of the window could be dropped, and <c>ReadOrientation</c> passed a buffer of 10000 records whatever the window,
/// 1.4 ms for a read of one epoch. The reads are now at start + i·step in the time frame of the window start (UTC steps for
/// a UTC window: review of #563), i from 0 while it is within the window (exact tick arithmetic), the references: the read of
/// each such epoch alone, the closed form of the grid, and CSPICE (<c>spkezr_c</c>, <c>ckgpav_c</c>; Validation).
/// </summary>
public partial class SpiceApiTest
{
    // 2026-04-01 00:00:00 TDB, the start of the measurement of #563
    private static readonly TimeSystem.Time WindowReadStart = TimeSystem.Time.CreateTDB(828273600.0);

    // The PHSRM CK of the tests, within its coverage (ReadOrientationFromExternalCKKernel)
    private static readonly TimeSystem.Time PhsrmStart = TimeSystem.Time.CreateTDB(374100000.0);

    [Theory]
    [InlineData(600.0, 0.1, 6001)]
    [InlineData(10798.8, 1.2, 9000)]
    [InlineData(1200.05, 0.1, 12001)]
    [InlineData(0.0, 1.0, 1)]
    public void ReadEphemeris_WindowRead_IsTheReadAtEachStartPlusIStep(double length, double step, int count)
    {
        // The Moon from the Earth in J2000: each state is the read of its epoch alone, start + i·step in TDB, labelled with it;
        // the last epoch of the window is read, and a read of more than 10000 epochs (in several native calls) is continuous
        var stepSize = TimeSpan.FromSeconds(step);
        var window = new Window(WindowReadStart, WindowReadStart.AddSeconds(length));
        var states = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, Aberration.LT, stepSize).Cast<StateVector>().ToArray();
        _output.WriteLine($"MEASURE #563 ReadEphemeris {length} s at {step} s: {states.Length} states, last at {states[^1].Epoch}");
        Assert.Equal(count, states.Length);
        for (int i = 0; i < count; i++)
        {
            var epoch = WindowReadStart + stepSize * i;
            LabelAssert.Equal(epoch, states[i].Epoch);
            var single = (StateVector)SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.LT);
            Assert.Equal(single.Position, states[i].Position);
            Assert.Equal(single.Velocity, states[i].Velocity);
        }
    }

    // The first of each month of 2026 at 00:00:00 UTC: TDB − TT, periodic, makes a UTC window of 600 s from 2 ticks shorter to
    // 2 ticks longer in TDB (review of #563)
    public static TheoryData<int> Months2026 => new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    [Theory]
    [MemberData(nameof(Months2026))]
    public void ReadEphemeris_UtcWindowRead_IsTheReadAtEachStartPlusIStepInUtc(int month)
    {
        // The grid of a UTC window is start + i·step in UTC, its end included (closed form: 600 s at 0.1 s, 6001 epochs, the
        // last at the end), each state the read of its epoch alone, labelled with it in UTC. Before, the grid was taken in TDB
        // and a window shorter in TDB than in UTC lost its end (6000 states in July and October 2026, the last at
        // 00:09:59.9000002Z)
        var start = new TimeSystem.Time(2026, month, 1, TimeSystem.TimeScale.UTC);
        var stepSize = TimeSpan.FromSeconds(0.1);
        var window = new Window(start, start.AddSeconds(600.0));
        var states = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, Aberration.LT, stepSize).Cast<StateVector>().ToArray();
        _output.WriteLine($"MEASURE #563 ReadEphemeris UTC 2026-{month:D2}: {states.Length} states, last at {states[^1].Epoch}");
        Assert.Equal(6001, states.Length);
        LabelAssert.Equal(window.EndDate, states[^1].Epoch);
        for (int i = 0; i < states.Length; i++)
        {
            var epoch = start + stepSize * i;
            LabelAssert.Equal(epoch, states[i].Epoch);
            Assert.Equal(TimeSystem.TimeScale.UTC, states[i].Epoch.Scale);
            var single = (StateVector)SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.LT);
            Assert.Equal(single.Position, states[i].Position);
            Assert.Equal(single.Velocity, states[i].Velocity);
        }
    }

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData(7)]
    [InlineData(10)]
    public void ReadEphemeris_UtcWindowRead_EqualsSpkezrAtEachUtcEpoch(int month)
    {
        // Against CSPICE, for the two months whose UTC window is shortest in TDB: each state is spkezr_c's at the TDB seconds of
        // start + i·step in UTC, bit for bit. Those seconds are the library's TDB − TT, its one-term series, which differs from
        // SPICE's (str2et_c) by up to 1.06 µs (docs/reference/time-and-windows.md); measured here, and bounded by twice the
        // worst on Linux x64 (1.073e-6 s, CI run 44225490669), rounded up at the second significant digit: a record of the
        // Validation category, which CI runs on Linux x64 alone
        var start = new TimeSystem.Time(2026, month, 1, TimeSystem.TimeScale.UTC);
        var stepSize = TimeSpan.FromSeconds(0.1);
        var window = new Window(start, start.AddSeconds(600.0));
        var states = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, Aberration.None, stepSize).Cast<StateVector>().ToArray();
        Assert.Equal(6001, states.Length);
        double worstEt = 0.0;
        for (int i = 0; i < states.Length; i++)
        {
            var epoch = start + stepSize * i;
            LabelAssert.Equal(epoch, states[i].Epoch);
            double et = epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds;
            double spiceEt = CspiceDirect.Str2et(epoch.DateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture));
            worstEt = System.Math.Max(worstEt, System.Math.Abs(et - spiceEt));
            var spkezr = CspiceDirect.Spkezr("301", et, "J2000", "NONE", "399");
            Assert.Equal(new IO.Astrodynamics.Math.Vector3(spkezr[0] * 1000.0, spkezr[1] * 1000.0, spkezr[2] * 1000.0), states[i].Position);
            Assert.Equal(new IO.Astrodynamics.Math.Vector3(spkezr[3] * 1000.0, spkezr[4] * 1000.0, spkezr[5] * 1000.0), states[i].Velocity);
        }

        _output.WriteLine($"MEASURE #563 UTC 2026-{month:D2}: worst |ET - str2et_c| {worstEt:E3} s");
        Assert.True(worstEt < TdbSeriesThreshold, $"{worstEt:E2} s");
    }

    private const double TdbSeriesThreshold = 2.2e-6;

    [Fact]
    [Trait("Category", "Validation")]
    public void ReadEphemeris_WindowRead_EqualsSpkezrAtEachStartPlusIStep()
    {
        var stepSize = TimeSpan.FromSeconds(0.1);
        var window = new Window(WindowReadStart, WindowReadStart.AddSeconds(600.0));
        var states = SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, Aberration.None, stepSize).Cast<StateVector>().ToArray();
        Assert.Equal(6001, states.Length);
        for (int i = 0; i < states.Length; i++)
        {
            double et = (WindowReadStart + stepSize * i).TimeSpanFromJ2000().TotalSeconds;
            var spkezr = CspiceDirect.Spkezr("301", et, "J2000", "NONE", "399");
            Assert.Equal(new IO.Astrodynamics.Math.Vector3(spkezr[0] * 1000.0, spkezr[1] * 1000.0, spkezr[2] * 1000.0), states[i].Position);
            Assert.Equal(new IO.Astrodynamics.Math.Vector3(spkezr[3] * 1000.0, spkezr[4] * 1000.0, spkezr[5] * 1000.0), states[i].Velocity);
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void WindowReads_StepNotPositive_Throw(double step)
    {
        // Before 11.0.0 a step of 0 threw the native layer's message and a negative step read 10000 epochs backwards (#563)
        var window = new Window(WindowReadStart, WindowReadStart.AddSeconds(60.0));
        var ephemeris = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpiceAPI.Instance.ReadEphemeris(window, 399, 301, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(step)));
        Assert.Equal("stepSize", ephemeris.ParamName);
        var orientation = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(step)));
        Assert.Equal("stepSize", orientation.ParamName);
    }

    [Theory]
    [InlineData(600.0, 0.1, 6001)]
    [InlineData(10798.8, 1.2, 9000)]
    [InlineData(1200.05, 0.1, 12001)]
    public void ReadOrientation_WindowRead_IsTheReadAtEachStartPlusIStep(double length, double step, int count)
    {
        // PHSRM's CK: each record is the read of its epoch alone, start + i·step in TDB (the CK's own epoch, which SPICE gives
        // through the clock); the last epoch of the window is read, and so is a window of more than 10000 epochs
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));
        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var stepSize = TimeSpan.FromSeconds(step);
            var tolerance = TimeSpan.FromSeconds(60.0);
            var window = new Window(PhsrmStart, PhsrmStart.AddSeconds(length));
            var records = SpiceAPI.Instance.ReadOrientation(window, -555, tolerance, Frames.Frame.ICRF, stepSize).ToArray();
            _output.WriteLine($"MEASURE #563 ReadOrientation {length} s at {step} s: {records.Length} records, last at {records[^1].Epoch}");
            Assert.Equal(count, records.Length);
            for (int i = 0; i < count; i++)
            {
                var epoch = PhsrmStart + stepSize * i;
                var single = SpiceAPI.Instance.ReadOrientation(new Window(epoch, epoch), -555, tolerance, Frames.Frame.ICRF, stepSize).Single();
                LabelAssert.Equal(single.Epoch, records[i].Epoch);
                Assert.Equal(single.Rotation, records[i].Rotation);
                Assert.Equal(single.AngularVelocity, records[i].AngularVelocity);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Theory]
    [InlineData(2012, 1)]
    [InlineData(2012, 4)]
    [InlineData(2012, 7)]
    [InlineData(2012, 10)]
    public void ReadOrientation_UtcWindowRead_ReadsEachStartPlusIStepInUtc(int year, int month)
    {
        // PHSRM's CK over a UTC window of 600 s at 0.1 s: 6001 records (closed form), record i the read of start + i·step in UTC
        // alone, labelled with the CK's epoch of that instant (sct2e_c of the clock ckgpav_c returns)
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));
        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var start = new TimeSystem.Time(year, month, 1, TimeSystem.TimeScale.UTC);
            var stepSize = TimeSpan.FromSeconds(0.1);
            var tolerance = TimeSpan.FromSeconds(60.0);
            var window = new Window(start, start.AddSeconds(600.0));
            var records = SpiceAPI.Instance.ReadOrientation(window, -555, tolerance, Frames.Frame.ICRF, stepSize).ToArray();
            _output.WriteLine($"MEASURE #563 ReadOrientation UTC {year}-{month:D2}: {records.Length} records, last at {records[^1].Epoch}");
            Assert.Equal(6001, records.Length);
            double worst = 0.0;
            for (int i = 0; i < records.Length; i++)
            {
                var epoch = start + stepSize * i;
                worst = System.Math.Max(worst, System.Math.Abs((records[i].Epoch - epoch.ToTDB()).TotalSeconds));
                var single = SpiceAPI.Instance.ReadOrientation(new Window(epoch, epoch), -555, tolerance, Frames.Frame.ICRF, stepSize).Single();
                LabelAssert.Equal(single.Epoch, records[i].Epoch);
                Assert.Equal(single.Rotation, records[i].Rotation);
                Assert.Equal(single.AngularVelocity, records[i].AngularVelocity);
            }

            _output.WriteLine($"MEASURE #563 ReadOrientation UTC {year}-{month:D2}: worst record epoch - start - i·step {worst:E3} s");
            Assert.True(worst <= RecordEpochThreshold, $"{worst:E2} s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void ReadOrientation_WindowRead_EqualsCkgpavAtEachStartPlusIStep()
    {
        // Against CSPICE (review of #563): at each start + i·step in TDB, sce2c_c, ckgpav_c in J2000 with the tolerance in ticks
        // the proxy takes (sce2c_c over 60 s at the first epoch) and sct2e_c of the record's clock: the epoch to the tick, the
        // angular velocity bit for bit, the C-matrix as the rotation angle between the axes it and the record's quaternion map
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));
        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var stepSize = TimeSpan.FromSeconds(0.1);
            var window = new Window(PhsrmStart, PhsrmStart.AddSeconds(600.0));
            var records = SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.FromSeconds(60.0), Frames.Frame.ICRF, stepSize).ToArray();
            Assert.Equal(6001, records.Length);
            double et0 = PhsrmStart.TimeSpanFromJ2000().TotalSeconds;
            double toleranceTicks = System.Math.Abs(CspiceDirect.Sce2c(-555, et0 + 60.0) - CspiceDirect.Sce2c(-555, et0));
            double worstAngle = 0.0, worstEpoch = 0.0;
            for (int i = 0; i < records.Length; i++)
            {
                var epoch = PhsrmStart + stepSize * i;
                double et = epoch.TimeSpanFromJ2000().TotalSeconds;
                var reference = CspiceDirect.Ckgpav(-555, CspiceDirect.Sce2c(-555, et), toleranceTicks, "J2000");
                Assert.NotNull(reference);
                var (cmat, av, recordEt) = reference.Value;
                LabelAssert.Equal(TimeSystem.Time.CreateTDB(recordEt), records[i].Epoch);
                worstEpoch = System.Math.Max(worstEpoch, System.Math.Abs((records[i].Epoch - epoch).TotalSeconds));
                Assert.Equal(new IO.Astrodynamics.Math.Vector3(av[0], av[1], av[2]), records[i].AngularVelocity);
                for (int axis = 0; axis < 3; axis++)
                {
                    var v = axis == 0 ? IO.Astrodynamics.Math.Vector3.VectorX : axis == 1 ? IO.Astrodynamics.Math.Vector3.VectorY : IO.Astrodynamics.Math.Vector3.VectorZ;
                    var expected = new IO.Astrodynamics.Math.Vector3(cmat[3 * 0 + axis], cmat[3 * 1 + axis], cmat[3 * 2 + axis]);
                    // atan2(|a × b|, a · b): exact near 0, where acos of the cosine rounds 1e-16 to 1.5e-8 rad or to 0
                    var actual = v.Rotate(records[i].Rotation);
                    worstAngle = System.Math.Max(worstAngle, System.Math.Atan2(actual.Cross(expected).Magnitude(), actual.Dot(expected)));
                }
            }

            _output.WriteLine($"MEASURE #563 ReadOrientation against ckgpav_c: worst angle {worstAngle:E3} rad, worst record epoch - start - i·step {worstEpoch:E3} s");
            Assert.True(worstAngle < CkgpavAngleThreshold, $"{worstAngle:E2} rad");
            Assert.True(worstEpoch <= RecordEpochThreshold, $"{worstEpoch:E2} s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    // The record's epoch, sct2e_c of the clock of sce2c_c of the epoch read, against start + i·step: 0 on Linux x64 (the
    // TDB window and the four UTC windows), Windows x64 and macOS arm64 (the four UTC windows; CI run 44225490669), so
    // equal to the tick
    private const double RecordEpochThreshold = 0.0;

    // The rotation of the record's quaternion against ckgpav_c's C-matrix: twice the worst on Linux x64 (1.570e-16 rad, CI
    // run 44225490669), rounded up at the second significant digit: a record of the Validation category, which CI runs on
    // Linux x64 alone
    private const double CkgpavAngleThreshold = 3.2e-16;

    [Fact]
    public void ReadOrientation_OneEpoch_AllocatesForOneRecord()
    {
        // A read of one epoch allocated a buffer of 10000 records, each with its frame name, 1.4 ms a call (#563): the CLI reads
        // one epoch at a time. The allocation of the managed thread, not a time, measured on 2026-11-17 (Linux x64): see the
        // MEASURE line; the bound is twice the worst measured after the fix, rounded up at the second significant digit
        var sclkFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_201008031645.tsc"));
        var ckFile = new FileInfo(Path.Combine(Constants.PhsrmDataPath.FullName, "phsrm_sc_test2_111108_130214_v00.bc"));
        SpiceAPI.Instance.LoadKernels(sclkFile);
        SpiceAPI.Instance.LoadKernels(ckFile);
        try
        {
            var window = new Window(PhsrmStart, PhsrmStart);
            SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0)).Single();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int reads = 100;
            for (int i = 0; i < reads; i++)
            {
                SpiceAPI.Instance.ReadOrientation(window, -555, TimeSpan.Zero, Frames.Frame.ICRF, TimeSpan.FromSeconds(1.0)).Single();
            }

            watch.Stop();
            long allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / reads;
            _output.WriteLine($"MEASURE #563 ReadOrientation of one epoch: {allocated} bytes, {watch.Elapsed.TotalMilliseconds / reads:E3} ms a read");
            Assert.True(allocated < OneEpochOrientationReadBytes, $"{allocated} bytes");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ckFile);
            SpiceAPI.Instance.UnloadKernels(sclkFile);
        }
    }

    // Twice the bytes a read of one epoch allocates after #563: 488 bytes on Linux x64, Windows x64 and macOS arm64 alike
    // (CI run 44225490669: 6.05e-2 ms a read on Linux x64, 4.67e-2 ms on Windows x64, 1.28e-2 ms on macOS arm64; locally on
    // Linux x64 it was 720 408 bytes and 1.83 ms a read before, 2026-11-17)
    private const long OneEpochOrientationReadBytes = 976;
}
