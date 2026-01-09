// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Xml.Schema;
using IO.Astrodynamics.CCSDS;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS
{
    public class CcsdsSchemaLoaderTests
    {
        [Fact]
        public void GetSchemaStream_CommonSchema_ReturnsStream()
        {
            using var stream = CcsdsSchemaLoader.GetSchemaStream(CcsdsSchemaLoader.SchemaFiles.Common);

            Assert.NotNull(stream);
            Assert.True(stream.CanRead);
            Assert.True(stream.Length > 0);
        }

        [Fact]
        public void GetSchemaStream_OmmSchema_ReturnsStream()
        {
            using var stream = CcsdsSchemaLoader.GetSchemaStream(CcsdsSchemaLoader.SchemaFiles.Omm);

            Assert.NotNull(stream);
            Assert.True(stream.CanRead);
            Assert.True(stream.Length > 0);
        }

        [Fact]
        public void GetSchemaStream_NonExistentSchema_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CcsdsSchemaLoader.GetSchemaStream("nonexistent-schema.xsd"));
        }

        [Fact]
        public void GetSchemaContent_CommonSchema_ReturnsValidXml()
        {
            var content = CcsdsSchemaLoader.GetSchemaContent(CcsdsSchemaLoader.SchemaFiles.Common);

            Assert.NotNull(content);
            Assert.NotEmpty(content);
            Assert.Contains("xsd:schema", content);
            Assert.Contains("urn:ccsds:schema:ndmxml", content);
        }

        [Fact]
        public void GetSchemaContent_OmmSchema_ReturnsValidXml()
        {
            var content = CcsdsSchemaLoader.GetSchemaContent(CcsdsSchemaLoader.SchemaFiles.Omm);

            Assert.NotNull(content);
            Assert.NotEmpty(content);
            Assert.Contains("xsd:schema", content);
            Assert.Contains("omm", content.ToLowerInvariant());
        }

        [Fact]
        public void GetOmmSchemaSet_ReturnsCompiledSchemaSet()
        {
            var schemaSet = CcsdsSchemaLoader.GetOmmSchemaSet();

            Assert.NotNull(schemaSet);
            Assert.True(schemaSet.IsCompiled);
            Assert.True(schemaSet.Count > 0);
        }

        [Fact]
        public void GetOmmSchemaSet_CalledMultipleTimes_ReturnsSameInstance()
        {
            var schemaSet1 = CcsdsSchemaLoader.GetOmmSchemaSet();
            var schemaSet2 = CcsdsSchemaLoader.GetOmmSchemaSet();

            Assert.Same(schemaSet1, schemaSet2);
        }

        [Fact]
        public void GetCommonSchemaSet_ReturnsCompiledSchemaSet()
        {
            var schemaSet = CcsdsSchemaLoader.GetCommonSchemaSet();

            Assert.NotNull(schemaSet);
            Assert.True(schemaSet.IsCompiled);
            Assert.True(schemaSet.Count > 0);
        }

        [Fact]
        public void GetCommonSchemaSet_CalledMultipleTimes_ReturnsSameInstance()
        {
            var schemaSet1 = CcsdsSchemaLoader.GetCommonSchemaSet();
            var schemaSet2 = CcsdsSchemaLoader.GetCommonSchemaSet();

            Assert.Same(schemaSet1, schemaSet2);
        }

        [Fact]
        public void SchemaFiles_ContainsExpectedValues()
        {
            Assert.Equal("ndmxml-4.0.0-common-4.0.xsd", CcsdsSchemaLoader.SchemaFiles.Common);
            Assert.Equal("ndmxml-4.0.0-omm-3.0.xsd", CcsdsSchemaLoader.SchemaFiles.Omm);
            Assert.Equal("ndmxml-4.0.0-opm-3.0.xsd", CcsdsSchemaLoader.SchemaFiles.Opm);
            Assert.Equal("ndmxml-4.0.0-oem-3.0.xsd", CcsdsSchemaLoader.SchemaFiles.Oem);
            Assert.Equal("ndmxml-4.0.0-cdm-1.0.xsd", CcsdsSchemaLoader.SchemaFiles.Cdm);
        }
    }
}
