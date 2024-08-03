using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.Surface
{
    public class LaunchSiteTests
    {
        public LaunchSiteTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }
        [Fact]
        public void Create()
        {
            LaunchSite site = new LaunchSite(33,"l1", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0), new AzimuthRange(1.0, 2.0));
            Assert.Equal("l1", site.Name);
            Assert.Equal(TestHelpers.EarthAtJ2000, site.CelestialBody);
            Assert.Equal(new Planetodetic(1.0, 2.0, 3.0), site.Planetodetic);
            Assert.Single(site.AzimuthRanges);
            Assert.Equal(new AzimuthRange(1.0, 2.0), site.AzimuthRanges.First());
        }


        [Fact]
        public void IsAzimuthAllowed()
        {
            LaunchSite site = new LaunchSite(33,"l1", TestHelpers.EarthAtJ2000, new Planetodetic(1.0, 2.0, 3.0),new AzimuthRange(1.0, 2.0), new AzimuthRange(4.0, 5.0));
            Assert.True(site.IsAzimuthAllowed(1.0));
            Assert.True(site.IsAzimuthAllowed(5.0));
            Assert.False(site.IsAzimuthAllowed(3.0));
            Assert.False(site.IsAzimuthAllowed(-1.0));
        }
    }
}
