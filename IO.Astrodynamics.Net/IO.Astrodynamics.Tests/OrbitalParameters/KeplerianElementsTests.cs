using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class KeplerianElementsTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public KeplerianElementsTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            TimeSystem.Time epoch = new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame);
            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF);
            Assert.Equal(20000.0, ke.SemiMajorAxis());
            Assert.Equal(0.5, ke.Eccentricity());
            Assert.Equal(30.0, ke.Inclination() * IO.Astrodynamics.Constants.Rad2Deg, 14);
            Assert.Equal(40.0, ke.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg);
            Assert.Equal(50.0, ke.ArgumentOfPeriapsis() * IO.Astrodynamics.Constants.Rad2Deg);
            Assert.Equal(10.0, ke.MeanAnomaly() * IO.Astrodynamics.Constants.Rad2Deg);
            Assert.Equal(earth, ke.Observer);
            Assert.Equal(epoch, ke.Epoch);
            Assert.Equal(Frames.Frame.ICRF, ke.Frame);
            // #476: ArgumentOutOfRangeException naming the eccentricity, as the CCSDS types (an ArgumentException before)
            Assert.Throws<ArgumentOutOfRangeException>(() => new KeplerianElements(20000, -0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, 0.5, 181 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentNullException>(() => new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, null, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentNullException>(() => new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                null));
        }

        [Fact]
        public void ToKeplerian()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            TimeSystem.Time epoch = new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame);
            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF);
            var ke2 = ke.ToKeplerianElements();
            Assert.Equal(ke, ke2);
        }

        [Fact]
        public void ToStateVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(6.800803544958167E+06, 1.353139738203394E-03,
                5.171921958517460E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                3.257605322534260E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                1.062574316262159E+02 * IO.Astrodynamics.Constants.Deg2Rad,
                4.541224977546975E+01 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.CreateTDB(663724800.00001490), Frames.Frame.ICRF);
            StateVector sv = ke.ToStateVector();
            Assert.Equal(TimeSystem.Time.CreateTDB(663724800.00001490), sv.Epoch);
            Assert.Equal(-6116559.4695568951, sv.Position.X, 3);
            Assert.Equal(-1546174.6986767196, sv.Position.Y, 3);
            Assert.Equal(2521950.1574303135, sv.Position.Z, 3);
            Assert.Equal(-807.83831153459232, sv.Velocity.X, 3);
            Assert.Equal(-5477.6462810837966, sv.Velocity.Y, 3);
            Assert.Equal(-5297.6334029262234, sv.Velocity.Z, 3);
        }

        [Fact]
        public void ToStateVector2()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            Astrodynamics.OrbitalParameters.OrbitalParameters ke = new KeplerianElements(6.800803544958167E+06, 1.353139738203394E-03,
                5.171921958517460E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                3.257605322534260E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                1.062574316262159E+02 * IO.Astrodynamics.Constants.Deg2Rad,
                4.541224977546975E+01 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.CreateTDB(663724800.00001490), Frames.Frame.ICRF);
            StateVector sv = ke.ToStateVector(TimeSystem.Time.CreateTDB(663724800.00001490));
            Assert.Equal(-6116559.4695568951, sv.Position.X, 3);
            Assert.Equal(-1546174.6986767196, sv.Position.Y, 3);
            Assert.Equal(2521950.1574303135, sv.Position.Z, 3);
            Assert.Equal(-807.83831153459232, sv.Velocity.X, 3);
            Assert.Equal(-5477.6462810837966, sv.Velocity.Y, 3);
            Assert.Equal(-5297.6334029262234, sv.Velocity.Z, 3);
        }

        [Fact]
        public void SingularityZeroEccentricity()
        {
            KeplerianElements original = new KeplerianElements(42000000, 0.0, 1.0, 2.0, 1.0, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            KeplerianElements transformed = original.ToStateVector().ToKeplerianElements();
            Assert.Equal(original.A, transformed.A, 3);
            Assert.Equal(original.E, transformed.E, 6);
            Assert.Equal(original.I, transformed.I, 6);
            Assert.Equal(original.RAAN, transformed.RAAN);
            Assert.Equal(original.MeanLongitude(), transformed.MeanLongitude(), 6);
            Assert.Equal(original.AOP + original.M, (transformed.AOP + transformed.M) % Astrodynamics.Constants._2PI, 6);
        }

        [Fact]
        public void SingularityZeroEccentricityZeroInclination()
        {
            KeplerianElements original = new KeplerianElements(42000000, 0.2, 0.3, 2.0, 1.0, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var originalSv = original.ToStateVector();
            KeplerianElements transformed = originalSv.ToKeplerianElements();
            Assert.Equal(original.A, transformed.A, 3);
            Assert.Equal(original.E, transformed.E, 6);
            Assert.Equal(original.I, transformed.I, 6);
            Assert.Equal(original.MeanLongitude(), transformed.MeanLongitude(), 6);
        }

        [Fact]
        public void TrueAnomaly10()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(33.34284399634042, v * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void TrueAnomaly0()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(12800000.0, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(0.0, v * IO.Astrodynamics.Constants.Rad2Deg, 9);
        }

        [Fact]
        public void TrueAnomaly180()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(180.0, v * IO.Astrodynamics.Constants.Rad2Deg, 6);
        }

        [Fact]
        public void TrueAnomaly300()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 300.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(241.18499907498312, v * IO.Astrodynamics.Constants.Rad2Deg, 6);
        }

        [Fact]
        public void ExcentricityVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(7487.36, 0.0918, 0.0, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            Vector3 ev = ke.EccentricityVector();
            Assert.Equal(0.09180000000000033, ev.Magnitude(), 3);
            Assert.Equal(0.09180000000000033, ev.X, 3);
            Assert.Equal(0.0, ev.Y, 3);
            Assert.Equal(0.0, ev.Z, 3);
        }

        [Fact]
        public void AscendingNodeVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(8811.47, 0.228, IO.Astrodynamics.Constants.PI2 * 0.5, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            Vector3 anv = ke.AscendingNodeVector().Normalize();
            Assert.Equal(1.0, anv.Magnitude());
            Assert.Equal(1.0, anv.X);
            Assert.Equal(0.0, anv.Y);
            Assert.Equal(0.0, anv.Z);
        }

        [Fact]
        public void DescendingNodeVector()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(8811.47, 0.228, IO.Astrodynamics.Constants.PI2 * 0.5, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            Vector3 anv = ke.DescendingNodeVector().Normalize();
            Assert.Equal(1.0, anv.Magnitude());
            Assert.Equal(-1.0, anv.X);
            Assert.Equal(0.0, anv.Y);
            Assert.Equal(0.0, anv.Z);
        }

        [Fact]
        public void EccentricAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            double ea = ke.EccentricAnomaly();
            Assert.Equal(IO.Astrodynamics.Constants.PI, ea);
        }

        [Fact]
        public void SpecificAngularMomentum()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            KeplerianElements ke = new KeplerianElements(149753367811.78582, 0.0010241359778564595, 0.0, 0.0, 0.0, 0.0,
                sun, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            Vector3 sa = ke.SpecificAngularMomentum();
            Assert.Equal(0.0, sa.X);
            Assert.Equal(0.0, sa.Y);
            Assert.Equal(4458039255280901.5, sa.Z);
        }

        [Fact]
        public void SpecificOrbitalEnergyMomentum()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(6800811.78582, 0.00134,
                51.71 * IO.Astrodynamics.Constants.Deg2Rad,
                32.57 * IO.Astrodynamics.Constants.Deg2Rad, 105.64 * IO.Astrodynamics.Constants.Deg2Rad,
                46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            double energy = ke.SpecificOrbitalEnergy();
            Assert.Equal(-29305357.070616387, energy, 6);
        }

        [Fact]
        public void PerigeeVectorAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            var pv = ke.PerigeeVector();
            Assert.Equal(10000.0, pv.Magnitude(), 9);
            Assert.Equal(659.6961052988253, pv.X, 3);
            Assert.Equal(9213.804796489718, pv.Y, 3);
            Assert.Equal(3830.2222155948903, pv.Z, 3);
        }

        [Fact]
        public void ApogeeVectorAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            var av = ke.ApogeeVector();
            Assert.Equal(30000.0, av.Magnitude(), 9);
            Assert.Equal(-1979.0883158964757, av.X, 3);
            Assert.Equal(-27641.41438946915, av.Y, 3);
            Assert.Equal(-11490.66664678467, av.Z, 3);
        }

        [Fact]
        public void TrueAnomalyToMeanAnomaly()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;

            KeplerianElements km0 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0, 0.0, earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
                Frames.Frame.ICRF);
            KeplerianElements km90 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                90.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            KeplerianElements km180 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                180.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
            KeplerianElements km270 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                270.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);

            double v0 = km0.TrueAnomaly();
            double v90 = km90.TrueAnomaly();
            double v180 = km180.TrueAnomaly();
            double v270 = km270.TrueAnomaly();

            double m = km0.MeanAnomaly(v0);
            Assert.Equal(0.0, m);

            m = km90.MeanAnomaly(v90);
            Assert.Equal(90.0, m * IO.Astrodynamics.Constants.Rad2Deg, 12);

            m = km180.MeanAnomaly(v180);
            Assert.Equal(180.0, m * IO.Astrodynamics.Constants.Rad2Deg);

            m = km270.MeanAnomaly(v270);
            Assert.Equal(270.0, m * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void Equality()
        {
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            TimeSystem.Time epoch = new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame);
            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF);
            KeplerianElements ke2 = new KeplerianElements(20000, 0.5, 30.1 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF);

            Assert.False(ke == ke2);
            Assert.True(ke != ke2);
            Assert.False(ke.Equals(ke2));
            Assert.False(ke.Equals((object)ke2));
            Assert.True(ke.Equals((object)ke));
            Assert.False(ke.Equals(null));

            var ko = ke as Astrodynamics.OrbitalParameters.OrbitalParameters;
            var ko2 = ke2 as Astrodynamics.OrbitalParameters.OrbitalParameters;
            Assert.False(ko == ko2);
            Assert.True(ko != ko2);
            Assert.False(ko.Equals(ko2));
            Assert.False(ko.Equals((object)ko2));
            Assert.True(ko.Equals((object)ko));
            Assert.False(ko.Equals(null));
        }

        [Fact]
        public void TrueLongitude()
        {
            KeplerianElements ke = new KeplerianElements(12000000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            Assert.Equal(100.0, ke.TrueLongitude() * Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void MeanLongitude()
        {
            KeplerianElements ke = new KeplerianElements(20000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.Equal(100.0, ke.MeanLongitude() * Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void IsParabolic()
        {
            KeplerianElements ke = new KeplerianElements(double.PositiveInfinity, 1.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame),
                Frames.Frame.ICRF, perigeeRadius: 6800000.0);
            Assert.True(ke.IsParabolic());
        }

        [Fact]
        public void IsCircular()
        {
            KeplerianElements ke = new KeplerianElements(20000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.True(ke.IsCircular());
        }

        [Fact]
        public void IsHyperbolic()
        {
            KeplerianElements ke = new KeplerianElements(20000, 1.1, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.True(ke.IsHyperbolic());
        }

        [Fact]
        public void HyperbolicToState()
        {
            var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var sv = ke.ToStateVector();
            var ke2 = sv.ToKeplerianElements();
            Assert.Equal(ke.A, ke2.A, 3);
            Assert.Equal(ke.E, ke2.E, 6);
            Assert.Equal(ke.I, ke2.I, 6);
            Assert.Equal(ke.RAAN, ke2.RAAN, 6);
            Assert.Equal(ke.AOP, ke2.AOP, 6);
            Assert.Equal(ke.M, ke2.M, 6);
            Assert.Equal(ke.Epoch, ke2.Epoch);
            Assert.Equal(ke.Observer, ke2.Observer);
        }

        [Fact]
        public void ParabolicToState()
        {
            var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                perigeeRadius: 6800000.0);
            var sv = ke.ToStateVector();
            var ke2 = sv.ToKeplerianElements();
            Assert.Equal(ke, ke2, TestHelpers.KeplerComparer);
        }

        [Fact]
        public void EllipticToState()
        {
            var ke = new KeplerianElements(6800000.0, 0.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var sv = ke.ToStateVector();
            var ke2 = sv.ToKeplerianElements();
            Assert.Equal(ke.A, ke2.A, 3);
            Assert.Equal(ke.E, ke2.E, 6);
            Assert.Equal(ke.I, ke2.I, 6);
            Assert.Equal(ke.RAAN, ke2.RAAN, 6);
            Assert.Equal(ke.AOP, ke2.AOP, 6);
            Assert.Equal(ke.M, ke2.M, 6);
            Assert.Equal(ke.Epoch, ke2.Epoch);
            Assert.Equal(ke.Observer, ke2.Observer);
        }

        [Fact]
        public void HyperbolicToEquinoctial()
        {
            var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var eq = ke.ToEquinoctial();
            var ke2 = eq.ToKeplerianElements();
            Assert.Equal(ke, ke2, TestHelpers.KeplerComparer);
        }

        [Fact]
        public void ParabolicToEquinoctial()
        {
            var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                perigeeRadius: 6800000.0);
            var eq = ke.ToEquinoctial();
            var ke2 = eq.ToKeplerianElements();
            Assert.Equal(ke, ke2, TestHelpers.KeplerComparer);
        }

        [Fact]
        public void EllipticToEquinoctial()
        {
            var ke = new KeplerianElements(6800000.0, 0.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var eq = ke.ToEquinoctial();
            var ke2 = eq.ToKeplerianElements();
            Assert.Equal(ke, ke2, TestHelpers.KeplerComparer);
        }

        [Fact]
        public void SemiLatusRectum()
        {
            KeplerianElements ke = new KeplerianElements(6800000.0, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame),
                Frames.Frame.ICRF);
            Assert.Equal(5100000.0, ke.SemiLatusRectum(), 6);
        }

        [Fact]
        public void ParabolicPerigeeRadius()
        {
            KeplerianElements ke = new KeplerianElements(double.PositiveInfinity, 1.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad, 50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                new TimeSystem.Time(DateTime.Now, TimeFrame.TDBFrame), Frames.Frame.ICRF, perigeeRadius: 6800000.0);
            Assert.Equal(6800000.0, ke.PerigeeRadius(), 6);
        }

        [Fact]
        public void TimeToApogeeRadius()
        {
            var epoch = TimeSystem.Time.J2000TDB;
            KeplerianElements ke = new KeplerianElements(8000000.0, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad, 50.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                epoch, Frames.Frame.ICRF);
            var orbitalPeriod = ke.Period();
            var expectedApogeeTime = epoch + 0.5 * orbitalPeriod;
            var apogeeRadius = ke.ApogeeVector().Magnitude();
            Assert.Equal(expectedApogeeTime, ke.TimeToRadius(apogeeRadius), TestHelpers.TimeComparer);
        }

        [Fact]
        public void TimeToPerigeeRadius()
        {
            var epoch = TimeSystem.Time.J2000TDB;
            KeplerianElements ke = new KeplerianElements(8000000.0, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad, 50.0 * IO.Astrodynamics.Constants.Deg2Rad, Astrodynamics.Constants.PI, TestHelpers.EarthAtJ2000,
                epoch, Frames.Frame.ICRF);
            var orbitalPeriod = ke.Period();
            var expectedApogeeTime = epoch + 0.5 * orbitalPeriod;
            var perigeeRadius = ke.PerigeeVector().Magnitude();
            Assert.Equal(expectedApogeeTime, ke.TimeToRadius(perigeeRadius), TestHelpers.TimeComparer);
        }

        // #686: the eccentricities of the apsis tests, from the circular orbit through the rounding level of the
        // eccentricity vector (|e| about 1e-16, where v × h / GM − r̂ cancels) to a high ellipse, and the semi-major axes
        internal static readonly double[] ApsisEccentricities = [0.0, 1e-18, 1e-17, 1e-16, 2e-16, 1e-15, 1e-12, 1e-9, 1e-6, 1e-3, 0.1, 0.7, 0.95];
        internal static readonly double[] ApsisSemiMajorAxes = [6800e3, 7000e3, 7178e3, 8000e3, 42164e3];

        // Seeded random elements of an eccentricity: the first two equatorial (prograde and retrograde), the others at
        // random angles; the mean anomaly is random too, the apsides do not depend on it
        internal static System.Collections.Generic.IEnumerable<KeplerianElements> RandomApsisElements(double e, int seed, int count)
        {
            var random = new Random(seed);
            for (int k = 0; k < count; k++)
            {
                double i = k == 0 ? 0.0 : k == 1 ? System.Math.PI : random.NextDouble() * System.Math.PI;
                double raan = random.NextDouble() * IO.Astrodynamics.Constants._2PI;
                double aop = random.NextDouble() * IO.Astrodynamics.Constants._2PI;
                double m = random.NextDouble() * IO.Astrodynamics.Constants._2PI;
                yield return new KeplerianElements(ApsisSemiMajorAxes[k % ApsisSemiMajorAxes.Length], e, i, raan, aop, m, TestHelpers.EarthAtJ2000,
                    TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            }
        }

        // The position of the conic of the elements at a mean anomaly, by SPICE conics (N0067), the reference of the apsides
        internal static Vector3 SpiceConicPosition(double a, double e, double i, double raan, double aop, double meanAnomaly)
        {
            var ke = new KeplerianElements(a, e, i, raan, aop, meanAnomaly, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            return SpiceAPI.Instance.ConvertConicElementsToStateVector(ke).Position;
        }

        internal static double AngleBetween(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

        // #686: the worst angle between an apsis vector of the elements and its reference: the perigee against the SPICE
        // conic at M = 0, the apogee against the opposite direction (the apoapsis of the conic is −a(1 + e) P̂; conics at
        // M = π propagates half an orbit and resolves only 6e-15 to 1.3e-14 rad), over 13 eccentricities from 0 to 0.95 and
        // 2000 sets of angles each, MEASURE 3.412e-16, 3.640e-16 and 3.565e-16 rad on Linux x64, Windows x64 and macOS arm64
        // (CI run 43745883709, 2026-11-15); twice the worst, rounded up at the second significant digit. On main: zero
        // vectors and angles up to 3.14 rad at e <= 2e-16, 8.6e-7 rad at 1e-9
        internal const double ApsisAngleTolerance = 7.3e-16;

        // #686: the worst | |r_p| / |r_p,SPICE| − 1 | and | |r_a| / (a (1 + e)) − 1 |, MEASURE 4.441e-16 on each of the three
        // platforms (CI run 43745883709, 2026-11-15); twice the worst, as above
        internal const double ApsisLengthTolerance = 8.9e-16;

        // #686: the worst | v_apsis / √(GM (2 / r − 1 / a)) − 1 |, r = a(1 ∓ e), MEASURE 7.105e-15, 7.438e-15 and 7.105e-15
        // on Linux x64, Windows x64 and macOS arm64 (CI run 43745883709, 2026-11-15), the ulp of the radius amplified by
        // the cancellation of 2 / r − 1 / a at the apogee of e = 0.95; twice the worst, as above
        internal const double ApsisVelocityTolerance = 1.5e-14;

        // The angle of each apsis against its reference, and the relative error of its radius, for orbital parameters whose
        // periapsis is the SPICE conic position spicePerigee, of semi-major axis a and eccentricity e
        internal static (double Angle, double Length) ApsisErrors(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit, in Vector3 spicePerigee,
            double a, double e)
        {
            var perigee = orbit.PerigeeVector();
            var apogee = orbit.ApogeeVector();
            double angle = System.Math.Max(AngleBetween(perigee, spicePerigee), AngleBetween(apogee, spicePerigee.Inverse()));
            double length = System.Math.Max(System.Math.Abs(perigee.Magnitude() / spicePerigee.Magnitude() - 1.0),
                System.Math.Abs(apogee.Magnitude() / (a * (1.0 + e)) - 1.0));
            return (angle, length);
        }

        // #686: PerigeeVector and ApogeeVector are the conic at periapsis and apoapsis, r(ν = 0) = a(1 − e) P̂ and
        // r(ν = π) = −a(1 + e) P̂, P̂ the first column of R3(−Ω) R1(−i) R3(−ω) (D. A. Vallado, Fundamentals of Astrodynamics
        // and Applications, 4th ed., 2013, algorithm 10 COE2RV; confirmed by S. Guillet, 2026-11-15), at every eccentricity, the
        // circular orbit included, where the periapsis lies at ω from the node; SPICE conics the reference of P̂
        [Fact]
        public void PerigeeAndApogeeVectors_AreTheConicAtPeriapsisAndApoapsisOfSpice_AtEveryEccentricity()
        {
            double worstAngle = 0.0, worstLength = 0.0, worstVelocity = 0.0;
            foreach (double e in ApsisEccentricities)
            {
                double worstAngleE = 0.0;
                foreach (var ke in RandomApsisElements(e, 6860 + System.Array.IndexOf(ApsisEccentricities, e), 2000))
                {
                    var spicePerigee = SpiceConicPosition(ke.A, ke.E, ke.I, ke.RAAN, ke.AOP, 0.0);
                    var (angle, length) = ApsisErrors(ke, spicePerigee, ke.A, ke.E);
                    worstAngleE = System.Math.Max(worstAngleE, angle);
                    worstLength = System.Math.Max(worstLength, length);

                    double gm = ke.Observer.GM;
                    double vp = System.Math.Sqrt(gm * (2.0 / (ke.A * (1.0 - ke.E)) - 1.0 / ke.A));
                    double va = System.Math.Sqrt(gm * (2.0 / (ke.A * (1.0 + ke.E)) - 1.0 / ke.A));
                    worstVelocity = System.Math.Max(worstVelocity, System.Math.Max(System.Math.Abs(ke.PerigeeVelocity() / vp - 1.0),
                        System.Math.Abs(ke.ApogeeVelocity() / va - 1.0)));
                }

                _output.WriteLine($"MEASURE #686 elements e = {e:E1}: worst apsis angle against SPICE conics {worstAngleE:E3} rad");
                worstAngle = System.Math.Max(worstAngle, worstAngleE);
            }

            _output.WriteLine($"MEASURE #686 elements: worst apsis angle {worstAngle:E3} rad, apsis radius {worstLength:E3}, apsis velocity {worstVelocity:E3}");
            Assert.True(worstAngle <= ApsisAngleTolerance, $"{worstAngle:E3}");
            Assert.True(worstLength <= ApsisLengthTolerance, $"{worstLength:E3}");
            Assert.True(worstVelocity <= ApsisVelocityTolerance, $"{worstVelocity:E3}");
        }

        // Review of #686: mean elements have no position, so no apsis vector. The apsides went through the eccentricity
        // vector of the state, which throws for mean elements (ToStateVector, "Cannot convert mean elements directly to a
        // state vector"); the perifocal axis of #686 had made them return a mean apsis silently, and PerigeeHeightManeuver,
        // ApogeeHeightManeuver and Launch accept a mean target orbit through them. They throw again, at every eccentricity,
        // as EccentricityVector of the same elements does
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.01)]
        [InlineData(0.7)]
        public void ApsisVectors_OfMeanElements_Throw_AsTheirEccentricityVectorDoes(double e)
        {
            var mean = new KeplerianElements(7000e3, e, 0.5, 1.0, 2.0, 0.3, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
                null, null, null, OrbitalElementsType.Mean);
            var meanEquinoctial = new EquinoctialElements(7000e3 * (1.0 - e * e), e * System.Math.Cos(3.0), e * System.Math.Sin(3.0),
                System.Math.Tan(0.25) * System.Math.Cos(1.0), System.Math.Tan(0.25) * System.Math.Sin(1.0), 3.3, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, null, OrbitalElementsType.Mean);

            foreach (IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit in new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[] { mean, meanEquinoctial })
            {
                Assert.Throws<InvalidOperationException>(() => orbit.EccentricityVector());
                Assert.Contains("mean elements", Assert.Throws<InvalidOperationException>(() => orbit.PerigeeVector()).Message);
                Assert.Contains("mean elements", Assert.Throws<InvalidOperationException>(() => orbit.ApogeeVector()).Message);
                Assert.Contains("mean elements", Assert.Throws<InvalidOperationException>(() => orbit.PerigeeVelocity()).Message);
                Assert.Contains("mean elements", Assert.Throws<InvalidOperationException>(() => orbit.ApogeeVelocity()).Message);
            }

            // The magnitude alone stays available, a(1 − e) of the elements
            Assert.Equal(7000e3 * (1.0 - e), mean.PerigeeRadius());
        }

        // Review of #686: a TLE holds mean elements, but its semi-major axis, eccentricity and angles are those of its
        // osculating elements at its epoch (SGP4/SDP4), so its apsides are those of its osculating orbit and do not throw
        // with the mean elements above. Reference: the periapsis of the SGP4 state by SPICE oscelt then conics at M = 0
        // (N0067); the radii are a(1 ∓ e) of the same elements. MEASURE (ISS, e about 1e-3 osculating; e = 0.72) 1.460e-13 and
        // 1.279e-16 rad on Linux x64, 1.844e-13 and 1.137e-16 on Windows x64, 1.459e-13 and 1.279e-16 on macOS arm64 (CI run
        // 43745883709, 2026-11-15); twice the worst, rounded up at the second significant digit
        private const double TleApsisAngleTolerance = 3.7e-13;

        [Theory]
        [InlineData("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703")]
        [InlineData("CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
            "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468")]
        public void ApsisVectors_OfATle_AreAlongThePeriapsisOfItsSgp4State(string name, string line1, string line2)
        {
            var tle = new IO.Astrodynamics.OrbitalParameters.TLE.TLE(name, line1, line2);
            var state = tle.ToStateVector();
            var osculating = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
            var spicePerigee = SpiceConicPosition(osculating.A, osculating.E, osculating.I, osculating.RAAN, osculating.AOP, 0.0);

            double perigeeAngle = AngleBetween(tle.PerigeeVector(), spicePerigee);
            double apogeeAngle = AngleBetween(tle.ApogeeVector(), spicePerigee.Inverse());
            _output.WriteLine($"MEASURE review of #686 TLE {name}: apsis angle against SPICE oscelt + conics of its SGP4 state {System.Math.Max(perigeeAngle, apogeeAngle):E3} rad");
            Assert.True(perigeeAngle <= TleApsisAngleTolerance, $"{perigeeAngle:E3}");
            Assert.True(apogeeAngle <= TleApsisAngleTolerance, $"{apogeeAngle:E3}");

            double a = tle.SemiMajorAxis();
            double e = tle.Eccentricity();
            Assert.Equal(a * (1.0 - e), tle.PerigeeVector().Magnitude(), a * 1e-15);
            Assert.Equal(a * (1.0 + e), tle.ApogeeVector().Magnitude(), a * 1e-15);
        }
    }
}
