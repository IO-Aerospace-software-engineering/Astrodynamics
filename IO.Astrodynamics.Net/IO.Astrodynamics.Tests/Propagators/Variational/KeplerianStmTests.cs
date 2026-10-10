// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// Checks of the Keplerian reference itself, before F2 uses it: its state against the two-body propagation of the
/// library, and its Φ against the properties and the finite differences of its own closed form.
/// </summary>
public class KeplerianStmTests : IClassFixture<ReferenceCases>
{
    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public KeplerianStmTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<double> TimesOfFlight => new() { 60.0, 3600.0, 21600.0, 86400.0 };

    // Fractions of the two revolutions of R5, from its perigee: just after it, an apogee, a perigee, the end
    public static TheoryData<double> R5Fractions => new() { 0.001, 0.25, 0.5, 1.0 };

    [Theory]
    [MemberData(nameof(TimesOfFlight))]
    public void State_EqualsTheTwoBodyPropagationOfTheLibrary(double dt)
    {
        AssertStateEqualsTheLibrary(_cases.R1, dt, "R1", 1e-12);
    }

    [Theory]
    [MemberData(nameof(R5Fractions))]
    public void R5_State_EqualsTheTwoBodyPropagationOfTheLibrary(double fraction)
    {
        // Measured 9.7e-16 to 1.4e-14 relative (Linux, .NET 10, 2026-10-10): the threshold of R1
        var referenceCase = _cases.TwoBody(_cases.R5);
        AssertStateEqualsTheLibrary(referenceCase, fraction * referenceCase.Duration.TotalSeconds, "R5", 1e-12);
    }

    [Theory]
    [MemberData(nameof(TimesOfFlight))]
    public void Phi_IsSymplecticWithUnitDeterminant(double dt)
    {
        // Arrange
        var start = _cases.R1.InitialState;
        double mu = _cases.R1.GravitationalParameter;

        // Act
        var (_, phi) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, dt);
        var canonical = StmMeasures.Canonical(phi, start.Position.Magnitude(), mu);

        // Assert: Φ is exact to rounding, so the symplectic defect is the rounding of ΦᵀJΦ, of order ε ‖Φ‖² (3e-11 after a
        // day, ‖Φ‖ = 552): relative to ‖Φ‖², a few ε
        double symplectic = StmMeasures.SymplecticDefect(canonical);
        double relative = StmMeasures.RelativeSymplecticDefect(canonical);
        double determinant = StmMeasures.DeterminantDefect(canonical);
        _output.WriteLine(
            $"Δt = {dt} s: ‖ΦᵀJΦ − J‖ = {symplectic:E2} ({relative:E2} of ‖Φ‖²), |det Φ − 1| = {determinant:E2}");
        Assert.True(relative < 1e-14, $"relative symplectic defect: {relative:E2}");
        Assert.True(determinant < 1e-11, $"determinant defect: {determinant:E2}");
    }

    [Theory]
    [MemberData(nameof(TimesOfFlight))]
    public void Phi_EqualsCentralDifferencesOfTheClosedForm(double dt)
    {
        // Steps of 1 m and 1 mm/s, about 1e-7 of the state; the truncation is of order 1e-14 relative and the rounding
        // ε |x| / δ about 1e-9 relative
        AssertPhiEqualsCentralDifferences(_cases.R1, dt, "R1", 1e-7);
    }

    [Theory]
    [MemberData(nameof(R5Fractions))]
    public void R5_Phi_EqualsCentralDifferencesOfTheClosedForm(double fraction)
    {
        // The same steps as R1, now 1e-7 of the perigee radius and 1e-7 of the perigee speed only, so the rounding grows:
        // measured 1.1e-9 to 1.2e-8 (Linux, .NET 10, 2026-10-10), under the threshold of R1
        var referenceCase = _cases.TwoBody(_cases.R5);
        AssertPhiEqualsCentralDifferences(referenceCase, fraction * referenceCase.Duration.TotalSeconds, "R5", 1e-7);
    }

    private void AssertStateEqualsTheLibrary(ReferenceCase referenceCase, double dt, string name, double threshold)
    {
        // Arrange: the library propagates through the Keplerian elements and Kepler's equation, an independent path
        var start = referenceCase.InitialState;
        var epoch = start.Epoch.AddSeconds(dt);
        var expected = start.AtEpoch(epoch).ToStateVector();

        // Act: the time of flight of the epoch, which carries it to 100 ns (5e-11 relative at the perigee of R5 otherwise)
        var (state, _) = KeplerianStm.Propagate(referenceCase.GravitationalParameter, start.Position, start.Velocity,
            (epoch - start.Epoch).TotalSeconds);

        // Assert
        double positionError = (new Vector3(state[0], state[1], state[2]) - expected.Position).Magnitude() /
                               expected.Position.Magnitude();
        double velocityError = (new Vector3(state[3], state[4], state[5]) - expected.Velocity).Magnitude() /
                               expected.Velocity.Magnitude();
        _output.WriteLine($"{name}, Δt = {dt} s: position {positionError:E2}, velocity {velocityError:E2} (relative)");
        Assert.True(positionError < threshold, $"position: {positionError:E2}");
        Assert.True(velocityError < threshold, $"velocity: {velocityError:E2}");
    }

    private void AssertPhiEqualsCentralDifferences(ReferenceCase referenceCase, double dt, string name,
        double threshold)
    {
        // Arrange
        var start = referenceCase.InitialState;
        double mu = referenceCase.GravitationalParameter;
        var (_, phi) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, dt);

        // Act
        var expected = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var (plus, _) = Perturbed(mu, start, column, delta, dt);
            var (minus, _) = Perturbed(mu, start, column, -delta, dt);
            for (int row = 0; row < 6; row++)
            {
                expected[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        // Assert
        double error = StmMeasures.WorstBlockError(phi, expected);
        _output.WriteLine($"{name}, Δt = {dt} s: Φ against central differences {error:E2} (worst 3×3 block)");
        Assert.True(error < threshold, $"Φ: {error:E2}");
    }

    [Fact]
    public void Phi_Composes()
    {
        // Arrange: Φ(t2, t0) = Φ(t2, t1) Φ(t1, t0), with Φ(t2, t1) from the state at t1
        var start = _cases.R1.InitialState;
        double mu = _cases.R1.GravitationalParameter;
        const double t1 = 30000.0;
        const double t2 = 86400.0;
        var (middle, first) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, t1);
        var (_, second) = KeplerianStm.Propagate(mu, new Vector3(middle[0], middle[1], middle[2]),
            new Vector3(middle[3], middle[4], middle[5]), t2 - t1);

        // Act
        var (_, whole) = KeplerianStm.Propagate(mu, start.Position, start.Velocity, t2);
        var composed = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                for (int k = 0; k < 6; k++)
                {
                    composed[i * 6 + j] += second[i * 6 + k] * first[k * 6 + j];
                }
            }
        }

        // Assert
        double error = StmMeasures.WorstBlockError(composed, whole);
        _output.WriteLine($"Φ(t2, t1) Φ(t1, t0) against Φ(t2, t0): {error:E2} (worst 3×3 block)");
        Assert.True(error < 1e-11, $"Φ: {error:E2}");
    }

    [Fact]
    public void CircularOrbit_HasNoSingularity()
    {
        // Arrange: e = 0 exactly, where the Keplerian elements are singular; the universal variables are not
        var earth = (CelestialItem)_cases.R1.InitialState.Observer;
        double radius = 7078137.0;
        double speed = System.Math.Sqrt(earth.GM / radius);
        var position = new Vector3(radius, 0.0, 0.0);
        var velocity = new Vector3(0.0, speed * System.Math.Cos(0.5), speed * System.Math.Sin(0.5));
        const double dt = 5000.0;

        // Act
        var (_, phi) = KeplerianStm.Propagate(earth.GM, position, velocity, dt);

        // Assert
        var expected = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var plus = KeplerianStm.Propagate(earth.GM, Shift(position, column, delta),
                Shift(velocity, column - 3, delta), dt).State;
            var minus = KeplerianStm.Propagate(earth.GM, Shift(position, column, -delta),
                Shift(velocity, column - 3, -delta), dt).State;
            for (int row = 0; row < 6; row++)
            {
                expected[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        double error = StmMeasures.WorstBlockError(phi, expected);
        _output.WriteLine($"circular orbit, Φ against central differences {error:E2} (worst 3×3 block)");
        Assert.True(error < 1e-7, $"Φ: {error:E2}");
    }

    [Fact]
    public void HyperbolicOrbit_Throws()
    {
        // Arrange: faster than the escape speed
        var earth = (CelestialItem)_cases.R1.InitialState.Observer;
        double radius = 7078137.0;
        var velocity = new Vector3(0.0, 1.5 * System.Math.Sqrt(2.0 * earth.GM / radius), 0.0);

        // Act and assert
        Assert.Throws<ArgumentException>(() =>
            KeplerianStm.Propagate(earth.GM, new Vector3(radius, 0.0, 0.0), velocity, 60.0));
    }

    private static (double[] State, double[] Phi) Perturbed(double mu, StateVector start, int component,
        double delta, double dt)
    {
        return KeplerianStm.Propagate(mu, Shift(start.Position, component, delta),
            Shift(start.Velocity, component - 3, delta), dt);
    }

    // Shift component k (0 to 2) of a vector by delta; other values of k leave it unchanged
    private static Vector3 Shift(Vector3 vector, int k, double delta) => k switch
    {
        0 => new Vector3(vector.X + delta, vector.Y, vector.Z),
        1 => new Vector3(vector.X, vector.Y + delta, vector.Z),
        2 => new Vector3(vector.X, vector.Y, vector.Z + delta),
        _ => vector
    };
}
