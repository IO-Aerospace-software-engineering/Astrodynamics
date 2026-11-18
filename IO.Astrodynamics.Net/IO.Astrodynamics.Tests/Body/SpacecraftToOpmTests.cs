// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// <c>Spacecraft.ToOpm</c> after a propagation writes the state, the mass and the maneuvers of one instant (#658, decided by
/// S. Guillet, 2026-11-17): the state read from the propagated states at the epoch, relative to the observer and in the frame
/// of the initial orbital parameters, the mass and the impulses ignited by then (the start of their thrust window, the
/// <c>MAN_EPOCH_IGNITION</c> written). Before, it wrote the two-body
/// (or SGP4) motion of the initial orbit with the mass and the maneuvers after the propagation, 1 212 km and 1 137 m/s off
/// one burn later. The case of the issue: a 400 × 1 000 km LEO from its apogee about a point-mass Earth, an
/// <see cref="ApogeeHeightManeuver"/> to ra + 500 km, 1 000 kg dry and 900 kg of fuel, Isp 450 s, 6 000 s at 10 s outputs.
/// The references: the state of <c>GetEphemeris</c> at the epoch, in km (the OPM's units, the same division), and against
/// the solution of the propagation; the mass by Tsiolkovsky's equation on the burn's ΔV; the burn by its own windows.
/// </summary>
public class SpacecraftToOpmTests
{
    private static readonly Time S = new(2024, 1, 15, 12, 0, 0, frame: TimeFrame.TDBFrame);
    private const double Dry = 1000.0, Fuel = 900.0, Isp = 450.0;

    /// <summary>The state of the OPM against the solution of the propagation, in the frame of the initial orbit, m: the read
    /// of the stored states against the solution (RK7(8): Lagrange of the outputs against its dense output; Velocity-Verlet:
    /// against its cubic Hermite, at the output epochs and the impulse), 0 at an output. Worst 3.840e-9 m on Linux x64 and
    /// Windows x64 (RK7(8), ICRF, before the ignition), 3.799e-9 m on macOS arm64 (RK7(8), ECLIPJ2000, after the ignition;
    /// CI run 44225490669). Twice the worst,
    /// rounded up at the second significant digit.</summary>
    internal const double StateAgainstSolution = 7.7e-9;

    private readonly ITestOutputHelper _output;

    public SpacecraftToOpmTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static (Spacecraft Spacecraft, ApogeeHeightManeuver Burn, PropagationSolution Solution) Propagated(int naifId, string integrator, string frame)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        double ra = 6378137.0 + 1000000.0, rp = 6378137.0 + 400000.0, a = (ra + rp) / 2.0;
        var orbit = new KeplerianElements(a, (ra - rp) / (ra + rp), 0.9, 0.3, 1.1, System.Math.PI, earth, S, Frame.ICRF).ToStateVector();
        var initial = frame == "ICRF" ? orbit : orbit.ToFrame(Frame.ECLIPTIC_J2000).ToStateVector();
        var spacecraft = new Spacecraft(naifId, $"OPM{-naifId}", Dry, 5000.0, new Clock($"OPM{-naifId}_CLK", 65536), initial, cosparId: "2024-001A");
        var tank = new FuelTank("ft", "model", "sn", Fuel, Fuel);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", Isp, 10.0, tank);
        spacecraft.AddEngine(engine);
        var burn = new ApogeeHeightManeuver(earth, S, TimeSpan.Zero, ra + 500000.0, engine);
        spacecraft.SetStandbyManeuver(burn);
        Integrator instance = integrator == "RK" ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(TimeSpan.FromSeconds(1.0));
        var solution = spacecraft.Propagate(new Window(S, S.AddSeconds(6000.0)), [earth], instance, false, false, TimeSpan.FromSeconds(10.0));
        return (spacecraft, burn, solution);
    }

    /// <summary>
    /// Before the burn (S + 1 000 s), one tick before its ignition, at its ignition, 1 s after it (before the impulse, 2.6 s
    /// after the ignition, the centroid of the burn's acceleration, #335; 3.4 s before 11.0.0), at the impulse, after it (S + 5 000 s), at the end and at the default epoch (the initial one): the
    /// state is <c>GetEphemeris</c>'s at the epoch, in the frame of the initial orbit, the mass Tsiolkovsky's after the burn
    /// when the burn is ignited by then and the full mass before, and the maneuvers the burn when it is ignited by then, none
    /// before (#658, decided by S. Guillet, 2026-11-17: "the mass and the maneuvers ignited by then"; the branch counted the
    /// burn from its impulse at first, so the epochs between the ignition and the impulse were red). Both integrators, from
    /// ICRF and from ECLIPJ2000. An epoch outside the propagated states throws.
    /// </summary>
    [Theory]
    [InlineData("RK", "ICRF", -6581)]
    [InlineData("VV", "ICRF", -6582)]
    [InlineData("RK", "ECLIPJ2000", -6583)]
    [InlineData("VV", "ECLIPJ2000", -6584)]
    public void Propagated_WritesTheStateMassAndManeuversOfTheEpoch(string integrator, string frame, int naifId)
    {
        // Arrange
        var (spacecraft, burn, solution) = Propagated(naifId, integrator, frame);
        // The epoch of the burn: the base of the segment after it
        Assert.Equal(2, solution.Segments.Count);
        var tc = solution.Segments[1].BaseEpoch;
        Assert.InRange((tc - S).TotalSeconds, 1000.0, 5000.0);
        var initialFrame = spacecraft.InitialOrbitalParameters.Frame;
        var earth = (CelestialItem)spacecraft.InitialOrbitalParameters.Observer;
        double massAfter = (Dry + Fuel) * System.Math.Exp(-burn.DeltaV.Magnitude() / (Isp * IO.Astrodynamics.Constants.g0));
        var ignition = burn.ThrustWindow!.Value.StartDate;
        Assert.True(ignition.AddSeconds(1.0) < tc);

        // The epochs off the output grid (about the ignition) are checked against the solution in RK7(8) only: there the
        // cubic Hermite of Velocity-Verlet departs from the Lagrange read of its outputs by up to 3.1e-4 m anywhere in the
        // window (1.4e-4 m at S + 2 000.25 s, far from the burn; measured on Linux x64), its own interpolation, not ToOpm's
        foreach (var (epoch, label, onGrid) in new (Time?, string, bool)[]
                 {
                     (null, "default", true), (S.AddSeconds(1000.0), "before", true), (ignition.AddSeconds(-1e-7), "a tick before the ignition", false),
                     (ignition, "at the ignition", false), (ignition.AddSeconds(1.0), "1 s after the ignition", false), (tc, "at the impulse", true),
                     (S.AddSeconds(5000.0), "after", true), (S.AddSeconds(6000.0), "end", true)
                 })
        {
            // Act
            var opm = spacecraft.ToOpm(epoch: epoch);

            // Assert: the state of GetEphemeris at the epoch, in km
            var t = epoch ?? spacecraft.InitialOrbitalParameters.Epoch;
            var read = spacecraft.GetEphemeris(t, earth, initialFrame, Aberration.None).ToStateVector();
            var sv = opm.Data.StateVector;
            Assert.Equal(read.Position.X / 1000.0, sv.X);
            Assert.Equal(read.Position.Y / 1000.0, sv.Y);
            Assert.Equal(read.Position.Z / 1000.0, sv.Z);
            Assert.Equal(read.Velocity.X / 1000.0, sv.XDot);
            Assert.Equal(read.Velocity.Y / 1000.0, sv.YDot);
            Assert.Equal(read.Velocity.Z / 1000.0, sv.ZDot);
            // The CCSDS name of the frame (#339): ICRF for Frame.ICRF (SPICE J2000), ECLIPJ2000 kept
            Assert.Equal(frame, opm.Metadata.ReferenceFrame);
            Assert.All(opm.Data.Maneuvers ?? [], m => Assert.Equal(frame, m.ManRefFrame));

            // Against the solution, in the frame of the initial orbit, which the integration keeps (#529)
            var (r, _) = solution.InterpolateAt(t);
            double dr = (new Vector3(sv.X, sv.Y, sv.Z) * 1000.0 - r).Magnitude();

            // The mass and the maneuvers of the epoch
            bool executed = t >= ignition;
            double expectedMass = executed ? massAfter : Dry + Fuel;
            int maneuvers = opm.Data.Maneuvers?.Count() ?? 0;
            _output.WriteLine($"MEASURE #658 {integrator} {frame} {label} (S + {(t - S).TotalSeconds:R} s, burn at S + {(tc - S).TotalSeconds:R} s): |r - solution| {dr:E3} m, " +
                              $"mass {opm.Data.Mass:R} kg against {expectedMass:R} kg, {maneuvers} maneuvers, ignition at S + {(ignition - S).TotalSeconds:R} s");
            if (onGrid || integrator == "RK")
            {
                Assert.True(dr <= StateAgainstSolution, $"{label}: {dr:E3} m from the solution");
            }

            Assert.True(System.Math.Abs(opm.Data.Mass!.Value - expectedMass) <= 1e-9 * expectedMass, $"{label}: mass {opm.Data.Mass} against {expectedMass}");
            Assert.Equal(executed ? 1 : 0, maneuvers);
            if (executed)
            {
                Assert.Equal(burn.ThrustWindow!.Value.StartDate.ToUTC().DateTime, opm.Data.Maneuvers.Single().ManEpochIgnition);
            }
        }

        // Outside the propagated states
        Assert.Throws<InvalidOperationException>(() => spacecraft.ToOpm(epoch: S.AddSeconds(-60.0)));
        Assert.Throws<InvalidOperationException>(() => spacecraft.ToOpm(epoch: S.AddSeconds(6060.0)));
    }

    /// <summary>
    /// A TLE spacecraft propagated numerically writes its propagated state in TEME, the frame of its TLE, not SGP4 (53.7 km
    /// from it after 1 h for the ISS); one only sampled by <see cref="TLEPropagator"/> writes SGP4, as before (the control).
    /// </summary>
    [Fact]
    public void TleSpacecraft_PropagatedWritesItsStates_SampledWritesSgp4()
    {
        var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        var earth = (CelestialItem)tle.Observer;
        var end = window.EndDate;
        var sgp4 = tle.ToStateVector(end);

        var propagated = new Spacecraft(-6585, "OPM6585", 1000.0, 3000.0, new Clock("OPM6585_CLK", 65536), tle, cosparId: "1998-067A");
        propagated.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(60.0));
        var opm = propagated.ToOpm(epoch: end);
        var read = propagated.GetEphemeris(end, earth, Frame.TEME, Aberration.None).ToStateVector();
        var written = new Vector3(opm.Data.StateVector.X, opm.Data.StateVector.Y, opm.Data.StateVector.Z) * 1000.0;
        double fromSgp4 = (written - sgp4.Position).Magnitude();
        _output.WriteLine($"MEASURE #658 TLE propagated: the OPM state {fromSgp4:E3} m from SGP4 at the end of 1 h");
        Assert.Equal("TEME", opm.Metadata.ReferenceFrame);
        Assert.Equal(read.Position.X / 1000.0, opm.Data.StateVector.X);
        Assert.Equal(read.Velocity.Z / 1000.0, opm.Data.StateVector.ZDot);
        Assert.True(fromSgp4 > 1000.0, $"{fromSgp4:E3} m");

        var sampled = new Spacecraft(-6586, "OPM6586", 1000.0, 3000.0, new Clock("OPM6586_CLK", 65536), tle, cosparId: "1998-067A");
        new TLEPropagator(window, sampled, TimeSpan.FromSeconds(60.0)).Propagate();
        var sampledOpm = sampled.ToOpm(epoch: end);
        Assert.Equal(sgp4.Position.X / 1000.0, sampledOpm.Data.StateVector.X);
        Assert.Equal(sgp4.Velocity.Z / 1000.0, sampledOpm.Data.StateVector.ZDot);
    }
}
