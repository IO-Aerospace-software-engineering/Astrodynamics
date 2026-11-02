using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The rules of the attitude record of a managed spacecraft (<see cref="SpacecraftFrame"/>, #523, #529, #534; choices 1 to
/// 3, 8 to 10 of the design of #537), on records built entry by entry, against closed forms: the rotation about a fixed
/// axis at a constant rate, whose SLERP between two samples is the rotation at the interpolated angle, and whose angular
/// velocity, in the convention of the frames (ICRF relative to the spacecraft, in the spacecraft frame), is -ω times the
/// axis.
/// </summary>
public class SpacecraftFrameRecordTests
{
    // PROVISIONAL: twice the worst over the rotations of these tests on Linux x64 (2026-11-02: 1.42e-16 rad, the entry from
    // ECLIPJ2000), the arithmetic of the quaternions; to be set from the Windows and macOS arm64 CI logs
    private const double Exact = 2.9e-16;

    // PROVISIONAL: twice the worst over the rates of these tests on Linux x64 (2026-11-02: 6.85e-18 rad/s), the rate of a
    // SLERP being its angle (1.1e-2 rad between two samples) over its span
    private const double RateTolerance = 1.4e-17;

    private readonly ITestOutputHelper _output;

    private void Rotation(double angle, string what)
    {
        _output.WriteLine($"MEASURE #537 record rotation {what}: {angle:E2} rad");
        Assert.True(angle <= Exact, $"{what}: {angle:E3} rad");
    }

    private void Rate(double delta, string what)
    {
        _output.WriteLine($"MEASURE #537 record rate {what}: {delta:E2} rad/s");
        Assert.True(delta <= RateTolerance, $"{what}: {delta:E3} rad/s");
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
    private static readonly Vector3 Axis = new Vector3(1.0, 2.0, 3.0).Normalize();
    private const double Omega = 1.1e-3;
    private const double Phase = 0.4;

    // A law: any object, compared by reference
    private static readonly object Law = new();

    public SpacecraftFrameRecordTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static Spacecraft Create(int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frame.ICRF);
        return new Spacecraft(naifId, $"REC{-naifId}", 1000.0, 3000.0, new Clock($"REC{-naifId}_CLK", 65536), orbit);
    }

    private static SpacecraftFrame FrameOf(Spacecraft spacecraft) => (SpacecraftFrame)spacecraft.Frame;

    // The rotation about the axis at the angle of the law at t seconds from the start
    private static Quaternion At(double t) => new(Axis, Phase + Omega * t);

    private static StateOrientation Sample(double t, Frame frame) => new(At(t), Vector3.Zero, Start.AddSeconds(t), frame);

    private static double Angle(Quaternion a, Quaternion b) => AttitudeRecordTests.RotationAngle(a, b);

    [Fact]
    public void EmptyRecord_IsTheIdentityOfIcrf_AndCannotBeWritten()
    {
        var spacecraft = Create(-8501);
        var orientation = spacecraft.Frame.GetStateOrientationToICRF(Start);
        Assert.Equal(Quaternion.Zero, orientation.Rotation);
        Assert.Equal(Vector3.Zero, orientation.AngularVelocity);
        Assert.Equal(spacecraft.Frame, orientation.ReferenceFrame);
        Assert.Null(spacecraft.Frame.GetLatestStateOrientationToICRF());
        Assert.Empty(spacecraft.Frame.GetStateOrientationsToICRF());
        // A spacecraft never propagated has no attitude to write (§5 of the design)
        Assert.Throws<InvalidOperationException>(() => spacecraft.WriteOrientation(new FileInfo("Data/User/AttitudeRecord537/never.bc")));
        // RecordEnd on an empty record records nothing
        FrameOf(spacecraft).RecordEnd(Start.AddSeconds(10.0));
        Assert.Empty(spacecraft.Frame.GetStateOrientationsToICRF());
    }

    [Fact]
    public void AddStateOrientationToICRF_IsADiscreteAttitude_ReplacingWithin2Microseconds()
    {
        var spacecraft = Create(-8502);
        var frame = spacecraft.Frame;
        Assert.Throws<ArgumentNullException>(() => frame.AddStateOrientationToICRF(null));
        // Labelled with the spacecraft frame, the source of the rotation (choice 9)
        Assert.Throws<ArgumentException>(() => frame.AddStateOrientationToICRF(Sample(0.0, Frame.ICRF)));

        Assert.True(frame.AddStateOrientationToICRF(Sample(0.0, frame)));
        Assert.True(frame.AddStateOrientationToICRF(Sample(10.0, frame)));
        // Within 2 µs of the entry at 10 s: replaces it, at its own epoch
        var replacing = new StateOrientation(At(50.0), Vector3.Zero, Start.AddSeconds(10.0 + 1e-6), frame);
        Assert.True(frame.AddStateOrientationToICRF(replacing));
        var record = frame.GetStateOrientationsToICRF().ToArray();
        Assert.Equal(2, record.Length);
        Assert.Equal(replacing, record[1]);
        // Exactly 2 µs apart: two epochs
        Assert.True(frame.AddStateOrientationToICRF(new StateOrientation(At(60.0), Vector3.Zero, Start.AddSeconds(10.0 + 3e-6), frame)));
        Assert.Equal(3, frame.GetStateOrientationsToICRF().Count());
        Assert.Equal(Start.AddSeconds(10.0 + 3e-6), frame.GetLatestStateOrientationToICRF().Epoch);

        // Discrete entries: the earlier one holds until the next, without angular velocity
        var between = frame.GetStateOrientationToICRF(Start.AddSeconds(4.0));
        Assert.Equal(At(0.0), between.Rotation);
        Assert.Equal(Vector3.Zero, between.AngularVelocity);
        Assert.Equal(frame, between.ReferenceFrame);
        // At an entry, before and after the record: that entry, the first, the last
        Assert.Equal(At(0.0), frame.GetStateOrientationToICRF(Start).Rotation);
        Assert.Equal(At(0.0), frame.GetStateOrientationToICRF(Start.AddSeconds(-100.0)).Rotation);
        Assert.Equal(At(60.0), frame.GetStateOrientationToICRF(Start.AddSeconds(1000.0)).Rotation);
        Assert.Equal(Vector3.Zero, frame.GetStateOrientationToICRF(Start.AddSeconds(1000.0)).AngularVelocity);

        frame.ClearStateOrientations();
        Assert.Empty(frame.GetStateOrientationsToICRF());
    }

    [Fact]
    public void SamplesOfOneLaw_AreInterpolated_AtTheLawsRate()
    {
        var spacecraft = Create(-8503);
        var frame = FrameOf(spacecraft);
        Assert.Throws<ArgumentNullException>(() => frame.Record(null, Law, false));
        Assert.True(frame.Record(Sample(0.0, Frame.ICRF), Law, false));
        // The sign of a quaternion is free (§3): the record takes the short arc
        Assert.True(frame.Record(new StateOrientation(At(10.0) * -1.0, Vector3.Zero, Start.AddSeconds(10.0), Frame.ICRF), Law, false));
        Assert.True(frame.Record(Sample(20.0, Frame.ICRF), Law, false));
        // A sample never replaces an entry at the same epoch, within 2 µs
        Assert.False(frame.Record(Sample(20.0 + 1e-6, Frame.ICRF), Law, false));

        var rate = Axis * -Omega;
        foreach (var seconds in new[] { 0.0, 3.7, 10.0, 14.2, 20.0 })
        {
            // The epoch in ticks of 100 ns, and the seconds it holds
            var epoch = Start.AddSeconds(seconds);
            double t = (epoch - Start).TotalSeconds;
            var read = frame.GetStateOrientationToICRF(epoch);
            Rotation(Angle(At(t), read.Rotation), $"interpolated at {t} s");
            Rate((read.AngularVelocity - rate).Magnitude(), $"interpolated at {t} s");
            Assert.Equal(frame, read.ReferenceFrame);
        }

        // The end of the propagation: the latest held, as a sample of its law; not within 2 µs of an entry
        frame.RecordEnd(Start.AddSeconds(20.0 + 1e-6));
        Assert.Equal(3, frame.GetStateOrientationsToICRF().Count());
        frame.RecordEnd(Start.AddSeconds(30.0));
        Assert.Equal(4, frame.GetStateOrientationsToICRF().Count());
        var held = frame.GetStateOrientationToICRF(Start.AddSeconds(25.0));
        Rotation(Angle(At(20.0), held.Rotation), "held after the end");
        // The rate of the CK: at the entry of 20 s, the mean of the law's rate and of the hold's (zero)
        Rate((frame.GetStateOrientationToICRF(Start.AddSeconds(20.0)).AngularVelocity - rate * 0.5).Magnitude(), "at the last sample, mean with the hold");
    }

    [Fact]
    public void EqualSamplesOfOneLaw_HaveNoAngularVelocity()
    {
        var frame = FrameOf(Create(-8504));
        Assert.True(frame.Record(Sample(0.0, Frame.ICRF), Law, false));
        Assert.True(frame.Record(new StateOrientation(At(0.0), Vector3.Zero, Start.AddSeconds(10.0), Frame.ICRF), Law, false));
        var read = frame.GetStateOrientationToICRF(Start.AddSeconds(5.0));
        Assert.Equal(At(0.0), read.Rotation);
        Assert.Equal(Vector3.Zero, read.AngularVelocity);
    }

    [Fact]
    public void LeadBeforeADiscreteChange_IsTheLawsLastSample_AndTheCksRecordBeforeTheChange()
    {
        var spacecraft = Create(-8505);
        var frame = FrameOf(spacecraft);
        Assert.Throws<ArgumentNullException>(() => frame.ChangesAfterSamplesOf(null));
        frame.Record(Sample(0.0, Frame.ICRF), Law, false);
        frame.Record(Sample(10.0, Frame.ICRF), Law, false);
        var change = Start.AddSeconds(15.0);
        var discrete = new StateOrientation(new Quaternion(Vector3.VectorX, 1.0), Vector3.Zero, change, Frame.ICRF);
        Assert.True(frame.Record(discrete, null, true));
        Assert.Equal(new[] { change }, frame.ChangesAfterSamplesOf(Law));
        Assert.Empty(frame.ChangesAfterSamplesOf(new object()));

        // Not a change after a sample of the law: no lead
        Assert.False(frame.RecordLead(Sample(10.0 - 1e-6, Frame.ICRF), Law, Start.AddSeconds(10.0)));
        Assert.False(frame.RecordLead(Sample(15.0, Frame.ICRF), Law, Start.AddSeconds(16.0)));
        Assert.Throws<ArgumentNullException>(() => frame.RecordLead(null, Law, change));
        var lead = new StateOrientation(At((change.AddSeconds(-1e-6) - Start).TotalSeconds), Vector3.Zero, change.AddSeconds(-1e-6), Frame.ICRF);
        Assert.True(frame.RecordLead(lead, Law, change));
        // Recorded once: a lead is not a sample a second lead follows
        Assert.Empty(frame.ChangesAfterSamplesOf(Law));

        // The law up to the change, the lead's rotation over the last microsecond, the change after it
        var rate = Axis * -Omega;
        foreach (var epoch in new[] { Start.AddSeconds(12.0), change.AddSeconds(-1e-6) })
        {
            double t = (epoch - Start).TotalSeconds;
            var read = frame.GetStateOrientationToICRF(epoch);
            Rotation(Angle(At(t), read.Rotation), $"{t} s");
            Rate((read.AngularVelocity - rate).Magnitude(), $"{t} s");
        }

        var inLead = frame.GetStateOrientationToICRF(change.AddSeconds(-0.5e-6));
        Rotation(Angle(lead.Rotation, inLead.Rotation), "in the lead");
        Assert.Equal(Vector3.Zero, inLead.AngularVelocity);
        Assert.Equal(discrete.Rotation, frame.GetStateOrientationToICRF(change).Rotation);

        // The CK: the C-matrices of the entries, the lead as the record before the change (no other record added)
        var records = frame.CkRecords();
        Assert.Equal(new[] { Start, Start.AddSeconds(10.0), change.AddSeconds(-1e-6), change }, records.Select(x => x.Epoch));
        Assert.All(records, x => Assert.Equal(Frame.ICRF, x.ReferenceFrame));
        Rotation(Angle(lead.Rotation.Conjugate(), records[2].Rotation), "CK record of the lead");
        Rotation(Angle(discrete.Rotation.Conjugate(), records[3].Rotation), "CK record of the change");
        // ckw03's rate, the spacecraft relative to J2000 in J2000: +ω about the axis, zero at the change
        Rate((records[1].AngularVelocity - Axis * Omega).Magnitude(), "CK rate at a sample");
        Assert.Equal(Vector3.Zero, records[3].AngularVelocity);

        // A discrete attitude replacing the change 1.5 µs after it: the lead, 2.5 µs before it, is an ordinary sample of its
        // law, held until the new attitude, its rate ramping to zero at the CK's repeated record 1 µs before it
        var replacing = new StateOrientation(new Quaternion(Vector3.VectorY, 0.5), Vector3.Zero, change.AddSeconds(1.5e-6), spacecraft.Frame);
        Assert.True(frame.AddStateOrientationToICRF(replacing));
        // A third of the hold of 1.5 µs (the 2.5 µs to the new attitude less the 1 µs of the repeated record), in ticks of
        // 100 ns: the rate at the lead is the law's (the interval before it), two thirds of it there
        var ramp = frame.GetStateOrientationToICRF(change.AddSeconds(-0.5e-6));
        Rotation(Angle(lead.Rotation, ramp.Rotation), "demoted lead held");
        Rate((ramp.AngularVelocity - rate * (2.0 / 3.0)).Magnitude(), "demoted lead ramp");
        records = frame.CkRecords();
        Assert.Equal(new[] { Start, Start.AddSeconds(10.0), change.AddSeconds(-1e-6), change.AddSeconds(0.5e-6), change.AddSeconds(1.5e-6) },
            records.Select(x => x.Epoch));
        Rotation(Angle(lead.Rotation.Conjugate(), records[3].Rotation), "CK repeated record after demotion");
    }

    [Fact]
    public void AnAttitudeInAnotherInertialFrame_IsRecordedInIcrf()
    {
        var spacecraft = Create(-8506);
        var frame = FrameOf(spacecraft);
        var q = new Quaternion(Axis, 0.7);
        var w = new Vector3(1e-3, -2e-3, 3e-3);
        Assert.True(frame.Record(new StateOrientation(q, w, Start, Frame.ECLIPTIC_J2000), null, true));
        var entry = frame.GetStateOrientationsToICRF().Single();
        // pxform(ECLIPJ2000, J2000) after the rotation to ECLIPJ2000
        var toIcrf = SpiceAPI.Instance.TransformFrame(Start, Frame.ECLIPTIC_J2000, Frame.ICRF).Rotation;
        Rotation(AttitudeRecordTests.RotationAngle(v => v.Rotate(q).Rotate(toIcrf), v => v.Rotate(entry.Rotation)), "ECLIPJ2000 entry");
        Assert.True((entry.AngularVelocity - w.Rotate(toIcrf)).Magnitude() <= 1e-3 * Exact);
        Assert.Equal(spacecraft.Frame, entry.ReferenceFrame);
    }
}
