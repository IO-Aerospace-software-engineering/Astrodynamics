using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class ApsidalAlignmentManeuevrTests
    {
        public ApsidalAlignmentManeuevrTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, sun, new DateTime(2021, 01, 01), Frames.Frame.ECLIPTIC_J2000);
            Clock clk1 = new Clock("My clock", 256);
            Payload pl1 = new Payload("pl1", 300, "sn");
            Spacecraft spc1 = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clk1, ke);
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            FuelTank fuelTank11 = new FuelTank("My fuel tank11", "ft2021", "sn1", 4000.0, 4000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            spc1.AddFuelTank(fuelTank10);
            spc1.AddFuelTank(fuelTank11);
            spc1.AddPayload(pl1);
            spc1.AddEngine(eng);

            var targetOrbit = new KeplerianElements(150000000.0, 1.0, 0.0, 0.0, 0.0, 0.0, sun, new DateTime(2021, 01, 01), Frames.Frame.ECLIPTIC_J2000);

            ApsidalAlignmentManeuver maneuver = new ApsidalAlignmentManeuver(new DateTime(2021, 1, 1), TimeSpan.FromDays(1.0), targetOrbit, spc1.Engines.First());
            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), maneuver.MinimumEpoch);
            Assert.Equal(eng, maneuver.Engine);
            Assert.Equal(targetOrbit, maneuver.TargetOrbit);
        }

        [Fact]
        public void CanExecute()
        {
            var orbitalParams = new KeplerianElements(14999992.500003746, 0.333333, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApsidalAlignmentManeuver maneuver = new ApsidalAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());

            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(150.0 * Astrodynamics.Constants.Deg2Rad))); // P incoming
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(155.0 * Astrodynamics.Constants.Deg2Rad))); // P incoming
            Assert.True(maneuver.CanExecute(orbitalParams.ToStateVector(156.5 * Astrodynamics.Constants.Deg2Rad))); // P
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(157.0 * Astrodynamics.Constants.Deg2Rad))); // P is behind
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(341.0 * Astrodynamics.Constants.Deg2Rad))); //Q incoming
            Assert.True(maneuver.CanExecute(orbitalParams.ToStateVector(341.8 * Astrodynamics.Constants.Deg2Rad))); //Q
            Assert.False(maneuver.CanExecute(orbitalParams.ToStateVector(343.0 * Astrodynamics.Constants.Deg2Rad))); //Q is behind
        }

        [Fact]
        public void ExecuteAtP()
        {
            var orbitalParams = new KeplerianElements(14999992.500003746, 0.333333, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApsidalAlignmentManeuver maneuver = new ApsidalAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var res = maneuver.TryExecute(orbitalParams.ToStateVector(156.5 * Astrodynamics.Constants.Deg2Rad));
            Assert.Equal(maneuver.DeltaV, new Vector3(-1352.4744534060974, 564.6811827253609, 0.0));
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T13:55:44.2314095"), TimeSpan.FromSeconds(10.7386318)), maneuver.ManeuverWindow);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T13:55:44.2314095"), TimeSpan.FromSeconds(10.7386318)), maneuver.ThrustWindow);
            Assert.Equal(536.93159217329242, maneuver.FuelBurned, 3);
        }

        [Fact]
        public void ExecuteAtQ()
        {
            var orbitalParams = new KeplerianElements(14999992.500003746, 0.333333, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
            var targetOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApsidalAlignmentManeuver maneuver = new ApsidalAlignmentManeuver(DateTime.MinValue, TimeSpan.Zero, targetOrbitalParams, spc.Engines.First());
            var res = maneuver.TryExecute(orbitalParams.ToStateVector(341.77 * Astrodynamics.Constants.Deg2Rad));
            Assert.Equal(maneuver.DeltaV, new Vector3(-1368.8299700588411, 498.1271161206587, 0.0), TestHelpers.VectorComparer);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T16:57:15.7421764"), TimeSpan.FromSeconds(10.6831353)), maneuver.ManeuverWindow);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T16:57:15.7421764"), TimeSpan.FromSeconds(10.6831353)), maneuver.ThrustWindow);
            Assert.Equal(534.15676829508755, maneuver.FuelBurned, 6);
        }
    }
}