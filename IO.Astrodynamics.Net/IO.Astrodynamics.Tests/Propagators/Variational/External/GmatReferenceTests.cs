// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// H2: Φ, Ψ, P and Q of the library against GMAT R2026a, a third-party implementation, on the cases G1 to G6
/// (<see cref="GmatCases"/>), with the model departures kept apart from the computation departures (design of #408).
/// </summary>
/// <remarks>
/// <para>
/// The GMAT outputs are committed in <c>Data/GmatReference</c>, produced by <c>tools/gmat_reference/run_gmat.sh</c>
/// from the scripts that <c>generate_scripts.py</c> writes from <c>cases.json</c>; GMAT is not needed to run the tests.
/// </para>
/// <para>
/// For Φ, at every output epoch (every hour and at the end of the arc), per 3×3 block, relative Frobenius norms:
/// </para>
/// <list type="bullet">
/// <item><description><b>total</b>: Φ of the library against Φ of GMAT (<c>Sat.OrbitSTM</c>);</description></item>
/// <item><description><b>models</b>: the central differences of whole propagations of the library against those of
/// GMAT, with the same steps: no variational equation enters, the departure is that of the models, to the resolution of
/// the two references;</description></item>
/// <item><description><b>GMAT's computation</b>: Φ of GMAT against GMAT's own central differences;</description></item>
/// <item><description><b>the library's computation</b>: Φ against the library's central differences (F1).</description></item>
/// </list>
/// </remarks>
[Trait("Category", "Validation")]
public class GmatReferenceTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1984;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public GmatReferenceTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // Bounds of Φ per case: total, models, GMAT's computation, the library's computation, and the state departure in
    // position (m) and velocity (m/s). G1: the threshold of the design (1e-10); the library's computation: the F1
    // thresholds (1e-7 for R1, 1e-4 otherwise); every other bound a record at twice the measurement of 2026-10-20,
    // rounded up at the second significant digit (H3 review of #408, N5)
    public static TheoryData<string, double, double, double, double, double, double> PhiCases => new()
    {
        { "G1", 1e-10, 1.4e-8, 3.5e-8, 1e-7, 1.1e-4, 1.2e-7 },
        { "G2", 1.7e-8, 2.6e-8, 3.3e-8, 1e-4, 0.12, 1.3e-4 },
        { "G3", 9.8e-12, 3.6e-8, 2.7e-8, 1e-4, 1.2e-4, 5.6e-9 },
        { "G4", 7.6e-8, 1.2e-7, 1.1e-7, 1e-4, 0.51, 3.7e-4 },
        { "G5", 2.1e-4, 3.8e-7, 2.0e-4, 1e-4, 1.7e-2, 1.9e-5 },
        { "G6", 1.1e-4, 1.8e-6, 1.1e-4, 1e-4, 0.23, 2.6e-4 }
    };

    [Theory]
    [MemberData(nameof(PhiCases))]
    public void Phi_AgainstGmat_ModelsAndComputationApart(string name, double totalBound, double modelsBound,
        double gmatBound, double ioBound, double positionBound, double velocityBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();
        var rotation = GmatReport.InitialRotation(gmatCase);
        var gmat = GmatReport.Read(name, "nominal", true, false, rotation);
        var gmatDifferences = GmatReport.CentralDifferencePhi(name, referenceCase.PositionStep, referenceCase.VelocityStep,
            rotation);

        // Act
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters));
        var phi = epochs.Select(t => StmMeasures.Phi(StmMeasures.Y(solution, t))).ToArray();
        var states = epochs.Select(t => WholePropagationDifferences.State(solution, t)).ToArray();
        var differences = WholePropagationDifferences.Phi(
            s => referenceCase.PropagateReference(NaifId - 1, s, ReferenceCases.DragCoefficient,
                ReferenceCases.ReflectivityCoefficient, ReferenceCases.ReferenceTolerance),
            start, referenceCase.PositionStep, referenceCase.VelocityStep, epochs);

        // Assert
        Assert.Equal(epochs.Length + 1, gmat.States.Length);
        var total = new double[4];
        var models = new double[4];
        var gmatComputation = new double[4];
        var ioComputation = new double[4];
        double position = 0.0, velocity = 0.0;
        for (int k = 0; k < epochs.Length; k++)
        {
            Max(total, StmMeasures.BlockErrors(phi[k], gmat.Phi[k + 1]));
            Max(models, StmMeasures.BlockErrors(differences[k], gmatDifferences[k]));
            Max(gmatComputation, StmMeasures.BlockErrors(gmat.Phi[k + 1], gmatDifferences[k]));
            Max(ioComputation, StmMeasures.BlockErrors(phi[k], differences[k]));
            position = System.Math.Max(position, Distance(states[k], gmat.States[k + 1], 0));
            velocity = System.Math.Max(velocity, Distance(states[k], gmat.States[k + 1], 3));
        }

        _output.WriteLine($"{name}: worst over {epochs.Length} epochs, blocks rr, rv, vr, vv");
        _output.WriteLine($"{name} total, library against GMAT: {Format(total)}");
        _output.WriteLine($"{name} models, central differences of the library against GMAT's: {Format(models)}");
        _output.WriteLine($"{name} GMAT's computation, its Φ against its central differences: {Format(gmatComputation)}");
        _output.WriteLine($"{name} library's computation, its Φ against its central differences: {Format(ioComputation)}");
        _output.WriteLine($"{name} state departure: position {position:E2} m, velocity {velocity:E2} m/s");
        Assert.True(total.Max() < totalBound, $"{name} total {total.Max():E2}");
        Assert.True(models.Max() < modelsBound, $"{name} models {models.Max():E2}");
        Assert.True(gmatComputation.Max() < gmatBound, $"{name} GMAT's computation {gmatComputation.Max():E2}");
        Assert.True(ioComputation.Max() < ioBound, $"{name} library's computation {ioComputation.Max():E2}");
        Assert.True(position < positionBound, $"{name} position {position:E2} m");
        Assert.True(velocity < velocityBound, $"{name} velocity {velocity:E2} m/s");
    }

    // Bounds of GMAT's Φ (worst block over the arc) and position (m) at its fixed step against half the step, records at
    // twice the measurement of 2026-10-20, rounded up at the second significant digit (H3 review of #408, N6). On G6
    // the departure between the two steps, 2.6e-5 (4.2e-5 before #410), is most of GMAT's computation (3.6e-5): there
    // that share is GMAT's step error at 10 s. G2 and G3 set again with the field of #410 at twice its measurement (G2 Φ
    // 7.24e-12, position 4.76e-5 m; G3 3.51e-13, 1.40e-5 m), decision of S. Guillet, 2026-10-23: at a fixed step that
    // departure is set by the last digits of the field GMAT is given, and seven fields within 0.4 m of radius and 1.5e-8
    // of GM gave 3.4e-7 to 1.4e-5 m on G3, so the bounds before (7.6e-7 m on G3) were twice one draw of GMAT's rounding
    public static TheoryData<string, double, double> StepCases => new()
    {
        { "G1", 4.0e-12, 2.0e-5 },
        { "G2", 1.5e-11, 9.6e-5 },
        { "G3", 7.1e-13, 2.8e-5 },
        { "G4", 2.2e-11, 1.3e-4 },
        { "G5", 1.0e-10, 7.0e-4 },
        { "G6", 8.4e-5, 4.7e-3 }
    };

    [Theory]
    [MemberData(nameof(StepCases))]
    public void Phi_AtTheFixedStepOfGmat_IsSettled(string name, double phiBound, double positionBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var rotation = GmatReport.InitialRotation(gmatCase);

        // Act
        var full = GmatReport.Read(name, "nominal", true, false, rotation);
        var half = GmatReport.Read(name, "nominal_half", true, false, rotation);

        // Assert: GMAT's Φ at its fixed step against half the step, worst block over the arc
        double worst = 0.0, position = 0.0;
        for (int k = 1; k < full.Phi.Length; k++)
        {
            worst = System.Math.Max(worst, StmMeasures.WorstBlockError(full.Phi[k], half.Phi[k]));
            position = System.Math.Max(position, Distance(full.States[k], half.States[k], 0));
        }

        _output.WriteLine($"{name} GMAT at {gmatCase.FixedStep} s against {gmatCase.FixedStep / 2.0} s: Φ {worst:E2}, " +
                          $"position {position:E2} m");
        Assert.True(worst < phiBound, $"{name} Φ at the step against half of it: {worst:E2}");
        Assert.True(position < positionBound, $"{name} position at the step against half of it: {position:E2} m");
    }

    // Bounds of Ψ over the arc relative to its largest value: against GMAT's central differences, the central
    // differences of the library against GMAT's (records at twice the measurement of 2026-10-20, rounded up at the
    // second significant digit), and the library against its own (F1, 1e-4). G5 models set again with #434 at twice
    // 1.37e-5 (Linux, 2026-10-22; 5.7e-6 before): the shadow fraction without cancellation reshuffled the steps of the
    // central differences of the library through the penumbra, while the library against GMAT did not move (5.52e-6,
    // 7.85e-6; decision of S. Guillet, 2026-10-22)
    public static TheoryData<string, double, double, double> PsiCases => new()
    {
        { "G5", 1.6e-5, 2.8e-5, 1e-4 },
        { "G6", 1.6e-6, 1.7e-6, 1e-4 }
    };

    [Theory]
    [MemberData(nameof(PsiCases))]
    public void Psi_AgainstGmatCentralDifferences(string name, double totalBound, double modelsBound, double ioBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();
        bool drag = referenceCase.AtmosphericDrag;
        double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
        double relativeStep = drag ? referenceCase.DragCoefficientStep : referenceCase.ReflectivityCoefficientStep;
        var gmatPsi = GmatReport.CentralDifferencePsi(name, drag ? "cd" : "cr", coefficient, relativeStep,
            GmatReport.InitialRotation(gmatCase));

        // Act: Ψ_Cr held with IncludeInStepControl, as in F1
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters, IncludeInStepControl: !drag));
        var psi = epochs.Select(t => StmMeasures.Column(StmMeasures.Y(solution, t), 6)).ToArray();
        var differences = WholePropagationDifferences.Psi(
            v => referenceCase.PropagateReference(NaifId - 1, start, drag ? v : ReferenceCases.DragCoefficient,
                drag ? ReferenceCases.ReflectivityCoefficient : v, ReferenceCases.ReferenceTolerance),
            coefficient, coefficient * relativeStep, epochs);

        // Assert
        var total = WholePropagationDifferences.PsiArcError(psi, gmatPsi);
        var models = WholePropagationDifferences.PsiArcError(differences, gmatPsi);
        var io = WholePropagationDifferences.PsiArcError(psi, differences);
        string label = drag ? "Ψ_Cd" : "Ψ_Cr";
        _output.WriteLine($"{name} {label} over the arc relative to its largest value (position, velocity):");
        _output.WriteLine($"{name} library against GMAT's central differences: {total.Position:E2}, {total.Velocity:E2}");
        _output.WriteLine($"{name} library's central differences against GMAT's: {models.Position:E2}, {models.Velocity:E2}");
        _output.WriteLine($"{name} library against its central differences: {io.Position:E2}, {io.Velocity:E2}");
        Assert.True(System.Math.Max(total.Position, total.Velocity) < totalBound, $"{name} {label} total");
        Assert.True(System.Math.Max(models.Position, models.Velocity) < modelsBound, $"{name} {label} models");
        Assert.True(System.Math.Max(io.Position, io.Velocity) < ioBound, $"{name} {label} library");
    }

    [Fact]
    public void Density_AlongTheDragCase_AgainstGmat()
    {
        // Arrange: GMAT's states and densities (kg/km³) every hour along G6
        var gmatCase = GmatCases.Get(_cases, "G6");
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var earth = (IO.Astrodynamics.Body.CelestialBody)start.Observer;
        var drag = new AtmosphericDrag(ReferenceCase.NewSpacecraft(NaifId - 3, start), earth);
        var rows = GmatReport.Rows("G6", "density");
        var rotation = GmatReport.InitialRotation(gmatCase);

        // Act: the density of the library at the same states
        double worst = 0.0, mean = 0.0;
        for (int k = 0; k < rows.Length; k++)
        {
            var epoch = start.Epoch.AddSeconds(k == rows.Length - 1 ? referenceCase.Duration.TotalSeconds
                : k * GmatCase.OutputInterval);
            var s = GmatReport.State(rows[k], rotation);
            var state = new IO.Astrodynamics.OrbitalParameters.StateVector(
                new IO.Astrodynamics.Math.Vector3(s[0], s[1], s[2]), new IO.Astrodynamics.Math.Vector3(s[3], s[4], s[5]),
                earth, epoch, start.Frame);
            double density = drag.Density(state.Position, drag.BodyFixedRotation(state), epoch);
            double gmatDensity = rows[k][6] * 1e-9;
            double ratio = density / gmatDensity - 1.0;
            worst = System.Math.Max(worst, System.Math.Abs(ratio));
            mean += ratio / rows.Length;
            _output.WriteLine($"G6 {k} h: library {density:E4} kg/m³, GMAT {gmatDensity:E4} kg/m³, ratio − 1 {ratio:E2}");
        }

        _output.WriteLine($"G6 density, library against GMAT: worst {worst:E2}, mean {mean:E2}");

        // Assert: the gate of the design (1e-3, decision 6 of #408), passed since #411 and #409 fixed the geodetic altitude
        // of the library and its TDB taken as UT (worst 5.0e-6 on 2026-10-20, 2.8e-2 before); G6 and the drag partials
        // are therefore compared as they are
        Assert.True(worst < 1e-3, $"G6 density {worst:E2}");
    }

    // Bounds of P (relative Frobenius, on the correlation matrix) and of Q (the first-order limit of GMAT's discrete
    // noise against the library, relative Frobenius), at twice the measurement of 2026-10-20, rounded up at the second
    // significant digit; GMAT's P resolves no better than its drift from its own Φ P0 Φᵀ (4.4e-9 on G1), so P on G1 is
    // held to 1e-8 rather than the 1e-10 of the design (decision of S. Guillet, 2026-10-19)
    public static TheoryData<string, double, double, double> CovarianceCases => new()
    {
        { "G1", 1e-8, 1e-8, 2.1e-8 },
        { "G2", 4.2e-8, 2.6e-6, 4.3e-8 }
    };

    [Theory]
    [MemberData(nameof(CovarianceCases))]
    public void CovarianceAndProcessNoise_AgainstGmat(string name, double pBound, double pCorrelationBound,
        double qBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var rotation = GmatReport.InitialRotation(gmatCase);
        var gmat = GmatReport.Read(name, "cov", true, true, rotation);
        // GMAT takes P0 in MJ2000Eq axes only: the scripts give it the P0 of cases.json there, so the library starts
        // from the same P0 turned to ICRF, R0 P0 R0ᵀ
        var start = WithRotatedCovariance(GmatCases.WithCovariance(referenceCase.InitialState), rotation);
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();

        // Act
        var solution = Propagate(referenceCase, start, null);
        // GMAT rejects a zero sigma: R and N at 1e-13 km/s^1.5, a density of 1e-20 m²/s³, the same here
        var noisy = Propagate(referenceCase, start, new RtnProcessNoise(1e-20, GmatCases.TangentialNoise, 1e-20));

        // Assert: P, and the parts of its departure on the correlation matrix (H3 review of #408, N4): GMAT's P against
        // its own Φ P0 Φᵀ, which it propagates step by step; the library's P against its own Φ P0 Φᵀ; and Φ P0 Φᵀ of
        // the library against that of GMAT, the departure of Φ alone, with the element where its worst falls
        double pRelative = 0.0, pCorrelation = 0.0, drift = 0.0, ioDrift = 0.0;
        var phiOnly = new CorrelationElement(0.0, 0, 0, 0);
        var p0 = Elements(start.Covariance!.Value);
        var phi = epochs.Select(t => Elements(solution.GetStateTransitionMatrix(t))).ToArray();
        for (int k = 0; k < epochs.Length; k++)
        {
            var p = Elements(solution.GetCovariance(epochs[k]));
            pRelative = System.Math.Max(pRelative, RelativeFrobenius(p, gmat.Covariance[k + 1]));
            pCorrelation = System.Math.Max(pCorrelation, CorrelationError(p, gmat.Covariance[k + 1]));
            var gmatPropagated = Propagated(gmat.Phi[k + 1], p0);
            drift = System.Math.Max(drift, CorrelationError(gmat.Covariance[k + 1], gmatPropagated));
            var ioPropagated = Propagated(phi[k], p0);
            ioDrift = System.Math.Max(ioDrift, CorrelationError(p, ioPropagated));
            var element = WorstCorrelationElement(ioPropagated, gmatPropagated, k);
            if (element.Error > phiOnly.Error)
            {
                phiOnly = element;
            }
        }

        _output.WriteLine($"{name} P against GMAT over {epochs.Length} epochs: relative Frobenius {pRelative:E2}, " +
                          $"on the correlation matrix {pCorrelation:E2}");
        _output.WriteLine($"{name} GMAT's computation, its P against its own Φ P0 Φᵀ, on the correlation matrix: " +
                          $"{drift:E2}");
        _output.WriteLine($"{name} library's computation, its P against its own Φ P0 Φᵀ, on the correlation matrix: " +
                          $"{ioDrift:E2}");
        _output.WriteLine($"{name} Φ alone, Φ P0 Φᵀ of the library against GMAT's, on the correlation matrix: " +
                          $"{phiOnly.Error:E2}, element ({phiOnly.Row}, {phiOnly.Column}) at " +
                          $"{gmatCase.Offsets[phiOnly.Epoch]} s");

        // The element (i, j) of Φ P0 Φᵀ is the product of the rows i and j of Φ whitened by P0, so its departure on the
        // correlation matrix is bounded by the relative departures of these two rows in the metric of P0,
        // e_i = √(δΦ_i P0 δΦ_iᵀ / (Φ_i P0 Φ_iᵀ)): large where the variance of the row is small against that of its
        // block, whatever the departure of the block
        var ioPhi = phi[phiOnly.Epoch];
        var gmatPhi = gmat.Phi[phiOnly.Epoch + 1];
        var gmatP = Propagated(gmatPhi, p0);
        int row = phiOnly.Row, column = phiOnly.Column;
        double rowI = WhitenedRowError(ioPhi, gmatPhi, p0, row);
        double rowJ = WhitenedRowError(ioPhi, gmatPhi, p0, column);
        _output.WriteLine($"{name} Φ alone at the worst element: rows {row} and {column} whitened by P0 departing by " +
                          $"{rowI:E2} and {rowJ:E2}; Φ per block {StmMeasures.WorstBlockError(ioPhi, gmatPhi):E2}; " +
                          $"σ of the rows {System.Math.Sqrt(gmatP[7 * row]):E3} and {System.Math.Sqrt(gmatP[7 * column]):E3}, " +
                          $"largest σ of their blocks {LargestSigma(gmatP, row):E3} and {LargestSigma(gmatP, column):E3}");
        Assert.True(phiOnly.Error <= 1.0001 * (rowI + rowJ), $"{name} the bound of the whitened rows");

        // The parts close: the departure of P is within the sum of its parts (design of #408, section 2)
        Assert.True(pCorrelation <= 1.0001 * (drift + ioDrift + phiOnly.Error),
            $"{name} P departs by {pCorrelation:E2}, above its parts {drift:E2} + {ioDrift:E2} + {phiOnly.Error:E2}");
        Assert.True(pRelative < pBound, $"{name} P {pRelative:E2}");
        Assert.True(pCorrelation < pCorrelationBound, $"{name} P on the correlation matrix {pCorrelation:E2}");

        // Q = P with the noise − P without, in both tools, at the end of the arc
        var end = epochs[^1];
        var q = Subtract(Elements(noisy.GetCovariance(end)), Elements(solution.GetCovariance(end)));
        var qGmat = new List<double[]>();
        foreach (double update in GmatCases.NoiseSteps)
        {
            var noisyGmat = GmatReport.Read(name, $"snc_{GmatCases.Format(update)}", true, true, rotation);
            qGmat.Add(Subtract(noisyGmat.Covariance[^1], Congruence(noisyGmat.Phi[^1], GmatCases.NoiseInitialDiagonal)));
            _output.WriteLine($"{name} Q at the end, GMAT's discrete noise every {update} s against the library: " +
                              $"relative Frobenius {RelativeFrobenius(qGmat[^1], q):E2}, on the correlation matrix " +
                              $"{CorrelationError(qGmat[^1], q):E2}");
        }

        // First order in the interval: each halving halves the departure, and the limit 2 Q(Δt/2) − Q(Δt) from each
        // pair of halving steps is the library's Q
        for (int i = 0; i + 1 < qGmat.Count; i++)
        {
            var limit = qGmat[i + 1].Zip(qGmat[i], (half, full) => 2.0 * half - full).ToArray();
            double ratio = RelativeFrobenius(qGmat[i], q) / RelativeFrobenius(qGmat[i + 1], q);
            double error = RelativeFrobenius(limit, q);
            _output.WriteLine($"{name} Q, first-order limit of GMAT from {GmatCases.NoiseSteps[i]} and " +
                              $"{GmatCases.NoiseSteps[i + 1]} s against the library: {error:E2} (departure halved: " +
                              $"ratio {ratio:F3})");
            Assert.InRange(ratio, 1.9, 2.1);
            Assert.True(error < qBound, $"{name} Q limit {error:E2}");
        }

        var tight = Propagate(referenceCase, start, new RtnProcessNoise(1e-20, GmatCases.TangentialNoise, 1e-20), 1e-14);
        var plain = Propagate(referenceCase, start, null, 1e-14);
        var qTight = Subtract(Elements(tight.GetCovariance(end)), Elements(plain.GetCovariance(end)));
        _output.WriteLine($"{name} Q of the library at 1e-13 against 1e-14: {RelativeFrobenius(q, qTight):E2}");
    }

    private static PropagationSolution Propagate(ReferenceCase referenceCase, IO.Astrodynamics.OrbitalParameters.StateVector start,
        RtnProcessNoise noise, double tolerance = ReferenceCases.Tolerance)
    {
        var spacecraft = ReferenceCase.NewSpacecraft(NaifId - 2, start);
        return spacecraft.PropagateWithVariationalEquations(referenceCase.Window, referenceCase.Bodies,
            new RK78Integrator(tolerance, tolerance), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, TimeSpan.FromHours(1.0),
            new VariationalOptions(ForceParameters.None, noise, PropagateCovariance: true));
    }

    // The state with its covariance P0 replaced by R0 P0 R0ᵀ, R0 applied to each 3×3 block
    private static IO.Astrodynamics.OrbitalParameters.StateVector WithRotatedCovariance(
        IO.Astrodynamics.OrbitalParameters.StateVector state, double[] rotation)
    {
        var p0 = state.Covariance!.Value;
        var b = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                b.Set(i, j, rotation[3 * i + j]);
                b.Set(3 + i, 3 + j, rotation[3 * i + j]);
            }
        }

        var rotated = b * p0 * b.Transpose();
        var symmetric = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                symmetric.Set(i, j, 0.5 * (rotated.Get(i, j) + rotated.Get(j, i)));
            }
        }

        return new IO.Astrodynamics.OrbitalParameters.StateVector(state.Position, state.Velocity, state.Observer,
            state.Epoch, state.Frame, symmetric);
    }

    private static double[] Elements(IO.Astrodynamics.Math.Matrix matrix)
    {
        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = matrix.Get(i, j);
            }
        }

        return values;
    }

    private static double[] Subtract(double[] a, double[] b) => a.Zip(b, (x, y) => x - y).ToArray();

    // Φ P Φᵀ, P row-major 6×6
    private static double[] Propagated(double[] phi, double[] p)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    for (int m = 0; m < 6; m++)
                    {
                        sum += phi[6 * i + k] * p[6 * k + m] * phi[6 * j + m];
                    }
                }

                result[6 * i + j] = sum;
            }
        }

        return result;
    }

    // Φ diag(d) Φᵀ
    private static double[] Congruence(double[] phi, double[] diagonal)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    sum += phi[6 * i + k] * diagonal[k] * phi[6 * j + k];
                }

                result[6 * i + j] = sum;
            }
        }

        return result;
    }

    private static double RelativeFrobenius(double[] actual, double[] expected)
    {
        double difference = 0.0, norm = 0.0;
        for (int i = 0; i < actual.Length; i++)
        {
            difference += (actual[i] - expected[i]) * (actual[i] - expected[i]);
            norm += expected[i] * expected[i];
        }

        return System.Math.Sqrt(difference / norm);
    }

    // max |ΔP_ij| / √(P_ii P_jj), with the diagonal of the expected matrix: the error on the correlation matrix,
    // independent of the units of the blocks (C6, #387)
    private static double CorrelationError(double[] actual, double[] expected) =>
        WorstCorrelationElement(actual, expected, 0).Error;

    // The worst element of CorrelationError, with where it falls
    private static CorrelationElement WorstCorrelationElement(double[] actual, double[] expected, int epoch)
    {
        var worst = new CorrelationElement(0.0, 0, 0, epoch);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double scale = System.Math.Sqrt(expected[7 * i] * expected[7 * j]);
                double error = System.Math.Abs(actual[6 * i + j] - expected[6 * i + j]) / scale;
                if (error > worst.Error)
                {
                    worst = new CorrelationElement(error, i, j, epoch);
                }
            }
        }

        return worst;
    }

    // √(δΦ_i P0 δΦ_iᵀ / (Φ_i P0 Φ_iᵀ)), δΦ = actual − expected, Φ the expected
    private static double WhitenedRowError(double[] actual, double[] expected, double[] p0, int row)
    {
        double departure = 0.0, variance = 0.0;
        for (int k = 0; k < 6; k++)
        {
            for (int m = 0; m < 6; m++)
            {
                double d = (actual[6 * row + k] - expected[6 * row + k]) * (actual[6 * row + m] - expected[6 * row + m]);
                departure += d * p0[6 * k + m];
                variance += expected[6 * row + k] * p0[6 * k + m] * expected[6 * row + m];
            }
        }

        return System.Math.Sqrt(departure / variance);
    }

    // The largest σ of the position (rows 0 to 2) or velocity (rows 3 to 5) block of a covariance
    private static double LargestSigma(double[] covariance, int row)
    {
        int first = row < 3 ? 0 : 3;
        return System.Math.Sqrt(Enumerable.Range(first, 3).Max(i => covariance[7 * i]));
    }

    private readonly record struct CorrelationElement(double Error, int Row, int Column, int Epoch);

    private static double Distance(double[] a, double[] b, int offset)
    {
        double sum = 0.0;
        for (int i = offset; i < offset + 3; i++)
        {
            sum += (a[i] - b[i]) * (a[i] - b[i]);
        }

        return System.Math.Sqrt(sum);
    }

    private static void Max(double[] worst, double[] errors)
    {
        for (int i = 0; i < worst.Length; i++)
        {
            worst[i] = System.Math.Max(worst[i], errors[i]);
        }
    }

    private static string Format(IEnumerable<double> values) => string.Join(", ", values.Select(v => v.ToString("E2")));
}
