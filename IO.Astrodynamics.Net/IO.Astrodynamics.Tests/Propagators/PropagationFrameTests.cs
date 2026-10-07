// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// A numerical propagation may start from a state in any inertial frame (#357): the forces must then combine vectors
/// of that frame only, and the trajectory must not depend on the frame chosen.
/// </summary>
public class PropagationFrameTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    private readonly ITestOutputHelper _output;

    public PropagationFrameTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string> InertialFrames => new("ECLIPJ2000", "B1950");

    private static Frames.Frame FrameNamed(string name) => name switch
    {
        "ECLIPJ2000" => Frames.Frame.ECLIPTIC_J2000,
        "B1950" => Frames.Frame.B1950,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static StateVector LeoState(CelestialBody earth)
    {
        // 400 km, 51.6 deg, on the day side near the March equinox
        return new KeplerianElements(6778137.0, 0.0005, 51.6 * IO.Astrodynamics.Constants.Deg2Rad,
            20.0 * IO.Astrodynamics.Constants.Deg2Rad, 15.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, earth, Epoch,
            Frames.Frame.ICRF).ToStateVector();
    }

    private static Vector3 ToFrame(in Vector3 icrfVector, CelestialBody earth, Frames.Frame frame)
    {
        // An inertial frame change is a rotation: carry the vector as a position, with no velocity
        return new StateVector(icrfVector, Vector3.Zero, earth, Epoch, Frames.Frame.ICRF).ToFrame(frame)
            .ToStateVector().Position;
    }

    private static double RelativeDifference(in Vector3 actual, in Vector3 expected)
    {
        return (actual - expected).Magnitude() / expected.Magnitude();
    }

    [Theory]
    [MemberData(nameof(InertialFrames))]
    public void ThirdBody_StateInAnotherInertialFrame_GivesTheRotatedIcrfAcceleration(string frameName)
    {
        // Arrange
        var frame = FrameNamed(frameName);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        var icrfState = LeoState(earth);
        var state = icrfState.ToFrame(frame).ToStateVector();
        var window = new Window(Epoch.ToTDB().AddHours(-1.0), Epoch.ToTDB().AddHours(1.0));
        var entries = new (CelestialItem, Aberration)[] { (moon, Aberration.None) };

        foreach (var withCache in new[] { false, true })
        {
            var icrfForce = new ThirdBodyPerturbation(moon, earth);
            var force = new ThirdBodyPerturbation(moon, earth);
            if (withCache)
            {
                icrfForce.EphemerisCache = new PropagationEphemerisCache(window, entries, earth, TimeSpan.FromSeconds(60));
                force.EphemerisCache = new PropagationEphemerisCache(window, entries, earth, frame,
                    TimeSpan.FromSeconds(60));
            }

            // Act
            var expected = ToFrame(icrfForce.Apply(icrfState), earth, frame);
            var actual = force.Apply(state);

            // Assert
            double difference = RelativeDifference(actual, expected);
            _output.WriteLine($"{frameName}, cache {withCache}: relative difference {difference:E2}");
            Assert.True(difference < 1e-12, $"{frameName}, cache {withCache}: relative difference {difference:E2}");
        }
    }

    [Theory]
    [MemberData(nameof(InertialFrames))]
    public void Drag_StateInAnotherInertialFrame_GivesTheRotatedIcrfAcceleration(string frameName)
    {
        // Arrange
        var frame = FrameNamed(frameName);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var icrfState = LeoState(earth);
        var state = icrfState.ToFrame(frame).ToStateVector();
        var spacecraft = new Spacecraft(-1971, "FRAMES", 100.0, 1000.0, new Clock("frames", 65536), icrfState, 10.0);
        var drag = new AtmosphericDrag(spacecraft, earth);

        // Act
        var expected = ToFrame(drag.Apply(icrfState), earth, frame);
        var actual = drag.Apply(state);

        // Assert
        double difference = RelativeDifference(actual, expected);
        _output.WriteLine($"{frameName}: relative difference {difference:E2}");
        Assert.True(difference < 1e-9, $"{frameName}: relative difference {difference:E2}");
    }

    [Theory]
    [MemberData(nameof(InertialFrames))]
    public void Propagation_FromAnotherInertialFrame_MatchesThePropagationFromIcrf(string frameName)
    {
        // Arrange: the case of #357, with drag and SRP added: LEO, EGM2008 10x10, NRLMSISE-00, Moon and Sun, 1 day; the
        // same initial state given in ICRF and in another inertial frame. A fixed step isolates the evaluation of the
        // forces: the error control of RK7(8) scales each component by its own magnitude, which a rotation changes, so
        // its step sequence differs between frames (1 mm after a day in two-body motion, 1.4 cm with drag).
        var frame = FrameNamed(frameName);

        StateVector FinalStateInIcrf(Frames.Frame initialFrame)
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
                new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
                new Nrlmsise00Model(SpaceWeather.Nominal));
            var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
            var sun = new CelestialBody(Stars.Sun);
            var initial = LeoState(earth).ToFrame(initialFrame).ToStateVector();
            var spacecraft = new Spacecraft(-1972, "FRAMES2", 100.0, 1000.0, new Clock("frames2", 65536), initial,
                10.0, 2.2, null, 1.5);
            var solution = spacecraft.Propagate(new Window(Epoch, Epoch.AddDays(1.0)),
                new CelestialItem[] { earth, moon, sun }, new VVIntegrator(TimeSpan.FromSeconds(1.0)), true, true,
                TimeSpan.FromSeconds(600.0));
            var final = solution.StateVectors.Last();
            Assert.Equal(initialFrame, final.Frame);
            return final.ToFrame(Frames.Frame.ICRF).ToStateVector();
        }

        // Act
        var reference = FinalStateInIcrf(Frames.Frame.ICRF);
        var actual = FinalStateInIcrf(frame);

        // Assert
        double positionDifference = (actual.Position - reference.Position).Magnitude();
        double velocityDifference = (actual.Velocity - reference.Velocity).Magnitude();
        _output.WriteLine($"{frameName}: final position difference {positionDifference:E3} m, " +
                          $"velocity difference {velocityDifference:E3} m/s");
        // Rounding only: 3e-5 m measured, against tens of metres before the fix
        Assert.True(positionDifference < 1e-3, $"{frameName}: final position difference {positionDifference:E3} m");
    }
}
