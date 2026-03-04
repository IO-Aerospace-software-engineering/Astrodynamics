using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class DragPropagationTests
{
    public DragPropagationTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static double CircularVelocity(double radius, double gm) => System.Math.Sqrt(gm / radius);

    private static double FinalSma(PropagationSolution solution, CelestialItem body)
    {
        var last = solution.StateVectors.Last()
            .RelativeTo(body, Aberration.None).ToStateVector();
        return last.SemiMajorAxis();
    }

    private static CelestialBody CreateEarthWithAtm()
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new EarthStandardAtmosphere());
    }

    private static CelestialBody CreateEarthWithNrlmsise(SpaceWeather weather)
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(weather));
    }

    #region Test 1: LEO Orbit Decay with EarthStandardAtmosphere

    [Fact]
    public void LeoOrbitDecay_EarthStandardAtmosphere()
    {
        var earth = CreateEarthWithAtm();
        double r = 6_678_000.0; // ~300 km altitude
        double v = CircularVelocity(r, earth.GM);

        Clock clk = new Clock("clk", 256);
        var orbit = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "Sat", 100.0, 10000.0, clk, orbit,
            sectionalArea: 10.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));
        var propagator = new Propagator.CentralBodyPropagator(window, spc,
            new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            true, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        double initialSma = orbit.SemiMajorAxis();
        double finalSma = FinalSma(solution, earth);

        Assert.True(finalSma < initialSma,
            $"Expected orbit decay: final SMA {finalSma:F1} should be < initial SMA {initialSma:F1}");
        Assert.True(initialSma - finalSma > 1.0,
            $"Expected measurable decay (>1 m), got {initialSma - finalSma:F3} m");
    }

    #endregion

    #region Test 2: LEO Orbit Decay with Nrlmsise00Model

    [Fact]
    public void LeoOrbitDecay_Nrlmsise00Model()
    {
        var earth = CreateEarthWithNrlmsise(SpaceWeather.Nominal);
        double r = 6_678_000.0; // ~300 km altitude
        double v = CircularVelocity(r, earth.GM);

        Clock clk = new Clock("clk", 256);
        var orbit = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "Sat", 100.0, 10000.0, clk, orbit,
            sectionalArea: 10.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));
        var propagator = new Propagator.CentralBodyPropagator(window, spc,
            new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            true, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        double initialSma = orbit.SemiMajorAxis();
        double finalSma = FinalSma(solution, earth);

        Assert.True(finalSma < initialSma,
            $"Expected orbit decay with NRLMSISE-00: final SMA {finalSma:F1} < initial SMA {initialSma:F1}");
    }

    #endregion

    #region Test 3: Drag Effect Scales with Area-to-Mass Ratio

    [Fact]
    public void DragScalesWithAreaToMassRatio()
    {
        var earth1 = CreateEarthWithAtm();
        var earth2 = CreateEarthWithAtm();
        double r = 6_678_000.0;
        double v = CircularVelocity(r, earth1.GM);

        // Small area spacecraft
        Clock clk1 = new Clock("clk1", 256);
        var orbit1 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth1, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "SmallArea", 100.0, 10000.0, clk1, orbit1,
            sectionalArea: 1.0);

        // Large area spacecraft
        Clock clk2 = new Clock("clk2", 256);
        var orbit2 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth2, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc2 = new Spacecraft(-1002, "LargeArea", 100.0, 10000.0, clk2, orbit2,
            sectionalArea: 20.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));

        var prop1 = new Propagator.CentralBodyPropagator(window, spc1,
            new CelestialItem[] { earth1, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));
        var prop2 = new Propagator.CentralBodyPropagator(window, spc2,
            new CelestialItem[] { earth2, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));

        var solution1 = prop1.Propagate();
        var solution2 = prop2.Propagate();

        double initialSma = orbit1.SemiMajorAxis();
        double finalSma1 = FinalSma(solution1, earth1);
        double finalSma2 = FinalSma(solution2, earth2);

        double decay1 = initialSma - finalSma1;
        double decay2 = initialSma - finalSma2;

        Assert.True(decay2 > decay1,
            $"Larger area should decay more: decay(20m²)={decay2:F1} m vs decay(1m²)={decay1:F1} m");
    }

    #endregion

    #region Test 4: Drag Effect Scales with Drag Coefficient

    [Fact]
    public void DragScalesWithDragCoefficient()
    {
        var earth1 = CreateEarthWithNrlmsise(SpaceWeather.Nominal);
        var earth2 = CreateEarthWithNrlmsise(SpaceWeather.Nominal);
        double r = 6_678_000.0;
        double v = CircularVelocity(r, earth1.GM);

        // Low Cd
        Clock clk1 = new Clock("clk1", 256);
        var orbit1 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth1, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "LowCd", 100.0, 10000.0, clk1, orbit1,
            sectionalArea: 10.0, dragCoeff: 1.0);

        // High Cd (default 2.2)
        Clock clk2 = new Clock("clk2", 256);
        var orbit2 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth2, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc2 = new Spacecraft(-1002, "HighCd", 100.0, 10000.0, clk2, orbit2,
            sectionalArea: 10.0, dragCoeff: 2.2);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));

        var prop1 = new Propagator.CentralBodyPropagator(window, spc1,
            new CelestialItem[] { earth1, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));
        var prop2 = new Propagator.CentralBodyPropagator(window, spc2,
            new CelestialItem[] { earth2, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));

        var solution1 = prop1.Propagate();
        var solution2 = prop2.Propagate();

        double initialSma = orbit1.SemiMajorAxis();
        double finalSma1 = FinalSma(solution1, earth1);
        double finalSma2 = FinalSma(solution2, earth2);

        double decay1 = initialSma - finalSma1;
        double decay2 = initialSma - finalSma2;

        Assert.True(decay2 > decay1,
            $"Higher Cd should decay more: decay(Cd=2.2)={decay2:F1} m vs decay(Cd=1.0)={decay1:F1} m");
    }

    #endregion

    #region Test 5: High Altitude Has Negligible Drag

    [Fact]
    public void HighAltitudeNegligibleDrag()
    {
        // Compare drag-on vs drag-off at GEO to confirm drag contribution is negligible.
        // Third-body perturbations change SMA, so we compare the difference between cases.
        var earthDragOn = CreateEarthWithAtm();
        var earthDragOff = CreateEarthWithAtm();
        double r = 42_164_000.0; // GEO radius
        double v = CircularVelocity(r, earthDragOn.GM);

        Clock clk1 = new Clock("clk1", 256);
        var orbit1 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earthDragOn, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "GeoDragOn", 100.0, 10000.0, clk1, orbit1,
            sectionalArea: 10.0);

        Clock clk2 = new Clock("clk2", 256);
        var orbit2 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earthDragOff, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc2 = new Spacecraft(-1002, "GeoDragOff", 100.0, 10000.0, clk2, orbit2,
            sectionalArea: 10.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));

        var prop1 = new Propagator.CentralBodyPropagator(window, spc1,
            new CelestialItem[] { earthDragOn, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(60.0));
        var prop2 = new Propagator.CentralBodyPropagator(window, spc2,
            new CelestialItem[] { earthDragOff, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, false, false, TimeSpan.FromSeconds(60.0));

        var solution1 = prop1.Propagate();
        var solution2 = prop2.Propagate();

        double finalSmaDragOn = FinalSma(solution1, earthDragOn);
        double finalSmaDragOff = FinalSma(solution2, earthDragOff);
        double dragContribution = System.Math.Abs(finalSmaDragOn - finalSmaDragOff);

        Assert.True(dragContribution < 1.0,
            $"GEO drag contribution should be negligible: |SMA_on - SMA_off| = {dragContribution:F6} m, expected < 1 m");
    }

    #endregion

    #region Test 6: Low Altitude Significant Drag

    [Fact]
    public void LowAltitudeSignificantDrag()
    {
        // Use NRLMSISE-00 for physically accurate density at low LEO
        var earth = CreateEarthWithNrlmsise(SpaceWeather.Nominal);
        double r = 6_628_000.0; // ~250 km altitude
        double v = CircularVelocity(r, earth.GM);

        Clock clk = new Clock("clk", 256);
        var orbit = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "LowSat", 100.0, 10000.0, clk, orbit,
            sectionalArea: 20.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(2));
        var propagator = new Propagator.CentralBodyPropagator(window, spc,
            new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY },
            true, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        double initialSma = orbit.SemiMajorAxis();
        double finalSma = FinalSma(solution, earth);

        Assert.True(finalSma < initialSma,
            $"Low altitude orbit should decay: final SMA {finalSma:F1} < initial SMA {initialSma:F1}");

        var lastState = solution.StateVectors.Last()
            .RelativeTo(earth, Aberration.None).ToStateVector();
        double initialEnergy = orbit.SpecificOrbitalEnergy();
        double finalEnergy = lastState.SpecificOrbitalEnergy();

        Assert.True(finalEnergy < initialEnergy,
            $"Orbital energy should decrease: final {finalEnergy:E3} < initial {initialEnergy:E3}");
    }

    #endregion

    #region Test 7: NRLMSISE-00 Solar Activity Effect on Orbit Decay

    [Fact]
    public void Nrlmsise00SolarActivityEffect()
    {
        var earthSolarMin = CreateEarthWithNrlmsise(SpaceWeather.SolarMinimum);
        var earthSolarMax = CreateEarthWithNrlmsise(SpaceWeather.SolarMaximum);

        double r = 6_728_000.0; // ~350 km altitude
        double v = CircularVelocity(r, earthSolarMin.GM);

        // Solar minimum case
        Clock clk1 = new Clock("clk1", 256);
        var orbit1 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earthSolarMin, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "SolMin", 100.0, 10000.0, clk1, orbit1,
            sectionalArea: 10.0);

        // Solar maximum case
        Clock clk2 = new Clock("clk2", 256);
        var orbit2 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earthSolarMax, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc2 = new Spacecraft(-1002, "SolMax", 100.0, 10000.0, clk2, orbit2,
            sectionalArea: 10.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(12));

        var prop1 = new Propagator.CentralBodyPropagator(window, spc1,
            new CelestialItem[] { earthSolarMin, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));
        var prop2 = new Propagator.CentralBodyPropagator(window, spc2,
            new CelestialItem[] { earthSolarMax, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));

        var solution1 = prop1.Propagate();
        var solution2 = prop2.Propagate();

        double initialSma = orbit1.SemiMajorAxis();
        double finalSmaSolMin = FinalSma(solution1, earthSolarMin);
        double finalSmaSolMax = FinalSma(solution2, earthSolarMax);

        double decaySolMin = initialSma - finalSmaSolMin;
        double decaySolMax = initialSma - finalSmaSolMax;

        Assert.True(decaySolMax > decaySolMin,
            $"Solar maximum should cause more drag: decay(max)={decaySolMax:F1} m vs decay(min)={decaySolMin:F1} m");
    }

    #endregion

    #region Test 8: Drag Disabled Preserves Orbit Better Than Drag Enabled

    [Fact]
    public void DragDisabledPreservesOrbitBetter()
    {
        var earth1 = CreateEarthWithAtm();
        var earth2 = CreateEarthWithAtm();
        double r = 6_678_000.0;
        double v = CircularVelocity(r, earth1.GM);

        // Drag enabled
        Clock clk1 = new Clock("clk1", 256);
        var orbit1 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth1, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc1 = new Spacecraft(-1001, "DragOn", 100.0, 10000.0, clk1, orbit1,
            sectionalArea: 10.0);

        // Drag disabled
        Clock clk2 = new Clock("clk2", 256);
        var orbit2 = new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0),
            earth2, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc2 = new Spacecraft(-1002, "DragOff", 100.0, 10000.0, clk2, orbit2,
            sectionalArea: 10.0);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(1));

        var prop1 = new Propagator.CentralBodyPropagator(window, spc1,
            new CelestialItem[] { earth1, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, TimeSpan.FromSeconds(10.0));
        var prop2 = new Propagator.CentralBodyPropagator(window, spc2,
            new CelestialItem[] { earth2, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, false, false, TimeSpan.FromSeconds(10.0));

        var solution1 = prop1.Propagate();
        var solution2 = prop2.Propagate();

        double finalSmaDragOn = FinalSma(solution1, earth1);
        double finalSmaDragOff = FinalSma(solution2, earth2);

        Assert.True(finalSmaDragOn < finalSmaDragOff,
            $"Drag-enabled should have lower SMA: drag-on={finalSmaDragOn:F1} vs drag-off={finalSmaDragOff:F1}");
    }

    #endregion
}
