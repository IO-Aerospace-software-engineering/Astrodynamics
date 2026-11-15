// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM;

/// <summary>
/// The schema validation of the OPM in the forms of NDM/XML 4.0.0 (third round of #668).
/// </summary>
public class OpmValidatorTests
{
    private readonly OpmValidator _validator = new();

    private static string SatelliteComplete =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CCSDS", "OPM", "satellite_complete.xml"));

    /// <summary>The OPMs of the tests: the writer's, wrapped in an NDM or not, and satellite_complete.xml.</summary>
    private static string Source(string source) => source switch
    {
        "writer, ndm" => OpmReaderTests.FullOpm().ToXmlString(),
        "writer, opm" => OpmReaderTests.FullOpm().ToXmlString(wrapInNdm: false),
        _ => SatelliteComplete
    };

    private static readonly string[] Sources = { "writer, ndm", "writer, opm", "satellite_complete.xml" };

    private static readonly string[] Forms = { "unqualified-ndm", "unqualified-message", "qualified-ndm", "qualified-message" };

    public static TheoryData<string, string> SourcesAndForms()
    {
        var data = new TheoryData<string, string>();
        foreach (var source in Sources)
        {
            foreach (var form in Forms)
            {
                data.Add(source, form);
            }
        }

        return data;
    }

    public static TheoryData<string, string, string, string> SourcesFormsAndMutations()
    {
        // The mutation and a fragment of the error that names its element. The verdicts are those of lxml 6.1.3 against the
        // SANA NDM/XML 4.0.0 schemas (sanaregistry.org/files/ndmxml_unqualified/ for the unqualified forms, the qualified set
        // IO embeds for the qualified ones), which reject each mutation in each form
        var mutations = new (string Mutation, string Names)[]
        {
            ("X units=m", "units"), // positionUnits: km only, ndmxml-4.0.0-common-4.0.xsd
            ("remove EPOCH", "EPOCH"), // required in stateVectorType
            ("ECCENTRICITY=-0.5", "ECCENTRICITY"), // nonNegativeDouble
            ("remove OBJECT_NAME", "OBJECT_NAME"), // required in opmMetadata
            ("FOO", "FOO"), // undeclared
            ("segment twice", "segment"), // opmBody holds one segment
            ("root foo", "foo") // no global declaration
        };
        var data = new TheoryData<string, string, string, string>();
        foreach (var source in Sources)
        {
            foreach (var form in Forms)
            {
                foreach (var (mutation, names) in mutations)
                {
                    data.Add(source, form, mutation, names);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SourcesFormsAndMutations))]
    public void ValidateSchema_StandardFormRejectedBySana_IsInvalid(string source, string form, string mutation, string names)
    {
        var xml = NdmDocuments.Mutated(NdmDocuments.InForm(Source(source), "opm", form), "opm", mutation);

        var result = _validator.ValidateSchemaFromXml(xml);

        // Before: valid, with a warning per element (no declaration matched an unqualified root)
        Assert.False(result.IsValid, $"{source}, {form}, {mutation}: reported valid");
        Assert.Contains(result.Errors, e => e.Message.Contains(names, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SourcesAndForms))]
    public void ValidateSchema_StandardForm_IsValidWithoutWarning(string source, string form)
    {
        var result = _validator.ValidateSchemaFromXml(NdmDocuments.InForm(Source(source), "opm", form));

        // No warning: the schema ran on every element (before: valid, with a warning per element)
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateSchema_WriterOutput_IsValidWithoutWarning(bool wrapInNdm)
    {
        foreach (var opm in new[] { OpmReaderTests.FullOpm(), Opm.CreateMinimal("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC",
                     new OpmStateVector(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0)) })
        {
            var result = _validator.ValidateSchemaFromXml(opm.ToXmlString(wrapInNdm));
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
            Assert.Empty(result.Warnings);

            NdmDocuments.WithTempFile("", file =>
            {
                opm.SaveToFile(file, validateBeforeSave: true, wrapInNdm: wrapInNdm);
                var saved = _validator.ValidateSchema(file);
                Assert.True(saved.IsValid, string.Join("; ", saved.Errors.Select(e => e.Message)));
                Assert.Empty(saved.Warnings);
            });
        }
    }

    [Fact]
    public void ValidateSchema_QualifiedRootOpm_KeepsItsVerdict()
    {
        // IO's form of a bare message, every element in the namespace: valid, its two-segment copy invalid, as before; an
        // <ndm> root in the namespace has no declaration, rejected as before (satellite_complete.xml as it is on disk)
        var minimal = Opm.CreateMinimal("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC",
            new OpmStateVector(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));
        var valid = _validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(minimal.ToXmlString(), "opm"));
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors.Select(e => e.Message)));
        Assert.False(_validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(NdmDocuments.TwoSegmentOpm(minimal), "opm")).IsValid);
        Assert.False(_validator.ValidateSchemaFromXml(SatelliteComplete).IsValid);
    }

    // An <ndm> that the NDM/XML 4.0.0 master accepts but that holds no OPM: empty, or holding only an OMM (lxml 6.1.3 with
    // SANA's unqualified set accepts both). The reader finds no OPM in it, so it is not an OPM document (third round of #668:
    // valid with no issue before, Opm.LoadFromString throwing OpmParseException)
    [Theory]
    [InlineData(null)]
    [InlineData("unqualified-ndm")]
    [InlineData("qualified-ndm")]
    public void ValidateSchema_NdmWithoutOpm_IsInvalid(string? ommForm)
    {
        var xml = ommForm == null ? "<ndm/>" : NdmDocuments.InForm(File.ReadAllText(NdmDocuments.IssOmmPath), "omm", ommForm);
        var parse = Assert.Throws<OpmParseException>(() => new OpmReader().ReadFromString(xml));
        Assert.Equal("Could not find OPM element in document.", parse.Message);

        var result = _validator.ValidateSchemaFromXml(xml);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("OPM006", error.Code);
        Assert.Contains("<opm>", error.Message);
        Assert.Empty(result.Warnings);
        var ex = Assert.Throws<OpmValidationException>(() => Opm.LoadFromString(xml));
        Assert.Equal(error.Message, Assert.Single(ex.ValidationResult.Errors).Message);
        NdmDocuments.WithTempFile(xml, file => Assert.Contains(_validator.ValidateFile(file).Errors, e => e.Message == error.Message));
    }
}
