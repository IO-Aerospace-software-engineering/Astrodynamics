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
    }
}