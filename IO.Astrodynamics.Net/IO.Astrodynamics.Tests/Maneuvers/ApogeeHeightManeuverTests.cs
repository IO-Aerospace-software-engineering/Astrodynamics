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
    public class ApogeeHeightManeuverTests
    {
        public ApogeeHeightManeuverTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            ApogeeHeightManeuver apogeeHeightManeuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0), 151000000.0, eng);

            Assert.NotNull(apogeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), apogeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), apogeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, apogeeHeightManeuver.TargetApogee);
        }

        [Fact]
        public void Create2()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            ApogeeHeightManeuver apogeeHeightManeuver = new ApogeeHeightManeuver(new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0),
                new KeplerianElements(151000000, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Earth, DateTimeExtension.J2000, Frames.Frame.ICRF), eng);

            Assert.NotNull(apogeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), apogeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), apogeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, apogeeHeightManeuver.TargetApogee);
        }

        [Fact]
        public void CanExecute()
        {
            var orbitalParams = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 9000.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 100.0, 1000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000, 1000));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApogeeHeightManeuver maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, spc.InitialOrbitalParameters.ApogeeVector().Magnitude() + 100000.0,
                spc.Engines.First());
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(-30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(-10)).ToStateVector()));
            Assert.True(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(-30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(-10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(30)).ToStateVector()));
        }

        [Fact]
        public void TryExecuteIncreaseApogee()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApogeeHeightManeuver maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 42164000.0, spc.Engines.First());

            var res = maneuver.TryExecute(orbitalParams);
            Assert.Equal(new StateVector(orbitalParams.Position, orbitalParams.Velocity, orbitalParams.Observer, orbitalParams.Epoch, orbitalParams.Frame), res.sv);
            Assert.Equal(new StateOrientation(Quaternion.Zero, Vector3.Zero, orbitalParams.Epoch, orbitalParams.Frame), res.so);
            Assert.Equal(new Vector3(0.0, 2424.6084273080614, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 11, 59, 49, 301, 844), TimeSpan.FromSeconds(16.0632973)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 11, 59, 49, 301, 844), TimeSpan.FromSeconds(16.0632973)), maneuver.ManeuverWindow);
            Assert.Equal(803.16486707364015, maneuver.FuelBurned);
        }
        
        [Fact]
        public void TryExecuteDecreaseApogee()
        {
            var orbitalParams = new StateVector(new Vector3(42164000.0, 0.0, 0.0), new Vector3(0.0, 3075.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApogeeHeightManeuver maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 6678000.0, spc.Engines.First());

            var res = maneuver.TryExecute(orbitalParams);
            Assert.Equal(new StateVector(orbitalParams.Position, orbitalParams.Velocity, orbitalParams.Observer, orbitalParams.Epoch, orbitalParams.Frame), res.sv);
            Assert.Equal(new StateOrientation(new Quaternion(0.0,-1.0,0.0,0.0), Vector3.Zero, orbitalParams.Epoch, orbitalParams.Frame), res.so);
            Assert.Equal(new Vector3(0.0, -1467.1724438487042, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 11, 59, 52, 841, 699).AddTicks(3), TimeSpan.FromSeconds(10.7481993)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 11, 59, 52, 841, 699).AddTicks(3), TimeSpan.FromSeconds(10.7481993)), maneuver.ManeuverWindow);
            Assert.Equal(537.40996740060154, maneuver.FuelBurned);
        }
    }
}