// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// C3 and C6 on the five reference cases: with the RTN process noise and the consider parameters of each case, Q, P
/// and the extended covariance stay symmetric and positive semi-definite (smallest eigenvalue of the correlation matrix
/// at least −1e-12 of its trace) over the whole case, at each hour and inside the steps.
/// </summary>
/// <remarks>
/// Measured (Linux, .NET 10, 2026-10-14), on the correlation matrix: the smallest eigenvalue is positive everywhere, at
/// least 2.4e-7 (R5) to 7.8e-5 (R4) of the trace for Q, and 9.8e-10 (R3) to 1.0e-7 (R4) for P. On the covariance itself,
/// where the position variances set the scale, 4.6e-13 to 2.2e-12 for Q and 4.1e-16 to 2.9e-13 for P (#384, before the
/// H3 review). The whole class runs in about 4 s.
/// </remarks>
[Trait("Category", "Validation")]
public class CovarianceValidationTests : IClassFixture<ReferenceCases>
{
    // The densities of the example of the specification (D2): 1e-12, 1e-11 and 1e-12 m²/s³ in R, T and N
    private static readonly RtnProcessNoise Noise = new(1e-12, 1e-11, 1e-12);

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public CovarianceValidationTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Theory]
    [InlineData("R1")]
    [InlineData("R2")]
    [InlineData("R3")]
    [InlineData("R4")]
    [InlineData("R5")]
    public void QPAndTheExtendedCovariance_AreSymmetricPositiveSemiDefinite(string name)
    {
        // Arrange: Cd as a consider parameter with drag, Cr with SRP, σ = 10 % of the coefficient
        var referenceCase = _cases[name];
        var start = CovariancePropagationTests.WithCovariance(referenceCase.InitialState);
        var parameters = (referenceCase.AtmosphericDrag ? ForceParameters.DragCoefficient : ForceParameters.None) |
                         (referenceCase.SolarRadiationPressure
                             ? ForceParameters.ReflectivityCoefficient
                             : ForceParameters.None);
        var consider = new ConsiderParameters(referenceCase.AtmosphericDrag ? 0.22 : null,
            referenceCase.SolarRadiationPressure ? 0.15 : null);
        var options = new VariationalOptions(parameters, Noise, PropagateCovariance: true,
            ConsiderParameters: parameters == ForceParameters.None ? null : consider);
        var spacecraft = new Spacecraft(-1966, "COVARIANCE", 100.0, 1000.0, new Clock("covariance", 65536), start, 10.0,
            2.2, null, 1.5);
        var solution = spacecraft.Propagate(new TimeSystem.Window(start.Epoch, start.Epoch + referenceCase.Duration),
            referenceCase.Bodies, new RK78Integrator(1e-11, 1e-11), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, TimeSpan.FromHours(1.0), options);
        int columns = 6 + (parameters == ForceParameters.None ? 0 : 1);
        int dimension = 6 + (options.ConsiderParameters == null ? 0 : 1);

        double worstQ = double.PositiveInfinity;
        double worstP = double.PositiveInfinity;
        double duration = referenceCase.Duration.TotalSeconds;
        for (double t = 0.0; t <= duration; t += System.Math.Min(3600.0, duration / 8.0))
        {
            foreach (double offset in new[] { 0.0, 437.3 })
            {
                double seconds = System.Math.Min(t + offset, duration);

                // Act
                var epoch = start.Epoch.AddSeconds(seconds);
                var y = new double[6 * columns];
                var q = new double[21];
                var p = new double[36];
                var extended = new double[dimension * dimension];
                solution.EvaluateVariational(epoch, y, q);
                solution.EvaluateCovariance(epoch, p);
                solution.EvaluateExtendedCovariance(epoch, extended);

                // Assert
                var fullQ = CovarianceAssert.Full(q);
                if (seconds > 0.0)
                {
                    CovarianceAssert.SymmetricPositiveSemiDefinite(fullQ, 6, $"{name}, Q at {seconds} s");
                    worstQ = System.Math.Min(worstQ, CovarianceAssert.SmallestCorrelationEigenvalueOverTrace(fullQ, 6));
                }

                CovarianceAssert.SymmetricPositiveSemiDefinite(p, 6, $"{name}, P at {seconds} s");
                CovarianceAssert.SymmetricPositiveSemiDefinite(extended, dimension,
                    $"{name}, extended covariance at {seconds} s");
                worstP = System.Math.Min(worstP, CovarianceAssert.SmallestCorrelationEigenvalueOverTrace(p, 6));
            }
        }

        _output.WriteLine($"{name}: smallest eigenvalue of the correlation matrix over its trace, Q {worstQ:E2}, P {worstP:E2}");
    }
}
