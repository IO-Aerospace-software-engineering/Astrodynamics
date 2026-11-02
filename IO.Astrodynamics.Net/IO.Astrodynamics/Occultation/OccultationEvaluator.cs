// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
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
/// serializes the calls to CSPICE) and the orientations through <see cref="Frame"/>. The positions and the light time
/// are those of <see cref="ApparentPositionReader"/> (CSPICE's <c>spkltc</c>, relative to the reference body of the
/// observer). As in <c>zzgfocu</c>, each body's frame is read at its own light-time corrected epoch (<c>zzcorepc</c>) and
/// stellar aberration is ignored, since it does not improve the determination of an occultation.
/// </para>
/// </remarks>
internal sealed class OccultationEvaluator
{
    private readonly ApparentPositionReader _reader;
    private readonly OccultationBodyModel _front;
    private readonly OccultationBodyModel _back;

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
        ArgumentNullException.ThrowIfNull(observer);
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

        _reader = new ApparentPositionReader(observer, aberration, false, false);
        _front = new OccultationBodyModel(front, frontShape, frontFrame);
        _back = new OccultationBodyModel(back, backShape, backFrame);

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
        var observer = _reader.Observer(epoch);
        // The state of the reference body from the solar system barycenter at the epoch, for the light time only
        StateVector referenceAtEpoch = null;
        front = Body(_front, epoch, observer, ref referenceAtEpoch);
        back = Body(_back, epoch, observer, ref referenceAtEpoch);
    }

    private OccultationBody Body(OccultationBodyModel model, in Time epoch, StateVector observer, ref StateVector referenceAtEpoch)
    {
        var position = _reader.Position(model.Item, epoch, observer, ref referenceAtEpoch, out var orientationEpoch);
        return model.ToBody(position, orientationEpoch);
    }
}
