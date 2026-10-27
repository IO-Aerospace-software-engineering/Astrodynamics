// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Occultation;

/// <summary>
/// The geometry of an occultation at any epoch: reads the states of the observer and of the two bodies and the
/// orientation of their body-fixed frames, and builds the <see cref="OccultationGeometry"/> of the instant. Immutable:
/// it keeps no state between evaluations, so one evaluator may serve concurrent calls.
/// </summary>
/// <remarks>
/// <para>
/// The data are read, never written: the states through <see cref="ILocalizable.GetGeometricStateRelativeTo"/> (the
/// propagated states of a spacecraft, the position of a site, the kernels of a body through the data provider, which
/// serializes the calls to CSPICE) and the orientations through <see cref="Frame"/>. The light time is that of CSPICE's
/// <c>spkltc</c> (N0067, read from its source): from the geometric distance at the epoch t, one iteration for LT, up to
/// five until the light time changes by less than 1e-17 of the epoch for CN; the position is the target's at t − lt
/// (t + lt for the transmission cases) minus the observer's at t, the light time is the distance of that position over
/// c. As in <c>zzgfocu</c>, each body's frame is read at its own light-time corrected epoch (<c>zzcorepc</c>) and
/// stellar aberration is ignored, since it does not improve the determination of an occultation.
/// </para>
/// <para>
/// The positions are taken relative to the reference body of the observer (the central body of a spacecraft, the body
/// of a site, the solar system barycenter otherwise), plus the motion of that body during the light time.
/// </para>
/// </remarks>
internal sealed class OccultationEvaluator
{
    // SPKLTC's iterations for the converged Newtonian light time, and its convergence threshold relative to the epoch
    private const int ConvergedIterations = 5;
    private const double ConvergenceThreshold = 1e-17;

    private readonly ILocalizable _observer;
    private readonly CelestialItem _reference;
    private readonly OccultationBodyModel _front;
    private readonly OccultationBodyModel _back;
    private readonly int _iterations;
    private readonly double _lightTimeSign;

    /// <summary>
    /// The evaluator of the occultation of <paramref name="back"/> by <paramref name="front"/> as
    /// <paramref name="observer"/> sees them.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="back">The occulted body.</param>
    /// <param name="backShape">Its shape.</param>
    /// <param name="backFrame">The body-fixed frame of its ellipsoid, or null for its own frame.</param>
    /// <param name="front">The occulting body.</param>
    /// <param name="frontShape">Its shape.</param>
    /// <param name="frontFrame">The body-fixed frame of its ellipsoid, or null for its own frame.</param>
    /// <param name="aberration">The aberration correction: none, or the light time (LT, CN, XLT, XCN; the stellar
    /// aberration of the "+S" corrections is ignored, as <c>gfoclt</c> does).</param>
    /// <param name="sphereEpoch">The epoch at which the body of larger apparent size is chosen to be mapped to the unit
    /// sphere, for every evaluation.</param>
    /// <exception cref="ArgumentException">Both bodies are points, a body modeled as an ellipsoid has no radii, or a body
    /// is the observer.</exception>
    internal OccultationEvaluator(ILocalizable observer, ILocalizable back, ShapeType backShape, Frame backFrame,
        ILocalizable front, ShapeType frontShape, Frame frontFrame, Aberration aberration, in Time sphereEpoch)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(front);
        if (backShape == ShapeType.Point && frontShape == ShapeType.Point)
        {
            throw new ArgumentException("The front and back bodies cannot both be points.");
        }

        if (back.NaifId == front.NaifId || back.NaifId == observer.NaifId || front.NaifId == observer.NaifId)
        {
            throw new ArgumentException("The observer, the front body and the back body must be distinct.");
        }

        _reference = CelestialItem.GetReferenceBodyFor(observer);
        _front = new OccultationBodyModel(front, frontShape, frontFrame);
        _back = new OccultationBodyModel(back, backShape, backFrame);
        (_iterations, _lightTimeSign) = aberration switch
        {
            Aberration.None => (0, 0.0),
            Aberration.LT or Aberration.LTS => (1, -1.0),
            Aberration.CN or Aberration.CNS => (ConvergedIterations, -1.0),
            Aberration.XLT or Aberration.XLTS => (1, 1.0),
            Aberration.XCN or Aberration.XCNS => (ConvergedIterations, 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(aberration))
        };

        if (frontShape == ShapeType.Point)
        {
            SphereIsFront = false;
        }
        else if (backShape == ShapeType.Point)
        {
            SphereIsFront = true;
        }
        else
        {
            // The body of larger apparent size becomes the unit sphere (zzocced's choice), fixed for the search
            Bodies(sphereEpoch, out var frontBody, out var backBody);
            SphereIsFront = OccultationGeometry.SphereIsFront(frontBody, backBody);
        }
    }

    /// <summary>Whether the front body is the one mapped to the unit sphere.</summary>
    internal bool SphereIsFront { get; }

    /// <summary>Whether the front body is a point.</summary>
    internal bool FrontIsPoint => _front.IsPoint;

    /// <summary>Whether the back body is a point.</summary>
    internal bool BackIsPoint => _back.IsPoint;

    /// <summary>The margins and, if asked, the order of the bodies at an epoch.</summary>
    internal OccultationState Evaluate(in Time epoch, OccultationMargin margins, bool order)
    {
        Bodies(epoch, out var front, out var back);
        return OccultationGeometry.Evaluate(front, back, SphereIsFront, margins, order);
    }

    /// <summary>The two bodies as the observer sees them at an epoch.</summary>
    internal void Bodies(in Time epoch, out OccultationBody front, out OccultationBody back)
    {
        var observer = _observer.GetGeometricStateRelativeTo(epoch, _reference).Position;
        // The position of the reference body from the solar system barycenter at the epoch, for the light time only
        Vector3? referenceAtEpoch = null;
        front = Body(_front, epoch, observer, ref referenceAtEpoch);
        back = Body(_back, epoch, observer, ref referenceAtEpoch);
    }

    private OccultationBody Body(OccultationBodyModel model, in Time epoch, in Vector3 observer, ref Vector3? referenceAtEpoch)
    {
        var position = model.Item.GetGeometricStateRelativeTo(epoch, _reference).Position - observer;
        var orientationEpoch = epoch;
        if (_iterations > 0)
        {
            referenceAtEpoch ??= ReferenceFromBarycenter(epoch);
            double epochSeconds = epoch.TimeSpanFromJ2000().TotalSeconds;
            double lightTime = position.Magnitude() / Constants.C;
            for (int i = 0; i < _iterations; i++)
            {
                // The states are read at the corrected epoch rounded to the 100 ns of a Time, and carried to the exact
                // one by their velocity: a 50 ns error would move the Earth by 1.5 mm, and a grazing contact by 1 µs
                double shift = _lightTimeSign * lightTime;
                var corrected = Shift(epoch, shift, out double residual);
                var target = model.Item.GetGeometricStateRelativeTo(corrected, _reference);
                position = target.Position + target.Velocity * residual - observer;
                if (_reference.NaifId != 0)
                {
                    var reference = _reference.GetGeometricStateFromICRF(corrected).ToStateVector();
                    position += reference.Position + reference.Velocity * residual - referenceAtEpoch.Value;
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

        return model.ToBody(position, orientationEpoch);
    }

    private Vector3 ReferenceFromBarycenter(in Time epoch)
    {
        return _reference.NaifId == 0 ? Vector3.Zero : _reference.GetGeometricStateFromICRF(epoch).ToStateVector().Position;
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
