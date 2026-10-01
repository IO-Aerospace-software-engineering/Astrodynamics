using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.TrackingData;

public class LroTrackingDataTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");
    private static readonly FileInfo LroSpkPath = new("Data/LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp");

    private readonly CelestialBody _earth;
    private readonly CelestialBody _moon;
    private readonly Site _site;
    private readonly Spacecraft _lro;
    private readonly TimeSystem.Time _windowStart;
    private readonly TimeSystem.Time _windowEnd;
    private readonly Window _searchWindow;
    private readonly TimeSpan _step;

    public LroTrackingDataTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(LroSpkPath);

        _earth = new CelestialBody(PlanetsAndMoons.EARTH);
        _moon = new CelestialBody(PlanetsAndMoons.MOON);
        _site = new Site(888, "AUWA01", _earth,
            new Planetodetic(
                115.348678 * IO.Astrodynamics.Constants.Deg2Rad,
                -29.045772 * IO.Astrodynamics.Constants.Deg2Rad,
                251.1));
        _windowStart = new TimeSystem.Time(2023, 12, 15, 0, 0, 0, frame: TimeFrame.UTCFrame);
        _windowEnd = _windowStart.Add(TimeSpan.FromHours(12));
        _searchWindow = new Window(_windowStart, _windowEnd);
        _lro = new Spacecraft(-85, "LRO", epoch: _windowStart);
        _step = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Compute tracking visibility arcs using high-level framework APIs:
    /// 1. FindWindowsOnCoordinateConstraint: LRO Z > 0 in site topocentric frame (above horizon)
    /// 2. FindWindowsOnOccultationConstraint: LRO occulted by Moon from site
    /// 3. Subtract occultation windows from above-horizon windows
    /// </summary>
    private List<Window> ComputeTrackingArcs()
    {
        // Find windows when LRO is above the horizon (Z > 0 in site topocentric frame)
        var aboveHorizonWindows = _lro.FindWindowsOnCoordinateConstraint(
            _searchWindow, _site, _site.Frame,
            CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 0.0, 0.0,
            Aberration.None, _step).ToList();

        // Find windows when LRO is occulted by Moon from site
        var occultations = _lro.FindWindowsOnOccultationConstraint(
            _searchWindow, _site,
            ShapeType.Point, _moon, ShapeType.Ellipsoid,
            OccultationType.Full, Aberration.None, _step).ToList();

        // Subtract occultation windows from above-horizon windows
        var trackingArcs = new List<Window>();
        foreach (var visWindow in aboveHorizonWindows)
        {
            var remaining = new List<Window> { visWindow };
            foreach (var occWindow in occultations)
            {
                var next = new List<Window>();
                foreach (var r in remaining)
                {
                    if (occWindow.EndDate <= r.StartDate || occWindow.StartDate >= r.EndDate)
                    {
                        next.Add(r);
                    }
                    else
                    {
                        if (occWindow.StartDate > r.StartDate)
                            next.Add(new Window(r.StartDate, occWindow.StartDate));
                        if (occWindow.EndDate < r.EndDate)
                            next.Add(new Window(occWindow.EndDate, r.EndDate));
                    }
                }
                remaining = next;
            }
            trackingArcs.AddRange(remaining);
        }

        // Conformance arcs are derived from minute-grid visibility samples.
        // Convert continuous windows into sampled half-open windows [rise, fall).
        var sampledArcs = new List<Window>();
        bool inArc = false;
        TimeSystem.Time arcStart = default!;

        for (var epoch = _searchWindow.StartDate; epoch < _searchWindow.EndDate; epoch = epoch.Add(_step))
        {
            bool isVisible = trackingArcs.Any(a => epoch >= a.StartDate && epoch < a.EndDate);

            if (isVisible && !inArc)
            {
                arcStart = epoch;
                inArc = true;
            }
            else if (!isVisible && inArc)
            {
                sampledArcs.Add(new Window(arcStart, epoch));
                inArc = false;
            }
        }

        if (inArc)
        {
            sampledArcs.Add(new Window(arcStart, _searchWindow.EndDate));
        }

        return sampledArcs;
    }

    /// <summary>
    /// Collect range and range-rate samples at each step within the tracking arcs.
    /// Uses GetHorizontalCoordinates for range and GetEphemeris for range-rate.
    /// </summary>
    private (List<double> RangeKm, List<double> RangeRateKmS) CollectTrackingObservables(List<Window> arcs)
    {
        var rangeKm = new List<double>();
        var rangeRateKmS = new List<double>();

        // Match conformance CSV generation semantics:
        // - Sample on a fixed 60 s grid from search-window start.
        // - Consider windows as half-open [start, end): fall epoch is excluded.
        for (var epoch = _searchWindow.StartDate; epoch < _searchWindow.EndDate; epoch = epoch.Add(_step))
        {
            if (!arcs.Any(x=> epoch >= x.StartDate && epoch < x.EndDate))
            {
                continue;
            }
            var horizontal = _site.GetHorizontalCoordinates(epoch, _lro, Aberration.None);
            rangeKm.Add(horizontal.Range / 1000.0);

            var sv = _lro.GetEphemeris(epoch, _site, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            double rangeRate = (sv.Position * sv.Velocity) / sv.Position.Magnitude();
            rangeRateKmS.Add(rangeRate / 1000.0);
        }

        return (rangeKm, rangeRateKmS);
    }

    #region Internal Consistency Tests

    [Fact]
    public void KernelBackedLroLoadsSuccessfully()
    {
        Assert.True(_lro.IsSpiceBacked);
        Assert.Equal(-85, _lro.NaifId);

        var midEpoch = _windowStart.Add(TimeSpan.FromHours(6));
        var sv = _lro.GetEphemeris(midEpoch, _earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        double distanceM = sv.Position.Magnitude();

        Assert.InRange(distanceM, 3.5e8, 4.0e8);
    }

    [Fact]
    public void RangeFromHorizontalCoordinatesMatchesEphemerisPositionMagnitude()
    {
        var arcs = ComputeTrackingArcs();
        Assert.True(arcs.Count > 0, "No tracking arcs found");

        // Pick 5 epochs spread across arcs
        var sampleEpochs = arcs.Take(5)
            .Select(a => a.StartDate.Add((a.EndDate - a.StartDate) * 0.5))
            .ToList();

        foreach (var epoch in sampleEpochs)
        {
            double rangeFromHorizontal = _site.GetHorizontalCoordinates(epoch, _lro, Aberration.None).Range;
            double rangeFromEphemeris = _lro.GetEphemeris(epoch, _site, Frames.Frame.ICRF, Aberration.None)
                .ToStateVector().Position.Magnitude();

            Assert.Equal(rangeFromHorizontal, rangeFromEphemeris, precision: 3);
        }
    }

    [Fact]
    public void RangeRateFromDotProductConsistentWithNumericalDerivative()
    {
        var arcs = ComputeTrackingArcs();
        Assert.True(arcs.Count > 0, "No tracking arcs found");

        // Pick epochs from the middle of arcs
        var sampleEpochs = arcs.Take(5)
            .Select(a => a.StartDate.Add((a.EndDate - a.StartDate) * 0.5))
            .ToList();

        var dt = TimeSpan.FromSeconds(1);

        foreach (var epoch in sampleEpochs)
        {
            var sv = _lro.GetEphemeris(epoch, _site, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            double analyticalRangeRate = (sv.Position * sv.Velocity) / sv.Position.Magnitude();

            double rangePlus = _lro.GetEphemeris(epoch.Add(dt), _site, Frames.Frame.ICRF, Aberration.None)
                .ToStateVector().Position.Magnitude();
            double rangeMinus = _lro.GetEphemeris(epoch.Add(-dt), _site, Frames.Frame.ICRF, Aberration.None)
                .ToStateVector().Position.Magnitude();
            double numericalRangeRate = (rangePlus - rangeMinus) / 2.0;

            Assert.True(System.Math.Abs(analyticalRangeRate - numericalRangeRate) < 0.5,
                $"At {epoch}: analytical={analyticalRangeRate:F6} vs numerical={numericalRangeRate:F6}, " +
                $"diff={System.Math.Abs(analyticalRangeRate - numericalRangeRate):E3}");
        }
    }

    [Fact]
    public void EphemerisFromSiteAndFromLroAreInverse()
    {
        var epoch = _windowStart.Add(TimeSpan.FromHours(1));

        var lroFromSite = _lro.GetEphemeris(epoch, _site, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var siteFromLro = _site.GetEphemeris(epoch, _lro, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        Assert.Equal(lroFromSite.Position.X, -siteFromLro.Position.X, 3);
        Assert.Equal(lroFromSite.Position.Y, -siteFromLro.Position.Y, 3);
        Assert.Equal(lroFromSite.Position.Z, -siteFromLro.Position.Z, 3);
        Assert.Equal(lroFromSite.Velocity.X, -siteFromLro.Velocity.X, 6);
        Assert.Equal(lroFromSite.Velocity.Y, -siteFromLro.Velocity.Y, 6);
        Assert.Equal(lroFromSite.Velocity.Z, -siteFromLro.Velocity.Z, 6);
    }

    #endregion

    #region Reference Value Tests (ANISE expected-result.json)

    [Fact]
    public void VisibilityArcsMatchExpectedCount()
    {
        var arcs = ComputeTrackingArcs();
        Assert.Equal(6, arcs.Count);
    }

    [Fact]
    public void VisibilityArcTimesMatchExpectedValues()
    {
        var arcs = ComputeTrackingArcs();
        Assert.Equal(6, arcs.Count);

        var expectedArcs = new (TimeSystem.Time Rise, TimeSystem.Time Fall)[]
        {
            (new TimeSystem.Time(2023, 12, 15, 0, 25, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 1, 36, 0, frame: TimeFrame.UTCFrame)),
            (new TimeSystem.Time(2023, 12, 15, 2, 22, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 3, 33, 0, frame: TimeFrame.UTCFrame)),
            (new TimeSystem.Time(2023, 12, 15, 4, 19, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 5, 30, 0, frame: TimeFrame.UTCFrame)),
            (new TimeSystem.Time(2023, 12, 15, 6, 16, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 7, 27, 0, frame: TimeFrame.UTCFrame)),
            (new TimeSystem.Time(2023, 12, 15, 8, 13, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 9, 24, 0, frame: TimeFrame.UTCFrame)),
            (new TimeSystem.Time(2023, 12, 15, 10, 10, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 11, 21, 0, frame: TimeFrame.UTCFrame)),
        };

        const double toleranceSeconds = 1.0;

        for (int i = 0; i < 6; i++)
        {
            double riseDiffS = System.Math.Abs((arcs[i].StartDate - expectedArcs[i].Rise).TotalSeconds);
            double fallDiffS = System.Math.Abs((arcs[i].EndDate - expectedArcs[i].Fall).TotalSeconds);

            Assert.True(riseDiffS <= toleranceSeconds,
                $"Arc {i + 1} rise: diff={riseDiffS:F1}s exceeds {toleranceSeconds}s tolerance");
            Assert.True(fallDiffS <= toleranceSeconds,
                $"Arc {i + 1} fall: diff={fallDiffS:F1}s exceeds {toleranceSeconds}s tolerance");
        }
    }

    [Fact]
    public void RangeStatisticsMatchExpectedValues()
    {
        var arcs = ComputeTrackingArcs();
        var (rangeKm, _) = CollectTrackingObservables(arcs);
        Assert.True(rangeKm.Count > 0, "No visible points collected");
        Assert.True(rangeKm.Count == 426, $"Expected 426 visible samples from conformance CSV, got {rangeKm.Count}");

        rangeKm.Sort();

        double mean = rangeKm.Average();
        double median = rangeKm[rangeKm.Count / 2];
        double min = rangeKm.Min();
        double max = rangeKm.Max();

        const double toleranceKm = 0.003;

        Assert.True(System.Math.Abs(mean - 363508.500) < toleranceKm,
            $"Range mean: {mean:F3} km, expected 363508.500 km, diff={System.Math.Abs(mean - 363508.500):F3} km");
        Assert.True(System.Math.Abs(median - 363320.057) < toleranceKm,
            $"Range median: {median:F3} km, expected 363320.057 km, diff={System.Math.Abs(median - 363320.057):F3} km");
        Assert.True(System.Math.Abs(min - 360939.402) < toleranceKm,
            $"Range min: {min:F3} km, expected 360939.402 km, diff={System.Math.Abs(min - 360939.402):F3} km");
        Assert.True(System.Math.Abs(max - 368358.996) < toleranceKm,
            $"Range max: {max:F3} km, expected 368358.996 km, diff={System.Math.Abs(max - 368358.996):F3} km");
    }

    [Fact]
    public void RangeRateStatisticsMatchExpectedValues()
    {
        var arcs = ComputeTrackingArcs();
        var (_, rangeRateKmS) = CollectTrackingObservables(arcs);
        Assert.True(rangeRateKmS.Count > 0, "No visible points collected");
        Assert.True(rangeRateKmS.Count == 426, $"Expected 426 visible samples from conformance CSV, got {rangeRateKmS.Count}");

        rangeRateKmS.Sort();

        double mean = rangeRateKmS.Average();
        double median = rangeRateKmS[rangeRateKmS.Count / 2];
        double min = rangeRateKmS.Min();
        double max = rangeRateKmS.Max();

        const double toleranceKmS = 0.001;

        Assert.True(System.Math.Abs(mean - (-0.059161)) < toleranceKmS,
            $"Range-rate mean: {mean:F6} km/s, expected -0.059161 km/s, diff={System.Math.Abs(mean - (-0.059161)):F6} km/s");
        Assert.True(System.Math.Abs(median - (-0.070184)) < toleranceKmS,
            $"Range-rate median: {median:F6} km/s, expected -0.070184 km/s, diff={System.Math.Abs(median - (-0.070184)):F6} km/s");
        Assert.True(System.Math.Abs(min - (-1.863064)) < toleranceKmS,
            $"Range-rate min: {min:F6} km/s, expected -1.863064 km/s, diff={System.Math.Abs(min - (-1.863064)):F6} km/s");
        Assert.True(System.Math.Abs(max - 1.871982) < toleranceKmS,
            $"Range-rate max: {max:F6} km/s, expected 1.871982 km/s, diff={System.Math.Abs(max - 1.871982):F6} km/s");
    }

    [Fact]
    public void RangeRateChangesSignDuringVisibilityArc()
    {
        var arcs = ComputeTrackingArcs();
        Assert.True(arcs.Count > 0, "No arcs detected");

        bool foundSignChange = false;

        foreach (var arc in arcs)
        {
            bool hasPositive = false;
            bool hasNegative = false;

            for (var epoch = arc.StartDate; epoch <= arc.EndDate; epoch = epoch.Add(_step))
            {
                var sv = _lro.GetEphemeris(epoch, _site, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                double rangeRate = (sv.Position * sv.Velocity) / sv.Position.Magnitude();

                if (rangeRate > 0) hasPositive = true;
                if (rangeRate < 0) hasNegative = true;

                if (hasPositive && hasNegative)
                {
                    foundSignChange = true;
                    break;
                }
            }

            if (foundSignChange) break;
        }

        Assert.True(foundSignChange, "Expected range-rate to change sign during at least one arc");
    }

    [Fact]
    public void VisibilityArcDurationsAreConsistentWithLroPeriod()
    {
        var arcs = ComputeTrackingArcs();
        Assert.True(arcs.Count >= 2, "Need at least 2 arcs");

        foreach (var arc in arcs)
        {
            double durationMinutes = (arc.EndDate - arc.StartDate).TotalMinutes;
            Assert.InRange(durationMinutes, 60, 80);
        }

        for (int i = 1; i < arcs.Count; i++)
        {
            double gapMinutes = (arcs[i].StartDate - arcs[i - 1].StartDate).TotalMinutes;
            Assert.InRange(gapMinutes, 110, 130);
        }
    }

    #endregion
}
