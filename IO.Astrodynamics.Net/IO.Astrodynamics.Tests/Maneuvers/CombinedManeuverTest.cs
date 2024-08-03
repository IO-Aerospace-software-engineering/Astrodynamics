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
    public class CombinedManeuverTest
    {
        public CombinedManeuverTest()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void Create()
        {
            FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
            Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);

            CombinedManeuver maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,new DateTime(2021, 01, 01), TimeSpan.FromDays(1.0), 151000000.0, 1.0, eng);

            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            Assert.Equal(new DateTime(2021, 01, 01), maneuver.MinimumEpoch);
            Assert.Equal(151000000.0, maneuver.TargetPerigeeHeight);
            Assert.Equal(1.0, maneuver.TargetInclination);
        }

        [Fact]
        public void CanExecute()
        {
            var orbitalParams = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 9000.0, 0.0), TestHelpers.EarthAtJ2000, DateTimeExtension.J2000,
                Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 100.0, 1000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000, 1000));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, spc.InitialOrbitalParameters.ApogeeVector().Magnitude() + 100000.0, 10.0,
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
            var orbitalParams = new KeplerianElements(24420999.959422689, 0.726546824, 28.5 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 42164000.0, 0.0, spc.Engines.First());

            var maneuverPoint = orbitalParams.ToStateVector(orbitalParams.Epoch + orbitalParams.Period() * 0.5);
            var res = maneuver.TryExecute(maneuverPoint);
            Assert.Equal(
                new StateOrientation(new Quaternion(0.21458639046372344, 0.9767050122876152, 0.0, -2.463944285530668E-11), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame),
                res.so);
            Assert.Equal(new Vector3( 1.9353959900568053E-08, -1661.6798912062677, 767.1890047800779), maneuver.DeltaV);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T17:15:44.8323340").ToTDB(), DateTime.Parse("2000-01-01T17:16:52.7296418").ToTDB()), maneuver.ManeuverWindow);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T17:15:44.8323340").ToTDB(), DateTime.Parse("2000-01-01T17:16:52.7296418").ToTDB()), maneuver.ThrustWindow);
            Assert.Equal(3394.8653913932048, maneuver.FuelBurned,3);
        }

        [Fact]
        public void TryExecuteDecreasePerigee()
        {
            var orbitalParams = new KeplerianElements(24420999.959422689, 0.726546824, 28.5 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000,
                DateTimeExtension.J2000, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 10000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 9000.0, 9000.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            var maneuver = new CombinedManeuver(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, 6600000.0, 0.0, spc.Engines.First());

            var maneuverPoint = orbitalParams.ToStateVector(orbitalParams.Epoch + orbitalParams.Period() * 0.5);
            var res = maneuver.TryExecute(maneuverPoint);
            Assert.Equal(
                new KeplerianElements(24381999.95942269, 0.72930850582462747, 0, 0, 6.2831853071795862, 3.1415926535897931, maneuverPoint.Observer, maneuverPoint.Epoch,
                    maneuverPoint.Frame), res.sv.ToKeplerianElements());
            Assert.Equal(
                new StateOrientation(new Quaternion(0.617876643698767, 0.7862750493126097, 0.0, -1.983508978702801E-11), Vector3.Zero, maneuverPoint.Epoch, maneuverPoint.Frame),
                res.so);
            Assert.Equal(new Vector3(1.9353612716996414E-08 , -186.70162770583374 , 767.1890047800779), maneuver.DeltaV);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T17:16:08.2299721").ToTDB(), DateTime.Parse("2000-01-01T17:16:40.9956911").ToTDB()), maneuver.ThrustWindow);
            Assert.Equal(new Window(DateTime.Parse("2000-01-01T17:16:08.2299721").ToTDB(), DateTime.Parse("2000-01-01T17:16:40.9956911").ToTDB()), maneuver.ManeuverWindow);
            Assert.Equal(1638.2859502950841, maneuver.FuelBurned);
        }
    }
}