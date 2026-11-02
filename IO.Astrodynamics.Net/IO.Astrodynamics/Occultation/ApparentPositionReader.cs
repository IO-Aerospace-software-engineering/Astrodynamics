// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Occultation;

/// <summary>
/// The position of a body as an observer sees it, with the light time of CSPICE's <c>spkltc</c> and, when asked, the
/// stellar aberration of <c>stelab</c>: the reading shared by the occultation and the field-of-view evaluators. Immutable:
/// it keeps no state between readings, so one reader may serve concurrent calls.
/// </summary>
/// <remarks>
/// <para>
/// The light time is that of CSPICE's <c>spkltc</c> (N0067, read from its source): from the geometric distance at the
/// epoch t, one iteration for LT, up to five until the light time changes by less than 1e-17 of the epoch for CN; the
/// position is the target's at t − lt (t + lt for the transmission cases) minus the observer's at t, the light time is
/// the distance of that position over c. The positions are taken relative to the reference body of the observer (the
/// central body of a spacecraft, the body of a site, the solar system barycenter otherwise), plus the motion of that body
/// during the light time.
/// </para>
/// <para>
/// The stellar aberration is that of CSPICE's <c>stelab</c> and <c>stlabx</c> (N0067, read from their sources), applied
/// after the light time as <c>spkapo</c> does: the position is rotated by asin(|u × v/c|) about u × v/c, u its direction
/// and v the observer's velocity relative to the solar system barycenter, −v for the transmission cases.
/// </para>
/// <para>
/// A star may be read as a direction: without light time, the stellar aberration applied, as CSPICE's <c>gfrfov</c>
/// reads a ray. Its light time would read it, and the reference body of the observer, years before the epoch.
/// </para>
/// </remarks>
internal sealed class ApparentPositionReader
{
    // SPKLTC's iterations for the converged Newtonian light time, and its convergence threshold relative to the epoch
    private const int ConvergedIterations = 5;
    private const double ConvergenceThreshold = 1e-17;

    private readonly ILocalizable _observer;
    private readonly int _iterations;
    private readonly double _lightTimeSign;
    private readonly bool _stellar;
    private readonly bool _starsAsDirections;

    /// <summary>The reader of the positions seen by <paramref name="observer"/>.</summary>
    /// <param name="observer">The observer.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <param name="stellar">Whether the stellar aberration of the "+S" corrections is applied; it is ignored otherwise,
    /// as <c>gfoclt</c> ignores it.</param>
    /// <param name="starsAsDirections">Whether a <see cref="Star"/> is read without light time.</param>
    internal ApparentPositionReader(ILocalizable observer, Aberration aberration, bool stellar, bool starsAsDirections)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        Reference = CelestialItem.GetReferenceBodyFor(observer);
        (_iterations, _lightTimeSign) = aberration switch
        {
            Aberration.None => (0, 0.0),
            Aberration.LT or Aberration.LTS => (1, -1.0),
            Aberration.CN or Aberration.CNS => (ConvergedIterations, -1.0),
            Aberration.XLT or Aberration.XLTS => (1, 1.0),
            Aberration.XCN or Aberration.XCNS => (ConvergedIterations, 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(aberration))
        };
        _stellar = stellar && aberration is Aberration.LTS or Aberration.CNS or Aberration.XLTS or Aberration.XCNS;
        _starsAsDirections = starsAsDirections;
    }

    /// <summary>The reference body of the observer.</summary>
    internal CelestialItem Reference { get; }

    /// <summary>The observer's geometric state relative to <see cref="Reference"/> at an epoch.</summary>
    internal StateVector Observer(in Time epoch)
    {
        return _observer.GetGeometricStateRelativeTo(epoch, Reference);
    }

    /// <summary>
    /// The position of <paramref name="item"/> from the observer at <paramref name="epoch"/>, corrected, in ICRF, and the
    /// epoch at which its body-fixed frame is read (the light-time corrected epoch, as <c>zzcorepc</c>).
    /// </summary>
    /// <param name="item">The body.</param>
    /// <param name="epoch">The epoch.</param>
    /// <param name="observer">The observer's geometric state relative to <see cref="Reference"/> at the epoch
    /// (<see cref="Observer"/>).</param>
    /// <param name="referenceAtEpoch">The state of the reference body from the solar system barycenter at the epoch,
    /// read once, when the light time or the stellar aberration needs it, and shared by the readings of one epoch.</param>
    /// <param name="orientationEpoch">The epoch of the body's orientation.</param>
    internal Vector3 Position(ILocalizable item, in Time epoch, StateVector observer, ref StateVector referenceAtEpoch, out Time orientationEpoch)
    {
        var observerPosition = observer.Position;
        var position = item.GetGeometricStateRelativeTo(epoch, Reference).Position - observerPosition;
        orientationEpoch = epoch;
        if (_iterations > 0 && !(_starsAsDirections && item is Star))
        {
            double epochSeconds = epoch.TimeSpanFromJ2000().TotalSeconds;
            double lightTime = position.Magnitude() / Constants.C;
            for (int i = 0; i < _iterations; i++)
            {
                // The states are read at the corrected epoch rounded to the 100 ns of a Time, and carried to the exact
                // one by their velocity: a 50 ns error would move the Earth by 1.5 mm, and a grazing contact by 1 µs
                double shift = _lightTimeSign * lightTime;
                var corrected = Shift(epoch, shift, out double residual);
                var target = item.GetGeometricStateRelativeTo(corrected, Reference);
                position = target.Position + target.Velocity * residual - observerPosition;
                if (Reference.NaifId != 0)
                {
                    referenceAtEpoch ??= ReferenceFromBarycenter(epoch);
                    var reference = Reference.GetGeometricStateFromICRF(corrected).ToStateVector();
                    position += reference.Position + reference.Velocity * residual - referenceAtEpoch.Position;
                }

                double previous = lightTime;
                lightTime = position.Magnitude() / Constants.C;
                double change = System.Math.Abs(lightTime - previous) / System.Math.Max(1.0, System.Math.Abs(epochSeconds + shift));
                if (change <= ConvergenceThreshold)
                {
                    break;
                }
            }

            orientationEpoch = Shift(epoch, _lightTimeSign * lightTime);
        }

        if (_stellar)
        {
            // The observer's velocity from the solar system barycenter; stlabx (transmission) takes its opposite
            var velocity = observer.Velocity;
            if (Reference.NaifId != 0)
            {
                referenceAtEpoch ??= ReferenceFromBarycenter(epoch);
                velocity += referenceAtEpoch.Velocity;
            }

            position = StellarAberration(position, _lightTimeSign > 0.0 ? velocity.Inverse() : velocity);
        }

        return position;
    }

    /// <summary>
    /// CSPICE's <c>stelab</c> (N0067): <paramref name="position"/> rotated by asin(|u × v/c|) about u × v/c, u its
    /// direction and v <paramref name="observerVelocity"/>, the observer's velocity from the solar system barycenter.
    /// </summary>
    internal static Vector3 StellarAberration(in Vector3 position, in Vector3 observerVelocity)
    {
        var u = position.Normalize();
        var h = u.Cross(observerVelocity / Constants.C);
        double sinPhi = h.Magnitude();
        if (sinPhi == 0.0)
        {
            return position;
        }

        // vrotv: Rodrigues' rotation of the position about h by phi
        double phi = System.Math.Asin(sinPhi);
        var axis = h / sinPhi;
        double cos = System.Math.Cos(phi);
        double sin = System.Math.Sin(phi);
        return position * cos + axis.Cross(position) * sin + axis * ((axis * position) * (1.0 - cos));
    }

    private StateVector ReferenceFromBarycenter(in Time epoch)
    {
        return Reference.GetGeometricStateFromICRF(epoch).ToStateVector();
    }

    /// <summary>
    /// The epoch <paramref name="seconds"/> later, rounded to the 100 ns of a <see cref="Time"/>, and what the rounding
    /// left out, in seconds.
    /// </summary>
    internal static Time Shift(in Time epoch, double seconds, out double residual)
    {
        long ticks = (long)System.Math.Round(seconds * TimeSpan.TicksPerSecond);
        residual = seconds - ticks / (double)TimeSpan.TicksPerSecond;
        return epoch.AddTicks(ticks);
    }

    /// <summary>The epoch <paramref name="seconds"/> later, to the 100 ns of a <see cref="Time"/>.</summary>
    internal static Time Shift(in Time epoch, double seconds)
    {
        return epoch.AddTicks((long)System.Math.Round(seconds * TimeSpan.TicksPerSecond));
    }
}
