// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeveloperGuideSamples;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// The C# examples of DEVELOPER_GUIDE.md (#668): each block equals its compiled region of
/// <see cref="DeveloperGuideExamples"/>, so the guide cannot drift from code the compiler checks, and the blocks that need
/// only Data/SolarSystem run. Before #668, 22 of the 60 blocks did not compile against the library (an Engine built
/// without its tank, members that never existed, parameters of other names or types).
/// </summary>
public class DeveloperGuideExamplesTests
{
    private static readonly string GuidePath = Path.Combine(AppContext.BaseDirectory, "Docs", "DEVELOPER_GUIDE.md");
    private static readonly string ExamplesPath = Path.Combine(AppContext.BaseDirectory, "Docs", "DeveloperGuideExamples.cs");

    private readonly ITestOutputHelper _output;

    public DeveloperGuideExamplesTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    #region The guide against the compiled regions

    // The csharp blocks of the guide, in order
    private static List<List<string>> GuideBlocks() => DocumentationCodeBlocks.Blocks(GuidePath);

    /// <summary>
    /// Every csharp block of the guide but the excerpt of <c>Constants</c> equals its region of
    /// <see cref="DeveloperGuideExamples"/>, its using directives aside, and there are as many regions as blocks: red on
    /// 9015ea2, whose 22 blocks that do not compile cannot equal compiled regions.
    /// </summary>
    [Fact]
    public void GuideBlocks_EqualTheirCompiledRegions()
    {
        DocumentationCodeBlocks.AssertBlocksEqualRegions("DEVELOPER_GUIDE.md", GuideBlocks(), 1, DocumentationCodeBlocks.Regions(ExamplesPath, "guide"));
    }

    /// <summary>The excerpt of <c>Constants</c> in the guide (its first csharp block) gives the library's values.</summary>
    [Fact]
    public void GuideConstants_EqualTheLibrary()
    {
        DocumentationCodeBlocks.AssertConstantsEqualTheLibrary(GuideBlocks()[0]);
    }

    // The methods of the guide's member tables: the type of the heading above (#### Name, its first word), each
    // `Name(arguments)` of a row's first cell, its argument count as the row writes it ("..." standing for any further
    // arguments), and the line
    private static List<(string Type, string Name, int Arguments, bool Open, int Line)> GuideMethodRows()
    {
        var lines = File.ReadAllLines(GuidePath);
        var rows = new List<(string, string, int, bool, int)>();
        string heading = null;
        for (int i = 0; i < lines.Length; i++)
        {
            var title = Regex.Match(lines[i], @"^#### (\w+)");
            if (title.Success)
            {
                heading = title.Groups[1].Value;
                continue;
            }

            int end = lines[i].IndexOf("` |", StringComparison.Ordinal);
            if (heading == null || !lines[i].StartsWith("| `", StringComparison.Ordinal) || end < 0)
            {
                continue;
            }

            foreach (Match member in Regex.Matches(lines[i][..(end + 1)], @"`(\w+)\(([^`]*)\)`"))
            {
                // The arguments split at the commas outside brackets and generic arguments
                var arguments = new List<string>();
                int depth = 0, start = 0;
                var text = member.Groups[2].Value;
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
                bool open = arguments.Remove("...");
                rows.Add((heading, member.Groups[1].Value, arguments.Count, open, i + 1));
            }
        }

        return rows;
    }

    /// <summary>
    /// Every method row of the guide's member tables names a public method (a constructor when it is the type's name) of
    /// the type of its heading, its bases or the library's extension methods, with an overload that takes the row's
    /// argument count, so a table cannot list a member that does not exist: red on 9a99acc, whose KeplerianElements
    /// table lists <c>ApogeeRadius()</c>, which the library never declared (#668, second round).
    /// </summary>
    [Fact]
    public void GuideMethodTables_NameMembersOfTheLibrary()
    {
        var assembly = typeof(IO.Astrodynamics.Constants).Assembly;
        var types = assembly.GetExportedTypes();
        var extensions = types.Where(t => t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)).ToList();
        var rows = GuideMethodRows();
        Assert.NotEmpty(rows);
        var misses = new List<string>();
        foreach (var (typeName, name, arguments, open, line) in rows)
        {
            var candidates = types.Where(t => (t.IsGenericType ? t.Name[..t.Name.IndexOf('`')] : t.Name) == typeName).ToList();
            if (candidates.Count == 0)
            {
                misses.Add($"line {line}: no public type {typeName}");
                continue;
            }

            bool Accepts(ParameterInfo[] parameters, int skip)
            {
                var own = parameters.Skip(skip).ToArray();
                int required = own.Count(p => !p.IsOptional && !p.IsDefined(typeof(ParamArrayAttribute), false));
                bool unbounded = own.Length > 0 && own[^1].IsDefined(typeof(ParamArrayAttribute), false);
                return (open || arguments >= required) && (unbounded || arguments <= own.Length);
            }

            bool found = candidates.Any(type =>
                (name == typeName && type.GetConstructors().Any(c => Accepts(c.GetParameters(), 0)))
                || type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Any(m => m.Name == name && Accepts(m.GetParameters(), 0))
                || extensions.Any(m => m.Name == name && m.GetParameters()[0].ParameterType.IsAssignableFrom(type) && Accepts(m.GetParameters(), 1)));
            if (!found)
            {
                misses.Add($"line {line}: {typeName}.{name} with {arguments} argument(s){(open ? " or more" : "")}");
            }
        }

        Assert.True(misses.Count == 0, string.Join(Environment.NewLine, misses));
    }

    #endregion

    #region The spacecraft section

    // The reference: the two-body state of the example's Keplerian elements at the elapsed TT seconds, by Kepler's
    // equation with the GM of the kernels (BODY399_GM), the orbit's RAAN and argument of periapsis zero
    private static (Vector3 Position, Vector3 Velocity) TwoBody(KeplerianElements orbit, double gm, double seconds)
    {
        double a = orbit.A, e = orbit.E, i = orbit.I;
        Assert.Equal(0.0, orbit.RAAN);
        Assert.Equal(0.0, orbit.AOP);
        double n = System.Math.Sqrt(gm / (a * a * a));
        double m = orbit.M + n * seconds;
        double bigE = m;
        for (int k = 0; k < 50; k++)
        {
            double step = (bigE - e * System.Math.Sin(bigE) - m) / (1.0 - e * System.Math.Cos(bigE));
            bigE -= step;
            if (System.Math.Abs(step) < 1e-15)
            {
                break;
            }
        }

        double root = System.Math.Sqrt(1.0 - e * e);
        double r = a * (1.0 - e * System.Math.Cos(bigE));
        double xp = a * (System.Math.Cos(bigE) - e), yp = a * root * System.Math.Sin(bigE);
        double factor = System.Math.Sqrt(gm * a) / r;
        double vxp = -factor * System.Math.Sin(bigE), vyp = factor * root * System.Math.Cos(bigE);
        // Rotated by the inclination about the line of nodes, the x axis
        return (new Vector3(xp, yp * System.Math.Cos(i), yp * System.Math.Sin(i)),
            new Vector3(vxp, vyp * System.Math.Cos(i), vyp * System.Math.Sin(i)));
    }

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the same on the three, CI run 43745883709, 2026-11-15;
    // MEASURE line below), rounded up at the second significant digit: the example's Velocity-Verlet propagation at 1 s
    // steps against the two-body closed form after 12 h, 1.245e2 m and 1.352e-1 m/s, the integrator's own error (second
    // order: 1.245e4 m at 10 s, 4.475e5 m at 60 s, the step of the guide before #668; RK7(8) 0.32 m at any of these steps)
    private const double SpacecraftExamplePositionLimit = 2.5e2;
    private const double SpacecraftExampleVelocityLimit = 2.8e-1;

    /// <summary>
    /// The spacecraft section runs: the tank added before the engine built on it (#676), the spacecraft's components and
    /// total mass from the inputs (500 kg dry, 100 kg of fuel), and the dense output at 12 h against the two-body state of
    /// its Keplerian elements (Kepler's equation, BODY399_GM, the elapsed TT seconds about the Earth, #501).
    /// </summary>
    [Fact]
    public void SpacecraftComponents_Example_Runs()
    {
        var (spacecraft, solution, orbit, epoch) = DeveloperGuideExamples.Block30();

        var tank = Assert.Single(spacecraft.FuelTanks);
        var engine = Assert.Single(spacecraft.Engines);
        Assert.Same(tank, engine.FuelTank);
        Assert.Equal(100.0, tank.Quantity);
        Assert.Equal(300.0 * 0.05 * IO.Astrodynamics.Constants.g0, engine.Thrust);
        Assert.Equal(600.0, spacecraft.GetTotalMass());
        Assert.Single(spacecraft.Instruments);

        var at = epoch.AddHours(12);
        double seconds = PropagationTimeArgument.Tt.SecondsBetween(epoch, at);
        var (position, velocity) = solution.InterpolateAt(at);
        var reference = TwoBody(orbit, PlanetsAndMoons.EARTH_BODY.GM, seconds);
        double dr = (position - reference.Position).Magnitude();
        double dv = (velocity - reference.Velocity).Magnitude();
        _output.WriteLine($"MEASURE #668 guide spacecraft example at 12 h: |dr| {dr:E3} m, |dv| {dv:E3} m/s");
        Assert.True(dr < SpacecraftExamplePositionLimit, $"{dr:E3} m");
        Assert.True(dv < SpacecraftExampleVelocityLimit, $"{dv:E3} m/s");
    }

    /// <summary>
    /// The order of the guide before #668, the engine added before its tank, throws: <c>AddEngine</c> takes an engine whose
    /// tank the spacecraft holds (#676), which is why the example adds the tank first.
    /// </summary>
    [Fact]
    public void SpacecraftComponents_EngineBeforeTank_Throws()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new TimeSystem.Time(2024, 1, 1);
        var orbit = new KeplerianElements(7000000, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0, 0, 0, earth, epoch, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1001, "MySat", 500.0, 1000.0, new Clock("MainClock", 256), orbit);
        var tank = new FuelTank("MainTank", "Propellant", "Tank1", 200.0, 100.0);
        var engine = new Engine("MainEngine", "RCS", "Engine1", 300.0, 0.05, tank);
        var exception = Assert.Throws<ArgumentException>(() => spacecraft.AddEngine(engine));
        Assert.Equal("engine", exception.ParamName);
        Assert.Empty(spacecraft.Engines);
    }

    #endregion

    #region The deep space example

    // The NAIF identifiers of the example's perturbing bodies: the Sun and the barycentres of Jupiter and Saturn
    private static readonly int[] DeepSpaceThirdBodies = { 10, 5, 6 };

    // The reference of the deep space example (#704): the same forces integrated independently of the library. A massless
    // probe in the Earth-centred ICRF, the point masses of the kernels (BODYnnn_GM of gm_de440.tpc through
    // GetCelestialBodyInfo), the third-body terms direct minus indirect, the bodies' positions read from SPICE (de440s) at
    // the TDB of TT0 + t over the TT seconds of the window (#501), classical RK4 at about 300 s. The central GM is the
    // Earth's, or the Earth's and the Moon's when moonMass is true (the Earth-Moon two-body problem of the Moon's SPK).
    private static (Vector3 Position, Vector3 Velocity) DeepSpaceReference(StateVector initial, TimeSystem.Time start, TimeSystem.Time end, bool moonMass)
    {
        var spice = SpiceAPI.Instance;
        double central = spice.GetCelestialBodyInfo(399).GM + (moonMass ? spice.GetCelestialBodyInfo(301).GM : 0.0);
        var gm = DeepSpaceThirdBodies.Select(id => spice.GetCelestialBodyInfo(id).GM).ToArray();
        var tt0 = start.ToTDT();
        double length = PropagationTimeArgument.Tt.SecondsBetween(start, end);

        Vector3 Acceleration(Vector3 r, double t)
        {
            var at = tt0.AddSeconds(t).ToTDB();
            var a = r * (-central / System.Math.Pow(r.Magnitude(), 3));
            for (int k = 0; k < DeepSpaceThirdBodies.Length; k++)
            {
                var body = spice.ReadEphemeris(at, 399, DeepSpaceThirdBodies[k], Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
                var d = body - r;
                a += d * (gm[k] / System.Math.Pow(d.Magnitude(), 3)) - body * (gm[k] / System.Math.Pow(body.Magnitude(), 3));
            }

            return a;
        }

        int steps = (int)System.Math.Ceiling(length / 300.0);
        double h = length / steps;
        var position = initial.Position;
        var velocity = initial.Velocity;
        double time = 0.0;
        for (int k = 0; k < steps; k++)
        {
            var k1r = velocity;
            var k1v = Acceleration(position, time);
            var k2r = velocity + k1v * (h / 2);
            var k2v = Acceleration(position + k1r * (h / 2), time + h / 2);
            var k3r = velocity + k2v * (h / 2);
            var k3v = Acceleration(position + k2r * (h / 2), time + h / 2);
            var k4r = velocity + k3v * h;
            var k4v = Acceleration(position + k3r * h, time + h);
            position += (k1r + k2r * 2 + k3r * 2 + k4r) * (h / 6);
            velocity += (k1v + k2v * 2 + k3v * 2 + k4v) * (h / 6);
            time += h;
        }

        return (position, velocity);
    }

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the same on all three, CI runs 43906476725 and
    // 43926830045; MEASURE lines below), rounded up at the second significant digit: the example's Velocity-Verlet
    // propagation at 5 min against the reference with GM_Earth + GM_Moon after 25 days, 4.684e2 m and 1.197e-3 m/s, the
    // integrator's own error (second order; the reference's own spread over its steps, 600 to 60 s, is about 1e-4 m)
    private const double DeepSpaceExamplePositionLimit = 9.4e2;
    private const double DeepSpaceExampleVelocityLimit = 2.4e-3;

    // Twice the worst over the three platforms (the same on all three), as above: the reference with GM_Earth + GM_Moon
    // against the Moon's SPK (de440s) after 25 days, 1.514e3 m, the perturbations the reference leaves out (the Earth's
    // and the Moon's figures, the other planets, relativity), and the example's propagated Moon against the SPK,
    // 1.972e3 m, both errors together
    private const double DeepSpaceMoonMassLimit = 3.1e3;
    private const double DeepSpaceExampleMoonLimit = 4.0e3;

    /// <summary>
    /// The deep space example of the guide propagates the Moon (#704): a massless probe at the Moon's state of J2000 TDB,
    /// 25 days about an Earth of GM_Earth + GM_Moon (a field of degree 0 with the model's GM), with the Sun and the
    /// barycentres of Jupiter and Saturn as point masses. It ends where an independent integration of these forces ends,
    /// the error of Velocity-Verlet at 5 min, and that integration ends within 1.5 km of the Moon's SPK, the physical
    /// reference the example prints its difference to. With the Earth's GM alone the reference ends about 54 310 km from
    /// the Moon, the Moon's own mass, which is what the example printed before #704 as its "position difference". Red on
    /// 46e6975: the block returned nothing to compare; red on the example of the first version of #704, which kept the
    /// Earth's GM alone: 5.431e7 m from the reference.
    /// </summary>
    [Fact]
    public async Task DeepSpaceExample_FollowsTheMoonOfTheEphemeris()
    {
        var (spacecraft, moonState, start, end) = await DeveloperGuideExamples.Block54();
        var earth = moonState.Observer;
        var moon = SpiceAPI.Instance.ReadEphemeris(end, 399, 301, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        var final = spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var reference = DeepSpaceReference(moonState, start, end, moonMass: true);
        double dr = (final.Position - reference.Position).Magnitude();
        double dv = (final.Velocity - reference.Velocity).Magnitude();
        _output.WriteLine($"MEASURE #704 guide deep space example against the N-body reference with GM_Earth + GM_Moon after 25 days: |dr| {dr:E3} m, |dv| {dv:E3} m/s");
        Assert.True(dr < DeepSpaceExamplePositionLimit, $"{dr:E3} m");
        Assert.True(dv < DeepSpaceExampleVelocityLimit, $"{dv:E3} m/s");

        double referenceToMoon = (reference.Position - moon.Position).Magnitude();
        double exampleToMoon = (final.Position - moon.Position).Magnitude();
        var earthOnly = DeepSpaceReference(moonState, start, end, moonMass: false);
        double earthOnlyToMoon = (earthOnly.Position - moon.Position).Magnitude();
        _output.WriteLine($"MEASURE #704 against the Moon's SPK after 25 days: the reference {referenceToMoon:E3} m, the example {exampleToMoon:E3} m, the reference with GM_Earth alone {earthOnlyToMoon:E3} m");
        Assert.True(referenceToMoon < DeepSpaceMoonMassLimit, $"{referenceToMoon:E3} m");
        Assert.True(exampleToMoon < DeepSpaceExampleMoonLimit, $"{exampleToMoon:E3} m");
        // The Moon's own mass: tens of thousands of km without it
        Assert.True(earthOnlyToMoon > 1.0e7, $"{earthOnlyToMoon:E3} m");
    }

    #endregion

    #region The other blocks

    // The blocks that run on Data/SolarSystem alone, with no file written or read and no global state changed, in a time
    // the test suite affords. Compiled only: 1, 2, 5 and 42 (kernel loads, ClearKernels), 6 and 41 (the global data
    // provider), 14 to 17, 19 to 22 (CCSDS files read or written), 23 (a spacecraft with executed maneuvers), 33 and 55
    // (a day at 1 s with a degree-10 geopotential), 49 (launch windows), 52 (a Cosmographia export), 30 and 54 (the Facts above)
    public static IEnumerable<object[]> RunnableBlocks() =>
        new[] { 3, 4, 7, 8, 9, 10, 11, 12, 13, 18, 25, 26, 27, 28, 29, 32, 34, 35, 36, 37, 38, 39, 40, 43, 44, 46, 47, 48, 50, 51, 53, 56, 57 }
            .Select(n => new object[] { n });

    /// <summary>A block of the guide that needs no input runs without an exception.</summary>
    [Theory]
    [MemberData(nameof(RunnableBlocks))]
    public async Task Block_Runs(int block)
    {
        var method = typeof(DeveloperGuideExamples).GetMethod($"Block{block:00}", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.Empty(method.GetParameters());
        if (method.Invoke(null, null) is Task task)
        {
            await task;
        }
    }

    // The inputs of the blocks that continue an earlier one, as their opening comments name them
    private static Spacecraft SpacecraftWithEngine(int naifId, out Engine engine)
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new TimeSystem.Time(2024, 1, 1);
        var orbit = new KeplerianElements(6800000.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0, earth, epoch, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(naifId, $"GUIDE{-naifId}", 500.0, 1000.0, new Clock($"GUIDE{-naifId}_CLK", 256), orbit);
        var tank = new FuelTank("Tank", "Model", "SN1", 100.0, 100.0);
        spacecraft.AddFuelTank(tank);
        engine = new Engine("Engine", "Model", "SN1", 300.0, 0.05, tank);
        spacecraft.AddEngine(engine);
        spacecraft.AddCircularInstrument(naifId * 1000 - 1, "Camera", "Imager", 0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        return spacecraft;
    }

    [Fact]
    public void Block24_ValidatesAnOpm()
    {
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0), PlanetsAndMoons.EARTH_BODY,
            new TimeSystem.Time(2024, 1, 1), Frames.Frame.ICRF);
        DeveloperGuideExamples.Block24(Opm.CreateFromStateVector("SAT-1", "2024-001A", state));
    }

    [Fact]
    public void Block31_BuildsTheTriadAttitudes()
    {
        var spacecraft = SpacecraftWithEngine(-6681, out var engine);
        var epoch = new TimeSystem.Time(2024, 1, 1, 12, 0, 0);
        DeveloperGuideExamples.Block31(PlanetsAndMoons.EARTH_BODY, new CelestialBody(PlanetsAndMoons.MOON), new CelestialBody(Stars.Sun),
            spacecraft.Instruments.First(), engine, epoch, epoch, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Block45_ReadsAnEphemeris()
    {
        DeveloperGuideExamples.Block45(new CelestialBody(PlanetsAndMoons.MOON), new TimeSystem.Time(2024, 1, 1), PlanetsAndMoons.EARTH_BODY, Frames.Frame.ICRF);
    }

    [Fact]
    public void Block58_BuildsTheFactoryAttitudes()
    {
        SpacecraftWithEngine(-6682, out var engine);
        DeveloperGuideExamples.Block58(PlanetsAndMoons.EARTH_BODY, new CelestialBody(Stars.Sun), engine, new TimeSystem.Time(2024, 1, 1),
            TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Block59_ReadsTheMoon()
    {
        DeveloperGuideExamples.Block59(new CelestialBody(PlanetsAndMoons.MOON), PlanetsAndMoons.EARTH_BODY, new TimeSystem.Time(2024, 1, 1));
    }

    #endregion
}
