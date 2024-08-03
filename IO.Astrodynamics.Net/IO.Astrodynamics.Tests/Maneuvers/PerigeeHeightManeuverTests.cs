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
    public class PerigeeHeightManeuverTests
    {
        public PerigeeHeightManeuverTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PerigeeHeightManeuver perigeeHeightManeuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0), 151000000.0, eng);

            Assert.NotNull(perigeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), perigeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), perigeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, perigeeHeightManeuver.TargetPerigeeHeight);
        }

        [Fact]
        public void Create2()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            PerigeeHeightManeuver perigeeHeightManeuver = new PerigeeHeightManeuver(new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0),
                new KeplerianElements(151000000, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.Earth, DateTimeExtension.J2000, Frames.Frame.ICRF), eng);

            Assert.NotNull(perigeeHeightManeuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), perigeeHeightManeuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), perigeeHeightManeuver.MinimumEpoch);
            Assert.Equal(151000000.0, perigeeHeightManeuver.TargetPerigeeHeight);
        }

        [Fact]
        public void CanExecute()
        {
            var orbitalParams = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 9000.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 100.0, 1000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000, 1000));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, spc.InitialOrbitalParameters.ApogeeVector().Magnitude() + 100000.0,
                spc.Engines.First());

            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(-30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(-10)).ToStateVector()));
            Assert.True(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(-30)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(-10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(10)).ToStateVector()));
            Assert.False(maneuver.CanExecute(orbitalParams.AtEpoch(DateTimeExtension.J2000.AddSeconds(30)).ToStateVector()));
            Assert.True(maneuver.CanExecute(orbitalParams.AtEpoch((DateTimeExtension.J2000 + (orbitalParams.Period() * 0.5)).AddSeconds(10)).ToStateVector()));
        }

        [Fact]
        public void TryExecuteIncreasePerigee()
        {
            var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 42164000.0, spc.Engines.First());

            var maneuverPoint = orbitalParams.ToStateVector(orbitalParams.Epoch + orbitalParams.Period() * 0.5);
            var res = maneuver.TryExecute(maneuverPoint);
            Assert.Equal(new StateOrientation(new Quaternion(0.0,-1.0,1.2250148722424995E-16,0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame), res.so);
            Assert.Equal(new Vector3( -2.9716210510169284E-13, -2425.783652387101, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 12, 45, 06, 27, 303).AddTicks(8), TimeSpan.FromSeconds(16.0691385)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 12, 45, 06, 27, 303).AddTicks(8), TimeSpan.FromSeconds(16.0691385)), maneuver.ManeuverWindow);
            Assert.Equal(803.45692655552136, maneuver.FuelBurned,3);
        }

        [Fact]
        public void TryExecuteDecreasePerigee()
        {
            var orbitalParams = new StateVector(new Vector3(42164000.0, 0.0, 0.0), new Vector3(0.0, 3075.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new PerigeeHeightManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 6678000.0, spc.Engines.First());

            var maneuverPoint = orbitalParams.ToStateVector(orbitalParams.Epoch + orbitalParams.Period() * 0.5);
            var res = maneuver.TryExecute(maneuverPoint);
            Assert.Equal(new StateVector(maneuverPoint.Position, maneuverPoint.Velocity, maneuverPoint.Observer, maneuverPoint.Epoch, maneuverPoint.Frame), res.sv);
            Assert.Equal(new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame), res.so);
            Assert.Equal(new Vector3(1.7963196147880806E-13, 1466.487832468652, 0.0), maneuver.DeltaV);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 23, 58, 08, 663, 658).AddTicks(2), TimeSpan.FromSeconds(10.7439713)), maneuver.ThrustWindow);
            Assert.Equal(new Window(new DateTime(2000, 01, 01, 23, 58, 08, 663, 658).AddTicks(2), TimeSpan.FromSeconds(10.7439713)), maneuver.ManeuverWindow);
            Assert.Equal(537.19856491695327, maneuver.FuelBurned,3);
        }
    }
}