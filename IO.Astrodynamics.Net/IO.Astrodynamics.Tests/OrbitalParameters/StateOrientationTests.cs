using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class StateOrientationTests
    {
        public StateOrientationTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            var so = new StateOrientation(new Quaternion(1, 2, 3, 4), Vector3.VectorX, new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.NotNull(so);
            Assert.Equal(new Quaternion(1, 2, 3, 4), so.Rotation);
            Assert.Equal(Vector3.VectorX, so.AngularVelocity);
            Assert.Equal(new TimeSystem.Time(DateTime.MaxValue, TimeFrame.TDBFrame), so.Epoch);
            Assert.Equal(Frames.Frame.ICRF, so.ReferenceFrame);
        }

        [Fact]
        public void RelativeToICRF()
        {
            var so = new StateOrientation(new Quaternion(Vector3.VectorX, 10.0 * IO.Astrodynamics.Constants.Deg2Rad),
                Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ECLIPTIC_J2000);
            var res = so.RelativeToICRF();
            Assert.NotNull(so);
            //Which is equal to ecliptic (23.44° + 10° relative to ecliptic)
            Assert.Equal(new Quaternion(0.9577239084752576, 0.2876889207718582, 0, 0), res.Rotation);
            Assert.Equal(Vector3.Zero, res.AngularVelocity);
            Assert.Equal(TimeSystem.Time.J2000TDB, res.Epoch);
            Assert.Equal(Frames.Frame.ICRF, res.ReferenceFrame);
        }
        
        [Fact]
        public void RelativeToICRFInSameFrame()
        {
            var so = new StateOrientation(new Quaternion(Vector3.VectorX, 10.0 * IO.Astrodynamics.Constants.Deg2Rad),
                Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var res = so.RelativeToICRF();
            Assert.NotNull(so);
            Assert.Equal(so, res);
        }

        [Fact]
        public void RelativeToTargetAndGoBack()
        {
            var so = new StateOrientation(new Quaternion(Vector3.VectorX, 10.0 * IO.Astrodynamics.Constants.Deg2Rad),
                Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ECLIPTIC_J2000);
            var res = so.RelativeToICRF();

            var resOrigin = res.RelativeTo(Frames.Frame.ECLIPTIC_J2000); //Go to the origin
            Assert.NotNull(so);

            Assert.Equal(so, resOrigin,TestHelpers.StateOrientationComparer);
        }

        [Fact]
        public void RelativeTo()
        {
            var so = new StateOrientation(new Quaternion(Vector3.VectorX, 10.0 * IO.Astrodynamics.Constants.Deg2Rad),
                Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ECLIPTIC_J2000);
            var res = so.RelativeTo(Frames.Frame.GALACTIC_SYSTEM2);
            Assert.NotNull(so);

            Assert.Equal(new Quaternion(0.6072910782107768, -0.3221176440801108, 0.2767233690846851, 0.6714625430359408), res.Rotation);
            Assert.Equal(Vector3.Zero, res.AngularVelocity);
            Assert.Equal(TimeSystem.Time.J2000TDB, res.Epoch);
            Assert.Equal(Frames.Frame.GALACTIC_SYSTEM2, res.ReferenceFrame);
        }

        [Fact]
        public void RelativeToITRF()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var so = new StateOrientation(Quaternion.Zero,
                Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ECLIPTIC_J2000);
            var res = so.RelativeTo(earth.Frame);
            Assert.NotNull(so);

            Assert.Equal(new Quaternion(0.75114277947940278, 0.15580379240149217, 0.13030227231228195, 0.62811704398107715), res.Rotation);
            Assert.Equal(new Vector3(-1.9637713280161757E-09, 2.9004497224156795E-05, 6.6904658702357438E-05), res.AngularVelocity);
            Assert.Equal(TimeSystem.Time.J2000TDB, res.Epoch);
            Assert.Equal(earth.Frame, res.ReferenceFrame);
        }
    }
}