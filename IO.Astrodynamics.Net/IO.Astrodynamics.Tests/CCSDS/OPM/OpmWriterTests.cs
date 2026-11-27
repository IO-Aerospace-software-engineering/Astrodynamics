// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Tests.CCSDS.Common;
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

        private static readonly string[] CovarianceKeywords =
        {
            "CX_X", "CY_X", "CY_Y", "CZ_X", "CZ_Y", "CZ_Z", "CX_DOT_X", "CX_DOT_Y", "CX_DOT_Z", "CX_DOT_X_DOT", "CY_DOT_X",
            "CY_DOT_Y", "CY_DOT_Z", "CY_DOT_X_DOT", "CY_DOT_Y_DOT", "CZ_DOT_X", "CZ_DOT_Y", "CZ_DOT_Z", "CZ_DOT_X_DOT",
            "CZ_DOT_Y_DOT", "CZ_DOT_Z_DOT"
        };

        // Every real value an OPM writes, each with 17 significant digits below 1000 in magnitude, reads back from the text
        // bit for bit (#728: the writer kept 14 decimals of a fixed-point value, up to 4.9e-11 relative lost below 1e-3,
        // measured over 1e5 values in [1e-4, 1e3) on a85098d; 43 of these 44 values lost)
        [Fact]
        public void WriteToString_EveryRealValue_ReadsBackBitForBit()
        {
            double V(int index, double low, double high) => CcsdsRoundTripValues.Value(index, low, high);
            var expected = new Dictionary<string, double>
            {
                ["X"] = V(0, -999.0, 999.0), ["Y"] = V(1, -1.0, 1.0), ["Z"] = V(2, -1.0e-3, 1.0e-3),
                ["X_DOT"] = V(3, -8.0, 8.0), ["Y_DOT"] = V(4, -1.0e-2, 1.0e-2), ["Z_DOT"] = V(5, 1.0e-4, 1.0e-3),
                ["SEMI_MAJOR_AXIS"] = V(6, 100.0, 999.0), ["ECCENTRICITY"] = V(7, 1.0e-4, 1.0e-3),
                ["INCLINATION"] = V(8, 0.0, 180.0), ["RA_OF_ASC_NODE"] = V(9, 0.0, 360.0),
                ["ARG_OF_PERICENTER"] = V(10, 0.0, 360.0), ["TRUE_ANOMALY"] = V(11, 0.0, 1.0), ["GM"] = V(12, 300.0, 400.0),
                ["MASS"] = V(13, 100.0, 999.0), ["SOLAR_RAD_AREA"] = V(14, 1.0, 10.0), ["SOLAR_RAD_COEFF"] = V(15, 1.0, 2.0),
                ["DRAG_AREA"] = V(16, 1.0, 10.0), ["DRAG_COEFF"] = V(17, 2.0, 3.0), ["MAN_DURATION"] = V(18, 0.0, 100.0),
                ["MAN_DELTA_MASS"] = V(19, -1.0, 0.0), ["MAN_DV_1"] = V(20, 1.0e-4, 1.0e-3), ["MAN_DV_2"] = V(21, -1.0e-3, 0.0),
                ["MAN_DV_3"] = V(22, 0.0, 1.0e-2)
            };
            for (int i = 0; i < CovarianceKeywords.Length; i++)
            {
                expected[CovarianceKeywords[i]] = V(23 + i, 1.0e-4, 1.0e-3);
            }

            double E(string keyword) => expected[keyword];
            var covariance = new CovarianceMatrix(
                E("CX_X"), E("CY_X"), E("CY_Y"), E("CZ_X"), E("CZ_Y"), E("CZ_Z"), E("CX_DOT_X"), E("CX_DOT_Y"), E("CX_DOT_Z"),
                E("CX_DOT_X_DOT"), E("CY_DOT_X"), E("CY_DOT_Y"), E("CY_DOT_Z"), E("CY_DOT_X_DOT"), E("CY_DOT_Y_DOT"),
                E("CZ_DOT_X"), E("CZ_DOT_Y"), E("CZ_DOT_Z"), E("CZ_DOT_X_DOT"), E("CZ_DOT_Y_DOT"), E("CZ_DOT_Z_DOT"));
            var data = new OpmData(
                new OpmStateVector(Clock, E("X"), E("Y"), E("Z"), E("X_DOT"), E("Y_DOT"), E("Z_DOT")),
                OpmKeplerianElements.CreateWithTrueAnomaly(E("SEMI_MAJOR_AXIS"), E("ECCENTRICITY"), E("INCLINATION"),
                    E("RA_OF_ASC_NODE"), E("ARG_OF_PERICENTER"), E("TRUE_ANOMALY"), E("GM")),
                E("MASS"), E("SOLAR_RAD_AREA"), E("SOLAR_RAD_COEFF"), E("DRAG_AREA"), E("DRAG_COEFF"), covariance,
                new[] { new OpmManeuverParameters(Clock, E("MAN_DURATION"), E("MAN_DELTA_MASS"), "RSW", E("MAN_DV_1"), E("MAN_DV_2"), E("MAN_DV_3")) });
            var opm = new Opm(new CcsdsHeader(Clock, "IO.Astrodynamics.Tests"),
                new OpmMetadata("SAT", "2010-001A", "EARTH", "ICRF", "UTC"), data);

            var xml = new OpmWriter().WriteToString(opm);

            CcsdsRoundTripValues.AssertWrittenExactly(xml, expected, 43);
            var read = new OpmReader().ReadFromString(xml).Data;
            Assert.Equal(BitConverter.DoubleToInt64Bits(E("Z")), BitConverter.DoubleToInt64Bits(read.StateVector.Z));
            Assert.Equal(BitConverter.DoubleToInt64Bits(E("ECCENTRICITY")), BitConverter.DoubleToInt64Bits(read.KeplerianElements.Eccentricity));
            Assert.Equal(BitConverter.DoubleToInt64Bits(E("CZ_DOT_Z_DOT")), BitConverter.DoubleToInt64Bits(read.Covariance.CzDotZDot));
            Assert.Equal(BitConverter.DoubleToInt64Bits(E("MAN_DV_1")), BitConverter.DoubleToInt64Bits(read.Maneuvers[0].ManDv1));
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
