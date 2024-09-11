using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class GravityTests
{
    public GravityTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeAcceleration()
    {
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree"), 70));
        GravitationalAcceleration gravitationalAcceleration =
            new GravitationalAcceleration(earth);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = gravitationalAcceleration.Apply(parkingOrbit);
        Assert.Equal(new Vector3(-8.621408092022785, 0.0, -4.0657581468206416E-20), res);
    }
}