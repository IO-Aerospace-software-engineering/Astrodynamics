using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class PlaneAlignmentManeuverTests
    {
        public PlaneAlignmentManeuverTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);


            var targetOrbit = new KeplerianElements(150000000.0, 1.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Sun, new DateTime(2021, 01, 01), Frames.Frame.ECLIPTIC_J2000);

            PlaneAlignmentManeuver planeAlignmentManeuver = new PlaneAlignmentManeuver(new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0), targetOrbit, eng);

            Assert.NotNull(planeAlignmentManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), planeAlignmentManeuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), planeAlignmentManeuver.MinimumEpoch);
            Assert.Equal(targetOrbit, planeAlignmentManeuver.TargetOrbit.AtEpoch(new DateTime(2021, 01, 01)));
            Assert.Equal(eng, planeAlignmentManeuver.Engine);
        }

        [Fact]
        public void CanExecuteAtDescendingNode()
        {
            var orbitalParams = new KeplerianElements(22959999.999999993, 0.5, 60.0 * Astrodynamics.Constants.Deg2Rad, 10.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
                TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 45.0 * Astrodynamics.Constants.Deg2Rad, 55.0 * Astrodynamics.Constants.Deg2Rad,
                30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PlaneAlignmentManeuver planeAlignmentManeuver = new PlaneAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var dnTrueAnomaly = 2.197937654;
            var anTrueAnomaly = dnTrueAnomaly + Astrodynamics.Constants.PI;

            //Execute at descending node
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly - 0.2)));
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly - 0.1)));
            Assert.True(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly + 0.1)));
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly + 0.2)));
        }

        [Fact]
        public void CanExecuteAtAscendingNode()
        {
            var orbitalParams = new KeplerianElements(22959999.999999993, 0.5, 60.0 * Astrodynamics.Constants.Deg2Rad, 10.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
                TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 45.0 * Astrodynamics.Constants.Deg2Rad, 55.0 * Astrodynamics.Constants.Deg2Rad,
                30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PlaneAlignmentManeuver planeAlignmentManeuver = new PlaneAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var dnTrueAnomaly = 2.197937654;
            var anTrueAnomaly = dnTrueAnomaly + Astrodynamics.Constants.PI;

            //Execute just after descending node to ensure is not capture by descending node
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly + 0.2)));

            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(anTrueAnomaly - 0.2)));
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(anTrueAnomaly - 0.1)));
            Assert.True(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(anTrueAnomaly + 0.1)));
            Assert.False(planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(anTrueAnomaly + 0.2)));
        }

        [Fact]
        public void ExecuteAtAscendingNode()
        {
            var orbitalParams = new KeplerianElements(11480000.0, 0.0, 60.0 * Astrodynamics.Constants.Deg2Rad, 10.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
                TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(11480000.0, 0.0, 45.0 * Astrodynamics.Constants.Deg2Rad, 55.0 * Astrodynamics.Constants.Deg2Rad,
                0.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 2000.0, 1900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PlaneAlignmentManeuver planeAlignmentManeuver = new PlaneAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var dnTrueAnomaly = 2.197937654;
            var anTrueAnomaly = dnTrueAnomaly + Astrodynamics.Constants.PI;

            planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(anTrueAnomaly - 0.1));

            //Execute at ascending node
            var res = planeAlignmentManeuver.TryExecute(orbitalParams.ToStateVector(anTrueAnomaly));

            Assert.Equal(new Vector3(-1485.9760225904602, 2563.0404801133755, -2458.4753263817515), planeAlignmentManeuver.DeltaV, TestHelpers.VectorComparer);
            Assert.Equal(1687.9426869962556, planeAlignmentManeuver.FuelBurned, 6);
        }

        [Fact]
        public void ExecuteAtDescendingNode()
        {
            var orbitalParams = new KeplerianElements(11480000.0, 0.0, 60.0 * Astrodynamics.Constants.Deg2Rad, 10.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0,
                TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(11480000.0, 0.0, 45.0 * Astrodynamics.Constants.Deg2Rad, 55.0 * Astrodynamics.Constants.Deg2Rad,
                0.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 2000.0, 1900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PlaneAlignmentManeuver planeAlignmentManeuver = new PlaneAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var dnTrueAnomaly = 2.197937654;
            var anTrueAnomaly = dnTrueAnomaly + Astrodynamics.Constants.PI;

            planeAlignmentManeuver.CanExecute(orbitalParams.ToStateVector(dnTrueAnomaly - 0.1));

            //Execute at ascending node
            var res = planeAlignmentManeuver.TryExecute(orbitalParams.ToStateVector(dnTrueAnomaly));

            Assert.Equal(new Vector3(1485.9760225904588, -2563.040480113374, 2458.47532638175), planeAlignmentManeuver.DeltaV,TestHelpers.VectorComparer);
            Assert.Equal(1687.9426869962549, planeAlignmentManeuver.FuelBurned,6);
        }
    }
}