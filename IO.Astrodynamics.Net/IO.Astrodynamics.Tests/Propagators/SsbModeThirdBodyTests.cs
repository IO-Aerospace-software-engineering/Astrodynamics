// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A LEO propagated in SSB mode, about the Sun with the Earth and the Moon as third bodies (#576, #544, #389): Battin's
/// 1 + q put a noise of about 1e-6 m/s² on the Earth's attraction, so RK7(8)'s step control followed the noise (tens of
/// thousands of steps for an hour), its step sequence changed with the last bits of its inputs, it did not finish at
/// 1e-12, and a start from ECLIPJ2000 or GALACTIC ended decimetres from its ICRF twin. The case of #544: a = 6 778 km,
/// e = 0.001, i = 51.6°, 2024-03-01 12:00 TDB, 1 h, outputs every 10 s.
/// </summary>
public class SsbModeThirdBodyTests
{
    private static readonly TimeSystem.Time Start = new(new DateTime(2024, 3, 1, 12, 0, 0), TimeScale.TDB);
    private static readonly Window Window = new(Start, Start.AddSeconds(3600.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(10.0);
    private readonly ITestOutputHelper _output;

    public SsbModeThirdBodyTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialItem[] Bodies() =>
        new CelestialItem[] { new CelestialBody(399), new CelestialBody(301), new CelestialBody(10) };

    // The geocentric LEO of #544, relative to the solar system barycentre, in the given frame
    private static StateVector SsbState(Frames.Frame frame)
    {
        var earth = new CelestialBody(399);
        var geocentric = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.3, 0.7,
            1.1, earth, Start, Frames.Frame.ICRF).ToStateVector();
        var ssb = geocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        return frame == Frames.Frame.ICRF ? ssb : ssb.ToFrame(frame).ToStateVector();
    }

    private static Spacecraft NewSpacecraft(StateVector orbit, int naifId) =>
        new(naifId, "SSBTB" + -naifId, 1000.0, orbit, 3000.0, new Clock("ssbtb" + -naifId, 65536));

    // The final position relative to the Sun in ICRF
    private static IO.Astrodynamics.Math.Vector3 FinalPosition(PropagationSolution solution, Frames.Frame frame)
    {
        var (position, velocity) = solution.InterpolateAt(Window.EndDate);
        var state = new StateVector(position, velocity, Stars.SUN_BODY, Window.EndDate, frame);
        return frame == Frames.Frame.ICRF ? position : state.ToFrame(Frames.Frame.ICRF).ToStateVector().Position;
    }

    private static int Steps(PropagationSolution solution) => solution.Segments.Sum(s => s.Steps.Count);

    /// <summary>
    /// RK7(8) at 1e-10 takes steps of the order of those relative to the Earth (23 there), not tens of thousands, and
    /// about as many when the tolerance moves by one ulp: the step control no longer follows Battin's noise of 1e-6 m/s².
    /// </summary>
    /// <remarks>Measured on a85098d (Linux x64, Release, 2026-11-17): 21 363 steps, 21 317 one ulp looser, the final
    /// positions 8.8e-2 m apart. With the fix (CI run 44225490669): 49 and 47 steps, 5.632e-3 m apart, on Linux x64 and
    /// Windows x64; 46 and 41 steps, 2.636e-3 m apart, on macOS arm64 (the tolerance relative to 1.5e11 m allows metres per
    /// step). Thresholds twice the worst of the three platforms. The step count is not the same one ulp apart: in SSB mode,
    /// below 1e-10, the step control still meets a floor of about 1e-8 m/s² on the Earth's attraction, the quantization of
    /// the epoch at which the third bodies are read (ticks of 100 ns, TDB seconds near 7.6e8 s in a double, 1.2e-7 s,
    /// millimetres of the Earth's position at 30 km/s), absent about the Earth. Draws one ulp apart on Linux x64 (the
    /// tolerance moved by 1 to 40 ulps or the start by 1 to 30 ulps) take 38 to 62 steps, two consecutive draws up to 20 steps apart, so
    /// <see cref="NudgedStepDifference"/> is set from those draws (decided by S. Guillet, 2026-11-18).</remarks>
    [Fact]
    public void Rk78_StepsFollowTheDynamics_NotTheRounding()
    {
        // Arrange
        var bodies = Bodies();
        const double tolerance = 1e-10;

        // Act
        var first = NewSpacecraft(SsbState(Frames.Frame.ICRF), -5761)
            .Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = tolerance, RelativeTolerance = tolerance }, OutputStep = Output, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None });
        double nudged = System.Math.BitIncrement(tolerance);
        var second = NewSpacecraft(SsbState(Frames.Frame.ICRF), -5762)
            .Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = nudged, RelativeTolerance = nudged }, OutputStep = Output, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None });

        // Assert
        int steps = Steps(first);
        int stepsNudged = Steps(second);
        double apart = (FinalPosition(first, Frames.Frame.ICRF) - FinalPosition(second, Frames.Frame.ICRF)).Magnitude();
        _output.WriteLine($"MEASURE RK7(8) 1e-10 in SSB mode: {steps} steps, {stepsNudged} one ulp looser, final positions {apart:E3} m apart");
        Assert.True(steps <= Rk78StepBudget, $"{steps} steps");
        Assert.True(System.Math.Abs(steps - stepsNudged) <= NudgedStepDifference, $"{steps} and {stepsNudged} steps");
        Assert.True(apart < NudgedApart, $"{apart:E3} m");
    }

    /// <summary>Twice the worst count with the fix, 49 on Linux x64 and Windows x64 (46 on macOS arm64; CI run
    /// 44225490669).</summary>
    private const int Rk78StepBudget = 98;

    /// <summary>Twice the worst difference of the draws one ulp apart on Linux x64, 20 steps (the start moved by one
    /// ulp; 16 for the tolerance), above the CI's 5 steps on macOS arm64 (46 and 41; 2 on Linux x64 and Windows x64; CI
    /// run 44225490669): the rule's 10 would be crossed by a later redraw of the dynamics without a defect (decided by
    /// S. Guillet, 2026-11-18).</summary>
    private const int NudgedStepDifference = 40;

    /// <summary>Twice the worst distance with the fix, 5.632e-3 m on Linux x64 and Windows x64 (2.636e-3 m on macOS arm64;
    /// CI run 44225490669), rounded up.</summary>
    private const double NudgedApart = 1.2e-2;

    /// <summary>
    /// RK7(8) at 1e-12 in SSB mode takes the steps of its dynamics (#389, #544: it did not finish in minutes in the attitude
    /// tests, hence their tolerance of 1e-9 in SSB mode); measured on a85098d (Linux x64, Release, 2026-11-17): 2 093 091
    /// steps for the hour; with the fix 5 210 on Linux x64, 5 216 on Windows x64 and 5 172 on macOS arm64 (CI run
    /// 44225490669).
    /// </summary>
    [Fact]
    public void Rk78_AtOneEMinus12_Finishes()
    {
        // Act
        var solution = NewSpacecraft(SsbState(Frames.Frame.ICRF), -5771)
            .Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = Output, ThirdBodies = Bodies(), CovariancePropagation = CovariancePropagationOptions.None });

        // Assert
        int steps = Steps(solution);
        _output.WriteLine($"MEASURE RK7(8) 1e-12 in SSB mode: {steps} steps");
        Assert.True(steps <= Rk78StepBudgetAt1e12, $"{steps} steps");
    }

    /// <summary>Twice the worst count with the fix, 5 216 on Windows x64.</summary>
    private const int Rk78StepBudgetAt1e12 = 10432;

    /// <summary>
    /// The same state given in ECLIPJ2000 and in GALACTIC ends where its ICRF twin ends, to the integration error:
    /// 0.07 to 0.56 m apart after 1 h before the fix (#544), the noise of the Earth's attraction depending on the rounding
    /// of the coordinates.
    /// </summary>
    [Theory]
    [InlineData("ECLIPJ2000", "VV")]
    [InlineData("GALACTIC", "VV")]
    [InlineData("ECLIPJ2000", "RK")]
    [InlineData("GALACTIC", "RK")]
    public void FrameTwins_EndTogether(string frameName, string integrator)
    {
        // Arrange
        var frame = frameName == "ECLIPJ2000" ? Frames.Frame.ECLIPJ2000 : Frames.Frame.GALACTIC;
        int id = frameName == "ECLIPJ2000" ? -5763 : -5765;
        if (integrator == "RK") id -= 10;

        PropagationSolution Propagate(Frames.Frame f, int naifId) => integrator == "VV"
            ? NewSpacecraft(SsbState(f), naifId).Propagate(Window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Output }, OutputStep = Output, ThirdBodies = Bodies() })
            : NewSpacecraft(SsbState(f), naifId).Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 }, OutputStep = Output, ThirdBodies = Bodies(), CovariancePropagation = CovariancePropagationOptions.None });

        // Act
        var twin = Propagate(frame, id);
        var reference = Propagate(Frames.Frame.ICRF, id - 1);

        // Assert
        double apart = (FinalPosition(twin, frame) - FinalPosition(reference, Frames.Frame.ICRF)).Magnitude();
        _output.WriteLine($"MEASURE {frameName} {integrator}: {apart:E3} m from the ICRF twin after 1 h ({Steps(twin)} and {Steps(reference)} steps)");
        Assert.True(apart < (integrator == "VV" ? TwinApartVelocityVerlet : TwinApartRk78), $"{apart:E3} m");
    }

    /// <summary>Twice the worst with the fix, 6.187e-4 m (GALACTIC) on Linux x64, Windows x64 and macOS arm64 alike (CI run
    /// 44225490669; 1.14 m on a85098d), rounded up.</summary>
    private const double TwinApartVelocityVerlet = 1.3e-3;

    /// <summary>Twice the worst with the fix, 2.029e-2 m on macOS arm64 (ECLIPJ2000, 421 and 46 steps; 1.051e-2 m on
    /// Linux x64 and Windows x64, 405 and 49 steps; CI run 44225490669; 6.0e-2 m on a85098d), rounded up: the integration
    /// error at 1e-10 of two step sequences (the ecliptic twin's z, of geocentric size, tightens the relative error
    /// control).</summary>
    private const double TwinApartRk78 = 4.1e-2;
}
