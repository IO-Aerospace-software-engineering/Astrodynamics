// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Equinoctial elements of circular and equatorial orbits (#452): f = g = 0 or h = k = 0 exactly, either sign of zero,
/// moved the satellite along its orbit, by Ω, by ϖ or by 2r. Every path through the elements, against the 60-digit
/// reference of <see cref="EquinoctialReference"/>, and the conventions of the Keplerian angles where they are
/// undefined.
/// </summary>
/// <remarks>
/// <para>
/// Every test prints its measures (MEASURE #452), relative to the norm of the reference state. The thresholds are
/// twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), rounded up at the second
/// significant digit.
/// </para>
/// <para>
/// The paths marked (K) solve Kepler's equation (<see cref="OrbitalParameters.ToKeplerianElements()"/>,
/// <see cref="OrbitalParameters.AtEpoch"/>, a true anomaly from a mean anomaly): their measures carry the error of the
/// solver, whose stop at 1e-6 is #453. They are measured again, and their thresholds tightened, once #453 is merged.
/// </para>
/// </remarks>
public class EquinoctialElementsSingularityTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0, frame: TimeFrame.TDBFrame);

    // The bounds of the direct state, relative: twice the worst over Linux, Windows and macOS arm64 (CI run
    // 40280932636, 2026-10-26) of DirectState_MatchesTheReference, 3.07e-16 (suite 153, Windows) and 2.67e-16
    // (suite 155)
    private const double DirectPosition = 6.2e-16;
    private const double DirectVelocity = 5.4e-16;

    private readonly CelestialBody _earth;
    private readonly EquinoctialReference _reference;
    private readonly ITestOutputHelper _output;

    public EquinoctialElementsSingularityTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        _reference = EquinoctialReference.Load();
    }

    // The cases of the direct state: those of the issue, the signed zeros, the inputs near the singular values, the
    // products of the argument of periapsis that underflow, the true longitude beyond a turn, the retrograde equatorial
    // orbit, h or k beyond 1e150 and the conformance cases 151 to 154
    private IEnumerable<EquinoctialReference.EquinoctialCase> DirectCases =>
        _reference.Equinoctial.Where(c => c.Group is "issue" or "signed" or "near" or "underflow" or "large L" or "retro"
            or "huge" or "suite");

    // The cases of the Keplerian path: the same without the conformance cases and the true longitude beyond a turn
    private IEnumerable<EquinoctialReference.EquinoctialCase> KeplerianCases =>
        DirectCases.Where(c => c.Group is not ("suite" or "large L"));

    private IEnumerable<EquinoctialReference.EquinoctialCase> LargeTrueLongitudes =>
        _reference.Equinoctial.Where(c => c.Group == "large L");

    private static bool IsExactlyCircular(EquinoctialElements e) => e.F == 0.0 && e.G == 0.0;
    private static bool IsExactlyEquatorial(EquinoctialElements e) => e.H == 0.0 && e.K == 0.0;

    // The classes of the (K) paths, whose solver error grows with the eccentricity
    private static string EccentricityClass(EquinoctialReference.EquinoctialCase c)
    {
        double e = System.Math.Sqrt(c.F * c.F + c.G * c.G);
        return c.Group == "large L" ? "large L"
            : e > 1.0 ? "hyperbola"
            : e > 0.5 ? "e 0.9"
            : "e <= 0.05";
    }

    [Fact]
    public void Reference_TakesTheGmOfTheKernels()
    {
        // The reference is exact for the GM of the Earth of these tests, read as the library reads it (CSPICE); the
        // closed form agrees with the classical rotations at 60 digits
        Assert.Equal(_reference.Gm["gm_de440"], _earth.GM);
        Assert.True(_reference.WalkerVersusClassical < 1e-45, $"{_reference.WalkerVersusClassical:E2} m");
        Assert.Equal(62, _reference.Equinoctial.Count);
        Assert.Contains(_reference.Equinoctial, c => double.IsNegative(c.F) && c.F == 0.0);
        Assert.Contains(_reference.Equinoctial, c => double.IsNegative(c.K) && c.K == 0.0);
    }

    [Fact]
    public void DirectState_MatchesTheReference()
    {
        // Arrange
        var cases = DirectCases.ToList();

        // Act
        var errors = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch, Frames.Frame.ICRF)
            .ToStateVector()))).ToList();

        // Assert; before #452, up to 2r for the special cases, NaN for h or k beyond 1.3e154 in the first version of
        // #452. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 3.07e-16 (suite
        // 153, hyperbola, Windows; 2.06e-16 on Linux and macOS arm64, L = 1e6 rad) and 2.67e-16 (suite 155)
        var worst = Report("DirectState_MatchesTheReference", errors);
        Assert.Equal(62, cases.Count);
        Assert.True(worst.Position < DirectPosition, $"position {worst.Position:E2}");
        Assert.True(worst.Velocity < DirectVelocity, $"velocity {worst.Velocity:E2}");
    }

    [Fact]
    public void KeplerianPath_MatchesTheReference()
    {
        // Arrange (K)
        var cases = KeplerianCases.ToList();

        // Act: through the Keplerian elements, and through AtEpoch at the epoch of the elements, as the start of a
        // propagation and ToStateVector(Time)
        var throughKeplerian = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch,
            Frames.Frame.ICRF).ToKeplerianElements().ToStateVector()))).ToList();
        var atEpoch = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch,
            Frames.Frame.ICRF).AtEpoch(Epoch).ToStateVector()))).ToList();

        // Assert; before #452, up to 2r for the special cases and those whose products of ω underflow (1.22 |r| for
        // h = k = 5e-324). Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), and
        // measured again after #453: 1.19e-15 (e = 0.9) and 7.25e-16 (h = −6.73e-320), the same on both paths
        var worstKeplerian = Report("KeplerianPath_MatchesTheReference, ToKeplerianElements", throughKeplerian);
        var worstAtEpoch = Report("KeplerianPath_MatchesTheReference, AtEpoch", atEpoch);
        Assert.Equal(53, cases.Count);
        foreach (var worst in new[] { worstKeplerian, worstAtEpoch })
        {
            Assert.True(worst.Position < 2.4e-15, $"position {worst.Position:E2}");
            Assert.True(worst.Velocity < 1.5e-15, $"velocity {worst.Velocity:E2}");
        }
    }

    [Fact]
    public void KeplerianPath_AtALargeTrueLongitude_MatchesTheReference()
    {
        // Arrange (K): L from 40 rad to ±1e10 rad, circular, equatorial or both. The special angles reduce L through its
        // sine and cosine; NormalizeAngle, a remainder by the double 2π, lost 2.4e-16 rad per turn (3.9e-7 at 1e10 rad)
        var cases = LargeTrueLongitudes.ToList();

        // Act
        var direct = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch, Frames.Frame.ICRF)
            .ToStateVector()))).ToList();
        var throughKeplerian = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch,
            Frames.Frame.ICRF).ToKeplerianElements().ToStateVector()))).ToList();
        var atEpoch = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch,
            Frames.Frame.ICRF).AtEpoch(Epoch).ToStateVector()))).ToList();

        // Assert: the direct state within its bounds. Twice the worst over Linux, Windows and macOS arm64 (CI run
        // 40280932636, 2026-10-26), and measured again after #453: 1.39e-15 and 1.28e-15 (L = 40 rad) through the
        // Keplerian elements and AtEpoch
        var worstDirect = Report("KeplerianPath_AtALargeTrueLongitude, direct", direct);
        var worst = Report("KeplerianPath_AtALargeTrueLongitude, ToKeplerianElements and AtEpoch",
            throughKeplerian.Concat(atEpoch).ToList());
        Assert.Equal(5, cases.Count);
        Assert.True(worstDirect.Position < DirectPosition && worstDirect.Velocity < DirectVelocity, $"direct {worstDirect}");
        Assert.True(worst.Position < 2.8e-15, $"position {worst.Position:E2}");
        Assert.True(worst.Velocity < 2.6e-15, $"velocity {worst.Velocity:E2}");
    }

    [Fact]
    public void AtAnotherEpoch_MatchesTheTwoBodyReference()
    {
        // Arrange (K): the cases of the Keplerian path and the large true longitudes, 600 s later and 3000 s earlier
        var cases = KeplerianCases.Concat(LargeTrueLongitudes).ToList();
        var bounds = new Dictionary<string, (double Position, double Velocity)>
        {
            // Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), and measured again
            // after #453: 1.59e-15 and 1.57e-15 (f = 1e-12, −3000 s); 8.35e-15, 4.74e-15; 4.67e-14, 1.37e-14; 1.55e-15,
            // 1.60e-15 (L = 1e3 rad, Windows)
            ["e <= 0.05"] = (3.2e-15, 3.2e-15), ["e 0.9"] = (1.7e-14, 9.5e-15), ["hyperbola"] = (9.4e-14, 2.8e-14),
            ["large L"] = (3.2e-15, 3.3e-15)
        };

        // Act: ToStateVector(Time) is AtEpoch(Time).ToStateVector(); TrueLongitude(Time) takes the true anomaly there
        var errors = cases.SelectMany(c => new[] { 600.0, -3000.0 }.Select(dt => (Class: EccentricityClass(c),
            Name: $"{c.Name}, dt {dt} s", Error: c.At(dt).RelativeError(c.Elements(_earth, Epoch, Frames.Frame.ICRF)
                .ToStateVector(Epoch + TimeSpan.FromSeconds(dt))), Longitude: EquinoctialReference.AngleError(
                c.Elements(_earth, Epoch, Frames.Frame.ICRF).TrueLongitude(Epoch + TimeSpan.FromSeconds(dt)),
                c.At(dt).TrueLongitude!.Value)))).ToList();

        // Assert
        Assert.Equal(58, cases.Count);
        foreach (var group in errors.GroupBy(e => e.Class))
        {
            var worst = Report($"AtAnotherEpoch_MatchesTheTwoBodyReference, {group.Key}",
                group.Select(e => (e.Name, e.Error)).ToList());
            Assert.True(worst.Position < bounds[group.Key].Position, $"{group.Key}: position {worst.Position:E2}");
            Assert.True(worst.Velocity < bounds[group.Key].Velocity, $"{group.Key}: velocity {worst.Velocity:E2}");
        }

        // The true longitude at the other epoch, in rad. Twice the worst over Linux, Windows and macOS arm64 (CI run
        // 40280932636, 2026-10-26), and measured again after #453: 2.25e-14 rad (the hyperbola, −3000 s)
        var worstLongitude = errors.MaxBy(e => Worst(e.Longitude));
        _output.WriteLine($"MEASURE #452 AtAnotherEpoch_MatchesTheTwoBodyReference, TrueLongitude(Time), worst: " +
                          $"{worstLongitude.Longitude:E2} rad ({worstLongitude.Name})");
        Assert.True(Worst(worstLongitude.Longitude) < 4.6e-14, $"{worstLongitude.Longitude:E2} rad ({worstLongitude.Name})");
    }

    [Fact]
    public void AtATrueAnomaly_MatchesTheReference()
    {
        // Arrange (K): ToStateVector(ν), which maneuvers use for their target orbit, at ν = 0.5 rad, the argument of
        // latitude of a circular orbit: its epoch (the next passage on an ellipse) and its state
        var cases = KeplerianCases.Concat(LargeTrueLongitudes).ToList();

        // Act
        var results = cases.Select(c =>
        {
            var state = c.Elements(_earth, Epoch, Frames.Frame.ICRF).ToStateVector(c.TrueAnomaly!.Value);
            var dt = c.TimeToTrueAnomaly!.Value;

            // The epoch is a whole number of 100 ns: the state is compared with the reference moved by the same δ
            double delta = (state.Epoch - Epoch).TotalSeconds - dt.High - dt.Low;
            return (Class: EccentricityClass(c), c.Name, Delta: System.Math.Abs(delta),
                Error: c.AtTrueAnomaly.MovedBy(delta, _earth.GM).RelativeError(state));
        }).ToList();

        // Assert. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), and measured
        // again after #453: 1.29e-15 and 1.29e-15 (h = −6.73e-320); 1.30e-14, 6.79e-15 (e = 0.9); 3.51e-16 (Linux),
        // 8.43e-17 (Windows and macOS arm64) (the hyperbola); 8.15e-16, 8.59e-16 (L = 1e10 rad). The epoch within a
        // tick of the time, 100 ns: 9.43e-8 s
        var bounds = new Dictionary<string, (double Position, double Velocity)>
        {
            ["e <= 0.05"] = (2.6e-15, 2.6e-15), ["e 0.9"] = (2.7e-14, 1.4e-14), ["hyperbola"] = (7.1e-16, 1.7e-16),
            ["large L"] = (1.7e-15, 1.8e-15)
        };
        Assert.Equal(58, cases.Count);
        foreach (var group in results.GroupBy(r => r.Class))
        {
            var worst = Report($"AtATrueAnomaly_MatchesTheReference, {group.Key}",
                group.Select(r => (r.Name, r.Error)).ToList());
            var worstDelta = group.MaxBy(r => Worst(r.Delta));
            _output.WriteLine($"MEASURE #452 AtATrueAnomaly_MatchesTheReference, {group.Key}, epoch, worst: " +
                              $"{worstDelta.Delta:E2} s ({worstDelta.Name})");
            Assert.True(worst.Position < bounds[group.Key].Position, $"{group.Key}: position {worst.Position:E2}");
            Assert.True(worst.Velocity < bounds[group.Key].Velocity, $"{group.Key}: velocity {worst.Velocity:E2}");
            Assert.True(Worst(worstDelta.Delta) < 1.9e-7, $"{group.Key}: epoch {worstDelta.Delta:E2} s");
        }
    }

    [Fact]
    public void KeplerianToEquinoctial_SignedZeros()
    {
        // Arrange (K, through the true anomaly of the Keplerian elements): the Keplerian cases of the issue, the 16
        // circular equatorial (Ω, ω) in {0, 90, 180, 270}°², which reach 13 of the 16 sign patterns of the zeros (all 16
        // are given directly in DirectState_MatchesTheReference), and i = π
        var cases = _reference.Keplerian.Where(c => c.Group is "issue" or "circular equatorial" or "retro").ToList();

        // Act
        var equinoctial = cases.Select(c => (Case: c, Elements: c.Elements(_earth, Epoch).ToEquinoctial())).ToList();
        var errors = equinoctial.Select(e => (e.Case.Name, Error: e.Case.At(0.0).RelativeError(e.Elements.ToStateVector())))
            .ToList();

        // Assert: the reference is the 60-digit state of the Keplerian doubles, not ke.ToStateVector(), which carries
        // the error of the solver (#453); its distance is printed. Twice the worst over Linux, Windows and macOS arm64
        // (CI run 40280932636, 2026-10-26): 1.30e-15 and 1.28e-15 (h = −0.0, whose L from ToEquinoctial is 9.46 rad)
        foreach (var (c, elements) in equinoctial)
        {
            var keplerian = c.Elements(_earth, Epoch).ToStateVector();
            var state = elements.ToStateVector();
            var (position, velocity) = RelativeError(state, keplerian);
            _output.WriteLine($"MEASURE #452 KeplerianToEquinoctial_SignedZeros, {c.Name}: |eq - ke.ToStateVector()| " +
                              $"position {position:E2}, velocity {velocity:E2} " +
                              $"(relative); F {elements.F:R}, G {elements.G:R}, H {elements.H:R}, K {elements.K:R}");
        }

        var worst = Report("KeplerianToEquinoctial_SignedZeros", errors);
        Assert.Equal(25, cases.Count);
        Assert.Contains(equinoctial, e => e.Elements.F == 0.0 && double.IsNegative(e.Elements.F));
        Assert.Contains(equinoctial, e => e.Elements.H == 0.0 && double.IsNegative(e.Elements.H));
        Assert.True(worst.Position < 2.7e-15, $"position {worst.Position:E2}");
        Assert.True(worst.Velocity < 2.6e-15, $"velocity {worst.Velocity:E2}");
    }

    [Fact]
    public void KeplerianToEquinoctial_ThroughKeplerianElementsAndAtAnotherEpoch()
    {
        // Arrange (K)
        var cases = _reference.Keplerian.Where(c => c.Group is "issue" or "circular equatorial" or "retro").ToList();

        // Act
        var throughKeplerian = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch)
            .ToEquinoctial().ToKeplerianElements().ToStateVector()))).ToList();
        var later = cases.Select(c => (c.Name, Error: c.At(600.0).RelativeError(c.Elements(_earth, Epoch).ToEquinoctial()
            .ToStateVector(Epoch + TimeSpan.FromSeconds(600.0))))).ToList();

        // Assert. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), and measured
        // again after #453: 1.14e-15 and 1.09e-15; 1.23e-15 and 1.22e-15 (h = −0.0, macOS arm64)
        var worstKeplerian = Report("KeplerianToEquinoctial, ToKeplerianElements", throughKeplerian);
        var worstLater = Report("KeplerianToEquinoctial, ToStateVector(epoch + 600 s)", later);
        Assert.True(worstKeplerian.Position < 2.3e-15, $"position {worstKeplerian.Position:E2}");
        Assert.True(worstKeplerian.Velocity < 2.2e-15, $"velocity {worstKeplerian.Velocity:E2}");
        Assert.True(worstLater.Position < 2.5e-15, $"position {worstLater.Position:E2}");
        Assert.True(worstLater.Velocity < 2.5e-15, $"velocity {worstLater.Velocity:E2}");
    }

    [Fact]
    public void KeplerianToEquinoctial_HighEccentricity()
    {
        // Arrange (K): equatorial, e = 0.9 and the hyperbola e = 1.5, where the solver dominates
        var cases = _reference.Keplerian.Where(c => c.Group == "high eccentricity").ToList();

        // Act
        var direct = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch).ToEquinoctial()
            .ToStateVector()))).ToList();
        var throughKeplerian = cases.Select(c => (c.Name, Error: c.At(0.0).RelativeError(c.Elements(_earth, Epoch)
            .ToEquinoctial().ToKeplerianElements().ToStateVector()))).ToList();
        var later = cases.Select(c => (c.Name, Error: c.At(600.0).RelativeError(c.Elements(_earth, Epoch).ToEquinoctial()
            .ToStateVector(Epoch + TimeSpan.FromSeconds(600.0))))).ToList();

        // Assert. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26), and measured
        // again after #453: 1.43e-14 and 1.23e-14; 2.92e-14 and 2.53e-14 (Windows); 2.04e-14 and 1.80e-14 (e = 0.9)
        var worstDirect = Report("KeplerianToEquinoctial_HighEccentricity, ToStateVector", direct);
        var worstKeplerian = Report("KeplerianToEquinoctial_HighEccentricity, ToKeplerianElements", throughKeplerian);
        var worstLater = Report("KeplerianToEquinoctial_HighEccentricity, ToStateVector(epoch + 600 s)", later);
        Assert.Equal(2, cases.Count);
        Assert.True(worstDirect.Position < 2.9e-14 && worstDirect.Velocity < 2.5e-14, $"direct {worstDirect}");
        Assert.True(worstKeplerian.Position < 5.9e-14 && worstKeplerian.Velocity < 5.1e-14, $"Keplerian {worstKeplerian}");
        Assert.True(worstLater.Position < 4.1e-14 && worstLater.Velocity < 3.7e-14, $"600 s {worstLater}");
    }

    [Fact]
    public void Angles_FollowTheConvention()
    {
        // Arrange: every case of the direct state
        var cases = DirectCases.Select(c => (Case: c, Elements: c.Elements(_earth, Epoch, Frames.Frame.ICRF))).ToList();

        // Act and assert: Ω = 0 for an equatorial orbit and ω = 0 for a circular one, as StateVector; ω and ν in [0, 2π]
        // in the special branches (f = g = 0, h = k = 0, or the products of ω that underflow), never −0.0; every angle,
        // the true longitude and the mean longitude against the 60-digit values of the conventions, modulo 2π
        var errors = new Dictionary<string, List<(string Name, double Error)>>
        {
            ["Ω"] = [], ["ω"] = [], ["ν"] = [], ["TrueLongitude()"] = [], ["MeanLongitude()"] = []
        };
        foreach (var (c, e) in cases)
        {
            double node = e.AscendingNode(), periapsis = e.ArgumentOfPeriapsis(), nu = e.TrueAnomaly();
            bool special = IsExactlyCircular(e) || IsExactlyEquatorial(e);
            if (IsExactlyEquatorial(e))
            {
                Assert.Equal(0.0, node);
                Assert.False(double.IsNegative(node), c.Name);
            }

            if (IsExactlyCircular(e))
            {
                Assert.Equal(0.0, periapsis);
            }

            if (special || c.Group == "underflow")
            {
                Assert.InRange(periapsis, 0.0, IO.Astrodynamics.Constants._2PI);
                Assert.False(double.IsNegative(periapsis), c.Name);
            }

            if (special)
            {
                Assert.InRange(nu, 0.0, IO.Astrodynamics.Constants._2PI);
                Assert.False(double.IsNegative(nu), c.Name);
            }

            errors["Ω"].Add((c.Name, EquinoctialReference.AngleError(node, c.Angles.Node)));
            errors["ω"].Add((c.Name, EquinoctialReference.AngleError(periapsis, c.Angles.Periapsis)));
            errors["ν"].Add((c.Name, EquinoctialReference.AngleError(nu, c.Angles.TrueAnomaly)));
            errors["TrueLongitude()"].Add((c.Name, EquinoctialReference.AngleError(e.TrueLongitude(),
                c.At(0.0).TrueLongitude!.Value)));
            errors["MeanLongitude()"].Add((c.Name, EquinoctialReference.AngleError(e.MeanLongitude(),
                c.MeanLongitude!.Value)));
        }

        // Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 1.92e-16 rad (circular,
        // Ω 200°), 5.68e-16 rad (h = −6.73e-320), 4.14e-16 rad (circular, Ω 200°), 4.44e-16 rad (f = −g = −h = k =
        // 1e-200) and 1.08e-15 rad (suite 152, e = 0.9)
        var bounds = new Dictionary<string, double>
        {
            ["Ω"] = 3.9e-16, ["ω"] = 1.2e-15, ["ν"] = 8.3e-16, ["TrueLongitude()"] = 8.9e-16, ["MeanLongitude()"] = 2.2e-15
        };
        Assert.Equal(62, cases.Count);
        foreach (var (quantity, list) in errors)
        {
            var worst = list.MaxBy(x => Worst(x.Error));
            _output.WriteLine($"MEASURE #452 Angles_FollowTheConvention, {quantity}, worst: {worst.Error:E2} rad " +
                              $"({worst.Name}), {list.Count} cases");
            Assert.True(Worst(worst.Error) < bounds[quantity], $"{quantity}: {worst.Error:E2} rad ({worst.Name})");
        }
    }

    public static TheoryData<string> RoundTripStates => new()
    {
        "prograde, tilt 0", "prograde, tilt 5e-11", "prograde, tilt 2e-10", "prograde, tilt 1e-9", "retrograde, tilt 0",
        "retrograde, tilt 5e-11", "retrograde, tilt 2e-10", "retrograde, tilt 1e-9",
        "circular equatorial, round-off eccentricity", "circular equatorial retrograde, round-off eccentricity",
        "generic", "parabolic at periapsis, inclined", "prograde, tilt 0, in TEME"
    };

    [Theory]
    [MemberData(nameof(RoundTripStates))]
    public void StateRoundTrip_ThroughEquinoctial(string name)
    {
        // Arrange
        var state = RoundTripState(name);

        // Act: directly, and (K) through the Keplerian elements
        var equinoctial = state.ToEquinoctial();
        var back = equinoctial.ToStateVector();
        var backThroughKeplerian = equinoctial.ToKeplerianElements().ToStateVector();

        // Assert. Within the equatorial tolerance StateVector drops the tilt, as SPICE does, so the out-of-plane
        // velocity does not come back: the rule of StateVectorAngleTests (5.00e-11 measured at a tilt of 5e-11).
        // Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 7.41e-16 for the
        // position, (K) 1.41e-15 (retrograde, tilt 1e-9), and 5.77e-16 for the velocity (retrograde, tilt 0), (K)
        // 1.31e-15 (retrograde, tilt 1e-9)
        var spice = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        bool droppedTilt = spice.RAAN == 0.0 && (spice.I == 0.0 || spice.I == System.Math.PI) &&
                           state.SpecificAngularMomentum().X != 0.0;
        var (position, velocity) = RelativeError(back, state);
        var (positionK, velocityK) = RelativeError(backThroughKeplerian, state);
        _output.WriteLine($"MEASURE #452 StateRoundTrip_ThroughEquinoctial, {name}: position {position:E2}, velocity " +
                          $"{velocity:E2} (relative); (K) position {positionK:E2}, velocity {velocityK:E2}; tilt dropped " +
                          $"{droppedTilt}; P {equinoctial.P:R}, F {equinoctial.F:R}, G {equinoctial.G:R}, " +
                          $"H {equinoctial.H:R}, K {equinoctial.K:R}");
        Assert.Equal(state.Frame, back.Frame);
        Assert.Equal(state.Epoch, back.Epoch);
        Assert.True(position < 1.5e-15, $"position {position:E2}");
        Assert.True(positionK < 2.9e-15, $"(K) position {positionK:E2}");
        Assert.True(velocity < (droppedTilt ? StateVector.EquatorialTolerance : 1.2e-15), $"velocity {velocity:E2}");
        Assert.True(velocityK < (droppedTilt ? StateVector.EquatorialTolerance : 2.7e-15), $"(K) velocity {velocityK:E2}");
    }

    public static TheoryData<double, double> NearParabolicStates => new()
    {
        // e − 1, true anomaly rad
        { -5e-7, 0.0 }, { -5e-7, 0.5 }, { 5e-7, 0.0 }, { 5e-7, 0.5 }
    };

    [Theory]
    [MemberData(nameof(NearParabolicStates))]
    public void StateRoundTrip_NearParabolic_ThroughEquinoctial(double eccentricityMinusOne, double trueAnomaly)
    {
        // Arrange: a state within the parabolic band, |e − 1| < 1e-6, q = 7000 km, i = 30°. Its ToEquinoctial has
        // P = ±∞ (out-of-scope defect (a)) and its perigee radius: the direct state takes p = q (1 + e), the conic of its
        // eccentricity, as SPICE CONICS (2q before, 1.75 m off at e = 1 − 5e-7)
        double e = 1.0 + eccentricityMinusOne, q = 7000.0e3, inclination = 30.0 * IO.Astrodynamics.Constants.Deg2Rad;
        double p = q * (1.0 + e), r = p / (1.0 + e * System.Math.Cos(trueAnomaly));
        double speed = System.Math.Sqrt(_earth.GM / p);
        double c = System.Math.Cos(inclination), s = System.Math.Sin(inclination);
        var state = new StateVector(
            new Vector3(r * System.Math.Cos(trueAnomaly), r * System.Math.Sin(trueAnomaly) * c,
                r * System.Math.Sin(trueAnomaly) * s),
            new Vector3(-speed * System.Math.Sin(trueAnomaly), speed * (e + System.Math.Cos(trueAnomaly)) * c,
                speed * (e + System.Math.Cos(trueAnomaly)) * s), _earth, Epoch, Frames.Frame.ICRF);

        // Act
        var equinoctial = state.ToEquinoctial();
        var (position, velocity) = RelativeError(equinoctial.ToStateVector(), state);

        // Assert, directly only: the Keplerian path of a near-parabolic orbit is out-of-scope defect (f), its own issue.
        // Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 2.86e-16 and 2.49e-16
        _output.WriteLine($"MEASURE #452 StateRoundTrip_NearParabolic, e - 1 = {eccentricityMinusOne:R}, ν = " +
                          $"{trueAnomaly:R}: position {position:E2}, velocity {velocity:E2} (relative); P {equinoctial.P:R}");
        Assert.True(equinoctial.IsParabolic() && !double.IsFinite(equinoctial.P), $"P {equinoctial.P:R}");
        Assert.True(position < 5.8e-16, $"position {position:E2}");
        Assert.True(velocity < 5.0e-16, $"velocity {velocity:E2}");
    }

    public static TheoryData<string, double, double, double, double, double> TemeStates => new()
    {
        // a m, e, i rad, ω rad, M rad; KeplerianElements in TEME, Ω = 0
        { "8000 km, e 0.1, ϖ 45°, i 0", 8000.0e3, 0.1, 0.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.3 },
        { "8000 km, e 0.1, ϖ 45°, i 5e-11", 8000.0e3, 0.1, 5e-11, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.3 },
        { "8000 km, e 0.1, ϖ 45°, i 1e-9", 8000.0e3, 0.1, 1e-9, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.3 },
        { "7000 km, e 0.001, i 0", 7000.0e3, 0.001, 0.0, 0.0, 1.0 }
    };

    [Theory]
    [MemberData(nameof(TemeStates))]
    public void ToTle_OfAnEquatorialTemeState(string name, double a, double e, double i, double periapsis, double m)
    {
        // Arrange: the state given directly in TEME, as TLETests, so that the fit does not depend on the rotation
        // between ICRF and TEME, which #451 changes. Within 1e-10 rad of the equator, the fit threw after 15 iterations
        // before #452 (best 111 467.53 m and 522 853.27 m at this epoch, 2021-03-20 12:00 TDB)
        var s = new KeplerianElements(a, e, i, 0.0, periapsis, m, _earth, Epoch, Frames.Frame.TEME).ToStateVector();
        var target = new StateVector(s.Position, s.Velocity, _earth, Epoch, Frames.Frame.TEME);
        var configuration = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(25666, "TEST", "98067A");

        // Act
        var tle = target.ToTLE(configuration);
        var fitted = tle.ToStateVector(Epoch);

        // Assert, in m and m/s as TLETests, and the two lines of the fit at i = 1e-9 rad, which never took the special
        // branches. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 1.588 m and
        // 2.30e-3 m/s at i = 0, 5e-11 and 1e-9 rad; 3.161 m and 3.45e-3 m/s at e = 0.001
        double position = (fitted.Position - target.Position).Magnitude();
        double velocity = (fitted.Velocity - target.Velocity).Magnitude();
        var atOneNanoradian = new KeplerianElements(a, e, 1e-9, 0.0, periapsis, m, _earth, Epoch, Frames.Frame.TEME)
            .ToStateVector();
        var tleAtOneNanoradian = new StateVector(atOneNanoradian.Position, atOneNanoradian.Velocity, _earth, Epoch,
            Frames.Frame.TEME).ToTLE(configuration);
        bool sameLines = tle.Line1 == tleAtOneNanoradian.Line1 && tle.Line2 == tleAtOneNanoradian.Line2;
        _output.WriteLine($"MEASURE #452 ToTle_OfAnEquatorialTemeState, {name}: position {position:F3} m, velocity " +
                          $"{velocity:E2} m/s; the lines of i = 1e-9 rad: {sameLines}");
        _output.WriteLine($"  {tle.Line1}{Environment.NewLine}  {tle.Line2}");
        Assert.Equal(Frames.Frame.TEME, fitted.Frame);
        Assert.True(sameLines, $"{tle.Line2} / {tleAtOneNanoradian.Line2}");
        bool nearlyCircular = e < 0.01;
        Assert.True(position < (nearlyCircular ? 6.4 : 3.2), $"position {position:F3} m");
        Assert.True(velocity < (nearlyCircular ? 7.0e-3 : 4.7e-3), $"velocity {velocity:E2} m/s");
    }

    [Fact]
    public void ToTle_OfEquatorialElementsInIcrf()
    {
        // Arrange: the elements of the conformance case 156 (h = k = 0) in ICRF. The fit takes its target from
        // ToFrame(TEME), and so from the direct state; before #452 it fitted a state 13 248 km away, without an error.
        // The target here is the 60-digit state rotated to TEME by the same rotation
        var c = _reference.Case("equatorial, e 0.05, lop 120 (suite 156)");
        var reference = c.At(0.0);
        var target = new StateVector(new Vector3(reference.PositionHigh[0], reference.PositionHigh[1],
                reference.PositionHigh[2]), new Vector3(reference.VelocityHigh[0], reference.VelocityHigh[1],
                reference.VelocityHigh[2]), _earth, Epoch, Frames.Frame.ICRF).ToFrame(Frames.Frame.TEME)
            .ToStateVector();
        var configuration = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(25666, "TEST", "98067A");

        // Act
        var tle = c.Elements(_earth, Epoch, Frames.Frame.ICRF).ToTLE(configuration);
        var fitted = tle.ToStateVector(Epoch);

        // Assert, in m and m/s. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26):
        // 3.302 m and 3.20e-3 m/s
        double position = (fitted.Position - target.Position).Magnitude();
        double velocity = (fitted.Velocity - target.Velocity).Magnitude();
        _output.WriteLine($"MEASURE #452 ToTle_OfEquatorialElementsInIcrf, {c.Name}: position {position:F3} m, velocity " +
                          $"{velocity:E2} m/s");
        _output.WriteLine($"  {tle.Line1}{Environment.NewLine}  {tle.Line2}");
        Assert.Equal(Frames.Frame.TEME, fitted.Frame);
        Assert.True(position < 6.7, $"position {position:F3} m");
        Assert.True(velocity < 6.5e-3, $"velocity {velocity:E2} m/s");
    }

    [Fact]
    public void Spacecraft_NotPropagated_AndItsOpm_AreAtTheReference()
    {
        // Arrange: a spacecraft whose initial orbit is equinoctial elements of the issue, not propagated: its state is
        // that of its orbit (Spacecraft.GetGeometricStateRelativeTo), which the conjunction analysis reads through
        // GetEphemeris, and its OPM takes ToStateVector(epoch). Before #452, 4 788 km off for the case 155, 13 248 km for
        // the case 156
        var cases = _reference.Equinoctial.Where(c => c.Group == "issue").ToList();

        // Act
        var errors = new List<(string Name, (double Position, double Velocity) Error)>();
        var later = new List<(string Name, (double Position, double Velocity) Error)>();
        foreach (var c in cases)
        {
            var spacecraft = new Spacecraft(-1452, "OPM452", 100.0, 1000.0, new Clock("opm452", 256),
                c.Elements(_earth, Epoch, Frames.Frame.ICRF), 1.0, 2.2, "2021-001A");
            var ephemeris = spacecraft.GetEphemeris(Epoch, _earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var opm = spacecraft.ToOpm().Data.StateVector;
            var fromOpm = new StateVector(new Vector3(opm.X * 1000.0, opm.Y * 1000.0, opm.Z * 1000.0),
                new Vector3(opm.XDot * 1000.0, opm.YDot * 1000.0, opm.ZDot * 1000.0), _earth, Epoch, Frames.Frame.ICRF);
            errors.Add(($"{c.Name}, GetEphemeris", c.At(0.0).RelativeError(ephemeris)));
            errors.Add(($"{c.Name}, OPM", c.At(0.0).RelativeError(fromOpm)));
            later.Add(($"{c.Name}, GetEphemeris at +600 s", c.At(600.0).RelativeError(spacecraft.GetEphemeris(
                Epoch + TimeSpan.FromSeconds(600.0), _earth, Frames.Frame.ICRF, Aberration.None).ToStateVector())));
        }

        // Assert; the OPM, in km, gives the same doubles here. Twice the worst over Linux, Windows and macOS arm64 (CI
        // run 40280932636, 2026-10-26), and (K) measured again after #453: 1.19e-15 and 5.53e-16 at the epoch, 1.09e-15
        // and 6.50e-16 600 s later (e = 0.9)
        var worst = Report("Spacecraft_NotPropagated_AndItsOpm", errors);
        var worstLater = Report("Spacecraft_NotPropagated, at another epoch", later);
        Assert.Equal(11, cases.Count);
        Assert.True(worst.Position < 2.4e-15 && worst.Velocity < 1.2e-15, $"{worst}");
        Assert.True(worstLater.Position < 2.2e-15 && worstLater.Velocity < 1.4e-15, $"{worstLater}");
    }

    [Fact]
    public void MeanElements_ToStateVector_Throws()
    {
        // Arrange: mean circular, equatorial and regular elements
        var cases = _reference.TleCreate.Select(c => new EquinoctialElements(c.P, c.F, c.G, c.H, c.K, c.L, _earth, Epoch,
            Frames.Frame.TEME, elementsType: OrbitalElementsType.Mean)).Append(_reference.Case("suite 151, generic")
            .Elements(_earth, Epoch, Frames.Frame.TEME, OrbitalElementsType.Mean)).ToList();

        // Act and assert: the exception and the messages of OrbitalParameters, unchanged
        foreach (var elements in cases)
        {
            var atEpoch = Assert.Throws<InvalidOperationException>(() => elements.ToStateVector());
            var atTime = Assert.Throws<InvalidOperationException>(() =>
                elements.ToStateVector(Epoch + TimeSpan.FromSeconds(60.0)));
            Assert.Equal("Cannot convert mean elements directly to a state vector. Mean elements require a specific " +
                         "propagator (e.g., SGP4/SDP4) for accurate position calculations. For TLE data, use " +
                         "TLE.ToOsculating() instead.", atEpoch.Message);
            Assert.Equal("Cannot convert mean elements directly to a state vector. Mean elements require a specific " +
                         "propagator (e.g., SGP4/SDP4) for accurate position calculations. For TLE data, use " +
                         "TLE.ToOsculating(epoch) instead.", atTime.Message);
        }
    }

    [Fact]
    public void Parabolic_FromKeplerianElements_TakesThePerigeeRadius()
    {
        // Arrange: KeplerianElements(∞, 1, …, perigee radius).ToEquinoctial() gives P = ∞ × 0 = NaN (out-of-scope
        // defect (a)): the state comes from the perigee radius, p = q (1 + e), 2q at e = 1
        var cases = _reference.Keplerian.Where(c => c.Group == "parabolic").ToList();

        // Act
        var equinoctial = cases.Select(c => (c.Name, State: c.At(0.0), Elements: c.Elements(_earth, Epoch).ToEquinoctial()))
            .ToList();
        var errors = equinoctial.Select(e => (e.Name, Error: e.State.RelativeError(e.Elements.ToStateVector()))).ToList();

        // Assert. P is NaN today: this assertion fails once (a) is fixed, which then states the P it expects.
        // Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 2.41e-16 (macOS arm64)
        // and 2.25e-16
        var worst = Report("Parabolic_FromKeplerianElements", errors);
        Assert.Equal(3, cases.Count);
        Assert.All(equinoctial, e => Assert.True(double.IsNaN(e.Elements.P)));
        Assert.True(worst.Position < 4.9e-16, $"position {worst.Position:E2}");
        Assert.True(worst.Velocity < 4.6e-16, $"velocity {worst.Velocity:E2}");
    }

    [Fact]
    public void Parabolic_WithAFinitePOrAPerigeeRadius_MatchesTheReference()
    {
        // Arrange: P = 14 000 km without a perigee radius, which overflowed the stack before #452 (PerigeeRadius,
        // SemiLatusRectum, SpecificAngularMomentum, ToStateVector and so on); P with a perigee radius, where P wins;
        // P = NaN with a perigee radius, p = q (1 + e); and within the parabolic band, e = 1 ∓ 9e-7: a finite P, which
        // the state takes (before #452, 2q: 3.18 m off), and P = ±∞ with the perigee radius, p = q (1 + e) (2q in the
        // first version of #452: 3.18 m off)
        var cases = _reference.Parabolic;

        // Act
        var states = cases.Select(c => (Case: c, State: c.Elements(_earth, Epoch, Frames.Frame.ICRF).ToStateVector()))
            .ToList();
        var errors = states.Select(s => (s.Case.Name, Error: s.Case.At(0.0).RelativeError(s.State))).ToList();
        var withoutPerigeeRadius = cases.Single(c => c.PerigeeRadiusReference.HasValue);
        double perigeeRadius = withoutPerigeeRadius.Elements(_earth, Epoch, Frames.Frame.ICRF).PerigeeRadius();
        double perigeeRadiusError = System.Math.Abs(perigeeRadius - withoutPerigeeRadius.PerigeeRadiusReference!.Value) /
                                    withoutPerigeeRadius.PerigeeRadiusReference.Value;

        // The state has its periapsis at the perigee radius of the elements, where one is given
        var periapsis = states.Where(s => s.Case.PerigeeRadius.HasValue).Select(s => (s.Case.Name,
            Error: System.Math.Abs(s.State.PerigeeRadius() - s.Case.PerigeeRadius!.Value) / s.Case.PerigeeRadius.Value))
            .ToList();

        // Assert. Twice the worst over Linux, Windows and macOS arm64 (CI run 40280932636, 2026-10-26): 9.58e-17 and
        // 8.84e-17 (e = 1), 1.85e-16 and 1.68e-16 (|e − 1| = 9e-7); PerigeeRadius() 7 000 000.000000002 m for
        // P / (1 + e) = 7 000 km, and the periapsis of the states, 2.66e-16
        var worst = Report("Parabolic_WithAFinitePOrAPerigeeRadius, e = 1",
            errors.Where(e => !e.Name.StartsWith("near-parabolic")).ToList());
        var worstBand = Report("Parabolic_WithAFinitePOrAPerigeeRadius, |e - 1| = 9e-7",
            errors.Where(e => e.Name.StartsWith("near-parabolic")).ToList());
        _output.WriteLine($"MEASURE #452 Parabolic_WithAFinitePOrAPerigeeRadius, PerigeeRadius() without a perigee radius: " +
                          $"{perigeeRadius:R} m, {perigeeRadiusError:E2} (relative)");
        var worstPeriapsis = periapsis.MaxBy(p => Worst(p.Error));
        _output.WriteLine($"MEASURE #452 Parabolic_WithAFinitePOrAPerigeeRadius, periapsis of the state against the perigee " +
                          $"radius, worst: {worstPeriapsis.Error:E2} (relative, {worstPeriapsis.Name})");
        Assert.Equal(6, cases.Count);
        Assert.True(worst.Position < 2.0e-16, $"position {worst.Position:E2}");
        Assert.True(worst.Velocity < 1.8e-16, $"velocity {worst.Velocity:E2}");
        Assert.True(worstBand.Position < 3.8e-16, $"position {worstBand.Position:E2}");
        Assert.True(worstBand.Velocity < 3.4e-16, $"velocity {worstBand.Velocity:E2}");
        Assert.True(perigeeRadiusError < 5.4e-16, $"perigee radius {perigeeRadiusError:E2}");
        Assert.True(Worst(worstPeriapsis.Error) < 5.4e-16, $"periapsis {worstPeriapsis.Error:E2} ({worstPeriapsis.Name})");
    }

    [Fact]
    public void WithoutAFinitePOrAPerigeeRadius_TheStateIsUndefined()
    {
        // Arrange: a parabola with P = NaN or +∞ and no perigee radius, and an ellipse with P = NaN
        var cases = _reference.Undefined;

        // Act and assert: an exception instead of a stack overflow (parabola) or a NaN state (ellipse)
        Assert.Equal(3, cases.Count);
        foreach (var c in cases)
        {
            var elements = c.Elements(_earth, Epoch, Frames.Frame.ICRF);
            var exception = Assert.Throws<InvalidOperationException>(() => elements.ToStateVector());
            Assert.Contains("P is not finite and no perigee radius", exception.Message);
            if (elements.IsParabolic())
            {
                Assert.Throws<InvalidOperationException>(() => elements.PerigeeRadius());
            }
        }
    }

    [Fact]
    public void OtherFrames_KeepTheirFrame()
    {
        // Arrange: the elements of the conformance cases 155 and 156
        foreach (var name in new[] { "circular, i 30, node 40 (suite 155)", "equatorial, e 0.05, lop 120 (suite 156)" })
        {
            var c = _reference.Case(name);
            var icrf = c.Elements(_earth, Epoch, Frames.Frame.ICRF).ToStateVector();
            foreach (var frame in new[] { Frames.Frame.TEME, Frames.Frame.ECLIPTIC_J2000, new Frames.Frame("ITRF93") })
            {
                // Act
                var state = c.Elements(_earth, Epoch, frame).ToStateVector();

                // Assert: the same numbers in the frame, at the epoch and about the observer of the elements
                Assert.Equal(frame, state.Frame);
                Assert.Equal(Epoch, state.Epoch);
                Assert.Equal(_earth, state.Observer);
                Assert.Equal(icrf.Position, state.Position);
                Assert.Equal(icrf.Velocity, state.Velocity);
            }
        }
    }

    [Fact]
    public void TleCreate_MeanCircularAndEquatorial()
    {
        Assert.Equal(3, _reference.TleCreate.Count);
        foreach (var c in _reference.TleCreate)
        {
            // Arrange
            var elements = new EquinoctialElements(c.P, c.F, c.G, c.H, c.K, c.L, _earth, Epoch, Frames.Frame.TEME,
                elementsType: OrbitalElementsType.Mean);

            // Act
            var tle = TLE.Create(elements, "TEST", 25666, "98067A", 0);

            // Assert: the fields of line 2 at the 4 decimals of the TLE, from the closed form with Ω = 0 for an
            // equatorial orbit and ω = 0 for a circular one (before #452: M 115.0000° for the circular orbit, ω 0.0000°
            // for the equatorial one; ω -0.0000 for g = -0.0 in the first version of #452)
            var fields = tle.Line2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _output.WriteLine($"MEASURE #452 TleCreate_MeanCircularAndEquatorial, {c.Name}: {tle.Line2}");
            Assert.Equal(c.Inclination, fields[2]);
            Assert.Equal(c.Raan, fields[3]);
            Assert.Equal(c.ArgumentOfPerigee, fields[5]);
            Assert.Equal(c.MeanAnomaly, fields[6]);
        }
    }

    private (double Position, double Velocity) Report(string test,
        IReadOnlyList<(string Name, (double Position, double Velocity) Error)> errors)
    {
        foreach (var (name, (position, velocity)) in errors)
        {
            _output.WriteLine($"MEASURE #452 {test}, {name}: position {position:E2}, velocity {velocity:E2} (relative)");
        }

        // A NaN error is the worst (MaxBy would pass over it: NaN compares below every number)
        var worstPosition = errors.MaxBy(e => Worst(e.Error.Position));
        var worstVelocity = errors.MaxBy(e => Worst(e.Error.Velocity));
        _output.WriteLine($"MEASURE #452 {test}, worst: position {worstPosition.Error.Position:E2} ({worstPosition.Name}), " +
                          $"velocity {worstVelocity.Error.Velocity:E2} ({worstVelocity.Name}) (relative)");
        return (Worst(worstPosition.Error.Position), Worst(worstVelocity.Error.Velocity));
    }

    private static double Worst(double error) => double.IsNaN(error) ? double.PositiveInfinity : error;

    private static (double Position, double Velocity) RelativeError(StateVector computed, StateVector expected)
    {
        return ((computed.Position - expected.Position).Magnitude() / expected.Position.Magnitude(),
            (computed.Velocity - expected.Velocity).Magnitude() / expected.Velocity.Magnitude());
    }

    private StateVector RoundTripState(string name)
    {
        double gm = _earth.GM;
        double radius = 7000.0e3;
        return name switch
        {
            "prograde, tilt 0" => Tilted(1.0, 0.0, Frames.Frame.ICRF),
            "prograde, tilt 5e-11" => Tilted(1.0, 5e-11, Frames.Frame.ICRF),
            "prograde, tilt 2e-10" => Tilted(1.0, 2e-10, Frames.Frame.ICRF),
            "prograde, tilt 1e-9" => Tilted(1.0, 1e-9, Frames.Frame.ICRF),
            "retrograde, tilt 0" => Tilted(-1.0, 0.0, Frames.Frame.ICRF),
            "retrograde, tilt 5e-11" => Tilted(-1.0, 5e-11, Frames.Frame.ICRF),
            "retrograde, tilt 2e-10" => Tilted(-1.0, 2e-10, Frames.Frame.ICRF),
            "retrograde, tilt 1e-9" => Tilted(-1.0, 1e-9, Frames.Frame.ICRF),
            "circular equatorial, round-off eccentricity" => new StateVector(new Vector3(radius, 0.0, 0.0),
                new Vector3(0.0, System.Math.Sqrt(gm / radius), 0.0), _earth, Epoch, Frames.Frame.ICRF),
            "circular equatorial retrograde, round-off eccentricity" => new StateVector(new Vector3(radius, 0.0, 0.0),
                new Vector3(0.0, -System.Math.Sqrt(gm / radius), 0.0), _earth, Epoch, Frames.Frame.ICRF),
            "generic" => new StateVector(new Vector3(6800000.0, 1000.0, -500.0), new Vector3(200.0, 8000.0, -300.0), _earth,
                Epoch, Frames.Frame.ICRF),
            "parabolic at periapsis, inclined" => new StateVector(new Vector3(radius, 0.0, 0.0),
                new Vector3(0.0, System.Math.Sqrt(2.0 * gm / radius) * System.Math.Cos(0.3),
                    System.Math.Sqrt(2.0 * gm / radius) * System.Math.Sin(0.3)), _earth, Epoch, Frames.Frame.ICRF),
            "prograde, tilt 0, in TEME" => Tilted(1.0, 0.0, Frames.Frame.TEME),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        // StateVectorAngleTests.Tilted: in the equatorial plane at 30°, slightly eccentric, the velocity tilted out of
        // the plane by tilt
        StateVector Tilted(double sense, double tilt, Frames.Frame frame)
        {
            double angle = 30.0 * IO.Astrodynamics.Constants.Deg2Rad;
            var position = new Vector3(6.8e6 * System.Math.Cos(angle), 6.8e6 * System.Math.Sin(angle), 0.0);
            var along = new Vector3(-System.Math.Sin(angle), System.Math.Cos(angle), 0.0) * sense;
            var velocity = along * (7900.0 * System.Math.Cos(tilt)) + new Vector3(0.0, 0.0, 7900.0 * System.Math.Sin(tilt)) +
                           position.Normalize() * 50.0;
            return new StateVector(position, velocity, _earth, Epoch, frame);
        }
    }
}
