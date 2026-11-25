// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO.Astrodynamics.Tests.Body;

namespace IO.Astrodynamics.Tests.Culture;

/// <summary>
/// The child process of <c>KernelWriterCultureTests</c> (#821, #798): the library's kernel writers in a named culture. The
/// repository runs every project with invariant globalization (<c>Directory.Build.props</c>), in which a named culture
/// formats and upper-cases as the invariant one; this project runs outside it, so the culture given is the one of the
/// thread, as it is in an application that does not set invariant globalization.
/// </summary>
/// <remarks>
/// <para>
/// Arguments: the kernel directory, the mode, the culture (a name such as <c>sv-SE</c>), the output directory, then the
/// arguments of the mode.
/// </para>
/// <para>
/// The mode <c>text-kernels</c> takes the NAIF id and the name of a spacecraft, the names of its three instruments, the id
/// and the name of a site: it writes the kernels of <see cref="CultureKernelSet"/> into the output directory, loads each,
/// and reads what SPICE then knows. Its lines on the standard output: <c>CULTURE</c>, the culture and the code points of
/// the text of −1 and of "i" upper-cased in it, so that the test checks that the child runs in the culture; <c>LOAD
/// &lt;file&gt; OK</c> or the type of the exception its load threw; <c>FRAME &lt;id&gt; &lt;name&gt;</c>, the frame the
/// loaded kernels define for the frame id of the spacecraft, of each instrument and of the site (<c>-</c> for none);
/// <c>FOV &lt;id&gt; &lt;shape&gt;</c>, the shape <c>getfov_c</c> reads for each instrument from the loaded instrument
/// kernels, or its error, <c>SKIPPED</c> on Windows, whose native library does not export CSPICE's routines;
/// <c>SITE-FRAME-NAME</c>, the name of the frame of the site.
/// </para>
/// <para>
/// The mode <c>export</c> takes the same arguments: it simulates the spacecraft and the site and exports the scenario to
/// Cosmographia into the output directory (<see cref="CultureKernelSet.ExportAsync"/>), then writes <c>EXPORT OK</c>.
/// </para>
/// <para>
/// Exit codes: 0 done; 1 an exception (on the standard error); 2 wrong arguments.
/// </para>
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Arguments: kernels mode culture output [mode arguments]");
            return 2;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(args[2]);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine($"CULTURE {culture.Name} MINUS {CodePoints((-1).ToString())} UPPER-I {CodePoints("i".ToUpper())}");

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(args[0]));
            var output = Directory.CreateDirectory(args[3]);
            switch (args[1])
            {
                case "text-kernels" when args.Length == 11:
                    TextKernels(output, int.Parse(args[4], CultureInfo.InvariantCulture), args[5], [args[6], args[7], args[8]],
                        int.Parse(args[9], CultureInfo.InvariantCulture), args[10]);
                    return 0;
                case "export" when args.Length == 11:
                    var (spacecraft, site) = CultureKernelSet.Build(int.Parse(args[4], CultureInfo.InvariantCulture), args[5],
                        [args[6], args[7], args[8]], int.Parse(args[9], CultureInfo.InvariantCulture), args[10]);
                    await CultureKernelSet.ExportAsync(spacecraft, site, output);
                    Console.WriteLine("EXPORT OK");
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown mode or wrong arguments: {string.Join(' ', args.Skip(1))}");
                    return 2;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    // Writes the set, loads each kernel and reports what SPICE knows of it
    private static void TextKernels(DirectoryInfo output, int spacecraftId, string spacecraftName, string[] instrumentNames,
        int siteId, string siteName)
    {
        var (spacecraft, site) = CultureKernelSet.Build(spacecraftId, spacecraftName, instrumentNames, siteId, siteName);
        CultureKernelSet.Write(spacecraft, site, output);
        var loaded = new System.Collections.Generic.List<FileInfo>();
        try
        {
            foreach (var name in CultureKernelSet.Files)
            {
                var file = new FileInfo(Path.Combine(output.FullName, name));
                try
                {
                    SpiceAPI.Instance.LoadKernels(file);
                    loaded.Add(file);
                    Console.WriteLine($"LOAD {name} OK");
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"LOAD {name} {exception.GetType().Name}");
                }
            }

            var frameIds = new[] { spacecraft.Frame.Id!.Value }.Concat(spacecraft.Instruments.Select(x => x.NaifId))
                .Append(site.Frame.Id!.Value);
            foreach (var id in frameIds)
            {
                Console.WriteLine($"FRAME {id.ToString(CultureInfo.InvariantCulture)} {SpiceAPI.KernelFrameNameOf(id) ?? "-"}");
            }

            Console.WriteLine($"SITE-FRAME-NAME {site.Frame.Name}");
            foreach (var instrument in spacecraft.Instruments)
            {
                string fov = OperatingSystem.IsWindows() ? "SKIPPED" : Fov(instrument.NaifId);
                Console.WriteLine($"FOV {instrument.NaifId.ToString(CultureInfo.InvariantCulture)} {fov}");
            }
        }
        finally
        {
            foreach (var file in loaded)
            {
                SpiceAPI.Instance.UnloadKernels(file);
            }
        }
    }

    private static string Fov(int instrumentId)
    {
        var (error, shape, _, _) = CspiceDirect.Getfov(instrumentId);
        return error ?? shape;
    }

    // The code points of a text, U+XXXX each, separated by '+'
    private static string CodePoints(string text) =>
        string.Join("+", text.EnumerateRunes().Select(r => "U+" + r.Value.ToString("X4", CultureInfo.InvariantCulture)));
}
