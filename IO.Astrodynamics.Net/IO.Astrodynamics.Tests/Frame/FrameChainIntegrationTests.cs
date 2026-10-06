using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class FrameChainIntegrationTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public FrameChainIntegrationTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    [Fact]
    public void GcrfToCirsMatchesSofaRc2i()
    {
        // SOFA test date MJD 53736.0 TT. rc2i maps GCRS to CIRS; the SPICE pivot is the ICRS, so
        // GCRF -> CIRS must be Q(t) alone, without a second application of the frame bias.
        var epoch = TimeSystem_Time.CreateFromJD(2400000.5 + 53736.0, TimeFrame.TDTFrame);
        var gcrfToCirs = Frames.Frame.GCRF.ToFrame(Frames.Frame.CIRS, epoch);

        // Column j is the image of the j-th GCRF axis expressed in CIRS.
        var columns = new[]
        {
            Vector3.VectorX.Rotate(gcrfToCirs.Rotation),
            Vector3.VectorY.Rotate(gcrfToCirs.Rotation),
            Vector3.VectorZ.Rotate(gcrfToCirs.Rotation)
        };

        // SOFA iauC2i06a at MJD 53736.0 TT (tools/sofa_reference/sofa_ref.c, label SOFA_test_MJD53736).
        double[,] rc2i =
        {
            { 9.999998323037156e-01, 5.581121259590205e-10, -5.791308491611245e-04 },
            { -2.3842530075257606e-08, 9.999999991917468e-01, -4.020579110174657e-05 },
            { 5.791308486706009e-04, 4.020579816732948e-05, 9.999998314954628e-01 }
        };

        // 5e-9 rad covers IAU 2000B against SOFA's IAU 2000A nutation (about 1 mas). A doubled
        // frame bias would shift the off-diagonal terms by up to 8e-8.
        const double tolerance = 5e-9;
        for (int j = 0; j < 3; j++)
        {
            Assert.Equal(rc2i[0, j], columns[j].X, tolerance);
            Assert.Equal(rc2i[1, j], columns[j].Y, tolerance);
            Assert.Equal(rc2i[2, j], columns[j].Z, tolerance);
        }
    }

    [Fact]
    public void TirsToCirsIsPureEraRotation()
    {
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        var tirsToCirs = Frames.Frame.TIRS.ToFrame(Frames.Frame.CIRS, epoch);

        // The rotation should be approximately R3(ERA) — a rotation about Z axis
        var rotMatrix = Matrix.FromQuaternion(tirsToCirs.Rotation);

        // In a pure Z rotation, [2][0], [2][1], [0][2], [1][2] should all be ~0
        // and [2][2] should be ~1
        Assert.Equal(1.0, rotMatrix.Get(2, 2), 1e-6);
        Assert.Equal(0.0, rotMatrix.Get(2, 0), 1e-6);
        Assert.Equal(0.0, rotMatrix.Get(2, 1), 1e-6);
        Assert.Equal(0.0, rotMatrix.Get(0, 2), 1e-6);
        Assert.Equal(0.0, rotMatrix.Get(1, 2), 1e-6);

        // The [0][0] and [1][1] should have the same magnitude (cos(ERA))
        Assert.Equal(System.Math.Abs(rotMatrix.Get(0, 0)),
                     System.Math.Abs(rotMatrix.Get(1, 1)), 1e-6);
    }

    [Fact]
    public void TirsToIcrfFullChain()
    {
        var epoch = new TimeSystem_Time(2010, 6, 15, 12, 0, 0, frame: TimeFrame.TDBFrame);

        // Direct TIRS→ICRF
        var tirsToIcrf = Frames.Frame.TIRS.GetStateOrientationToICRF(epoch);

        // The rotation matrix should be orthogonal
        var rotMatrix = Matrix.FromQuaternion(tirsToIcrf.Rotation);
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
    public void StateVectorIcrfToTirsRoundTrip()
    {
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var tirs = Frames.Frame.TIRS;

        // ISS-like state vector in ICRF
        var posIcrf = new Vector3(6778136.3, 0.0, 0.0);
        var velIcrf = new Vector3(0.0, 7656.2, 0.0);

        // Transform to TIRS
        var icrfToTirs = Frames.Frame.ICRF.ToFrame(tirs, epoch);
        var posTirs = posIcrf.Rotate(icrfToTirs.Rotation);
        var velTirs = velIcrf.Rotate(icrfToTirs.Rotation);

        // Transform back to ICRF
        var tirsToIcrf = tirs.ToFrame(Frames.Frame.ICRF, epoch);
        var posBack = posTirs.Rotate(tirsToIcrf.Rotation);
        var velBack = velTirs.Rotate(tirsToIcrf.Rotation);

        // Round-trip error should be < 1e-6 m
        Assert.Equal(posIcrf.X, posBack.X, 1e-6);
        Assert.Equal(posIcrf.Y, posBack.Y, 1e-6);
        Assert.Equal(posIcrf.Z, posBack.Z, 1e-6);
        Assert.Equal(velIcrf.X, velBack.X, 1e-6);
        Assert.Equal(velIcrf.Y, velBack.Y, 1e-6);
        Assert.Equal(velIcrf.Z, velBack.Z, 1e-6);
    }

    [Fact]
    public void TirsApproximatesItrf93WithinPolarMotionMagnitude()
    {
        // TIRS with NullEop (no polar motion) should approximate ITRF93
        // within ~0.5" (the typical polar motion magnitude)
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var tirs = Frames.Frame.TIRS;
        var itrf93 = new Frames.Frame("ITRF93");

        var tirsOrientation = tirs.GetStateOrientationToICRF(epoch);
        var itrfOrientation = itrf93.GetStateOrientationToICRF(epoch);

        // Compare the rotation matrices
        var tirsMat = Matrix.FromQuaternion(tirsOrientation.Rotation);
        var itrfMat = Matrix.FromQuaternion(itrfOrientation.Rotation);

        // The difference should be within ~1e-5 rad (~2") accounting for:
        // - Polar motion (~0.5")
        // - Nutation model difference (IAU 2000B vs SPICE's model) (~1 mas)
        // - Different Earth rotation models
        var relQ = tirsOrientation.Rotation.Conjugate() * itrfOrientation.Rotation;
        double angle = 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(relQ.W)));

        // Allow up to 5" = 2.4e-5 rad for all the model differences
        Assert.True(angle < 5e-5,
            $"TIRS-ITRF93 angle should be < 5e-5 rad (~10\"), got {angle} rad ({angle * 180 * 3600 / System.Math.PI:F2}\")");
    }

    [Theory]
    [InlineData("ITRF93")]
    [InlineData("IAU_MOON")]
    [InlineData("MOON_ME")]
    [InlineData("DSS-13_TOPO")]
    public void SpiceFrameRotationFollowsItsAngularVelocity(string frameName)
    {
        // The convention every frame follows (xf2rav_c): the angular velocity of ICRF relative to the frame, in the
        // frame axes. Over one second the measured residual is below 2e-13 rad. StateOrientation.AtDate, built for
        // attitudes, would turn the other way.
        var frame = new Frames.Frame(frameName);
        var epoch = new TimeSystem_Time(2021, 1, 1, 12, 0, 0);

        var propagated = TestHelpers.RotateWithFrameAngularVelocity(frame.GetStateOrientationToICRF(epoch), 1.0);
        var recomputed = frame.GetStateOrientationToICRF(epoch.AddSeconds(1)).Rotation;

        var relative = propagated.Conjugate() * recomputed;
        double angle = 2.0 * System.Math.Atan2(relative.VectorPart.Magnitude(), System.Math.Abs(relative.W));
        Assert.True(angle < 1e-12, $"Propagated and recomputed {frameName} rotations differ by {angle:E3} rad");
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void TirsAngularVelocityMatchesItrf93(int year)
    {
        // Both frames turn about the CIP at the Earth rotation rate; SPICE takes ITRF93 from the high precision Earth
        // orientation kernel. TIRS leaves out the precession-nutation rate of the CIP (a few 1e-12 rad/s) and the
        // length-of-day variations (about 1e-12 rad/s). The former TIRS angular velocity had the opposite sign,
        // 1.5e-4 rad/s away.
        var epoch = new TimeSystem_Time(year, 1, 1, 12, 0, 0);

        var tirs = Frames.Frame.ICRF.ToFrame(Frames.Frame.TIRS, epoch).AngularVelocity;
        var itrf93 = Frames.Frame.ICRF.ToFrame(new Frames.Frame("ITRF93"), epoch).AngularVelocity;

        double difference = (tirs - itrf93).Magnitude();
        Assert.True(difference < 5e-11, $"|w(TIRS) - w(ITRF93)| = {difference:E3} rad/s");
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void PointFixedInTirsMovesAtTheDerivativeOfItsIcrfPosition(int year)
    {
        // A point fixed in TIRS at LEO distance: the ICRF velocity given by ToFrame must be the time derivative of its
        // ICRF position, here a central difference over +/- 1 s (truncation error about 1e-9 in relative terms). TIRS
        // leaves out the precession-nutation rate of the CIP, a few 1e-12 against 7.3e-5 rad/s: about 1e-7. The former
        // TIRS angular velocity gave the opposite velocity.
        var epoch = new TimeSystem_Time(year, 1, 1, 12, 0, 0);
        var position = new Vector3(4.2e6, -3.2e6, 4.4e6);

        var velocity = FixedPointInIcrf(Frames.Frame.TIRS, position, epoch).Velocity;
        var derivative = CentralDifference(Frames.Frame.TIRS, position, epoch, 1.0);

        double error = (velocity - derivative).Magnitude();
        Assert.True(error < 1e-6 * derivative.Magnitude(), $"|v - dr/dt| = {error:E3} m/s, |dr/dt| = {derivative.Magnitude():E6} m/s");
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void PointFixedInCirsMovesAtTheDerivativeOfItsIcrfPosition(int year)
    {
        // CIRS follows the CIP at a few 1e-12 rad/s, so a point fixed in CIRS at lunar distance moves at a few mm/s in
        // ICRF. The CIRS angular velocity is itself a central difference over +/- 0.01 s, accurate to a few 1e-4,
        // hence the 1e-3 tolerance. The former CIRS angular velocity gave the opposite velocity.
        var epoch = new TimeSystem_Time(year, 1, 1, 12, 0, 0);
        var position = new Vector3(2.3e8, 0.0, 3.07e8);

        var velocity = FixedPointInIcrf(Frames.Frame.CIRS, position, epoch).Velocity;
        var derivative = CentralDifference(Frames.Frame.CIRS, position, epoch, 100.0);

        double error = (velocity - derivative).Magnitude();
        Assert.True(error < 1e-3 * derivative.Magnitude(), $"|v - dr/dt| = {error:E3} m/s, |dr/dt| = {derivative.Magnitude():E6} m/s");
    }

    [Fact]
    public void StateRoundTripThroughTirsRestoresTheState()
    {
        var epoch = new TimeSystem_Time(2021, 1, 1, 12, 0, 0);
        var state = new StateVector(new Vector3(4211623.0, -3218447.0, 4402165.0), new Vector3(4523.1, 5601.7, 1188.4),
            PlanetsAndMoons.EARTH_BODY, epoch, Frames.Frame.ICRF);

        var roundTrip = state.ToFrame(Frames.Frame.TIRS).ToFrame(Frames.Frame.ICRF).ToStateVector();

        Assert.True((roundTrip.Position - state.Position).Magnitude() < 1e-12 * state.Position.Magnitude());
        Assert.True((roundTrip.Velocity - state.Velocity).Magnitude() < 1e-12 * state.Velocity.Magnitude());
    }

    private static StateVector FixedPointInIcrf(Frames.Frame frame, Vector3 position, TimeSystem_Time epoch)
    {
        return new StateVector(position, Vector3.Zero, PlanetsAndMoons.EARTH_BODY, epoch, frame)
            .ToFrame(Frames.Frame.ICRF).ToStateVector();
    }

    private static Vector3 CentralDifference(Frames.Frame frame, Vector3 position, TimeSystem_Time epoch, double step)
    {
        var after = FixedPointInIcrf(frame, position, epoch.AddSeconds(step)).Position;
        var before = FixedPointInIcrf(frame, position, epoch.AddSeconds(-step)).Position;
        return (after - before) / (2.0 * step);
    }

    [Fact]
    public void FramesStaticInstancesAreCorrectTypes()
    {
        Assert.IsType<GcrfFrame>(Frames.Frame.GCRF);
        Assert.IsType<CirsFrame>(Frames.Frame.CIRS);
        Assert.IsType<TirsFrame>(Frames.Frame.TIRS);
    }

    [Fact]
    public void TemeToCircTransformation()
    {
        var epoch = new TimeSystem_Time(2024, 1, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

        // This tests that the hub-and-spoke architecture works:
        // TEME→ICRF→CIRS
        var temeToCirs = Frames.Frame.TEME.ToFrame(
            Frames.Frame.CIRS, epoch);

        // Should produce a valid rotation
        var rotMatrix = Matrix.FromQuaternion(temeToCirs.Rotation);
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
    public void FullChainGcrsToItrsAtSofaTestDate()
    {
        // SOFA reference values use TT = UT1 (artificial). Use UTC epoch so that
        // UT1 = UTC (with NullEop), and precession-nutation from TDB ≈ UTC + ~66s
        // (negligible difference in t for precession).
        // Epoch: 2010-06-15 12:00 UTC (MJD 55362.0, not on a leap second boundary)
        var epoch = TimeSystem_Time.CreateFromJD(2400000.5 + 55362.0, TimeFrame.UTCFrame);

        var tirs = new TirsFrame();
        var orientation = tirs.GetStateOrientationToICRF(epoch);
        var m = Matrix.FromQuaternion(orientation.Rotation);

        // The TIRS→ICRF matrix is the TRANSPOSE of SOFA's rc2t (GCRS→ITRS)
        // So our m should equal rc2t^T, or equivalently m^T should match rc2t
        var mT = m.Transpose();

        // SOFA rc2t_gcrs_to_itrs (no polar motion, UT1=TT):
        // Tolerance accounts for IAU 2000B vs 2000A nutation (~1 mas) plus
        // the ~66s TDB-UTC offset affecting precession-nutation computation.
        double tol = 2e-8;
        Assert.Equal(-1.212538003906374340e-01, mT.Get(0, 0), tol);
        Assert.Equal(-9.926215281615898833e-01, mT.Get(0, 1), tol);
        Assert.Equal(9.926209842961773999e-01, mT.Get(1, 0), tol);
        Assert.Equal(-1.212538733089508491e-01, mT.Get(1, 1), tol);
        // The [2][0] and [2][1] elements depend on CIP coordinates
        Assert.Equal(1.047580887328611408e-03, mT.Get(2, 0), tol);
        Assert.Equal(6.142246342155655014e-06, mT.Get(2, 1), tol);
        Assert.Equal(9.999994512681280590e-01, mT.Get(2, 2), tol);
    }
}
