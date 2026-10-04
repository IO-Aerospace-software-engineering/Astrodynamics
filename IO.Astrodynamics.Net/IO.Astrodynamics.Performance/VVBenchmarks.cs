// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Performance;

/// <summary>
/// Benchmarks for the fixed-step Velocity-Verlet integrator (the default of CentralBodyPropagator) on the LEO
/// scenario of <see cref="RK78Benchmarks"/>: one orbit of conformance case 001, EGM2008 degree 10, Moon and Sun.
/// The step is also the output cadence. Compare with RK78Benchmarks.LeoOneOrbit_EGM10_SunMoon.
/// </summary>
[MarkdownExporterAttribute.GitHub]
[MemoryDiagnoser(true)]
[MediumRunJob]
public class VVBenchmarks
{
    private static readonly Time LeoEpoch = new(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);

    private CelestialBody _earthLeo = null!;
    private CentralBodyPropagator _leoPropagator = null!;

    [Params(0.5, 1.0, 2.0)]
    public double StepSeconds { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        _earthLeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, LeoEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var clk = new Clock("BenchClock", 256);
        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            _earthLeo, LeoEpoch, Frame.ICRF);
        var spc = new Spacecraft(-1001, "LEO_Bench_VV", 100.0, 10000.0, clk, orbit);
        var window = new Window(LeoEpoch, orbit.Period());
        _leoPropagator = new CentralBodyPropagator(window, spc,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            false, false, TimeSpan.FromSeconds(StepSeconds));
    }

    /// <summary>
    /// LEO 1 orbit (~93 min) — EGM2008 degree-10, Moon + Sun, Velocity-Verlet at the given step.
    /// </summary>
    [Benchmark(Description = "LEO 1 orbit EGM2008/10 Moon+Sun, Velocity-Verlet")]
    public void LeoOneOrbit_EGM10_SunMoon()
    {
        _leoPropagator.Propagate();
    }
}
