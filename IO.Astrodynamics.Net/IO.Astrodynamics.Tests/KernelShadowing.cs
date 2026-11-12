// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// What a kernel defines that another kernel loaded after it would shadow, read from the files without SPICE: the variables
/// of a text kernel (between <c>\begindata</c> and <c>\begintext</c>, NAIF, Kernel Required Reading, "Text Kernel
/// Specifications"), the body names and codes, frame names and ids and frame associations it defines, the targets of an
/// SPK and the frame classes of a binary PCK (their DAF summaries, DAF Required Reading, "The File Record" and "Summary
/// Records").
/// </summary>
internal sealed class KernelContent
{
    private static readonly Regex FrameById = new(@"^(?:FRAME|TKFRAME)_(-?\d+)_", RegexOptions.CultureInvariant);
    private static readonly Regex FrameByName = new(@"^FRAME_(?!-?\d+_)(.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex FrameName = new(@"^FRAME_-?\d+_NAME$", RegexOptions.CultureInvariant);
    private static readonly Regex BodyVariable = new(@"^BODY(-?\d+)_", RegexOptions.CultureInvariant);
    private static readonly Regex ObjectFrame = new(@"^OBJECT_(.+)_FRAME$", RegexOptions.CultureInvariant);

    internal const string BodyNames = "NAIF_BODY_NAME";
    internal const string BodyCodes = "NAIF_BODY_CODE";

    /// <summary>The values of each variable a text kernel assigns, with <c>=</c> or <c>+=</c>, strings without quotes.</summary>
    internal Dictionary<string, List<string>> Variables { get; } = new(StringComparer.Ordinal);

    internal HashSet<int> SpkTargets { get; } = new();

    internal HashSet<int> PckFrames { get; } = new();

    /// <summary>The meta-kernels, whose kernels are not read.</summary>
    internal List<string> MetaKernels { get; } = new();

    /// <summary>The body names of <c>NAIF_BODY_NAME</c>, as SPICE compares them: upper case, blanks compressed.</summary>
    internal IEnumerable<string> Names => Values(BodyNames).Select(NormalizeName);

    /// <summary>The codes of <c>NAIF_BODY_CODE</c>, of the <c>BODYnnn_</c> variables and the SPK targets.</summary>
    internal IEnumerable<int> Codes => Values(BodyCodes).Select(ParseInt).OfType<int>()
        .Concat(Variables.Keys.Select(k => BodyVariable.Match(k)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)))
        .Concat(SpkTargets);

    /// <summary>The frame names, from <c>FRAME_id_NAME</c> and <c>FRAME_name = id</c>, upper case.</summary>
    internal IEnumerable<string> FrameNames => Variables.Where(v => FrameName.IsMatch(v.Key)).SelectMany(v => v.Value)
        .Concat(Variables.Where(v => IsFrameIdAssignment(v)).Select(v => FrameByName.Match(v.Key).Groups[1].Value))
        .Select(NormalizeName);

    /// <summary>The frame ids, from <c>FRAME_id_</c>, <c>TKFRAME_id_</c> and <c>FRAME_name = id</c>.</summary>
    internal IEnumerable<int> FrameIds => Variables.Keys.Select(k => FrameById.Match(k)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
        .Concat(Variables.Where(v => IsFrameIdAssignment(v)).Select(v => ParseInt(v.Value[0]).Value));

    /// <summary>The objects of <c>OBJECT_object_FRAME</c>, upper case.</summary>
    internal IEnumerable<string> FrameAssociations =>
        Variables.Keys.Select(k => ObjectFrame.Match(k)).Where(m => m.Success).Select(m => NormalizeName(m.Groups[1].Value));

    internal IEnumerable<string> Values(string variable) => Variables.TryGetValue(variable, out var values) ? values : [];

    private static bool IsFrameIdAssignment(KeyValuePair<string, List<string>> variable) =>
        FrameByName.IsMatch(variable.Key) && variable.Value.Count == 1 && ParseInt(variable.Value[0]).HasValue;

    internal static int? ParseInt(string value) =>
        double.TryParse(value.Replace('D', 'E').Replace('d', 'e'), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) && number == System.Math.Floor(number) &&
        System.Math.Abs(number) < int.MaxValue
            ? (int)number
            : null;

    internal static string NormalizeName(string name) => Regex.Replace(name.Trim(), @"\s+", " ").ToUpperInvariant();

    /// <summary>The content of the kernel files <see cref="SpiceAPI.LoadKernels"/> hands to SPICE for each path.</summary>
    internal static KernelContent Read(IEnumerable<FileSystemInfo> paths)
    {
        var content = new KernelContent();
        foreach (var file in paths.SelectMany(SpiceAPI.KernelFiles))
        {
            content.ReadFile(file);
        }

        return content;
    }

    /// <summary>The content of one kernel file.</summary>
    internal static KernelContent ReadKernel(string file)
    {
        var content = new KernelContent();
        content.ReadFile(file);
        return content;
    }

    private void ReadFile(string file)
    {
        var head = new byte[8];
        int read;
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            read = stream.ReadAtLeast(head, head.Length, false);
        }

        string idWord = Encoding.ASCII.GetString(head, 0, read);
        if (idWord.StartsWith("DAF/", StringComparison.Ordinal) || idWord.StartsWith("NAIF/DAF", StringComparison.Ordinal))
        {
            ReadDaf(file, idWord);
        }
        else if (!idWord.StartsWith("DAS/", StringComparison.Ordinal) && !idWord.StartsWith("NAIF/DAS", StringComparison.Ordinal))
        {
            // A DAS (DSK, EK) defines nothing that Data/SolarSystem defines
            ReadText(file, idWord.StartsWith("KPL/MK", StringComparison.Ordinal));
        }
    }

    private void ReadDaf(string file, string idWord)
    {
        const int recordLength = 1024;
        var record = new byte[recordLength];
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.ReadExactly(record);
        bool bigEndian = Encoding.ASCII.GetString(record, 88, 8) == "BIG-IEEE";
        int Int(int offset) => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(offset)) : BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(offset));
        double Double(int offset) => BitConverter.Int64BitsToDouble(bigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(record.AsSpan(offset))
            : BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(offset)));

        int nd = Int(8);
        int ni = Int(12);
        HashSet<int> ids = idWord.StartsWith("DAF/SPK", StringComparison.Ordinal) || (idWord.StartsWith("NAIF/DAF", StringComparison.Ordinal) && nd == 2 && ni == 6)
            ? SpkTargets
            : idWord.StartsWith("DAF/PCK", StringComparison.Ordinal) || (idWord.StartsWith("NAIF/DAF", StringComparison.Ordinal) && nd == 2 && ni == 5)
                ? PckFrames
                : null;
        if (ids == null)
        {
            // A CK: Data/SolarSystem holds none
            return;
        }

        int summarySize = nd + (ni + 1) / 2;
        int next = Int(76);
        for (int records = 0; next > 0; records++)
        {
            if (records > 100000) throw new InvalidDataException($"{file}: a cycle of summary records.");
            stream.Seek((long)(next - 1) * recordLength, SeekOrigin.Begin);
            stream.ReadExactly(record);
            next = (int)Double(0);
            int count = (int)Double(16);
            for (int i = 0; i < count; i++)
            {
                // The first integer of a summary: the target of an SPK, the frame class of a binary PCK
                ids.Add(Int(24 + i * summarySize * 8 + nd * 8));
            }
        }
    }

    private void ReadText(string file, bool metaKernel)
    {
        if (metaKernel)
        {
            MetaKernels.Add(file);
        }

        var data = new StringBuilder();
        bool inData = false;
        foreach (var line in File.ReadLines(file))
        {
            string marker = line.Trim();
            if (marker == "\\begindata")
            {
                inData = true;
            }
            else if (marker == "\\begintext")
            {
                inData = false;
            }
            else if (inData)
            {
                data.Append(line).Append('\n');
            }
        }

        var tokens = Tokenize(data.ToString()).ToArray();
        for (int i = 0; i < tokens.Length;)
        {
            if (tokens[i].Kind != TokenKind.Word || i + 1 >= tokens.Length || tokens[i + 1].Kind != TokenKind.Assignment)
            {
                throw new InvalidDataException($"{file}: no assignment at token {i} ({tokens[i].Text}).");
            }

            string name = tokens[i].Text;
            if (tokens[i + 1].Text == "=" || !Variables.ContainsKey(name))
            {
                Variables[name] = new List<string>();
            }

            var values = Variables[name];
            i += 2;
            if (i < tokens.Length && tokens[i].Kind == TokenKind.Open)
            {
                for (i++; i < tokens.Length && tokens[i].Kind != TokenKind.Close; i++)
                {
                    values.Add(tokens[i].Text);
                }

                i++;
            }
            else if (i < tokens.Length)
            {
                values.Add(tokens[i++].Text);
            }

            if (name == "KERNELS_TO_LOAD")
            {
                MetaKernels.Add(file);
            }
        }
    }

    private enum TokenKind
    {
        Word,
        String,
        Assignment,
        Open,
        Close
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private static IEnumerable<Token> Tokenize(string data)
    {
        int i = 0;
        while (i < data.Length)
        {
            char c = data[i];
            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
            }
            else if (c == '(')
            {
                i++;
                yield return new Token(TokenKind.Open, "(");
            }
            else if (c == ')')
            {
                i++;
                yield return new Token(TokenKind.Close, ")");
            }
            else if (c == '=')
            {
                i++;
                yield return new Token(TokenKind.Assignment, "=");
            }
            else if (c == '+' && i + 1 < data.Length && data[i + 1] == '=')
            {
                i += 2;
                yield return new Token(TokenKind.Assignment, "+=");
            }
            else if (c == '\'')
            {
                // A quote inside a string is doubled
                var text = new StringBuilder();
                for (i++; i < data.Length; i++)
                {
                    if (data[i] == '\'')
                    {
                        if (i + 1 < data.Length && data[i + 1] == '\'')
                        {
                            text.Append('\'');
                            i++;
                            continue;
                        }

                        i++;
                        break;
                    }

                    text.Append(data[i]);
                }

                yield return new Token(TokenKind.String, text.ToString());
            }
            else
            {
                int start = i;
                while (i < data.Length && !char.IsWhiteSpace(data[i]) && data[i] != ',' && data[i] != '(' && data[i] != ')' && data[i] != '=' &&
                       data[i] != '\'' && !(data[i] == '+' && i + 1 < data.Length && data[i + 1] == '='))
                {
                    i++;
                }

                yield return new Token(TokenKind.Word, data[start..i]);
            }
        }
    }
}

/// <summary>
/// The kernels that would shadow <c>Data/SolarSystem</c> loaded after it, which load-once (<see cref="TestKernels"/>)
/// keeps above it for the rest of the process: a kernel that assigns a variable it assigns (the cumulative body names and
/// codes aside), a variable of one of its bodies or of an object it associates a frame with, a body name or code it
/// defines or covers, a frame name or id it defines, an SPK target or a binary PCK frame class of its kernels.
/// </summary>
internal static class KernelShadowing
{
    private static readonly Lazy<KernelContent> SolarSystemContent = new(() => KernelContent.Read([Constants.SolarSystemKernelPath]));

    /// <summary>What <c>Data/SolarSystem</c> defines.</summary>
    internal static KernelContent SolarSystem => SolarSystemContent.Value;

    /// <summary>What each kernel file of <paramref name="paths"/> shadows of <paramref name="reference"/>, one line per conflict.</summary>
    internal static IReadOnlyList<string> Find(IEnumerable<FileSystemInfo> paths, KernelContent reference)
    {
        var names = reference.Names.ToHashSet();
        var codes = reference.Codes.ToHashSet();
        var frameNames = reference.FrameNames.ToHashSet();
        var frameIds = reference.FrameIds.ToHashSet();
        var associations = reference.FrameAssociations.ToHashSet();
        var conflicts = new List<string>();
        foreach (var file in paths.SelectMany(SpiceAPI.KernelFiles).Distinct())
        {
            var content = KernelContent.ReadKernel(file);
            void Add(string what) => conflicts.Add($"{file}: {what}");
            if (content.MetaKernels.Count > 0)
            {
                Add("a meta-kernel, whose kernels are not checked");
            }

            foreach (var variable in content.Variables.Keys)
            {
                var body = Regex.Match(variable, @"^BODY(-?\d+)_");
                var association = Regex.Match(variable, @"^OBJECT_(.+)_FRAME$");
                if (variable != KernelContent.BodyNames && variable != KernelContent.BodyCodes && reference.Variables.ContainsKey(variable))
                {
                    Add($"assigns {variable}, which Data/SolarSystem assigns");
                }
                else if (body.Success && codes.Contains(int.Parse(body.Groups[1].Value)))
                {
                    Add($"assigns {variable}, a variable of body {body.Groups[1].Value} of Data/SolarSystem");
                }
                else if (association.Success && (associations.Contains(KernelContent.NormalizeName(association.Groups[1].Value)) ||
                                                 (int.TryParse(association.Groups[1].Value, out int code) && codes.Contains(code))))
                {
                    Add($"assigns {variable}, the frame of an object of Data/SolarSystem");
                }
            }

            conflicts.AddRange(content.Names.Where(names.Contains).Distinct().Select(n => $"{file}: names a body {n}, a name of Data/SolarSystem"));
            conflicts.AddRange(content.Values(KernelContent.BodyCodes).Select(KernelContent.ParseInt).OfType<int>()
                .Where(codes.Contains).Distinct().Select(c => $"{file}: names the body code {c}, a code of Data/SolarSystem"));
            conflicts.AddRange(content.FrameNames.Where(frameNames.Contains).Distinct().Select(n => $"{file}: defines the frame {n}, a frame of Data/SolarSystem"));
            conflicts.AddRange(content.FrameIds.Where(frameIds.Contains).Distinct().Select(i => $"{file}: defines the frame id {i}, a frame of Data/SolarSystem"));
            conflicts.AddRange(content.SpkTargets.Where(reference.SpkTargets.Contains).Select(t => $"{file}: an SPK of target {t}, an object of Data/SolarSystem"));
            conflicts.AddRange(content.PckFrames.Where(reference.PckFrames.Contains).Select(f => $"{file}: a binary PCK of frame class {f}, a frame of Data/SolarSystem"));
        }

        return conflicts;
    }
}
