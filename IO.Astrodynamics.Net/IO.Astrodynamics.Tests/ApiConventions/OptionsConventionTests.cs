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
/// D1's options rule (#668, design PR #671) over the public surface, read by reflection (<see cref="PublicSurface"/>):
/// every public type named *Options is one sealed record per operation, of values set at init, with a static Default
/// equal to new(), taken last as <c>options = null</c>. Today's exceptions are the allow-list OptionsConventionBaseline.txt,
/// which may only shrink: a new exception fails, and so does an entry that no longer is one. The options lots (L4, L6,
/// L7) empty it.
/// <list type="bullet">
/// <item>D1.sealed-record: the type is a sealed record (rule 1; the closed abstract IntegratorOptions of rule 5 excepted,
/// a record whose constructors are not visible outside the library).</item>
/// <item>D1.init-only: no public property has a public setter other than init (rules 1 and 3).</item>
/// <item>D1.default: a public static get-only property Default of the type itself, equal to a new instance (rule 1,
/// sub-choice 2).</item>
/// <item>D1.values: no property holds an integrator, a force or a delegate (rule 5).</item>
/// <item>D1.parameter: a parameter of an options type is named options, optional with a null default, and last, before
/// out parameters and a CancellationToken only (rule 1, sub-choice 5).</item>
/// </list>
/// </summary>
public class OptionsConventionTests
{
    internal const string BaselineFile = "OptionsConventionBaseline.txt";

    [Fact]
    public void OptionsTypes_FollowD1_OrAreInTheBaseline()
    {
        Assert.NotEmpty(OptionsTypes());
        PublicSurface.AssertMatchesBaseline(BaselineFile, Violations());
    }

    // The public options types: an exported type whose name ends with Options
    internal static IReadOnlyList<Type> OptionsTypes() =>
        PublicSurface.Types.Where(t => PublicSurface.SimpleName(t).EndsWith("Options", StringComparison.Ordinal) && !t.IsEnum)
            .ToList();

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$", PublicSurface.Declared) != null;

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    // The closed hierarchy of rule 5: an abstract record no caller can derive from
    private static bool IsClosedAbstractRecord(Type type) =>
        type.IsAbstract && IsRecord(type) &&
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(c => !(c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly));

    private static bool HoldsBehaviour(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return typeof(IIntegrator).IsAssignableFrom(t) || typeof(ForceBase).IsAssignableFrom(t) || typeof(Delegate).IsAssignableFrom(t);
    }

    /// <summary>Every violation of D1 by the options types and their parameters, as "rule&lt;TAB&gt;symbol".</summary>
    internal static IEnumerable<string> Violations()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var options = OptionsTypes();
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

        foreach (var type in PublicSurface.Types)
        {
            foreach (var method in PublicSurface.MethodsAndConstructors(type))
            {
                if (method is MethodInfo { IsSpecialName: true } || options.Contains(method.DeclaringType) &&
                    (method is ConstructorInfo || method.Name is "Deconstruct" or "Equals"))
                {
                    continue;
                }

                var parameters = method.GetParameters();
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

                    var owner = method is ConstructorInfo ? PublicSurface.Name(type) + ".ctor" : PublicSurface.Key(method);
                    if (i != last || p.Name != "options" || !p.IsOptional || p.DefaultValue != null)
                    {
                        found.Add($"D1.parameter\t{owner}({p.Name})");
                    }
                }
            }
        }

        return found;
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
}
