// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Celestial Intermediate Reference System (CIRS).
/// Uses IAU 2006 precession with IAU 2000B nutation.
/// The CIO-based Q(t) matrix produced by the model is <c>ICRF → CIRS</c>, so
/// <see cref="GetStateOrientationToICRF(Time)"/> returns its transpose, <c>CIRS → ICRF</c>.
/// </summary>
/// <remarks>
/// CIRS depends on precession-nutation only, so it takes no Earth orientation parameters:
/// UT1-UTC enters at <see cref="TirsFrame"/>. Polar motion belongs to the TIRS to ITRS step, which the
/// library does not provide yet.
/// </remarks>
public sealed class CirsFrame : Frame
{
    public CirsFrame() : base("CIRS")
    {
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(date);

        return _stateOrientationsToICRF.GetOrAdd(date, ComputeOrientation);
    }

    private StateOrientation ComputeOrientation(Time date)
    {
        // Compute the standard CIO-based matrix Q(t), which maps ICRF -> CIRS.
        var tdb = date.ConvertTo(TimeFrame.TDBFrame);
        double t = tdb.Centuries();

        var (x, y) = Iau2006Model.CipXY(t);
        double s = Iau2006Model.CioLocator(t, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        // This API returns source -> ICRF, so transpose Q(t) to obtain CIRS -> ICRF.
        var cirs2icrf = qt.Transpose();
        var rotation = cirs2icrf.ToQuaternion();

        // Angular velocity of ICRF relative to CIRS, in CIRS axes (convention of the SPICE frames, see
        // GetStateOrientationToICRF), derived numerically from the returned CIRS -> ICRF rotation.
        var angularVelocity = ComputeAngularVelocity(t);

        return new StateOrientation(rotation, angularVelocity, date, this);
    }

    private static Quaternion ComputeOrientationQuaternion(double t)
    {
        var (x, y) = Iau2006Model.CipXY(t);
        double s = Iau2006Model.CioLocator(t, x, y);
        return Iau2006Model.PrecessionNutationMatrix(x, y, s).Transpose().ToQuaternion().Normalize();
    }

    private static Vector3 ComputeAngularVelocity(double t)
    {
        // Central difference over +/- 100 s. The rate is only 2e-12 to 8e-12 rad/s, so the step is set by the rounding
        // of the quaternion components (about 1e-16 / 2 dt, near 1e-7 of the rate here, 1e-3 over +/- 0.01 s), while
        // the truncation error, driven by nutation periods of days, stays below 1e-7.
        double dt = 100.0; // seconds
        double dtCenturies = dt / (36525.0 * 86400.0);

        double tMinus = t - dtCenturies;
        double tPlus = t + dtCenturies;

        var qm = ComputeOrientationQuaternion(tMinus);
        var qp = ComputeOrientationQuaternion(tPlus);

        // Central difference on the returned orientation quaternion q(t), where q maps CIRS -> ICRF.
        var dq = new Quaternion(
            (qp.W - qm.W) / (2.0 * dt),
            new Vector3(
                (qp.VectorPart.X - qm.VectorPart.X) / (2.0 * dt),
                (qp.VectorPart.Y - qm.VectorPart.Y) / (2.0 * dt),
                (qp.VectorPart.Z - qm.VectorPart.Z) / (2.0 * dt)));

        // q maps CIRS to ICRF, so 2 q^-1 dq/dt is the angular velocity of CIRS relative to ICRF, in CIRS axes.
        // The frames return the opposite, the angular velocity of ICRF relative to CIRS.
        var qCenter = ComputeOrientationQuaternion(t);
        var qInverse = qCenter.Conjugate() / (qCenter.Magnitude() * qCenter.Magnitude());
        var omegaQ = qInverse * dq;
        return new Vector3(-2.0 * omegaQ.VectorPart.X, -2.0 * omegaQ.VectorPart.Y, -2.0 * omegaQ.VectorPart.Z);
    }
}
