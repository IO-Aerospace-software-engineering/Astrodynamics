// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Tests.CCSDS.CDM;
using IO.Astrodynamics.Tests.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS;

/// <summary>
/// The one verb set of the CCSDS messages (R22 of #668, lot L7): <c>Load</c>, <c>LoadAll</c>, <c>Parse</c>,
/// <c>ParseAll</c>, <c>TryLoad</c>, <c>TryParse</c>, <c>Save</c>, <c>ToXml</c>, <c>Validate</c> and <c>IsValid</c> on
/// <see cref="Omm"/>, <see cref="Opm"/> and <see cref="Cdm"/>, with <see cref="CcsdsReadOptions"/> and
/// <see cref="CcsdsWriteOptions"/>. The expected messages and texts are those of the internal reader and writer, the former
/// path of the removed verbs.
/// </summary>
public class CcsdsVerbsTests
{
    public CcsdsVerbsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static Opm MinimalOpm() =>
        Opm.CreateMinimal("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC",
            new OpmStateVector(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));

    private static Cdm ManualCdm() =>
        CdmTests.CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 10, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "VERBS"
        });

    // An OMM whose content its validator refuses: an EPOCH the data model accepts but the content check requires
    private static Omm InvalidOmm()
    {
        var elements = MeanElements.CreateWithMeanMotion(default, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0);
        var omm = new Omm(CcsdsHeader.CreateDefault(), OmmMetadata.CreateForSgp4("ISS", "1998-067A"), OmmData.CreateMinimal(elements));
        Assert.False(omm.IsValid());
        return omm;
    }

    // An OPM whose content its validator refuses: a negative variance, which the data model accepts
    private static Opm InvalidOpm()
    {
        var minimal = MinimalOpm();
        var covariance = new CovarianceMatrix(
            cxX: -1.0e-6,
            cyX: 0.0, cyY: 1.0e-6,
            czX: 0.0, czY: 0.0, czZ: 1.0e-6,
            cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
            cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
            czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
        var opm = new Opm(minimal.Header, minimal.Metadata, new OpmData(minimal.Data.StateVector, covariance: covariance));
        Assert.False(opm.IsValid());
        return opm;
    }

    // A CDM whose content its validator refuses: no MESSAGE_ID
    private static Cdm InvalidCdm()
    {
        var cdm = ManualCdm();
        var invalid = new Cdm(cdm.Header with { MessageId = "" }, cdm.RelativeMetadataData, cdm.Segments);
        Assert.False(invalid.IsValid());
        return invalid;
    }

    [Fact]
    public void Options_DefaultsAreEveryCheckAndTheFormerOutput()
    {
        Assert.Equal(new CcsdsReadOptions(), CcsdsReadOptions.Default);
        Assert.True(CcsdsReadOptions.Default.ValidateSchema);
        Assert.True(CcsdsReadOptions.Default.ValidateContent);
        Assert.Equal(new CcsdsWriteOptions(), CcsdsWriteOptions.Default);
        Assert.True(CcsdsWriteOptions.Default.ValidateContent);
        Assert.True(CcsdsWriteOptions.Default.WrapInNdmContainer);
        Assert.True(CcsdsWriteOptions.Default.Indent);
        Assert.Equal(new CdmExportOptions(), CdmExportOptions.Default);
    }

    // The readers, writers and validators are internal: the messages are read and written through their own verbs
    [Theory]
    [InlineData(typeof(OmmReader))]
    [InlineData(typeof(OmmWriter))]
    [InlineData(typeof(OmmValidator))]
    [InlineData(typeof(OpmReader))]
    [InlineData(typeof(OpmWriter))]
    [InlineData(typeof(OpmValidator))]
    [InlineData(typeof(CdmReader))]
    [InlineData(typeof(CdmWriter))]
    [InlineData(typeof(CdmValidator))]
    public void ReadersWritersValidators_AreInternal(Type type)
    {
        Assert.False(type.IsPublic);
    }

    #region OMM

    [Fact]
    public void Omm_LoadAndParse_ReadAsTheReader()
    {
        var expected = new OmmReader().ReadFromFile(NdmDocuments.IssOmmPath);
        var xml = File.ReadAllText(NdmDocuments.IssOmmPath);

        var loaded = Omm.Load(NdmDocuments.IssOmmPath);
        var parsed = Omm.Parse(xml);
        using var reader = new StringReader(xml);
        var fromReader = Omm.Parse(reader);

        var text = new OmmWriter().WriteToString(expected);
        Assert.Equal(text, new OmmWriter().WriteToString(loaded));
        Assert.Equal(text, new OmmWriter().WriteToString(parsed));
        Assert.Equal(text, new OmmWriter().WriteToString(fromReader));
    }

    [Fact]
    public void Omm_LoadAll_ReadsEveryMessage_LoadNamesIt()
    {
        var xml = NdmDocuments.TwoOmm();

        var all = Omm.ParseAll(xml);
        Assert.Equal(["ISS (ZARYA)", "ISS COPY"], all.Select(o => o.ObjectName));
        NdmDocuments.WithTempFile(xml, file =>
        {
            Assert.Equal(["ISS (ZARYA)", "ISS COPY"], Omm.LoadAll(file).Select(o => o.ObjectName));
            var ex = Assert.Throws<OmmParseException>(() => Omm.Load(file));
            Assert.Contains("LoadAll", ex.Message);
            Assert.False(Omm.TryLoad(file, out var none));
            Assert.Null(none);
        });
        Assert.Contains("ParseAll", Assert.Throws<OmmParseException>(() => Omm.Parse(xml)).Message);
        Assert.Single(Omm.LoadAll(NdmDocuments.IssOmmPath));
    }

    [Fact]
    public void Omm_LoadAll_DocumentWithoutOmm_Throws()
    {
        var ex = Assert.Throws<OmmParseException>(() => Omm.ParseAll("<ndm/>", CcsdsReadOptions.Default with { ValidateSchema = false }));
        Assert.Equal("Could not find OMM element in document.", ex.Message);
        Assert.Throws<OmmValidationException>(() => Omm.ParseAll("<ndm/>"));
    }

    [Fact]
    public void Omm_Try_FalseWithoutException()
    {
        Assert.True(Omm.TryLoad(NdmDocuments.IssOmmPath, out var loaded));
        Assert.Equal("ISS (ZARYA)", loaded!.ObjectName);
        Assert.True(Omm.TryParse(File.ReadAllText(NdmDocuments.IssOmmPath), out var parsed));
        Assert.NotNull(parsed);

        Assert.False(Omm.TryLoad("no/such/file.omm", out var missing));
        Assert.Null(missing);
        Assert.False(Omm.TryLoad(null!, out _));
        Assert.False(Omm.TryParse(null!, out _));
        Assert.False(Omm.TryParse("", out _));
        Assert.False(Omm.TryParse("<omm", out _));
        Assert.False(Omm.TryParse(OPM.OpmReaderTests.FullOpm().ToXml(), out _));
    }

    [Fact]
    public void Omm_Load_MissingFileOrNull_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => Omm.Load("no/such/file.omm"));
        Assert.Throws<ArgumentNullException>(() => Omm.Load(null!));
        Assert.Throws<ArgumentNullException>(() => Omm.Parse((string)null!));
        Assert.Throws<ArgumentNullException>(() => Omm.Parse((TextReader)null!));
        Assert.Throws<OmmParseException>(() => Omm.Parse("  "));
    }

    [Fact]
    public void Omm_SaveAndToXml_WriteAsTheWriter()
    {
        var omm = Omm.Load(NdmDocuments.IssOmmPath);

        Assert.Equal(new OmmWriter().WriteToString(omm), omm.ToXml());
        var bare = CcsdsWriteOptions.Default with { WrapInNdmContainer = false, Indent = false };
        Assert.Equal(new OmmWriter { WrapInNdmContainer = false, IndentOutput = false }.WriteToString(omm), omm.ToXml(bare));

        var path = Path.Combine(Path.GetTempPath(), $"verbs_{Guid.NewGuid():N}.omm");
        try
        {
            omm.Save(path);
            var expected = Path.ChangeExtension(path, ".expected");
            new OmmWriter().WriteToFile(omm, expected);
            Assert.Equal(File.ReadAllBytes(expected), File.ReadAllBytes(path));
            File.Delete(expected);
        }
        finally
        {
            File.Delete(path);
        }

        using var stream = new MemoryStream();
        omm.Save(stream, bare);
        using var expectedStream = new MemoryStream();
        new OmmWriter { WrapInNdmContainer = false, IndentOutput = false }.WriteToStream(omm, expectedStream);
        Assert.Equal(expectedStream.ToArray(), stream.ToArray());
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void Omm_Write_ValidatesTheContentByDefault()
    {
        var omm = InvalidOmm();

        Assert.Throws<OmmValidationException>(() => omm.ToXml());
        Assert.Throws<OmmValidationException>(() => omm.Save(new MemoryStream()));
        var unchecked_ = CcsdsWriteOptions.Default with { ValidateContent = false };
        Assert.Equal(new OmmWriter().WriteToString(omm), omm.ToXml(unchecked_));
    }

    [Fact]
    public void Omm_Read_ContentCheckCanBeTurnedOff()
    {
        var xml = new OmmWriter().WriteToString(InvalidOmm());
        Assert.Throws<OmmValidationException>(() => Omm.Parse(xml, CcsdsReadOptions.Default with { ValidateSchema = false }));
        var read = Omm.Parse(xml, new CcsdsReadOptions { ValidateSchema = false, ValidateContent = false });
        Assert.False(read.IsValid());
    }

    // The validator switches of the read (annex R22 and IC-18 of #668): null runs the check, false turns it off
    [Fact]
    public void Omm_Read_ValidatorSwitches()
    {
        Assert.Null(CcsdsReadOptions.Default.ValidatePhysicalConstraints);
        Assert.Null(CcsdsReadOptions.Default.ValidateTleRequirements);
        Assert.Null(CcsdsReadOptions.Default.WarnOnMissingOptionalFields);

        // TLE parameters without NORAD_CAT_ID: refused by the TLE requirements alone
        var omm = Omm.CreateForTle("ISS", "1998-067A", new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), 15.5, 0.0001,
            51.6, 120.0, 90.0, 45.0, 1.0e-4, 0.0, 0.0);
        var xml = new OmmWriter().WriteToString(omm);
        var refused = Assert.Throws<OmmValidationException>(() => Omm.Parse(xml));
        Assert.Contains(refused.ValidationResult.Errors, e => e.Path == "Data.TleParameters.NoradCatalogId");
        var read = Omm.Parse(xml, new CcsdsReadOptions { ValidateTleRequirements = false });
        Assert.Equal(xml, new OmmWriter().WriteToString(read));

        // The result an exception carries is that of the validator with the same switches (the former public path)
        var invalidXml = new OmmWriter().WriteToString(InvalidOmm());
        var options = new CcsdsReadOptions
        {
            ValidateSchema = false, ValidatePhysicalConstraints = false, ValidateTleRequirements = false,
            WarnOnMissingOptionalFields = false
        };
        var expected = new OmmValidator
        {
            ValidatePhysicalConstraints = false, ValidateTleRequirements = false, WarnOnMissingOptionalFields = false
        }.Validate(Omm.Parse(invalidXml, options with { ValidateContent = false }));
        var actual = Assert.Throws<OmmValidationException>(() => Omm.Parse(invalidXml, options)).ValidationResult;
        Assert.Equal(expected.Issues.Select(i => (i.Code, i.Path, i.Severity)), actual.Issues.Select(i => (i.Code, i.Path, i.Severity)));
        var byDefault = Assert.Throws<OmmValidationException>(() => Omm.Parse(invalidXml, CcsdsReadOptions.Default with { ValidateSchema = false }))
            .ValidationResult;
        var expectedByDefault = new OmmValidator().Validate(Omm.Parse(invalidXml, options with { ValidateContent = false }));
        Assert.Equal(expectedByDefault.Issues.Select(i => (i.Code, i.Path, i.Severity)), byDefault.Issues.Select(i => (i.Code, i.Path, i.Severity)));
    }

    // A switch turned off on a message that has no such check throws (D1.2 of #668)
    [Fact]
    public void Read_SwitchOfACheckTheTypeHasNot_Throws()
    {
        var opmXml = new OpmWriter().WriteToString(MinimalOpm());
        var noTle = new CcsdsReadOptions { ValidateTleRequirements = false };
        Assert.Equal("options", Assert.Throws<ArgumentException>(() => Opm.Parse(opmXml, noTle)).ParamName);
        Assert.NotNull(Opm.Parse(opmXml, new CcsdsReadOptions { ValidatePhysicalConstraints = false, WarnOnMissingOptionalFields = false }));
        Assert.NotNull(Opm.Parse(opmXml, new CcsdsReadOptions { ValidateTleRequirements = true }));

        var cdmXml = ManualCdm().ToXml();
        foreach (var options in new[]
                 {
                     new CcsdsReadOptions { ValidatePhysicalConstraints = false }, new CcsdsReadOptions { ValidateTleRequirements = false },
                     new CcsdsReadOptions { WarnOnMissingOptionalFields = false }
                 })
        {
            Assert.Equal("options", Assert.Throws<ArgumentException>(() => Cdm.Parse(cdmXml, options)).ParamName);
        }

        Assert.NotNull(Cdm.Parse(cdmXml, new CcsdsReadOptions { ValidatePhysicalConstraints = true }));
    }

    // Save names its own parameter for a blank path
    [Fact]
    public void Save_BlankPath_NamesPath()
    {
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => Omm.Load(NdmDocuments.IssOmmPath).Save(" ")).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => MinimalOpm().Save("")).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => ManualCdm().Save(" ")).ParamName);
    }

    #endregion

    #region OPM

    [Fact]
    public void Opm_Verbs_ReadAndWriteAsTheReaderAndWriter()
    {
        var opm = OpmReaderTests.FullOpm();
        var xml = new OpmWriter().WriteToString(opm);
        Assert.Equal(xml, opm.ToXml());
        Assert.Equal(new OpmWriter { WrapInNdmContainer = false }.WriteToString(opm),
            opm.ToXml(CcsdsWriteOptions.Default with { WrapInNdmContainer = false }));

        var parsed = Opm.Parse(xml);
        Assert.Equal(xml, parsed.ToXml());
        Assert.True(parsed.IsValid());
        NdmDocuments.WithTempFile(xml, file =>
        {
            Assert.Equal(xml, Opm.Load(file).ToXml());
            Assert.True(Opm.TryLoad(file, out var loaded));
            Assert.Equal(xml, loaded!.ToXml());
        });

        var second = Opm.CreateMinimal("SECOND SAT", "2024-002A", "EARTH", "ICRF", "UTC", MinimalOpm().Data.StateVector);
        var two = NdmDocuments.TwoOpm(opm, second);
        Assert.Equal([opm.ObjectName, "SECOND SAT"], Opm.ParseAll(two).Select(o => o.ObjectName));
        using (var reader = new StringReader(two))
            Assert.Equal(2, Opm.ParseAll(reader).Count);
        Assert.Contains("LoadAll", Assert.Throws<OpmParseException>(() => Opm.Parse(two)).Message);
        Assert.False(Opm.TryParse(two, out _));
    }

    [Fact]
    public void Opm_Write_ValidatesTheContentByDefault()
    {
        var opm = InvalidOpm();

        Assert.Throws<OpmValidationException>(() => opm.ToXml());
        var path = Path.Combine(Path.GetTempPath(), $"verbs_{Guid.NewGuid():N}.opm");
        Assert.Throws<OpmValidationException>(() => opm.Save(path));
        Assert.False(File.Exists(path));
        var xml = opm.ToXml(CcsdsWriteOptions.Default with { ValidateContent = false });
        Assert.Throws<OpmValidationException>(() => Opm.Parse(xml));
        Assert.False(Opm.Parse(xml, CcsdsReadOptions.Default with { ValidateContent = false }).IsValid());
    }

    #endregion

    #region CDM

    [Fact]
    public void Cdm_Verbs_ReadAndWriteAsTheReaderAndWriter()
    {
        var cdm = ManualCdm();
        var xml = new CdmWriter().WriteToString(cdm);
        Assert.Equal(xml, cdm.ToXml());
        Assert.Equal(new CdmWriter { IndentOutput = false }.WriteToString(cdm, false),
            cdm.ToXml(new CcsdsWriteOptions { WrapInNdmContainer = false, Indent = false }));

        var parsed = Cdm.Parse(xml);
        Assert.Equal(xml, parsed.ToXml());
        using var stream = new MemoryStream();
        cdm.Save(stream);
        Assert.Equal(xml, Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿'));

        var path = Path.Combine(Path.GetTempPath(), $"verbs_{Guid.NewGuid():N}.cdm");
        try
        {
            cdm.Save(path);
            Assert.Equal(xml, Cdm.Load(path).ToXml());
            Assert.True(Cdm.TryLoad(path, out var loaded));
            Assert.Equal(xml, loaded!.ToXml());
            Assert.Single(Cdm.LoadAll(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Cdm_LoadAll_ReadsEveryMessage()
    {
        var unqualified = RemoveNamespaces(ManualCdm().ToXml(CcsdsWriteOptions.Default with { WrapInNdmContainer = false }));
        var two = NdmDocuments.TwoCdm(unqualified);

        Assert.Equal(["VERBS", "SECOND"], Cdm.ParseAll(two).Select(c => c.Header.MessageId));
        Assert.Contains("LoadAll", Assert.Throws<CdmParseException>(() => Cdm.Parse(two)).Message);
    }

    // Before 11.0.0 the CDM readers ran no check: Load and Parse now check the schema and the content by default
    [Fact]
    public void Cdm_Read_ChecksTheSchemaAndTheContentByDefault()
    {
        var xml = ManualCdm().ToXml();
        var mutated = NdmDocuments.WithElementValue(xml, "MANEUVERABLE", "MAYBE");
        // The schema refuses the value before the reader
        var schema = Assert.Throws<CdmValidationException>(() => Cdm.Parse(mutated));
        Assert.False(schema.ValidationResult.IsValid);
        Assert.IsAssignableFrom<CcsdsFormatException>(schema);
        Assert.Throws<CdmParseException>(() => Cdm.Parse(mutated, CcsdsReadOptions.Default with { ValidateSchema = false }));
        Assert.False(Cdm.TryParse(mutated, out _));

        var invalid = InvalidCdm().ToXml(CcsdsWriteOptions.Default with { ValidateContent = false });
        var content = Assert.Throws<CdmValidationException>(() => Cdm.Parse(invalid, CcsdsReadOptions.Default with { ValidateSchema = false }));
        Assert.Contains(content.ValidationResult.Errors, e => e.Path == "Header.MessageId");
    }

    // Before 11.0.0 the CDM writers did not validate
    [Fact]
    public void Cdm_Write_ValidatesTheContentByDefault()
    {
        var cdm = InvalidCdm();

        Assert.Throws<CdmValidationException>(() => cdm.ToXml());
        Assert.Throws<CdmValidationException>(() => cdm.Save(new MemoryStream()));
        Assert.Equal(new CdmWriter().WriteToString(cdm), cdm.ToXml(CcsdsWriteOptions.Default with { ValidateContent = false }));
    }

    private static string RemoveNamespaces(string xml)
    {
        var root = XDocument.Parse(xml).Root!;
        foreach (var element in root.DescendantsAndSelf())
        {
            element.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.Namespace == XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance")).Remove();
            element.Name = element.Name.LocalName;
        }

        return root.ToString();
    }

    #endregion
}
