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
        public LaunchTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
            Assert.Equal(TestHelpers.Moon.InitialOrbitalParameters, launch.TargetOrbit);
        }

        [Fact]
        public void FindLaunchWindows()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2);

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
            Assert.Equal(new TimeSystem.Time("2021-06-02T02:51:10.7077003z").ToTDB(), firstWindow.Window.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T02:51:10.7077003z").ToTDB(), firstWindow.Window.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(135.19503938478633, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(137.4475046281589, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.0268499482199, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.471264849687, firstWindow.NonInertialInsertionVelocity, 6);

            var secondWindow = launchWindows.ElementAt(1);
            Assert.Equal(new TimeSystem.Time("2021-06-02T18:12:40.1968381 TDB"), secondWindow.Window.StartDate, TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T18:12:40.1968381 TDB").ToTDB(), secondWindow.Window.EndDate, TestHelpers.TimeComparer);
            Assert.Equal(44.804960615213673, secondWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(42.552495371841097, secondWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, secondWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.471264849687, secondWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindLaunchWindowsByDay()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2);

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
            Assert.Equal(new TimeSystem.Time("2021-06-02T18:11:31.0119753Z"), firstWindow.Window.StartDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T18:11:31.0119753Z"), firstWindow.Window.EndDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(44.804960615213673, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(42.552495371841097, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7384.471264849687, firstWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindSouthLaunchWindowsByDay()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2);

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
            Assert.Equal(new TimeSystem.Time("2021-06-02T15:08:24.4541548Z"), firstWindow.Window.StartDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T15:08:24.4541548Z"), firstWindow.Window.EndDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(55.142763550082414, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(53.583106156953086, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3150062037766, firstWindow.NonInertialInsertionVelocity, 6);
        }

        [Fact]
        public void FindSouthLaunchWindows()
        {
            var epoch = new TimeSystem.Time(2021, 6, 2);

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
            Assert.Equal(new TimeSystem.Time("2021-06-02T08:55:43.6080970z"), firstWindow.Window.StartDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T08:55:43.6080970z"), firstWindow.Window.EndDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(124.85723644991758, firstWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(126.41689384304691, firstWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, firstWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3150062037766, firstWindow.NonInertialInsertionVelocity, 6);

            var secondWindow = res.ElementAt(1);
            Assert.Equal(new TimeSystem.Time("2021-06-02T15:08:24.4541548Z"), secondWindow.Window.StartDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(new TimeSystem.Time("2021-06-02T15:08:24.4541548Z"), secondWindow.Window.EndDate.ToUTC(), TestHelpers.TimeComparer);
            Assert.Equal(55.142763550082414, secondWindow.InertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(53.583106156953086, secondWindow.NonInertialAzimuth * IO.Astrodynamics.Constants.Rad2Deg, 6);
            Assert.Equal(7667.02685, secondWindow.InertialInsertionVelocity, 6);
            Assert.Equal(7381.3150062037766, secondWindow.NonInertialInsertionVelocity, 6);
        }
    }
}