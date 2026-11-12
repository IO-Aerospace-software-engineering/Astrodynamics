using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class StateVectorTests
    {
        private readonly ITestOutputHelper _output;

        public StateVectorTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new TimeSystem.Time(2021, 12, 12);
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
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new TimeSystem.Time(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);
            var res = sv.Inverse();
            Assert.Equal(new StateVector(pos.Inverse(), vel.Inverse(), earth, epoch, Frames.Frame.ICRF), res);
        }

        [Fact]
        public void Add()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new TimeSystem.Time(2021, 12, 12);
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
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            Vector3 pos = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel = new Vector3(4.0, 5.0, 6.0);
            var epoch = new TimeSystem.Time(2021, 12, 12);
            StateVector sv = new StateVector(pos, vel, earth, epoch, Frames.Frame.ICRF);

            Vector3 pos2 = new Vector3(1.0, 2.0, 3.0);
            Vector3 vel2 = new Vector3(4.0, 5.0, 6.0);
            StateVector sv2 = new StateVector(pos2, vel2, earth, epoch.Add(TimeSpan.FromSeconds(1.0)), Frames.Frame.ICRF);
            Assert.Throws<ArgumentException>(() => sv + sv2);
        }

        [Fact]
        public void Eccentricity()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06),
                new Vector3(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            Assert.Equal(1.3532176446914895E-03, sv.Eccentricity(), 6);
        }

        [Fact]
        public void EccentricityVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
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

            StateVector sv = new StateVector(new Vector3(149600000.0, 0.0, 0.0), new Vector3(0.0, 29.8, 0.0), sun, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(4458080000.0, sv.SpecificAngularMomentum().Magnitude());
            Assert.Equal(0.0, sv.SpecificAngularMomentum().X);
            Assert.Equal(0.0, sv.SpecificAngularMomentum().Y);
            Assert.Equal(4458080000.0, sv.SpecificAngularMomentum().Z);
        }

        [Fact]
        public void SpecificOrbitalEnergyMomentum()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06),
                new Vector3(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(-29305465.598506544, sv.SpecificOrbitalEnergy()); //ISS orbital energy in MJ
        }

        [Fact]
        public void Inclination()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5000.0, 5000.0), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            Assert.Equal(System.Math.PI / 4.0, sv.Inclination(), 6);
        }

        [Fact]
        public void SemiMajorAxis()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(8000000.0, 0.0, 0.0), new Vector3(0.0, 6000.0, 6000.0), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            Assert.Equal(14415872.186388023, sv.SemiMajorAxis(), 6);
        }

        [Fact]
        public void AscendingNodeVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(8000000.0, 0.0, 0.0), new Vector3(0.0, 6000.0, 0.0), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            var v = sv.AscendingNodeVector().Normalize();
            Assert.Equal(1.0, v.X);
            Assert.Equal(0.0, v.Y);
            Assert.Equal(0.0, v.Z);
        }

        [Fact]
        public void AscendingNode()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(9208000.0, 3352000, 0.0), new Vector3(-1750, 4830, 5140), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            Assert.Equal(20.00308830929978, sv.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg, 6);

            sv = new StateVector(new Vector3(7507000, -6299000, 0.0), new Vector3(3300, 3930, 5140), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            Assert.Equal(320.0005416342622, sv.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg, 6);
        }

        [Fact]
        public void ArgumentOfPeriapis()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(8237000.0, 17000.0, 5308000.0), new Vector3(-2000.0, 6000.0, 3000.0), earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);

            Assert.Equal(53.621820299859436, sv.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg, 3);

            sv = new StateVector(new Vector3(3973000.0, -4881000.0, -931000.0), new Vector3(5400.0, 3360.0, 5890.0), earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);

            Assert.Equal(350.51061587951995, sv.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg, 3);
        }

        [Fact]
        public void TrueAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(5070000.0, -2387000.0, 1430000.0), new Vector3(2450.0, 6350.0, 6440.0), earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);

            Assert.Equal(30.57538215436613, sv.TrueAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 6);

            sv = new StateVector(new Vector3(1664000.0, -4862000.0, -2655000.0), new Vector3(7520.0, 890, 5520.0), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(329.477000818209, sv.TrueAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 6);
        }

        [Fact]
        public void EccentricAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(6700000.0, 2494000.0, 0.0), new Vector3(-2150.0, 8850.0, 0.0), earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);

            Assert.Equal(0.20776173316152752, sv.EccentricAnomaly(), 6);
        }

        [Fact]
        public void MeanAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(59.99823497591805, sv.MeanAnomaly() * IO.Astrodynamics.Constants.Rad2Deg, 3);
        }

        [Fact]
        public void Period()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(1.5501796754722221, sv.Period().TotalHours, 6);
        }

        [Fact]
        public void MeanMotion()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            StateVector sv = new StateVector(new Vector3(-5775.068936894231E+03, -3372.353197848874E+03, 651.695854037289E+03),
                new Vector3(-0.661469579672604E+03, -7.147573777688288E+03, -2.915719736461653E+03), earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);

            Assert.Equal(0.0011258883596591215, sv.MeanMotion(), 3);
        }

        [Fact]
        public void ToFrame()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            //J2000->Ecliptic
            //Earth from sun at 0 TDB
            var sv = new StateVector(new Vector3(-26499033.67742509, 132757417.33833946, 57556718.47053819), new Vector3(-29.79426007, -5.01805231, -2.17539380), sun,
                new TimeSystem.Time(2000, 1, 1, 12, 0, 0), Frames.Frame.ICRF);

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
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            var epoch = new TimeSystem.Time(2000, 1, 1, 12, 0, 0);
            var earthFrame = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);

            //J2000->IAU_EARTH
            //Earth from sun at 0 TDB
            var sv = new StateVector(new Vector3(-26499033.67742509, 132757417.33833946, 57556718.47053819), new Vector3(-29.79426007, -5.01805231, -2.17539380), earth,
                epoch.ToTDB(), Frames.Frame.ICRF);

            double[] res = sv.ToFrame(earthFrame).ToStateVector().ToArray();
            // SPICE state transformation matrix (sxform_c, tools/spice_reference/spice_ref.c).
            Assert.Equal(-135352868.83073342, res[0], 6);
            Assert.Equal(-2583535.7733796588, res[1], 6);
            Assert.Equal(57553737.736814931, res[2], 6);
            Assert.Equal(-188.72096169477405, res[3], 6);
            Assert.Equal(9839.8755381037336, res[4], 6);
            Assert.Equal(-2.1567010716200286, res[5], 6);
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
            var sv = earth.GetEphemeris(TimeSystem.Time.J2000TDB, TestHelpers.Sun, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            Assert.Equal(
                new StateVector(new Vector3(-26499033677.425095, 132757417338.33946, 57556718470.53819),
                    new Vector3(-29794.26007042197, -5018.052308786111, -2175.3938028266693), TestHelpers.Sun, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), sv);
            var ke = sv.ToKeplerianElements();

            // Against SPICE OSCELT: the node, 1.3e-5 rad, is where the former acos lost 5.8e-12 rad (#368)
            var expected = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv);
            Assert.Equal(expected.A, ke.A, 1e-14 * expected.A);
            Assert.Equal(expected.E, ke.E, 1e-14);
            Assert.Equal(expected.I, ke.I, 1e-14);
            Assert.Equal(expected.RAAN, ke.RAAN, 1e-14 * expected.RAAN);
            Assert.Equal(expected.AOP, ke.AOP, 1e-14);
            Assert.Equal(expected.M, ke.M, 1e-14);
        }

        [Fact]
        public void ToKeplerianSingularity()
        {
            var svOrigin =
                new StateVector(new Vector3(0, 6700000, 0), new Vector3(7900, 0, 0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var keOrigin = svOrigin.ToKeplerianElements();
            Assert.Equal(new KeplerianElements(7045497.0180164594, 0.0490379908093026, 3.141592653589793, 0.0,
                4.71238898038469, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), keOrigin);
            var svFinal = keOrigin.ToStateVector();
            Assert.Equal(svFinal, svOrigin, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void ToParabolicKeplerian()
        {
            KeplerianElements ke = new KeplerianElements(Double.PositiveInfinity, 1.0, 10.0 * IO.Astrodynamics.Constants.Deg2Rad,
                20.0 * IO.Astrodynamics.Constants.Deg2Rad,
                30.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF, perigeeRadius: 6800000.0);
            var sv = ke.ToStateVector();
            var svref = SpiceAPI.Instance.ConvertConicElementsToStateVector(ke);
            var res = sv.ToKeplerianElements();
            Assert.Equal(ke, res, TestHelpers.KeplerComparer);
        }

        [Fact]
        public void ToEquatorial()
        {
            var moon = TestHelpers.MoonAtJ2000;
            var ra = moon.GetEphemeris(TimeSystem.Time.J2000TDB, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToEquatorial();
            Assert.Equal(new Equatorial(-0.19024413568211912, 3.8824377884372114, 402448639.8873273, TimeSystem.Time.J2000TDB), ra);
        }

        [Fact]
        public void RelativeTo()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var moonSv = originalSV.RelativeTo(TestHelpers.MoonAtJ2000, Aberration.None).ToStateVector();
            Assert.Equal(
                new StateVector(new Vector3(298408384.63343549, 266716833.39423338, 76102487.099902019), new Vector3(-643.53138771903275, 8666.0876840916299, 301.32570498227307),
                    TestHelpers.MoonAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
                moonSv,TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void RelativeToAndRevert()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var svSSB = originalSV.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
            var svEarth = svSSB.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
            var deltaP = svEarth.Position - originalSV.Position;
            var deltaV = svEarth.Velocity - originalSV.Velocity;
            Assert.Equal(Vector3.Zero, deltaP);
            Assert.Equal(Vector3.Zero, deltaV);
            Assert.Equal(TestHelpers.EarthAtJ2000, svEarth.Observer);
            Assert.Equal(TimeSystem.Time.J2000TDB, svEarth.Epoch);
            Assert.Equal(Frames.Frame.ICRF, svEarth.Frame);
        }

        [Fact]
        public void EllipticToKeplerian()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(200.0, 8000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var ke = originalSV.ToKeplerianElements();
            var sv = ke.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void ParabolicToKeplerian()
        {
            var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                perigeeRadius: 6800000.0);
            var sv = ke.ToStateVector();
            var ke2 = sv.ToKeplerianElements();

            var sv2 = ke2.ToStateVector();
            Assert.Equal(sv, sv2, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void HyperbolicToKeplerian()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(4000.0, 10000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var ke = originalSV.ToKeplerianElements();
            var sv = ke.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void EllipticToEquinoctial()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(200.0, 8000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var eq = originalSV.ToEquinoctial();
            var sv = eq.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void ParabolicToEquinoctial()
        {
            var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                perigeeRadius: 6800000.0);
            var sv = ke.ToStateVector();
            var eq = sv.ToEquinoctial();

            var sv2 = eq.ToStateVector();
            Assert.Equal(sv, sv2, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void HyperbolicToEquinoctial()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(4000.0, 10000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var eq = originalSV.ToEquinoctial();
            var sv = eq.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void EllipticToKeplerianUpdatePV()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(200.0, 8000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var ke = originalSV.ToKeplerianElements();
            var sv = ke.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void ParabolicToKeplerianUpdatePV()
        {
            var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                perigeeRadius: 6800000.0);
            var sv = ke.ToStateVector();
            var ke2 = sv.ToKeplerianElements();

            var sv2 = ke2.ToStateVector();
            Assert.Equal(sv, sv2, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void HyperbolicToKeplerianUpdatePV()
        {
            var originalSV = new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(4000.0, 10000.0, -300.0), TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var ke = originalSV.ToKeplerianElements();
            var sv = ke.ToStateVector();
            Assert.Equal(originalSV, sv, TestHelpers.StateVectorComparer);
        }

        [Fact]
        public void RelativeTo2()
        {
            var earthSv = TestHelpers.EarthAtJ2000.GetEphemeris(Astrodynamics.TimeSystem.Time.J2000TDB, TestHelpers.MoonAtJ2000, Frames.Frame.ICRF, Aberration.None)
                .ToStateVector();
            var earthSvFromSun = earthSv.RelativeTo(TestHelpers.Sun, Aberration.None).ToStateVector();
            Assert.Equal(
                new StateVector(new Vector3(-26499033677.42509, 132757417338.33946, 57556718470.53819),
                    new Vector3(-29794.26007042197, -5018.052308786111, -2175.3938028266693), TestHelpers.Sun, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), earthSvFromSun);
        }
        [Fact]
        public void CreateRtnRotation_CircularEquatorialOrbit()
        {
            // Circular equatorial orbit along +X, velocity along +Y
            var sv = new StateVector(
                new Vector3(7000000.0, 0.0, 0.0),
                new Vector3(0.0, 7546.0, 0.0),
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var rtn = sv.CreateRtnRotation();

            // R should be along +X
            Assert.Equal(1.0, rtn.Get(0, 0), 10);
            Assert.Equal(0.0, rtn.Get(0, 1), 10);
            Assert.Equal(0.0, rtn.Get(0, 2), 10);
            // T should be along +Y
            Assert.Equal(0.0, rtn.Get(1, 0), 10);
            Assert.Equal(1.0, rtn.Get(1, 1), 10);
            Assert.Equal(0.0, rtn.Get(1, 2), 10);
            // N should be along +Z
            Assert.Equal(0.0, rtn.Get(2, 0), 10);
            Assert.Equal(0.0, rtn.Get(2, 1), 10);
            Assert.Equal(1.0, rtn.Get(2, 2), 10);
        }

        [Fact]
        public void CreateRtnRotation_IsOrthogonal()
        {
            var sv = new StateVector(
                new Vector3(6800000.0, 1000000.0, -500000.0),
                new Vector3(-1000.0, 7000.0, 500.0),
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var rtn = sv.CreateRtnRotation();
            // R * R^T should be identity
            var product = rtn * rtn.Transpose();
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.Equal(i == j ? 1.0 : 0.0, product.Get(i, j), 10);
        }

        [Fact]
        public void ToRtn_RadialPosition_MapsToRComponent()
        {
            var sv = new StateVector(
                new Vector3(7000000.0, 0.0, 0.0),
                new Vector3(0.0, 7546.0, 0.0),
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var rtn = sv.ToRtn(sv.Position);
            Assert.Equal(7000000.0, rtn.X, 1); // R component
            Assert.Equal(0.0, rtn.Y, 1);       // T component
            Assert.Equal(0.0, rtn.Z, 1);       // N component
        }

        [Fact]
        public void FromRtn_RoundTrip()
        {
            var sv = new StateVector(
                new Vector3(6800000.0, 1000000.0, -500000.0),
                new Vector3(-1000.0, 7000.0, 500.0),
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var original = new Vector3(100.0, 200.0, 300.0);
            var rtn = sv.ToRtn(original);
            var recovered = sv.FromRtn(rtn);
            Assert.Equal(original.X, recovered.X, 6);
            Assert.Equal(original.Y, recovered.Y, 6);
            Assert.Equal(original.Z, recovered.Z, 6);
        }

        [Fact]
        public void RotateCovarianceToRtn_DiagonalCovariance()
        {
            // For aligned orbit, diagonal covariance stays diagonal
            var sv = new StateVector(
                new Vector3(7000000.0, 0.0, 0.0),
                new Vector3(0.0, 7546.0, 0.0),
                TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

            var cov = new Matrix(6, 6);
            cov.Set(0, 0, 100.0); // X variance → maps to R
            cov.Set(1, 1, 200.0); // Y variance → maps to T
            cov.Set(2, 2, 300.0); // Z variance → maps to N
            cov.Set(3, 3, 1.0);
            cov.Set(4, 4, 2.0);
            cov.Set(5, 5, 3.0);

            var rtnCov = sv.RotateCovarianceToRtn(cov);
            Assert.Equal(100.0, rtnCov.Get(0, 0), 6); // R variance
            Assert.Equal(200.0, rtnCov.Get(1, 1), 6); // T variance
            Assert.Equal(300.0, rtnCov.Get(2, 2), 6); // N variance
        }

        /// <summary>
        /// A 6×6 covariance in m², m²/s, m²/s², with distinct terms in every block.
        /// </summary>
        internal static Matrix DistinctCovariance()
        {
            var c = new double[6, 6];
            c[0, 0] = 100.0;
            c[1, 1] = 400.0;
            c[2, 2] = 900.0;
            c[3, 3] = 1.0e-4;
            c[4, 4] = 4.0e-4;
            c[5, 5] = 9.0e-4;
            c[1, 0] = c[0, 1] = 50.0;
            c[2, 0] = c[0, 2] = -30.0;
            c[2, 1] = c[1, 2] = 20.0;
            c[3, 0] = c[0, 3] = 0.05;
            c[4, 1] = c[1, 4] = 0.1;
            c[5, 2] = c[2, 5] = -0.15;
            c[4, 3] = c[3, 4] = 1.0e-5;
            c[5, 4] = c[4, 5] = 2.0e-5;
            return new Matrix(c);
        }

        /// <summary>
        /// At its own epoch, tagged as it is, a state vector is itself, with its covariance (#551).
        /// </summary>
        [Fact]
        public void ToStateVectorAtItsOwnEpoch_IsTheStateWithItsCovariance()
        {
            var epoch = new TimeSystem.Time(new DateTime(2024, 1, 15, 12, 0, 0), TimeFrame.UTCFrame);
            var sv = new StateVector(new Vector3(6778137.0, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0), TestHelpers.EarthAtJ2000, epoch, Frames.Frame.ICRF,
                DistinctCovariance());

            Assert.Same(sv, sv.ToStateVector(epoch));
        }

        /// <summary>
        /// At the instant of its epoch tagged in another time frame, a state vector keeps its position, velocity and
        /// covariance, bit for bit, and takes the tag it is read at (#551).
        /// </summary>
        [Theory]
        [InlineData("TDB")]
        [InlineData("TAI")]
        [InlineData("TT")]
        [InlineData("GPS")]
        public void ToStateVectorAtTheSameInstantInAnotherTimeFrame_KeepsTheStateAndItsCovariance(string frame)
        {
            var epoch = new TimeSystem.Time(new DateTime(2024, 1, 15, 12, 0, 0), TimeFrame.UTCFrame);
            var covariance = DistinctCovariance();
            var sv = new StateVector(new Vector3(6778137.0, 1000.0, -2000.0), new Vector3(10.0, 7500.0, 300.0), TestHelpers.EarthAtJ2000, epoch,
                Frames.Frame.ECLIPTIC_J2000, covariance);
            var tag = frame switch
            {
                "TDB" => epoch.ToTDB(),
                "TAI" => epoch.ToTAI(),
                "TT" => epoch.ToTDT(),
                _ => epoch.ConvertTo(TimeFrame.GPSFrame)
            };
            Assert.NotEqual(epoch, tag);

            var read = sv.ToStateVector(tag);

            Assert.Equal(sv.Position, read.Position);
            Assert.Equal(sv.Velocity, read.Velocity);
            Assert.Equal(tag, read.Epoch);
            Assert.Equal(sv.Frame, read.Frame);
            Assert.Equal(sv.Observer, read.Observer);
            Assert.True(read.Covariance.HasValue);
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                Assert.Equal(covariance.Get(i, j), read.Covariance.Value.Get(i, j));
        }

        /// <summary>
        /// At another instant, a state vector is its two-body motion, without a covariance: moving the covariance
        /// would take the state transition matrix (#551). The circular orbit is the closed form
        /// r (cos nΔt, sin nΔt, 0), Δt the TT seconds between the two UTC epochs, away from a leap second (#501, #508).
        /// </summary>
        [Fact]
        public void ToStateVectorAtAnotherEpoch_IsTheTwoBodyMotionWithoutCovariance()
        {
            var earth = TestHelpers.EarthAtJ2000;
            const double r = 6778137.0;
            double v = System.Math.Sqrt(earth.GM / r);
            double n = System.Math.Sqrt(earth.GM / (r * r * r));
            var epoch = new TimeSystem.Time(new DateTime(2024, 1, 15, 12, 0, 0), TimeFrame.UTCFrame);
            var sv = new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, v, 0.0), earth, epoch, Frames.Frame.ICRF, DistinctCovariance());
            const double dt = 600.0;

            var later = sv.ToStateVector(epoch + TimeSpan.FromSeconds(dt));

            Assert.False(later.Covariance.HasValue);
            Assert.Equal(epoch + TimeSpan.FromSeconds(dt), later.Epoch);
            var expectedPosition = new Vector3(r * System.Math.Cos(n * dt), r * System.Math.Sin(n * dt), 0.0);
            var expectedVelocity = new Vector3(-v * System.Math.Sin(n * dt), v * System.Math.Cos(n * dt), 0.0);
            // Measured 0 m and 0 m/s on Linux and Windows, 9.313e-10 m (one ulp of r) and 1.286e-12 m/s on macOS arm64 (run
            // 41571571334, 2026-11-03): the libm's cos and sin differ there by an ulp. Twice the worst, 1.9e-9 m and
            // 2.6e-12 m/s, is below the rounding floor that bounds it, 4 ulp of r (3.7e-9 m) and of v (3.6e-12 m/s), room
            // for an ulp of the libm's cos and sin on each component.
            double positionBound = 4.0 * (System.Math.BitIncrement(r) - r);
            double velocityBound = 4.0 * (System.Math.BitIncrement(v) - v);
            _output.WriteLine($"MEASURE #551 two-body motion at 600 s: {(later.Position - expectedPosition).Magnitude():E3} m, {(later.Velocity - expectedVelocity).Magnitude():E3} m/s");
            Assert.True((later.Position - expectedPosition).Magnitude() <= positionBound, $"{(later.Position - expectedPosition).Magnitude():E3} m");
            Assert.True((later.Velocity - expectedVelocity).Magnitude() <= velocityBound, $"{(later.Velocity - expectedVelocity).Magnitude():E3} m/s");
        }
    }
}
