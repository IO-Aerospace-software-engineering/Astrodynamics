// Copyright 2025. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.Performance;

/// <summary>
/// Benchmarks for the RK78 adaptive integrator via CentralBodyPropagator.
/// The LEO and SSO scenarios use the orbits and force models of conformance cases 001 and 003; the GEO
/// scenario uses the orbit of case 002 with a lighter geopotential (degree 10 instead of 70).
/// </summary>
[MarkdownExporterAttribute.GitHub]
[MemoryDiagnoser(true)]
[SkewnessColumn]
[KurtosisColumn]
[StatisticalTestColumn]
[MediumRunJob]
public class RK78Benchmarks
{
    // Epochs from conformance test cases
    private static readonly Time LeoEpoch = new(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame);
    private static readonly Time SsoEpoch = new(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame);

    // Shared celestial bodies — created once in GlobalSetup, safe for sequential reuse.
    // GeopotentialGravitationalField is NOT thread-safe but sequential benchmark
    // iterations never overlap, so sharing is correct here.
    private CelestialBody _earthLeo = null!;      // EGM2008 deg-10, no atmosphere
    private CelestialBody _earthSso = null!;      // EGM2008 deg-10, no atmosphere
    private CelestialBody _earthGeo = null!;      // EGM2008 deg-70, no atmosphere
    private CelestialBody _earthLeoAtm = null!;   // EGM2008 deg-10, with atmosphere
    private Time _geoTleEpoch;

    // Propagators — recreated before each measured iteration (IterationSetup is
    // excluded from timing), so each Propagate() call starts from a clean state.
    private CentralBodyPropagator _leoPropagator = null!;
    private CentralBodyPropagator _ssoPropagator = null!;
    private CentralBodyPropagator _geoPropagator = null!;
    private CentralBodyPropagator _leoDragSrpPropagator = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));

        _earthLeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, LeoEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        _earthSso = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, SsoEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        var geoTle = new TLE("INTELSAT 901",
            "1 26824U 01024A   26040.43262683 -.00000207  00000-0  00000+0 0  9994",
            "2 26824   0.9230  86.6125 0002726 324.4840  12.2632  0.98820941 21659");
        _geoTleEpoch = geoTle.Epoch;

        _earthGeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, _geoTleEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        _earthLeoAtm = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, LeoEpoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new EarthStandardAtmosphere());
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _leoPropagator = BuildLeoPropagator();
        _ssoPropagator = BuildSsoPropagator();
        _geoPropagator = BuildGeoPropagator();
        _leoDragSrpPropagator = BuildLeoDragSrpPropagator();
    }

    /// <summary>
    /// LEO 1 orbit (~93 min) — EGM2008 degree-10, Moon + Sun (conformance case 001).
    /// </summary>
    [Benchmark(Description = "LEO 1 orbit EGM2008/10 Moon+Sun")]
    public void LeoOneOrbit_EGM10_SunMoon()
    {
        _leoPropagator.Propagate();
    }

    /// <summary>
    /// SSO 1 orbit (~100 min) — EGM2008 degree-10, Moon + Sun (conformance case 003).
    /// </summary>
    [Benchmark(Description = "SSO 1 orbit EGM2008/10 Moon+Sun")]
    public void SsoOneOrbit_EGM10_SunMoon()
    {
        _ssoPropagator.Propagate();
    }

    /// <summary>
    /// GEO 1 orbit (~24 h) — EGM2008 degree-10, all planets. Orbit of conformance case 002, whose geopotential
    /// is degree 70.
    /// </summary>
    [Benchmark(Description = "GEO 1 orbit EGM2008/10 all planets")]
    public void GeoOneOrbit_EGM10_AllPlanets()
    {
        _geoPropagator.Propagate();
    }

    /// <summary>
    /// LEO 1 orbit (~93 min) — EGM2008 degree-10, Moon + Sun, atmospheric drag + SRP.
    /// Full force model; quantifies the extra cost of drag and SRP evaluations.
    /// </summary>
    [Benchmark(Description = "LEO 1 orbit EGM2008/10 Moon+Sun drag+SRP")]
    public void LeoOneOrbit_EGM10_SunMoon_DragSrp()
    {
        _leoDragSrpPropagator.Propagate();
    }

    // -------------------------------------------------------------------------
    // Builder helpers
    // -------------------------------------------------------------------------

    private CentralBodyPropagator BuildLeoPropagator()
    {
        var clk = new Clock("BenchClock", 256);
        var orbit = new StateVector(
            new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
            new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
            _earthLeo, LeoEpoch, Frame.ICRF);
        var spc = new Spacecraft(-1001, "LEO_Bench", 100.0, 10000.0, clk, orbit);
        var window = new Window(LeoEpoch, orbit.Period());
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);
        return new CentralBodyPropagator(window, spc, integrator,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            false, false, TimeSpan.FromSeconds(10.0));
    }

    private CentralBodyPropagator BuildSsoPropagator()
    {
        var clk = new Clock("BenchClock", 256);
        var keplerianOrbit = new KeplerianElements(
            7078137.0, 0.001,
            98.186 * System.Math.PI / 180.0,
            75.0 * System.Math.PI / 180.0,
            90.0 * System.Math.PI / 180.0,
            0.0,
            _earthSso, SsoEpoch, Frame.ICRF);
        var orbit = keplerianOrbit.ToStateVector();
        var spc = new Spacecraft(-1002, "SSO_Bench", 100.0, 1000.0, clk, orbit);
        var window = new Window(SsoEpoch, keplerianOrbit.Period());
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);
        return new CentralBodyPropagator(window, spc, integrator,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            false, false, TimeSpan.FromSeconds(10.0));
    }

    private CentralBodyPropagator BuildGeoPropagator()
    {
        var clk = new Clock("BenchClock", 256);
        var tle = new TLE("INTELSAT 901",
            "1 26824U 01024A   26040.43262683 -.00000207  00000-0  00000+0 0  9994",
            "2 26824   0.9230  86.6125 0002726 324.4840  12.2632  0.98820941 21659");
        var osculatingIcrf = tle.ToStateVector().ToFrame(Frame.ICRF).ToStateVector();
        var orbit = new StateVector(osculatingIcrf.Position, osculatingIcrf.Velocity,
            _earthGeo, osculatingIcrf.Epoch, Frame.ICRF);
        var spc = new Spacecraft(-1003, "GEO_Bench", 3000.0, 5000.0, clk, orbit,
            sectionalArea: 50.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
        var window = new Window(_geoTleEpoch, orbit.Period());
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);
        return new CentralBodyPropagator(window, spc, integrator,
            new CelestialItem[]
            {
                PlanetsAndMoons.MOON_BODY,
                Stars.SUN_BODY,
                Barycenters.MERCURY_BARYCENTER,
                Barycenters.VENUS_BARYCENTER,
                Barycenters.MARS_BARYCENTER,
                Barycenters.JUPITER_BARYCENTER,
                Barycenters.SATURN_BARYCENTER,
                Barycenters.URANUS_BARYCENTER,
                Barycenters.NEPTUNE_BARYCENTER,
                Barycenters.PLUTO_BARYCENTER
            },
            false, false, TimeSpan.FromSeconds(10.0));
    }

    private CentralBodyPropagator BuildLeoDragSrpPropagator()
    {
        var clk = new Clock("BenchClock", 256);
        var r0 = 6800000.0;
        var v0 = System.Math.Sqrt(_earthLeoAtm.GM / r0);
        var orbit = new StateVector(
            new Vector3(r0, 0.0, 0.0), new Vector3(0.0, v0, 0.0),
            _earthLeoAtm, LeoEpoch, Frame.ICRF);
        var spc = new Spacecraft(-1004, "LEO_Full_Bench", 100.0, 10000.0, clk, orbit,
            sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
        var window = new Window(LeoEpoch, orbit.Period());
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);
        return new CentralBodyPropagator(window, spc, integrator,
            new CelestialItem[] { _earthLeoAtm, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            includeAtmosphericDrag: true, includeSolarRadiationPressure: true,
            TimeSpan.FromSeconds(10.0));
    }
}
