// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Text;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmReaderTests
    {
        private readonly OpmReader _reader = new();

        public OpmReaderTests()
        {
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void ReadFromString_ValidMinimalOpm_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
            Assert.Equal("2024-001A", opm.ObjectId);
            Assert.Equal(6800, opm.Data.StateVector.X);
        }

        [Fact]
        public void ReadFromString_NdmWrapped_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<ndm>
    <opm id=""CCSDS_OPM_VERS"" version=""3.0"">
        <header>
            <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
            <ORIGINATOR>TestOrg</ORIGINATOR>
        </header>
        <body>
            <segment>
                <metadata>
                    <OBJECT_NAME>TestSat</OBJECT_NAME>
                    <OBJECT_ID>2024-001A</OBJECT_ID>
                    <CENTER_NAME>EARTH</CENTER_NAME>
                    <REF_FRAME>ICRF</REF_FRAME>
                    <TIME_SYSTEM>UTC</TIME_SYSTEM>
                </metadata>
                <data>
                    <stateVector>
                        <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                        <X>6800</X>
                        <Y>0</Y>
                        <Z>0</Z>
                        <X_DOT>0</X_DOT>
                        <Y_DOT>7.5</Y_DOT>
                        <Z_DOT>0</Z_DOT>
                    </stateVector>
                </data>
            </segment>
        </body>
    </opm>
</ndm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
        }

        [Fact]
        public void ReadFromString_NullXml_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(null));
        }

        [Fact]
        public void ReadFromString_EmptyXml_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(""));
        }

        [Fact]
        public void ReadFromString_InvalidXml_ThrowsOpmParseException()
        {
            // Act & Assert
            Assert.Throws<OpmParseException>(() =>
                _reader.ReadFromString("not valid xml"));
        }

        [Fact]
        public void ReadFromFile_ValidFile_ReturnsOpm()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";
                File.WriteAllText(tempFile, xml);

                // Act
                var opm = _reader.ReadFromFile(tempFile);

                // Assert
                Assert.Equal("TestSat", opm.ObjectName);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void ReadFromFile_NullFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromFile(null));
        }

        [Fact]
        public void ReadFromFile_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                _reader.ReadFromFile("/nonexistent/file.opm"));
        }

        [Fact]
        public void ReadFromStream_ValidStream_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            // Act
            var opm = _reader.ReadFromStream(stream);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
        }

        [Fact]
        public void ReadFromStream_NullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _reader.ReadFromStream(null));
        }

        [Fact]
        public void ReadFromString_WithComments_ParsesComments()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <COMMENT>Header comment</COMMENT>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <COMMENT>Metadata comment</COMMENT>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <COMMENT>StateVector comment</COMMENT>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Contains("Header comment", opm.Header.Comments);
            Assert.Contains("Metadata comment", opm.Metadata.Comments);
            Assert.Contains("StateVector comment", opm.Data.StateVector.Comments);
        }

        [Fact]
        public void ReadFromString_MissingRequiredElement_ThrowsOpmParseException()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <!-- OBJECT_ID is missing -->
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act & Assert
            Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml));
        }
    }
}
