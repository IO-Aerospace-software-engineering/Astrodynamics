// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The elements of a near-circular state vector, inclined or not, and the round trip through
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.AtEpoch"/> at an unchanged epoch (#398).
/// </summary>
/// <remarks>
/// Before #398, a circular orbit (e &lt; 1e-3) inclined by less than 1e-3 rad counted its true anomaly from the x axis,
/// which lies in the orbit plane only if the orbit is equatorial or its node is on it: the R4 GEO state with z moved by
/// 1 km came back 0.38 m away, and the derivative of the round trip was 2.6 away from the identity. The expected values
/// are independent of the library: the state itself for the round trip, SPICE OSCELT for the elements.
/// </remarks>
public class NearCircularRoundTripTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;
    private const double GeoSemiMajorAxis = 42164137.0;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public NearCircularRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
    }

    // Eccentricities and inclinations on both sides of the thresholds of the circular orbit (1e-3) and of the former
    // branch (1e-3 rad), down to 5e-10 rad, and retrograde orbits within 1e-3 rad of π, which never took the former
    // branch; inside the equatorial tolerance (1e-10 rad) of #368 excluded: there the tilt is dropped
    public static TheoryData<double, double> Grid
    {
        get
        {
            var data = new TheoryData<double, double>();
            foreach (double e in new[] { 1e-7, 2e-4, 9.99e-4, 1.001e-3 })
            {
                foreach (double i in new[]
                         {
                             5e-10, 1e-9, 1e-6, 1e-4, 0.05 * Deg, 9.99e-4, 1.001e-3, 3e-2, System.Math.PI - 9.99e-4,
                             System.Math.PI - 1e-6
                         })
                {
                    data.Add(e, i);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void NearCircularState_MeanLongitudeMatchesSpice_AndAtEpochReturnsTheState(double eccentricity,
        double inclination)
    {
        double worstLongitude = 0.0, worstPosition = 0.0, worstVelocity = 0.0;
        foreach (double node in new[] { 0.3, 2.0, 5.5 })
        {
            // Arrange
            var state = new KeplerianElements(GeoSemiMajorAxis, eccentricity, inclination, node, 1.0, 0.7, _earth, Epoch,
                Frames.Frame.ICRF).ToStateVector();

            // Act
            var expected = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
            var back = state.AtEpoch(state.Epoch).ToStateVector();

            // Assert: Ω, ω and M are each ill-conditioned near e = 0 and i = 0; their sum, the mean longitude, is not
            worstLongitude = System.Math.Max(worstLongitude, TestHelpers.AngleDifference(
                state.AscendingNode() + state.ArgumentOfPeriapsis() + state.MeanAnomaly(),
                expected.RAAN + expected.AOP + expected.M));
            worstPosition = System.Math.Max(worstPosition, Relative(back.Position, state.Position));
            worstVelocity = System.Math.Max(worstVelocity, Relative(back.Velocity, state.Velocity));
        }

        _output.WriteLine($"e {eccentricity:E3}, i {inclination:E3}: mean longitude against SPICE {worstLongitude:E2} rad; " +
                          $"round trip, position {worstPosition:E2}, velocity {worstVelocity:E2} (relative)");
        // Measured (Linux, .NET 10, 2026-10-17): mean longitude 8.9e-16 rad, round trip 7.9e-16 at most; before #398,
        // every cell with e < 1e-3 and 1e-6 <= i < 1e-3 failed. The 1e-14 of the round trip of #368
        Assert.True(worstLongitude < 1e-14, $"mean longitude: {worstLongitude:E2}");
        Assert.True(worstPosition < 1e-14, $"position: {worstPosition:E2}");
        Assert.True(worstVelocity < 1e-14, $"velocity: {worstVelocity:E2}");
    }

    public static TheoryData<string, int, double> Perturbations => new()
    {
        { "R4", 0, 0.0 }, { "R4, z + 10 m", 2, 10.0 }, { "R4, v_z + 1 cm/s", 5, 0.01 }, { "R4, z + 1 km", 2, 1000.0 },
        { "R4, x + 10 m", 0, 10.0 }, { "R4, v_x + 1 cm/s", 3, 0.01 }, { "R4, y - 10 m", 1, -10.0 }
    };

    [Theory]
    [MemberData(nameof(Perturbations))]
    public void PerturbedGeoState_AtEpochReturnsTheState(string name, int component, double delta)
    {
        // Arrange: the states of #398
        var state = Perturbed(R4(), component, delta);

        // Act
        var back = state.AtEpoch(state.Epoch).ToStateVector();

        // Assert
        double position = (back.Position - state.Position).Magnitude();
        double velocity = (back.Velocity - state.Velocity).Magnitude();
        _output.WriteLine($"{name}: position {position:E2} m, velocity {velocity:E2} m/s");
        // Measured (Linux, .NET 10, 2026-10-17): 5.8e-8 m at most (1.4e-15 of r), against 3.8 mm (z + 10 m), 3.0 cm
        // (v_z + 1 cm/s) and 0.38 m (z + 1 km) before #398
        Assert.True(position / state.Position.Magnitude() < 1e-14, $"{name}, position: {position:E2} m");
        Assert.True(velocity / state.Velocity.Magnitude() < 1e-14, $"{name}, velocity: {velocity:E2} m/s");
    }

    [Fact]
    public void AtEpochRoundTripOfTheGeoState_HasTheIdentityForDerivative()
    {
        // Arrange: ∂x0'/∂x0 by central differences of the round trip, at the steps of the F1 references of R4
        var state = R4();
        var steps = new[] { 10.0, 10.0, 10.0, 1e-2, 1e-2, 1e-2 };

        // Act
        double worst = 0.0;
        var where = (0, 0);
        for (int column = 0; column < 6; column++)
        {
            var plus = RoundTrip(Perturbed(state, column, steps[column])).ToArray();
            var minus = RoundTrip(Perturbed(state, column, -steps[column])).ToArray();
            for (int row = 0; row < 6; row++)
            {
                double departure = System.Math.Abs((plus[row] - minus[row]) / (2.0 * steps[column]) -
                                                   (row == column ? 1.0 : 0.0));
                if (departure > worst)
                {
                    worst = departure;
                    where = (row, column);
                }
            }
        }

        // Assert
        _output.WriteLine($"largest |∂x0'/∂x0 - I|: {worst:E2} (row {where.Item1}, column {where.Item2})");
        // Measured (Linux, .NET 10, 2026-10-17): 2.6e-6 (y against v_x), the rounding of the round trip over the step,
        // smaller at larger steps; 2.6 before #398 (y against v_z), whatever the step
        Assert.True(worst < 1e-5, $"largest |∂x0'/∂x0 - I|: {worst:E2} at row {where.Item1}, column {where.Item2}");

        static StateVector RoundTrip(StateVector s) => s.AtEpoch(s.Epoch).ToStateVector();
    }

    // The GEO state of R4: a = 42 164 137 m, e = 2e-4, i = 0.05°, Ω = 0, ω = 30°, M = 0
    private StateVector R4()
    {
        return new KeplerianElements(GeoSemiMajorAxis, 0.0002, 0.05 * Deg, 0.0, 30.0 * Deg, 0.0, _earth, Epoch,
            Frames.Frame.ICRF).ToStateVector();
    }

    private static StateVector Perturbed(StateVector state, int component, double delta)
    {
        var x = state.ToArray();
        x[component] += delta;
        return new StateVector(new Vector3(x[0], x[1], x[2]), new Vector3(x[3], x[4], x[5]), state.Observer, state.Epoch,
            state.Frame);
    }

    private static double Relative(Vector3 actual, Vector3 expected)
    {
        return (actual - expected).Magnitude() / expected.Magnitude();
    }
}
