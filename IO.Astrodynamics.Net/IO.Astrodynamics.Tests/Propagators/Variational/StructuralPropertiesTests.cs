// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// F2: the structural properties of the state transition matrix integrated by RK7(8), on conservative dynamics, and Φ
/// against the closed-form Keplerian STM on R1.
/// </summary>
/// <remarks>
/// <para>
/// The flow of a Hamiltonian system is symplectic, ΦᵀJΦ = J with J = [[0, I], [−I, 0]], and preserves the phase-space
/// volume, det Φ = 1 (Liouville's theorem); the two-body, geopotential and third-body accelerations derive from a
/// potential, time-dependent for the rotating Earth and the moving bodies. Reference: Arnold, Mathematical Methods of
/// Classical Mechanics, 2nd ed., Springer (1989), section 16 (Liouville's theorem) and chapter 8 (Hamiltonian phase
/// flows preserve the symplectic structure); to be verified by S. Guillet.
/// </para>
/// <para>
/// How the integrated Φ departs from it, derived: with A = [[0, I], [G, D]], d(ΦᵀJΦ)/dt = Φᵀ (AᵀJ + JA) Φ and
/// AᵀJ + JA = [[G − Gᵀ, D], [−Dᵀ, 0]]. For a conservative force D = 0, so the defect grows only through the asymmetry
/// of G, integrated with the position rows of Φ on both sides, and through the RK step map, which is not symplectic.
/// The point-mass and third-body partials (B2, B4) are symmetric; the geopotential partials are central differences
/// until step 5a, asymmetric by 2e-11 to 6e-11 of G, which puts the geopotential cases at 2e-9 to 2e-7 after a day.
/// Their determinant is asserted here, and their symplectic defect is written to the output, measured but not asserted
/// until the analytic partials of step 5a; the symplectic defect is asserted on the cases with a point-mass Earth.
/// </para>
/// <para>
/// Φ is measured in canonical units (<see cref="StmMeasures.Canonical"/>), with the initial radius and the matching
/// time unit, so that its components are of order one to a few hundred after a day.
/// </para>
/// </remarks>
public class StructuralPropertiesTests : IClassFixture<ReferenceCases>
{
    // The tolerance of the reference propagations of F1 in the specification. The defects decrease with it, so they are
    // the error of the integration (R1_DefectsDecreaseWithTheTolerance): on R1 after a day, symplectic defect 3.0e-7,
    // 2.8e-9 and 2.1e-10, determinant defect 3.8e-8, 2.6e-10 and 4.8e-11, Keplerian error 1.2e-7, 9.9e-10 and 8.2e-12,
    // at 1e-9, 1e-11 and 1e-13
    private const double Tolerance = 1e-13;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public StructuralPropertiesTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // R5 is checked relative to ‖Φ‖² (decision of S. Guillet, 2026-10-08): after two revolutions of a 300 × 36 000 km
    // orbit, ‖Φ‖ = 2.7e3 in canonical units and the rounding of the integration alone gives 1.05e-9, 1.09e-9 and 2.3e-9
    // at 1e-12, 1e-13 and 1e-14, which no longer decrease with the tolerance; relative to ‖Φ‖² that is 1.5e-16, ε
    public static TheoryData<string, bool> PointMassCases => new()
    {
        { "R2", false }, { "R3", false }, { "R4", false }, { "R5", true }
    };

    // The conservative geopotential cases of F2, with and without their third bodies
    public static TheoryData<string, bool> GeopotentialCases => new()
    {
        { "R2", true }, { "R3", true }, { "R4", true }, { "R5", true },
        { "R2", false }, { "R3", false }, { "R4", false }, { "R5", false }
    };

    [Fact]
    public void R1_PhiIsSymplecticWithUnitDeterminant()
    {
        // Arrange
        var referenceCase = _cases.R1;
        var solution = Propagate(referenceCase);

        // Act
        var canonical = CanonicalPhi(referenceCase, solution, referenceCase.InitialState.Epoch + referenceCase.Duration);

        // Assert
        double symplectic = StmMeasures.SymplecticDefect(canonical);
        double determinant = StmMeasures.DeterminantDefect(canonical);
        _output.WriteLine($"R1 after a day: ‖ΦᵀJΦ − J‖ = {symplectic:E2}, |det Φ − 1| = {determinant:E2}");
        Assert.True(symplectic < 1e-9, $"symplectic defect: {symplectic:E2}");
        Assert.True(determinant < 1e-9, $"determinant defect: {determinant:E2}");
    }

    [Fact]
    public void R1_PhiMatchesTheKeplerianStm()
    {
        // Arrange
        var referenceCase = _cases.R1;
        var start = referenceCase.InitialState;
        var solution = Propagate(referenceCase);

        foreach (double fraction in new[] { 0.25, 0.5, 1.0 })
        {
            // Act
            var epoch = fraction < 1.0
                ? start.Epoch.AddSeconds(fraction * referenceCase.Duration.TotalSeconds)
                : start.Epoch + referenceCase.Duration;
            var phi = Phi(solution, epoch);
            var (_, expected) = KeplerianStm.Propagate(referenceCase.GravitationalParameter, start.Position,
                start.Velocity, (epoch - start.Epoch).TotalSeconds);

            // Assert
            double error = StmMeasures.WorstBlockError(phi, expected);
            _output.WriteLine($"R1 at {fraction:F2} of a day: Φ against the Keplerian STM {error:E2} (worst 3×3 block)");
            Assert.True(error < 1e-9, $"Φ at {fraction:F2} of a day: {error:E2}");
        }
    }

    [Theory]
    [MemberData(nameof(PointMassCases))]
    public void PointMassEarthWithThirdBodies_PhiIsSymplecticWithUnitDeterminant(string name, bool relative)
    {
        // Arrange: the state and third bodies of the case, around a point-mass Earth
        var referenceCase = _cases.WithPointMassEarth(_cases[name]);
        var solution = Propagate(referenceCase);

        // Act
        var canonical = CanonicalPhi(referenceCase, solution, referenceCase.InitialState.Epoch + referenceCase.Duration);

        // Assert
        double symplectic = StmMeasures.SymplecticDefect(canonical);
        double relativeSymplectic = StmMeasures.RelativeSymplecticDefect(canonical);
        double determinant = StmMeasures.DeterminantDefect(canonical);
        _output.WriteLine($"{name}, point-mass Earth and third bodies: ‖ΦᵀJΦ − J‖ = {symplectic:E2} " +
                          $"({relativeSymplectic:E2} of ‖Φ‖²), |det Φ − 1| = {determinant:E2}");
        if (relative)
        {
            Assert.True(relativeSymplectic < 1e-14, $"relative symplectic defect: {relativeSymplectic:E2}");
        }
        else
        {
            Assert.True(symplectic < 1e-9, $"symplectic defect: {symplectic:E2}");
        }

        Assert.True(determinant < 1e-9, $"determinant defect: {determinant:E2}");
    }

    [Fact]
    public void R5TwoBody_PhiMatchesTheKeplerianStm()
    {
        // Arrange: the R5 state around a point-mass Earth, no third body, two revolutions starting at perigee. The step
        // changes by orders of magnitude along the arc and ‖Φ‖ reaches 2.7e3 in canonical units, where R1 runs at an
        // almost constant step with a Φ of order 1e2.
        var referenceCase = _cases.TwoBody(_cases.R5);
        var start = referenceCase.InitialState;
        var solution = Propagate(referenceCase);

        // Act and assert: at the apogees and the perigees. Measured 2.7e-14, 7.8e-13, 2.4e-12 and 3.5e-12 (Linux, .NET 10,
        // 2026-10-10), growing with Φ; the threshold is about three times the largest
        foreach (double fraction in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var epoch = fraction < 1.0
                ? start.Epoch.AddSeconds(fraction * referenceCase.Duration.TotalSeconds)
                : start.Epoch + referenceCase.Duration;
            var phi = Phi(solution, epoch);
            var (_, expected) = KeplerianStm.Propagate(referenceCase.GravitationalParameter, start.Position,
                start.Velocity, (epoch - start.Epoch).TotalSeconds);

            double error = StmMeasures.WorstBlockError(phi, expected);
            _output.WriteLine($"R5 two-body at {fraction:F2} of two revolutions: Φ against the Keplerian STM " +
                              $"{error:E2} (worst 3×3 block)");
            Assert.True(error < 1e-11, $"Φ at {fraction:F2} of two revolutions: {error:E2}");
        }
    }

    [Theory]
    [MemberData(nameof(GeopotentialCases))]
    public void ConservativeGeopotentialCases_HaveUnitDeterminant(string name, bool thirdBodies)
    {
        // Arrange: the case without drag and SRP, with or without its third bodies
        var referenceCase = thirdBodies ? _cases[name].Conservative : ReferenceCases.GeopotentialOnly(_cases[name]);
        var solution = Propagate(referenceCase);

        // Act
        var canonical = CanonicalPhi(referenceCase, solution, referenceCase.InitialState.Epoch + referenceCase.Duration);

        // Assert: the determinant only. The symplectic defect is measured, not asserted, until the analytic partials of
        // step 5a: the central-difference partials of the geopotential are asymmetric and dominate it.
        double symplectic = StmMeasures.SymplecticDefect(canonical);
        double relativeSymplectic = StmMeasures.RelativeSymplecticDefect(canonical);
        double determinant = StmMeasures.DeterminantDefect(canonical);
        _output.WriteLine($"{name}, geopotential{(thirdBodies ? " and third bodies" : " only")}: " +
                          $"‖ΦᵀJΦ − J‖ = {symplectic:E2} ({relativeSymplectic:E2} of ‖Φ‖², not asserted until 5a), " +
                          $"|det Φ − 1| = {determinant:E2}");
        Assert.True(determinant < 1e-9, $"determinant defect: {determinant:E2}");
    }

    [Fact]
    public void R1_DefectsDecreaseWithTheTolerance()
    {
        // Arrange
        var referenceCase = _cases.R1;
        var start = referenceCase.InitialState;
        var end = start.Epoch + referenceCase.Duration;
        var (_, keplerian) = KeplerianStm.Propagate(referenceCase.GravitationalParameter, start.Position,
            start.Velocity, referenceCase.Duration.TotalSeconds);
        var previous = (Symplectic: double.MaxValue, Determinant: double.MaxValue, Keplerian: double.MaxValue);

        foreach (double tolerance in new[] { 1e-9, 1e-11, 1e-13 })
        {
            // Act
            var solution = Propagate(referenceCase, tolerance);
            var phi = Phi(solution, end);
            var canonical = CanonicalPhi(referenceCase, solution, end);
            var current = (StmMeasures.SymplecticDefect(canonical), StmMeasures.DeterminantDefect(canonical),
                StmMeasures.WorstBlockError(phi, keplerian));

            // Assert: every defect decreases with the tolerance, so it is the error of the integration
            _output.WriteLine($"R1 at {tolerance:E0}: ‖ΦᵀJΦ − J‖ = {current.Item1:E2}, |det Φ − 1| = {current.Item2:E2}, " +
                              $"Φ against the Keplerian STM {current.Item3:E2}");
            Assert.True(current.Item1 < previous.Symplectic, $"symplectic defect at {tolerance:E0}: {current.Item1:E2}");
            Assert.True(current.Item2 < previous.Determinant,
                $"determinant defect at {tolerance:E0}: {current.Item2:E2}");
            Assert.True(current.Item3 < previous.Keplerian, $"Keplerian error at {tolerance:E0}: {current.Item3:E2}");
            previous = current;
        }
    }

    [Fact]
    public void R5PointMass_RelativeSymplecticDefectIsAtTheRoundingWhateverTheTolerance()
    {
        // Arrange
        var referenceCase = _cases.WithPointMassEarth(_cases.R5);
        var end = referenceCase.InitialState.Epoch + referenceCase.Duration;

        foreach (double tolerance in new[] { 1e-12, 1e-13, 1e-14 })
        {
            // Act
            var canonical = CanonicalPhi(referenceCase, Propagate(referenceCase, tolerance), end);

            // Assert: the absolute defect no longer decreases with the tolerance; relative to ‖Φ‖² it is the rounding
            double symplectic = StmMeasures.SymplecticDefect(canonical);
            double relative = StmMeasures.RelativeSymplecticDefect(canonical);
            _output.WriteLine($"R5, point-mass Earth and third bodies, at {tolerance:E0}: ‖ΦᵀJΦ − J‖ = " +
                              $"{symplectic:E2} ({relative:E2} of ‖Φ‖²)");
            Assert.True(relative < 1e-14, $"relative symplectic defect at {tolerance:E0}: {relative:E2}");
        }
    }

    private static PropagationSolution Propagate(ReferenceCase referenceCase, double tolerance = Tolerance)
    {
        var start = referenceCase.InitialState;
        return PartialsTestCases.Spacecraft(start).Propagate(
            new TimeSystem.Window(start.Epoch, start.Epoch + referenceCase.Duration), referenceCase.Bodies,
            new RK78Integrator(tolerance, tolerance), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, TimeSpan.FromHours(1.0), new VariationalOptions());
    }

    private static double[] Phi(PropagationSolution solution, TimeSystem.Time epoch)
    {
        var y = new double[36];
        solution.EvaluateVariational(epoch, y, Array.Empty<double>());
        return y;
    }

    private static double[] CanonicalPhi(ReferenceCase referenceCase, PropagationSolution solution,
        TimeSystem.Time epoch)
    {
        return StmMeasures.Canonical(Phi(solution, epoch), referenceCase.InitialState.Position.Magnitude(),
            referenceCase.GravitationalParameter);
    }
}
