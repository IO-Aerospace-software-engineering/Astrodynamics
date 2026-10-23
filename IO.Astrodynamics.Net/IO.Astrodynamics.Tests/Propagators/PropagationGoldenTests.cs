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
/// (lots B and A), which must not move a single bit of the trajectory. Recaptured with the fix of #363: the output
/// states, and the event and restart of a maneuver, come from shortened RK7(8) steps instead of a cubic Hermite
/// interpolation. The accepted steps before the maneuver did not move; the output states moved by up to 13 m, 77 m and
/// 63 m, and the maneuver by 99 µs. Recaptured with the fix of #368: the initial state goes through the Keplerian
/// elements, whose angles now come from atan2 instead of acos, so it is closer to the given state (the GEO z velocity is
/// now exactly the 10 m/s given, against 10 + 3.3e-11 before); the GEO and maneuver cases moved by 1.4 µm at most, the
/// SRP case did not move, and the maneuver epoch is unchanged. Recaptured with the fix of #360: SRP reads geometric
/// positions instead of the light time from the observer, so the SRP case moved by 1.7 mm and 1.8e-6 m/s at most, with
/// the same 102 steps; the two other cases have no SRP and did not move. Recaptured with the fix of #398: a state vector
/// at the start of the window is taken as it is, instead of through the Keplerian elements, so each case starts from
/// exactly the state given (1.9e-9 m and 2.1e-12 m/s away before, at most); the SRP case moved by 6.0 mm and 6.1e-6 m/s
/// at most, with 99 steps instead of 102 (the step control took other steps from the start), the GEO case by 9.2e-7 m,
/// the maneuver case by 5.8e-7 m, with the maneuver epoch unchanged. Recaptured with the fixes of #411 and #409: drag
/// takes the exact geodetic altitude, which the relation of the surface used to put up to 1.7 km too high at 400 km and
/// mid-latitudes, and NRLMSISE-00 takes the epoch in UTC instead of the TDB calendar; the SRP case, the only one with
/// drag, moved by 118 m and 0.133 m/s at most over the 3 h (117 m from the altitude, 1.06 m from the time scale), with
/// 102 steps instead of 99; the two other cases did not move. Recaptured with the fix of #434: the shadow fraction is
/// the sum of two circular segments instead of the closed form of the two-circle area, which cancelled near the
/// contacts of the penumbra; the acceleration in the penumbra changed by the rounding of the former form (a few 1e-9 of
/// ν, up to 1e-5 within a millimetre of a contact), and the step control took other steps from the first step whose
/// acceleration changed (step 13, at 1 646 s): the SRP case moved by 0.41 mm and 4.6e-7 m/s at most over the 3 h, with
/// the same 102 steps, less than its own integration error (1.19 mm at 1e-11 against 1e-13); the two other cases have
/// no SRP and did not move. A golden holds on one platform only (the mathematical library differs between
/// operating systems), so the test is in the <c>Validation</c> category, which the CI runs on Linux only.
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

        return spacecraft.PropagateWithVariationalEquations(new Window(Start, Start.AddHours(3.0)), new CelestialItem[] { earth, moon },
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

        return spacecraft.PropagateWithVariationalEquations(new Window(Start, Start.AddDays(1.0)), new CelestialItem[] { earth, moon, sun },
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

        return spacecraft.PropagateWithVariationalEquations(new Window(Start, Start.AddHours(4.0)), new CelestialItem[] { earth, moon, sun },
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
