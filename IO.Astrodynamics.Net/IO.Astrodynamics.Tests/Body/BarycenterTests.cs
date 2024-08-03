using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

public class BarycenterTests
{
    public BarycenterTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        var earthbc = new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId);
        Assert.Equal("EARTH BARYCENTER", earthbc.Name);
        Assert.Equal(3, earthbc.NaifId);
        Assert.Equal(6.0456262922775433E+24, earthbc.Mass);
        Assert.Equal(4.0350323562548006E+14, earthbc.GM);
        Assert.Equal(0, earthbc.InitialOrbitalParameters.Observer.NaifId);
    }
    
    [Fact]
    public void Mass()
    {
        var earthbc = new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId);
        Assert.Equal(6.0456262922775433E+24, earthbc.Mass);
    }
}