using IO.Astrodynamics.Tests.Propagators;
using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class ApsidalAlignmentManeuevrTests
    {
        public ApsidalAlignmentManeuevrTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            CelestialBody sun = new CelestialBody(Stars.Sun);

            var ke = new KeplerianElements(150000000.0, 0.0, 0.0, 0.0, 0.0, 0.0, sun, new TimeSystem.Time(new DateTime(2021, 01, 01), TimeFrame.TDBFrame),
                Frames.Frame.ECLIPTIC_J2000);
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

            var targetOrbit = new KeplerianElements(double.PositiveInfinity, 1.0, 0.0, 0.0, 0.0, 0.0, sun, new TimeSystem.Time(new DateTime(2021, 01, 01), TimeFrame.TDBFrame),
                Frames.Frame.ECLIPTIC_J2000, perigeeRadius: 6800000.0);

            ApsidalAlignmentManeuver maneuver = new ApsidalAlignmentManeuver(new TimeSystem.Time(2021, 1, 1), TimeSpan.FromDays(1.0), targetOrbit, spc1.Engines.First());
            Assert.NotNull(maneuver.Engine);
            Assert.Equal(TimeSpan.FromDays(1.0), maneuver.ManeuverHoldDuration);
            LabelAssert.Equal(new TimeSystem.Time(new DateTime(2021, 01, 01), TimeFrame.TDBFrame), maneuver.MinimumEpoch);
            Assert.Equal(eng, maneuver.Engine);
            LabelAssert.Equal(targetOrbit, maneuver.TargetOrbit);
        }

        [Fact]
        public void ExecuteAtP()
        {
            var orbitalParams = new KeplerianElements(14999992.500003746, 0.333333, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var targtOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApsidalAlignmentManeuver maneuver =
                new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targtOrbitalParams, spc.Engines.First());
            var atP = orbitalParams.ToStateVector(156.5 * Astrodynamics.Constants.Deg2Rad);
            var res = maneuver.TryExecute(atP);
            Assert.Equal(maneuver.DeltaV, new Vector3(-1352.4744534060974, 564.6811827253609, 0.0),TestHelpers.VectorComparer);
            // The epoch of the true anomaly counts TT seconds from the epoch of the elements, the time of a two-body
            // motion about the Earth (#501, #508): 2.4e-6 s after the 13:55:44.2314095 of the TDB seconds, the change of
            // TDB − TT over the 2 h to it. The thrust lasts the nearest tick of the rocket equation (#637): 1900 kg / 50 kg/s
            // (1 − exp(−|Δv| / (450 s g0))) = 10.73863185460560 s at 40 digits (mpmath) from the |Δv| of 1465.623413476362 m/s
            // this execution gives, 107 386 319 ticks (107 386 318, truncated, before 11.0.0). The burn's acceleration
            // centred on the impulse (#335): from 13:55:45.7173641 (13:55:44.2314119 at 0.666 of the burn before 11.0.0)
            TimelineRules.AssertThrustWindow(atP.Epoch, maneuver.DeltaV.Magnitude(), 450.0, TimeSpan.FromTicks(107386319), maneuver.ThrustWindow);
            LabelAssert.Equal(maneuver.ThrustWindow, maneuver.ManeuverWindow);
            Assert.Equal(536.93159217329242, maneuver.FuelBurned, 3);
        }

        [Fact]
        public void ExecuteAtQ()
        {
            var orbitalParams = new KeplerianElements(14999992.500003746, 0.333333, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var targetOrbitalParams = new KeplerianElements(18000000.000000004, 0.5, 0.0, 0.0, 30.0 * Astrodynamics.Constants.Deg2Rad, 0.0, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

            ApsidalAlignmentManeuver maneuver =
                new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbitalParams, spc.Engines.First());
            var atQ = orbitalParams.ToStateVector(341.77 * Astrodynamics.Constants.Deg2Rad);
            var res = maneuver.TryExecute(atQ);
            Assert.Equal(maneuver.DeltaV, new Vector3(-1368.8299700588411, 498.1271161206587, 0.0), TestHelpers.VectorComparer);
            // At the true anomaly in TT seconds (#501, #508), the burn's acceleration centred on the impulse (#335): from
            // 16:57:17.2222560 (16:57:15.7421824 at 0.666 of the burn before 11.0.0)
            TimelineRules.AssertThrustWindow(atQ.Epoch, maneuver.DeltaV.Magnitude(), 450.0, TimeSpan.FromSeconds(10.6831353), maneuver.ThrustWindow);
            LabelAssert.Equal(maneuver.ThrustWindow, maneuver.ManeuverWindow);
            Assert.Equal(534.15676737769638, maneuver.FuelBurned, 6);
        }
    }
}