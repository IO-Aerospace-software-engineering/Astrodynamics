// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Occultation;

/// <summary>
/// The two paths of an occultation search behind <c>FindWindowsOnOccultationConstraint</c>: the managed search of the
/// library, and CSPICE's <c>gfoclt</c> when every participant is in the kernels. Both apply the same rules to the
/// shapes and to <see cref="OccultationType.None"/>.
/// </summary>
internal static class OccultationSearch
{
    /// <summary>
    /// The managed search: the windows, in TDB, where <paramref name="frontBody"/> occults <paramref name="target"/> as
    /// <paramref name="observer"/> sees them.
    /// </summary>
    /// <exception cref="ArgumentException">Both bodies are points, a body modeled as an ellipsoid has no radii, or two
    /// participants are the same.</exception>
    internal static IReadOnlyList<Window> Managed(in Window searchWindow, ILocalizable observer, ILocalizable target,
        ShapeType targetShape, INaifObject frontBody, ShapeType frontShape, OccultationType occultationType,
        Aberration aberration, in TimeSpan stepSize, OccultationSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frontBody);
        options ??= OccultationSearchOptions.Default;
        if (occultationType == OccultationType.None)
        {
            return Complement(Managed(searchWindow, observer, target, targetShape, frontBody, frontShape, OccultationType.Any, aberration, stepSize, options),
                searchWindow);
        }

        var front = frontBody as ILocalizable ?? CelestialItem.Create(frontBody.NaifId);
        var evaluator = new OccultationEvaluator(observer, target, targetShape, options.TargetFrame, front, frontShape, options.FrontFrame, aberration,
            searchWindow.StartDate.ToTDB());
        return OccultationFinder.FindWindows(evaluator, searchWindow, occultationType, stepSize, options.ConvergenceTolerance);
    }

    /// <summary>
    /// The search by CSPICE's <c>gfoclt</c>, for participants that are all in the kernels: the windows, in TDB.
    /// </summary>
    /// <remarks>
    /// <c>gfoclt</c> accepts only <c>ANY</c> with a point: the full occultation of a point back body and the annular
    /// one of a point front body, which are that condition, are searched as <c>ANY</c>, and the types that cannot happen
    /// with a point give no window, as in the managed search (<see cref="OccultationFinder.MarginsOf"/>).
    /// </remarks>
    /// <exception cref="ArgumentException">Both bodies are points.</exception>
    internal static IReadOnlyList<Window> Spice(in Window searchWindow, int observerId, int targetId, ShapeType targetShape,
        int frontBodyId, ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize,
        OccultationSearchOptions options)
    {
        if (targetShape == ShapeType.Point && frontShape == ShapeType.Point)
        {
            throw new ArgumentException("The front and back bodies cannot both be points.");
        }

        options ??= OccultationSearchOptions.Default;
        if (occultationType == OccultationType.None)
        {
            return Complement(Spice(searchWindow, observerId, targetId, targetShape, frontBodyId, frontShape, OccultationType.Any, aberration, stepSize, options),
                searchWindow);
        }

        bool hasPoint = targetShape == ShapeType.Point || frontShape == ShapeType.Point;
        if (OccultationFinder.MarginsOf(occultationType, frontShape == ShapeType.Point, targetShape == ShapeType.Point) == OccultationMargin.None)
        {
            return Array.Empty<Window>();
        }

        return SpiceAPI.Instance.FindWindowsOnOccultationConstraint(searchWindow, observerId, targetId, targetShape, frontBodyId, frontShape,
            hasPoint ? OccultationType.Any : occultationType, aberration, stepSize, options.FrontFrame, options.TargetFrame).ToArray();
    }

    /// <summary>The parts of the search window, in TDB, outside the given windows, which are sorted and disjoint.</summary>
    internal static IReadOnlyList<Window> Complement(IReadOnlyList<Window> windows, in Window searchWindow)
    {
        var result = new List<Window>();
        var cursor = searchWindow.StartDate.ToTDB();
        var end = searchWindow.EndDate.ToTDB();
        foreach (var window in windows.OrderBy(w => w.StartDate))
        {
            var start = window.StartDate.ToTDB();
            if (start > cursor)
            {
                result.Add(new Window(cursor, start));
            }

            var windowEnd = window.EndDate.ToTDB();
            if (windowEnd > cursor)
            {
                cursor = windowEnd;
            }
        }

        if (end > cursor)
        {
            result.Add(new Window(cursor, end));
        }

        return result;
    }
}
