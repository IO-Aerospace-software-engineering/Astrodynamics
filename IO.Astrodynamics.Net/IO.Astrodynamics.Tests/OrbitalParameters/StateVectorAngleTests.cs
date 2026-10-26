// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The angles of <see cref="StateVector"/> where acos used to lose half the digits (#368): near an apsis, a node, the
/// equator, against SPICE OSCELT, and the round trip state, elements, state.
/// </summary>
public class StateVectorAngleTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    private readonly ITestOutputHelper _output;

    public StateVectorAngleTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<string> Cases => new()
    {
        "HEO 1 µs after perigee", "HEO 100 µs after perigee", "HEO 1 s before perigee", "HEO 1 µs after apogee",
        "LEO 1 µs after the ascending node", "LEO 1 µs before the descending node", "equatorial prograde",
        "near-equatorial prograde, tilt 5e-11", "near-equatorial prograde, tilt 2e-10", "equatorial retrograde",
        "near-equatorial retrograde, tilt 5e-11", "near-equatorial retrograde, tilt 2e-10", "generic"
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Elements_MatchSpice_AndRoundTripToTheState(string name)
    {
        // Arrange
        var state = State(name);

        // Act
        var elements = state.ToKeplerianElements();
        var expected = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        var back = elements.ToStateVector();

        // Assert
        double a = System.Math.Abs(elements.A - expected.A) / expected.A;
        double e = System.Math.Abs(elements.E - expected.E);
        double i = System.Math.Abs(elements.I - expected.I);
        double node = TestHelpers.AngleDifference(elements.RAAN, expected.RAAN);
        double periapsis = TestHelpers.AngleDifference(elements.AOP, expected.AOP);
        double m = TestHelpers.AngleDifference(elements.M, expected.M);
        double position = (back.Position - state.Position).Magnitude() / state.Position.Magnitude();
        double velocity = (back.Velocity - state.Velocity).Magnitude() / state.Velocity.Magnitude();
        _output.WriteLine($"{name}: against SPICE, a {a:E2} (relative), e {e:E2}, i {i:E2}, Ω {node:E2}, ω {periapsis:E2}, " +
                          $"M {m:E2} rad; round trip, position {position:E2}, velocity {velocity:E2} (relative)");
        // Measured (Linux, .NET 10, 2026-10-11): 3e-15 at most against SPICE, 8e-16 at most for the round trip, except
        // within the equatorial tolerance, where the tilt is dropped as SPICE does, so the out-of-plane velocity, at
        // most EquatorialTolerance of the speed (5e-11 measured at a tilt of 5e-11), does not come back. On 2026-10-25
        // (#453) the round trip of the HEO 1 µs after apogee is 2.3e-15 in velocity (8.3e-16 elsewhere): the Kepler
        // solver returns the double nearest the root, 2.2e-16 below it, and TrueAnomaly() adds fl(2π), 2.45e-16 short
        // of 2π, to the negative ν; before #453 the solver was 2.3e-16 above the root and the two cancelled (6e-18 of
        // ν), now they add (4.4e-16 of ν, 1/(1 − e) on the velocity at apogee)
        bool droppedTilt = expected.RAAN == 0.0 && (expected.I == 0.0 || expected.I == System.Math.PI) &&
                           state.SpecificAngularMomentum().X != 0.0;
        double roundTrip = droppedTilt ? StateVector.EquatorialTolerance : 1e-14;
        Assert.True(a < 1e-14, $"a: {a:E2}");
        Assert.True(e < 1e-14, $"e: {e:E2}");
        Assert.True(i < 1e-14, $"i: {i:E2}");
        Assert.True(node < 1e-14, $"Ω: {node:E2}");
        Assert.True(periapsis < 1e-14, $"ω: {periapsis:E2}");
        Assert.True(m < 1e-14, $"M: {m:E2}");
        Assert.True(position < 1e-14, $"position: {position:E2}");
        Assert.True(velocity < roundTrip, $"velocity: {velocity:E2}");
    }

    [Fact]
    public void ArgumentOfPeriapsisOfAnEquatorialOrbit_CoversTheWholeCircle()
    {
        // Before #368, ω of an equatorial orbit was reflected on the sign of e_z, which is zero: ω stayed in [0, π],
        // and the state at another epoch was wrong by up to the orbit diameter (3 609 km in a conjunction test)
        var earth = TestHelpers.EarthAtJ2000;
        for (double periapsis = 0.25; periapsis < IO.Astrodynamics.Constants._2PI; periapsis += 0.5)
        {
            var elements = new KeplerianElements(7000000.0, 0.1, 0.0, 0.0, periapsis, 1.0, earth, TimeSystem.Time.J2000TDB,
                Frames.Frame.ICRF);
            var state = elements.ToStateVector();

            double error = TestHelpers.AngleDifference(state.ArgumentOfPeriapsis(), periapsis);
            double spice = TestHelpers.AngleDifference(SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state).AOP,
                periapsis);
            Assert.True(error < 1e-13, $"ω = {periapsis}: {error:E2}");
            Assert.True(spice < 1e-13, $"SPICE, ω = {periapsis}: {spice:E2}");
        }
    }

    private static StateVector State(string name)
    {
        var earth = TestHelpers.EarthAtJ2000;
        var epoch = TimeSystem.Time.J2000TDB;
        var frame = Frames.Frame.ICRF;
        return name switch
        {
            "HEO 1 µs after perigee" => AfterPeriapsis(Heo(), 1e-6),
            "HEO 100 µs after perigee" => AfterPeriapsis(Heo(), 1e-4),
            "HEO 1 s before perigee" => AfterPeriapsis(Heo(), -1.0),
            "HEO 1 µs after apogee" => AfterPeriapsis(Heo(), 0.5 * Heo().Period().TotalSeconds + 1e-6),
            "LEO 1 µs after the ascending node" => AfterAngle(Leo(), 0.0, 1e-6),
            "LEO 1 µs before the descending node" => AfterAngle(Leo(), System.Math.PI, -1e-6),
            "equatorial prograde" => Tilted(1.0, 0.0),
            "near-equatorial prograde, tilt 5e-11" => Tilted(1.0, 5e-11),
            "near-equatorial prograde, tilt 2e-10" => Tilted(1.0, 2e-10),
            "equatorial retrograde" => Tilted(-1.0, 0.0),
            "near-equatorial retrograde, tilt 5e-11" => Tilted(-1.0, 5e-11),
            "near-equatorial retrograde, tilt 2e-10" => Tilted(-1.0, 2e-10),
            "generic" => new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(200.0, 8000.0, -300.0), earth,
                epoch, frame),
            _ => throw new System.ArgumentOutOfRangeException(nameof(name))
        };

        // 300 × 36 000 km at 63.4°, argument of perigee 270°, from its perigee
        KeplerianElements Heo() => new(0.5 * (6678137.0 + 42378137.0), (42378137.0 - 6678137.0) / (42378137.0 + 6678137.0),
            63.4 * Deg, 20.0 * Deg, 270.0 * Deg, 0.0, earth, epoch, frame);

        // 400 × 1000 km at 51.6°, node at 30°, argument of perigee 40°
        KeplerianElements Leo() => new(0.5 * (6778137.0 + 7378137.0), (7378137.0 - 6778137.0) / (7378137.0 + 6778137.0),
            51.6 * Deg, 30.0 * Deg, 40.0 * Deg, 0.0, earth, epoch, frame);

        // dt seconds after the periapsis of the elements, by the closed form of the tests
        StateVector AfterPeriapsis(KeplerianElements elements, double dt)
        {
            var periapsis = elements.ToStateVector();
            var (x, _) = KeplerianStm.Propagate(earth.GM, periapsis.Position, periapsis.Velocity, dt);
            return new StateVector(new Vector3(x[0], x[1], x[2]), new Vector3(x[3], x[4], x[5]), earth, epoch, frame);
        }

        // dt seconds after the argument of latitude u, by the closed form of the tests
        StateVector AfterAngle(KeplerianElements elements, double u, double dt)
        {
            double trueAnomaly = u - elements.AOP;
            var atAngle = elements.ToStateVector(trueAnomaly < 0.0 ? trueAnomaly + IO.Astrodynamics.Constants._2PI : trueAnomaly);
            var (x, _) = KeplerianStm.Propagate(earth.GM, atAngle.Position, atAngle.Velocity, dt);
            return new StateVector(new Vector3(x[0], x[1], x[2]), new Vector3(x[3], x[4], x[5]), earth, epoch, frame);
        }

        // In the equatorial plane at 30°, slightly eccentric, the velocity tilted out of the plane by tilt
        StateVector Tilted(double sense, double tilt)
        {
            double angle = 30.0 * Deg;
            var position = new Vector3(6.8e6 * System.Math.Cos(angle), 6.8e6 * System.Math.Sin(angle), 0.0);
            var along = new Vector3(-System.Math.Sin(angle), System.Math.Cos(angle), 0.0) * sense;
            var velocity = along * (7900.0 * System.Math.Cos(tilt)) + new Vector3(0.0, 0.0, 7900.0 * System.Math.Sin(tilt)) +
                           position.Normalize() * 50.0;
            return new StateVector(position, velocity, earth, epoch, frame);
        }
    }
}
