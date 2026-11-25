// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// The window searches of an observer: the windows where a target, seen from the observer, satisfies a constraint of
/// distance, of coordinate or of occultation (R15 of #668, lot L6, choice 1, decided by S. Guillet, 2026-11-19). The
/// receiver is the observer; the settings are one options record per kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Paths.</b> CSPICE searches (<c>gfdist_c</c>, <c>gfposc_c</c>, <c>gfoclt_c</c>) when the observer and the target are
/// from the kernels and one of them is a spacecraft (a <see cref="CelestialItem"/> target), or when the observer is a
/// spacecraft from the kernels and the target a site from the kernels; a coordinate search in a frame only the library
/// holds (TEME, CIRS, TIRS, a site or spacecraft frame the kernels do not define) and an occultation of a front body
/// not from the kernels are the library's. Every other search is the library's (<see cref="GeometryFinder"/>, the
/// managed occultation search): a site observer, a propagated spacecraft, two natural bodies, any other
/// <see cref="ILocalizable"/>. A search equals the former search of the target with the observer as argument (removed in
/// 11.0.0, §7 of the re-baseline), bit for bit, but for the occultation of a site observer, which CSPICE ran and the
/// library runs (choice 4).
/// </para>
/// <para>
/// <b>Settings.</b> The step is required (<see cref="ArgumentException"/> naming it), but for an occultation where the
/// search infers it; a null aberration is <see cref="Aberration.None"/>; a convergence tolerance applies to the library's
/// search and throws on CSPICE's, whose tolerance is 1 µs; an <see cref="WindowSearchOptions.AdjustValue"/> applies to an
/// absolute extreme and throws with another relation and on <c>gfdist_c</c>, which takes none.
/// </para>
/// <para>
/// <b>Labels.</b> The windows are labelled in the scale of the search window's start: an end at a bound of the search
/// window is that bound, every other end the same instant in that scale (the former CSPICE and occultation searches
/// labelled TDB), but an end inside a UTC leap second, which no UTC label holds, keeps its TDB label. On a UTC or TT
/// window, <see cref="Window.Length"/> then counts TT seconds (#508).
/// </para>
/// </remarks>
public static class WindowSearches
{
    /// <summary>
    /// The windows where the distance of <paramref name="target"/> from the observer satisfies the relation.
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="window">The search window.</param>
    /// <param name="target">The target.</param>
    /// <param name="relationalOperator">The relation.</param>
    /// <param name="value">The distance, m; ignored by the extremes.</param>
    /// <param name="options">The step (required), the aberration, the convergence tolerance and the adjustment value.</param>
    /// <returns>The windows, in time order, labelled in the scale of the search window's start.</returns>
    /// <exception cref="ArgumentNullException">The observer or the target is null.</exception>
    /// <exception cref="ArgumentException">The step is not given, an adjustment value is given with a relation other than an
    /// absolute extreme or on CSPICE's <c>gfdist_c</c>, or a convergence tolerance on CSPICE's search (<c>options</c>).</exception>
    /// <exception cref="InvalidOperationException">On the path of CSPICE, SPICE cannot run the search, with its message; on
    /// the library's, a state cannot be read.</exception>
    public static IReadOnlyList<Window> FindDistanceWindows(this ILocalizable observer, in Window window, ILocalizable target,
        RelationnalOperator relationalOperator, double value, WindowSearchOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        options ??= WindowSearchOptions.Default;
        WindowSearchSettings.CheckAdjustValue(options, relationalOperator);
        var step = WindowSearchSettings.RequireStep(options.Step, nameof(WindowSearchOptions));
        var aberration = options.Aberration ?? Aberration.None;
        if (SearchesOnSpice(observer, target))
        {
            WindowSearchSettings.RejectToleranceOnCspice(options.ConvergenceTolerance, nameof(WindowSearchOptions), "gfdist_c");
            if (options.AdjustValue.HasValue)
            {
                throw new ArgumentException(
                    "WindowSearchOptions.AdjustValue cannot apply: CSPICE's gfdist_c runs this search, and the library gives it no adjustment value. Leave it null.",
                    nameof(options));
            }

            // SpiceAPI takes metres: the native layer converts them to km for gfdist_c (#466)
            return WindowSearchSettings.InScaleOf(
                SpiceAPI.Instance.FindWindowsOnDistanceConstraint(window, observer.NaifId, target.NaifId, relationalOperator, value, aberration, step), window);
        }

        return GeometryFinder.Find(window, date => target.GetEphemeris(date, observer, Frame.ICRF, aberration).Position.Magnitude(), relationalOperator,
            value, options.AdjustValue ?? 0.0, step, GeometryFinder.ToleranceOf(options.ConvergenceTolerance));
    }

    /// <summary>
    /// The windows where a coordinate of <paramref name="target"/> seen from the observer in <paramref name="frame"/>
    /// satisfies the relation (the rules of <see cref="GeometryFinder"/> on the library's path, <c>gfposc_c</c>'s on
    /// CSPICE's).
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="window">The search window.</param>
    /// <param name="target">The target.</param>
    /// <param name="frame">The frame of the coordinates.</param>
    /// <param name="coordinateSystem">The coordinate system.</param>
    /// <param name="coordinate">The coordinate.</param>
    /// <param name="relationalOperator">The relation.</param>
    /// <param name="value">The value, m or rad; ignored by the extremes.</param>
    /// <param name="options">The step (required), the aberration, the convergence tolerance and the adjustment value.</param>
    /// <returns>The windows, in time order, labelled in the scale of the search window's start.</returns>
    /// <exception cref="ArgumentNullException">The observer or the target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">With a comparison operator, <paramref name="value"/> is an angle outside
    /// its range, or NaN (R11 of #668): a right ascension or a planetographic longitude outside [0, 2π], a geodetic
    /// longitude outside [−π, π], a declination or a latitude outside [−π/2, π/2], in radians.</exception>
    /// <exception cref="ArgumentException">The step is not given, an adjustment value is given with a relation other than an
    /// absolute extreme, or a convergence tolerance on CSPICE's search (<c>options</c>); SPICE does not know the frame
    /// (<c>frame</c>).</exception>
    /// <exception cref="InvalidOperationException">On the path of CSPICE, SPICE cannot run the search, with its message; on
    /// the library's, a state cannot be read.</exception>
    public static IReadOnlyList<Window> FindCoordinateWindows(this ILocalizable observer, in Window window, ILocalizable target, Frame frame,
        CoordinateSystem coordinateSystem, Coordinate coordinate, RelationnalOperator relationalOperator, double value, WindowSearchOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        GeometryFinder.CheckCoordinateThreshold(coordinateSystem, coordinate, relationalOperator, value);
        options ??= WindowSearchOptions.Default;
        WindowSearchSettings.CheckAdjustValue(options, relationalOperator);
        var step = WindowSearchSettings.RequireStep(options.Step, nameof(WindowSearchOptions));
        var aberration = options.Aberration ?? Aberration.None;
        double adjust = options.AdjustValue ?? 0.0;
        // CSPICE searches only in a frame SPICE reads as the library does (third review of #690)
        if (SearchesOnSpice(observer, target) && !SpiceAPI.IsManagedFrame(frame))
        {
            WindowSearchSettings.RejectToleranceOnCspice(options.ConvergenceTolerance, nameof(WindowSearchOptions), "gfposc_c");
            // SPICE uses km for the distance coordinates, the library m; angles are radians in both
            bool isDistance = coordinate is Coordinate.X or Coordinate.Y or Coordinate.Z or Coordinate.Range or Coordinate.Radius or Coordinate.Altitude;
            double scale = isDistance ? 1000.0 : 1.0;
            return WindowSearchSettings.InScaleOf(SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(window, observer.NaifId, target.NaifId, frame,
                coordinateSystem, coordinate, relationalOperator, value / scale, adjust / scale, aberration, step), window);
        }

        return GeometryFinder.Find(window, CelestialItem.CoordinateQuantity(target, observer, frame, coordinateSystem, coordinate, aberration, window.StartDate),
            relationalOperator, value, adjust, step, GeometryFinder.ToleranceOf(options.ConvergenceTolerance));
    }

    /// <summary>
    /// The windows when <paramref name="front"/> occults <paramref name="target"/> (the back body) as the observer sees
    /// them, with the types, shapes and frames of CSPICE's <c>gfoclt</c> (see the reference page on occultations).
    /// </summary>
    /// <param name="observer">The observer.</param>
    /// <param name="window">The search window.</param>
    /// <param name="target">The occulted body.</param>
    /// <param name="front">The occulting body.</param>
    /// <param name="options">The step, the aberration, the shapes and frames of the bodies, the type of occultation and the
    /// convergence tolerance.</param>
    /// <returns>The windows, in time order, labelled in the scale of the search window's start.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The step is not given where the search does not infer it, a convergence tolerance
    /// is given on CSPICE's search, SPICE does not know a frame of the options (<c>options</c>, SPICE's error as inner
    /// exception); both bodies are points, a body modeled as an ellipsoid has no radii, or two participants are the
    /// same.</exception>
    /// <exception cref="InvalidOperationException">On the path of CSPICE, SPICE cannot run the search, with its message; on
    /// the library's, a state cannot be read.</exception>
    public static IReadOnlyList<Window> FindOccultationWindows(this ILocalizable observer, in Window window, ILocalizable target, INaifObject front,
        OccultationSearchOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(front);
        options ??= OccultationSearchOptions.Default;
        var aberration = options.Aberration ?? Aberration.None;
        var targetShape = options.TargetShape ?? OccultationBodyModel.DefaultShape(target);
        var frontShape = options.FrontShape ?? OccultationBodyModel.DefaultShape(front as ILocalizable ?? CelestialItem.Create(front.NaifId));
        try
        {
            if (target is CelestialItem item && item.SearchesOnSpice(observer) && front is ILocalizable frontItem && frontItem.IsSpiceBacked)
            {
                WindowSearchSettings.RejectToleranceOnCspice(options.ConvergenceTolerance, nameof(OccultationSearchOptions), "gfoclt_c");
                return WindowSearchSettings.InScaleOf(OccultationSearch.Spice(window, observer.NaifId, target.NaifId, targetShape, front.NaifId, frontShape,
                    options.OccultationType, aberration, WindowSearchSettings.RequireStep(options.Step, nameof(OccultationSearchOptions)), options), window);
            }

            return WindowSearchSettings.InScaleOf(OccultationSearch.Managed(window, observer, target, targetShape, front, frontShape, options.OccultationType,
                aberration, OccultationStep(window, observer, target, front, options.Step), options), window);
        }
        catch (ArgumentException e) when (e.ParamName is "frontFrame" or "targetFrame")
        {
            throw FrameError(e, nameof(OccultationSearchOptions), e.ParamName == "frontFrame" ? "FrontFrame" : "TargetFrame");
        }
    }

    /// <summary>
    /// The step of the library's occultation search (decision 106 of #668, decided by S. Guillet, 2026-11-25): for a
    /// propagated or TLE spacecraft observer or target whose centre of propagation is the front body, inferred when not
    /// given, the smallest period about it over the window over 16, capped at half the window (one tick at least), and a
    /// step given above half that period refused; required otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">The step is not given and no spacecraft infers it (<c>options</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The step given exceeds half the period (<c>options</c>).</exception>
    private static TimeSpan OccultationStep(in Window window, ILocalizable observer, ILocalizable target, INaifObject front, TimeSpan? step)
    {
        TimeSpan? period = null;
        foreach (var participant in new[] { observer, target }.OfType<Spacecraft.Spacecraft>())
        {
            var p = participant.SmallestPeriodAbout(front, window);
            if (p.HasValue && (period is null || p < period)) period = p;
        }

        if (period is null)
        {
            return step ?? throw new ArgumentException(
                "OccultationSearchOptions.Step is required: give the step of the samples, shorter than the shortest window or gap to find. The search " +
                "infers it only for a propagated or TLE spacecraft whose centre of propagation is the front body, on the library's search.", "options");
        }

        if (step is { } given)
        {
            if (given > period.Value / 2)
            {
                throw new ArgumentOutOfRangeException("options", given,
                    $"OccultationSearchOptions.Step {given} exceeds half the period of the orbit about {front.Name}, {period.Value}: a step of at most half the period finds every eclipse.");
            }

            return given;
        }

        var halfWindow = (window.EndDate.ToTDB() - window.StartDate.ToTDB()) / 2;
        var cap = halfWindow > TimeSpan.Zero ? halfWindow : TimeSpan.FromTicks(1);
        var inferred = period.Value / 16;
        return inferred < cap ? inferred : cap;
    }

    /// <summary>
    /// A frame of the options SPICE does not know: the error of the search, its parameter the options (IC-14 of lot L6 of
    /// #668), SPICE's error kept as inner exception.
    /// </summary>
    internal static ArgumentException FrameError(ArgumentException error, string record, string property) =>
        new($"{record}.{property} names a frame SPICE does not know.", "options", error.InnerException ?? error);

    // Whether CSPICE runs a distance or coordinate search of the target seen from the observer
    private static bool SearchesOnSpice(ILocalizable observer, ILocalizable target) => target switch
    {
        CelestialItem item => item.SearchesOnSpice(observer),
        Site site => site.SearchesOnSpice(observer),
        _ => false
    };
}
