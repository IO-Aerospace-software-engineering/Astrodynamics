using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class FindOrbitalParametersFromObservations
{
    public FindOrbitalParametersFromObservations()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);
        var e1 = new TimeSystem.Time(2023, 12, 31);
        var e2 = new TimeSystem.Time(2024, 1, 2);
        var e3 = new TimeSystem.Time(2024, 1, 4);
        var moon = new CelestialBody(301);
        var obs1 = moon.GetEphemeris(e1, site, Frames.Frame.ICRF, Aberration.LT);
        var obs2 = moon.GetEphemeris(e2, site, Frames.Frame.ICRF, Aberration.LT);
        var obs3 = moon.GetEphemeris(e3, site, Frames.Frame.ICRF, Aberration.LT);
        var orbitalParams =
            Astrodynamics.OrbitalParameters.OrbitalParameters.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site, Barycenters.EARTH_BARYCENTER);
        Assert.Equal(obs2.ToKeplerianElements(), orbitalParams.ToKeplerianElements());
    }
}