using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class GcrfFrameTests
{
    [Fact]
    public void GcrfRotationIsConstantAcrossEpochs()
    {
        var gcrf = new GcrfFrame();
        var epoch1 = TimeSystem_Time.J2000TDB;
        var epoch2 = TimeSystem_Time.J2000TDB.AddDays(365.25);
        var epoch3 = TimeSystem_Time.J2000TDB.AddDays(3652.5);

        var q1 = gcrf.GetStateOrientationToICRF(epoch1).Rotation;
        var q2 = gcrf.GetStateOrientationToICRF(epoch2).Rotation;
        var q3 = gcrf.GetStateOrientationToICRF(epoch3).Rotation;

        Assert.Equal(q1.W, q2.W, 1e-15);
        Assert.Equal(q1.VectorPart.X, q2.VectorPart.X, 1e-15);
        Assert.Equal(q1.VectorPart.Y, q2.VectorPart.Y, 1e-15);
        Assert.Equal(q1.VectorPart.Z, q2.VectorPart.Z, 1e-15);

        Assert.Equal(q1.W, q3.W, 1e-15);
        Assert.Equal(q1.VectorPart.X, q3.VectorPart.X, 1e-15);
    }

    [Fact]
    public void GcrfAngularVelocityIsZero()
    {
        var gcrf = new GcrfFrame();
        var orientation = gcrf.GetStateOrientationToICRF(TimeSystem_Time.J2000TDB);

        Assert.Equal(0.0, orientation.AngularVelocity.X, 1e-20);
        Assert.Equal(0.0, orientation.AngularVelocity.Y, 1e-20);
        Assert.Equal(0.0, orientation.AngularVelocity.Z, 1e-20);
    }

    [Fact]
    public void GcrfBiasMagnitudeIsAbout23Mas()
    {
        var gcrf = new GcrfFrame();
        var orientation = gcrf.GetStateOrientationToICRF(TimeSystem_Time.J2000TDB);

        // Convert quaternion to rotation angle
        var q = orientation.Rotation;
        double angle = 2.0 * System.Math.Acos(System.Math.Abs(q.W));

        // ~23 mas ≈ 1.1e-7 rad
        Assert.True(angle < 2e-7, $"Bias angle {angle} should be ~1e-7 rad (~23 mas)");
        Assert.True(angle > 5e-8, $"Bias angle {angle} should be ~1e-7 rad (~23 mas)");
    }

    [Fact]
    public void GcrfFrameNameIsCorrect()
    {
        var gcrf = new GcrfFrame();
        Assert.Equal("GCRF", gcrf.Name);
    }

    [Fact]
    public void GcrfReferenceFrameIsSelf()
    {
        var gcrf = new GcrfFrame();
        var orientation = gcrf.GetStateOrientationToICRF(TimeSystem_Time.J2000TDB);

        Assert.Same(gcrf, orientation.ReferenceFrame);
    }

    [Fact]
    public void GcrfToIcrfRoundTrip()
    {
        var gcrf = new GcrfFrame();
        var epoch = TimeSystem_Time.J2000TDB;

        // Transform a vector from GCRF to ICRF and back
        var vGcrf = new Vector3(6778136.3, 0.0, 0.0);
        var orientation = gcrf.GetStateOrientationToICRF(epoch);
        var vIcrf = vGcrf.Rotate(orientation.Rotation);
        var vBack = vIcrf.Rotate(orientation.Rotation.Conjugate());

        Assert.Equal(vGcrf.X, vBack.X, 1e-6);
        Assert.Equal(vGcrf.Y, vBack.Y, 1e-6);
        Assert.Equal(vGcrf.Z, vBack.Z, 1e-6);
    }
}
