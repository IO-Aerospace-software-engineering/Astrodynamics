// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The constructors of the impulses, lot L5b of #668 (D1 rule 8, decisions 61, 65 and 68, decided by S. Guillet,
/// 2026-11-23): the target first, then the optional <c>engine</c>, <c>minEpoch</c>, <c>holdDuration</c> and
/// <c>maneuverCenter</c> (no hold for <see cref="PhasingManeuver"/>, no centre for the target-orbit forms, whose centre is
/// the observer of the target); radii finite and above 0, an apogee at or above the equatorial radius of a celestial body
/// centre (the constructor for a given centre, the run for an inferred one), an inclination in [0, π]; the frame of a
/// target orbit checked by the plan and the run only; and the renamed members <c>TargetApogeeRadius</c>,
/// <c>TargetPerigeeRadius</c>, <c>MinEpoch</c>, <c>HoldDuration</c>.
/// </summary>
public class ImpulseConstructionTests
{
    public ImpulseConstructionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, S);

    private static KeplerianElements Orbit(CelestialBody earth, double a = 7000e3, double e = 0.01) =>
        new(a, e, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF);

    [Fact]
    public void TheTargetAlone_BuildsAnImpulseOfDefaults()
    {
        var maneuvers = new ImpulseManeuver[]
        {
            new ApogeeHeightManeuver(8000e3), new PerigeeHeightManeuver(7100e3), new CombinedManeuver(7100e3, 0.6),
            new PhasingManeuver(1.0, 2u), new PlaneAlignmentManeuver(Orbit(Earth())), new ApsidalAlignmentManeuver(Orbit(Earth())),
            new ApogeeHeightManeuver(Orbit(Earth())), new PerigeeHeightManeuver(Orbit(Earth())), new PhasingManeuver(Orbit(Earth()), 2u)
        };

        foreach (var maneuver in maneuvers)
        {
            Assert.Null(maneuver.Engine);
            Assert.True(maneuver.MinEpoch.EqualsExact(default(Time)), maneuver.GetType().Name);
            Assert.Equal(TimeSpan.Zero, maneuver.HoldDuration);
        }

        // The radius forms infer their centre, the target-orbit forms take the observer of the target
        Assert.Null(maneuvers[0].ManeuverCenter);
        Assert.Null(maneuvers[3].ManeuverCenter);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, maneuvers[4].ManeuverCenter.NaifId);
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, maneuvers[6].ManeuverCenter.NaifId);
    }

    [Fact]
    public void TheParameters_AreInTheOrderOfD1Rule8()
    {
        string[] Names(Type type, Type first) => type.GetConstructors()
            .Single(c => c.GetParameters()[0].ParameterType == first).GetParameters().Select(p => p.Name).ToArray();

        Assert.Equal(["apogeeRadius", "engine", "minEpoch", "holdDuration", "maneuverCenter"], Names(typeof(ApogeeHeightManeuver), typeof(double)));
        Assert.Equal(["targetOrbit", "engine", "minEpoch", "holdDuration"],
            Names(typeof(ApogeeHeightManeuver), typeof(IO.Astrodynamics.OrbitalParameters.OrbitalParameters)));
        Assert.Equal(["perigeeRadius", "engine", "minEpoch", "holdDuration", "maneuverCenter"], Names(typeof(PerigeeHeightManeuver), typeof(double)));
        Assert.Equal(["perigeeRadius", "inclination", "engine", "minEpoch", "holdDuration", "maneuverCenter"], Names(typeof(CombinedManeuver), typeof(double)));
        Assert.Equal(["trueLongitude", "revolutionNumber", "engine", "minEpoch", "maneuverCenter"], Names(typeof(PhasingManeuver), typeof(double)));
        Assert.Equal(["targetOrbit", "revolutionNumber", "engine", "minEpoch"],
            Names(typeof(PhasingManeuver), typeof(IO.Astrodynamics.OrbitalParameters.OrbitalParameters)));
        Assert.Equal(["targetOrbit", "engine", "minEpoch", "holdDuration"],
            Names(typeof(PlaneAlignmentManeuver), typeof(IO.Astrodynamics.OrbitalParameters.OrbitalParameters)));
        Assert.Equal(["targetOrbit", "engine", "minEpoch", "holdDuration"],
            Names(typeof(ApsidalAlignmentManeuver), typeof(IO.Astrodynamics.OrbitalParameters.OrbitalParameters)));
        string[] optional = ["engine", "minEpoch", "holdDuration", "maneuverCenter"];
        foreach (var type in new[] { typeof(ApogeeHeightManeuver), typeof(PerigeeHeightManeuver), typeof(CombinedManeuver), typeof(PhasingManeuver),
                     typeof(PlaneAlignmentManeuver), typeof(ApsidalAlignmentManeuver) })
        {
            Assert.All(type.GetConstructors().SelectMany(c => c.GetParameters()).Where(p => optional.Contains(p.Name)),
                p => Assert.True(p.HasDefaultValue, $"{type.Name}.{p.Name} is optional"));
        }
    }

    [Fact]
    public void TheRenamedMembers()
    {
        Assert.NotNull(typeof(ApogeeHeightManeuver).GetProperty("TargetApogeeRadius"));
        Assert.Null(typeof(ApogeeHeightManeuver).GetProperty("TargetApogee"));
        Assert.NotNull(typeof(PerigeeHeightManeuver).GetProperty("TargetPerigeeRadius"));
        Assert.Null(typeof(PerigeeHeightManeuver).GetProperty("TargetPerigeeHeight"));
        Assert.NotNull(typeof(CombinedManeuver).GetProperty("TargetPerigeeRadius"));
        Assert.Null(typeof(CombinedManeuver).GetProperty("TargetPerigeeHeight"));
        Assert.NotNull(typeof(Maneuver.Maneuver).GetProperty("MinEpoch"));
        Assert.Null(typeof(Maneuver.Maneuver).GetProperty("MinimumEpoch"));
        Assert.NotNull(typeof(Maneuver.Maneuver).GetProperty("HoldDuration"));
        Assert.Null(typeof(Maneuver.Maneuver).GetProperty("ManeuverHoldDuration"));

        Assert.Equal(8000e3, new ApogeeHeightManeuver(8000e3).TargetApogeeRadius);
        Assert.Equal(7100e3, new PerigeeHeightManeuver(7100e3).TargetPerigeeRadius);
        Assert.Equal(7100e3, new CombinedManeuver(7100e3, 0.6).TargetPerigeeRadius);
        var hold = new ApogeeHeightManeuver(8000e3, holdDuration: TimeSpan.FromMinutes(5.0), minEpoch: S);
        Assert.Equal(TimeSpan.FromMinutes(5.0), hold.HoldDuration);
        Assert.True(hold.MinEpoch.EqualsExact(S));
        Assert.Equal("holdDuration",
            Assert.Throws<ArgumentOutOfRangeException>(() => new ApogeeHeightManeuver(8000e3, holdDuration: TimeSpan.FromSeconds(-1.0))).ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ARadiusNotFiniteOrNotAboveZero_Throws(double radius)
    {
        var apogee = Assert.Throws<ArgumentOutOfRangeException>(() => new ApogeeHeightManeuver(radius));
        Assert.Equal("apogeeRadius", apogee.ParamName);
        Assert.Contains("R + h", apogee.Message);
        Assert.Equal("perigeeRadius", Assert.Throws<ArgumentOutOfRangeException>(() => new PerigeeHeightManeuver(radius)).ParamName);
        Assert.Equal("perigeeRadius", Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedManeuver(radius, 0.5)).ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.1)]
    [InlineData(System.Math.PI + 1e-12)]
    [InlineData(10.0)]
    [InlineData(double.PositiveInfinity)]
    public void AnInclinationOutsideZeroToPi_Throws(double inclination)
    {
        Assert.Equal("inclination", Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedManeuver(7100e3, inclination)).ParamName);
    }

    [Fact]
    public void TheInclinationBounds_AreAccepted()
    {
        Assert.Equal(0.0, new CombinedManeuver(7100e3, 0.0).TargetInclination);
        Assert.Equal(System.Math.PI, new CombinedManeuver(7100e3, System.Math.PI).TargetInclination);
    }

    [Fact]
    public void AnApogeeBelowTheSurfaceOfAGivenCentre_Throws_APerigeeThereIsADeorbit()
    {
        var earth = Earth();
        var radius = Assert.Throws<ArgumentException>(() => new ApogeeHeightManeuver(1000e3, maneuverCenter: earth));
        Assert.Equal("apogeeRadius", radius.ParamName);
        Assert.Contains("R + h", radius.Message);
        // The target-orbit form: an apogee radius of 4 400 km about the Earth
        var target = Assert.Throws<ArgumentException>(() => new ApogeeHeightManeuver(Orbit(earth, 4000e3, 0.1)));
        Assert.Equal("targetOrbit", target.ParamName);

        Assert.Equal(earth.EquatorialRadius, new ApogeeHeightManeuver(earth.EquatorialRadius, maneuverCenter: earth).TargetApogeeRadius);
        Assert.Equal(1000e3, new PerigeeHeightManeuver(1000e3, maneuverCenter: earth).TargetPerigeeRadius);
        // A deferred body: its radius is read at the constructor (IC-7)
        Assert.Throws<ArgumentException>(() => new ApogeeHeightManeuver(1000e3, maneuverCenter: PlanetsAndMoons.EARTH_BODY));
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AnApogeeBelowTheSurfaceOfAnInferredCentre_ThrowsAtTheRun(string integrator)
    {
        var earth = Earth();
        var spacecraft = new Spacecraft(-899901 - (integrator == "VV" ? 1 : 0), "ApogeeRule" + integrator, 1000.0, Orbit(earth));
        var tank = spacecraft.AddFuelTank(new FuelTank("tank", 1000.0, 1000.0));
        spacecraft.AddEngine(new Engine("engine", 300.0, 10.0, tank));
        var window = new Window(S, S.AddHours(2.0));
        var options = new PropagationOptions
        {
            Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
            OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None
        };
        var propagator = new CentralBodyPropagator(window, spacecraft, options);
        spacecraft.SetManeuvers(new ApogeeHeightManeuver(1000e3));

        var constructor = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(window, spacecraft, options));
        Assert.Contains("R + h", constructor.Message);
        Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.False(spacecraft.IsPropagated);
    }

    [Fact]
    public void ATargetOrbitAboutASite_Throws()
    {
        var earth = Earth();
        var site = new Site(13, "DSS-13", earth);
        var aboutSite = new StateVector(new Vector3(7000e3, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0), site, S, Frames.Frame.ICRF);

        Assert.Equal("targetOrbit", Assert.Throws<ArgumentException>(() => new PlaneAlignmentManeuver(aboutSite)).ParamName);
        Assert.Equal("targetOrbit", Assert.Throws<ArgumentException>(() => new ApsidalAlignmentManeuver(aboutSite)).ParamName);
        Assert.Equal("targetOrbit", Assert.Throws<ArgumentException>(() => new PhasingManeuver(aboutSite, 1u)).ParamName);
        Assert.Equal("targetOrbit", Assert.Throws<ArgumentException>(() => new PerigeeHeightManeuver(aboutSite)).ParamName);
    }

    [Fact]
    public void TheFrameOfATargetOrbit_IsCheckedByThePlan_NotByTheConstructor()
    {
        var earth = Earth();
        var spacecraft = new Spacecraft(-899903, "FrameOfTarget", 1000.0, Orbit(earth));
        var tank = spacecraft.AddFuelTank(new FuelTank("tank", 1000.0, 1000.0));
        var engine = spacecraft.AddEngine(new Engine("engine", 300.0, 10.0, tank));
        var ecliptic = Orbit(earth).ToStateVector().ToFrame(Frames.Frame.ECLIPJ2000);

        var maneuver = new PlaneAlignmentManeuver(ecliptic, engine);

        Assert.Equal("maneuvers", Assert.Throws<ArgumentException>(() => spacecraft.SetManeuvers(maneuver)).ParamName);
    }
}
