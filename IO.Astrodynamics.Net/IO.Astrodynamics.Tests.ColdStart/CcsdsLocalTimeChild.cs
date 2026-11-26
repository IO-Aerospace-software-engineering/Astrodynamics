// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.Ccsds;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>ccsds-local-time</c> of the child process (#617): the CCSDS writers in the local time zone the parent gives
/// the child through <c>TZ</c> (Linux and macOS; Windows ignores it and the child runs in the machine's zone), which the
/// test process cannot change for itself without changing it for every test that runs beside.
/// </summary>
/// <remarks>
/// <para>
/// The argument after the mode is the path of a CDM the parent wrote. The child prints one line per value, a key and the
/// text written, read back from the written XML by local name:
/// <list type="bullet">
/// <item><c>OFFSET</c>: the zone's id and its offset from UTC, in seconds, at the clock reading <see cref="Clock"/>.</item>
/// <item><c>CDM-UNSPECIFIED-&lt;KEYWORD&gt;</c>: the eight dates of the CDM (<c>CREATION_DATE</c>, <c>TCA</c>, the screen
/// period, the screen entry and exit, the last observations of object 1), each given as the clock reading
/// <see cref="Clock"/> with <see cref="DateTimeKind.Unspecified"/>.</item>
/// <item><c>CDM-LOCAL-TCA</c>: the TCA given as <see cref="Clock"/> with <see cref="DateTimeKind.Local"/>.</item>
/// <item><c>OPM-UTC-LOCAL-&lt;KEYWORD&gt;</c> and <c>OMM-UTC-LOCAL-&lt;KEYWORD&gt;</c>: <c>CREATION_DATE</c>, <c>EPOCH</c>
/// (and the OPM's <c>MAN_EPOCH_IGNITION</c>) given as <see cref="Clock"/> with <see cref="DateTimeKind.Local"/>, under
/// <c>TIME_SYSTEM = UTC</c>.</item>
/// <item><c>OPM-TDB-UNSPECIFIED-EPOCH</c> and <c>OMM-TDB-UNSPECIFIED-EPOCH</c>: <c>EPOCH</c> given as <see cref="Clock"/> with
/// <see cref="DateTimeKind.Unspecified"/> under <c>TIME_SYSTEM = TDB</c>.</item>
/// <item><c>OPM-TDB-LOCAL</c> and <c>OMM-TDB-LOCAL</c>: the type of the exception the writer throws for an <c>EPOCH</c> of
/// kind <see cref="DateTimeKind.Local"/> under <c>TIME_SYSTEM = TDB</c>, and the parameter it names, or <c>written</c>
/// and the text written.</item>
/// </list>
/// Exit 0 once every line is printed, 1 with the exception on the standard error.
/// </para>
/// </remarks>
internal static class CcsdsLocalTimeChild
{
    /// <summary>
    /// The clock reading of every date: the TCA of the XML example of CCSDS 508.0-B-1 Cor. 2, 4.4.
    /// </summary>
    internal static readonly DateTime Clock = new(2010, 3, 13, 22, 37, 52, 618, DateTimeKind.Unspecified);

    internal static int Run(string cdmPath)
    {
        try
        {
            Console.WriteLine($"OFFSET {TimeZoneInfo.Local.Id} " +
                              TimeZoneInfo.Local.GetUtcOffset(Clock).TotalSeconds.ToString(CultureInfo.InvariantCulture));
            WriteCdm(cdmPath);
            WriteOpm();
            WriteOmm();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void WriteCdm(string cdmPath)
    {
        var cdm = Cdm.Load(cdmPath);
        var unspecified = DateTime.SpecifyKind(Clock, DateTimeKind.Unspecified);
        var segments = cdm.Segments.Select((segment, index) => index == 0
            ? segment with
            {
                Data = segment.Data! with
                {
                    OdParameters = new CdmOdParameters
                    {
                        TimeLastObservationStartUtc = unspecified,
                        TimeLastObservationEndUtc = unspecified
                    }
                }
            }
            : segment).ToList();
        var relative = cdm.RelativeMetadataData with
        {
            TcaUtc = unspecified,
            StartScreenPeriodUtc = unspecified,
            StopScreenPeriodUtc = unspecified,
            ScreenEntryTimeUtc = unspecified,
            ScreenExitTimeUtc = unspecified
        };
        var written = XDocument.Parse(new Cdm(cdm.Header with { CreationDateUtc = unspecified }, relative, segments).ToXml());
        foreach (var keyword in new[]
                 {
                     "CREATION_DATE", "TCA", "START_SCREEN_PERIOD", "STOP_SCREEN_PERIOD", "SCREEN_ENTRY_TIME",
                     "SCREEN_EXIT_TIME", "TIME_LASTOB_START", "TIME_LASTOB_END"
                 })
        {
            Console.WriteLine($"CDM-UNSPECIFIED-{keyword} {Text(written, keyword)}");
        }

        var local = new Cdm(cdm.Header, cdm.RelativeMetadataData with { TcaUtc = DateTime.SpecifyKind(Clock, DateTimeKind.Local) },
            cdm.Segments);
        Console.WriteLine($"CDM-LOCAL-TCA {Text(XDocument.Parse(local.ToXml()), "TCA")}");
    }

    private static void WriteOpm()
    {
        var local = DateTime.SpecifyKind(Clock, DateTimeKind.Local);
        var maneuver = OpmManeuverParameters.CreateImpulsive(local, new IO.Astrodynamics.Math.Vector3(0.001, 0.0, 0.0));
        var data = new OpmData(new OpmStateVector(local, 6678.0, 0.0, 0.0, 0.0, 7.7, 0.0), maneuvers: new[] { maneuver });
        var utc = new Opm(new CcsdsHeader(local, "IO.Astrodynamics.Tests"),
            new OpmMetadata("SAT", "2010-001A", "EARTH", "ICRF", "UTC"), data);
        var written = XDocument.Parse(new OpmWriter().WriteToString(utc));
        foreach (var keyword in new[] { "CREATION_DATE", "EPOCH", "MAN_EPOCH_IGNITION" })
        {
            Console.WriteLine($"OPM-UTC-LOCAL-{keyword} {Text(written, keyword)}");
        }

        var unspecified = Opm.CreateMinimal("SAT", "2010-001A", "EARTH", "ICRF", "TDB",
            new OpmStateVector(Clock, 6678.0, 0.0, 0.0, 0.0, 7.7, 0.0));
        Console.WriteLine($"OPM-TDB-UNSPECIFIED-EPOCH {Text(XDocument.Parse(new OpmWriter().WriteToString(unspecified)), "EPOCH")}");

        var tdbLocal = Opm.CreateMinimal("SAT", "2010-001A", "EARTH", "ICRF", "TDB",
            new OpmStateVector(local, 6678.0, 0.0, 0.0, 0.0, 7.7, 0.0));
        Console.WriteLine($"OPM-TDB-LOCAL {Outcome(() => new OpmWriter().WriteToString(tdbLocal))}");
    }

    private static void WriteOmm()
    {
        var local = DateTime.SpecifyKind(Clock, DateTimeKind.Local);
        var elements = MeanElements.CreateWithMeanMotion(local, 15.5, 0.0005, 51.6, 10.0, 20.0, 30.0);
        var utc = new Omm(new CcsdsHeader(local, "IO.Astrodynamics.Tests"),
            new OmmMetadata("SAT", "2010-001A", "EARTH", "TEME", "UTC", "SGP4"), OmmData.CreateMinimal(elements));
        var written = XDocument.Parse(new OmmWriter().WriteToString(utc));
        foreach (var keyword in new[] { "CREATION_DATE", "EPOCH" })
        {
            Console.WriteLine($"OMM-UTC-LOCAL-{keyword} {Text(written, keyword)}");
        }

        var unspecified = Omm.CreateMinimal("SAT", "2010-001A", "EARTH", "TEME", "TDB", "SGP4",
            MeanElements.CreateWithMeanMotion(Clock, 15.5, 0.0005, 51.6, 10.0, 20.0, 30.0));
        Console.WriteLine($"OMM-TDB-UNSPECIFIED-EPOCH {Text(XDocument.Parse(new OmmWriter().WriteToString(unspecified)), "EPOCH")}");

        var tdbLocal = Omm.CreateMinimal("SAT", "2010-001A", "EARTH", "TEME", "TDB", "SGP4", elements);
        Console.WriteLine($"OMM-TDB-LOCAL {Outcome(() => new OmmWriter().WriteToString(tdbLocal))}");
    }

    private static string Text(XDocument document, string keyword)
    {
        return document.Descendants().Single(e => e.Name.LocalName == keyword).Value;
    }

    private static string Outcome(Func<string> write)
    {
        try
        {
            return $"written {Text(XDocument.Parse(write()), "EPOCH")}";
        }
        catch (ArgumentException exception)
        {
            return $"{exception.GetType().Name} {exception.ParamName}";
        }
    }
}
