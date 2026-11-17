// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmWriterTests
    {
        private static readonly DateTime Clock = new(2010, 3, 13, 22, 37, 52, 618, DateTimeKind.Unspecified);

        // An OPM whose EPOCH, REF_FRAME_EPOCH and MAN_EPOCH_IGNITION are the given dates, under the given TIME_SYSTEM
        private static Opm Create(string timeSystem, DateTime epoch, DateTime referenceFrameEpoch, DateTime ignition, DateTime creation)
        {
            var maneuver = OpmManeuverParameters.CreateImpulsive(ignition, new Vector3(0.001, 0.0, 0.0));
            var data = new OpmData(new OpmStateVector(epoch, 6678.0, 0.0, 0.0, 0.0, 7.7, 0.0), maneuvers: new[] { maneuver });
            return new Opm(new CcsdsHeader(creation, "IO.Astrodynamics.Tests"),
                new OpmMetadata("SAT", "2010-001A", "EARTH", "ICRF", timeSystem, referenceFrameEpoch), data);
        }

        private static string Text(string xml, string keyword)
        {
            return XDocument.Parse(xml).Descendants().Single(e => e.Name.LocalName == keyword).Value;
        }

        private static string Odm(DateTime value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        }

        // The EPOCH, REF_FRAME_EPOCH and MAN_EPOCH_IGNITION of an OPM are in TIME_SYSTEM (CCSDS 502.0-B-3): a local time
        // has no reading in TDB, TT, TAI, GPS or a time system the library does not know, which the writer could give
        // without a conversion of time scales, so it is refused (#617, decided by S. Guillet, 2026-11-17; before, its clock
        // reading was written as a reading in TIME_SYSTEM, whatever the zone)
        [Theory]
        [InlineData("TDB", "EPOCH")]
        [InlineData("TT", "EPOCH")]
        [InlineData("TAI", "REF_FRAME_EPOCH")]
        [InlineData("GPS", "MAN_EPOCH_IGNITION")]
        [InlineData("MET", "EPOCH")]
        public void WriteToString_LocalDateUnderAnotherTimeSystemThanUtc_ThrowsArgumentException(string timeSystem, string keyword)
        {
            var local = DateTime.SpecifyKind(Clock, DateTimeKind.Local);
            var opm = Create(timeSystem,
                keyword == "EPOCH" ? local : Clock,
                keyword == "REF_FRAME_EPOCH" ? local : Clock,
                keyword == "MAN_EPOCH_IGNITION" ? local : Clock,
                DateTime.UtcNow);

            var exception = Assert.Throws<ArgumentException>(() => new OpmWriter().WriteToString(opm));
            Assert.Equal("opm", exception.ParamName);
            Assert.Contains(keyword, exception.Message);
            Assert.Contains($"TIME_SYSTEM = {timeSystem}", exception.Message);
        }

        // Under TIME_SYSTEM = UTC a local time is written as its UTC (.NET's meaning of Local); a date of kind Utc or
        // Unspecified is the clock reading in TIME_SYSTEM, written as given under any time system; CREATION_DATE is UTC
        // (CCSDS 502.0-B-3, Table 3-1), a local one converted whatever TIME_SYSTEM
        [Theory]
        [InlineData("UTC", DateTimeKind.Local)]
        [InlineData("UTC", DateTimeKind.Utc)]
        [InlineData("UTC", DateTimeKind.Unspecified)]
        [InlineData("TDB", DateTimeKind.Utc)]
        [InlineData("TDB", DateTimeKind.Unspecified)]
        [InlineData("TT", DateTimeKind.Unspecified)]
        public void WriteToString_DateOfEachKind_WrittenAsTheReadingInTimeSystem(string timeSystem, DateTimeKind kind)
        {
            var given = DateTime.SpecifyKind(Clock, kind);
            var creation = DateTime.SpecifyKind(Clock, DateTimeKind.Local);
            var xml = new OpmWriter().WriteToString(Create(timeSystem, given, given, given, creation));

            var expected = Odm(kind == DateTimeKind.Local ? given.ToUniversalTime() : given);
            Assert.Equal(expected, Text(xml, "EPOCH"));
            Assert.Equal(expected, Text(xml, "REF_FRAME_EPOCH"));
            Assert.Equal(expected, Text(xml, "MAN_EPOCH_IGNITION"));
            Assert.Equal(Odm(creation.ToUniversalTime()), Text(xml, "CREATION_DATE"));
        }
    }
}
