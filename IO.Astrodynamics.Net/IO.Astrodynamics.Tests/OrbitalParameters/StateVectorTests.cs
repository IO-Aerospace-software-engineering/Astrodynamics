using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class StateVectorTests
    {
        public StateVectorTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);
            Assert.Equal(earth, sv.Observer);
            Assert.Equal(epoch, sv.Epoch);
            Assert.Equal(Frames.Frame.ICRF, sv.Frame);
            Assert.Equal(pos, sv.Position);
            Assert.Equal(vel, sv.Velocity);
        }

        [Fact]
        public void Inverse()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);
            var res = sv.Inverse();
            Assert.Equal(new StateVector(pos.Inverse(), vel.Inverse(), earth, epoch, Frames.Frame.ICRF), res);
        }

        [Fact]
        public void Add()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);

            Vector3 pos2 = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel2 = new Vector3(4.0, 5.0, 6.0);
            StateVector sv2 = new StateVector(pos2, vel2, earth, epoch, Frames.Frame.ICRF);
            var sv3 = sv + sv2;
            Assert.Equal(new StateVector(new Vector3(2.0, 4.0, 6.0), new Vector3(8.0, 10.0, 12.0), earth, epoch, Frames.Frame.ICRF), sv3);
        }

        [Fact]
        public void AddExcept()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);

            Vector3 pos2 = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel2 = new Vector3(4.0, 5.0, 6.0);
            StateVector sv2 = new StateVector(pos2, vel2, earth, epoch.Add(TimeSpan.FromSeconds(1.0)), Frames.Frame.ICRF);
            Assert.Throws<ArgumentException>(() => sv + sv2);
        }

        [Fact]
        public void Subtract()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);
            var sv2 = new StateVector(new Vector3(2.0, 4.0, 6.0), new Vector3(8.0, 10.0, 12.0), earth, epoch, Frames.Frame.ICRF);
            var sv3 = sv2 - sv;
            Assert.Equal(new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(4.0, 5.0, 6.0), earth, epoch, Frames.Frame.ICRF), sv3);
        }

        [Fact]
        public void SubtractExcept()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new DateTime(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);

            Vector3 pos2 = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel2 = new Vector3(4.0, 5.0, 6.0);
            StateVector sv2 = new StateVector(pos2, vel2, earth, epoch.Add(TimeSpan.FromSeconds(1.0)), Frames.Frame.ICRF);
            Assert.Throws<ArgumentException>(() => sv - sv2);
        }

        [Fact]
        public void Eccentricity()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06),
                new Vector3(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Assert.Equal(1.3532176446914895E-03, sv.Eccentricity(), 6);
        }

        [Fact]
        public void EccentricityVector()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Vector3 ev = sv.EccentricityVector();
            Assert.Equal(0.091820181898252251, ev.Magnitude());
            Assert.Equal(0.091820181898252251, ev.X);
            Assert.Equal(0.0, ev.Y);
            Assert.Equal(0.0, ev.Z);
        }

        [Fact]
        public void SpecificAngularMomentum()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            StateVector sv = new StateVector(new Vector3(149600000.0, 0.0, 0.0), new Vector3(0.0, 29.8, 0.0), sun, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(4458080000.0, sv.SpecificAngularMomentum().Magnitude());
            Assert.Equal(0.0, sv.SpecificAngularMomentum().X);
            Assert.Equal(0.0, sv.SpecificAngularMomentum().Y);
            Assert.Equal(4458080000.0, sv.SpecificAngularMomentum().Z);
        }

        [Fact]
        public void SpecificOrbitalEnergyMomentum()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06),
                new Vector3(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(-29305465.598506544, sv.SpecificOrbitalEnergy()); //ISS orbital energy in MJ
        }

        [Fact]
        public void Inclination()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(6800.0, 0.0, 0.0), new Vector3(0.0, 5.0, 5.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Assert.Equal(System.Math.PI / 4.0, sv.Inclination());
        }

        [Fact]
        public void SemiMajorAxis()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(8000000.0, 0.0, 0.0), new Vector3(0.0, 6000.0, 6000.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Assert.Equal(14415872.186388023, sv.SemiMajorAxis());
        }

        [Fact]
        public void AscendingNodeVector()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(8000000.0, 0.0, 0.0), new Vector3(0.0, 6000.0, 0.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            var v = sv.AscendingNodeVector().Normalize();
            Assert.Equal(1.0, v.X);
            Assert.Equal(0.0, v.Y);
            Assert.Equal(0.0, v.Z);
        }

        [Fact]
        public void AscendingNode()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(9208000.0, 3352000, 0.0), new Vector3(-1750, 4830, 5140), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Assert.Equal(20.00308830929978, sv.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg);

            sv = new StateVector(new Vector3(7507000, -6299000, 0.0), new Vector3(3300, 3930, 5140), earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Assert.Equal(320.0005416342622, sv.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void ArgumentOfPeriapis()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(8237000.0, 17000.0, 5308000.0), new Vector3(-2000.0, 6000.0, 3000.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(53.621820299859436, sv.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg, 3);

            sv = new StateVector(new Vector3(3973000.0, -4881000.0, -931000.0), new Vector3(5400.0, 3360.0, 5890.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(350.51061587951995, sv.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg, 3);
        }

        [Fact]
        public void TrueAnomaly()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(5070000.0, -2387000.0, 1430000.0), new Vector3(2450.0, 6350.0, 6440.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(30.57538215436613, sv.TrueAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 6);

            sv = new StateVector(new Vector3(1664000.0, -4862000.0, -2655000.0), new Vector3(7520.0, 890, 5520.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(329.477000818209, sv.TrueAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 6);
        }

        [Fact]
        public void EccentricAnomaly()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(6700000.0, 2494000.0, 0.0), new Vector3(-2150.0, 8850.0, 0.0), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(0.20776173316152752, sv.EccentricAnomaly(), 6);
        }

        [Fact]
        public void MeanAnomaly()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(59.99823497591805, sv.MeanAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 3);
        }

        [Fact]
        public void Period()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(1.5501796754722221, sv.Period().TotalHours, 6);
        }

        [Fact]
        public void MeanMotion()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, DateTime.UtcNow, Frames.Frame.ICRF);

            Assert.Equal(0.0011258883596591215, sv.MeanMotion(), 3);
        }

        [Fact]
        public void ToFrame()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            //J2000->Ecliptic
            //Earth from sun at 0 TDB
            var sv = new StateVector(new Vector3(-26499033.67742509, 132757417.33833946, 57556718.47053819), new Vector3(-29.79426007, -5.01805231, -2.17539380), sun,
                new DateTime(2000, 1, 1, 12, 0, 0), Frames.Frame.ICRF);

            double[] res = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector().ToArray();
            Assert.Equal(-26499033.67742509, res[0]);
            Assert.Equal(144697296.7925432, res[1]);
            Assert.Equal(-611.1494260467589, res[2]);
            Assert.Equal(-29.79426007, res[3]);
            Assert.Equal(-5.46929493974574, res[4]);
            Assert.Equal(0.0001817867528557393, res[5]);
        }

        [Fact]
        public void ToNonInertialFrame()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            var epoch = new DateTime(2000, 1, 1, 12, 0, 0);
            var earthFrame = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);

            //J2000->IAU_EARTH
            //Earth from sun at 0 TDB
            var sv = new StateVector(new Vector3(-26499033.67742509, 132757417.33833946, 57556718.47053819), new Vector3(-29.79426007, -5.01805231, -2.17539380), earth,
                epoch.ToTDB(), Frames.Frame.ICRF);

            double[] res = sv.ToFrame(earthFrame).ToStateVector().ToArray();
            Assert.Equal(-135352868.83029744, res[0]);
            Assert.Equal(-2583535.869143948, res[1]);
            Assert.Equal(57553737.733541526, res[2]);
            Assert.Equal(-188.61117766406102, res[3]);
            Assert.Equal(9839.761815065332, res[4]);
            Assert.Equal(-1.9036033768843523, res[5]);
        }

        [Fact]
        public void PerigeeVelocity()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, earth.InitialOrbitalParameters.Epoch, Frames.Frame.ICRF);
            Assert.Equal(8000.0, sv.PerigeeVelocity(), 6);
        }

        [Fact]
        public void ApogeeVelocity()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, earth.InitialOrbitalParameters.Epoch, Frames.Frame.ICRF);
            Assert.Equal(6654.4277759934785, sv.ApogeeVelocity());
        }

        [Fact]
        public void Equality()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, earth.InitialOrbitalParameters.Epoch, Frames.Frame.ICRF);
            var sv2 = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, earth.InitialOrbitalParameters.Epoch, Frames.Frame.ICRF);
            var sv3 = new StateVector(new Vector3(6900000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, earth.InitialOrbitalParameters.Epoch, Frames.Frame.ICRF);
            Assert.Equal(sv, sv2);
            Assert.Equal(sv, (object)sv2);
            Assert.False(sv.Equals((object)null));
            Assert.True(sv.Equals((object)sv2));
            Assert.True(sv.Equals((object)sv));
            Assert.False(sv.Equals(null));
            Assert.True(sv.Equals(sv2));
            Assert.True(sv.Equals(sv));
            Assert.NotEqual(sv3, sv2);
            Assert.True(sv == sv2);
            Assert.True(sv3 != sv2);
        }

        [Fact]
        public void ToKeplerian()
        {
            var earth = TestHelpers.EarthAtJ2000;
            var ke = earth.GetEphemeris(DateTimeExtension.J2000, TestHelpers.Sun, Frames.Frame.ICRF, Aberration.None).ToKeplerianElements();
            Assert.Equal(
                new KeplerianElements(149665479724.91623, 0.01712168303475997, 0.40908763696755318, 1.2954012328856077E-05,
                    1.77688489436688, 6.259056257653703, TestHelpers.Sun, DateTimeExtension.J2000, Frames.Frame.ICRF), ke);
        }

        [Fact]
        public void ToKeplerianSingularity()
        {
            var svOrigin =
                new StateVector(new Vector3(0, 6700000, 0), new Vector3(7900, 0, 0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);

            var keOrigin = svOrigin.ToKeplerianElements();
            Assert.Equal(new KeplerianElements(7045497.0180164594, 0.049037990809302601, 3.1415926535897931, 0.0,
                4.7123889803846897, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF), keOrigin);
            var svFinal = keOrigin.ToStateVector();
            Assert.Equal(svFinal, svOrigin, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void ToEquatorial()
        {
            var moon = TestHelpers.MoonAtJ2000;
            var ra = moon.GetEphemeris(DateTimeExtension.J2000, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToEquatorial();
            Assert.Equal(new Equatorial(-0.19024413568211912, 3.8824377884372114, 402448639.8873273, DateTimeExtension.J2000), ra);
        }

        [Fact]
        public void RelativeTo()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var moonSv = originalSV.RelativeTo(TestHelpers.MoonAtJ2000, Aberration.None);
            Assert.Equal(
                new StateVector(new Vector3(298408384.63343549, 266716833.39423338, 76102487.099902019), new Vector3(-643.53138771903275, 8666.0876840916299, 301.32570498227307),
                    TestHelpers.MoonAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF),
                moonSv);
        }
    }
}