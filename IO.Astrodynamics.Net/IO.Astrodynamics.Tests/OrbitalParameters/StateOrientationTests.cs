using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class StateOrientationTests
    {
        [Fact]
        public void Create()
        {
            var so = new StateOrientation(new Quaternion(1, 2, 3, 4), Vector3.VectorX, new TimeSystem.Time(DateTime.MaxValue,TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.NotNull(so);
            Assert.Equal(new Quaternion(1, 2, 3, 4), so.Rotation);
            Assert.Equal(Vector3.VectorX, so.AngularVelocity);
            Assert.Equal(new TimeSystem.Time(DateTime.MaxValue,TimeFrame.TDBFrame), so.Epoch);
            Assert.Equal(Frames.Frame.ICRF, so.ReferenceFrame);
        }

        [Fact]
        public void RelativeToICRF()
        {
            var so = new StateOrientation(new Quaternion(Vector3.VectorX, 10.0 * IO.Astrodynamics.Constants.Deg2Rad),
                Vector3.Zero, new TimeSystem.Time(DateTime.MaxValue,TimeFrame.TDBFrame), Frames.Frame.ECLIPTIC_J2000);
            var res = so.RelativeToICRF();
            Assert.NotNull(so);

            //Which is equal to ecliptic (23.44° + 10° relative to ecliptic)
            Assert.Equal(new Quaternion(0.9577239084752576, 0.2876889207718582, 0, 0), res.Rotation);
            Assert.Equal(new TimeSystem.Time(DateTime.MaxValue,TimeFrame.TDBFrame), res.Epoch);
            Assert.Equal(Frames.Frame.ICRF, res.ReferenceFrame);
        }

        [Fact]
        public void RelativeToICRF2()
        {
            var res = Frames.Frame.ICRF.ToFrame(Frames.Frame.ICRF, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame)).RelativeTo(Frames.Frame.ICRF);


            //Which is equal to ecliptic (23.44° + 10° relative to ecliptic)
            Assert.Equal(new Quaternion(1.0, 0.0, 0, 0), res.Rotation);
            Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), res.Epoch);
            Assert.Equal(Frames.Frame.ICRF, res.ReferenceFrame);
        }
    }
}