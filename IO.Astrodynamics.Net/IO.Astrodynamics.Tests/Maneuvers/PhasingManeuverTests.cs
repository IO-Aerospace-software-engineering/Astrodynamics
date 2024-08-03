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
    public class PhasingManeuverTests
    {
        public PhasingManeuverTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PhasingManeuver maneuver = new PhasingManeuver(TestHelpers.EarthAtJ2000,new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0), 3.0, 2, eng);

            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), maneuver.MinimumEpoch);
            Assert.Equal(3.0, maneuver.TargetTrueLongitude);
            Assert.Equal((uint)2, maneuver.RevolutionNumber);
        }

        [Fact]
        public void CanExecute()
        {
            var orbitalParams = new KeplerianElements(13600000.0, 0.5, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(13600000.0, 0.5, 0.0, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PhasingManeuver maneuver = new PhasingManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, 3, spc.Engines.First());

            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(DateTimeExtension.J2000.AddSeconds(-10))));
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(DateTimeExtension.J2000.AddSeconds(-1))));
            Assert.True(maneuver.CanExecute(orbitalParams.ToStateVector(DateTimeExtension.J2000.AddSeconds(1))));
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(DateTimeExtension.J2000.AddSeconds(2))));
        }

        [Fact]
        public void Execute()
        {
            var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 345.0 * Astrodynamics.Constants.Deg2Rad, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            PhasingManeuver maneuver = new PhasingManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, 3, spc.Engines.First());
            maneuver.TryExecute(orbitalParams.ToStateVector());

            Assert.Equal(new Vector3(0.0, 14.039767793719875, 0.0), maneuver.DeltaV,TestHelpers.VectorComparer);
            Assert.Equal(6.0351723087570868, maneuver.FuelBurned,3);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T11:59:59.9196115"), TimeSpan.Parse("2.17:31:12.7762440")), maneuver.ManeuverWindow);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T11:59:59.9196115"), TimeSpan.FromSeconds(0.1207034)), maneuver.ThrustWindow);
        }
    }
}