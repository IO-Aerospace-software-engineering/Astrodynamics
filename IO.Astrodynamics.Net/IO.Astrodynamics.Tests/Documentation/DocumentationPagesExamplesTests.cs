// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// The C# examples of the pages of docs/ and README.md (#705): each csharp block equals its compiled region of the page's
/// file under Documentation/Pages (one method per block, in a namespace outside IO.Astrodynamics, as for
/// DEVELOPER_GUIDE.md), so a page cannot drift from code the compiler checks, and the blocks that need only
/// Data/SolarSystem run. Before #705 the blocks were read for two text rules only (<see cref="DocumentationPagesTests"/>):
/// 34 lines in 12 pages did not compile against the library (members that do not exist, as <c>tle.NoradId</c> and
/// <c>LambertSolution.DepartureVelocity</c>, parameters of other names, as <c>shortWay:</c> and <c>occultingBody:</c>, a
/// lambda for an <c>IProgress</c>, required members elided), and 12 blocks were signature listings or fragments, now
/// fenced <c>csharp-signature</c> or written out in full.
/// </summary>
public class DocumentationPagesExamplesTests
{
    private static readonly string Docs = Path.Combine(AppContext.BaseDirectory, "Docs");
    private static readonly string Examples = Path.Combine(Docs, "PageExamples");

    private readonly ITestOutputHelper _output;

    public DocumentationPagesExamplesTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    #region The pages against the compiled regions

    // The blocks a page opens with that are checked otherwise: the excerpt of Constants of standards-and-units.md, checked
    // by value (StandardsConstants_EqualTheLibrary)
    private static readonly Dictionary<string, int> FirstCompiledBlock = new() { ["docs/standards-and-units.md"] = 1 };

    // The changelog, whose blocks keep the code of former versions
    private const string Changelog = "docs/contributing/versioning.md";

    // A page by its path in the repository ("docs/tutorials/maneuvers.md", "README.md"), as its regions name it
    private static string MarkdownPath(string page) =>
        page == "README.md" ? Path.Combine(Docs, "README.md") : Path.Combine(Docs, "pages", page["docs/".Length..]);

    // The pages of docs/ and README.md, the changelog aside
    private static IEnumerable<string> AllPages() =>
        Directory.GetFiles(Path.Combine(Docs, "pages"), "*.md", SearchOption.AllDirectories)
            .Select(p => "docs/" + Path.GetRelativePath(Path.Combine(Docs, "pages"), p).Replace('\\', '/'))
            .Where(p => p != Changelog)
            .Append("README.md")
            .OrderBy(p => p, StringComparer.Ordinal);

    // The pages that hold csharp blocks
    public static IEnumerable<object[]> Pages() =>
        AllPages().Where(p => DocumentationCodeBlocks.Blocks(MarkdownPath(p)).Count > 0).Select(p => new object[] { p });

    private static string Pascal(string name) =>
        string.Concat(name.Split('-', '_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

    // The compiled file and the class of a page: docs/tutorials/tle-and-omm.md is Tutorials/TleAndOmmPage.cs, class
    // DocumentationPageSamples.Tutorials.TleAndOmmPage
    private static (string File, string Type) Compiled(string page)
    {
        if (page == "README.md")
        {
            return (Path.Combine(Examples, "ReadmePage.cs"), "DocumentationPageSamples.ReadmePage");
        }

        var parts = page["docs/".Length..^".md".Length].Split('/');
        var directories = parts[..^1].Select(Pascal).ToArray();
        var name = Pascal(parts[^1]) + "Page";
        var file = Path.Combine(new[] { Examples }.Concat(directories).Append(name + ".cs").ToArray());
        var type = string.Join(".", new[] { "DocumentationPageSamples" }.Concat(directories).Append(name));
        return (file, type);
    }

    /// <summary>
    /// Every csharp block of a page equals its region of the page's compiled file, its using directives aside, and there
    /// are as many regions as blocks; a page with csharp blocks and no compiled file fails, so a new page is compiled too.
    /// Red on 46e6975: no page had a compiled file, and 34 lines of 12 pages did not compile.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void PageBlocks_EqualTheirCompiledRegions(string page)
    {
        var (file, _) = Compiled(page);
        Assert.True(File.Exists(file), $"{page}: no compiled file {Path.GetRelativePath(Examples, file)}");
        int first = FirstCompiledBlock.GetValueOrDefault(page);
        DocumentationCodeBlocks.AssertBlocksEqualRegions(page, DocumentationCodeBlocks.Blocks(MarkdownPath(page)), first,
            DocumentationCodeBlocks.Regions(file, page));
    }

    /// <summary>Every compiled file is the file of a page with csharp blocks: none is left when its page goes.</summary>
    [Fact]
    public void CompiledFiles_BelongToPages()
    {
        var expected = Pages().Select(p => Path.GetFullPath(Compiled((string)p[0]).File)).ToHashSet();
        var files = Directory.GetFiles(Examples, "*.cs", SearchOption.AllDirectories).Select(Path.GetFullPath).ToList();
        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.Contains(f, expected));
    }

    /// <summary>The excerpt of <c>Constants</c> in standards-and-units.md (its first csharp block) gives the library's values.</summary>
    [Fact]
    public void StandardsConstants_EqualTheLibrary()
    {
        DocumentationCodeBlocks.AssertConstantsEqualTheLibrary(DocumentationCodeBlocks.Blocks(MarkdownPath("docs/standards-and-units.md"))[0]);
    }

    // The pages with csharp-signature blocks
    public static IEnumerable<object[]> SignaturePages() =>
        AllPages().Where(p => DocumentationCodeBlocks.SignatureBlocks(MarkdownPath(p)).Count > 0).Select(p => new object[] { p });

    // The arguments of a listing, split at the commas outside brackets and generic arguments
    private static List<string> SplitArguments(string text)
    {
        var arguments = new List<string>();
        int depth = 0, start = 0;
        for (int k = 0; k <= text.Length; k++)
        {
            if (k == text.Length || (text[k] == ',' && depth == 0))
            {
                arguments.Add(text[start..k].Trim());
                start = k + 1;
            }
            else if (text[k] is '(' or '<' or '[') depth++;
            else if (text[k] is ')' or '>' or ']') depth--;
        }

        arguments.RemoveAll(a => a.Length == 0);
        return arguments;
    }

    private static readonly Regex Listing = new(@"^(?:public\s+(?<return>[\w<>\[\],.]+)\s+)?(?<new>new\s+)?(?<name>\w+)\s*\((?<arguments>.*)\)\s*;?$",
        RegexOptions.Singleline);

    /// <summary>
    /// Every csharp-signature block of a page, a constructor or method listing or a constructor call of free names, names
    /// a public member of the library: a type with a public constructor of that many parameters (of those names, in order,
    /// where the listing declares them), or a public method of that name, those parameter names in order and that return
    /// type. Red on the listing of reference/lambert.md as 46e6975 wrote it (then fenced csharp, so read by no check),
    /// whose third parameter of <c>LambertSolver.Solve</c> was <c>targetObjectOrbitalParameters</c>, which the library then
    /// spelled <c>targetOjectOrbitalParameters</c>; red again on the listing renamed by #662 against a85098d, before the
    /// library's parameter was renamed to <c>targetObjectOrbitalParameters</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(SignaturePages))]
    public void SignatureBlocks_NameMembersOfTheLibrary(string page)
    {
        var types = typeof(IO.Astrodynamics.Constants).Assembly.GetExportedTypes();
        var faults = new List<string>();
        foreach (var block in DocumentationCodeBlocks.SignatureBlocks(MarkdownPath(page)))
        {
            var text = string.Join(" ", block.Select(l => l.Trim()));
            var match = Listing.Match(text);
            if (!match.Success)
            {
                faults.Add($"not a listing: {text}");
                continue;
            }

            var arguments = SplitArguments(match.Groups["arguments"].Value);
            // A declared parameter is "Type name"; a free name of a constructor call has no type
            var names = arguments.Select(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(w => w.Length > 1 ? w[^1] : null).ToList();
            bool Matches(ParameterInfo[] parameters) =>
                parameters.Length == arguments.Count && names.Select((n, k) => n == null || parameters[k].Name == n).All(b => b);
            var name = match.Groups["name"].Value;
            bool found = match.Groups["return"].Success
                ? types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    .Any(m => m.Name == name && m.ReturnType.Name == match.Groups["return"].Value && Matches(m.GetParameters()))
                : types.Where(t => t.Name == name).SelectMany(t => t.GetConstructors()).Any(c => Matches(c.GetParameters()));
            if (!found)
            {
                faults.Add(text);
            }
        }

        Assert.True(faults.Count == 0, $"{page}: {string.Join("; ", faults)}");
    }

    #endregion

    #region The blocks as a reader compiles them

    // The using directives a console application of the .NET SDK holds implicitly (ImplicitUsings of Microsoft.NET.Sdk)
    private static readonly string[] ImplicitUsings =
        { "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks" };

    // The references of a reader's application: the assemblies of the runtime, the library and those it references
    private static readonly Lazy<List<MetadataReference>> ReaderReferences = new(() =>
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => string.Equals(Path.GetDirectoryName(p), runtime, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<Assembly>(new[] { typeof(IO.Astrodynamics.Constants).Assembly });
        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            if (!paths.Add(assembly.Location))
            {
                continue;
            }

            foreach (var name in assembly.GetReferencedAssemblies())
            {
                var referenced = Assembly.Load(name);
                if (!string.Equals(Path.GetDirectoryName(referenced.Location), runtime, StringComparison.Ordinal))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    });

    private static readonly CSharpParseOptions ReaderParse = new(LanguageVersion.Latest);
    private static readonly CSharpCompilationOptions ReaderOptions = new(OutputKind.DynamicallyLinkedLibrary);

    // The global using directives of a reader's application
    private static SyntaxTree ImplicitUsingsTree() =>
        CSharpSyntaxTree.ParseText(string.Concat(ImplicitUsings.Select(u => $"global using {u};\n")), ReaderParse);

    // The page's compiled file, bound under its own directives, and its methods
    private static (SemanticModel Model, List<MethodDeclarationSyntax> Methods) CompiledMethods(string page)
    {
        var file = Compiled(page).File;
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ReaderParse, file);
        var model = CSharpCompilation.Create("Page", new[] { tree }, ReaderReferences.Value, ReaderOptions).GetSemanticModel(tree);
        return (model, tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToList());
    }

    // The using directives a reader holds at block n of a page: those the block declares and those of the earlier blocks
    private static List<string> UsingsAt(string page, int n)
    {
        var blocks = DocumentationCodeBlocks.Blocks(MarkdownPath(page));
        int first = FirstCompiledBlock.GetValueOrDefault(page);
        var usings = new List<string>();
        for (int k = 1; k <= n; k++)
        {
            usings.AddRange(DocumentationCodeBlocks.SplitUsings(blocks[first + k - 1]).Usings.Where(u => !usings.Contains(u)));
        }

        return usings;
    }

    // The method of block n as a reader's source: the reader's directives, a class outside any namespace, as a top-level
    // program's, the signature in fully qualified names, the given body; with a console, a StringWriter field named
    // Console, to which the block's Console.WriteLine binds (the class's member hides System.Console)
    private static string ReaderSource(string page, int n, SemanticModel model, MethodDeclarationSyntax method, string body, bool console)
    {
        string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Assert.Contains($"#region {page}:{n}", method.ToFullString());
        var symbol = model.GetDeclaredSymbol(method)!;
        Assert.All(symbol.Parameters, p => Assert.NotEqual(TypeKind.Error, p.Type.TypeKind));
        var parameters = string.Join(", ", symbol.Parameters.Select(p => $"{Name(p.Type)} {p.Name}"));
        var modifier = method.Modifiers.Any(SyntaxKind.AsyncKeyword) ? "async " : "";
        var field = console ? "    internal static readonly global::System.IO.StringWriter Console = new();\n" : "";
        return string.Concat(UsingsAt(page, n).Select(u => u + "\n")) +
               $"\ninternal static class Block{n:00}OfThePage\n{{\n{field}    internal static {modifier}{Name(symbol.ReturnType)} Block({parameters})\n    {body}\n}}\n";
    }

    // The errors of a compilation, each with the block of its tree and its line
    private static List<string> Errors(CSharpCompilation compilation, IReadOnlyDictionary<SyntaxTree, int> blockOfTree) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => blockOfTree.TryGetValue(d.Location.SourceTree!, out int n)
                ? $"block {n}: {d.Id} {d.GetMessage(CultureInfo.InvariantCulture)} in \"{d.Location.SourceTree!.GetText().Lines[d.Location.GetLineSpan().StartLinePosition.Line].ToString().Trim()}\""
                : $"{d.Id} {d.GetMessage(CultureInfo.InvariantCulture)}")
            .ToList();

    // The pages with a block that declares using directives, each of whose blocks is compiled with them (#716)
    public static IEnumerable<object[]> PagesWithUsings() =>
        Pages().Where(p => DocumentationCodeBlocks.Blocks(MarkdownPath((string)p[0])).Any(b => DocumentationCodeBlocks.SplitUsings(b).Usings.Count > 0));

    /// <summary>
    /// Every csharp block of a page that declares using directives, whether the block declares some or not, compiles, as
    /// its method of the page's file, with the directives it declares, the ones the earlier blocks of the page declare and
    /// the implicit ones of a console application, against the library alone: a reader who copies the block with the
    /// directives the page gave compiles it. The regions compile in their file under the directives of every namespace of
    /// the library, and <see cref="PageBlocks_EqualTheirCompiledRegions"/> checks only that a block's directives name
    /// namespaces. Red on the first blocks of tle-and-omm.md (<c>TLE</c>, whose namespace
    /// <c>IO.Astrodynamics.OrbitalParameters.TLE</c> the block did not import, CS0246) and coordinate-conversions.md
    /// (<c>StateVector</c> and <c>PlanetsAndMoons</c>, CS0246 and CS0103), which the first version of #705 left; and, once
    /// the blocks without directives of their own were compiled too (#716; #705 compiled only those that declare some), on
    /// a80f6a5 on six blocks of three pages whose namespace the page never imported: <c>Planetodetic</c> in block 5 of
    /// coordinate-conversions.md (<c>IO.Astrodynamics.Coordinates</c>, CS0246), <c>CentralBodyPropagator</c> and
    /// <c>CentralBodyPropagatorBuilder</c> in blocks 3, 6 and 7 of numerical-propagation.md
    /// (<c>IO.Astrodynamics.Propagator</c>, CS0246), <c>Omm</c> and <c>Opm</c> in blocks 7 and 10 of tle-and-omm.md
    /// (<c>IO.Astrodynamics.CCSDS.OMM</c> and <c>.OPM</c>, CS0103). The pages that declare no directive are not read here:
    /// they give the reader none.
    /// </summary>
    [Theory]
    [MemberData(nameof(PagesWithUsings))]
    public void PageBlocks_CompileWithTheUsingsOfTheirPage(string page)
    {
        var (model, methods) = CompiledMethods(page);
        var blocks = DocumentationCodeBlocks.Blocks(MarkdownPath(page));
        int first = FirstCompiledBlock.GetValueOrDefault(page);
        var trees = new List<SyntaxTree> { ImplicitUsingsTree() };
        var blockOfTree = new Dictionary<SyntaxTree, int>();
        for (int n = 1; n <= blocks.Count - first; n++)
        {
            var method = Assert.Single(methods, m => m.Identifier.Text == $"Block{n:00}");
            var tree = CSharpSyntaxTree.ParseText(ReaderSource(page, n, model, method, method.Body!.ToFullString(), false), ReaderParse);
            trees.Add(tree);
            blockOfTree[tree] = n;
        }

        Assert.True(blockOfTree.Count > 0, page);
        var errors = Errors(CSharpCompilation.Create("Reader", trees, ReaderReferences.Value, ReaderOptions), blockOfTree);
        Assert.True(errors.Count == 0, $"{page}: {string.Join("; ", errors)}");
    }

    /// <summary>
    /// Runs block n of a page, the text of the page, as a reader compiles it
    /// (<see cref="PageBlocks_CompileWithTheUsingsOfTheirPage"/>), without the lines <paramref name="skip"/> names, and
    /// returns what it writes to the console. The block takes no input and returns nothing.
    /// </summary>
    private static string RunAsReader(string page, int n, Func<string, bool> skip)
    {
        var (model, methods) = CompiledMethods(page);
        var method = Assert.Single(methods, m => m.Identifier.Text == $"Block{n:00}");
        Assert.Empty(method.ParameterList.Parameters);
        Assert.Equal("void", method.ReturnType.ToString());
        var block = DocumentationCodeBlocks.Blocks(MarkdownPath(page))[FirstCompiledBlock.GetValueOrDefault(page) + n - 1];
        var code = DocumentationCodeBlocks.SplitUsings(block).Code.Where(l => !skip(l.Trim()));
        var tree = CSharpSyntaxTree.ParseText(ReaderSource(page, n, model, method, "{\n" + string.Join("\n", code) + "\n}", true), ReaderParse);
        var compilation = CSharpCompilation.Create($"Reader{Guid.NewGuid():N}", new[] { ImplicitUsingsTree(), tree }, ReaderReferences.Value, ReaderOptions);
        var errors = Errors(compilation, new Dictionary<SyntaxTree, int> { [tree] = n });
        Assert.True(errors.Count == 0, $"{page}: {string.Join("; ", errors)}");

        using var image = new MemoryStream();
        Assert.True(compilation.Emit(image).Success, page);
        image.Position = 0;
        // A collectible context: the library and the kernels resolve to those of the test process
        var context = new System.Runtime.Loader.AssemblyLoadContext($"{page}:{n}", isCollectible: true);
        try
        {
            var type = context.LoadFromStream(image).GetType($"Block{n:00}OfThePage")!;
            type.GetMethod("Block", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            return type.GetField("Console", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString()!;
        }
        finally
        {
            context.Unload();
        }
    }

    #endregion

    #region The blocks that run

    // The blocks that need no input and run on Data/SolarSystem alone, with no file written or read and no global state
    // changed, in a time the test suite affords; the others are compiled only, or run with their inputs by the Facts below
    // (kernel loads, the global data provider, CCSDS and Cosmographia files, LRO outside Data/LRO's coverage, long
    // propagations)
    public static IEnumerable<object[]> RunnableBlocks() =>
        new (string Page, int Block)[]
            {
                ("docs/get-started.md", 4), ("docs/get-started.md", 5),
                ("docs/reference/integrators.md", 1), ("docs/reference/integrators.md", 2), ("docs/reference/integrators.md", 3),
                ("docs/reference/math.md", 1), ("docs/reference/predefined-objects.md", 1),
                ("docs/reference/spacecraft.md", 4), ("docs/reference/spacecraft.md", 6), ("docs/reference/stars.md", 1),
                ("docs/reference/time-and-windows.md", 1), ("docs/reference/time-and-windows.md", 2), ("docs/reference/tle.md", 1),
                ("docs/tutorials/coordinate-conversions.md", 1), ("docs/tutorials/coordinate-conversions.md", 5),
                ("docs/tutorials/ephemeris-queries.md", 2), ("docs/tutorials/monte-carlo.md", 3),
                ("docs/tutorials/numerical-propagation.md", 2), ("docs/tutorials/orbit-creation.md", 1),
            }
            .Select(b => new object[] { b.Page, b.Block });

    private static MethodInfo Block(string page, int block)
    {
        var type = typeof(DocumentationPagesExamplesTests).Assembly.GetType(Compiled(page).Type);
        Assert.NotNull(type);
        var method = type.GetMethod($"Block{block:00}", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method;
    }

    /// <summary>A block of a page that needs no input runs without an exception.</summary>
    [Theory]
    [MemberData(nameof(RunnableBlocks))]
    public async Task PageBlock_Runs(string page, int block)
    {
        var method = Block(page, block);
        Assert.Empty(method.GetParameters());
        if (method.Invoke(null, null) is Task task)
        {
            await task;
        }
    }

    #endregion

    #region The blocks that run with their inputs

    private static readonly TimeSystem.Time Epoch = new TimeSystem.Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);

    private static StateVector Leo(double radius, double inclination, TimeSystem.Time epoch)
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        double v = System.Math.Sqrt(earth.GM / radius);
        return new StateVector(new Vector3(radius, 0.0, 0.0), new Vector3(0.0, v * System.Math.Cos(inclination), v * System.Math.Sin(inclination)),
            earth, epoch, Frames.Frame.ICRF);
    }

    /// <summary>
    /// The first block of the TLE tutorial, run as a reader compiles it with the page's directives, prints the TLE's name,
    /// catalog number and mean inclination as its lines write them: the columns 3 to 7 of line 1 and 9 to 16 of line 2
    /// (#705: it read <c>tle.NoradId</c>, which does not exist, and multiplied the method group <c>tle.Inclination</c>, and
    /// its directives lacked <c>IO.Astrodynamics.OrbitalParameters.TLE</c>). The block's kernel load is not run: the test
    /// process holds Data/SolarSystem, which no test reloads. Red on the page of 46e6975 (CS0246 for <c>TLE</c>; with the
    /// directive added, CS1061 for <c>NoradId</c> and CS0019 for <c>Inclination</c>) and on the first version of #705
    /// (CS0246).
    /// </summary>
    [Fact]
    public void TleTutorial_FirstBlock_PrintsTheFieldsOfItsLines()
    {
        const string page = "docs/tutorials/tle-and-omm.md";
        var block = DocumentationCodeBlocks.Blocks(MarkdownPath(page))[0];
        var lines = block.SelectMany(l => Regex.Matches(l, "\"([12] [^\"]+)\"").Select(m => m.Groups[1].Value)).ToList();
        Assert.Equal(2, lines.Count);

        var printed = RunAsReader(page, 1, st => st == "SpiceAPI.Instance.LoadKernels(new DirectoryInfo(\"Data/SolarSystem\"));")
            .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Contains(':')).ToDictionary(l => l[..l.IndexOf(':')], l => l[(l.IndexOf(':') + 1)..].Trim());
        _output.WriteLine($"MEASURE #705 TLE tutorial, first block: {string.Join("; ", printed.Select(p => $"{p.Key}: {p.Value}"))}");
        Assert.Equal("ISS (ZARYA)", printed["Name"]);
        Assert.Equal(lines[0].Substring(2, 5).TrimStart('0'), printed["NORAD ID"]);
        Assert.Equal(lines[1].Substring(8, 8).Trim() + " deg", printed["Inclination"]);
    }

    // The two-body position from (r0, v0) after the given seconds, by Kepler's equation in universal variables (the
    // Lagrange f and g of the universal anomaly, Newton's method; Vallado, Fundamentals of Astrodynamics, algorithm 8):
    // the reference of the Lambert blocks, a closed form independent of the solver and of the library's propagators
    private static Vector3 TwoBodyPosition(Vector3 r0, Vector3 v0, double gm, double seconds)
    {
        double r = r0.Magnitude(), sqrtGm = System.Math.Sqrt(gm);
        double radialVelocity = r0 * v0 / r;
        double alpha = 2.0 / r - v0 * v0 / gm;
        (double C, double S) Stumpff(double z)
        {
            if (System.Math.Abs(z) < 1e-8)
            {
                return (0.5 - z / 24.0, 1.0 / 6.0 - z / 120.0);
            }

            double q = System.Math.Sqrt(System.Math.Abs(z));
            return z > 0
                ? ((1.0 - System.Math.Cos(q)) / z, (q - System.Math.Sin(q)) / (q * q * q))
                : ((System.Math.Cosh(q) - 1.0) / -z, (System.Math.Sinh(q) - q) / (q * q * q));
        }

        double chi = sqrtGm * System.Math.Abs(alpha) * seconds;
        for (int k = 0; k < 100; k++)
        {
            double z = alpha * chi * chi;
            var (c, s) = Stumpff(z);
            double f = r * radialVelocity / sqrtGm * chi * chi * c + (1.0 - alpha * r) * chi * chi * chi * s + r * chi - sqrtGm * seconds;
            double df = r * radialVelocity / sqrtGm * chi * (1.0 - z * s) + (1.0 - alpha * r) * chi * chi * c + r;
            double step = f / df;
            chi -= step;
            if (System.Math.Abs(step) <= 1e-15 * System.Math.Abs(chi))
            {
                break;
            }
        }

        var (cf, sf) = Stumpff(alpha * chi * chi);
        double lagrangeF = 1.0 - chi * chi / r * cf;
        double lagrangeG = seconds - chi * chi * chi * sf / sqrtGm;
        return r0 * lagrangeF + v0 * lagrangeG;
    }

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (CI runs 43906476725 and 43926830045; MEASURE lines
    // below), rounded up at the second significant digit: the arrival of the two-body motion from the departure with
    // each Lambert velocity, against the arrival position: 3.217e-7 m at worst on Windows, 3.062e-7 m on Linux and
    // macOS arm64 (one revolution, right branch; 2 revolutions and less, over 5 h), the solver's; the closed form
    // resolves about 1e-8 m (the rounding of the flight time, 1e-16 of 1.8e4 s)
    private const double LambertArrivalLimit = 6.5e-7;

    /// <summary>
    /// The Lambert blocks of the mission design tutorial run (#705: they passed <c>shortWay:</c> and
    /// <c>nRevolutions:</c>, which <c>Solve</c> does not have, and read <c>DepartureVelocity</c>, <c>ArrivalVelocity</c> and
    /// <c>TotalDeltaV</c>, which <c>LambertSolution</c> does not have), and each solution's departure velocity carries the
    /// two-body motion from the departure to the arrival position: the closed form of <see cref="TwoBodyPosition"/>
    /// (Kepler's equation in universal variables, the Lagrange f and g), BODY399_GM, the TT seconds of the flight (#508).
    /// </summary>
    [Fact]
    public void MissionDesignTutorial_LambertBlocks_ReachTheArrival()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var departure = Leo(7000000.0, 0.5, Epoch);
        var arrivalEpoch = Epoch.AddHours(5.0);
        double angle = 160.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var arrival = new StateVector(new Vector3(7300000.0 * System.Math.Cos(angle), 7300000.0 * System.Math.Sin(angle), 400000.0),
            new Vector3(0.0, 0.0, 0.0), earth, arrivalEpoch, Frames.Frame.ICRF);
        double seconds = PropagationTimeArgument.Tt.SecondsBetween(Epoch, arrivalEpoch);

        var direct = (LambertSolution)Block("docs/tutorials/mission-design.md", 1).Invoke(null, new object[] { departure, arrival, earth });
        var result = (LambertResult)Block("docs/tutorials/mission-design.md", 2).Invoke(null, new object[] { new LambertSolver(), departure, arrival, earth });

        Assert.Equal(0u, direct.Revolutions);
        Assert.Contains(result.Solutions, s => s.Revolutions == 2);
        double worst = 0.0;
        foreach (var solution in result.Solutions.Prepend(direct))
        {
            double miss = (TwoBodyPosition(departure.Position, solution.V1, earth.GM, seconds) - arrival.Position).Magnitude();
            _output.WriteLine($"MEASURE #705 mission design Lambert, {solution.Revolutions} revolution(s) {solution.Branch}: arrival miss {miss:E3} m");
            worst = System.Math.Max(worst, miss);
        }

        Assert.True(worst < LambertArrivalLimit, $"{worst:E3} m");
    }

    // The obliquity of ECLIPJ2000, the IAU 1976 value at J2000 (84381.448 arcseconds; NAIF, Frames Required Reading)
    private const double ObliquityJ2000 = 84381.448 / 3600.0 * IO.Astrodynamics.Constants.Deg2Rad;

    // Twice the worst over the three platforms (the same on all three), as above: the ecliptic state of the tutorial
    // against the rotation by the obliquity, 9.313e-10 m and 1.819e-12 m/s, rounding (1.4e-16 of the position); the
    // CIRS round trip against the ICRF position, 1.370e-16 relative to its magnitude
    private const double EclipticPositionLimit = 1.9e-9;
    private const double EclipticVelocityLimit = 3.7e-12;
    private const double CirsRoundTripLimit = 2.8e-16;

    /// <summary>
    /// The frame blocks of the coordinate conversions tutorial run (#705: they read <c>Position</c> on the
    /// <c>OrbitalParameters</c> that <c>ToFrame</c> returns, and the CIRS block used <c>positionCirs</c> before declaring
    /// it): the ecliptic state is the ICRF state rotated about x by the obliquity of ECLIPJ2000, and the CIRS round trip
    /// gives the ICRF position back.
    /// </summary>
    [Fact]
    public void CoordinateConversionsTutorial_FrameBlocks_Run()
    {
        var (svIcrf, svEcliptic) = ((StateVector, StateVector))Block("docs/tutorials/coordinate-conversions.md", 1).Invoke(null, null)!;
        double c = System.Math.Cos(ObliquityJ2000), s = System.Math.Sin(ObliquityJ2000);
        Vector3 Rotate(Vector3 x) => new(x.X, c * x.Y + s * x.Z, -s * x.Y + c * x.Z);
        double dr = (svEcliptic.Position - Rotate(svIcrf.Position)).Magnitude();
        double dv = (svEcliptic.Velocity - Rotate(svIcrf.Velocity)).Magnitude();
        _output.WriteLine($"MEASURE #705 coordinate conversions, ecliptic against the obliquity rotation: |dr| {dr:E3} m, |dv| {dv:E3} m/s");
        Assert.True(dr < EclipticPositionLimit, $"{dr:E3} m");
        Assert.True(dv < EclipticVelocityLimit, $"{dv:E3} m/s");

        var (positionIcrf, positionCirs, positionBack) =
            ((Vector3, Vector3, Vector3))Block("docs/tutorials/coordinate-conversions.md", 3).Invoke(null, new object[] { svIcrf, svIcrf.Epoch })!;
        Assert.Equal(svIcrf.Position, positionIcrf);
        Assert.True((positionCirs - positionIcrf).Magnitude() > 1.0e3, "CIRS differs from ICRF by precession and nutation");
        double back = (positionBack - positionIcrf).Magnitude() / positionIcrf.Magnitude();
        _output.WriteLine($"MEASURE #705 coordinate conversions, CIRS round trip: {back:E3} relative");
        Assert.True(back < CirsRoundTripLimit, $"{back:E3}");
    }

    // The angle between two vectors, from atan2 of their cross and dot products
    private static double Angle(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    /// <summary>
    /// The lunar eclipse block of the geometry tutorial (#705: it passed <c>occultingBody:</c> and <c>occultedBody:</c>,
    /// which the search does not have, on the Sun) finds the penumbral lunar eclipse of 2024-03-25 as one window, each
    /// end bracketed within 60 s by the discs of the Earth and the Moon as the Sun's centre sees them (the bodies read from
    /// SPICE, angular radii asin(R/d)): disjoint 60 s outside it with the equatorial radii, overlapping 60 s inside it with
    /// the Earth's polar radius, the limb of the ellipsoid lying between the two.
    /// </summary>
    [Fact]
    public void GeometryTutorial_LunarEclipse_IsBracketedByTheDiscs()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);
        var searchWindow = new Window(new TimeSystem.Time(2024, 3, 24, 0, 0, 0, TimeScale.TDB), new TimeSystem.Time(2024, 3, 27, 0, 0, 0, TimeScale.TDB));
        var windows = ((IEnumerable<Window>)Block("docs/tutorials/geometry-and-visibility.md", 1)
            .Invoke(null, new object[] { moon, searchWindow, sun, earth })!).ToList();

        var eclipse = Assert.Single(windows);
        _output.WriteLine($"MEASURE #705 geometry tutorial, lunar eclipse from the Sun's centre: {eclipse.StartDate} to {eclipse.EndDate}");
        // The separation of the discs minus the sum of their angular radii, for the Earth's radius given
        double Margin(TimeSystem.Time at, double earthRadius)
        {
            var toEarth = SpiceAPI.Instance.ReadEphemeris(at, 10, 399, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
            var toMoon = SpiceAPI.Instance.ReadEphemeris(at, 10, 301, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
            return Angle(toEarth, toMoon) - System.Math.Asin(earthRadius / toEarth.Magnitude()) - System.Math.Asin(moon.EquatorialRadius / toMoon.Magnitude());
        }

        foreach (var (outside, inside) in new[] { (eclipse.StartDate.AddSeconds(-60.0), eclipse.StartDate.AddSeconds(60.0)), (eclipse.EndDate.AddSeconds(60.0), eclipse.EndDate.AddSeconds(-60.0)) })
        {
            Assert.True(Margin(outside, earth.EquatorialRadius) > 0.0, $"{outside}: the discs overlap");
            Assert.True(Margin(inside, earth.PolarRadius) < 0.0, $"{inside}: the discs are apart");
        }
    }

    /// <summary>
    /// The setup, run and results blocks of the batch propagation tutorial run on two LEOs over 1 h (#705: the results
    /// block read <c>ElapsedTime</c>, which <c>PropagationResult</c> does not have, and the progress block passed a lambda
    /// where <c>BatchOptions.Progress</c> takes an <c>IProgress&lt;BatchProgress&gt;</c>).
    /// </summary>
    [Fact]
    public void BatchTutorial_Blocks_Run()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var window = new Window(Epoch, Epoch.AddHours(1.0));
        var spacecraft1 = new Spacecraft(-7051, "BATCH1", 100.0, 1000.0, new Clock("BATCH1_CLK", 256), Leo(6800000.0, 0.9, Epoch));
        var spacecraft2 = new Spacecraft(-7052, "BATCH2", 100.0, 1000.0, new Clock("BATCH2_CLK", 256), Leo(7000000.0, 0.3, Epoch));

        var tasks = (List<PropagationTask>)Block("docs/tutorials/batch-propagation.md", 1).Invoke(null, new object[] { spacecraft1, window, earth, spacecraft2 })!;
        var result = (BatchResult)Block("docs/tutorials/batch-propagation.md", 2).Invoke(null, new object[] { tasks })!;
        Block("docs/tutorials/batch-propagation.md", 3).Invoke(null, new object[] { result });

        Assert.True(result.AllSucceeded, string.Join("; ", result.Failed.Select(f => f.Error.Message)));
        Assert.Equal(new[] { spacecraft1, spacecraft2 }, result.Results.Select(r => r.Spacecraft));
    }

    /// <summary>
    /// The per-task block of the thread-safety guide runs: two tasks over 1 h, each on its own Earth with EGM2008 to
    /// degree 10, its state rebuilt on that Earth (#705: the block called an <c>AtEpoch</c> of three arguments, which does
    /// not exist).
    /// </summary>
    [Fact]
    public void ThreadSafetyGuide_PerTaskBlock_Runs()
    {
        var result = (BatchResult)Block("docs/guides/thread-safety.md", 1)
            .Invoke(null, new object[] { 2, Leo(6800000.0, 0.9, Epoch), new Window(Epoch, Epoch.AddHours(1.0)) })!;
        Assert.True(result.AllSucceeded, string.Join("; ", result.Failed.Select(f => f.Error.Message)));
        Assert.Equal(2, result.Results.Count);
    }

    /// <summary>
    /// The pattern the per-task block stood for before #705, the state about a shared Earth without the geopotential and
    /// a per-task Earth with it in the bodies, fails every task: the bodies of one NAIF identifier must have the same
    /// models (#674), which is why the block rebuilds the state on the task's Earth.
    /// </summary>
    [Fact]
    public void ThreadSafetyGuide_StateOnASharedEarth_FailsEveryTask()
    {
        var orbit = Leo(6800000.0, 0.9, Epoch);
        var window = new Window(Epoch, Epoch.AddHours(1.0));
        var tasks = new List<PropagationTask>();
        for (int i = 0; i < 2; i++)
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
            var spacecraft = new Spacecraft(-(7061 + i), $"SHARED{i}", 100.0, 10000.0, new Clock($"SHARED{i}_CLK", 256), orbit);
            tasks.Add(new PropagationTask(spacecraft, window, new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
                IncludeAtmosphericDrag: false, IncludeSolarRadiationPressure: false, DeltaT: TimeSpan.FromSeconds(60),
                IntegratorFactory: () => new IO.Astrodynamics.Propagator.Integrators.RK78Integrator(1e-10, 1e-10, 30.0)));
        }

        var result = BatchPropagator.Propagate(tasks);
        Assert.Equal(2, result.Failed.Count);
        Assert.All(result.Failed, f => Assert.IsType<ArgumentException>(f.Error));
    }

    #endregion
}
