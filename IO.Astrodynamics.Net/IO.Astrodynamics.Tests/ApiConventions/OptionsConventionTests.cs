// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// D1's options rule (#668, design PR #671, the reflection test of D1's "Tests") over the public surface, read by
/// reflection (<see cref="PublicSurface"/>): every public type named *Options is one sealed record per operation, of values
/// set at init, with a static Default equal to new(), taken last as <c>options = null</c>; no member takes two bools; a
/// time quantity in double seconds says so; sibling settings agree. Today's exceptions are the allow-list
/// OptionsConventionBaseline.txt, which may only shrink: a new exception fails, and so does an entry that no longer is
/// one. The options lots (L4 to L7) and lot L9 emptied it; the tolerances that are no time are exempt by their key
/// (<see cref="NonTimeTolerances"/>).
/// <list type="bullet">
/// <item>D1.sealed-record: the type is a sealed record (rule 1; the closed abstract IntegratorOptions of rule 5 excepted,
/// a record whose constructors are not visible outside the library).</item>
/// <item>D1.init-only: no public property has a public setter other than init (rules 1 and 3).</item>
/// <item>D1.default: a public static get-only property Default of the type itself, equal to a new instance (rule 1,
/// sub-choice 2).</item>
/// <item>D1.values: no property holds an integrator, a force or a delegate, alone, in an array, a collection or any
/// generic argument (rule 5).</item>
/// <item>D1.parameter: a parameter of an options type is named options, optional with a null default, and last, before
/// out parameters and a CancellationToken only (rule 1, sub-choice 5).</item>
/// <item>D1.bools: no method or constructor of the surface takes two bool parameters or more, out parameters aside
/// (positional flags become options, "the 23 members with two or more bools").</item>
/// <item>D1.seconds: no double property, field or parameter of the surface whose name holds Step, Duration or Tolerance
/// lacks the Seconds suffix (sub-choice 7: a duration is a TimeSpan, a tolerance below 100 ns a double named
/// ...Seconds). The rule reads names, not units: a tolerance that is no time is listed too, for its lot to rename or to
/// settle; lot L4 settled the error tolerances of an integrator (AbsoluteTolerance, RelativeTolerance).</item>
/// <item>D1.siblings: two options types that hold a property of one name hold it with one type and one value in new()
/// (sub-choice 11: Step, Aberration, ConvergenceTolerance repeated "with the same names, types, defaults"), but for
/// the pairs whose defaults differ by decision (CovariancePropagation, structuring choice 3 of #668).</item>
/// </list>
/// Every method and constructor is keyed by its signature (<see cref="PublicSurface.Signature"/>), so a new overload is
/// a new symbol.
/// </summary>
public class OptionsConventionTests
{
    internal const string BaselineFile = "OptionsConventionBaseline.txt";

    [Fact]
    public void OptionsTypes_FollowD1_OrAreInTheBaseline()
    {
        Assert.NotEmpty(OptionsTypes(PublicSurface.Types));
        PublicSurface.AssertMatchesBaseline(BaselineFile, Violations(PublicSurface.Types));
    }

    // The options types among the given ones: a type whose name ends with Options
    internal static IReadOnlyList<Type> OptionsTypes(IEnumerable<Type> types) =>
        types.Where(t => PublicSurface.SimpleName(t).EndsWith("Options", StringComparison.Ordinal) && !t.IsEnum).ToList();

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$", PublicSurface.Declared) != null;

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    // The closed hierarchy of rule 5: an abstract record no caller can derive from, its constructors not visible outside
    // the library; the copy constructor the compiler requires protected on a record that is not sealed (CS8878) aside, a
    // derived record outside the library failing on the internal abstract members of the hierarchy (lot L4,
    // IntegratorOptions)
    private static bool IsClosedAbstractRecord(Type type) =>
        type.IsAbstract && IsRecord(type) &&
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => !IsCopyConstructor(type, c))
            .All(c => !(c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly));

    private static bool IsCopyConstructor(Type type, ConstructorInfo constructor) =>
        constructor.GetParameters() is { Length: 1 } parameters && parameters[0].ParameterType == type;

    // An integrator, a force or a delegate, or a type built on one: an array, a nullable, a collection or any generic
    // argument of it (IReadOnlyList<ForceBase>, Func<Integrator>, Dictionary<string, ForceBase>)
    internal static bool HoldsBehaviour(Type type)
    {
        if (type.IsByRef || type.IsArray)
        {
            return HoldsBehaviour(type.GetElementType()!);
        }

        if (typeof(IIntegrator).IsAssignableFrom(type) || typeof(ForceBase).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(HoldsBehaviour);
    }

    private static bool IsBool(Type type) => type == typeof(bool) || type == typeof(bool?);

    private static bool IsDouble(Type type) => type == typeof(double) || type == typeof(double?);

    // The error tolerances of an integrator, in m, m/s and relative, no time: the names RK78IntegratorOptions takes from
    // the design (R06 of #668, D1's Before/After), settled by lot L4 as the rule's doc foresees
    private static readonly string[] IntegratorErrorTolerances = ["AbsoluteTolerance", "RelativeTolerance"];

    // The tolerances of the surface that are no time, exempt by their key: an angle, a matrix test and the residuals of the
    // root finders, in the unit of their function (the exemptions of the options convention test, the tolerances that are
    // not seconds, validated by S. Guillet, 2026-11-22; applied by lot L9 of #668, which empties the baseline)
    private static readonly HashSet<string> NonTimeTolerances = new(StringComparer.Ordinal)
    {
        "IO.Astrodynamics.Constants.AngularTolerance",
        "IO.Astrodynamics.Math.Matrix.Cholesky(double):tolerance",
        "IO.Astrodynamics.Math.Matrix.IsSymmetric(double):tolerance",
        "IO.Astrodynamics.Math.NewtonRaphson.BoundedNewtonRaphson(Func<double, double>, Func<double, double>, double, double, double, double, int):tolerance",
        "IO.Astrodynamics.Math.NewtonRaphson.Solve(Func<double, double>, Func<double, double>, double, double, int):tolerance",
        "IO.Astrodynamics.Math.NewtonRaphson.SolveVector(Func<double[], double[]>, Func<double[], double[,]>, double[], double, int):tolerance"
    };

    // A time quantity by its name: Step, Duration or Tolerance in it, and not ...Seconds
    internal static bool LacksSecondsSuffix(string name) =>
        (name.Contains("step", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("duration", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("tolerance", StringComparison.OrdinalIgnoreCase)) &&
        !name.EndsWith("Seconds", StringComparison.Ordinal) &&
        !IntegratorErrorTolerances.Contains(name, StringComparer.OrdinalIgnoreCase);

    // The siblings whose defaults differ by decision: CovariancePropagation, null (Auto) in PropagationOptions and
    // Covariance mode in ConjunctionAnalysisOptions, whose null throws (R06 of #668, structuring choice 3, decided by
    // S. Guillet, 2026-11-19); and Step, an integration step of 1 s in VVIntegratorOptions (D1.11, lot L4) and a search step, null where it is
    // inferred or required, in the three window search records (R15 of #668, lot L6)
    private static readonly (string A, string B)[] DecidedSiblings =
    [
        ("IO.Astrodynamics.Propagator.PropagationOptions.CovariancePropagation",
            "IO.Astrodynamics.Ssa.ConjunctionAnalysisOptions.CovariancePropagation"),
        ("IO.Astrodynamics.FieldOfViewSearchOptions.Step", "IO.Astrodynamics.Propagator.Integrators.VVIntegratorOptions.Step"),
        ("IO.Astrodynamics.OccultationSearchOptions.Step", "IO.Astrodynamics.Propagator.Integrators.VVIntegratorOptions.Step"),
        ("IO.Astrodynamics.Propagator.Integrators.VVIntegratorOptions.Step", "IO.Astrodynamics.WindowSearchOptions.Step")
    ];

    /// <summary>Every violation of D1 by the given types, as "rule&lt;TAB&gt;symbol".</summary>
    internal static IEnumerable<string> Violations(IReadOnlyList<Type> types)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var options = OptionsTypes(types);
        foreach (var type in options)
        {
            var name = PublicSurface.Name(type);
            if (!(type.IsSealed && IsRecord(type)) && !IsClosedAbstractRecord(type))
            {
                found.Add($"D1.sealed-record\t{name}");
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
                {
                    found.Add($"D1.init-only\t{name}.{property.Name}");
                }

                if (HoldsBehaviour(property.PropertyType))
                {
                    found.Add($"D1.values\t{name}.{property.Name}");
                }
            }

            if (!type.IsAbstract && !HasDefaultEqualToNew(type))
            {
                found.Add($"D1.default\t{name}");
            }
        }

        found.UnionWith(Siblings(options));

        foreach (var type in types)
        {
            foreach (var member in PublicSurface.Members(type))
            {
                var valueType = member switch
                {
                    PropertyInfo p => p.PropertyType,
                    FieldInfo f => f.FieldType,
                    _ => null
                };
                if (valueType != null && IsDouble(valueType) && LacksSecondsSuffix(member.Name) &&
                    !NonTimeTolerances.Contains(PublicSurface.Key(member)))
                {
                    found.Add($"D1.seconds\t{PublicSurface.Key(member)}");
                }
            }

            foreach (var method in PublicSurface.MethodsAndConstructors(type))
            {
                var parameters = method.GetParameters();
                bool generatedByRecord = options.Contains(method.DeclaringType) &&
                                         (method is ConstructorInfo || method.Name is "Deconstruct" or "Equals");
                if (method is MethodInfo { IsSpecialName: true })
                {
                    continue;
                }

                if (parameters.Count(p => !p.IsOut && IsBool(p.ParameterType)) >= 2)
                {
                    found.Add($"D1.bools\t{PublicSurface.Signature(method)}");
                }

                foreach (var p in parameters.Where(p => !p.IsOut && IsDouble(p.ParameterType) && LacksSecondsSuffix(p.Name!) &&
                                                        !NonTimeTolerances.Contains(PublicSurface.ParameterKey(method, p))))
                {
                    found.Add($"D1.seconds\t{PublicSurface.ParameterKey(method, p)}");
                }

                if (generatedByRecord)
                {
                    continue;
                }

                // The parameters after which an options parameter may come: out parameters and a CancellationToken
                int last = parameters.Length - 1;
                while (last >= 0 && (parameters[last].IsOut || parameters[last].ParameterType == typeof(CancellationToken)))
                {
                    last--;
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    if (p.IsOut || !options.Contains(p.ParameterType))
                    {
                        continue;
                    }

                    if (i != last || p.Name != "options" || !p.IsOptional || p.DefaultValue != null)
                    {
                        found.Add($"D1.parameter\t{PublicSurface.ParameterKey(method, p)}");
                    }
                }
            }
        }

        return found;
    }

    // Sub-choice 11: a property of one name on two options types has one type and, on new(), one value
    private static IEnumerable<string> Siblings(IReadOnlyList<Type> options)
    {
        var byName = options
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => (Type: t, Property: p)))
            .GroupBy(x => x.Property.Name, StringComparer.Ordinal);
        foreach (var group in byName)
        {
            var holders = group.OrderBy(x => PublicSurface.Name(x.Type), StringComparer.Ordinal).ToList();
            for (int i = 0; i < holders.Count; i++)
            {
                for (int j = i + 1; j < holders.Count; j++)
                {
                    var (a, b) = (holders[i], holders[j]);
                    if (!Agree(a.Type, a.Property, b.Type, b.Property) &&
                        !DecidedSiblings.Contains((PublicSurface.Key(a.Property), PublicSurface.Key(b.Property))))
                    {
                        yield return $"D1.siblings\t{PublicSurface.Key(a.Property)} ~ {PublicSurface.Key(b.Property)}";
                    }
                }
            }
        }
    }

    private static bool Agree(Type typeA, PropertyInfo a, Type typeB, PropertyInfo b)
    {
        if (a.PropertyType != b.PropertyType)
        {
            return false;
        }

        // The defaults compare where both types can be built; an abstract type has no default of its own
        var newA = typeA.IsAbstract ? null : typeA.GetConstructor(Type.EmptyTypes);
        var newB = typeB.IsAbstract ? null : typeB.GetConstructor(Type.EmptyTypes);
        return newA == null || newB == null || Equals(a.GetValue(newA.Invoke(null)), b.GetValue(newB.Invoke(null)));
    }
    // A public static get-only Default of the type itself, equal to the instance of the parameterless constructor
    private static bool HasDefaultEqualToNew(Type type)
    {
        var property = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (property == null || property.PropertyType != type || property.SetMethod != null)
        {
            return false;
        }

        var constructor = type.GetConstructor(Type.EmptyTypes);
        return constructor != null && Equals(property.GetValue(null), constructor.Invoke(null));
    }

    // The review's probe of lot L0: an options record with two bools, a double step, a collection of forces, and a
    // method with two bools and a double step; each must be reported
    public sealed record ProbeOptions
    {
        public static ProbeOptions Default { get; } = new();
        public bool UseA { get; init; }
        public bool UseB { get; init; }
        public double OutputStep { get; init; }
        public IReadOnlyList<ForceBase> Forces { get; init; }
        public Func<Integrator> IntegratorFactory { get; init; }
        public TimeSpan Step { get; init; }
    }

    public sealed record ProbeSiblingOptions
    {
        public static ProbeSiblingOptions Default { get; } = new();
        public double Step { get; init; }
        public double OutputStepSeconds { get; init; } = 60.0;
    }

    public sealed record ProbeTwinOptions
    {
        public static ProbeTwinOptions Default { get; } = new();
        public double OutputStepSeconds { get; init; } = 30.0;
    }

    public static class ProbeReader
    {
        public static void Read(bool includeA, bool includeB, double searchStep) { }
        public static void Read(bool includeA, double searchStepSeconds, ProbeOptions options = null) { }
        public static bool TryRead(string path, out bool a, out bool b) => a = b = false;
    }

    [Fact]
    public void Violations_ReportTheProbesOfTheReview()
    {
        var violations = Violations([typeof(ProbeOptions), typeof(ProbeSiblingOptions), typeof(ProbeTwinOptions), typeof(ProbeReader)])
            .ToHashSet();
        const string tests = "IO.Astrodynamics.Tests.ApiConventions.OptionsConventionTests";
        Assert.Contains($"D1.values\t{tests}.ProbeOptions.Forces", violations);
        Assert.Contains($"D1.values\t{tests}.ProbeOptions.IntegratorFactory", violations);
        Assert.Contains($"D1.seconds\t{tests}.ProbeOptions.OutputStep", violations);
        Assert.Contains($"D1.seconds\t{tests}.ProbeSiblingOptions.Step", violations);
        Assert.Contains($"D1.bools\t{tests}.ProbeReader.Read(bool, bool, double)", violations);
        Assert.Contains($"D1.seconds\t{tests}.ProbeReader.Read(bool, bool, double):searchStep", violations);
        Assert.Contains($"D1.siblings\t{tests}.ProbeOptions.Step ~ {tests}.ProbeSiblingOptions.Step", violations);
        Assert.Contains($"D1.siblings\t{tests}.ProbeSiblingOptions.OutputStepSeconds ~ {tests}.ProbeTwinOptions.OutputStepSeconds",
            violations);
        // The record's own members, its one-bool overload with its options last and out bools are no violation
        Assert.DoesNotContain(violations, v => v.Contains("Equals") || v.Contains("TryRead") || v.Contains("Deconstruct"));
        Assert.DoesNotContain(violations, v => v.Contains("Read(bool, double, "));
        Assert.DoesNotContain(violations, v => v.StartsWith("D1.default", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.StartsWith("D1.sealed-record", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(ForceBase), true)]
    [InlineData(typeof(IReadOnlyList<ForceBase>), true)]
    [InlineData(typeof(ForceBase[]), true)]
    [InlineData(typeof(Dictionary<string, IReadOnlyList<RK78Integrator>>), true)]
    [InlineData(typeof(Func<Integrator>), true)]
    [InlineData(typeof(IReadOnlyList<TimeSpan>), false)]
    [InlineData(typeof(double?), false)]
    public void HoldsBehaviour_LooksIntoArraysCollectionsAndGenericArguments(Type type, bool holds)
    {
        Assert.Equal(holds, HoldsBehaviour(type));
    }
}
