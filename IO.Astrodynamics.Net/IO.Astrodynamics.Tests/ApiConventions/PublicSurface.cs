// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The public surface of the library read by reflection, for the convention tests of the ergonomics review (#668, lot
/// L0): the exported types, and their public members and the protected members of the types a caller can derive from,
/// as the PublicAPI files of the library list them. The types the xsd tool generated (the PDS schemas, marked
/// <see cref="GeneratedCodeAttribute"/>) and the members the compiler synthesizes (a record's <c>EqualityContract</c>,
/// <c>PrintMembers</c>, <c>&lt;Clone&gt;$</c>) are outside the rules, as for the CA rules. Reflection reads metadata only:
/// no type initializer runs.
/// </summary>
internal static class PublicSurface
{
    internal const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    internal static Assembly Library => typeof(Configuration).Assembly;

    /// <summary>The exported types of the library, the generated ones aside.</summary>
    internal static IReadOnlyList<Type> Types { get; } = Library.GetExportedTypes()
        .Where(t => !IsGenerated(t))
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .ToArray();

    private static bool IsGenerated(Type type)
    {
        for (var t = type; t != null; t = t.DeclaringType)
        {
            if (t.IsDefined(typeof(GeneratedCodeAttribute), false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A type's name as C# writes it: nested types with a dot, no generic arity.</summary>
    internal static string Name(Type type)
    {
        var name = type.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        return type.DeclaringType == null ? $"{type.Namespace}.{name}" : $"{Name(type.DeclaringType)}.{name}";
    }

    /// <summary>A type's simple name, without its generic arity.</summary>
    internal static string SimpleName(Type type)
    {
        int tick = type.Name.IndexOf('`');
        return tick >= 0 ? type.Name[..tick] : type.Name;
    }

    /// <summary>Whether a member is part of the surface: public, or protected on a type a caller can derive from.</summary>
    internal static bool IsVisible(MemberInfo member)
    {
        if (member.IsDefined(typeof(CompilerGeneratedAttribute), false) || member.Name.Contains('<'))
        {
            return false;
        }

        var type = member.DeclaringType!;
        bool derivable = !type.IsSealed && !type.IsInterface && !type.IsValueType &&
                         type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Any(c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly);
        return member switch
        {
            FieldInfo f => f.IsPublic || derivable && (f.IsFamily || f.IsFamilyOrAssembly),
            MethodBase m => m.IsPublic || derivable && (m.IsFamily || m.IsFamilyOrAssembly),
            // The accessors of an auto-property are compiler-generated: their visibility alone counts
            PropertyInfo p => p.GetAccessors(true).Any(a => a.IsPublic || derivable && (a.IsFamily || a.IsFamilyOrAssembly)),
            EventInfo e => e.AddMethod is { } add && (add.IsPublic || derivable && (add.IsFamily || add.IsFamilyOrAssembly)),
            _ => false
        };
    }

    /// <summary>The members of a type that are part of the surface, its nested types aside.</summary>
    internal static IEnumerable<MemberInfo> Members(Type type) =>
        type.GetMembers(Declared).Where(m => m is not Type && IsVisible(m));

    /// <summary>
    /// The visible methods and constructors of a type, the accessors and operators included, whose parameters the
    /// rules read.
    /// </summary>
    internal static IEnumerable<MethodBase> MethodsAndConstructors(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
            .Where(m => !m.Name.Contains('<') && !m.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                        (m.IsPublic || IsVisible(m)));

    /// <summary>A member's key, "Namespace.Type.Member", the same for its overloads.</summary>
    internal static string Key(MemberInfo member) => $"{Name(member.DeclaringType!)}.{member.Name}";

    /// <summary>
    /// The entries of a baseline file copied next to the tests: one "rule&lt;TAB&gt;symbol" per line, '#' comments and blank
    /// lines aside.
    /// </summary>
    internal static SortedSet<string> ReadBaseline(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ApiConventions", fileName);
        Assert.True(File.Exists(path), $"no baseline {path}");
        return new SortedSet<string>(File.ReadAllLines(path)
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0 && !l.StartsWith('#')), StringComparer.Ordinal);
    }

    /// <summary>
    /// Compares the violations found with a baseline that may only shrink: a violation that is not in the baseline fails,
    /// and so does an entry of the baseline that is no longer a violation (remove it), each listed as the line to add or
    /// remove.
    /// </summary>
    internal static void AssertMatchesBaseline(string fileName, IEnumerable<string> violations)
    {
        var found = new SortedSet<string>(violations, StringComparer.Ordinal);
        var baseline = ReadBaseline(fileName);
        var added = found.Where(v => !baseline.Contains(v)).ToList();
        var stale = baseline.Where(b => !found.Contains(b)).ToList();
        var message = "";
        if (added.Count > 0)
        {
            message += $"{added.Count} new violation(s), not in {fileName}; fix the name (the baseline only shrinks):\n  " +
                       string.Join("\n  ", added) + "\n";
        }

        if (stale.Count > 0)
        {
            message += $"{stale.Count} entr(y/ies) of {fileName} no longer violate the rule; remove them:\n  " +
                       string.Join("\n  ", stale) + "\n";
        }

        Assert.True(message.Length == 0, message);
    }
}
