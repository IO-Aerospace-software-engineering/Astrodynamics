using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters
{
    public class KeplerianElementsTests
    {
        public KeplerianElementsTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            DateTime epoch = DateTime.Now;
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
            Assert.Throws<ArgumentException>(() => new KeplerianElements(-20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, -0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, 0.5, 181 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                -40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                -50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
                Frames.Frame.ICRF));
            Assert.Throws<ArgumentException>(() => new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, -10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, epoch,
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
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            DateTime epoch = DateTime.Now;
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
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(6.800803544958167E+06, 1.353139738203394E-03,
                5.171921958517460E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                3.257605322534260E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                1.062574316262159E+02 * IO.Astrodynamics.Constants.Deg2Rad,
                4.541224977546975E+01 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTimeExtension.CreateTDB(663724800.00001490), Frames.Frame.ICRF);
            StateVector sv = ke.ToStateVector();
            Assert.Equal(-6116559.468933809, sv.Position.X, 6);
            Assert.Equal(-1546174.6944518015, sv.Position.Y, 6);
            Assert.Equal(2521950.161516389, sv.Position.Z, 6);
            Assert.Equal(-807.83831153459232, sv.Velocity.X, 6);
            Assert.Equal(-5477.6462810837966, sv.Velocity.Y, 6);
            Assert.Equal(-5297.6334029262234, sv.Velocity.Z, 6);
        }

        [Fact]
        public void ToStateVector2()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Astrodynamics.OrbitalParameters.OrbitalParameters ke = new KeplerianElements(6.800803544958167E+06, 1.353139738203394E-03,
                5.171921958517460E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                3.257605322534260E+01 * IO.Astrodynamics.Constants.Deg2Rad,
                1.062574316262159E+02 * IO.Astrodynamics.Constants.Deg2Rad,
                4.541224977546975E+01 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTimeExtension.CreateTDB(663724800.00001490), Frames.Frame.ICRF);
            StateVector sv = ke.ToStateVector(DateTimeExtension.CreateTDB(663724800.00001490));
            Assert.Equal(-6116559.468933809, sv.Position.X, 6);
            Assert.Equal(-1546174.6944518015, sv.Position.Y, 6);
            Assert.Equal(2521950.161516389, sv.Position.Z, 6);
            Assert.Equal(-807.83831153459232, sv.Velocity.X, 6);
            Assert.Equal(-5477.646280596454, sv.Velocity.Y, 6);
            Assert.Equal(-5297.6334029262234, sv.Velocity.Z, 6);
        }

        [Fact]
        public void SingularityZeroEccentricity()
        {
            KeplerianElements original = new KeplerianElements(42000000, 0.0, 1.0, 2.0, 1.0, 0.5, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            KeplerianElements transformed = original.ToStateVector().ToKeplerianElements();
            Assert.Equal(original.A, transformed.A, 3);
            Assert.Equal(original.E, transformed.E, 6);
            Assert.Equal(original.I, transformed.I, 6);
            Assert.Equal(original.RAAN, transformed.RAAN);
            Assert.Equal(original.MeanLongitude(), transformed.MeanLongitude());
            Assert.Equal(original.AOP + original.M, (transformed.AOP + transformed.M) % Astrodynamics.Constants._2PI);
        }

        [Fact]
        public void SingularityZeroEccentricityZeroInclination()
        {
            KeplerianElements original = new KeplerianElements(42000000, 0.0, 0.0, 2.0, 1.0, 0.5, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
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
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow, Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(33.342843885635396, v * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void TrueAnomaly0()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow, Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(0.0, v * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void TrueAnomaly180()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow,
                Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(180.0, v * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void TrueAnomaly300()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 300.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow,
                Frames.Frame.ICRF);
            double v = ke.TrueAnomaly();
            Assert.Equal(241.18499907498312, v * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void ExcentricityVector()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(7487.36, 0.0918, 0.0, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Vector3 ev = ke.EccentricityVector();
            Assert.Equal(0.09180000000000033, ev.Magnitude(), 3);
            Assert.Equal(0.09180000000000033, ev.X, 3);
            Assert.Equal(0.0, ev.Y, 3);
            Assert.Equal(0.0, ev.Z, 3);
        }

        [Fact]
        public void AscendingNodeVector()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(8811.47, 0.228, IO.Astrodynamics.Constants.PI2 * 0.5, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Vector3 anv = ke.AscendingNodeVector().Normalize();
            Assert.Equal(1.0, anv.Magnitude());
            Assert.Equal(1.0, anv.X);
            Assert.Equal(0.0, anv.Y);
            Assert.Equal(0.0, anv.Z);
        }

        [Fact]
        public void DescendingNodeVector()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(8811.47, 0.228, IO.Astrodynamics.Constants.PI2 * 0.5, 0.0, 0.0,
                0.0 * IO.Astrodynamics.Constants.Deg2Rad, earth, DateTime.UtcNow, Frames.Frame.ICRF);
            Vector3 anv = ke.DescendingNodeVector().Normalize();
            Assert.Equal(1.0, anv.Magnitude());
            Assert.Equal(-1.0, anv.X);
            Assert.Equal(0.0, anv.Y);
            Assert.Equal(0.0, anv.Z);
        }

        [Fact]
        public void EccentricAnomaly()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow,
                Frames.Frame.ICRF);
            double ea = ke.EccentricAnomaly();
            Assert.Equal(IO.Astrodynamics.Constants.PI, ea);
        }

        [Fact]
        public void SpecificAngularMomentum()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            KeplerianElements ke = new KeplerianElements(149753367811.78582, 0.0010241359778564595, 0.0, 0.0, 0.0, 0.0,
                sun, DateTime.UtcNow, Frames.Frame.ICRF);
            Vector3 sa = ke.SpecificAngularMomentum();
            Assert.Equal(0.0, sa.X);
            Assert.Equal(0.0, sa.Y);
            Assert.Equal(4458039255280901.5, sa.Z);
        }

        [Fact]
        public void SpecificOrbitalEnergyMomentum()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(6800811.78582, 0.00134,
                51.71 * IO.Astrodynamics.Constants.Deg2Rad,
                32.57 * IO.Astrodynamics.Constants.Deg2Rad, 105.64 * IO.Astrodynamics.Constants.Deg2Rad,
                46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow, Frames.Frame.ICRF);
            double energy = ke.SpecificOrbitalEnergy();
            Assert.Equal(-29305357.070616387, energy, 6);
        }

        [Fact]
        public void PerigeeVectorAnomaly()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow,
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
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements ke = new KeplerianElements(20000, 0.5, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 180.0 * IO.Astrodynamics.Constants.Deg2Rad, earth,
                DateTime.UtcNow,
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
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);

            KeplerianElements km0 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0, 0.0, earth, DateTime.UtcNow,
                Frames.Frame.ICRF);
            KeplerianElements km90 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                90.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, DateTime.UtcNow, Frames.Frame.ICRF);
            KeplerianElements km180 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                180.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, DateTime.UtcNow, Frames.Frame.ICRF);
            KeplerianElements km270 = new KeplerianElements(20000, 0.3, 0.0, 0.0, 0.0,
                270.0 * IO.Astrodynamics.Constants.Deg2Rad,
                earth, DateTime.UtcNow, Frames.Frame.ICRF);

            double v0 = km0.TrueAnomaly();
            double v90 = km90.TrueAnomaly();
            double v180 = km180.TrueAnomaly();
            double v270 = km270.TrueAnomaly();

            double m = km0.MeanAnomaly(v0);
            Assert.Equal(0.0, m);

            m = km90.MeanAnomaly(v90);
            Assert.Equal(89.99999997471907, m * IO.Astrodynamics.Constants.Rad2Deg, 12);

            m = km0.MeanAnomaly(v180);
            Assert.Equal(180.0, m * IO.Astrodynamics.Constants.Rad2Deg);

            m = km0.MeanAnomaly(v270);
            Assert.Equal(270.0000000252809, m * IO.Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void Equality()
        {
            CelestialBody earth = new CelestialBody(PlanetsAndMoons.EARTH);
            DateTime epoch = DateTime.Now;
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
            KeplerianElements ke = new KeplerianElements(20000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, DateTime.Now,
                Frames.Frame.ICRF);
            Assert.Equal(100.0, ke.TrueLongitude() * Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void MeanLongitude()
        {
            KeplerianElements ke = new KeplerianElements(20000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, DateTime.Now,
                Frames.Frame.ICRF);
            Assert.Equal(100.0, ke.MeanLongitude() * Astrodynamics.Constants.Rad2Deg);
        }

        [Fact]
        public void IsParabolic()
        {
            KeplerianElements ke = new KeplerianElements(20000, 1.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, DateTime.Now,
                Frames.Frame.ICRF);
            Assert.True(ke.IsParabolic());
        }

        [Fact]
        public void IsCircular()
        {
            KeplerianElements ke = new KeplerianElements(20000, 0.0, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, DateTime.Now,
                Frames.Frame.ICRF);
            Assert.True(ke.IsCircular());
        }

        [Fact]
        public void IsHyperbolic()
        {
            KeplerianElements ke = new KeplerianElements(20000, 1.1, 30.0 * IO.Astrodynamics.Constants.Deg2Rad,
                40.0 * IO.Astrodynamics.Constants.Deg2Rad,
                50.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000, DateTime.Now,
                Frames.Frame.ICRF);
            Assert.True(ke.IsHyperbolic());
        }
    }
}