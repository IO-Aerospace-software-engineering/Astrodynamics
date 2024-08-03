using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Time;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class AtmosphericDragTests
{
    public AtmosphericDragTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeAcceleration()
    {
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")),
            new EarthAtmosphericModel());
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth,
                DateTimeExtension.J2000, Frames.Frame.ICRF), dragCoeff: 1.0);
        AtmosphericDrag atmosphericDrag = new AtmosphericDrag(spc,earth);

        StateVector parkingOrbit = new StateVector(new Vector3(7380000.0, 0.0, 0.0), new Vector3(0.0, 9700.0, 0.0), earth, DateTimeExtension.J2000,
            Frames.Frame.ICRF);
        var res = atmosphericDrag.Apply(parkingOrbit);
        Assert.Equal(new Vector3(0.0, -1.4911628830275622E-09, 0.0), res);
    }
}