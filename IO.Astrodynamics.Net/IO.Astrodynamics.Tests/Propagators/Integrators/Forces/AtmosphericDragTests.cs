using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class AtmosphericDragTests
{
    public AtmosphericDragTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeAcceleration()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")),
            new EarthStandardAtmosphere());
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), dragCoeff: 1.0);
        AtmosphericDrag atmosphericDrag = new AtmosphericDrag(spc,earth);

        StateVector parkingOrbit = new StateVector(new Vector3(7380000.0, 0.0, 0.0), new Vector3(0.0, 9700.0, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = atmosphericDrag.Apply(parkingOrbit);
        Assert.Equal(new Vector3(-0.0, -1.3302926867698408E-09, 2.184861621946448E-15), res, TestHelpers.VelocityVectorComparer);
    }
}