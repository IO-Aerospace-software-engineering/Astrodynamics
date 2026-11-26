using System;
using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class LaunchTests
    {
        // The non-inertial azimuths and insertion velocities depend on the speed of the launch site in ICRF. Their
        // expected values take that speed from SPICE (sxform_c, tools/spice_reference/spice_ref.c).
        public LaunchTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void CreateWithOrbitalParameter()
        {
            LaunchSite site = new LaunchSite(33, "l1", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0), default, new AzimuthRange(1.0, 2.0));
            Site recoverySite = new Site(34, "l2", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0));
            Launch launch = new Launch(site, recoverySite, TestHelpers.Moon.InitialOrbitalParameters, IO.Astrodynamics.Constants.CivilTwilight, true);
            Assert.NotNull(launch);
            Assert.Equal(site, launch.LaunchSite);
            Assert.Equal(recoverySite, launch.RecoverySite);
            Assert.True(launch.LaunchByDay);
            LabelAssert.Equal(TestHelpers.Moon.InitialOrbitalParameters, launch.TargetOrbit);
        }

        [Fact]
        public void FindLaunchWindows()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2, TimeScale.TDB);

            var earth = TestHelpers.Earth;
            LaunchSite site = new LaunchSite(33, "l1", earth, new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0), default,
                new AzimuthRange(0.0, 6.0));
            Site recoverySite = new Site(34, "l2", earth, new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));
            //ISS at 2021-06-02 TDB
            Launch launch = new Launch(site, recoverySite,
                new StateVector(new Vector3(-1.144243683349977E+06, 4.905086030671815E+06, 4.553416875193589E+06),
                    new Vector3(-5.588288926819989E+03, -4.213222250603758E+03, 3.126518392859475E+03), earth, epoch, Frames.Frame.ICRF), IO.Astrodynamics.Constants.CivilTwilight,
                null);
            var res = launch.FindLaunchWindows(new Window(epoch, TimeSpan.FromDays(1.0)));
            var launchWindows = res as LaunchWindow[] ?? res.ToArray();
            Assert.Equal(2, launchWindows.Count());
            var firstWindow = launchWindows.ElementAt(0);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T02:51:10.7077003z").ToTdb(), firstWindow.Window.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T02:51:10.7077003z").ToTdb(), firstWindow.Window.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(135.19503938478633, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(137.44746151989264, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.0268499482199, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.4763653063428, firstWindow.NonInertialInsertionVelocity, 6);

            var secondWindow = launchWindows.ElementAt(1);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T18:12:40.1968381 TDB"), secondWindow.Window.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T18:12:40.1968381 TDB").ToTdb(), secondWindow.Window.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(44.804960615213673, secondWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(42.552538480107366, secondWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, secondWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.4763653063428, secondWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindLaunchWindowsByDay()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2, TimeScale.TDB);

            var earth = TestHelpers.Earth;
            LaunchSite site = new LaunchSite(33, "l1", earth, new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0), default,
                new AzimuthRange(0.0, 6.0));
            Site recoverySite = new Site(34, "l2", earth, new Planetodetic(-81.0 * IO.Astrodynamics.Constants.Deg2Rad, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));
            //ISS at 2021-06-02 TDB
            Launch launch = new Launch(site, recoverySite,
                new StateVector(new Vector3(-1.144243683349977E+06, 4.905086030671815E+06, 4.553416875193589E+06),
                    new Vector3(-5.588288926819989E+03, -4.213222250603758E+03, 3.126518392859475E+03), earth, epoch, Frames.Frame.ICRF), IO.Astrodynamics.Constants.CivilTwilight,
                true);
            var res = launch.FindLaunchWindows(new Window(epoch, TimeSpan.FromDays(1.0)));
            Assert.Single(res);
            var firstWindow = res.ElementAt(0);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T18:11:31.0119753Z"), firstWindow.Window.StartDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T18:11:31.0119753Z"), firstWindow.Window.EndDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(44.804960615213673, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(42.552538480107366, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.4763653063428, firstWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindSouthLaunchWindowsByDay()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2, TimeScale.TDB);

            var earth = TestHelpers.Earth;
            LaunchSite site = new LaunchSite(33, "l1", earth, new Planetodetic(-104.0 * IO.Astrodynamics.Constants.Deg2Rad, -41.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
                default,
                new AzimuthRange(0.0, 6.0));
            Site recoverySite = new Site(34, "l2", earth, new Planetodetic(-104.0 * IO.Astrodynamics.Constants.Deg2Rad, -41.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));
            //ISS at 2021-06-02 TDB
            Launch launch = new Launch(site, recoverySite,
                new StateVector(new Vector3(-1.144243683349977E+06, 4.905086030671815E+06, 4.553416875193589E+06),
                    new Vector3(-5.588288926819989E+03, -4.213222250603758E+03, 3.126518392859475E+03), earth, epoch, Frames.Frame.ICRF), IO.Astrodynamics.Constants.CivilTwilight,
                true);
            var res = launch.FindLaunchWindows(new Window(epoch, TimeSpan.FromDays(1.0)));
            Assert.Single(res);
            var firstWindow = res.ElementAt(0);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T15:08:24.4541548Z"), firstWindow.Window.StartDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T15:08:24.4541548Z"), firstWindow.Window.EndDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(55.142763550082414, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(53.58307076976331, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3088265255128, firstWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindSouthLaunchWindows()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2, TimeScale.TDB);

            var earth = TestHelpers.Earth;
            LaunchSite site = new LaunchSite(33, "l1", earth, new Planetodetic(-104.0 * IO.Astrodynamics.Constants.Deg2Rad, -41.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0),
                new AzimuthRange(0.0, 6.0));
            Site recoverySite = new Site(34, "l2", earth, new Planetodetic(-104.0 * IO.Astrodynamics.Constants.Deg2Rad, -41.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));
            //ISS at 2021-06-02 TDB
            Launch launch = new Launch(site, recoverySite,
                new StateVector(new Vector3(-1.144243683349977E+06, 4.905086030671815E+06, 4.553416875193589E+06),
                    new Vector3(-5.588288926819989E+03, -4.213222250603758E+03, 3.126518392859475E+03), earth, epoch, Frames.Frame.ICRF), IO.Astrodynamics.Constants.CivilTwilight,
                null);
            var res = launch.FindLaunchWindows(new Window(epoch, TimeSpan.FromDays(1.0)));
            Assert.Equal(2, res.Count());
            var firstWindow = res.ElementAt(0);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T08:55:43.6080970z"), firstWindow.Window.StartDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T08:55:43.6080970z"), firstWindow.Window.EndDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(124.85723644991758, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(126.41692923023668, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3088265255128, firstWindow.NonInertialInsertionVelocity, 6);

            var secondWindow = res.ElementAt(1);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T15:08:24.4541548Z"), secondWindow.Window.StartDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(TimeSystem.Time.Parse("2021-06-02T15:08:24.4541548Z"), secondWindow.Window.EndDate.ToUtc(), TestHelpers.TimeComparer);
            Assert.Equal(55.142763550082414, secondWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(53.58307076976331, secondWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, secondWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3088265255128, secondWindow.NonInertialInsertionVelocity, 6);
        }

        // #686: the insertion velocity of the issue's near-circular target is the circular velocity √(GM / r) of 7178 km
        // (PerigeeHeightManeuverTests.Issue686Elements); it was infinite
        [Fact]
        public void GetInertialInsertionVelocity_NearCircularTarget_IsTheCircularVelocity()
        {
            LaunchSite site = new LaunchSite(33, "l1", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0), default, new AzimuthRange(1.0, 2.0));
            Site recoverySite = new Site(34, "l2", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0));
            var launch = new Launch(site, recoverySite, PerigeeHeightManeuverTests.Issue686Elements(), IO.Astrodynamics.Constants.CivilTwilight, true);
            double error = System.Math.Abs(launch.GetInertialInsertionVelocity() / System.Math.Sqrt(TestHelpers.EarthAtJ2000.GM / 7178e3) - 1.0);
            System.Console.WriteLine($"MEASURE #686 issue elements: insertion velocity against sqrt(GM/r) {error:E3}");
            Assert.True(error <= PerigeeHeightManeuverTests.Issue686Tolerance, $"{error:E3}");
        }
}
}