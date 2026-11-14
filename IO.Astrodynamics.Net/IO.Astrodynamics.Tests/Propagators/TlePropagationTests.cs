// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.Tests.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The numerical propagation of a spacecraft whose initial orbit is a TLE or an osculating TEME state (#675). A TLE starts
/// from SGP4 at the window start on the Earth of the TLE, a TEME state from itself at its epoch; either is rotated once to
/// ICRF (the covariance through the Jacobian) and integrated in ICRF about the observer of the initial orbit. Before the
/// fix every path threw "Spacecraft initial orbital parameters must be defined in inertial frame".
/// </summary>
/// <remarks>
/// The references: the twin, a spacecraft whose initial orbit is that start built by the test from SGP4 (CSPICE
/// <c>evsgp4_c</c>, checked against Vallado's sgp4 2.27 by <c>Sgp4ReferenceTests</c>) and the managed TEME rotation of
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame"/> (checked against GMAT by <c>TemeFrameTests</c>), outside the
/// propagator; the two-body closed form of <see cref="KeplerClosedForm"/>; CSPICE reading the SPK the library writes; SGP4
/// for the reads of an unpropagated TLE spacecraft.
/// </remarks>
public class TlePropagationTests
{
    private const string Line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string Line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

    private static readonly string[] Paths =
        { "Propagate", "VVIntegrator", "RK78Integrator", "PropagateAsync", "Scenario", "BatchPropagator", "Constructor", "Builder" };

    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(60.0);

    // Twice the worst of TleStart_PointMass_FollowsKepler over Linux x64, Windows and macOS arm64 (CI run 43591654097, 2026-11-14), rounded up
    // at the second significant digit: position 9.249e-7, 9.171e-7 and 9.244e-7 m; velocity 1.172e-9, 1.164e-9 and
    // 1.173e-9 m/s
    private const double KeplerPosition = 1.9e-6;
    private const double KeplerVelocity = 2.4e-9;

    // Twice the worst of UnpropagatedOrSampledTleSpacecraft_ReadsSgp4 over Linux x64, Windows and macOS arm64 (CI run 43591654097, 2026-11-14),
    // rounded up at the second significant digit: the Lagrange interpolation of the TLEPropagator samples at mid-step, ISS
    // at 60 s, 7.593e-4, 7.593e-4 and 7.593e-4 m
    private const double SampledEphemerisBetweenSamples = 1.6e-3;

    private readonly ITestOutputHelper _output;

    public TlePropagationTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static TLE NewTle() => new("ISS", Line1, Line2);

    private static Window OneHour(TLE tle) => new(tle.Epoch, tle.Epoch.AddHours(1.0));

    /// <summary>The twin of a TLE start: SGP4 at the TDB start of the window, rotated to ICRF, on the Earth of the TLE.</summary>
    private static StateVector Twin(TLE tle, in Window window)
    {
        var t0 = window.StartDate.ToTDB();
        var s = tle.ToOsculating(t0).ToFrame(Frames.Frame.ICRF).ToStateVector();
        return new StateVector(s.Position, s.Velocity, tle.Observer, t0, Frames.Frame.ICRF);
    }

    private static Spacecraft NewSpacecraft(int naifId, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit) =>
        new(naifId, $"TLEP{-naifId}", 1000.0, 3000.0, new Clock($"tlep{-naifId}", 65536), orbit, 10.0, 2.2, null, 1.5);

    private static (Spacecraft Spacecraft, Engine Engine) WithEngine(int naifId, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
    {
        var spacecraft = NewSpacecraft(naifId, orbit);
        var tank = new FuelTank("ft675", "model", "sn675", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng675", "model", "sn675", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        return (spacecraft, engine);
    }

    private static void AssertSameStates(IReadOnlyList<StateVector> expected, IReadOnlyList<StateVector> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Epoch, actual[i].Epoch);
            Assert.Equal(expected[i].Position, actual[i].Position);
            Assert.Equal(expected[i].Velocity, actual[i].Velocity);
        }
    }

    /// <summary>One activation path of the public surface; the solution, null for the scenario, which returns none.</summary>
    private static PropagationSolution Run(string path, Spacecraft spacecraft, in Window window, CelestialItem[] bodies, bool srp)
    {
        switch (path)
        {
            case "Propagate":
                return spacecraft.Propagate(window, bodies, false, srp, OutputStep);
            case "VVIntegrator":
                return spacecraft.Propagate(window, bodies, new VVIntegrator(TimeSpan.FromSeconds(1.0)), false, srp, OutputStep);
            case "RK78Integrator":
                return spacecraft.Propagate(window, bodies, new RK78Integrator(), false, srp, OutputStep);
            case "PropagateAsync":
                return spacecraft.PropagateAsync(window, bodies, false, srp, OutputStep).GetAwaiter().GetResult();
            case "Scenario":
            {
                var scenario = new IO.Astrodynamics.Mission.Scenario($"TleScn{-spacecraft.NaifId}",
                    new IO.Astrodynamics.Mission.Mission($"tlemis{-spacecraft.NaifId}"), window);
                foreach (var body in bodies)
                {
                    scenario.AddCelestialItem(body);
                }

                scenario.AddSpacecraft(spacecraft);
                scenario.SimulateAsync(false, srp, OutputStep).GetAwaiter().GetResult();
                return null;
            }
            case "BatchPropagator":
            {
                var result = BatchPropagator.Propagate(new[]
                {
                    new PropagationTask(spacecraft, window, bodies, false, srp, OutputStep, () => new RK78Integrator())
                });
                Assert.True(result.AllSucceeded, result.Failed.FirstOrDefault()?.Error?.ToString());
                return result.Results[0].Solution;
            }
            case "Constructor":
            {
                using var propagator = new CentralBodyPropagator(window, spacecraft, new RK78Integrator(), bodies, false, srp, OutputStep);
                return propagator.Propagate();
            }
            case "Builder":
            {
                var builder = new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(), OutputStep).WithPerturbingBodies(bodies);
                if (srp)
                {
                    builder.IncludeSolarRadiationPressure();
                }

                using var propagator = builder.Build();
                return propagator.Propagate();
            }
            default:
                throw new ArgumentException(path);
        }
    }

    [Theory]
    [InlineData("Propagate", false)]
    [InlineData("Propagate", true)]
    [InlineData("VVIntegrator", false)]
    [InlineData("VVIntegrator", true)]
    [InlineData("RK78Integrator", false)]
    [InlineData("RK78Integrator", true)]
    [InlineData("PropagateAsync", true)]
    [InlineData("Scenario", false)]
    [InlineData("Scenario", true)]
    [InlineData("BatchPropagator", true)]
    [InlineData("Constructor", false)]
    [InlineData("Constructor", true)]
    [InlineData("Builder", false)]
    [InlineData("Builder", true)]
    public void TleStart_EqualsItsIcrfTwin(string path, bool thirdBodiesAndSrp)
    {
        // Arrange: the TLE spacecraft and its twin, the central body only (the point-mass Earth of the TLE), or with the Moon
        // and the Sun and SRP; no other Earth in the bodies (an Earth twin is ignored for gravity, a separate defect)
        var tle = NewTle();
        var window = OneHour(tle);
        var bodies = thirdBodiesAndSrp
            ? new CelestialItem[] { new CelestialBody(PlanetsAndMoons.MOON), new CelestialBody(Stars.Sun) }
            : Array.Empty<CelestialItem>();
        int id = -6750 - 2 * Array.IndexOf(Paths, path) - (thirdBodiesAndSrp ? 100 : 0);
        var fromTle = NewSpacecraft(id, tle);
        var twin = NewSpacecraft(id - 1, Twin(tle, window));

        // Act
        var solution = Run(path, fromTle, window, bodies, thirdBodiesAndSrp);
        var twinSolution = Run(path, twin, window, bodies, thirdBodiesAndSrp);

        // Assert: the solution in ICRF, bit for bit the twin's; the stored states too, and no maneuver
        if (solution != null)
        {
            Assert.Equal(Frames.Frame.ICRF, solution.Frame);
            Assert.All(solution.StateVectors, s => Assert.Equal(Frames.Frame.ICRF, s.Frame));
            AssertSameStates(twinSolution.StateVectors, solution.StateVectors);
        }

        Assert.Equal(61, fromTle.PropagatedStates.Length);
        AssertSameStates(twin.PropagatedStates, fromTle.PropagatedStates);
        Assert.Equal(twin.ExecutedManeuvers.Count, fromTle.ExecutedManeuvers.Count);
    }

    [Theory]
    [InlineData("Propagate")]
    [InlineData("RK78Integrator")]
    [InlineData("Scenario")]
    [InlineData("Builder")]
    public void TleStart_AwayFromItsEpoch_IsSgp4AtTheWindowStart(string path)
    {
        // Arrange: a window 6 h after the TLE epoch; the start is SGP4 at the window start, rotated to ICRF, not SGP4 at the
        // TLE epoch carried to the window start by another motion
        var tle = NewTle();
        var window = new Window(tle.Epoch.AddHours(6.0), tle.Epoch.AddHours(7.0));
        var t0 = window.StartDate.ToTDB();
        var sgp4 = tle.ToStateVector(t0).ToFrame(Frames.Frame.ICRF).ToStateVector();
        int id = -7060 - 2 * Array.IndexOf(Paths, path);
        var fromTle = NewSpacecraft(id, tle);
        var twin = NewSpacecraft(id - 1, Twin(tle, window));

        // Act
        var solution = Run(path, fromTle, window, Array.Empty<CelestialItem>(), false);
        var twinSolution = Run(path, twin, window, Array.Empty<CelestialItem>(), false);

        // Assert: the first state is SGP4 at the window start bit for bit, every state the twin's
        var first = solution?.StateVectors[0] ?? fromTle.PropagatedStates[0];
        Assert.Equal(t0, first.Epoch);
        Assert.Equal(sgp4.Position, first.Position);
        Assert.Equal(sgp4.Velocity, first.Velocity);
        Assert.Equal(sgp4.Position, fromTle.PropagatedStates[0].Position);
        Assert.Equal(sgp4.Velocity, fromTle.PropagatedStates[0].Velocity);
        if (solution != null)
        {
            AssertSameStates(twinSolution.StateVectors, solution.StateVectors);
        }

        AssertSameStates(twin.PropagatedStates, fromTle.PropagatedStates);
    }

    [Theory]
    [InlineData("Covariance")]
    [InlineData("LaterEpoch")]
    [InlineData("Ssb")]
    [InlineData("DragVV")]
    [InlineData("DragRK78")]
    public void TemeStart_EqualsItsIcrfTwin(string variant)
    {
        // Arrange: a TEME state, the SGP4 state of the ISS, and its twin rotated to ICRF by ToFrame
        var tle = NewTle();
        var window = OneHour(tle);
        var t0 = window.StartDate.ToTDB();
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);
        CelestialBody earth = variant.StartsWith("Drag")
            ? new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, t0,
                new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new Nrlmsise00Model(SpaceWeather.Nominal))
            : new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, t0);
        var sgp4 = tle.ToOsculating(variant == "LaterEpoch" ? t0.AddMinutes(10.0) : t0);
        StateVector teme = new StateVector(sgp4.Position, sgp4.Velocity, earth, sgp4.Epoch, Frames.Frame.TEME);
        CelestialItem[] bodies = { earth, moon, sun };
        if (variant == "Ssb")
        {
            // The same orbit relative to the solar-system barycentre, in TEME
            var icrf = teme.ToFrame(Frames.Frame.ICRF).ToStateVector();
            var earthFromSsb = earth.GetGeometricStateRelativeTo(t0, Barycenters.SOLAR_SYSTEM_BARYCENTER);
            teme = new StateVector(earthFromSsb.Position + icrf.Position, earthFromSsb.Velocity + icrf.Velocity,
                Barycenters.SOLAR_SYSTEM_BARYCENTER, t0, Frames.Frame.ICRF).ToFrame(Frames.Frame.TEME).ToStateVector();
        }

        if (variant == "Covariance")
        {
            var p0 = new Matrix(6, 6);
            double[] sigma = { 30.0, 80.0, 20.0, 0.05, 0.02, 0.01 };
            for (int i = 0; i < 6; i++)
            {
                p0.Set(i, i, sigma[i] * sigma[i]);
            }

            p0.Set(0, 4, -0.5 * sigma[0] * sigma[4]);
            p0.Set(4, 0, -0.5 * sigma[0] * sigma[4]);
            teme = new StateVector(teme.Position, teme.Velocity, teme.Observer, teme.Epoch, Frames.Frame.TEME, p0);
        }

        var twinState = teme.ToFrame(Frames.Frame.ICRF).ToStateVector();
        bool drag = variant.StartsWith("Drag");

        PropagationSolution Propagate(StateVector start, int id, bool withDrag = true)
        {
            var spacecraft = NewSpacecraft(id, start);
            return variant switch
            {
                "Covariance" => spacecraft.Propagate(window, bodies, new RK78Integrator(), false, false, OutputStep,
                    new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }),
                "Ssb" => spacecraft.Propagate(window, bodies, new RK78Integrator(1e-9, 1e-9), false, false, OutputStep),
                "DragVV" => spacecraft.Propagate(window, bodies, new VVIntegrator(TimeSpan.FromSeconds(1.0)), drag && withDrag, true, OutputStep),
                _ => spacecraft.Propagate(window, bodies, new RK78Integrator(), drag && withDrag, true, OutputStep)
            };
        }

        int baseId = -6950 - 10 * Array.IndexOf(new[] { "Covariance", "LaterEpoch", "Ssb", "DragVV", "DragRK78" }, variant);

        // Act
        var solution = Propagate(teme, baseId);
        var twin = Propagate(twinState, baseId - 1);

        // Assert: in ICRF, bit for bit the twin's
        Assert.Equal(Frames.Frame.ICRF, solution.Frame);
        Assert.All(solution.StateVectors, s => Assert.Equal(Frames.Frame.ICRF, s.Frame));
        AssertSameStates(twin.StateVectors, solution.StateVectors);
        var end = window.EndDate.ToTDB();
        if (variant == "Covariance")
        {
            Assert.Equal(twin.GetStateTransitionMatrix(end).ToArray(), solution.GetStateTransitionMatrix(end).ToArray());
            Assert.Equal(twin.GetCovariance(end).ToArray(), solution.GetCovariance(end).ToArray());
            Assert.Equal(twin.GetCovariance(t0).ToArray(), solution.GetCovariance(t0).ToArray());
        }

        if (drag)
        {
            // Drag on the Earth of the state, which carries NRLMSISE-00: not dropped
            var withoutDrag = Propagate(teme, baseId - 2, withDrag: false);
            double moved = (withoutDrag.StateVectors[^1].Position - solution.StateVectors[^1].Position).Magnitude();
            _output.WriteLine($"MEASURE TlePropagationTests.TemeStart_EqualsItsIcrfTwin {variant}: drag moves the end by {moved:E3} m");
            Assert.True(moved > 1.0, $"{moved:E3} m");
        }
    }

    [Fact]
    public void MeanTemeSet_StaysRejected()
    {
        // Arrange: the mean elements of the TLE (Mean, TEME): no state without their theory (#463)
        var tle = NewTle();
        var window = OneHour(tle);
        var mean = NewSpacecraft(-6981, tle.ToMeanKeplerianElements());
        Assert.Equal(OrbitalElementsType.Mean, mean.InitialOrbitalParameters.ElementsType);
        Assert.Equal(Frames.Frame.TEME, mean.InitialOrbitalParameters.Frame);

        // Act, assert: the exception of the inertial-frame check, as before
        var e = Assert.Throws<ArgumentException>(() => mean.Propagate(window, Array.Empty<CelestialItem>(), false, false, OutputStep));
        Assert.Contains("must be defined in inertial frame", e.Message);

        // A TEME target chained on a TLE spacecraft: rejected when the propagator is built, the previous run left as it was
        var (spacecraft, engine) = WithEngine(-6982, tle);
        spacecraft.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep);
        var before = spacecraft.PropagatedStates;
        var temeTarget = new KeplerianElements(6800000.0, 0.0, 55.0 * IO.Astrodynamics.Constants.Deg2Rad, 6.0, 0.0, 0.0,
            tle.Observer, window.StartDate.ToTDB(), Frames.Frame.TEME);
        var icrfTarget = temeTarget.ToFrame(Frames.Frame.ICRF);
        var first = new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, icrfTarget, engine);
        first.SetNextManeuver(new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, temeTarget, engine));
        spacecraft.SetStandbyManeuver(first);
        var chained = Assert.Throws<ArgumentException>(() =>
            spacecraft.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep));
        Assert.Contains("ToFrame(Frame.ICRF)", chained.Message);
        AssertSameStates(before, spacecraft.PropagatedStates);
    }

    [Fact]
    public void TleStart_PointMass_FollowsKepler()
    {
        // Arrange: the central body only, the point-mass Earth of the TLE; the closed form from the SGP4 ICRF start
        var tle = NewTle();
        var window = OneHour(tle);
        var spacecraft = NewSpacecraft(-6990, tle);
        var start = Twin(tle, window);
        var kepler = new KeplerClosedForm(((CelestialBody)tle.Observer).GM, start.Position, start.Velocity, 0.0);

        // Act
        var solution = spacecraft.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(1e-12, 1e-12), false, false, OutputStep);

        // Assert: every output against the two-body motion over the TT seconds from the TDB start of the window, where the
        // integration starts (the UTC start of the window is 7 ns off its TDB label, 5.3e-5 m)
        double dr = 0.0, dv = 0.0;
        foreach (var state in solution.StateVectors)
        {
            var (position, velocity) = kepler.StateAt(PropagationTimeArgument.Tt.SecondsBetween(start.Epoch, state.Epoch));
            dr = System.Math.Max(dr, (state.Position - position).Magnitude());
            dv = System.Math.Max(dv, (state.Velocity - velocity).Magnitude());
        }

        _output.WriteLine($"MEASURE TlePropagationTests.TleStart_PointMass_FollowsKepler: {dr:E3} m, {dv:E3} m/s");
        Assert.InRange(dr, 0.0, KeplerPosition);
        Assert.InRange(dv, 0.0, KeplerVelocity);
    }

    [Theory]
    [InlineData("VVIntegrator")]
    [InlineData("RK78Integrator")]
    [InlineData("Builder")]
    public void PropagatedTleSpacecraft_ReadsItsStates(string path)
    {
        // Arrange: a TLE spacecraft propagated on its point-mass Earth, 1 h; the builder leaves IsPropagated false
        var tle = NewTle();
        var window = OneHour(tle);
        int id = path switch { "VVIntegrator" => -7001, "RK78Integrator" => -7011, _ => -7021 };
        var spacecraft = NewSpacecraft(id, tle);
        Run(path, spacecraft, window, Array.Empty<CelestialItem>(), false);
        Assert.Equal(path != "Builder", spacecraft.IsPropagated);
        var earth = (CelestialItem)tle.Observer;

        // Act, assert: its reads are its stored states, 50 km and more from SGP4 at the end (53.7 km measured): the geometric
        // state, which the reads from any other body and the searches go through (GetEphemeris from the Earth without
        // aberration reads the stored states directly, whatever the initial orbit), and the window it interpolates
        var stored = spacecraft.PropagatedStates;
        foreach (var state in stored)
        {
            var geometric = spacecraft.GetGeometricStateRelativeTo(state.Epoch, earth);
            Assert.Equal(state.Position, geometric.Position);
            Assert.Equal(state.Velocity, geometric.Velocity);
            var read = spacecraft.GetEphemeris(state.Epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            Assert.Equal(state.Position, read.Position);
            Assert.Equal(state.Velocity, read.Velocity);
        }

        Assert.Equal(new Window(stored[0].Epoch, stored[^1].Epoch), spacecraft.InterpolatedStatesWindow);
        var last = stored[^1];
        double fromSgp4 = (tle.ToStateVector(last.Epoch).ToFrame(Frames.Frame.ICRF).ToStateVector().Position - last.Position).Magnitude();
        _output.WriteLine($"MEASURE TlePropagationTests.PropagatedTleSpacecraft_ReadsItsStates {path}: {fromSgp4 / 1000.0:F3} km from SGP4 at 1 h");
        Assert.True(fromSgp4 > 50000.0, $"{fromSgp4:F1} m");

        // The SPK the library writes, read by CSPICE (spkezr), against the stored states
        var directory = Directory.CreateDirectory($"Data/User/TlePropagation675/{path}");
        var file = new FileInfo(Path.Combine(directory.FullName, $"{-id}.spk"));
        if (file.Exists)
        {
            file.Delete();
        }

        spacecraft.WriteEphemeris(file);
        double worst = 0.0;
        SpiceAPI.Instance.LoadKernels(file);
        try
        {
            foreach (var state in stored)
            {
                var fromSpk = SpiceAPI.Instance.ReadEphemeris(state.Epoch, 399, spacecraft.NaifId, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                worst = System.Math.Max(worst, (fromSpk.Position - state.Position).Magnitude());
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(file);
        }

        _output.WriteLine($"MEASURE TlePropagationTests.PropagatedTleSpacecraft_ReadsItsStates {path}: SPK against the stored states {worst:E3} m");
        Assert.InRange(worst, 0.0, PropagatedArcsTests.SpkAgainstManaged);

        // The conjunction on its states: a window past them throws (#527)
        var secondary = NewSpacecraft(id - 1, new StateVector(last.Position + new Vector3(1000.0, 0.0, 0.0), last.Velocity * -1.0, earth, last.Epoch,
            Frames.Frame.ICRF));
        var past = new Window(window.StartDate, window.EndDate.AddMinutes(10.0));
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(spacecraft), secondary, past));
    }

    [Fact]
    public void PropagatedTleSpacecraft_EvaluatesAvoidanceOnItsForceModel()
    {
        // Arrange: the TLE spacecraft and its twin in RK7(8), 3 h, a secondary near each at 2 h
        var tle = NewTle();
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(3.0));
        var fromTle = NewSpacecraft(-7031, tle);
        var twin = NewSpacecraft(-7032, Twin(tle, window));
        fromTle.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep);
        twin.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep);
        var tca = tle.Epoch.AddHours(2.0);
        var earth = (CelestialItem)tle.Observer;
        var secondaryState = AvoidanceFallbackCase.SecondaryNear(fromTle.GetEphemeris(tca, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector());
        var screening = new Window(tle.Epoch, tle.Epoch.AddHours(2.5));
        var options = new AvoidanceSearchOptions
        {
            LeadTimes = new[] { TimeSpan.FromHours(1.0) }, DeltaVMagnitudesMetersPerSecond = new[] { 0.1 }, MaxReturnedOptions = 6
        };

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(fromTle),
            NewSpacecraft(-7033, secondaryState), screening), options);
        var twinAvoidance = ConjunctionAssessment.EvaluateAvoidance(ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(twin),
            NewSpacecraft(-7034, secondaryState), screening), options);

        // Assert: the force model of its propagation, not the two-body orbit, bit for bit the twin's
        Assert.Equal(twinAvoidance.Count, avoidance.Count);
        Assert.NotEmpty(avoidance);
        for (int i = 0; i < avoidance.Count; i++)
        {
            Assert.Equal(AvoidanceTrajectory.Propagated, avoidance[i].Trajectory);
            Assert.Equal(twinAvoidance[i].BurnEpoch, avoidance[i].BurnEpoch);
            Assert.Equal(twinAvoidance[i].DeltaVInertial, avoidance[i].DeltaVInertial);
            Assert.Equal(twinAvoidance[i].PostManeuverEncounter.EncounterState.Epoch, avoidance[i].PostManeuverEncounter.EncounterState.Epoch);
            Assert.Equal(twinAvoidance[i].PostManeuverEncounter.EncounterState.MissDistanceMeters,
                avoidance[i].PostManeuverEncounter.EncounterState.MissDistanceMeters);
        }
    }

    [Fact]
    public void UnpropagatedOrSampledTleSpacecraft_ReadsSgp4()
    {
        // Arrange: one TLE spacecraft unpropagated, one sampled by TLEPropagator
        var tle = NewTle();
        var window = OneHour(tle);
        var unpropagated = NewSpacecraft(-7041, tle);
        var sampled = NewSpacecraft(-7042, tle);
        using (var propagator = new TLEPropagator(window, sampled, OutputStep))
        {
            propagator.Propagate();
        }

        // Act, assert: the geometric state is SGP4 in ICRF at any epoch, between the samples too, and the window of
        // interpolated states is none (#527's check skipped); the ephemeris of the unpropagated one from its Earth too
        var earth = (CelestialItem)tle.Observer;
        foreach (double s in new[] { 0.0, 30.0, 1234.5, 3600.0 })
        {
            var epoch = window.StartDate.ToTDB().AddSeconds(s);
            var sgp4 = tle.ToStateVector(epoch).ToFrame(Frames.Frame.ICRF).ToStateVector();
            foreach (var spacecraft in new[] { unpropagated, sampled })
            {
                var read = spacecraft.GetGeometricStateRelativeTo(epoch, earth);
                Assert.Equal(sgp4.Position, read.Position);
                Assert.Equal(sgp4.Velocity, read.Velocity);
                Assert.Null(spacecraft.InterpolatedStatesWindow);
            }

            var ephemeris = unpropagated.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            Assert.Equal(sgp4.Position, ephemeris.Position);
            Assert.Equal(sgp4.Velocity, ephemeris.Velocity);
        }

        // The ephemeris of the sampled one from its Earth without aberration reads its stored states directly, as before
        // #675: the SGP4 samples bit for bit, their Lagrange interpolation between them (SGP4 at the exact instant the
        // reference)
        double worst = 0.0;
        for (int i = 0; i < 60; i++)
        {
            var sample = window.StartDate.ToTDB().AddSeconds(60.0 * i);
            var atSample = sampled.GetEphemeris(sample, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var sgp4AtSample = tle.ToStateVector(sample).ToFrame(Frames.Frame.ICRF).ToStateVector();
            Assert.Equal(sgp4AtSample.Position, atSample.Position);
            Assert.Equal(sgp4AtSample.Velocity, atSample.Velocity);
            var middle = sample.AddSeconds(30.0);
            var between = sampled.GetEphemeris(middle, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            worst = System.Math.Max(worst, (tle.ToStateVector(middle).ToFrame(Frames.Frame.ICRF).ToStateVector().Position - between.Position).Magnitude());
        }

        _output.WriteLine($"MEASURE TlePropagationTests.UnpropagatedOrSampledTleSpacecraft_ReadsSgp4: sampled ephemeris from the Earth " +
                          $"between the samples {worst:E3} m from SGP4");
        Assert.InRange(worst, 0.0, SampledEphemerisBetweenSamples);
    }

    [Fact]
    public void TleSpacecraft_TargetOrbitManeuver()
    {
        // Arrange: a plane alignment to a plane at 55° of inclination, its target in ICRF, on the TLE spacecraft and its twin
        var tle = NewTle();
        var window = OneHour(tle);
        var t0 = window.StartDate.ToTDB();
        IO.Astrodynamics.OrbitalParameters.OrbitalParameters Target(Frames.Frame frame) => new KeplerianElements(6800000.0, 0.0,
            55.0 * IO.Astrodynamics.Constants.Deg2Rad, 6.0, 0.0, 0.0, tle.Observer, t0, frame);

        (Spacecraft, PlaneAlignmentManeuver) Build(int id, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
        {
            var (spacecraft, engine) = WithEngine(id, orbit);
            var maneuver = new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, Target(Frames.Frame.ICRF), engine);
            spacecraft.SetStandbyManeuver(maneuver);
            return (spacecraft, maneuver);
        }

        // Act: the ICRF target constructs on a TLE spacecraft (it threw "must be defined in the same frame")
        var (fromTle, maneuver) = Build(-7051, tle);
        var (twin, twinManeuver) = Build(-7052, Twin(tle, window));
        var solution = fromTle.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep);
        var twinSolution = twin.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep);

        // Assert: executed, its ΔV, epoch and the final state bit for bit the twin's
        Assert.NotNull(maneuver.ThrustWindow);
        Assert.Single(fromTle.ExecutedManeuvers);
        Assert.Equal(twinManeuver.ThrustWindow, maneuver.ThrustWindow);
        Assert.Equal(twinManeuver.DeltaV, maneuver.DeltaV);
        Assert.Equal(twinSolution.StateVectors[^1].Position, solution.StateVectors[^1].Position);
        Assert.Equal(twinSolution.StateVectors[^1].Velocity, solution.StateVectors[^1].Velocity);
        AssertSameStates(twin.PropagatedStates, fromTle.PropagatedStates);

        // A TEME target: constructs, as before, and is rejected when the propagation is built, naming the fix
        var (temeSpacecraft, engine) = WithEngine(-7053, tle);
        var temeManeuver = new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, Target(Frames.Frame.TEME), engine);
        temeSpacecraft.SetStandbyManeuver(temeManeuver);
        var e = Assert.Throws<ArgumentException>(() =>
            temeSpacecraft.Propagate(window, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, OutputStep));
        Assert.Contains("ToFrame(Frame.ICRF)", e.Message);
        Assert.False(temeSpacecraft.IsPropagated);

        // Built before the engine is attached (no check at construction): rejected at the propagation as well
        var tank = new FuelTank("ft675b", "model", "sn675b", 1000.0, 1000.0);
        var detached = new Engine("eng675b", "model", "sn675b", 300.0, 10.0, tank);
        var early = new PlaneAlignmentManeuver(window.StartDate, TimeSpan.Zero, Target(Frames.Frame.ECLIPTIC_J2000), detached);
        var late = NewSpacecraft(-7054, tle);
        late.AddFuelTank(tank);
        late.AddEngine(detached);
        late.SetStandbyManeuver(early);
        Assert.Throws<ArgumentException>(() => new CentralBodyPropagator(window, late, new RK78Integrator(), Array.Empty<CelestialItem>(),
            false, false, OutputStep));
    }
}
