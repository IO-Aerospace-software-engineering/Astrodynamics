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
/// 1.4 ms for a read of one epoch. The reads are now at start + i·step in TDB, i from 0 while it is within the window
/// (exact tick arithmetic), the reference: the read of each such epoch alone.
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
            Assert.Equal(epoch, states[i].Epoch);
            var single = (StateVector)SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.LT);
            Assert.Equal(single.Position, states[i].Position);
            Assert.Equal(single.Velocity, states[i].Velocity);
        }
    }

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
                Assert.Equal(single.Epoch, records[i].Epoch);
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

    // Twice the bytes a read of one epoch allocates on Linux x64 after #563 (488 bytes, 1.28e-2 ms a read; 720 408 bytes and
    // 1.83 ms before, 2026-11-17), rounded up at the second significant digit;
    // provisional, to be set again from the Windows x64 and macOS arm64 CI logs
    private const long OneEpochOrientationReadBytes = 980;
}
