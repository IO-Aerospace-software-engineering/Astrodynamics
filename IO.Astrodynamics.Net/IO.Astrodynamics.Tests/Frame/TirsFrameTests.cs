using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class TirsFrameTests
{
    private static double QuaternionAngleDifference(Quaternion left, Quaternion right)
    {
        // atan2 keeps full precision for small angles, where 2 acos(|w|) cannot resolve less than ~3e-8 rad.
        var relative = left.Conjugate() * right;
        return 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
    }

    [Fact]
    public void TirsResolvesSubMillisecondEpochs()
    {
        // 0.4 ms of UT1 is 2.9e-8 rad of Earth rotation. The ERA used to be fed a millisecond-truncated
        // Julian date, so both epochs gave the same orientation.
        var tirs = new TirsFrame();
        var first = new TimeSystem_Time(new DateTime(2024, 1, 1, 6, 30, 15).AddTicks(1234567), TimeFrame.UTCFrame);
        var second = first.Add(TimeSpan.FromTicks(4000));

        double angle = QuaternionAngleDifference(tirs.GetStateOrientationToICRF(first).Rotation,
            tirs.GetStateOrientationToICRF(second).Rotation);

        // Tolerance: ~2e-12 day of double resolution on the date, times the Earth rotation rate, is 1.3e-11 rad.
        const double earthRotationRate = 2.0 * System.Math.PI * 1.00273781191135448 / 86400.0;
        Assert.Equal(earthRotationRate * 4e-4, angle, 2e-11);
    }

    [Fact]
    public void TirsAngularVelocityIsEarthRotation()
    {
        var tirs = new TirsFrame();
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = tirs.GetStateOrientationToICRF(epoch);
        double omegaMag = orientation.AngularVelocity.Magnitude();

        // Earth rotation rate: ~7.2921150e-5 rad/s
        Assert.Equal(7.2921150e-5, omegaMag, 1e-8);
    }

    [Fact]
    public void TirsAngularVelocityDirectionIsNearZAxis()
    {
        var tirs = new TirsFrame();
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = tirs.GetStateOrientationToICRF(epoch);
        var omega = orientation.AngularVelocity;
        double mag = omega.Magnitude();

        // Angular velocity is expressed in ICRF, so it should align with the CIP.
        double zFraction = System.Math.Abs(omega.Z) / mag;
        Assert.True(zFraction > 0.99, $"Z fraction of angular velocity should be >0.99, got {zFraction}");
        Assert.True(System.Math.Abs(omega.X) < 2e-7, $"ICRF X component should stay small, got {omega.X}");
        Assert.True(System.Math.Abs(omega.Y) < 2e-7, $"ICRF Y component should stay small, got {omega.Y}");
    }

    [Fact]
    public void TirsOrientationMatrixIsOrthogonal()
    {
        var tirs = new TirsFrame();
        var epoch = new TimeSystem_Time(2010, 6, 15, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var orientation = tirs.GetStateOrientationToICRF(epoch);
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
    public void TirsRotationChangesSignificantlyOver1Hour()
    {
        var tirs = new TirsFrame();
        var epoch1 = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var epoch2 = epoch1.AddHours(1);

        var q1 = tirs.GetStateOrientationToICRF(epoch1).Rotation;
        var q2 = tirs.GetStateOrientationToICRF(epoch2).Rotation;

        var relQ = q1.Conjugate() * q2;
        double angle = 2.0 * System.Math.Acos(System.Math.Abs(relQ.W));

        // 1 hour of Earth rotation: ~15 degrees = 0.26 rad
        Assert.Equal(15.0 * System.Math.PI / 180.0, angle, 0.01);
    }

    [Fact]
    public void TirsFrameNameIsCorrect()
    {
        var tirs = new TirsFrame();
        Assert.Equal("TIRS", tirs.Name);
    }

    [Fact]
    public void TirsWithNullEopMatchesDefaultConstructor()
    {
        var tirs1 = new TirsFrame();
        var tirs2 = new TirsFrame(NullEop.Instance);
        var epoch = TimeSystem_Time.J2000TDB;

        var q1 = tirs1.GetStateOrientationToICRF(epoch).Rotation;
        var q2 = tirs2.GetStateOrientationToICRF(epoch).Rotation;

        Assert.Equal(q1.W, q2.W, 1e-15);
        Assert.Equal(q1.VectorPart.X, q2.VectorPart.X, 1e-15);
        Assert.Equal(q1.VectorPart.Y, q2.VectorPart.Y, 1e-15);
        Assert.Equal(q1.VectorPart.Z, q2.VectorPart.Z, 1e-15);
    }

    [Fact]
    public void TirsReferenceFrameIsSelf()
    {
        var tirs = new TirsFrame();
        var orientation = tirs.GetStateOrientationToICRF(TimeSystem_Time.J2000TDB);

        Assert.Same(tirs, orientation.ReferenceFrame);
    }

    [Fact]
    public void TirsAtDateMatchesFreshRecomputationOverOneSecond()
    {
        var tirs = new TirsFrame();
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var propagated = tirs.GetStateOrientationToICRF(epoch).AtDate(epoch.AddSeconds(1));
        var recomputed = tirs.GetStateOrientationToICRF(epoch.AddSeconds(1));

        double angle = QuaternionAngleDifference(propagated.Rotation, recomputed.Rotation);

        Assert.True(angle < 1e-9, $"Propagated and recomputed TIRS rotations should agree within 1e-9 rad, got {angle}");
    }
}
