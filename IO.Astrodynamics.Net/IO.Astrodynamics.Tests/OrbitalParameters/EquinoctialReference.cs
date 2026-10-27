// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// Also compiled into IO.Astrodynamics.CLI.Tests, whose nullable context is enabled
#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The 60-digit reference of the tests of #452, <c>Data/EquinoctialElementsReference/equinoctial_reference.json</c>,
/// written by <c>tools/equinoctial_elements/equinoctial_reference.py</c>: the states of equinoctial and Keplerian
/// elements given as doubles, from the closed form of the GMAT Mathematical Specification R2026a, §3.1.4, Eqs 3.44 to
/// 3.47, with the true longitude of Walker, Ireland and Owens (1985; to be verified by S. Guillet), checked against the
/// classical rotations, and the exact two-body states at other epochs, all evaluated with mpmath at 60 digits.
/// </summary>
/// <remarks>
/// Each component of a state is a pair of doubles whose sum is the 60-digit value, so that the error of a state in
/// doubles is measured without the rounding of the reference to a double, half a unit in the last place, which is of
/// the order of the error measured (a reference must resolve better than what it measures). The inputs are written
/// with repr, so that −0.0 survives. The CLI tests link this file and the JSON (their kernels carry gm_de431).
/// </remarks>
internal sealed class EquinoctialReference
{
    internal const string ReferenceFile = "Data/EquinoctialElementsReference/equinoctial_reference.json";

    private EquinoctialReference(JsonElement root)
    {
        Gm = root.GetProperty("gm").EnumerateObject().ToDictionary(p => p.Name, p => Number(p.Value));
        WalkerVersusClassical = Number(root.GetProperty("walker_vs_classical_m"));
        Equinoctial = root.GetProperty("equinoctial").EnumerateArray().Select(EquinoctialCase.Read).ToList();
        Cli = root.GetProperty("cli").EnumerateArray().Select(EquinoctialCase.Read).ToList();
        Parabolic = root.GetProperty("parabolic").EnumerateArray().Select(EquinoctialCase.Read).ToList();
        Undefined = root.GetProperty("undefined").EnumerateArray().Select(EquinoctialCase.Read).ToList();
        Keplerian = root.GetProperty("keplerian").EnumerateArray().Select(KeplerianCase.Read).ToList();
        TleCreate = root.GetProperty("tle_create").EnumerateArray().Select(TleCreateCase.Read).ToList();
    }

    /// <summary>The Earth GM of the reference, m³/s², by kernel: gm_de440 for these tests, gm_de431 for the CLI
    /// tests.</summary>
    internal IReadOnlyDictionary<string, double> Gm { get; }

    /// <summary>The largest difference between the closed form and the classical rotations, m.</summary>
    internal double WalkerVersusClassical { get; }

    internal IReadOnlyList<EquinoctialCase> Equinoctial { get; }
    internal IReadOnlyList<EquinoctialCase> Cli { get; }
    internal IReadOnlyList<EquinoctialCase> Parabolic { get; }
    internal IReadOnlyList<EquinoctialCase> Undefined { get; }
    internal IReadOnlyList<KeplerianCase> Keplerian { get; }
    internal IReadOnlyList<TleCreateCase> TleCreate { get; }

    internal static EquinoctialReference Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        return new EquinoctialReference(document.RootElement);
    }

    internal EquinoctialCase Case(string name) => Equinoctial.Single(c => c.Name == name);

    /// <summary>A double of the file; the sign of a zero is checked, since the signed zeros are the subject of
    /// #452.</summary>
    internal static double Number(JsonElement element)
    {
        var text = element.GetString()!;
        double value = double.Parse(text, CultureInfo.InvariantCulture);
        Assert.True(double.IsNaN(value) || double.IsNegative(value) == text.StartsWith('-'), $"sign of {text}");
        return value;
    }

    // 2π as two doubles: 2π − TwoPiHigh = TwoPiLow to 1e-32
    private const double TwoPiHigh = 6.283185307179586;
    private const double TwoPiLow = 2.4492935982947064e-16;

    /// <summary>A pair [hi, lo] of the file: two doubles whose sum is the 60-digit value.</summary>
    internal static (double High, double Low) Pair(JsonElement pair) => (Number(pair[0]), Number(pair[1]));

    /// <summary>|<paramref name="computed"/> − reference| modulo 2π, the reference a pair [hi, lo] in [0, 2π), without
    /// the rounding of the subtraction or of 2π: the difference by TwoSum, the turn as two doubles.</summary>
    internal static double AngleError(double computed, (double High, double Low) reference)
    {
        double s = computed - reference.High;
        double bp = s - computed;
        double rounding = (computed - (s - bp)) + (-reference.High - bp);
        double turns = System.Math.Round(s / TwoPiHigh);
        return System.Math.Abs((s - turns * TwoPiHigh) + rounding - reference.Low - turns * TwoPiLow);
    }

    /// <summary>A reference state, <paramref name="dt"/> seconds after the epoch of the elements, with its true
    /// longitude modulo 2π.</summary>
    internal sealed record ReferenceState(double Dt, double[] PositionHigh, double[] PositionLow, double[] VelocityHigh,
        double[] VelocityLow, (double High, double Low)? TrueLongitude = null)
    {
        internal static ReferenceState Read(JsonElement state) => ReadAt(state, Number(state.GetProperty("dt")));

        internal static ReferenceState ReadAt(JsonElement state, double dt)
        {
            double[] Part(string vector, int index) =>
                state.GetProperty(vector).EnumerateArray().Select(c => Number(c[index])).ToArray();

            return new ReferenceState(dt, Part("r", 0), Part("r", 1), Part("v", 0), Part("v", 1),
                state.TryGetProperty("l", out var l) ? Pair(l) : null);
        }

        /// <summary>The reference state moved by <paramref name="delta"/> seconds along its orbit, to first order in
        /// the position and the velocity (a state computed at an epoch rounded to the 100 ns of the time, for one): its
        /// error is of the order of the acceleration times δ², 1e-14 m for δ = 100 ns.</summary>
        internal ReferenceState MovedBy(double delta, double gm)
        {
            var r = new Vector3(PositionHigh[0], PositionHigh[1], PositionHigh[2]);
            var acceleration = r * (-gm / System.Math.Pow(r.Magnitude(), 3));
            double[] Add(double[] low, double[] rate) => low.Select((x, i) => x + rate[i] * delta).ToArray();
            return this with
            {
                PositionLow = Add(PositionLow, VelocityHigh),
                VelocityLow = Add(VelocityLow, [acceleration.X, acceleration.Y, acceleration.Z])
            };
        }

        /// <summary>|Δr| / |r| and |Δv| / |v| of <paramref name="state"/>, the difference taken without rounding the
        /// reference: (computed − high) − low.</summary>
        internal (double Position, double Velocity) RelativeError(StateVector state)
        {
            return (Error(state.Position, PositionHigh, PositionLow), Error(state.Velocity, VelocityHigh, VelocityLow));
        }

        private static double Error(in Vector3 computed, double[] high, double[] low)
        {
            double dx = computed.X - high[0] - low[0];
            double dy = computed.Y - high[1] - low[1];
            double dz = computed.Z - high[2] - low[2];
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz) / new Vector3(high[0], high[1], high[2]).Magnitude();
        }
    }

    /// <summary>The Keplerian angles under the conventions of #452, modulo 2π.</summary>
    internal sealed record KeplerianAngles((double High, double Low) Node, (double High, double Low) Periapsis,
        (double High, double Low) TrueAnomaly);

    /// <summary>Equinoctial elements (P, F, G, H, K, L0), their reference states, their Keplerian angles and mean
    /// longitude modulo 2π, and their state at the true anomaly 0.5 rad with the time to reach it.</summary>
    internal sealed record EquinoctialCase(string Name, string Group, double P, double F, double G, double H, double K,
        double L, double? PerigeeRadius, double? PerigeeRadiusReference, string Gm, IReadOnlyList<ReferenceState> States,
        KeplerianAngles Angles, (double High, double Low)? MeanLongitude, double? TrueAnomaly,
        ReferenceState AtTrueAnomaly, (double High, double Low)? TimeToTrueAnomaly)
    {
        internal static EquinoctialCase Read(JsonElement c)
        {
            double Get(string name) => Number(c.GetProperty(name));
            double? Optional(string name) => c.TryGetProperty(name, out var value) ? Number(value) : null;
            double? reference = c.TryGetProperty("perigee_radius_reference", out var pair)
                ? Number(pair[0]) + Number(pair[1])
                : null;
            var states = c.TryGetProperty("states", out var s)
                ? s.EnumerateArray().Select(ReferenceState.Read).ToList()
                : new List<ReferenceState>();
            var angles = c.TryGetProperty("angles", out var a)
                ? new KeplerianAngles(Pair(a.GetProperty("node")), Pair(a.GetProperty("periapsis")),
                    Pair(a.GetProperty("nu")))
                : null;
            bool hasNu = c.TryGetProperty("at_true_anomaly", out var at);
            var dt = hasNu ? Pair(at.GetProperty("dt")) : ((double, double)?)null;
            return new EquinoctialCase(c.GetProperty("name").GetString(),
                c.TryGetProperty("group", out var group) ? group.GetString() : null, Get("p"), Get("f"), Get("g"),
                Get("h"), Get("k"), Get("l"), Optional("perigee_radius"), reference,
                c.TryGetProperty("gm", out var gm) ? gm.GetString() : null, states, angles,
                c.TryGetProperty("mean_longitude", out var lambda) ? Pair(lambda) : null,
                hasNu ? Number(at.GetProperty("nu")) : null, hasNu ? ReferenceState.ReadAt(at, dt!.Value.Item1) : null,
                dt);
        }

        internal EquinoctialElements Elements(ILocalizable observer, TimeSystem.Time epoch, Frames.Frame frame,
            OrbitalElementsType elementsType = OrbitalElementsType.Osculating)
        {
            return new EquinoctialElements(P, F, G, H, K, L, observer, epoch, frame, PerigeeRadius, elementsType);
        }

        internal ReferenceState At(double dt) => States.Single(s => s.Dt == dt);

        /// <summary>The values of the CLI option, in the order of the constructor.</summary>
        internal string CliValues => string.Join(" ",
            new[] { P, F, G, H, K, L }.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
    }

    /// <summary>Keplerian elements (a, e, i, Ω, ω, M) and their reference states.</summary>
    internal sealed record KeplerianCase(string Name, string Group, double A, double E, double I, double Node,
        double Periapsis, double M, double? PerigeeRadius, IReadOnlyList<ReferenceState> States)
    {
        internal static KeplerianCase Read(JsonElement c)
        {
            double Get(string name) => Number(c.GetProperty(name));
            return new KeplerianCase(c.GetProperty("name").GetString(), c.GetProperty("group").GetString(), Get("a"),
                Get("e"), Get("i"), Get("o"), Get("w"), Get("m"),
                c.TryGetProperty("perigee_radius", out var rp) ? Number(rp) : null,
                c.GetProperty("states").EnumerateArray().Select(ReferenceState.Read).ToList());
        }

        internal KeplerianElements Elements(ILocalizable observer, TimeSystem.Time epoch)
        {
            return new KeplerianElements(A, E, I, Node, Periapsis, M, observer, epoch, Frames.Frame.ICRF,
                perigeeRadius: PerigeeRadius);
        }

        internal ReferenceState At(double dt) => States.Single(s => s.Dt == dt);
    }

    /// <summary>Mean elements for <see cref="IO.Astrodynamics.OrbitalParameters.TLE.TLE.Create"/>, and the fields of
    /// line 2 under the conventions of #452, in degrees with 4 decimals.</summary>
    internal sealed record TleCreateCase(string Name, double P, double F, double G, double H, double K, double L,
        string Inclination, string Raan, string ArgumentOfPerigee, string MeanAnomaly)
    {
        internal static TleCreateCase Read(JsonElement c)
        {
            double Get(string name) => Number(c.GetProperty(name));
            var line2 = c.GetProperty("line2");
            return new TleCreateCase(c.GetProperty("name").GetString(), Get("p"), Get("f"), Get("g"), Get("h"),
                Get("k"), Get("l"), line2.GetProperty("inclination").GetString(), line2.GetProperty("raan").GetString(),
                line2.GetProperty("argument_of_perigee").GetString(), line2.GetProperty("mean_anomaly").GetString());
        }
    }
}
