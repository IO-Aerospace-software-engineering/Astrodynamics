using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class CirsFrameTests
{
    private static double QuaternionAngleDifference(Quaternion left, Quaternion right)
    {
        // atan2 keeps full precision for small angles, where 2 acos(|w|) cannot resolve less than ~3e-8 rad.
        var relative = left.Conjugate() * right;
        return 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
    }

    [Fact]
    public void CirsAtJ2000IsCloseToGcrf()
    {
        var cirs = new CirsFrame();
        var gcrf = new GcrfFrame();
        var epoch = TimeSystem_Time.J2000TDB;

        var cirsOrientation = cirs.GetStateOrientationToICRF(epoch);
        var gcrfOrientation = gcrf.GetStateOrientationToICRF(epoch);

        // At J2000, CIRS differs from GCRF only by the J2000 nutation
        // (precession is zero). The nutation at J2000 is ~14" in longitude,
        // ~5.8" in obliquity, so the difference should be of that order.
        var relQ = cirsOrientation.Rotation.Conjugate() * gcrfOrientation.Rotation;
        double angle = 2.0 * System.Math.Acos(System.Math.Abs(relQ.W));

        // Expect ~3e-5 rad (~6") difference from nutation at J2000
        Assert.True(angle < 1e-4, $"CIRS-GCRF angle at J2000 should be small, got {angle}");
    }

    [Fact]
    public void CirsAt2024HasSignificantPrecession()
    {
        var cirs = new CirsFrame();
        // 2024-01-01 12:00:00 TDB
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = cirs.GetStateOrientationToICRF(epoch);

        // At 2024 (~24 years from J2000), precession is ~490" ≈ 2.4e-3 rad
        // The CIP X coordinate should be ~2.3e-3 rad
        var q = orientation.Rotation;
        double angle = 2.0 * System.Math.Acos(System.Math.Abs(q.W));
        Assert.True(angle > 1e-3, $"CIRS angle at 2024 should be >1e-3 rad, got {angle}");
        Assert.True(angle < 5e-3, $"CIRS angle at 2024 should be <5e-3 rad, got {angle}");
    }

    [Fact]
    public void CirsAngularVelocityIsPrecessionRate()
    {
        var cirs = new CirsFrame();
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = cirs.GetStateOrientationToICRF(epoch);
        double omegaMag = orientation.AngularVelocity.Magnitude();

        // Precession rate: ~50"/year = 50 * 4.848e-6 / (365.25 * 86400) ≈ 7.7e-12 rad/s
        // Nutation adds short-period variations, so allow wider range
        Assert.True(omegaMag < 1e-9, $"CIRS angular velocity should be ~1e-11 rad/s, got {omegaMag}");
        Assert.True(omegaMag > 1e-14, $"CIRS angular velocity should be nonzero, got {omegaMag}");
    }

    [Fact]
    public void CirsOrientationMatrixIsOrthogonal()
    {
        var cirs = new CirsFrame();
        var epoch = new TimeSystem_Time(2010, 6, 15, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = cirs.GetStateOrientationToICRF(epoch);
        var rotMatrix = Matrix.FromQuaternion(orientation.Rotation);
        var product = rotMatrix.Transpose().Multiply(rotMatrix);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : 0.0;
                Assert.Equal(expected, product.Get(i, j), 1e-12);
            }
        }
    }

    [Fact]
    public void CirsFrameNameIsCorrect()
    {
        var cirs = new CirsFrame();
        Assert.Equal("CIRS", cirs.Name);
    }

    [Fact]
    public void CirsReferenceFrameIsSelf()
    {
        var cirs = new CirsFrame();
        var orientation = cirs.GetStateOrientationToICRF(TimeSystem_Time.J2000TDB);

        Assert.Same(cirs, orientation.ReferenceFrame);
    }

    [Fact]
    public void CirsAngularVelocityMatchesFreshRecomputationOverOneSecond()
    {
        // The angular velocity follows the SPICE convention of the frames, not the one of StateOrientation.AtDate.
        var cirs = new CirsFrame();
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var propagated = TestHelpers.RotateWithFrameAngularVelocity(cirs.GetStateOrientationToICRF(epoch), 1.0);
        var recomputed = cirs.GetStateOrientationToICRF(epoch.AddSeconds(1));

        double angle = QuaternionAngleDifference(propagated, recomputed.Rotation);

        Assert.True(angle < 1e-12, $"Propagated and recomputed CIRS rotations should agree within 1e-12 rad, got {angle}");
    }
}
