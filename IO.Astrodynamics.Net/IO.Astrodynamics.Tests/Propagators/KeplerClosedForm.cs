// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The two-body orbit of a state, in closed form and written here, with no conversion of the library: its elements, the
/// times of a true anomaly, the state at a time, the intersections with a coplanar orbit and the delta-V of the apsis
/// changes. The reference of the gate tests of #569. Times in seconds of the time argument of the propagation from a
/// common origin (TT seconds about the Earth, TDB about the Moon).
/// </summary>
internal sealed class KeplerClosedForm
{
    /// <summary>Gravitational parameter, m³/s².</summary>
    public double Mu { get; }

    /// <summary>Semi-major axis, m.</summary>
    public double A { get; }

    /// <summary>Eccentricity.</summary>
    public double E { get; }

    /// <summary>Mean motion, rad/s.</summary>
    public double N { get; }

    /// <summary>Period, s.</summary>
    public double T { get; }

    /// <summary>Semi-latus rectum, m.</summary>
    public double P { get; }

    /// <summary>The mean anomaly at <see cref="T0"/>.</summary>
    public double M0 { get; }

    /// <summary>The time of the state the orbit was built from.</summary>
    public double T0 { get; }

    /// <summary>Unit vectors of the perifocal frame: to the perigee, 90° ahead of it in the plane, and the normal.</summary>
    public Vector3 Ph { get; }

    public Vector3 Qh { get; }

    public Vector3 Hh { get; }

    public KeplerClosedForm(double mu, Vector3 r, Vector3 v, double t0)
    {
        Mu = mu;
        T0 = t0;
        var h = r.Cross(v);
        Hh = h.Normalize();
        double rm = r.Magnitude();
        var ev = v.Cross(h) * (1.0 / mu) - r * (1.0 / rm);
        E = ev.Magnitude();
        A = 1.0 / (2.0 / rm - v * v / mu);
        P = h * h / mu;
        Ph = ev.Normalize();
        Qh = Hh.Cross(Ph);
        N = System.Math.Sqrt(mu / (A * A * A));
        T = 2.0 * System.Math.PI / N;
        M0 = MeanOfTrue(System.Math.Atan2(r * Qh, r * Ph));
    }

    /// <summary>The perigee radius, m.</summary>
    public double PerigeeRadius => A * (1.0 - E);

    /// <summary>The apogee radius, m.</summary>
    public double ApogeeRadius => A * (1.0 + E);

    /// <summary>The mean anomaly of the true anomaly <paramref name="nu"/>, in [0, 2π).</summary>
    public double MeanOfTrue(double nu)
    {
        double ea = 2.0 * System.Math.Atan2(System.Math.Sqrt(1.0 - E) * System.Math.Sin(nu / 2.0),
            System.Math.Sqrt(1.0 + E) * System.Math.Cos(nu / 2.0));
        return Normalized(ea - E * System.Math.Sin(ea));
    }

    /// <summary>An angle in [0, 2π).</summary>
    public static double Normalized(double angle)
    {
        angle %= 2.0 * System.Math.PI;
        return angle < 0.0 ? angle + 2.0 * System.Math.PI : angle;
    }

    /// <summary>The first time at or after <paramref name="after"/> at which the true anomaly is <paramref name="nu"/>.</summary>
    public double TimeOfTrue(double nu, double after)
    {
        double t = T0 + Normalized(MeanOfTrue(nu) - M0) / N;
        while (t < after) t += T;
        while (t - T >= after) t -= T;
        return t;
    }

    /// <summary>The first apogee (or perigee) at or after <paramref name="after"/>.</summary>
    public double NextApsis(double after, bool apogee) => TimeOfTrue(apogee ? System.Math.PI : 0.0, after);

    /// <summary>The state at the time <paramref name="t"/>, by Kepler's equation (Newton).</summary>
    public (Vector3 Position, Vector3 Velocity) StateAt(double t)
    {
        double m = M0 + N * (t - T0);
        double ea = m;
        for (int i = 0; i < 50; i++) ea -= (ea - E * System.Math.Sin(ea) - m) / (1.0 - E * System.Math.Cos(ea));
        double nu = 2.0 * System.Math.Atan2(System.Math.Sqrt(1.0 + E) * System.Math.Sin(ea / 2.0),
            System.Math.Sqrt(1.0 - E) * System.Math.Cos(ea / 2.0));
        return StateAtTrue(nu);
    }

    /// <summary>The state at the true anomaly <paramref name="nu"/>.</summary>
    public (Vector3 Position, Vector3 Velocity) StateAtTrue(double nu)
    {
        double r = P / (1.0 + E * System.Math.Cos(nu));
        var position = Ph * (r * System.Math.Cos(nu)) + Qh * (r * System.Math.Sin(nu));
        double k = System.Math.Sqrt(Mu / P);
        var velocity = Ph * (-k * System.Math.Sin(nu)) + Qh * (k * (E + System.Math.Cos(nu)));
        return (position, velocity);
    }

    /// <summary>The true anomaly of the direction <paramref name="direction"/> in the plane of the orbit.</summary>
    public double TrueAnomalyOf(Vector3 direction) => System.Math.Atan2(direction * Qh, direction * Ph);

    /// <summary>The speed at the radius <paramref name="r"/> (vis-viva).</summary>
    public double SpeedAt(double r) => System.Math.Sqrt(Mu * (2.0 / r - 1.0 / A));

    /// <summary>The delta-V that moves the apogee to <paramref name="targetApogee"/> at the perigee (vis-viva).</summary>
    public double ApogeeChange(double targetApogee) =>
        System.Math.Abs(System.Math.Sqrt(Mu * (2.0 / PerigeeRadius - 2.0 / (PerigeeRadius + targetApogee))) - SpeedAt(PerigeeRadius));

    /// <summary>The delta-V that moves the perigee to <paramref name="targetPerigee"/> at the apogee (vis-viva).</summary>
    public double PerigeeChange(double targetPerigee) =>
        System.Math.Abs(System.Math.Sqrt(Mu * (2.0 / ApogeeRadius - 2.0 / (ApogeeRadius + targetPerigee))) - SpeedAt(ApogeeRadius));

    /// <summary>
    /// The true anomalies, in this orbit, of its two intersections with the coplanar orbit <paramref name="other"/>:
    /// p₁ (1 + e₂ cos(ν − Δω)) = p₂ (1 + e₁ cos ν), Δω the angle from this perigee to the other's.
    /// </summary>
    public (double First, double Second, double DeltaOmega) IntersectionsWith(KeplerClosedForm other)
    {
        double dw = System.Math.Atan2(other.Ph * Qh, other.Ph * Ph);
        double a = P * other.E * System.Math.Cos(dw) - other.P * E;
        double b = P * other.E * System.Math.Sin(dw);
        double c = other.P - P;
        double phi = System.Math.Atan2(b, a);
        double ac = System.Math.Acos(c / System.Math.Sqrt(a * a + b * b));
        return (Normalized(phi + ac), Normalized(phi - ac), dw);
    }
}
