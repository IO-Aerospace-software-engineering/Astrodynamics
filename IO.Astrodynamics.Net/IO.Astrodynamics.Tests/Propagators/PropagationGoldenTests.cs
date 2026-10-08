// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// Bit-for-bit golden of three RK7(8) propagations: every accepted step and every output state.
/// </summary>
/// <remarks>
/// <para>
/// Captured on <c>main</c> before the force partial derivatives and the variational equations of phase 2, feature 1
/// (lots B and A), which must not move a single bit of the trajectory. A golden holds on one platform only (the
/// mathematical library differs between operating systems), so the test is in the <c>Validation</c> category, which
/// the CI runs on Linux only.
/// </para>
/// <para>
/// The cases avoid two known defects, so that fixing them does not invalidate the golden: SRP is zero when the Sun is
/// among the bodies (#356), and the propagation must be in ICRF (#357). The SRP case therefore has the Moon as only
/// third body.
/// </para>
/// <para>
/// To recapture after an intended change of the trajectory, set the environment variable
/// <c>IO_ASTRODYNAMICS_GOLDEN_CAPTURE</c> to the <c>Data/Golden</c> directory of the test project and run the test: it
/// writes the files instead of comparing, then fails, so that a capture never passes unnoticed.
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class PropagationGoldenTests
{
    private const string CaptureVariable = "IO_ASTRODYNAMICS_GOLDEN_CAPTURE";
    private const string GoldenDirectory = "Data/Golden";

    private static readonly TimeSystem.Time Start = new(2021, 3, 20, 12, 0, 0);

    public PropagationGoldenTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Theory]
    [InlineData("rk78_leo_drag_srp")]
    [InlineData("rk78_geo_moon_sun")]
    [InlineData("rk78_leo_maneuver")]
    public void Rk78Propagation_IsBitIdenticalToGolden(string caseName)
    {
        // Arrange
        var fileName = caseName + ".json";

        // Act
        var actual = GoldenRecord.From(Propagate(caseName));

        // Assert
        var captureDirectory = Environment.GetEnvironmentVariable(CaptureVariable);
        if (!string.IsNullOrEmpty(captureDirectory))
        {
            File.WriteAllText(Path.Combine(captureDirectory, fileName), JsonSerializer.Serialize(actual));
            Assert.Fail($"Golden {fileName} captured in {captureDirectory}, not compared.");
        }

        var expected = JsonSerializer.Deserialize<GoldenRecord>(File.ReadAllText(Path.Combine(GoldenDirectory, fileName)));
        AssertBitIdentical(expected!, actual);
    }

    /// <summary>
    /// The same propagations with the variational equations (Φ, Ψ for Cd and Cr, and Q): the step control is on the
    /// state only and the variational stages never feed the state, so the trajectory is the same golden (A2).
    /// </summary>
    [Theory]
    [InlineData("rk78_leo_drag_srp")]
    [InlineData("rk78_geo_moon_sun")]
    [InlineData("rk78_leo_maneuver")]
    public void Rk78PropagationWithTheVariationalEquations_IsBitIdenticalToGolden(string caseName)
    {
        // Arrange
        var options = new VariationalOptions(ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient,
            new ConstantProcessNoise(new[] { 1e-12, 0.0, 0.0, 0.0, 1e-12, 0.0, 0.0, 0.0, 1e-12 }));

        // Act
        var solution = Propagate(caseName, options);

        // Assert
        Assert.All(solution.Segments, segment => Assert.NotNull(segment.Variational));
        var expected = JsonSerializer.Deserialize<GoldenRecord>(
            File.ReadAllText(Path.Combine(GoldenDirectory, caseName + ".json")));
        AssertBitIdentical(expected!, GoldenRecord.From(solution));
    }

    private static PropagationSolution Propagate(string caseName, VariationalOptions options = null)
    {
        return caseName switch
        {
            "rk78_leo_drag_srp" => PropagateLeoWithDragAndSrp(options),
            "rk78_geo_moon_sun" => PropagateGeo(options),
            "rk78_leo_maneuver" => PropagateLeoWithManeuver(options),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName))
        };
    }

    private static PropagationSolution PropagateLeoWithDragAndSrp(VariationalOptions options)
    {
        // 400 km, 51.6 deg, EGM2008 10x10, NRLMSISE-00 (nominal space weather), Moon, drag and SRP, 3 h.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6778137.0, 0.0, 0.0), new Vector3(0.0, 4751.0, 5993.0), earth, Start,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1951, "GOLDEN1", 100.0, 1000.0, new Clock("golden1", 65536), orbit, 10.0, 2.2,
            null, 1.5);

        return spacecraft.Propagate(new Window(Start, Start.AddHours(3.0)), new CelestialItem[] { earth, moon },
            new RK78Integrator(1e-11, 1e-11), true, true, TimeSpan.FromSeconds(60.0), options);
    }

    private static PropagationSolution PropagateGeo(VariationalOptions options)
    {
        // GEO, EGM2008 10x10, Moon and Sun, 1 day.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun);
        var orbit = new StateVector(new Vector3(42164137.0, 0.0, 0.0), new Vector3(0.0, 3074.66, 10.0), earth, Start,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1952, "GOLDEN2", 2000.0, 5000.0, new Clock("golden2", 65536), orbit);

        return spacecraft.Propagate(new Window(Start, Start.AddDays(1.0)), new CelestialItem[] { earth, moon, sun },
            new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(600.0), options);
    }

    private static PropagationSolution PropagateLeoWithManeuver(VariationalOptions options)
    {
        // 400 x 1000 km LEO starting at apogee, EGM2008 10x10, Moon and Sun, apogee raised to 2000 km at the first
        // perigee (about 50 min later), 4 h.
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun);
        var orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), earth, Start,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1953, "GOLDEN3", 1000.0, 3000.0, new Clock("golden3", 65536), orbit);
        var tank = new FuelTank("golden3tank", "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("golden3engine", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, 8378137.0, engine));

        return spacecraft.Propagate(new Window(Start, Start.AddHours(4.0)), new CelestialItem[] { earth, moon, sun },
            new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(60.0), options);
    }

    private static void AssertBitIdentical(GoldenRecord expected, GoldenRecord actual)
    {
        Assert.Equal(expected.Segments.Count, actual.Segments.Count);
        for (int s = 0; s < expected.Segments.Count; s++)
        {
            Assert.Equal(expected.Segments[s].Steps.Count, actual.Segments[s].Steps.Count);
            AssertSameBits(expected.Segments[s].BaseEpoch, actual.Segments[s].BaseEpoch, $"segment {s} base epoch");
            for (int i = 0; i < expected.Segments[s].Steps.Count; i++)
            {
                AssertSameBits(expected.Segments[s].Steps[i], actual.Segments[s].Steps[i], $"segment {s}, step {i}");
            }
        }

        Assert.Equal(expected.States.Count, actual.States.Count);
        for (int i = 0; i < expected.States.Count; i++)
        {
            AssertSameBits(expected.States[i], actual.States[i], $"output state {i}");
        }
    }

    private static void AssertSameBits(double[] expected, double[] actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int k = 0; k < expected.Length; k++)
        {
            AssertSameBits(expected[k], actual[k], $"{what}, value {k}");
        }
    }

    private static void AssertSameBits(double expected, double actual, string what)
    {
        Assert.True(BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{what}: expected {expected:R}, got {actual:R}");
    }

    /// <summary>
    /// Every accepted step (cumulative time, step size, start and end position, velocity and acceleration) and every
    /// output state (TDB seconds from J2000, position, velocity) of a solution. The default JSON serialization of a
    /// double round-trips exactly.
    /// </summary>
    private sealed record GoldenRecord(List<GoldenSegment> Segments, List<double[]> States)
    {
        public static GoldenRecord From(PropagationSolution solution)
        {
            var segments = solution.Segments.Select(segment => new GoldenSegment(
                segment.BaseEpoch.TimeSpanFromJ2000().TotalSeconds,
                segment.Steps.Select(step => new[]
                {
                    step.CumulativeTime, step.StepSize,
                    step.StartPosition.X, step.StartPosition.Y, step.StartPosition.Z,
                    step.StartVelocity.X, step.StartVelocity.Y, step.StartVelocity.Z,
                    step.EndPosition.X, step.EndPosition.Y, step.EndPosition.Z,
                    step.EndVelocity.X, step.EndVelocity.Y, step.EndVelocity.Z,
                    step.StartAcceleration.X, step.StartAcceleration.Y, step.StartAcceleration.Z,
                    step.EndAcceleration.X, step.EndAcceleration.Y, step.EndAcceleration.Z
                }).ToList())).ToList();
            var states = solution.StateVectors.Select(state => new[]
            {
                state.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds,
                state.Position.X, state.Position.Y, state.Position.Z,
                state.Velocity.X, state.Velocity.Y, state.Velocity.Z
            }).ToList();
            return new GoldenRecord(segments, states);
        }
    }

    private sealed record GoldenSegment(double BaseEpoch, List<double[]> Steps);
}
