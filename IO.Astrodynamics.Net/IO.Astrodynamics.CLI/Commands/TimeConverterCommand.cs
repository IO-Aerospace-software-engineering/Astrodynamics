using System;
using System.Globalization;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Commands;

public class TimeConverterCommand
{
    public TimeConverterCommand()
    {
    }

    [Command("time-converter", Description = "Convert a time system to another")]
    public Task TimeConverter(
        EpochParameters epochParameters,
        [Option('t', Description = "Convert to TDB")]
        bool toTDB,
        [Option('u', Description = "Convert to UTC")]
        bool toUTC,
        [Option('l', Description = "Convert to Local")]
        bool toLocal,
        [Option('j', Description = "Convert to Julian date")]
        bool toJulian,
        [Option('e', Description = "Convert to elapsed seconds from J2000 epoch")]
        bool toSecondsFromJ2000,
        [Option('d', Description = "Convert to Time (ISO 8601)")]
        bool toDateTime)
    {
        if (!(toUTC ^ toTDB ^ toLocal))
        {
            throw new ArgumentException("Target either UTC or TDB or Local. use --help for more information");
        }

        if (!(toJulian ^ toSecondsFromJ2000 ^ toDateTime))
        {
            throw new ArgumentException("Target either Julian or SecondsFromJ2000 or Time . use --help for more information");
        }

        var input = Helpers.ConvertDateTimeInput(epochParameters.Epoch);

        //Output
        if (toTDB)
        {
            input = input.ToTDB();
        }
        else if (toUTC)
        {
            input = input.ToUTC();
        }
        else
        {
            input = input.ToLocal();
        }

        string res = string.Empty;
        if (toJulian)
        {
            res = $"{input.ToJulianDate()} JD";
        }
        else if (toSecondsFromJ2000)
        {
            if (toUTC)
            {
                res = $"{input.TimeSpanFromJ2000().TotalSeconds}";
            }
            else if (toTDB)
            {
                res = $"{input.TimeSpanFromJ2000().TotalSeconds}";
            }
            else
            {
                res = $"{input.TimeSpanFromJ2000().TotalSeconds}";
            }
        }
        else if (toDateTime)
        {
            res = input.ToString();
        }

        Console.WriteLine($"{res}");
        return Task.CompletedTask;
    }
}