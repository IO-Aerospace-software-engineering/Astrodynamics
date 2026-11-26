// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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

    [Fact]
    public void TheLibrary_PacksTheProps()
    {
        var project = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Docs", "IO.Astrodynamics.csproj"));
        var item = project.Descendants("None").Single(e => (string)e.Attribute("Update") == @"build\IO.Astrodynamics.props");
        Assert.Equal("true", (string)item.Attribute("Pack"));
        Assert.Equal(@"build\IO.Astrodynamics.props", (string)item.Attribute("PackagePath"));
    }
}
