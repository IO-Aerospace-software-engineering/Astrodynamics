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
/// Those cases are measured in the PR of step 4 and tested with the analytic partials of step 5a; the cases here have
/// a point-mass Earth.
/// </para>
/// <para>
/// Φ is measured in canonical units (<see cref="StmMeasures.Canonical"/>), with the initial radius and the matching
/// time unit, so that its components are of order one to a few hundred after a day.
/// </para>
/// </remarks>
public class StructuralPropertiesTests : IClassFixture<ReferenceCases>
{
    // The tolerance of the reference propagations of F1 in the specification. The defects decrease with it, so they are
    // the error of the integration: on R1 after a day, symplectic defect 3.0e-7, 2.8e-9 and 2.1e-10, determinant defect
    // 3.8e-8, 2.6e-10 and 4.8e-11, Keplerian error 1.2e-7, 9.9e-10 and 8.2e-12, at 1e-9, 1e-11 and 1e-13
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

    private static PropagationSolution Propagate(ReferenceCase referenceCase)
    {
        var start = referenceCase.InitialState;
        return PartialsTestCases.Spacecraft(start).Propagate(
            new TimeSystem.Window(start.Epoch, start.Epoch + referenceCase.Duration), referenceCase.Bodies,
            new RK78Integrator(Tolerance, Tolerance), referenceCase.AtmosphericDrag,
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
