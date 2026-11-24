using System.Text;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The page of the command-line tool, <c>docs/reference/cli.md</c>, holds the help of the tool and of each of its commands
/// as the tool writes it (R05 of #668, decided by S. Guillet, 2026-11-19: the CLI page generated and compared in CI). Each
/// block follows its marker <c>&lt;!-- generated: astro … --help --&gt;</c>; set <c>ASTRO_CLI_PAGE_UPDATE=1</c> to write
/// them again from the tool.
/// </summary>
public class CliPageTests
{
    private static readonly Regex Marker = new(@"<!-- generated: astro (?<command>[a-z-]* ?)--help -->\n```text\n(?<help>.*?)```\n",
        RegexOptions.Singleline);

    [Fact]
    public void ThePage_HoldsTheHelpOfTheToolAndOfEachCommand()
    {
        using (Configuration.Lock())
        {
            var page = PagePath();
            var text = File.ReadAllText(page).ReplaceLineEndings("\n");
            var expected = Generated(text);
            if (Environment.GetEnvironmentVariable("ASTRO_CLI_PAGE_UPDATE") == "1")
            {
                File.WriteAllText(page, expected);
                text = expected;
            }

            Assert.Equal(expected, text);
        }
    }

    /// <summary>The page with the blocks of the tool's help: one per command of <c>astro --help</c>, in its order.</summary>
    private static string Generated(string page)
    {
        var start = page.IndexOf("<!-- generated: astro --help -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page has no generated section");
        var builder = new StringBuilder(page[..start]);
        var root = Help();
        builder.Append(Block(null, root));
        foreach (var command in Commands(root))
        {
            builder.Append('\n').Append($"### {command}\n\n").Append(Block(command, Help(command)));
        }

        return builder.ToString();
    }

    private static string Block(string? command, string help) =>
        $"<!-- generated: astro {(command == null ? string.Empty : command + " ")}--help -->\n```text\n{help}```\n";

    private static string Help(string? command = null)
    {
        var result = command == null ? CliRun.Run("--help") : CliRun.Run(command, "--help");
        Assert.Equal(0, result.ExitCode);
        return result.Out.ReplaceLineEndings("\n");
    }

    /// <summary>The commands the help of the tool lists.</summary>
    private static IEnumerable<string> Commands(string rootHelp)
    {
        var lines = rootHelp.Split('\n');
        return lines.SkipWhile(x => x != "Commands:").Skip(1).TakeWhile(x => x.StartsWith("  ", StringComparison.Ordinal))
            .Select(x => x.Trim().Split(' ')[0]);
    }

    private static string PagePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "mkdocs.yml")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "docs", "reference", "cli.md");
    }

    [Fact]
    public void EveryCommand_HasItsBlock()
    {
        using (Configuration.Lock())
        {
            var text = File.ReadAllText(PagePath()).ReplaceLineEndings("\n");
            var blocks = Marker.Matches(text).Select(m => m.Groups["command"].Value.Trim()).ToArray();
            Assert.Equal(new[] { string.Empty }.Concat(Commands(Help())), blocks);
        }
    }
}
