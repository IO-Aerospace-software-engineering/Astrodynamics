// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The 2D probability of collision (#610, #611, #612) against its exact value: the seven designed encounters of lot 6 of
/// #457 (high, mid, low, near 5e-12, a hard-body radius much larger than the covariance, a covariance not positive
/// definite in the encounter plane, Frisbee's maximum), through the internal integral and the public
/// <see cref="ConjunctionAssessment.Analyze(ProtectedSpacecraftProfile, Propagator.PropagationSolution, Body.Spacecraft.Spacecraft, Propagator.PropagationSolution, Window, ConjunctionAnalysisOptions)"/>,
/// the reproductions of the three issues, and the private erf and erfc of the Pc path.
/// </summary>
/// <remarks>
/// The reference, <c>Data/CollisionProbabilityReference/collision_probability_reference.json</c>, comes from
/// <c>tools/collision_probability/collision_probability_reference.py</c>: CARA's Pc2D (the integral of the Gaussian of the
/// encounter plane over the disc, CARA's clip (1e-4 R)²) and Frisbee's maximum Pc, evaluated at 50 digits on the double
/// inputs by tanh-sinh and Gauss-Legendre agreeing to 1e-30, so the reference is exact for the inputs.
/// </remarks>
public partial class ConjunctionAssessmentTests
{
    private const string CollisionProbabilityReferenceFile = "Data/CollisionProbabilityReference/collision_probability_reference.json";

    // Every bound below is twice the worst relative error over Linux x64, Windows x64 and macOS arm64 (CI of #622 at
    // 7f5e6dfb, 2026-11-10), rounded up to two digits. The internal integral on the seven encounters (the doubles of
    // the suite's inputs.yaml): worst 2.01e-14 (003), the same on the three
    private const double DesignedEncounterPcBound = 4.1e-14;

    // The public Analyze on straight-line trajectories, the seven encounters: worst 3.15e-10 (005, the large hard-body
    // radius, at its exact TCA tick; 003 6.66e-11), PROVISIONAL (Linux x64, 2026-11-10) until the CI of the three
    // platforms. Every refined TCA is now the tick nearest the linear TCA, within 4.3e-7 tick of it (#624); before, the
    // tick below the root of the library's range rate, one tick early for 003 and 005, over which the covariance was held
    // in RTN, and 003's Pc of 1.1e-8 (miss 11 sigma) amplified that rotation: 9.03e-9, the same on the three platforms,
    // bound 1.9e-8. The integral itself is at 2.0e-14
    private const double DesignedEncounterAnalyzePcBound = 6.3e-10;

    // The 25 reproductions of #610, #611 and #612: worst 7.64e-15 on Linux x64 and Windows x64, 7.43e-15 on macOS arm64
    // (#612, λ2 1e-2 m² at 30°); before the fix 0 or 4e-86 for 1 (#610), −4.6 % (#611), null (#612)
    private const double ReproductionPcBound = 1.6e-14;

    // A covariance far wider than the disc with a miss off its axes (PC-1 of the lot's review: σ 1e5 to 1e6 m, R 1 m),
    // where the erfc difference of the chord probability cancels to about ε σ/R: worst 5.01e-11 on the three platforms
    // ((0, 1e6) m, diag(1e12, 1e12) m², the integrand's own rounding at 9 evaluations); 8.2e-14 to 5.4e-12 for the
    // three others (8.74e-14, 8.23e-14 and 9.10e-14 for (1e5, 3e5) m on Linux x64, Windows x64 and macOS arm64); before
    // the fix 1.3e-6 at the 200 000-evaluation budget
    private const double CancellationPcBound = 1.1e-10;

    // Fdlibm's erf and erfc, relative, at 22 points of its branches: worst 1.85e-16, the same on the three platforms
    private const double ErrorFunctionBound = 3.7e-16;

    // The exact Pc of IO's CARA checks, tools/collision_probability (rows "cara"): Omitron case 01 and Alfano case 03 of
    // NASA CARA Pc2D_Foster_UnitTest.m, as the tests build their inputs
    private const double OmitronCase01ExactPc = 2.706023527355529e-05;
    private const double AlfanoCase03ExactPc = 0.1003509475910938;

    // The internal integral on Omitron 01, Alfano 03 and Frisbee's table 1 against their exact Pc: worst 2.63e-15, the
    // same on the three platforms (Omitron 01; Alfano 03 1.38e-16 on Windows x64, 0 elsewhere; before #611, 1.1e-6,
    // hidden by the published values' 1e-5 and 3 %)
    private const double CaraDirectPcBound = 5.3e-15;

    // The same through Analyze and AnalyzeAll on straight-line trajectories: worst 2.20e-7, the same on the three
    // platforms (Omitron 01, whose states are not at the linear TCA: the covariance is held in RTN over the refined
    // TCA's offset), unchanged by the nearer tick of #624 (Linux x64, 2026-11-10); Alfano 03 2.7e-11 (4.8e-11 before
    // #624)
    private const double CaraEntryPointPcBound = 4.4e-7;

    private static readonly Lazy<JsonDocument> CollisionProbabilityReference =
        new(() => JsonDocument.Parse(File.ReadAllText(CollisionProbabilityReferenceFile)));

    [Fact]
    public void CollisionProbability_DesignedEncounters_MatchExactPc()
    {
        double worst = 0.0;
        foreach (var encounter in CollisionProbabilityReference.Value.RootElement.GetProperty("encounters").EnumerateArray())
        {
            var (r1, v1, c1, r2, v2, c2, hardBodyRadius) = ReadEncounter(encounter, 1.0);
            var exact = encounter.GetProperty("exact");
            var combined = c1 is null ? c2 : c2 is null ? c1 : Add(c1.Value, c2.Value);
            var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(r2 - r1, v2 - v1, combined!.Value);
            var pc = exact.GetProperty("maximum").GetBoolean()
                ? ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(miss2D, cov2D, hardBodyRadius)
                : ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, hardBodyRadius);

            Assert.NotNull(pc);
            double error = RelativeError(pc.Value, exact.GetProperty("pc"));
            worst = System.Math.Max(worst, error);
            _output.WriteLine($"MEASURE #611 internal {encounter.GetProperty("name").GetString()}: Pc {pc.Value:R}, relative error {error:E2}");
            Assert.True(error <= DesignedEncounterPcBound, $"{encounter.GetProperty("name").GetString()}: {error:E2}");
        }

        _output.WriteLine($"MEASURE #611 internal worst {worst:E2}");
    }

    [Fact]
    public void Analyze_DesignedEncounters_MatchExactPcAndFlags()
    {
        // Arrange: the runner's path, each object a spacecraft whose initial state and covariance are its state at TCA,
        // straight-line trajectories over TCA ± 10 s, the combined hard-body radius on the protected object
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        double worst = 0.0;
        int id = 0;
        foreach (var encounter in CollisionProbabilityReference.Value.RootElement.GetProperty("encounters").EnumerateArray())
        {
            var (r1, v1, c1, r2, v2, c2, hardBodyRadius) = ReadEncounter(encounter, 1000.0);
            var exact = encounter.GetProperty("exact");
            var protectedSpacecraft = c1 is null
                ? CreateSpacecraftWithoutCovariance(-6100 - 2 * id, "PC_P", earth, r1, v1, hardBodyRadius)
                : CreateSpacecraft(-6100 - 2 * id, "PC_P", earth, r1, v1, BuildStateCovarianceFromPosition(c1.Value), hardBodyRadius);
            var secondarySpacecraft = c2 is null
                ? CreateSpacecraftWithoutCovariance(-6101 - 2 * id, "PC_S", earth, r2, v2, 0.0)
                : CreateSpacecraft(-6101 - 2 * id, "PC_S", earth, r2, v2, BuildStateCovarianceFromPosition(c2.Value), 0.0);
            id++;
            var protectedTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
                seconds => (r1 + v1 * (seconds - 10.0), v1, Vector3.Zero));
            var secondaryTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
                seconds => (r2 + v2 * (seconds - 10.0), v2, Vector3.Zero));

            // Act
            var result = ConjunctionAssessment.Analyze(
                new ProtectedSpacecraftProfile(protectedSpacecraft),
                protectedTrajectory,
                secondarySpacecraft,
                secondaryTrajectory,
                window,
                new ConjunctionAnalysisOptions
                {
                    SampleStep = TimeSpan.FromDays(1.0),
                    MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                    SecondaryHardBodyRadiusMeters = 0.0,
                    LowRelativeSpeedThresholdMetersPerSecond = 0.0
                });

            // Assert: the Pc, the remediation and the maximum-Pc flags
            var flags = result.EncounterState.QualityFlags;
            Assert.NotNull(result.CollisionRisk.ProbabilityOfCollision);
            double error = RelativeError(result.CollisionRisk.ProbabilityOfCollision.Value, exact.GetProperty("pc"));
            worst = System.Math.Max(worst, error);
            _output.WriteLine($"MEASURE #611 Analyze {encounter.GetProperty("name").GetString()}: relative error {error:E2}, flags {flags}");
            Assert.True(error <= DesignedEncounterAnalyzePcBound, $"{encounter.GetProperty("name").GetString()}: {error:E2}");
            Assert.Equal(exact.GetProperty("remediated").GetBoolean(), flags.HasFlag(EncounterQualityFlags.CovarianceRemediated));
            Assert.Equal(exact.GetProperty("maximum").GetBoolean(), flags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed));
            Assert.False(flags.HasFlag(EncounterQualityFlags.CovarianceUnavailable));
        }

        _output.WriteLine($"MEASURE #611 Analyze worst {worst:E2}");
    }

    [Fact]
    public void CollisionProbability_IssueReproductions_MatchExactPc()
    {
        // #610: a Gaussian narrower than R/29 inside or at the edge of the disc (0 or 4e-86 before);
        // #611: the scan of C2D diag(1e6, 4e4) m², R 10 m, down to 5.7e-15 (−4.6 % below 1e-10 before);
        // #612: λ1 1e12 m² and λ2 from 1e-2 to −1 m² at 0° and 30° (null below the clip before)
        double worst = 0.0;
        foreach (var row in CollisionProbabilityReference.Value.RootElement.GetProperty("reproductions").EnumerateArray())
        {
            var miss = row.GetProperty("miss").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var cov = row.GetProperty("cov").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var cov2D = new Matrix(2, 2);
            cov2D.Set(0, 0, cov[0]);
            cov2D.Set(0, 1, cov[1]);
            cov2D.Set(1, 0, cov[1]);
            cov2D.Set(1, 1, cov[2]);

            var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss, cov2D, row.GetProperty("hbr").GetDouble());

            Assert.NotNull(pc);
            double error = RelativeError(pc.Value, row.GetProperty("exact").GetProperty("pc"));
            worst = System.Math.Max(worst, error);
            _output.WriteLine($"MEASURE #{row.GetProperty("issue").GetString()} {row.GetProperty("name").GetString()}: Pc {pc.Value:R}, relative error {error:E2}");
            Assert.True(error <= ReproductionPcBound, $"{row.GetProperty("name").GetString()}: {error:E2}");
        }

        _output.WriteLine($"MEASURE #610-#612 reproductions worst {worst:E2}");
    }

    [Fact]
    public void CollisionProbability_CovarianceFarWiderThanTheDisc_MatchExactPc()
    {
        // PC-1: (1e5, 3e5) m and its mirror with diag(1e10, 1e10) m², (1e6, 1e6) m with diag(1e12, 1e10) m², (0, 1e6) m
        // with diag(1e12, 1e12) m², R 1 m; a Pc at the budget's truncation was 1.3e-6 off, so the bound also shows that no
        // case reaches it
        double worst = 0.0;
        foreach (var row in CollisionProbabilityReference.Value.RootElement.GetProperty("cancellation").EnumerateArray())
        {
            var miss = row.GetProperty("miss").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var cov = row.GetProperty("cov").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var cov2D = new Matrix(2, 2);
            cov2D.Set(0, 0, cov[0]);
            cov2D.Set(0, 1, cov[1]);
            cov2D.Set(1, 0, cov[1]);
            cov2D.Set(1, 1, cov[2]);

            var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss, cov2D, row.GetProperty("hbr").GetDouble());
            double direct = EncounterPlaneProbability.Integrate(miss[0], miss[1], cov[0], cov[2], row.GetProperty("hbr").GetDouble());

            Assert.NotNull(pc);
            Assert.Equal(direct, pc.Value);
            double error = RelativeError(pc.Value, row.GetProperty("exact").GetProperty("pc"));
            worst = System.Math.Max(worst, error);
            _output.WriteLine($"MEASURE #611 cancellation {row.GetProperty("name").GetString()}: Pc {pc.Value:R}, relative error {error:E2}");
            Assert.True(error <= CancellationPcBound, $"{row.GetProperty("name").GetString()}: {error:E2}");
        }

        _output.WriteLine($"MEASURE #611 cancellation worst {worst:E2}");
    }

    [Fact]
    public void CollisionProbability_PrivateErrorFunctions_MatchMpmath()
    {
        double worst = 0.0;
        foreach (var row in CollisionProbabilityReference.Value.RootElement.GetProperty("erf").EnumerateArray())
        {
            double x = row.GetProperty("x").GetDouble();
            double erfError = RelativeError(EncounterPlaneProbability.Erf(x), row.GetProperty("erf"));
            double erfcError = RelativeError(EncounterPlaneProbability.Erfc(x), row.GetProperty("erfc"), 1.0e-300);
            worst = System.Math.Max(worst, System.Math.Max(erfError, erfcError));
            Assert.True(erfError <= ErrorFunctionBound, $"erf({x:R}): {erfError:E2}");
            Assert.True(erfcError <= ErrorFunctionBound, $"erfc({x:R}): {erfcError:E2}");
        }

        _output.WriteLine($"MEASURE #611 erf/erfc worst {worst:E2}");
    }

    [Fact]
    public void CollisionProbability_DegenerateInputs_GiveZeroOrNull()
    {
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, 100.0);
        cov2D.Set(1, 1, 25.0);

        Assert.Equal(0.0, ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0, 0.0 }, cov2D, 0.0));
        Assert.Null(ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0, 0.0 }, cov2D, double.NaN));
        Assert.Null(ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0, 0.0 }, new Matrix(3, 3), 5.0));
        var notFinite = new Matrix(2, 2);
        notFinite.Set(0, 0, double.PositiveInfinity);
        notFinite.Set(1, 1, 1.0);
        Assert.Null(ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0, 0.0 }, notFinite, 5.0));
        Assert.Equal(0.0, EncounterPlaneProbability.Integrate(double.NaN, 0.0, 1.0, 1.0, 1.0));
        // A disc beyond the double range of the Gaussian: every sample underflows
        Assert.Equal(0.0, ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0e6, 0.0 }, cov2D, 5.0));
        Assert.Null(ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(new[] { double.NaN, 0.0 }, cov2D, 5.0));
        Assert.Null(ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(new[] { 1.0 }, cov2D, 5.0));
        // A clip that underflows to 0 leaves a zero covariance without variance: no Pc
        Assert.Null(ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 1.0, 0.0 }, new Matrix(2, 2), 1.0e-200));
        Assert.Null(ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(new[] { 1.0, 0.0 }, notFinite, 5.0));
        Assert.Null(ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(new[] { 1.0 }, cov2D));
        Assert.Null(ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(new[] { 1.0, 0.0 }, new Matrix(3, 3)));
        Assert.Null(ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(new[] { 1000.0, 0.0 }, new Matrix(2, 2)));
        // A zero miss or a miss inside one sigma keeps the known covariance
        foreach (var miss in new[] { new[] { 0.0, 0.0 }, new[] { 5.0, 0.0 } })
        {
            var kept = ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(miss, cov2D);
            Assert.NotNull(kept);
            Assert.Equal(new[] { 100.0, 0.0, 0.0, 25.0 }, new[] { kept.Value.Get(0, 0), kept.Value.Get(0, 1), kept.Value.Get(1, 0), kept.Value.Get(1, 1) });
        }
    }

    private static (Vector3 r1, Vector3 v1, Matrix? c1, Vector3 r2, Vector3 v2, Matrix? c2, double hardBodyRadius) ReadEncounter(
        JsonElement encounter, double lengthScale)
    {
        Vector3 Read(string name)
        {
            var c = encounter.GetProperty(name).EnumerateArray().Select(x => x.GetDouble()).ToArray();
            return new Vector3(c[0], c[1], c[2]) * lengthScale;
        }

        var r1 = Read("r1");
        var v1 = Read("v1");
        var r2 = Read("r2");
        var v2 = Read("v2");

        Matrix? Covariance(string name, Vector3 r, Vector3 v)
        {
            var element = encounter.GetProperty(name);
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            // The CDM's lower triangle R, TR, TT, NR, NT, NN
            var l = element.EnumerateArray().Select(x => x.GetDouble() * lengthScale * lengthScale).ToArray();
            var rtn = BuildPositionCovariance(new[,] { { l[0], l[1], l[3] }, { l[1], l[2], l[4] }, { l[3], l[4], l[5] } });
            return RotateRtnPositionCovarianceToInertial(r, v, rtn);
        }

        return (r1, v1, Covariance("cov1_rtn", r1, v1), r2, v2, Covariance("cov2_rtn", r2, v2),
            encounter.GetProperty("hbr").GetDouble() * lengthScale);
    }

    private static double RelativeError(double value, JsonElement exactText, double floor = 0.0)
    {
        double exact = double.Parse(exactText.GetString()!, CultureInfo.InvariantCulture);
        return exact == 0.0 ? System.Math.Abs(value) : System.Math.Abs(value - exact) / System.Math.Max(System.Math.Abs(exact), floor);
    }
}
