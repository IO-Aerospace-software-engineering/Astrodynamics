// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// The implicit usings of the package (N7 of D2, #668, lot L9): build/IO.Astrodynamics.props, which NuGet imports into
/// every project that references the package, adds the eleven namespaces of the decision when the project turns
/// ImplicitUsings on, unless it sets IOAstrodynamicsImplicitUsings to false. The docs tests give a reader's application
/// the namespaces this file lists (DocumentationPagesExamplesTests), and the SmokeTests compile against the packed file.
/// </summary>
public class PackageImplicitUsingsTests
{
    private static readonly string Props = Path.Combine(AppContext.BaseDirectory, "Docs", "IO.Astrodynamics.props");

    // The namespaces N7 names, in its order
    private static readonly string[] Decided =
    {
        "IO.Astrodynamics", "IO.Astrodynamics.Body", "IO.Astrodynamics.Body.Spacecraft", "IO.Astrodynamics.TimeSystem",
        "IO.Astrodynamics.Frames", "IO.Astrodynamics.OrbitalParameters", "IO.Astrodynamics.SolarSystemObjects",
        "IO.Astrodynamics.Propagator", "IO.Astrodynamics.Propagator.Integrators", "IO.Astrodynamics.Maneuver",
        "IO.Astrodynamics.Math"
    };

    [Fact]
    public void Props_AddsTheElevenNamespacesOfN7()
    {
        Assert.Equal(Decided, DocumentationPagesExamplesTests.PackageImplicitUsings());
    }

    [Fact]
    public void Props_IsConditionedOnImplicitUsingsAndTheOptOut()
    {
        var group = XDocument.Load(Props).Root!.Elements("ItemGroup").Single();
        var condition = (string)group.Attribute("Condition");
        Assert.Contains("'$(ImplicitUsings)' == 'enable'", condition);
        Assert.Contains("'$(IOAstrodynamicsImplicitUsings)' != 'false'", condition);
    }

    [Fact]
    public void EveryNamespace_HoldsAPublicTypeOfTheLibrary()
    {
        var namespaces = typeof(IO.Astrodynamics.Constants).Assembly.GetExportedTypes().Select(t => t.Namespace).ToHashSet();
        Assert.All(Decided, n => Assert.Contains(n, namespaces));
    }

    // The implicit usings of the SDK's Microsoft.NET.Sdk, which a project with ImplicitUsings imports beside the package's
    private static readonly string[] SdkImplicitUsings =
    {
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading",
        "System.Threading.Tasks"
    };

    /// <summary>
    /// N8 of D2: no two public types of the eleven namespaces share a simple name (and generic arity), and none shares one
    /// with a public type of the SDK's implicit namespaces, so that a program with ImplicitUsings names each without a
    /// CS0104 ambiguity. <c>TLE.Configuration</c> against <c>IO.Astrodynamics.Configuration</c> was the one such pair
    /// before lot L9 (decision 113 of #668).
    /// </summary>
    [Fact]
    public void TheImplicitNamespaces_HoldNoDuplicateSimpleName()
    {
        var library = typeof(IO.Astrodynamics.Constants).Assembly.GetExportedTypes()
            .Where(t => !t.IsNested && Decided.Contains(t.Namespace)).ToList();
        var duplicates = library.GroupBy(t => t.Name).Where(g => g.Count() > 1)
            .Select(g => string.Join(" and ", g.Select(t => t.FullName))).ToList();
        Assert.True(duplicates.Count == 0, "duplicate simple names: " + string.Join("; ", duplicates));

        var sdk = SdkTypeNames();
        Assert.Contains("String", sdk.Keys);
        Assert.Contains("HttpClient", sdk.Keys);
        var clashes = library.Where(t => sdk.ContainsKey(t.Name)).Select(t => $"{t.FullName} and {sdk[t.Name]}").ToList();
        Assert.True(clashes.Count == 0, "simple names of the SDK's implicit namespaces: " + string.Join("; ", clashes));
    }

    // The public top-level types of the SDK's implicit namespaces, by CLR name (with the generic arity), from the assemblies
    // of the runtime
    private static Dictionary<string, string> SdkTypeNames()
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                     .Where(p => string.Equals(Path.GetDirectoryName(p), runtime, StringComparison.Ordinal)))
        {
            Type[] types;
            try
            {
                types = Assembly.Load(AssemblyName.GetAssemblyName(path)).GetExportedTypes();
            }
            catch (Exception e) when (e is BadImageFormatException or FileLoadException or FileNotFoundException or
                                          NotSupportedException or ReflectionTypeLoadException)
            {
                continue; // a native or unloadable file of the runtime directory
            }

            foreach (var type in types.Where(t => !t.IsNested && SdkImplicitUsings.Contains(t.Namespace)))
            {
                names.TryAdd(type.Name, type.FullName!);
            }
        }

        return names;
    }

    [Fact]
    public void TheLibrary_PacksTheProps()
    {
        var project = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Docs", "IO.Astrodynamics.csproj"));
        var item = project.Descendants("None").Single(e => (string)e.Attribute("Update") == @"build\IO.Astrodynamics.props");
        Assert.Equal("true", (string)item.Attribute("Pack"));
        Assert.Equal(@"build\IO.Astrodynamics.props", (string)item.Attribute("PackagePath"));
    }
}
