using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// <c>AddPropagatedStates</c> and the gaps between the windows it is given (decision 42 of #668, #792, its gaps defined by
/// decisions 83 to 87, decided by S. Guillet, 2026-11-23): a call is one window, which opens a new arc when its hull meets
/// no stored arc (83); a call whose hull covers the gap between two arcs merges them, arcs that touch at an impulse never
/// (84); the light-time margin of #527 applies at every arc end, and a read in a gap outside every widened arc throws as
/// a read outside the stored states (85); a window argument that meets a gap is refused up front with
/// <c>ArgumentException</c> (86); an arc of one state reads at its exact epoch only and is not written (87).
/// </summary>
/// <remarks>
/// The references are the two-body orbit the states are taken from, and, to the bit, a spacecraft that holds the window
/// read alone (the decided rule: a read inside a window is the read of that window). Before the change the windows made
/// one arc, read across the gap without an error: 9.28e9 m off the two-body orbit 2 h 56 min into a gap of 4 h (the probe
/// of the first attempt of this stage, two calls of one hour at 60 s).
/// </remarks>
public class AddedStatesGapTests
{
    private readonly ITestOutputHelper _output;

    public AddedStatesGapTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    // Largest distance of a Lagrange read of the two-body states every 60 s from the two-body orbit, 8 epochs every 15 s
    // inside a window and within the light time of the Moon outside its ends, m. Measured 3.4e-4 m (worst of the reads of
    // this class, Linux x64, 2026-11-23); the reads one-sided at the ends of an arc set it. Twice the worst, rounded up.
    private const double TwoBodyTolerance = 1.0e-3;

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frame.ICRF, Start);

    private static StateVector Orbit(CelestialItem earth) =>
        new(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), earth, Start, Frame.ICRF);

    // Two-body states every step seconds over [from, to] seconds from the start, in the frame given
    private static StateVector[] States(StateVector orbit, double from, double to, double step = 60.0, Frame frame = null) =>
        Enumerable.Range(0, (int)System.Math.Round((to - from) / step) + 1)
            .Select(i =>
            {
                var sv = orbit.AtEpoch(Start.AddSeconds(from + step * i)).ToStateVector();
                return frame == null || frame == Frame.ICRF ? sv : sv.ToFrame(frame).ToStateVector();
            }).ToArray();

    private static Spacecraft NewSpacecraft(int naifId, StateVector orbit) =>
        new(naifId, $"GA{-naifId}", 1000.0, 3000.0, new Clock($"GA{-naifId}_CLK", 65536), orbit);

    private static double Seconds(in Time epoch) => (epoch - Start).TotalSeconds;

    private static void AssertSameState(StateVector expected, StateVector actual)
    {
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Velocity, actual.Velocity);
    }

    // The distance of a read from the two-body orbit at its epoch, m
    private static double FromTwoBody(StateVector orbit, StateVector read, in Time epoch) =>
        (read.Position - orbit.AtEpoch(epoch).ToStateVector().Position).Magnitude();

    #region Decision 83: one call, one window

    /// <summary>
    /// Two calls over [0, 1 h] and [5 h, 6 h] make two arcs; a read in the gap throws on both readers, as a read outside
    /// the stored states (#527); each window reads, to the bit, as the spacecraft that holds it alone, and within
    /// <see cref="TwoBodyTolerance"/> of the two-body orbit. The second window given in ICRF and in ECLIPJ2000.
    /// </summary>
    [Theory]
    [InlineData("ICRF", -7921)]
    [InlineData("ECLIPJ2000", -7924)]
    public void TwoDisjointWindows_MakeTwoArcs_AReadInTheGapThrows_AndEachWindowReadsAsAlone(string frameName, int naifId)
    {
        // Arrange: states over [0, 1 h] and [5 h, 6 h], added in two calls; the same windows each on a spacecraft alone
        var frame = frameName == "ICRF" ? Frame.ICRF : Frame.ECLIPTIC_J2000;
        var earth = Earth();
        var orbit = Orbit(earth);
        var first = States(orbit, 0.0, 3600.0);
        var second = States(orbit, 18000.0, 21600.0, frame: frame);
        var both = NewSpacecraft(naifId, orbit);
        var firstAlone = NewSpacecraft(naifId - 1, orbit);
        var secondAlone = NewSpacecraft(naifId - 2, orbit);

        // Act
        both.AddPropagatedStates(first);
        both.AddPropagatedStates(second);
        firstAlone.AddPropagatedStates(first);
        secondAlone.AddPropagatedStates(second);

        // Assert: two arcs, the windows as added
        Assert.Equal(2, both.PropagatedArcs.Count);
        LabelAssert.Equal(first.Select(x => x.Epoch), both.PropagatedArcs[0].Select(x => x.Epoch));
        LabelAssert.Equal(second.Select(x => x.Epoch), both.PropagatedArcs[1].Select(x => x.Epoch));
        LabelAssert.Equal(new Window(Start, Start.AddSeconds(21600.0)), both.InterpolatedStatesWindow);

        // A read in the gap throws, by GetGeometricStateRelativeTo and by the GetEphemeris short-circuit, as outside (#527)
        foreach (double s in new[] { 3600.5, 3660.0, 10800.0, 17999.5 })
        {
            var t = Start.AddSeconds(s);
            Assert.Throws<InvalidOperationException>(() => both.GetGeometricStateRelativeTo(t, earth));
            Assert.Throws<InvalidOperationException>(() => both.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
        }

        Assert.Throws<InvalidOperationException>(() => both.GetGeometricStateRelativeTo(Start.AddSeconds(-1.0), earth));
        Assert.Throws<InvalidOperationException>(() => both.GetGeometricStateRelativeTo(Start.AddSeconds(21601.0), earth));

        // Each window reads, to the bit, as the spacecraft that holds it alone, its ends included, near the two-body orbit
        double worst = 0.0;
        foreach (var (alone, from, to) in new[] { (firstAlone, 0.0, 3600.0), (secondAlone, 18000.0, 21600.0) })
        {
            for (double s = from; s <= to; s += 15.0)
            {
                var t = Start.AddSeconds(s);
                var expected = alone.GetGeometricStateRelativeTo(t, earth);
                var read = both.GetGeometricStateRelativeTo(t, earth);
                AssertSameState(expected, read);
                AssertSameState(alone.GetEphemeris(t, earth, Frame.ICRF, Aberration.None), both.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
                worst = System.Math.Max(worst, FromTwoBody(orbit, read, t));
            }
        }

        _output.WriteLine($"MEASURE #792 two windows {frameName}: worst read {worst:E3} m from the two-body orbit");
        Assert.True(worst <= TwoBodyTolerance, $"{worst:E3} m");
    }

    /// <summary>
    /// A window whose hull touches a stored arc meets it (closed intervals): it joins that arc; one that does not makes
    /// its own, the gap between them refused; interleaved windows make one arc.
    /// </summary>
    [Fact]
    public void AWindowThatTouchesAnArc_JoinsIt_OneAMinuteAway_OpensItsOwn()
    {
        var earth = Earth();
        var orbit = Orbit(earth);

        // [0, 600] then [600, 1200]: the hulls touch at 600 s, one arc
        var touching = NewSpacecraft(-7931, orbit);
        touching.AddPropagatedStates(States(orbit, 0.0, 600.0));
        touching.AddPropagatedStates(States(orbit, 600.0, 1200.0));
        Assert.Single(touching.PropagatedArcs);
        Assert.Equal(21, touching.PropagatedStates.Length);

        // [0, 600] then [660, 1200]: two arcs, the minute between them a gap
        var apart = NewSpacecraft(-7932, orbit);
        apart.AddPropagatedStates(States(orbit, 0.0, 600.0));
        apart.AddPropagatedStates(States(orbit, 660.0, 1200.0));
        Assert.Equal(2, apart.PropagatedArcs.Count);
        Assert.Throws<InvalidOperationException>(() => apart.GetGeometricStateRelativeTo(Start.AddSeconds(630.0), earth));
        Assert.Throws<InvalidOperationException>(() => apart.GetEphemeris(Start.AddSeconds(630.0), earth, Frame.ICRF, Aberration.None));

        // The even minutes, then the odd ones: the second hull meets the first arc, one arc read as the whole window
        var interleaved = NewSpacecraft(-7933, orbit);
        var all = States(orbit, 0.0, 1200.0);
        interleaved.AddPropagatedStates(all.Where((_, i) => i % 2 == 0).ToArray());
        interleaved.AddPropagatedStates(all.Where((_, i) => i % 2 == 1).ToArray());
        var whole = NewSpacecraft(-7934, orbit);
        whole.AddPropagatedStates(all);
        Assert.Single(interleaved.PropagatedArcs);
        for (double s = 0.0; s <= 1200.0; s += 15.0)
        {
            var t = Start.AddSeconds(s);
            AssertSameState(whole.GetGeometricStateRelativeTo(t, earth), interleaved.GetGeometricStateRelativeTo(t, earth));
            AssertSameState(whole.GetGeometricStateRelativeTo(t, earth), touching.GetGeometricStateRelativeTo(t, earth));
        }
    }

    /// <summary>
    /// A window of one state per call, one minute apart: each call is a window of its own, so 21 arcs of one state, each
    /// read at its epoch only (decisions 83 and 87; before, one arc of 21 states read between them).
    /// </summary>
    [Fact]
    public void OneStatePerCall_MakesOneArcPerCall()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var spacecraft = NewSpacecraft(-7935, orbit);
        var states = States(orbit, 0.0, 1200.0);
        foreach (var state in states)
        {
            spacecraft.AddPropagatedStates(state);
        }

        Assert.Equal(21, spacecraft.PropagatedArcs.Count);
        Assert.All(spacecraft.PropagatedArcs, arc => Assert.Single(arc));
        foreach (var state in states)
        {
            AssertSameState(state, spacecraft.GetGeometricStateRelativeTo(state.Epoch, earth));
            AssertSameState(state, spacecraft.GetEphemeris(state.Epoch, earth, Frame.ICRF, Aberration.None));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(state.Epoch.AddSeconds(30.0), earth));
        }
    }

    /// <summary>
    /// A window that meets one arc and lies partly in the gap before it, or after it, extends that arc over the gap it
    /// covers: the call is one window (decision 83), the gap it leaves between the arcs still throws (decision 42).
    /// </summary>
    [Fact]
    public void AWindowThatMeetsOneArc_ExtendsIt_TheGapLeftThrows()
    {
        var earth = Earth();
        var orbit = Orbit(earth);

        // [0, 1 h], [5 h, 6 h], then [3 h, 5.5 h], which meets the second only
        var later = NewSpacecraft(-7941, orbit);
        later.AddPropagatedStates(States(orbit, 0.0, 3600.0));
        later.AddPropagatedStates(States(orbit, 18000.0, 21600.0));
        later.AddPropagatedStates(States(orbit, 10800.0, 19800.0));
        Assert.Equal(2, later.PropagatedArcs.Count);
        Assert.Equal(61, later.PropagatedArcs[0].Length);
        Assert.Equal(181, later.PropagatedArcs[1].Length);
        Assert.Equal(10800.0, Seconds(later.PropagatedArcs[1][0].Epoch));
        Assert.Throws<InvalidOperationException>(() => later.GetGeometricStateRelativeTo(Start.AddSeconds(7200.0), earth));

        // [0, 1 h], [5 h, 6 h], then [0.5 h, 2 h], which meets the first only
        var earlier = NewSpacecraft(-7942, orbit);
        earlier.AddPropagatedStates(States(orbit, 0.0, 3600.0));
        earlier.AddPropagatedStates(States(orbit, 18000.0, 21600.0));
        earlier.AddPropagatedStates(States(orbit, 1800.0, 7200.0));
        Assert.Equal(2, earlier.PropagatedArcs.Count);
        Assert.Equal(121, earlier.PropagatedArcs[0].Length);
        Assert.Equal(61, earlier.PropagatedArcs[1].Length);
        Assert.Throws<InvalidOperationException>(() => earlier.GetGeometricStateRelativeTo(Start.AddSeconds(10800.0), earth));

        double worst = 0.0;
        foreach (var (spacecraft, from, to) in new[] { (later, 10800.0, 21600.0), (earlier, 0.0, 7200.0) })
        {
            for (double s = from; s <= to; s += 15.0)
            {
                var t = Start.AddSeconds(s);
                worst = System.Math.Max(worst, FromTwoBody(orbit, spacecraft.GetGeometricStateRelativeTo(t, earth), t));
                worst = System.Math.Max(worst, FromTwoBody(orbit, spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None), t));
            }
        }

        _output.WriteLine($"MEASURE #792 extended arcs: worst read {worst:E3} m from the two-body orbit");
        Assert.True(worst <= TwoBodyTolerance, $"{worst:E3} m");
    }

    #endregion

    #region Decision 84: a call over the gap merges the arcs, never at an impulse

    /// <summary>
    /// A call whose hull covers the gap between two arcs merges them: the bridge [1 h, 5 h], which touches both, and a
    /// window [0.5 h, 5.5 h] over both; the merged arc reads as the whole window added at once, to the bit.
    /// </summary>
    [Theory]
    [InlineData(3600.0, 18000.0, -7951)]
    [InlineData(1800.0, 19800.0, -7952)]
    public void ACallOverTheGap_MergesTheTwoArcs(double from, double to, int naifId)
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var spacecraft = NewSpacecraft(naifId, orbit);
        spacecraft.AddPropagatedStates(States(orbit, 0.0, 3600.0));
        spacecraft.AddPropagatedStates(States(orbit, 18000.0, 21600.0));
        Assert.Equal(2, spacecraft.PropagatedArcs.Count);

        // Act
        spacecraft.AddPropagatedStates(States(orbit, from, to));

        // Assert: one arc, every minute of [0, 6 h] once, read as the whole window
        Assert.Single(spacecraft.PropagatedArcs);
        Assert.Equal(361, spacecraft.PropagatedStates.Length);
        var whole = NewSpacecraft(naifId - 10, orbit);
        whole.AddPropagatedStates(States(orbit, 0.0, 21600.0));
        double worst = 0.0;
        for (double s = 0.0; s <= 21600.0; s += 37.5)
        {
            var t = Start.AddSeconds(s);
            var read = spacecraft.GetGeometricStateRelativeTo(t, earth);
            AssertSameState(whole.GetGeometricStateRelativeTo(t, earth), read);
            worst = System.Math.Max(worst, FromTwoBody(orbit, read, t));
        }

        _output.WriteLine($"MEASURE #792 merged arcs [{from}, {to}]: worst read {worst:E3} m from the two-body orbit");
        Assert.True(worst <= TwoBodyTolerance, $"{worst:E3} m");
    }

    /// <summary>
    /// Arcs that meet at an impulse touch and never merge: after a propagation split at an impulse (synthetic states), a
    /// window after the end opens its own arc; a call over the impulse and the gap merges the gap only, the impulse still
    /// splits the arcs, its two states kept.
    /// </summary>
    [Fact]
    public void ArcsThatTouchAtAnImpulse_NeverMerge()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var spacecraft = NewSpacecraft(-7953, orbit);
        var outputs = States(orbit, 0.0, 3600.0);
        // An impulse at 1 800 s that adds 10 m/s along the velocity: the arc after it is the two-body orbit from that state
        var before = orbit.AtEpoch(Start.AddSeconds(1800.0)).ToStateVector();
        var after = new StateVector(before.Position, before.Velocity + before.Velocity.Normalize() * 10.0, earth, before.Epoch, Frame.ICRF);
        var outputsAfter = outputs.Select(x => Seconds(x.Epoch) > 1800.0 ? after.AtEpoch(x.Epoch).ToStateVector() : x).ToArray();
        spacecraft.SetPropagatedStates(outputsAfter, [new ImpulseBoundary(before.Epoch, before, after)], null);
        Assert.Equal(2, spacecraft.PropagatedArcs.Count);
        var impulseArcs = spacecraft.PropagatedArcs.Select(a => a.Length).ToArray();

        // A window after the end, a gap before it: its own arc
        var late = States(after, 7200.0, 9000.0);
        spacecraft.AddPropagatedStates(late);
        Assert.Equal(3, spacecraft.PropagatedArcs.Count);
        Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(Start.AddSeconds(5400.0), earth));

        // A call over the impulse and the gap: the gap merged, the impulse kept
        var bridge = States(after, 2400.0, 7200.0).Prepend(orbit.AtEpoch(Start.AddSeconds(1530.0)).ToStateVector()).ToArray();
        spacecraft.AddPropagatedStates(bridge);
        var arcs = spacecraft.PropagatedArcs;
        Assert.Equal(2, arcs.Count);
        Assert.Equal(impulseArcs[0] + 1, arcs[0].Length);
        Assert.Same(before, arcs[0][^1]);
        Assert.Same(after, arcs[1][0]);
        Assert.Equal(9000.0, Seconds(arcs[1][^1].Epoch));
        AssertSameState(after, spacecraft.GetGeometricStateRelativeTo(before.Epoch, earth));
        double worst = 0.0;
        for (double s = 1830.0; s <= 9000.0; s += 30.0)
        {
            var t = Start.AddSeconds(s);
            worst = System.Math.Max(worst, (spacecraft.GetGeometricStateRelativeTo(t, earth).Position - after.AtEpoch(t).ToStateVector().Position).Magnitude());
        }

        _output.WriteLine($"MEASURE #792 merged after an impulse: worst read {worst:E3} m from the two-body orbit after it");
        Assert.True(worst <= TwoBodyTolerance, $"{worst:E3} m");
    }

    #endregion

    #region Decision 85: the light-time margin at every arc end

    /// <summary>
    /// A light-time read from the Moon at the start of the second window reads the spacecraft 1.3 s before it, in the gap
    /// within the margin of the second arc: it reads that arc, to the bit the read of the spacecraft that holds it alone
    /// (before, the first arc, extrapolated over 4 h); so at the end of the first window by transmission. A light-time read
    /// whose observation epoch lies in the gap throws.
    /// </summary>
    [Theory]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.CNS)]
    [InlineData(Aberration.XLT)]
    [InlineData(Aberration.XCNS)]
    public void ALightTimeReadWithinTheMarginOfAnArc_ReadsThatArc(Aberration aberration)
    {
        var earth = Earth();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Start);
        var orbit = Orbit(earth);
        var first = States(orbit, 0.0, 3600.0);
        var second = States(orbit, 18000.0, 21600.0);
        int id = -7960 - (int)aberration;
        var both = NewSpacecraft(id * 10, orbit);
        both.AddPropagatedStates(first);
        both.AddPropagatedStates(second);
        var firstAlone = NewSpacecraft(id * 10 - 1, orbit);
        firstAlone.AddPropagatedStates(first);
        var secondAlone = NewSpacecraft(id * 10 - 2, orbit);
        secondAlone.AddPropagatedStates(second);
        var twoBody = NewSpacecraft(id * 10 - 3, orbit);

        // The ends of each arc: by reception the corrected epoch lies before the observation, by transmission after it
        bool reception = aberration is Aberration.LT or Aberration.CNS;
        double worst = 0.0;
        foreach (var (alone, s) in new[] { (firstAlone, 0.0), (firstAlone, 3600.0), (secondAlone, 18000.0), (secondAlone, 21600.0) })
        {
            var t = Start.AddSeconds(s);
            var read = both.GetEphemeris(t, moon, Frame.ICRF, aberration);
            AssertSameState(alone.GetEphemeris(t, moon, Frame.ICRF, aberration), read);
            worst = System.Math.Max(worst, (read.Position - twoBody.GetEphemeris(t, moon, Frame.ICRF, aberration).Position).Magnitude());
        }

        _output.WriteLine($"MEASURE #792 light time {aberration} at the arc ends: worst read {worst:E3} m from the two-body orbit");
        Assert.True(worst <= TwoBodyTolerance, $"{worst:E3} m");

        // The observation epoch in the gap: throws, as outside the stored states
        var inGap = Start.AddSeconds(reception ? 3600.5 : 17999.5);
        Assert.Throws<InvalidOperationException>(() => both.GetEphemeris(inGap, moon, Frame.ICRF, aberration));
        Assert.Throws<InvalidOperationException>(() => both.GetEphemeris(Start.AddSeconds(10800.0), moon, Frame.ICRF, aberration));
    }

    #endregion

    #region Decision 86: a window argument that meets a gap

    /// <summary>
    /// A window inside one arc is accepted; one that meets the gap, or lies in it, is refused up front with
    /// <c>ArgumentException</c> naming the parameter, by the conjunction analysis as by the check itself.
    /// </summary>
    [Fact]
    public void AWindowThatMeetsAGap_IsRefused()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var spacecraft = NewSpacecraft(-7971, orbit);
        spacecraft.AddPropagatedStates(States(orbit, 0.0, 3600.0));
        spacecraft.AddPropagatedStates(States(orbit, 18000.0, 21600.0));

        // Inside one arc: accepted, its ends included
        spacecraft.CheckInterpolatedStatesCover(new Window(Start, Start.AddSeconds(3600.0)), "search window", "searchWindow");
        spacecraft.CheckInterpolatedStatesCover(new Window(Start.AddSeconds(18000.0), Start.AddSeconds(21600.0)), "search window", "searchWindow");

        // Across the gap, over it, in it: refused
        foreach (var (from, to) in new[] { (0.0, 21600.0), (3000.0, 3601.0), (3600.0, 18000.0), (7200.0, 10800.0), (17999.0, 18600.0) })
        {
            var window = new Window(Start.AddSeconds(from), Start.AddSeconds(to));
            var exception = Assert.Throws<ArgumentException>(() => spacecraft.CheckInterpolatedStatesCover(window, "search window", "searchWindow"));
            Assert.Equal("searchWindow", exception.ParamName);
        }

        // The conjunction analysis of the ILocalizable overloads refuses it before any search
        var secondary = NewSpacecraft(-7972, new StateVector(new Vector3(-7378137.0, 0.0, 1000.0), new Vector3(0.0, -5509.9, -4623.4), earth, Start, Frame.ICRF));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        var across = new Window(Start.AddSeconds(3000.0), Start.AddSeconds(19000.0));
        Assert.Equal("screeningWindow", Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, secondary, across)).ParamName);
        Assert.Equal("screeningWindow", Assert.Throws<ArgumentException>(() => ConjunctionAssessment.AnalyzeAll(profile, secondary, across)).ParamName);
    }

    #endregion

    #region Decision 87: an arc of one state

    /// <summary>
    /// A state added alone away from the stored arcs is an arc of one state: a read at its epoch returns it, on both readers;
    /// any other read throws as a read in a gap; the SPK refuses it (<c>ArgumentException</c>, a segment needs two epochs).
    /// </summary>
    [Fact]
    public void AnArcOfOneState_ReadsAtItsEpochOnly_AndIsNotWritten()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var spacecraft = NewSpacecraft(-7981, orbit);
        spacecraft.AddPropagatedStates(States(orbit, 0.0, 3600.0));
        var lone = orbit.AtEpoch(Start.AddSeconds(10800.0)).ToStateVector();
        spacecraft.AddPropagatedStates(lone);

        Assert.Equal(2, spacecraft.PropagatedArcs.Count);
        Assert.Same(lone, Assert.Single(spacecraft.PropagatedArcs[1]));
        AssertSameState(lone, spacecraft.GetGeometricStateRelativeTo(lone.Epoch, earth));
        AssertSameState(lone, spacecraft.GetEphemeris(lone.Epoch, earth, Frame.ICRF, Aberration.None));
        foreach (double ds in new[] { -1.0, -1.0e-7, 1.0e-7, 1.0 })
        {
            var t = lone.Epoch.AddSeconds(ds);
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(t, earth));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
        }

        // A light-time read from the Moon at its epoch reads it 1.3 s before: throws
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Start);
        Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(lone.Epoch, moon, Frame.ICRF, Aberration.LT));

        // The window of its epoch alone is covered; any wider is not
        spacecraft.CheckInterpolatedStatesCover(new Window(lone.Epoch, lone.Epoch), "search window", "searchWindow");
        Assert.Throws<ArgumentException>(() => spacecraft.CheckInterpolatedStatesCover(new Window(lone.Epoch, lone.Epoch.AddSeconds(1.0)), "search window", "searchWindow"));

        var directory = Directory.CreateDirectory(Path.Combine("Data/User/AddedStatesGap", "one"));
        var file = new FileInfo(Path.Combine(directory.FullName, "lone.bsp"));
        try
        {
            Assert.Throws<ArgumentException>(() => spacecraft.WriteEphemeris(file));
            file.Refresh();
            Assert.False(file.Exists);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// A spacecraft that stores one state only reads it through the arcs, as an arc of one state beside others (decision
    /// 89 of #668, decided by S. Guillet, 2026-11-23): its exact epoch returns the stored state on every reader (the
    /// short-circuit of <c>GetEphemeris</c>, the reference-body path from the Moon, <c>ToOpm</c>), any other read throws as a
    /// read in a gap, a window argument wider than its epoch is refused up front, a managed search over a window throws at
    /// its first read and the SPK refuses it. Before, the two-body orbit of the initial orbital parameters answered: the
    /// probe of the first review of lot L5a read 1.408e7 m off the stored state at its epoch, and a state 600 s later
    /// without an error. The state stored here lies off the initial orbit, so that the two-body read cannot pass for it. A
    /// spacecraft that stores no state keeps the two-body orbit.
    /// </summary>
    [Fact]
    public void AStoreOfOneState_ReadsAtItsEpochOnly_TheTwoBodyOrbitWithoutAState()
    {
        var earth = Earth();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Start);
        var orbit = Orbit(earth);
        var spacecraft = new Spacecraft(-7985, "GA7985", 1000.0, 3000.0, new Clock("GA7985_CLK", 65536), orbit, cosparId: "2026-001A");
        spacecraft.AddPropagatedStates(new StateVector(new Vector3(8000000.0, 0.0, 0.0), new Vector3(0.0, 7000.0, 0.0), earth,
            Start.AddSeconds(3600.0), Frame.ECLIPTIC_J2000));
        var lone = Assert.Single(Assert.Single(spacecraft.PropagatedArcs));
        LabelAssert.Equal(new Window(lone.Epoch, lone.Epoch), spacecraft.InterpolatedStatesWindow);

        // Its exact epoch: the stored state, on every reader
        AssertSameState(lone, spacecraft.GetGeometricStateRelativeTo(lone.Epoch, earth));
        AssertSameState(lone, spacecraft.GetEphemeris(lone.Epoch, earth, Frame.ICRF, Aberration.None));
        var moonFromEarth = moon.GetGeometricStateRelativeTo(lone.Epoch, earth);
        var fromMoon = spacecraft.GetEphemeris(lone.Epoch, moon, Frame.ICRF, Aberration.None);
        Assert.True((fromMoon.Position - (lone.Position - moonFromEarth.Position)).Magnitude() <= 1.0e-6);
        var opm = spacecraft.ToOpm(epoch: lone.Epoch);
        Assert.Equal(lone.Position.X / 1000.0, opm.Data.StateVector.X);
        Assert.Equal(lone.Velocity.Y / 1000.0, opm.Data.StateVector.YDot);

        // Any other epoch throws, the initial epoch of the two-body orbit included
        foreach (double ds in new[] { -3600.0, -1.0e-7, 1.0e-7, 600.0 })
        {
            var t = lone.Epoch.AddSeconds(ds);
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetGeometricStateRelativeTo(t, earth));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
            Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(t, moon, Frame.ICRF, Aberration.None));
        }

        Assert.Throws<InvalidOperationException>(() => spacecraft.ToOpm());
        Assert.Throws<InvalidOperationException>(() => spacecraft.GetEphemeris(lone.Epoch, moon, Frame.ICRF, Aberration.LT));

        // A window argument: its epoch alone is covered, any wider is refused before any search
        spacecraft.CheckInterpolatedStatesCover(new Window(lone.Epoch, lone.Epoch), "search window", "searchWindow");
        var wider = new Window(lone.Epoch, lone.Epoch.AddSeconds(600.0));
        Assert.Equal("searchWindow", Assert.Throws<ArgumentException>(() => spacecraft.CheckInterpolatedStatesCover(wider, "search window", "searchWindow")).ParamName);
        var secondary = NewSpacecraft(-7986, new StateVector(new Vector3(-7378137.0, 0.0, 1000.0), new Vector3(0.0, -5509.9, -4623.4), earth, Start, Frame.ICRF));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        Assert.Equal("screeningWindow", Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, secondary, wider)).ParamName);
        Assert.Throws<InvalidOperationException>(() => spacecraft.FindWindowsOnDistanceConstraint(wider, moon, RelationnalOperator.Greater, 1.0,
            Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray());

        // The SPK refuses it
        var directory = Directory.CreateDirectory(Path.Combine("Data/User/AddedStatesGap", "store-of-one"));
        var file = new FileInfo(Path.Combine(directory.FullName, "lone.bsp"));
        try
        {
            Assert.Throws<ArgumentException>(() => spacecraft.WriteEphemeris(file));
        }
        finally
        {
            directory.Delete(true);
        }

        // No state stored: the two-body orbit of the initial orbital parameters, in ICRF
        var unpropagated = NewSpacecraft(-7987, orbit);
        Assert.Null(unpropagated.InterpolatedStatesWindow);
        foreach (double s in new[] { 0.0, 3600.0, 4200.0 })
        {
            var t = Start.AddSeconds(s);
            AssertSameState(orbit.ToStateVector(t).ToFrame(Frame.ICRF).ToStateVector(), unpropagated.GetGeometricStateRelativeTo(t, earth));
        }
    }

    #endregion

    #region Decision 91: the arc that reads an epoch

    /// <summary>
    /// Two arcs one second apart, [0, 600 s] and [601 s, 1 201 s], read with a margin of 1.5 s (the internal reader of a
    /// corrected read, #527): an epoch inside an arc is read by that arc, to the bit the spacecraft that holds it alone,
    /// even within the margin of the other (599.8 s, 601.2 s); an epoch in the gap within the margin of both is read by
    /// the nearest arc (600.3 s the first, 600.7 s the second), the later one on a tie (600.5 s) (decision 91 of #668,
    /// decided by S. Guillet, 2026-11-23; before, the arc of the epoch, the first, read every epoch of the gap it reached).
    /// </summary>
    [Fact]
    public void AnEpochInAGapWithinTheMarginOfTwoArcs_IsReadByTheNearest_TheLaterOnATie()
    {
        const double margin = 1.5;
        var earth = Earth();
        var orbit = Orbit(earth);
        var first = States(orbit, 0.0, 600.0);
        var second = States(orbit, 601.0, 1201.0);
        var both = NewSpacecraft(-7991, orbit);
        both.AddPropagatedStates(first);
        both.AddPropagatedStates(second);
        Assert.Equal(2, both.PropagatedArcs.Count);
        var firstAlone = NewSpacecraft(-7992, orbit);
        firstAlone.AddPropagatedStates(first);
        var secondAlone = NewSpacecraft(-7993, orbit);
        secondAlone.AddPropagatedStates(second);

        foreach (var (s, alone) in new[] { (300.0, firstAlone), (599.8, firstAlone), (600.3, firstAlone), (600.5, secondAlone), (600.7, secondAlone), (601.2, secondAlone), (900.0, secondAlone) })
        {
            var t = Start.AddSeconds(s);
            AssertSameState(alone.GetGeometricStateRelativeTo(t, earth, margin), both.GetGeometricStateRelativeTo(t, earth, margin));
        }

        // Inside an arc the margin changes nothing: the read without it, bit for bit
        foreach (double s in new[] { 599.8, 601.2 })
        {
            var t = Start.AddSeconds(s);
            AssertSameState(both.GetGeometricStateRelativeTo(t, earth), both.GetGeometricStateRelativeTo(t, earth, margin));
        }

        // Without the margin the gap throws
        Assert.Throws<InvalidOperationException>(() => both.GetGeometricStateRelativeTo(Start.AddSeconds(600.5), earth));
    }

    #endregion

    #region A TLE spacecraft sampled by TLEPropagator

    /// <summary>
    /// A TLE spacecraft sampled by <see cref="TLEPropagator"/> over [0, 1 h], then given SGP4 states over [3 h, 4 h]: two
    /// arcs; a read in the gap from the Earth without aberration reads SGP4, as outside its samples (before, the short-circuit
    /// interpolated across the gap, 1.648e7 m off SGP4 at 2 h in the probes of R08).
    /// </summary>
    [Fact]
    public void ATleSpacecraft_ReadsSgp4InTheGap()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = new Spacecraft(-7991, "GA7991", 100.0, 10000.0, new Clock("GA7991_CLK", 65536), tle);
        new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, TimeSpan.FromSeconds(60.0)).Propagate();
        var later = Enumerable.Range(0, 61)
            .Select(i => tle.ToStateVector(tle.Epoch.AddHours(3.0).AddSeconds(60.0 * i)).ToFrame(Frame.ICRF).ToStateVector()).ToArray();
        spacecraft.AddPropagatedStates(later);
        Assert.Equal(2, spacecraft.PropagatedArcs.Count);

        var earth = (CelestialItem)spacecraft.PropagatedArcs[0][0].Observer;
        foreach (double hours in new[] { 1.5, 2.0, 2.9 })
        {
            var t = tle.Epoch.AddHours(hours);
            var sgp4 = spacecraft.GetGeometricStateRelativeTo(t, earth);
            AssertSameState(tle.ToStateVector(t).ToFrame(Frame.ICRF).ToStateVector(), sgp4);
            AssertSameState(sgp4, spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None));
        }
    }

    #endregion

    /// <summary>
    /// The arcs of <see cref="PropagatedEphemeris.Add"/> (decisions 83 and 84): a hull that meets no arc, between two arcs
    /// split at an impulse and after a gap, opens its own; the call over a gap merges the two arcs on either side of it.
    /// </summary>
    [Fact]
    public void PropagatedEphemeris_Add_OneWindowPerCall()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Add(States(orbit, 600.0, 1200.0));
        // Before the first arc, apart: its own arc, inserted first
        ephemeris.Add(States(orbit, 0.0, 300.0));
        // After, apart, then between them
        ephemeris.Add(States(orbit, 3000.0, 3600.0));
        ephemeris.Add(States(orbit, 1800.0, 2400.0));
        Assert.Equal(new[] { 0.0, 600.0, 1800.0, 3000.0 }, ephemeris.Arcs.Select(a => Seconds(a[0].Epoch)));
        Assert.Equal(new[] { 300.0, 1200.0, 2400.0, 3600.0 }, ephemeris.Arcs.Select(a => Seconds(a[^1].Epoch)));
        // An empty call changes nothing
        ephemeris.Add(Array.Empty<StateVector>());
        Assert.Equal(4, ephemeris.Arcs.Count);
        // A call from 270 s to 3 000 s, over the three gaps: one arc, the state at 3 000 s replaced
        ephemeris.Add(new[] { orbit.AtEpoch(Start.AddSeconds(3000.0)).ToStateVector(), orbit.AtEpoch(Start.AddSeconds(270.0)).ToStateVector() });
        Assert.Single(ephemeris.Arcs);
        Assert.Equal(40, ephemeris.Count);
    }

    /// <summary>
    /// The arcs read by <see cref="PropagatedEphemeris.InterpolateWithin"/> after an impulse at the end of a propagation
    /// (#575): the last arc, the state after the impulse alone, touching the arc before it at the impulse, reads at its
    /// epoch and, within the margin, past it: the state after the impulse, as before (decision 90 of #668, decided by
    /// S. Guillet, 2026-11-23; decisions 85 and 87 apply to arcs separated by a gap); without a margin a read past it reads
    /// none, and one before it reads the arc before the impulse.
    /// </summary>
    [Fact]
    public void PropagatedEphemeris_AnImpulseAtTheEnd_ItsArcOfOneStateReadsWithinTheMarginPastIt()
    {
        var earth = Earth();
        var orbit = Orbit(earth);
        var outputs = States(orbit, 0.0, 600.0);
        var before = outputs[^1];
        var after = new StateVector(before.Position, before.Velocity + before.Velocity.Normalize() * 10.0, earth, before.Epoch, Frame.ICRF);
        var ephemeris = new PropagatedEphemeris();
        ephemeris.Set(outputs, [new ImpulseBoundary(before.Epoch, before, after)], null);
        Assert.Equal(2, ephemeris.Arcs.Count);
        Assert.Same(after, Assert.Single(ephemeris.Arcs[1]));

        Assert.Same(after, ephemeris.InterpolateWithin(before.Epoch, 0.0));
        var past = before.Epoch.AddSeconds(1.0);
        Assert.Null(ephemeris.InterpolateWithin(past, 0.0));
        AssertSameState(after, ephemeris.InterpolateWithin(past, 2.0));
        Assert.Null(ephemeris.InterpolateWithin(before.Epoch.AddSeconds(3.0), 2.0));
        var earlier = before.Epoch.AddSeconds(-1.0);
        AssertSameState(Lagrange.InterpolateSorted(ephemeris.Arcs[0], earlier), ephemeris.InterpolateWithin(earlier, 2.0));
        // A single window: the arcs touch at the impulse
        Assert.Single(ephemeris.Windows);
    }
}
