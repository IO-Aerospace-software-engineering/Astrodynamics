// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// PI step-size controller for the Prince-Dormand RK7(8) integrator.
/// Implements the formula from Hairer, Norsett, Wanner, "Solving ODEs I", Section II.4:
///   h_new = h * clamp(safety * (1/err)^alpha * (prevErr)^beta, minFactor, maxFactor)
/// where alpha = 1/(p+1) = 1/8 for order-8 acceptance, and beta provides the PI stabilization.
/// On rejection, uses the more aggressive exponent 1/p = 1/7.
/// </summary>
internal sealed class RK78StepController
{
    // Order constants for Prince-Dormand 7(8)
    private const double AcceptExponent = 1.0 / 8.0; // 1/(p+1) for accepted steps
    private const double RejectExponent = 1.0 / 7.0; // 1/p for rejected steps
    private const double BetaPI = 0.04;               // PI stabilization (Hairer recommendation)

    public double SafetyFactor { get; }
    public double MinFactor { get; }
    public double MaxFactor { get; }
    public double MinStepSize { get; }
    public double MaxStepSize { get; }

    private double _prevError;
    private bool _hasPreviousError;

    public RK78StepController(
        double safetyFactor = 0.9,
        double minFactor = 0.2,
        double maxFactor = 5.0,
        double minStepSize = 1e-6,
        double maxStepSize = 86400.0)
    {
        SafetyFactor = safetyFactor;
        MinFactor = minFactor;
        MaxFactor = maxFactor;
        MinStepSize = minStepSize;
        MaxStepSize = maxStepSize;
        _prevError = 1.0;
        _hasPreviousError = false;
    }

    /// <summary>
    /// Evaluate step acceptance and propose a new step size.
    /// The error is already normalized: step is accepted when err &lt;= 1.0.
    /// </summary>
    /// <param name="currentH">Current step size in seconds (always positive).</param>
    /// <param name="err">Normalized error estimate (accepted when &lt;= 1.0).</param>
    /// <returns>(accepted, newH) where newH is the proposed next step size (positive).</returns>
    public (bool accepted, double newH) Evaluate(double currentH, double err)
    {
        // Guard against zero or negative error (perfect step)
        if (err <= 0.0)
            err = 1e-16;

        if (err <= 1.0)
        {
            // Accepted: compute growth factor
            double factor;
            if (_hasPreviousError)
            {
                // PI controller: blend current and previous error
                factor = SafetyFactor
                         * System.Math.Pow(1.0 / err, AcceptExponent)
                         * System.Math.Pow(_prevError, BetaPI);
            }
            else
            {
                // I controller (first accepted step, no history)
                factor = SafetyFactor * System.Math.Pow(1.0 / err, AcceptExponent);
            }

            factor = System.Math.Clamp(factor, MinFactor, MaxFactor);
            _prevError = err;
            _hasPreviousError = true;

            double newH = System.Math.Clamp(System.Math.Abs(currentH) * factor, MinStepSize, MaxStepSize);
            return (true, newH);
        }
        else
        {
            // Rejected: shrink step (more aggressive exponent)
            double factor = SafetyFactor * System.Math.Pow(1.0 / err, RejectExponent);
            factor = System.Math.Max(factor, MinFactor);

            double newH = System.Math.Clamp(System.Math.Abs(currentH) * factor, MinStepSize, MaxStepSize);
            // Do NOT update _prevError on rejection (preserves PI stability)
            return (false, newH);
        }
    }

    /// <summary>
    /// Reset the controller state (clears PI history).
    /// </summary>
    public void Reset()
    {
        _prevError = 1.0;
        _hasPreviousError = false;
    }
}
