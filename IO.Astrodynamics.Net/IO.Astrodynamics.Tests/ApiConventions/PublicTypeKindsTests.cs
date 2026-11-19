// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The kind of each exported type of the library (review of lot L0 of #668): PublicApiAnalyzers 3.3.4 records neither the
/// modifiers of a type (sealed, abstract, static, readonly) nor whether it is a record, whose members the compiler
/// synthesizes (Equals(T), ==, !=, ToString, the copy constructor), so sealing a class or turning a class into a record,
/// which the options lots do, leaves PublicAPI.Unshipped.txt unchanged and the build silent. PublicTypeKinds.txt holds
/// one "kind&lt;TAB&gt;type" per exported type, generated ones included, in ordinal order, and must equal the surface: a
/// pull request that adds, removes or changes the kind of a public type changes its line, and the review reads that
/// diff with the diff of PublicAPI.Unshipped.txt. Unlike the convention baselines it is no list of violations: it follows
/// the surface and is never emptied.
/// </summary>
public class PublicTypeKindsTests
{
    internal const string KindsFile = "PublicTypeKinds.txt";

    /// <summary>A type's kind as C# declares it: its modifiers and its kind of declaration.</summary>
    internal static string Kind(Type type)
    {
        if (type.IsEnum)
        {
            return "enum";
        }

        if (type.IsInterface)
        {
            return "interface";
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return "delegate";
        }

        if (type.IsValueType)
        {
            bool recordStruct = type.GetMethod("PrintMembers", PublicSurface.Declared) != null;
            bool readOnly = type.IsDefined(typeof(IsReadOnlyAttribute), false);
            return (readOnly ? "readonly " : "") + (type.IsByRefLike ? "ref " : "") + (recordStruct ? "record struct" : "struct");
        }

        bool record = type.GetMethod("<Clone>$", PublicSurface.Declared) != null;
        string modifier = type.IsAbstract && type.IsSealed ? "static "
            : type.IsAbstract ? "abstract "
            : type.IsSealed ? "sealed "
            : "";
        return modifier + (record ? "record" : "class");
    }

    /// <summary>The lines of the exported types of the library, "kind&lt;TAB&gt;type", in ordinal order.</summary>
    internal static IReadOnlyList<string> Lines(IEnumerable<Type> types) =>
        types.Select(t => $"{Kind(t)}\t{PublicSurface.Name(t)}{Arity(t)}").Distinct()
            .OrderBy(l => l, StringComparer.Ordinal).ToList();

    // The generic arity of a type, `1 for List<T>, so that a generic and a non-generic type of one name have their lines
    private static string Arity(Type type)
    {
        int count = type.GetGenericArguments().Length - (type.DeclaringType?.GetGenericArguments().Length ?? 0);
        return count > 0 ? $"`{count}" : "";
    }

    [Fact]
    public void PublicTypeKinds_EqualTheFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ApiConventions", KindsFile);
        Assert.True(File.Exists(path), $"no file {path}");
        var file = File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var surface = Lines(PublicSurface.Library.GetExportedTypes());
        var added = surface.Except(file, StringComparer.Ordinal).ToList();
        var gone = file.Except(surface, StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0 && gone.Count == 0,
            $"{KindsFile} differs from the surface; write the lines of the surface and remove the others (ordinal order):\n" +
            $"  add:\n    {string.Join("\n    ", added)}\n  remove:\n    {string.Join("\n    ", gone)}");
        Assert.Equal(file.OrderBy(l => l, StringComparer.Ordinal), file);
    }

    public class ProbeClass;

    public sealed class ProbeSealedClass;

    public abstract class ProbeAbstractClass;

    public static class ProbeStaticClass;

    public sealed record ProbeSealedRecord;

    public record ProbeRecord;

    public readonly struct ProbeReadOnlyStruct;

    public readonly record struct ProbeReadOnlyRecordStruct;

    public delegate void ProbeDelegate();

    // The review's probe: sealing a class or making it a record changes its line, which the analyzer does not see
    [Theory]
    [InlineData(typeof(ProbeClass), "class")]
    [InlineData(typeof(ProbeSealedClass), "sealed class")]
    [InlineData(typeof(ProbeAbstractClass), "abstract class")]
    [InlineData(typeof(ProbeStaticClass), "static class")]
    [InlineData(typeof(ProbeSealedRecord), "sealed record")]
    [InlineData(typeof(ProbeRecord), "record")]
    [InlineData(typeof(ProbeReadOnlyStruct), "readonly struct")]
    [InlineData(typeof(ProbeReadOnlyRecordStruct), "readonly record struct")]
    [InlineData(typeof(ProbeDelegate), "delegate")]
    [InlineData(typeof(IDisposable), "interface")]
    [InlineData(typeof(DayOfWeek), "enum")]
    public void Kind_ReadsTheModifiersTheAnalyzerDoesNotRecord(Type type, string kind)
    {
        Assert.Equal(kind, Kind(type));
    }
}
