// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using IDataProvider = IO.Astrodynamics.DataProvider.IDataProvider;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;
using LaunchWindow = IO.Astrodynamics.Maneuver.LaunchWindow;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The scope of the public surface (R04 of #668, lot L3, with the provider part of R20): the interop structs of the native
/// layer (<c>IO.Astrodynamics.DTO</c>), the library's implementation helpers, the JSON model of the Cosmographia exporter
/// and the ten raw conversions of <see cref="SpiceAPI"/> are not public; no visible member exposes a type a caller cannot
/// name; every public value type is read only; the data provider seam speaks domain types. Read by reflection over the
/// public surface (<see cref="PublicSurface"/>), by name, so that each rule reads the library as it is.
/// </summary>
public class PublicSurfaceScopeTests
{
    /// <summary>
    /// The 14 interop structs of the P/Invoke layer, 11 of them named as domain types (<c>DTO.CelestialBody</c>,
    /// <c>DTO.StateVector</c>, <c>DTO.Quaternion</c>...), which a <c>using IO.Astrodynamics.DTO</c> made ambiguous (CS0104):
    /// before 11.0.0 they were public, and <see cref="IDataProvider.GetCelestialBodyInfo"/> returned one.
    /// </summary>
    [Fact]
    public void InteropStructs_AreNotExported()
    {
        var exported = PublicSurface.Library.GetExportedTypes().Where(t => t.Namespace == "IO.Astrodynamics.DTO")
            .Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(exported.Count == 0, "exported interop structs: " + string.Join(", ", exported));
    }

    /// <summary>The helpers of the implementation (the first annex of #671, R04, and the Cosmographia model of R27).</summary>
    [Theory]
    [InlineData("IO.Astrodynamics.DictionnaryExtension")]
    [InlineData("IO.Astrodynamics.Enumeration")]
    [InlineData("IO.Astrodynamics.ArrayBuilder")]
    [InlineData("IO.Astrodynamics.Converters.ProfilesConfiguration")]
    [InlineData("IO.Astrodynamics.Helpers.Utf8StringWriter")]
    [InlineData("IO.Astrodynamics.Propagator.PropagationEphemerisCache")]
    [InlineData("IO.Astrodynamics.Propagator.PropagationFrameOrientationCache")]
    public void ImplementationHelper_IsNotExported(string fullName)
    {
        var type = PublicSurface.Library.GetType(fullName);
        Assert.NotNull(type);
        Assert.False(type.IsVisible, $"{fullName} is public");
    }

    /// <summary>The JSON model of <see cref="Cosmographia.CosmographiaExporter"/>, whose files are its only output.</summary>
    [Fact]
    public void CosmographiaModel_IsNotExported()
    {
        var exported = PublicSurface.Library.GetExportedTypes().Where(t => t.Namespace == "IO.Astrodynamics.Cosmographia.Model")
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(exported.Count == 0, "exported Cosmographia model types: " + string.Join(", ", exported));
    }

    /// <summary>
    /// The ten raw conversions of <see cref="SpiceAPI"/> that twin the object API (<c>TLE.ToStateVector</c>,
    /// <c>ToKeplerianElements</c>, <c>ToStateVector</c>, <c>AtEpoch</c>): removed from the public API in 11.0.0, outright
    /// (section 7 of the re-baseline of #671, decided by S. Guillet, 2026-11-19), kept internal as the SPICE references of
    /// the tests.
    /// </summary>
    [Theory]
    [InlineData("ConvertTleToStateVector")]
    [InlineData("ConvertStateVectorToConicOrbitalElement")]
    [InlineData("ConvertEquinoctialElementsToStateVector")]
    [InlineData("ConvertConicElementsToStateVector")]
    [InlineData("Propagate2Bodies")]
    public void SpiceApiConversion_IsNotPublic(string name)
    {
        Assert.NotEmpty(typeof(SpiceAPI).GetMethods(PublicSurface.Declared).Where(m => m.Name == name));
        var visible = typeof(SpiceAPI).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == name).Select(PublicSurface.Signature).ToList();
        Assert.True(visible.Count == 0, "public: " + string.Join(", ", visible));
    }

    /// <summary>
    /// No visible member (public, or protected on a type a caller can derive from) takes, returns or holds a type that is
    /// not public, nor a public type derives from one: the compiler rejects these (CS0050 to CS0061), and this rule keeps
    /// the public surface free of them whatever the compiler. An internal interface a public type implements (the
    /// library's own seams, such as <c>IGatedEventDetector</c>) is not exposed: a caller cannot name it.
    /// </summary>
    [Fact]
    public void NoVisibleMember_ExposesATypeACallerCannotName()
    {
        var violations = new List<string>();
        foreach (var type in PublicSurface.Types)
        {
            if (type.BaseType is { } baseType && !IsNameable(baseType))
            {
                violations.Add($"{PublicSurface.Name(type)} : {baseType}");
            }

            foreach (var member in PublicSurface.Members(type))
            {
                foreach (var exposed in ExposedTypes(member).Where(t => !IsNameable(t)))
                {
                    violations.Add($"{PublicSurface.Key(member)} exposes {exposed}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations.Distinct()));
    }

    private static IEnumerable<Type> ExposedTypes(MemberInfo member) => member switch
    {
        FieldInfo f => [f.FieldType],
        PropertyInfo p => [p.PropertyType],
        EventInfo e => [e.EventHandlerType],
        MethodInfo m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType),
        ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType),
        _ => []
    };

    // A type a caller can name: public through its declaring types, and so its element and generic argument types
    private static bool IsNameable(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            return IsNameable(type.GetElementType()!);
        }

        if (type.IsGenericParameter)
        {
            return true;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition && !type.GetGenericArguments().All(IsNameable))
        {
            return false;
        }

        return type.IsVisible || type.Assembly != PublicSurface.Library && type.IsPublic;
    }

    /// <summary>
    /// Every public value type of the library is a <c>readonly</c> struct: a mutable struct copied by a property or a
    /// collection changes the copy, silently. Before 11.0.0 the positional record structs <c>IntegrationResult</c>,
    /// <c>EventInfo</c> and <c>EventResult</c> had public setters, <c>Plane</c> was not read only, and so were the interop
    /// structs.
    /// </summary>
    [Fact]
    public void PublicValueTypes_AreReadOnly()
    {
        var mutable = PublicSurface.Types.Where(t => t.IsValueType && !t.IsEnum && !t.IsDefined(typeof(IsReadOnlyAttribute), false))
            .Select(PublicSurface.Name).ToList();
        Assert.True(mutable.Count == 0, "mutable public structs: " + string.Join(", ", mutable));
    }

    /// <summary>
    /// The seam of the data providers in domain types (R04 with R20, so that a custom provider changes once): the state of
    /// <see cref="IDataProvider.GetEphemeris"/> is a <see cref="StateVector"/>, the body
    /// information a public record of the library, not an interop struct.
    /// </summary>
    [Fact]
    public void DataProviderSeam_SpeaksDomainTypes()
    {
        var seam = typeof(IDataProvider);
        Assert.Equal(typeof(StateVector), seam.GetMethod(nameof(IDataProvider.GetEphemeris))!.ReturnType);
        var info = seam.GetMethod(nameof(IDataProvider.GetCelestialBodyInfo))!.ReturnType;
        Assert.Equal("IO.Astrodynamics.DataProvider.CelestialBodyInfo", info.FullName);
        Assert.True(info.IsVisible && info.IsClass && info.IsSealed, $"{info} is not a public sealed record");
        foreach (var method in seam.GetMethods())
        {
            Assert.All(method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
                t => Assert.NotEqual("IO.Astrodynamics.DTO", (t.IsByRef ? t.GetElementType()! : t).Namespace));
        }
    }

    /// <summary>
    /// The window searches return an <see cref="IReadOnlyList{T}"/> of windows, their count and indexing visible, never an
    /// <see cref="IEnumerable{T}"/> of windows (R16 of #668, choice 2, decided by S. Guillet, 2026-11-19): the 21 public
    /// searches of <c>CelestialItem</c>, <c>ILocalizable</c>, <c>Site</c>, <c>Instrument</c>, <c>GeometryFinder</c> and
    /// <c>SpiceAPI</c>, which returned the arrays and lists they build as an <c>IEnumerable&lt;Window&gt;</c> before 11.0.0,
    /// and the 8 searches on the observer of R15 (<c>WindowSearches</c>, <c>Site</c>, <c>Instrument</c>, the static
    /// <c>GeometryFinder.FindWindows</c>).
    /// </summary>
    [Fact]
    public void WindowSearches_ReturnAReadOnlyList()
    {
        var searches = PublicSurface.Types
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.StartsWith("Find") && m.Name.Contains("Windows") && m.ReturnType != typeof(IEnumerable<LaunchWindow>))
            .ToList();
        Assert.Equal(29, searches.Count);
        Assert.All(searches, m => Assert.True(m.ReturnType == typeof(IReadOnlyList<Window>), $"{m.DeclaringType}.{m}"));
        Assert.DoesNotContain(PublicSurface.Types.SelectMany(t => t.GetMethods(PublicSurface.Declared)).Where(m => m.IsPublic || m.IsFamily),
            m => m.ReturnType == typeof(IEnumerable<Window>));
    }
}
