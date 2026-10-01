// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Dense output (continuous extension) for the Prince-Dormand RK7(8) method.
/// Provides cubic Hermite interpolation within a completed step using the
/// endpoint positions, velocities, and accelerations. This yields C1-continuous
/// 3rd-order interpolation — sufficient for event root-finding.
/// </summary>
internal sealed class RK78DenseOutput
{
    private Vector3 _y0Pos, _y0Vel;
    private Vector3 _y1Pos, _y1Vel;
    private Vector3 _f0Pos, _f0Vel; // derivatives at start: f0Pos = v0, f0Vel = a0
    private Vector3 _f1Pos, _f1Vel; // derivatives at end: f1Pos = v1, f1Vel = a1
    private double _h;
    private bool _hasData;

    /// <summary>
    /// Store data from a completed RK step for later interpolation.
    /// </summary>
    /// <param name="y0Pos">Position at start of step.</param>
    /// <param name="y0Vel">Velocity at start of step.</param>
    /// <param name="y1Pos">Position at end of step.</param>
    /// <param name="y1Vel">Velocity at end of step.</param>
    /// <param name="a0">Acceleration at start of step.</param>
    /// <param name="a1">Acceleration at end of step.</param>
    /// <param name="h">Step size in seconds.</param>
    public void StoreStep(
        in Vector3 y0Pos, in Vector3 y0Vel,
        in Vector3 y1Pos, in Vector3 y1Vel,
        in Vector3 a0, in Vector3 a1,
        double h)
    {
        _y0Pos = y0Pos;
        _y0Vel = y0Vel;
        _y1Pos = y1Pos;
        _y1Vel = y1Vel;
        _f0Pos = y0Vel; // dr/dt at t0 = velocity at t0
        _f0Vel = a0;    // dv/dt at t0 = acceleration at t0
        _f1Pos = y1Vel; // dr/dt at t1 = velocity at t1
        _f1Vel = a1;    // dv/dt at t1 = acceleration at t1
        _h = h;
        _hasData = true;
    }

    /// <summary>
    /// Whether valid step data is stored.
    /// </summary>
    public bool HasData => _hasData;

    /// <summary>
    /// Interpolate state at fractional position theta within the step.
    /// Uses cubic Hermite interpolation for both position and velocity.
    /// </summary>
    /// <param name="theta">Fractional position in [0, 1], where theta = (t - t_n) / h.</param>
    /// <returns>Interpolated (position, velocity).</returns>
    public (Vector3 position, Vector3 velocity) Interpolate(double theta)
    {
        // Cubic Hermite basis functions:
        // H00(t) = 2t^3 - 3t^2 + 1
        // H10(t) = t^3 - 2t^2 + t
        // H01(t) = -2t^3 + 3t^2
        // H11(t) = t^3 - t^2
        double t = theta;
        double t2 = t * t;
        double t3 = t2 * t;

        double h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
        double h10 = t3 - 2.0 * t2 + t;
        double h01 = -2.0 * t3 + 3.0 * t2;
        double h11 = t3 - t2;

        // Position interpolation: p(theta) = H00*y0 + H10*h*f0 + H01*y1 + H11*h*f1
        var pos = _y0Pos * h00 + _f0Pos * (h10 * _h) + _y1Pos * h01 + _f1Pos * (h11 * _h);

        // Velocity interpolation: p(theta) = H00*v0 + H10*h*a0 + H01*v1 + H11*h*a1
        var vel = _y0Vel * h00 + _f0Vel * (h10 * _h) + _y1Vel * h01 + _f1Vel * (h11 * _h);

        return (pos, vel);
    }
}
