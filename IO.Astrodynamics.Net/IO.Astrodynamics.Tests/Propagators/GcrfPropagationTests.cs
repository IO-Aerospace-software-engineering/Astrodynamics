// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A propagation from a GCRF state (#543). The library's GCRF has the axes of ICRF (SPICE <c>J2000</c>): the rotation
/// between the two is the identity, with no frame bias (<see cref="GcrfFrame"/>), the IAU definition of the GCRS axes as
/// those of the ICRS (IAU 2000 Resolution B1.3), with the convention, as SPICE's, that J2000 is aligned with the ICRF. A
/// propagation from a GCRF state is then, bit for bit, that of its ICRF twin: same numbers, the forces read in the same
/// axes, the same step sequence of RK7(8). Before the fix the propagator rejected GCRF ("must be defined in inertial
/// frame"), and a SPICE ephemeris read in GCRF handed spkezr a frame it does not know (NaN).
/// </summary>
public class GcrfPropagationTests
{
    // The state of the issue: 2024-03-01 12:00 TDB, (6 778 137 m, 0, 0), (0, 7 668.6 m/s, 0) about the Earth, 600 s
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

    private static readonly Window Window = new(Start, Start.AddSeconds(600.0));

    public GcrfPropagationTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
        new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

    private static CelestialItem[] Bodies(CelestialBody earth) =>
        new CelestialItem[] { earth, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start), new CelestialBody(Stars.Sun) };

    private static StateVector State(ILocalizable observer, Frames.Frame frame, Matrix? covariance = null) =>
        new(new Vector3(6778137.0, 0.0, 0.0), new Vector3(0.0, 7668.6, 0.0), observer, Start, frame, covariance);

    private static void AssertSameState(StateVector expected, StateVector actual)
    {
        Assert.Equal(expected.Epoch, actual.Epoch);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Velocity, actual.Velocity);
    }

    [Fact]
    public void GcrfToIcrf_IsTheIdentity()
    {
        // The rotation the library uses between GCRF and ICRF, both ways: the identity, no angular velocity
        foreach (var epoch in new[] { TimeSystem.Time.J2000TDB, Start, new TimeSystem.Time(2050, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame) })
        {
            var toIcrf = Frames.Frame.GCRF.ToFrame(Frames.Frame.ICRF, epoch);
            var fromIcrf = Frames.Frame.ICRF.ToFrame(Frames.Frame.GCRF, epoch);
            Assert.Equal(Quaternion.Identity, toIcrf.Rotation);
            Assert.Equal(Vector3.Zero, toIcrf.AngularVelocity);
            Assert.Equal(Quaternion.Identity, fromIcrf.Rotation);
            Assert.Equal(Vector3.Zero, fromIcrf.AngularVelocity);
        }
    }

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.CNS)]
    public void SpiceEphemeris_InGcrf_IsTheIcrfState(Aberration aberration)
    {
        // The SPICE path of an ephemeris read: the Moon and the Sun from the Earth, labelled GCRF, in Frame.GCRF and in a
        // frame built from the name (the CLI), which is not a GcrfFrame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        foreach (var target in new CelestialItem[] { new CelestialBody(PlanetsAndMoons.MOON), new CelestialBody(Stars.Sun) })
        {
            var icrf = target.GetEphemeris(Start, earth, Frames.Frame.ICRF, aberration).ToStateVector();
            Assert.True(icrf.Position.Magnitude() > 3.0e8);
            foreach (var frame in new[] { Frames.Frame.GCRF, new Frames.Frame("GCRF") })
            {
                var gcrf = target.GetEphemeris(Start, earth, frame, aberration).ToStateVector();
                Assert.Same(frame, gcrf.Frame);
                AssertSameState(icrf, gcrf);
            }
        }
    }

    [Theory]
    [InlineData("VV")]
    [InlineData("RK78")]
    [InlineData("VV-Impulse-Nadir")]
    [InlineData("VV-SSB")]
    public void Propagation_FromGcrf_IsThePropagationOfItsIcrfTwin_BitForBit(string variant)
    {
        (PropagationSolution Solution, Spacecraft Spacecraft) Run(Frames.Frame frame, int id)
        {
            var earth = Earth();
            var initial = State(earth, frame);
            if (variant.EndsWith("SSB"))
            {
                // SSB mode: the same orbit relative to the solar-system barycentre
                var earthFromSsb = earth.GetGeometricStateRelativeTo(Start, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                initial = new StateVector(earthFromSsb.Position + initial.Position, earthFromSsb.Velocity + initial.Velocity,
                    Barycenters.SOLAR_SYSTEM_BARYCENTER, Start, frame);
            }

            if (variant.Contains("Impulse"))
            {
                // An eccentric orbit about 180 s before its perigee, where the apogee-raising impulse fires
                initial = new StateVector(initial.Position, new Vector3(-50.0, 7790.0, 0.0), earth, Start, frame);
            }

            var spacecraft = new Spacecraft(id, $"GCRF{-id}", 1000.0, 3000.0, new Clock($"gcrf{-id}", 65536), initial, 10.0, 2.2, null, 1.5);
            if (variant.Contains("Impulse"))
            {
                var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
                spacecraft.AddFuelTank(tank);
                var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
                spacecraft.AddEngine(engine);
                var apogee = new ApogeeHeightManeuver(earth, Start.AddSeconds(100.0), TimeSpan.Zero, 7600000.0, engine);
                apogee.SetNextManeuver(new NadirAttitude(earth, Start.AddSeconds(100.0), TimeSpan.Zero, engine));
                spacecraft.SetStandbyManeuver(apogee);
            }

            Integrator integrator = variant == "RK78" ? new RK78Integrator() : new VVIntegrator(TimeSpan.FromSeconds(1.0));
            // In SSB mode drag comes from the listed bodies with an atmospheric model, and this Earth has none: drag off, as
            // the variant ran before #674, where the request was dropped
            var solution = spacecraft.Propagate(Window, Bodies(earth), integrator, !variant.EndsWith("SSB"), true,
                TimeSpan.FromSeconds(60.0));
            return (solution, spacecraft);
        }

        // Act
        var (icrf, icrfSpacecraft) = Run(Frames.Frame.ICRF, -1543);
        var (gcrf, gcrfSpacecraft) = Run(Frames.Frame.GCRF, -1544);
        // #562: a frame built by name, in any case, is Frame.GCRF: the propagator's inertial-frame check (==) accepts it, and
        // the propagation is that of its twin, where it threw "must be defined in inertial frame"
        var byNameFrame = new Frames.Frame("gcrf");
        var (byName, byNameSpacecraft) = Run(byNameFrame, -1545);

        // Assert: the solution in GCRF, equal to its twin; the stored states and the attitude (in ICRF) too
        Assert.Equal(Frames.Frame.GCRF, gcrf.Frame);
        Assert.Equal(icrf.StateVectors.Count, gcrf.StateVectors.Count);
        for (int i = 0; i < icrf.StateVectors.Count; i++)
        {
            // The label itself, not only a frame of its name: equality ignores case and type (#562)
            Assert.Same(Frames.Frame.GCRF, gcrf.StateVectors[i].Frame);
            AssertSameState(icrf.StateVectors[i], gcrf.StateVectors[i]);
        }

        Assert.Same(byNameFrame, byName.Frame);
        Assert.Equal(icrf.StateVectors.Count, byName.StateVectors.Count);
        for (int i = 0; i < icrf.StateVectors.Count; i++)
        {
            Assert.Same(byNameFrame, byName.StateVectors[i].Frame);
            AssertSameState(icrf.StateVectors[i], byName.StateVectors[i]);
        }

        var observer = icrfSpacecraft.InitialOrbitalParameters.Observer;
        foreach (var epoch in new[] { Start, Start.AddSeconds(150.0), Start.AddSeconds(437.5), Window.EndDate })
        {
            var expected = icrfSpacecraft.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            AssertSameState(expected, gcrfSpacecraft.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None).ToStateVector());
            AssertSameState(expected, byNameSpacecraft.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None).ToStateVector());
            Assert.Equal(icrfSpacecraft.Frame.GetStateOrientationToICRF(epoch).Rotation, gcrfSpacecraft.Frame.GetStateOrientationToICRF(epoch).Rotation);
            Assert.Equal(icrfSpacecraft.Frame.GetStateOrientationToICRF(epoch).Rotation, byNameSpacecraft.Frame.GetStateOrientationToICRF(epoch).Rotation);
        }

        if (variant.Contains("Impulse"))
        {
            // The impulse fired and the nadir attitude followed it
            Assert.Single(gcrfSpacecraft.ExecutedManeuvers.OfType<ApogeeHeightManeuver>());
            Assert.NotEqual(Quaternion.Identity, gcrfSpacecraft.Frame.GetStateOrientationToICRF(Window.EndDate).Rotation);
        }
    }

    [Fact]
    public void Covariance_FromGcrf_IsThatOfItsIcrfTwin_BitForBit()
    {
        // RK7(8) with the covariance, through the builder: P0 in GCRF is P0 in ICRF
        var p0 = new Matrix(new double[,]
        {
            { 100.0, 10.0, 0.0, 0.0, 0.0, 0.0 }, { 10.0, 100.0, 0.0, 0.0, 0.0, 0.0 }, { 0.0, 0.0, 100.0, 0.0, 0.0, 0.0 },
            { 0.0, 0.0, 0.0, 1e-4, 0.0, 0.0 }, { 0.0, 0.0, 0.0, 0.0, 1e-4, 0.0 }, { 0.0, 0.0, 0.0, 0.0, 0.0, 1e-4 }
        });
        var options = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };

        PropagationSolution Run(Frames.Frame frame, int id)
        {
            var earth = Earth();
            var spacecraft = new Spacecraft(id, $"GCRFC{-id}", 1000.0, 3000.0, new Clock($"gcrfc{-id}", 65536), State(earth, frame, p0), 10.0);
            return new CentralBodyPropagatorBuilder(Window, spacecraft, new RK78Integrator(), TimeSpan.FromSeconds(60.0))
                .WithPerturbingBodies(Bodies(earth))
                .IncludeAtmosphericDrag()
                .WithCovariancePropagation(options)
                .Build()
                .Propagate();
        }

        // Act
        var icrf = Run(Frames.Frame.ICRF, -1545);
        var gcrf = Run(Frames.Frame.GCRF, -1546);

        // Assert
        foreach (var epoch in new[] { Start.AddSeconds(90.0), Window.EndDate })
        {
            var expected = icrf.GetCovariance(epoch);
            var actual = gcrf.GetCovariance(epoch);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(expected.Get(i, j), actual.Get(i, j));
                }
            }
        }

        AssertSameState(icrf.StateVectors.Last(), gcrf.StateVectors.Last());
    }
}
