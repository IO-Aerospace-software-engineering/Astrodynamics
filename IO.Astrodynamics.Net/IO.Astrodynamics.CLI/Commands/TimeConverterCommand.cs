using System;
using System.Globalization;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Time;

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
        [Option('d', Description = "Convert to DateTime (ISO 8601)")]
        bool toDateTime)
    {
        if (!(toUTC ^ toTDB ^ toLocal))
        {
            throw new ArgumentException("Target either UTC or TDB or Local. use --help for more information");
        }

        if (!(toJulian ^ toSecondsFromJ2000 ^ toDateTime))
        {
            throw new ArgumentException("Target either Julian or SecondsFromJ2000 or DateTime . use --help for more information");
        }

        var input = Helpers.ConvertDateTimeInput(epochParameters.Epoch);

        //Output
        string suffix = toLocal ? "" : toUTC ? "UTC" : "TDB";
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
            input = input.ToUTC().ToLocalTime();
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
                res = $"{input.SecondsFromJ2000UTC()}";
            }
            else if (toTDB)
            {
                res = $"{input.SecondsFromJ2000TDB()}";
            }
            else
            {
                res = $"{input.SecondsFromJ2000Local()}";
            }
        }
        else if (toDateTime)
        {
            res = input.ToString("O", CultureInfo.InvariantCulture);
        }

        Console.WriteLine($"{res} {suffix}");
        return Task.CompletedTask;
    }
}