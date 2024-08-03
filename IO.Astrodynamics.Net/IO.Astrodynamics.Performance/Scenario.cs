// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Numerics;
using BenchmarkDotNet.Attributes;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Time;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Performance;

[MarkdownExporterAttribute.GitHub]
[MemoryDiagnoser]
[SkewnessColumn]
[KurtosisColumn]
[StatisticalTestColumn]
[ShortRunJob]
public class Scenario
{
    private readonly GeopotentialGravitationalField _geopotential;
    private readonly SolarRadiationPressure _srp;
    private readonly AtmosphericDrag _atm;
    private readonly CelestialBody _earth;
    private readonly CelestialBody _sun;
    private readonly CelestialBody _moon;
    private readonly VVIntegrator _integrator;
    private readonly Propagator.SpacecraftPropagator _spacecraftPropagator;
    // IO.Astrodynamics.Tests.Mission.ScenarioTests _scenario = new IO.Astrodynamics.Tests.Mission.ScenarioTests();

    public Scenario()
    {
        API.Instance.LoadKernels(new DirectoryInfo("Data"));
        _earth = new CelestialBody(399, new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 30), new EarthAtmosphericModel());
        _moon = new CelestialBody(301);
        _sun = new CelestialBody(10);
        _geopotential = new GeopotentialGravitationalField(new StreamReader("Data/SolarSystem/EGM2008_to70_TideFree"));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), _earth, DateTimeExtension.J2000, Frames.Frame.ICRF));
        _srp = new SolarRadiationPressure(spc);
        _atm = new AtmosphericDrag(spc, _earth);
        List<ForceBase> forces = new List<ForceBase>();
        forces.Add(new GravitationalAcceleration(_sun));
        forces.Add(new GravitationalAcceleration(_moon));
        forces.Add(new GravitationalAcceleration(_earth));
        forces.Add(new AtmosphericDrag(spc, _earth));
        forces.Add(new SolarRadiationPressure(spc));
        _integrator = new VVIntegrator(forces, TimeSpan.FromSeconds(1.0), new StateVector(new Vector3(6800000.0 - Random.Shared.NextDouble(), 0.0, 0.0),
            new Vector3(0.0, 8000.0 - Random.Shared.NextDouble(), 0.0), _earth,
            DateTimeExtension.J2000, Frame.ICRF));
        _spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(DateTimeExtension.J2000, DateTimeExtension.J2000 + spc.InitialOrbitalParameters.Period()), spc,
            new[] { _moon, _earth, _sun }, true, true, TimeSpan.FromSeconds(1.0));
    }

    // [Benchmark(Description = "Spacecraft spacecraftPropagator C++")]
    public void Propagate()
    {
        // _scenario.PropagateWithoutManeuver();
    }

    // [Benchmark(Description = "Compute gravitational acceleration")]
    public void Gravity()
    {
        var sv = new StateVector(new Vector3(6800000.0 - Random.Shared.NextDouble(), 0.0, 0.0), new Vector3(0.0, 8000.0 - Random.Shared.NextDouble(), 0.0), _earth,
            DateTimeExtension.J2000, Frame.ICRF);
        var res = _geopotential.ComputeGravitationalAcceleration(sv);
    }

    // [Benchmark(Description = "Solar radiation pressure")]
    public void SRP()
    {
        var sv = new StateVector(new Vector3(6800000.0 - Random.Shared.NextDouble(), 0.0, 0.0), new Vector3(0.0, 8000.0 - Random.Shared.NextDouble(), 0.0), _earth,
            DateTimeExtension.J2000, Frame.ICRF);
        var res = _srp.Apply(sv);
    }

    // [Benchmark(Description = "Atmospheric drag")]
    public void AtmosphericDrag()
    {
        var sv = new StateVector(new Vector3(6800000.0 - Random.Shared.NextDouble(), 0.0, 0.0), new Vector3(0.0, 8000.0 - Random.Shared.NextDouble(), 0.0), _earth,
            DateTimeExtension.J2000, Frame.ICRF);
        var res = _atm.Apply(sv);
    }


    // [Benchmark(Description = "VV integrator")]
    public void VVIntegration()
    {
        var sv = new StateVector(new Vector3(6800000.0 - Random.Shared.NextDouble(), 0.0, 0.0), new Vector3(0.0, 8000.0 - Random.Shared.NextDouble(), 0.0), _earth,
            DateTimeExtension.J2000, Frame.ICRF);
        // var res = _integrator.Integrate(sv);
    }

    [Benchmark(Description = "SpacecraftPropagator per orbit (GeoPotentials // Moon and sun perturbation // Atmospheric drag // Solar radiation) ")]
    public void Propagator()
    {
        var res = _spacecraftPropagator.Propagate();
    }

    // [Benchmark(Description = "IO Vector", Baseline = true)]
    public void Vector()
    {
        Vector3 vectorA = new Vector3(1.0, 2.0, 3.0);
        Vector3 vectorB = new Vector3(10.0, 20.0, 30.0);
        var res = (vectorA * vectorB);
        var dif = vectorA - vectorB;
        var add = vectorA + vectorB + vectorA - vectorB;
        var d = vectorA * 9.24;
        var tot = dif * res + add + d;
        var mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
        res = (vectorA * vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = tot.Magnitude();
    }

    // [Benchmark(Description = "Numerics Vector")]
    public void VectorNumerics()
    {
        var vectorA = new Vector<double>([0.0, 1.0, 2.0, 3.0]);
        var vectorB = new Vector<double>([0.0, 10.0, 20.0, 30.0]);
        var res = System.Numerics.Vector.Dot(vectorA, vectorB);
        var dif = vectorA - vectorB;
        var add = vectorA + vectorB + vectorA - vectorB;
        var d = vectorA * 9.24;
        var tot = dif * res + add + d;
        var mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
        res = System.Numerics.Vector.Dot(vectorA, vectorB);
        dif = vectorA - vectorB;
        add = vectorA + vectorB + vectorA - vectorB;
        d = vectorA * 9.24;
        tot = dif * res + add + d;
        mag = System.Math.Sqrt(System.Numerics.Vector.Sum(tot * tot));
    }
}