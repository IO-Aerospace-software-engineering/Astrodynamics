// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Mean elements stay mean through <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.AtEpoch"/> and
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToKeplerianElements(Time)"/>, so the guard of
/// <c>ToStateVector()</c> holds, and a numerical propagation refuses a mean initial orbit (#463). Mean elements have no
/// state of their own: it needs the theory they belong to (SGP4 for a TLE), as CLAUDE.md, the TLE tutorial and the
/// enumeration page state. The TLE's osculating set, taken through SGP4, stays osculating, against the two-body motion of
/// CSPICE <c>prop2b</c> over the TT seconds of the motion about the Earth.
/// </summary>
public class MeanElementsTypeTests
{
    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    // Case A of the issue: the ISS TLE of TleTests
    private static Tle IssTle() => new("ISS",
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    private static readonly TimeSystem.Time EpochB = new(new DateTime(2024, 1, 15, 12, 0, 0), TimeScale.UTC);

    public MeanElementsTypeTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, EpochB);
    }

    // Case B: FromOMM, 15.5 rev/day, e 0.0007, i 51.64°, Ω 120°, ω 80°, M 30°, in a frame
    private KeplerianElements CaseB(Frames.Frame frame) =>
        KeplerianElements.FromOMM(15.5, 0.0007, 51.64, 120.0, 80.0, 30.0, _earth, EpochB, frame);

    private IO.Astrodynamics.OrbitalParameters.OrbitalParameters[] MeanSets()
    {
        var fromConstructor = new KeplerianElements(6800000.0, 0.001, 0.9, 1.0, 2.0, 0.5, _earth, EpochB,
            Frames.Frame.ECLIPJ2000, elementsType: OrbitalElementsType.Mean);
        return
        [
            IssTle().ToMeanKeplerianElements(), CaseB(Frames.Frame.ICRF), fromConstructor, CaseB(Frames.Frame.ICRF).ToEquinoctial(),
            new EquinoctialElements(6800000.0, 0.001, 0.0005, 0.2, 0.1, 1.0, _earth, EpochB, Frames.Frame.ICRF,
                elementsType: OrbitalElementsType.Mean),
        ];
    }

    [Fact]
    public void MeanSets_StayMeanThroughAtEpochAndToKeplerianElementsAtAnEpoch()
    {
        foreach (var mean in MeanSets())
        {
            foreach (var t in new[] { mean.Epoch, mean.Epoch.AddSeconds(3600.0) })
            {
                // Act
                var atEpoch = mean.AtEpoch(t);
                var keplerian = mean.ToKeplerianElements(t);

                // Assert: Mean (before: Osculating, and so the guard let them through)
                Assert.Equal(OrbitalElementsType.Mean, atEpoch.ElementsType);
                Assert.Equal(OrbitalElementsType.Mean, keplerian.ElementsType);
                Assert.Throws<InvalidOperationException>(() => atEpoch.ToStateVector());
                Assert.Throws<InvalidOperationException>(() => keplerian.ToStateVector());
                Assert.Throws<InvalidOperationException>(() => atEpoch.ToFrame(Frames.Frame.GCRF));
            }
        }
    }

    [Fact]
    public void OsculatingSets_StayOsculating()
    {
        var osculating = new KeplerianElements(6800000.0, 0.001, 0.9, 1.0, 2.0, 0.5, _earth, EpochB, Frames.Frame.ICRF);
        var later = EpochB.AddSeconds(3600.0);
        foreach (var orbit in new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[]
                     { osculating, osculating.ToEquinoctial(), osculating.ToStateVector() })
        {
            Assert.Equal(OrbitalElementsType.Osculating, orbit.AtEpoch(later).ElementsType);
            Assert.Equal(OrbitalElementsType.Osculating, orbit.ToKeplerianElements(later).ElementsType);
        }
    }

    [Fact]
    public void TheOsculatingSetOfATle_StaysOsculating()
    {
        // Arrange: a TLE is Mean, its accessors the osculating set of SGP4 at its epoch
        var tle = IssTle();
        var t = tle.Epoch.AddSeconds(3600.0);

        // Act
        var elements = tle.ToKeplerianElements(t);
        var state = elements.ToStateVector();

        // Assert: Osculating, the two-body motion of the SGP4 state over the TT seconds of the hour (prop2b), and AtEpoch
        // still SGP4
        Assert.Equal(OrbitalElementsType.Mean, tle.ElementsType);
        Assert.Equal(OrbitalElementsType.Osculating, elements.ElementsType);
        var start = tle.ToOsculating();
        double dt = PropagationTimeArgument.Tt.SecondsBetween(start.Epoch, t);
        var reference = SpiceApi.Instance.Propagate2Bodies(start, start.Observer.GM, TimeSpan.FromTicks((long)System.Math.Round(dt * 1e7)));
        double error = (state.Position - reference.Position).Magnitude() / reference.Position.Magnitude();
        _output.WriteLine($"MEASURE #463 tle.ToKeplerianElements(t0 + 1 h) against prop2b, |Δr| / |r|: {error:E3}");
        // The rounding of the element conversions over an hour, against prop2b from evsgp4_c's state (both of the native
        // library, which MSVC builds on Windows): 8.9e-16 on Linux x64 and macOS arm64, 3.8e-15 on Windows x64 (CI run
        // 44225490669, about 2.6e-8 m at the ISS's |r|); twice the worst
        Assert.InRange(error, 0.0, 7.7e-15);
        var sgp4 = tle.ToOsculating(t);
        var atEpoch = tle.AtEpoch(t).ToStateVector();
        Assert.Equal(sgp4.Position, atEpoch.Position);
        Assert.Equal(sgp4.Velocity, atEpoch.Velocity);
    }

    [Fact]
    public void AMeanSetMovedToItsOwnEpoch_WritesTheSameTle()
    {
        // Arrange: case A's mean elements, moved to their own epoch (before: Osculating, refused by Tle.Create)
        var tle = IssTle();
        var mean = tle.ToMeanKeplerianElements();

        // Act
        var direct = Tle.Create(mean, "ISS", 25544, "98067A", 2570, bStar: 0.10270e-3, meanMotionDot: 0.00016717);
        var moved = Tle.Create(mean.AtEpoch(tle.Epoch), "ISS", 25544, "98067A", 2570, bStar: 0.10270e-3, meanMotionDot: 0.00016717);
        var throughKeplerian = Tle.Create(mean.ToKeplerianElements(tle.Epoch), "ISS", 25544, "98067A", 2570, bStar: 0.10270e-3,
            meanMotionDot: 0.00016717);

        // Assert: the same two lines
        Assert.Equal(direct.Line1, moved.Line1);
        Assert.Equal(direct.Line2, moved.Line2);
        Assert.Equal(direct.Line2, throughKeplerian.Line2);
    }

    private Spacecraft MeanSpacecraft(int naifId, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit) =>
        new(naifId, $"MEAN{-naifId}", 100.0, orbit, 1000.0, new Clock($"mean{-naifId}", 65536));

    [Fact]
    public async Task APropagationFromAMeanInitialOrbit_IsRefused()
    {
        // Arrange: mean sets in inertial frames (a mean TEME set is refused by the frame check, as before), with a window
        // at their epoch and one after it
        var sets = new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[]
        {
            CaseB(Frames.Frame.ICRF), CaseB(Frames.Frame.ICRF).ToEquinoctial(),
            new KeplerianElements(6800000.0, 0.001, 0.9, 1.0, 2.0, 0.5, _earth, EpochB, Frames.Frame.ECLIPJ2000,
                elementsType: OrbitalElementsType.Mean),
        };
        int naifId = -4630;
        foreach (var set in sets)
        {
            foreach (var start in new[] { EpochB, EpochB.AddSeconds(600.0) })
            {
                var window = new Window(start, start.AddSeconds(600.0));
                var bodies = new CelestialItem[] { _earth };
                var spacecraft = MeanSpacecraft(naifId--, set);
                var step = TimeSpan.FromSeconds(10.0);

                // Act, assert: ArgumentException naming the spacecraft and pointing to SGP4 (before: the two-body state of
                // the mean numbers, 11.75 km from SGP4 at t0 for the ISS, 468 km after a day)
                var paths = new (string Name, Func<Task> Run)[]
                {
                    ("CentralBodyPropagator", () => Task.FromResult(new CentralBodyPropagator(window, spacecraft, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step, ThirdBodies = bodies }))),
                    ("CentralBodyPropagator RK7(8)", () => Task.FromResult(new CentralBodyPropagator(window, spacecraft, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None }))),
                    ("Propagate", () => Task.FromResult(spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step, ThirdBodies = bodies }))),
                    ("Propagate RK7(8)", () => Task.FromResult(spacecraft.Propagate(window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None }))),
                    ("scenario", () => SimulateScenario(window, spacecraft, step)),
                };
                foreach (var (name, run) in paths)
                {
                    var exception = await Assert.ThrowsAsync<ArgumentException>(run);
                    Assert.Equal("spacecraft", exception.ParamName);
                    Assert.Contains("mean elements", exception.Message);
                    Assert.Contains(spacecraft.Name, exception.Message);
                    Assert.Contains("TlePropagator", exception.Message);
                    _output.WriteLine($"{name}: refused");
                }

                // The spacecraft has no state, before and after, as its guarded two-body ephemeris says
                Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(start, _earth));
            }
        }
    }

    private async Task<PropagationSolution> SimulateScenario(Window window, Spacecraft spacecraft, TimeSpan step)
    {
        var scenario = new Scenario("MEAN463", new IO.Astrodynamics.Mission.Mission("MEAN463"), window);
        scenario.AddCelestialItem(_earth);
        scenario.AddSpacecraft(spacecraft);
        scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step });
        return null;
    }
}
