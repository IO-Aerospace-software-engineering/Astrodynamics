// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

/// <summary>
/// F3 on R2 (geopotential, Sun, Moon, NRLMSISE-00 drag), and with Cd (R2) or Cr (R3) sampled as well, which validates
/// the sensitivity Ψ through the covariance with the coefficient as a consider parameter (<see cref="CovarianceMonteCarlo"/>).
/// </summary>
/// <remarks>
/// <para>
/// 5 to 6.5 min each on 4 cores: in the <c>LongValidation</c> category, run by the manual workflow
/// <c>long-validation.yml</c> (decision of S. Guillet, 2026-10-06). Cd sampled with σ = 0.22 (10 %), Cr with σ = 0.15,
/// independent of the state; P then holds the coefficient as a consider parameter, Ψc Pp Ψcᵀ, so the element-wise
/// test also checks Ψ.
/// </para>
/// <para>
/// Measured (Linux, .NET 10, 2026-10-16; seed 20261016, RK7(8) at 1e-11), every 10 min up to 6 h, at one revolution and
/// at 24 h. Element-wise, every epoch within 4 standard deviations: worst |z| 1.89 (R2), 2.26 (R2, Cd sampled), 2.14
/// (R3, Cr sampled), a noise set by the along-track variance in inertial axes. Along every direction of the state
/// (<see cref="CovarianceMonteCarlo.EpochResult"/>) the ratios of S to P stay within their sampling band up to 1 h
/// (asserted), then the largest is 1.15, 1.05 and 1.13 at one revolution, 609, 2.16 and 362 at 6 h, 2.4e6, 87 and
/// 1.3e6 at 24 h; over the position alone 0.95 to 1.05 up to 6 h, and 5.00, 3.96 and 4.32 at 24 h (radial alone 1.17,
/// 1.11 and 1.11). Kolmogorov-Smirnov at one revolution: p = 0.032 (R2, 1.543 h), 0.37 (R2, Cd sampled), 0.011 (R3,
/// Cr sampled, 1.646 h). Both tests pass up to 1.67 h and fail from 1.83 h for R2 and for R3 with Cr sampled; with Cd
/// sampled up to 2.33 h, failing from 2.5 h (asserted within half an hour: at 1.67 h the significance of R3 is 0.01003). The sampled Cd widens the along-track spread, which the thin direction of
/// P across the curved orbit then tolerates longer; the test even passes again at 3.17 h and 3.33 h before failing for
/// good from 3.5 h. The departure follows the curvature of the orbit, as for R1: at 24 h a mean radial offset of
/// −129 m against −124 m (R2), −200 m against −188 m (Cd sampled), −104 m against −102 m (R3).
/// </para>
/// <para>
/// Re-measured 2026-10-20 after #411 and #409 (exact geodetic coordinates, NRLMSISE-00 in UTC), which move the density
/// of the drag cases: with Cd sampled, the Kolmogorov-Smirnov p at one revolution from 0.34 to 0.37, the largest ratio
/// at 6 h from 2.17 to 2.16, the position at 24 h from 3.93 to 3.96, the mean radial offset at 24 h from −198 m against
/// −186 m to −200 m against −188 m; R2, the position at 24 h from 4.99 to 5.00. The other figures are unchanged.
/// </para>
/// </remarks>
[Trait("Category", "LongValidation")]
public class CovarianceMonteCarloLongTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1985;
    private const int Seed = 20261016;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public CovarianceMonteCarloLongTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // Case, σ of Cd and of Cr (0 when not sampled), first epoch at which a test fails (hours, on the 10 min grid)
    public static TheoryData<string, double, double, double> Cases => new()
    {
        { "R2", 0.0, 0.0, 11.0 / 6.0 },
        { "R2", 0.22, 0.0, 2.5 },
        { "R3", 0.0, 0.15, 11.0 / 6.0 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PropagatedCovariance_MatchesMonteCarlo(string name, double dragCoefficientSigma,
        double reflectivityCoefficientSigma, double firstFailureHours)
    {
        // Arrange
        var referenceCase = _cases[name];
        var start = referenceCase.InitialState.Epoch;
        var epochs = new[] { start.Add(referenceCase.InitialState.Period()), start.AddHours(6.0), start.AddHours(24.0) }
            .Concat(Enumerable.Range(1, 36).Select(k => start.AddMinutes(10.0 * k))).Distinct().OrderBy(t => t).ToArray();

        // Act
        var results = CovarianceMonteCarlo.Run(referenceCase, NaifId, referenceCase.Window, epochs, 5000, Seed,
            dragCoefficientSigma > 0.0 ? dragCoefficientSigma : null,
            reflectivityCoefficientSigma > 0.0 ? reflectivityCoefficientSigma : null);

        // Assert
        string label = $"{name}{(dragCoefficientSigma > 0.0 ? ", Cd sampled" : "")}{(reflectivityCoefficientSigma > 0.0 ? ", Cr sampled" : "")}";
        foreach (var r in results)
        {
            _output.WriteLine(CovarianceMonteCarlo.Describe(label, start, r));
            Assert.True(r.WorstZ < 4.0, $"element-wise at {r.Epoch}: |z| = {r.WorstZ:F2}");
        }

        // Both tests pass at one revolution (specification, F3), and up to the boundary of the domain of validity,
        // measured on the 10 min grid and asserted within half an hour (the KS significance crosses α there by a few
        // samples); up to 1 h the sample covariance is P along every direction of the state, within the sampling band
        var oneRevolution = start.Add(referenceCase.InitialState.Period());
        var revolution = results.Single(r => r.Epoch == oneRevolution);
        Assert.True(revolution.Passes, $"one revolution: |z| {revolution.WorstZ:F2}, p {revolution.KsPValue:E2}");
        CovarianceMonteCarlo.AssertWithinTheSamplingBand(results.Where(r => r.Epoch <= start.AddHours(1.0)), 5000);
        var firstFailure = results.First(r => !r.Passes).Epoch;
        Assert.InRange((firstFailure - start).TotalHours, firstFailureHours - 0.5, firstFailureHours + 0.5);
        Assert.All(results.Where(r => r.Epoch < firstFailure), r => Assert.True(r.Passes));
    }
}
