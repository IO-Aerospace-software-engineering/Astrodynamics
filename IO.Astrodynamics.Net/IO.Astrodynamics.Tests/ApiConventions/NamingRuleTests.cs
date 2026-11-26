// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The casing and word rules N1 to N3 of D2 (#668, design PR #671) over the public surface of the library, read by
/// reflection (<see cref="PublicSurface"/>): no CA rule checks acronym casing (CA1709 is not in the SDK, IDE1006 accepts
/// <c>ToUTC</c>), so this test is the rule. Today's violations are the checked-in baseline NamingRuleBaseline.txt, which
/// may only shrink: a new violation fails, and so does an entry no longer violated. The renaming lot (L9) empties it.
/// <list type="bullet">
/// <item>N1.case: a type, member or enum member starts with a capital letter, a parameter with a small one.</item>
/// <item>N1.underscore: no underscore in a name.</item>
/// <item>N1.acronym: no acronym of three capitals or more (<c>ToUTC</c>, <c>SpiceAPI</c>, <c>TLEPropagator</c>): a run of
/// capitals followed by a small letter gives its last capital to the next word, so <c>IOStream</c> and <c>VVIntegrator</c>
/// pass, and digits end a run (<c>RK78Integrator</c> passes, <c>EGM2008</c> does not).</item>
/// <item>N2: an enum member or a static instance declared on its own type whose whole name is a SPICE, IAU/IERS or CCSDS
/// symbol is written as the standard writes it: the closed list <see cref="StandardSymbols"/> is exempt from N1.</item>
/// <item>N3.deny: no word of a name is one of the misspellings and second terms of N3 (<see cref="DenyList"/>), the
/// words split at the casing and the digits (<see cref="Words"/>).</item>
/// </list>
/// Each segment of a namespace that holds an exported type (the namespaces of the generated PDS types included: N4 renames
/// PDS to Pds) is a name of N1 and N3, reported as "namespace Namespace" by the namespace it ends. A method is keyed by its
/// signature and a parameter by its method's signature and its name (<see cref="PublicSurface.Signature"/>), so that a new
/// overload of a name in the baseline is a new violation.
/// A parameter of a record's primary constructor or of its <c>Deconstruct</c> that bears the name of a property of the
/// record of the same type is the property's name, PascalCase (D1 sub-choice 1, <c>PropagationTask(..., Options)</c>).
/// </summary>
public class NamingRuleTests
{
    internal const string BaselineFile = "NamingRuleBaseline.txt";

    /// <summary>
    /// The closed list of N2: each entry is "Type.Member" (the type by its simple name), an enum member or a static
    /// instance of its own type whose whole name is a symbol of SPICE (frame names, aberration codes), IAU/IERS (frames,
    /// time scales) or CCSDS (keyword values: REF_FRAME, TIME_SYSTEM, MEAN_ELEMENT_THEORY). An entry whose spelling the
    /// standard does not write (Aberration.LTS for "LT+S") is not a symbol: it stays in the baseline until L9 renames it.
    /// The entries named by the design (D2 N2, resolution C1) are there whether the member exists yet or not.
    /// </summary>
    internal static readonly IReadOnlySet<string> StandardSymbols = new HashSet<string>(StringComparer.Ordinal)
    {
        // Frame: SPICE frame names (Frames Required Reading) and the IERS/IAU frames of the library
        "Frame.ICRF", "Frame.TEME", "Frame.GCRF", "Frame.CIRS", "Frame.TIRS", "Frame.ECLIPJ2000", "Frame.ECLIPB1950",
        "Frame.GALACTIC", "Frame.B1950", "Frame.FK4", "Frame.ITRF93",
        // Aberration: SPICE's codes (spkezr_c ABCORR) that a C# name can spell
        "Aberration.LT", "Aberration.CN", "Aberration.XLT", "Aberration.XCN",
        // Time scales (D3, resolution C1: TimeScale { TDB, TT, TAI, UTC, GPS })
        "TimeScale.TDB", "TimeScale.TT", "TimeScale.TAI", "TimeScale.UTC", "TimeScale.GPS",
        // CCSDS keyword values (CCSDS 502.0-B-3, 508.0-B-1)
        "CcsdsTimeSystem.UTC", "CcsdsTimeSystem.TAI", "CcsdsTimeSystem.TDB", "CcsdsTimeSystem.TT", "CcsdsTimeSystem.GPS",
        "CcsdsReferenceFrame.ICRF", "CcsdsReferenceFrame.EME2000", "CcsdsReferenceFrame.GCRF", "CcsdsReferenceFrame.TEME",
        "CcsdsReferenceFrame.ITRF93", "CcsdsReferenceFrame.ECLIPJ2000", "CcsdsReferenceFrame.ECLIPB1950",
        "CcsdsReferenceFrame.B1950", "CcsdsReferenceFrame.FK4",
        "CdmReferenceFrame.EME2000", "CdmReferenceFrame.GCRF", "CdmReferenceFrame.ITRF",
        "MeanElementTheory.SGP4"
    };

    /// <summary>N3's deny-list (D2, NamingRuleTests): misspellings and the second terms of a concept, in any case.</summary>
    internal static readonly IReadOnlyList<string> DenyList =
        ["Relationnal", "Sideral", "Derivation", "Rigth", "Oject", "Dictionnary", "Tsiolkovski", "Bissection"];

    [Fact]
    public void PublicSurface_FollowsN1ToN3_OrIsInTheBaseline()
    {
        var violations = Violations(PublicSurface.Types, NamespacesOf(PublicSurface.Library.GetExportedTypes())).ToList();
        Assert.NotEmpty(PublicSurface.Types);
        PublicSurface.AssertMatchesBaseline(BaselineFile, violations);
    }

    [Fact]
    public void Baseline_IsSortedAndHasNoDuplicate()
    {
        // The file is kept in ordinal order, so that a lot's removals read as one block in the diff
        var lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(AppContext.BaseDirectory, "ApiConventions", BaselineFile))
            .Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.Equal(lines.OrderBy(l => l, StringComparer.Ordinal).Distinct(), lines);
    }

    [Theory]
    [InlineData("targetOjectOrbitalParameters", true)]
    [InlineData("ProjectedCovariance", false)]
    [InlineData("SideralRotationPeriod", true)]
    [InlineData("rigthAscendingNode", true)]
    [InlineData("FirstDerivationMeanMotion", true)]
    [InlineData("SecondDerivativeMeanMotion", false)]
    public void DenyList_MatchesWholeWords(string name, bool denied)
    {
        Assert.Equal(denied, Words(name).Any(word => DenyList.Contains(word, StringComparer.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("SpiceApi", true)]
    [InlineData("SpiceAPI", false)]
    [InlineData("ToUTC", false)]
    [InlineData("ToUtc", true)]
    [InlineData("TLEPropagator", false)]
    [InlineData("IOStream", true)]
    [InlineData("VVIntegrator", true)]
    [InlineData("RK78Integrator", true)]
    [InlineData("EGM2008", false)]
    [InlineData("J2000Tdb", true)]
    [InlineData("TT", true)]
    [InlineData("JD", true)]
    [InlineData("GetStateOrientationToICRF", false)]
    public void AcronymRule_CountsTheCapitalsOfOneWord(string name, bool passes)
    {
        Assert.Equal(passes, LongestAcronym(name) < 3);
    }

    // The length of the longest acronym of a name: a run of capitals, less its last one when a small letter follows it
    internal static int LongestAcronym(string name)
    {
        int longest = 0;
        for (int i = 0; i < name.Length;)
        {
            if (!char.IsUpper(name[i]))
            {
                i++;
                continue;
            }

            int start = i;
            while (i < name.Length && char.IsUpper(name[i]))
            {
                i++;
            }

            int run = i - start;
            if (i < name.Length && char.IsLower(name[i]))
            {
                run--;
            }

            longest = System.Math.Max(longest, run);
        }

        return longest;
    }

    // The words of a name, split at its casing and its digits: targetOjectOrbitalParameters is target, Oject, Orbital,
    // Parameters, so that the deny-list matches whole words (Oject, not the "oject" of Projected)
    internal static IEnumerable<string> Words(string name) =>
        System.Text.RegularExpressions.Regex.Matches(name, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+").Select(m => m.Value);

    // The violations of one name: N1 and N3, or none for a symbol of N2
    private static IEnumerable<string> Check(string name, string symbol, bool parameter, bool standardSymbol)
    {
        if (!standardSymbol)
        {
            if (name.Length > 0 && (parameter ? !char.IsLower(name[0]) : !char.IsUpper(name[0])))
            {
                yield return $"N1.case\t{symbol}";
            }

            if (name.Contains('_'))
            {
                yield return $"N1.underscore\t{symbol}";
            }

            if (LongestAcronym(name) >= 3)
            {
                yield return $"N1.acronym\t{symbol}";
            }
        }

        if (Words(name).Any(word => DenyList.Contains(word, StringComparer.OrdinalIgnoreCase)))
        {
            yield return $"N3.deny\t{symbol}";
        }
    }

    // A static instance of its own type or an enum member, whose whole name the closed list of N2 holds
    private static bool IsStandardSymbol(MemberInfo member)
    {
        var type = member.DeclaringType!;
        bool value = member switch
        {
            FieldInfo f => f.IsStatic && (type.IsEnum || f.FieldType == type),
            PropertyInfo p => p.GetMethod is { IsStatic: true } && p.PropertyType == type,
            _ => false
        };
        return value && StandardSymbols.Contains($"{PublicSurface.SimpleName(type)}.{member.Name}");
    }

    // A record's primary-constructor or Deconstruct parameter named after its property
    private static bool IsRecordPropertyParameter(MethodBase method, ParameterInfo parameter)
    {
        var type = method.DeclaringType!;
        bool record = type.GetMethod("<Clone>$", PublicSurface.Declared) != null ||
                      type.IsValueType && type.GetProperty("EqualityContract", PublicSurface.Declared) == null &&
                      type.GetMethod("PrintMembers", PublicSurface.Declared) != null;
        if (!record || !(method is ConstructorInfo || method.Name == "Deconstruct"))
        {
            return false;
        }

        var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
        var property = type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance);
        return property != null && property.PropertyType == parameterType;
    }

    /// <summary>The namespaces of the given types and their parents, IO.Astrodynamics and below.</summary>
    internal static IReadOnlyList<string> NamespacesOf(IEnumerable<Type> types) =>
        types.Select(t => t.Namespace).Where(n => n != null)
            .SelectMany(n => Enumerable.Range(1, n!.Split('.').Length).Select(k => string.Join(".", n.Split('.').Take(k))))
            .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Every violation of N1 to N3 by the given types and namespaces, as "rule&lt;TAB&gt;symbol".
    /// </summary>
    internal static IEnumerable<string> Violations(IEnumerable<Type> types, IEnumerable<string> namespaces)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var ns in namespaces)
        {
            found.UnionWith(Check(ns[(ns.LastIndexOf('.') + 1)..], $"namespace {ns}", false, false));
        }

        foreach (var type in types)
        {
            var typeName = PublicSurface.Name(type);
            found.UnionWith(Check(PublicSurface.SimpleName(type), typeName, false, false));

            foreach (var member in PublicSurface.Members(type))
            {
                if (member is ConstructorInfo || member is MethodInfo { IsSpecialName: true } || member.Name == "value__")
                {
                    continue;
                }

                var symbol = member is MethodBase method ? PublicSurface.Signature(method) : PublicSurface.Key(member);
                found.UnionWith(Check(member.Name, symbol, false, IsStandardSymbol(member)));
            }

            var methods = PublicSurface.MethodsAndConstructors(type).ToList();
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                methods.AddRange(type.GetMethods(PublicSurface.Declared).Where(m => m.Name == "Invoke"));
            }

            foreach (var method in methods)
            {
                foreach (var parameter in method.GetParameters())
                {
                    if (string.IsNullOrEmpty(parameter.Name) || IsRecordPropertyParameter(method, parameter))
                    {
                        continue;
                    }

                    found.UnionWith(Check(parameter.Name, PublicSurface.ParameterKey(method, parameter), true, false));
                }
            }
        }

        return found;
    }

    // The review's probes of lot L0: a namespace with an underscore and an acronym, and a new overload of a name the
    // baseline already holds
    [Fact]
    public void Violations_ReportANamespaceAndANewOverload()
    {
        var violations = Violations([typeof(Probes.TDM_Probe.TdmReader)],
            NamespacesOf([typeof(Probes.TDM_Probe.TdmReader)])).ToHashSet();
        Assert.Contains("N1.underscore\tnamespace IO.Astrodynamics.Tests.ApiConventions.Probes.TDM_Probe", violations);
        Assert.Contains("N1.acronym\tnamespace IO.Astrodynamics.Tests.ApiConventions.Probes.TDM_Probe", violations);
        Assert.DoesNotContain(violations, v => v.EndsWith("namespace IO.Astrodynamics.Tests.ApiConventions.Probes", StringComparison.Ordinal));
        const string reader = "IO.Astrodynamics.Tests.ApiConventions.Probes.TDM_Probe.TdmReader";
        Assert.Contains($"N1.acronym\t{reader}.GetTotalISP()", violations);
        Assert.Contains($"N1.acronym\t{reader}.GetTotalISP(int)", violations);
        Assert.Contains($"N1.acronym\t{reader}.GetTotalISP(int):probeUTCValue", violations);
    }

    // R21 and R22 of #668 (C14 of the design): the text formats share one verb set, Load, LoadAll, Parse, ParseAll, TryLoad
    // and TryParse, public and static, and no other public static read verb (Load…, Read…, Parse…, TryLoad…, TryParse…)
    private static readonly string[] TextFormatVerbs = ["Load", "LoadAll", "Parse", "ParseAll", "TryLoad", "TryParse"];

    [Theory]
    [InlineData(typeof(IO.Astrodynamics.OrbitalParameters.TLE.Tle))]
    [InlineData(typeof(IO.Astrodynamics.CCSDS.OMM.Omm))]
    [InlineData(typeof(IO.Astrodynamics.CCSDS.OPM.Opm))]
    [InlineData(typeof(IO.Astrodynamics.CCSDS.CDM.Cdm))]
    public void TextFormats_ShareOneVerbSet(Type type)
    {
        var statics = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).ToHashSet();
        Assert.All(TextFormatVerbs, verb => Assert.Contains(verb, statics));
        string[] readPrefixes = ["Load", "Read", "Parse", "TryLoad", "TryParse"];
        Assert.DoesNotContain(statics, name => readPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) && !TextFormatVerbs.Contains(name));
    }
}
