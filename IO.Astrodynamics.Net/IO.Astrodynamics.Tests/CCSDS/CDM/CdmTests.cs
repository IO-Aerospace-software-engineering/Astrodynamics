#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.CDM;

public class CdmTests
{
    public CdmTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void ToCdm_ManualEncounter_ExportsExpectedValues()
    {
        var encounter = CreateManualEncounterCase();
        var options = new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageFor = "CDM-Test-Desk",
            MessageId = "CDM-UNIT-001"
        };

        var cdm = encounter.ToCdm(options);

        var contentValidation = cdm.Validate();
        Assert.True(contentValidation.IsValid, FormatValidationIssues(contentValidation.Issues));

        Assert.Equal(options.CreationDateUtc!.Value, cdm.Header.CreationDateUtc);
        Assert.Equal(options.Originator, cdm.Header.Originator);
        Assert.Equal(options.MessageFor, cdm.Header.MessageFor);
        Assert.Equal(options.MessageId, cdm.Header.MessageId);

        Assert.Equal(encounter.EncounterState.Epoch.ToUTC().DateTime, cdm.RelativeMetadataData.TcaUtc);
        Assert.Equal(encounter.EncounterState.MissDistanceMeters, cdm.RelativeMetadataData.MissDistanceMeters, 9);
        Assert.NotNull(cdm.RelativeMetadataData.RelativeSpeedMetersPerSecond);
        Assert.Equal(encounter.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude(),
            cdm.RelativeMetadataData.RelativeSpeedMetersPerSecond!.Value, 9);
        Assert.NotNull(cdm.RelativeMetadataData.CollisionProbability);
        Assert.Equal(encounter.CollisionRisk.ProbabilityOfCollision!.Value, cdm.RelativeMetadataData.CollisionProbability!.Value, 12);
        Assert.Equal("FOSTER-2D", cdm.RelativeMetadataData.CollisionProbabilityMethod);
        Assert.NotNull(cdm.RelativeMetadataData.RelativeStateVector);
        AssertVectorApproximatelyEqual(
            encounter.EncounterState.RelativeState.RelativePositionRtn,
            cdm.RelativeMetadataData.RelativeStateVector!.RelativePositionRtnMeters,
            1.0e-12);
        AssertVectorApproximatelyEqual(
            encounter.EncounterState.RelativeState.RelativeVelocityRtn,
            cdm.RelativeMetadataData.RelativeStateVector.RelativeVelocityRtnMetersPerSecond,
            1.0e-12);
        Assert.Equal(encounter.ScreeningWindow.StartDate.ToUTC().DateTime, cdm.RelativeMetadataData.StartScreenPeriodUtc);
        Assert.Equal(encounter.ScreeningWindow.EndDate.ToUTC().DateTime, cdm.RelativeMetadataData.StopScreenPeriodUtc);

        var protectedSegment = cdm.Segments[0];
        var secondarySegment = cdm.Segments[1];

        Assert.Equal(CdmObjectRole.Object1, protectedSegment.Metadata!.Object);
        Assert.Equal(CdmObjectRole.Object2, secondarySegment.Metadata!.Object);

        Assert.Equal("41001", protectedSegment.Metadata.ObjectDesignator);
        Assert.Equal("NAIF", protectedSegment.Metadata.CatalogName);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.Name, protectedSegment.Metadata.ObjectName);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.CosparId, protectedSegment.Metadata.InternationalDesignator);
        Assert.Equal(ObjectType.Payload, protectedSegment.Metadata.ObjectType);
        Assert.Equal("IO.Astrodynamics", protectedSegment.Metadata.EphemerisName);
        Assert.Equal(CdmCovarianceMethod.Calculated, protectedSegment.Metadata.CovarianceMethod);
        Assert.Equal(CdmManeuverable.Yes, protectedSegment.Metadata.Maneuverable);
        Assert.Equal("EARTH", protectedSegment.Metadata.OrbitCenter);
        Assert.Equal(CdmReferenceFrame.Gcrf, protectedSegment.Metadata.ReferenceFrame);
        // No OD and no propagated covariance: whether solar radiation pressure was used for the OD is not stated (CCSDS
        // 508.0-B-1 Cor. 2, Table 3-3; YES from Cr > 0, a capability, before)
        Assert.Null(protectedSegment.Metadata.SolarRadPressure);
        Assert.Null(secondarySegment.Metadata!.SolarRadPressure);
        Assert.DoesNotContain(XDocument.Parse(cdm.WriteToString()).Descendants(), e => e.Name.LocalName == "SOLAR_RAD_PRESSURE");
        // No OD and no propagated covariance: whether in-track thrust was modeled for the OD is not stated (#684; NO, from
        // the absent engines, before)
        Assert.Null(protectedSegment.Metadata.IntrackThrust);
        Assert.Null(secondarySegment.Metadata.IntrackThrust);

        Assert.Equal("41002", secondarySegment.Metadata.ObjectDesignator);
        Assert.Equal("NAIF", secondarySegment.Metadata.CatalogName);
        Assert.Equal(encounter.SecondaryObject.Name, secondarySegment.Metadata.ObjectName);
        Assert.Equal(((Spacecraft)encounter.SecondaryObject).CosparId, secondarySegment.Metadata.InternationalDesignator);
        Assert.Equal(ObjectType.Payload, secondarySegment.Metadata.ObjectType);
        Assert.Equal("IO.Astrodynamics", secondarySegment.Metadata.EphemerisName);
        Assert.Equal(CdmCovarianceMethod.Calculated, secondarySegment.Metadata.CovarianceMethod);
        // An engineless spacecraft states no maneuver capacity: N/A (#683; YES before)
        Assert.Equal(CdmManeuverable.NotApplicable, secondarySegment.Metadata.Maneuverable);
        Assert.Equal("EARTH", secondarySegment.Metadata.OrbitCenter);
        Assert.Equal(CdmReferenceFrame.Gcrf, secondarySegment.Metadata.ReferenceFrame);

        AssertVectorApproximatelyEqual(encounter.ProtectedState.Position, protectedSegment.Data!.StateVector!.PositionMeters, 1.0e-12);
        AssertVectorApproximatelyEqual(encounter.ProtectedState.Velocity, protectedSegment.Data.StateVector.VelocityMetersPerSecond, 1.0e-12);
        AssertVectorApproximatelyEqual(encounter.SecondaryState.Position, secondarySegment.Data!.StateVector!.PositionMeters, 1.0e-12);
        AssertVectorApproximatelyEqual(encounter.SecondaryState.Velocity, secondarySegment.Data.StateVector.VelocityMetersPerSecond, 1.0e-12);

        Assert.NotNull(protectedSegment.Data.AdditionalParameters);
        Assert.NotNull(secondarySegment.Data.AdditionalParameters);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters!.MassKilograms);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.GetTotalMass(), protectedSegment.Data.AdditionalParameters.MassKilograms!.Value, 12);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters.AreaPcSquareMeters);
        Assert.Equal(System.Math.PI * System.Math.Pow(encounter.ProtectedAsset.Spacecraft.HardBodyRadius, 2.0),
            protectedSegment.Data.AdditionalParameters.AreaPcSquareMeters!.Value, 12);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters.AreaDragSquareMeters);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters.AreaSrpSquareMeters);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.SectionalArea, protectedSegment.Data.AdditionalParameters.AreaDragSquareMeters!.Value, 12);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.SectionalArea, protectedSegment.Data.AdditionalParameters.AreaSrpSquareMeters!.Value, 12);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters.CdAreaOverMass);
        Assert.NotNull(protectedSegment.Data.AdditionalParameters.CrAreaOverMass);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.DragCoefficient * encounter.ProtectedAsset.Spacecraft.SectionalArea /
                     encounter.ProtectedAsset.Spacecraft.GetTotalMass(),
            protectedSegment.Data.AdditionalParameters.CdAreaOverMass!.Value, 12);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.SolarRadiationCoeff * encounter.ProtectedAsset.Spacecraft.SectionalArea /
                     encounter.ProtectedAsset.Spacecraft.GetTotalMass(),
            protectedSegment.Data.AdditionalParameters.CrAreaOverMass!.Value, 12);

        var secondaryRadius = encounter.CollisionRisk.CombinedHardBodyRadiusMeters - encounter.ProtectedAsset.Spacecraft.HardBodyRadius;
        Assert.NotNull(secondarySegment.Data.AdditionalParameters!.AreaPcSquareMeters);
        Assert.Equal(System.Math.PI * secondaryRadius * secondaryRadius,
            secondarySegment.Data.AdditionalParameters.AreaPcSquareMeters!.Value, 12);

        AssertMatrixApproximatelyEqual(RotateCovarianceToRtn(encounter.ProtectedState),
            protectedSegment.Data.CovarianceMatrix!.StateCovarianceRtn, 1.0e-12);
        AssertMatrixApproximatelyEqual(RotateCovarianceToRtn(encounter.SecondaryState),
            secondarySegment.Data.CovarianceMatrix!.StateCovarianceRtn, 1.0e-9);
    }

    [Fact]
    public void WriteToString_QualifiedWrappedAndStandaloneOutput_ValidateAgainstSchema()
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 5, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-002"
        });

        var wrappedXml = cdm.WriteToString();
        var standaloneXml = cdm.WriteToString(wrapInNdmContainer: false);

        var wrappedValidation = Cdm.ValidateSchemaFromXml(wrappedXml);
        var standaloneValidation = Cdm.ValidateSchemaFromXml(standaloneXml);

        Assert.True(wrappedValidation.IsValid, FormatValidationIssues(wrappedValidation.Issues));
        Assert.True(standaloneValidation.IsValid, FormatValidationIssues(standaloneValidation.Issues));
    }

    public static TheoryData<string, string, string> CdmFormsAndMutations()
    {
        // The mutation and a fragment of the error that names its element. The verdicts are those of the SANA NDM/XML 4.0.0
        // schemas (sanaregistry.org/files/ndmxml_unqualified/ for the unqualified forms, the qualified set IO embeds for the
        // qualified ones), which reject each mutation in each form (#614)
        var mutations = new (string Mutation, string Names)[]
        {
            ("COLLISION_PROBABILITY=1.5", "COLLISION_PROBABILITY"), // probabilityType: at most 1, ndmxml-4.0.0-common-4.0.xsd
            ("remove OBJECT_NAME", "OBJECT_NAME"), // required in cdmMetadata
            ("FOO", "FOO"), // undeclared
            ("segment twice", "segment"), // cdmBody holds two segments
            ("root foo", "foo") // no global declaration
        };
        var data = new TheoryData<string, string, string>();
        foreach (var form in new[] { "unqualified-ndm", "unqualified-message", "qualified-ndm", "qualified-message" })
        {
            data.Add(form, null!, null!);
            foreach (var (mutation, names) in mutations)
            {
                data.Add(form, mutation, names);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CdmFormsAndMutations))]
    public void ValidateSchema_StandardForms_FollowTheSanaSchema(string form, string? mutation, string? names)
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 10, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-614"
        });
        var xml = NdmDocuments.InForm(cdm.WriteToString(), "cdm", form);
        if (mutation != null)
        {
            xml = NdmDocuments.Mutated(xml, "cdm", mutation);
        }

        var result = Cdm.ValidateSchemaFromXml(xml);

        if (mutation == null)
        {
            // No warning: the schema ran on every element
            Assert.True(result.IsValid, FormatValidationIssues(result.Issues));
            Assert.Empty(result.Issues);
        }
        else
        {
            // Before: valid, with a warning per element (#614: a probability of 1.5 reported valid)
            Assert.False(result.IsValid, $"{form}, {mutation}: reported valid");
            Assert.Contains(result.Issues, e => e.Severity == CdmValidationSeverity.Error && e.Message.Contains(names!, StringComparison.Ordinal));
        }
    }

    // An <ndm> that the NDM/XML 4.0.0 schema accepts but that holds no CDM: empty, or holding only an OMM (lxml 6.1.3 with
    // SANA's unqualified set accepts both), without a namespace or in the namespace, whose root the CDM validator checks
    // against the NDM schema. The reader finds no CDM in it, so it is not a CDM document (third round of #668: valid with
    // no issue before)
    [Theory]
    [InlineData("empty")]
    [InlineData("unqualified-ndm")]
    [InlineData("qualified-ndm")]
    [InlineData("namespace, empty")]
    [InlineData("namespace, omm")]
    public void ValidateSchema_NdmWithoutCdm_IsInvalid(string document)
    {
        var issOmm = File.ReadAllText(NdmDocuments.IssOmmPath);
        var xml = document switch
        {
            "empty" => "<ndm/>",
            "namespace, empty" => $"<ndm xmlns=\"{NdmDocuments.NdmNamespace}\"/>",
            "namespace, omm" => new XElement(XName.Get("ndm", NdmDocuments.NdmNamespace),
                XElement.Parse(NdmDocuments.QualifiedMessage(issOmm, "omm"))).ToString(),
            _ => NdmDocuments.InForm(issOmm, "omm", document)
        };
        var parse = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml));
        Assert.Equal("Could not find CDM element in document.", parse.Message);

        var result = Cdm.ValidateSchemaFromXml(xml);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Issues);
        Assert.Equal(CdmValidationSeverity.Error, error.Severity);
        Assert.Equal("CDM001", error.Code);
        Assert.Contains("<cdm>", error.Message);
        NdmDocuments.WithTempFile(xml, file => Assert.Equal(error.Message, Assert.Single(Cdm.ValidateSchema(file).Issues).Message));
    }

    [Fact]
    public void ReadFromString_ParsesQualifiedAndUnqualifiedDocuments()
    {
        var original = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 10, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-003"
        });

        var qualifiedXml = original.WriteToString();
        var unqualifiedXml = RemoveNamespaces(qualifiedXml);

        var parsedQualified = Cdm.ReadFromString(qualifiedXml);
        var parsedUnqualified = Cdm.ReadFromString(unqualifiedXml);

        AssertCdmEquivalent(original, parsedQualified);
        AssertCdmEquivalent(original, parsedUnqualified);
    }

    [Fact]
    public void ReadFromString_NdmWithTwoCdm_Throws()
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 10, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "FIRST"
        });
        var xml = NdmDocuments.TwoCdm(RemoveNamespaces(cdm.WriteToString(wrapInNdmContainer: false)));

        var ex = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml));
        Assert.Contains("2 CDM messages", ex.Message);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var exStream = Assert.Throws<CdmParseException>(() => Cdm.ReadFromStream(stream));
        Assert.Contains("2 CDM messages", exStream.Message);
        var exFile = Assert.Throws<CdmParseException>(() => NdmDocuments.WithTempFile(xml, file => Cdm.ReadFromFile(file)));
        Assert.Contains("2 CDM messages", exFile.Message);

        // Two nested CDMs under a root that is not an NDM
        var cdmElement = XDocument.Parse(xml).Root!.Elements().First();
        var nested = new XElement("wrapper", new XElement("a", new XElement(cdmElement)), new XElement("b", new XElement(cdmElement))).ToString();
        var exNested = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(nested));
        Assert.Contains("2 CDM messages", exNested.Message);
    }

    // A CDM holds one header and one body, the body one relativeMetadataData and exactly two segments, one per object,
    // each segment one metadata and one data section, the data at most one of each block (NDM/XML 4.0.0,
    // ndmxml-4.0.0-cdm-1.0.xsd: cdmType, cdmBody, cdmSegment, cdmData; CCSDS 508.0-B-1, 3.2)
    // ToCdm writes no odParameters block, so each segment is given one, so that every block of cdmData is present
    private static string ManualCdmXml(bool qualified = false)
    {
        var exported = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 10, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "STRUCTURE"
        });
        var odParameters = new CdmOdParameters
        {
            TimeLastObservationStartUtc = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc),
            TimeLastObservationEndUtc = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
            ActualOdSpanDays = 3.0,
            ObservationsUsed = 120
        };
        var cdm = new Cdm(exported.Header, exported.RelativeMetadataData,
            exported.Segments.Select(segment => segment with { Data = segment.Data! with { OdParameters = odParameters } }).ToList());
        var xml = cdm.WriteToString(wrapInNdmContainer: false);
        return qualified ? xml : RemoveNamespaces(xml);
    }

    private static string WithSegmentCount(string xml, int count)
    {
        var document = XDocument.Parse(xml);
        var body = document.Root!.Elements().Single(e => e.Name.LocalName == "body");
        var segments = body.Elements().Where(e => e.Name.LocalName == "segment").ToList();
        if (count < segments.Count)
        {
            segments.Skip(count).ToList().ForEach(e => e.Remove());
        }
        else
        {
            for (int i = segments.Count; i < count; i++)
            {
                body.Add(new XElement(segments[1]));
            }
        }

        var result = document.ToString();
        Assert.Equal(count, XDocument.Parse(result).Root!.Elements().Single(e => e.Name.LocalName == "body").Elements()
            .Count(e => e.Name.LocalName == "segment"));
        return result;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ReadFromString_CdmWithOneOrThreeSegments_ThrowsParseException(int count)
    {
        // The embedded schema rejects the qualified document: a CDM holds exactly two segments
        Assert.True(Cdm.ValidateSchemaFromXml(ManualCdmXml(qualified: true)).IsValid);
        Assert.False(Cdm.ValidateSchemaFromXml(WithSegmentCount(ManualCdmXml(qualified: true), count)).IsValid);

        // Before: ArgumentException from the Cdm constructor, not the reader's documented exception
        foreach (var xml in new[] { WithSegmentCount(ManualCdmXml(), count), WithSegmentCount(ManualCdmXml(qualified: true), count) })
        {
            var ex = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml));
            Assert.Contains($"{count} segment elements", ex.Message);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            Assert.Throws<CdmParseException>(() => Cdm.ReadFromStream(stream));
            Assert.Throws<CdmParseException>(() => NdmDocuments.WithTempFile(xml, file => Cdm.ReadFromFile(file)));
        }
    }

    [Theory]
    [InlineData("header")]
    [InlineData("body")]
    [InlineData("body/relativeMetadataData")]
    [InlineData("body/relativeMetadataData/relativeStateVector")]
    [InlineData("body/segment/metadata")]
    [InlineData("body/segment/data")]
    [InlineData("body/segment/data/odParameters")]
    [InlineData("body/segment/data/additionalParameters")]
    [InlineData("body/segment/data/stateVector")]
    [InlineData("body/segment/data/covarianceMatrix")]
    public void ReadFromString_CdmWithDuplicateStructuralElement_Throws(string path)
    {
        // The first segment's element when the path goes through a segment; before, the first was read (a second
        // relativeMetadataData's miss distance dropped)
        var xml = ManualCdmXml();
        Assert.All(Cdm.ReadFromString(xml).Segments, segment => Assert.NotNull(segment.Data!.OdParameters));
        var document = XDocument.Parse(xml);
        if (path.StartsWith("body/segment/", StringComparison.Ordinal))
        {
            // Duplicate within the first segment only
            var segment = document.Root!.Elements().Single(e => e.Name.LocalName == "body").Elements().First(e => e.Name.LocalName == "segment");
            var element = path.Substring("body/segment/".Length).Split('/')
                .Aggregate(segment, (parent, name) => parent.Elements().Single(e => e.Name.LocalName == name));
            element.AddAfterSelf(new XElement(element));
            Assert.Equal(2, element.Parent!.Elements().Count(e => e.Name.LocalName == element.Name.LocalName));
            xml = document.ToString();
        }
        else
        {
            xml = NdmDocuments.WithDuplicatedElement(xml, "cdm", path);
        }

        var ex = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml));
        Assert.Contains($"2 {path.Split('/')[^1]} elements", ex.Message);
    }

    [Fact]
    public void WriteToFile_ValidateSchemaAndReadFromFile_RoundTripsGeneratedCdm()
    {
        var original = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 12, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-003-FILE"
        });

        var filePath = Path.Combine(Path.GetTempPath(), $"cdm-{Guid.NewGuid():N}.xml");

        try
        {
            original.WriteToFile(filePath);

            var schemaValidation = Cdm.ValidateSchema(filePath);
            Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));

            var parsed = Cdm.ReadFromFile(filePath);
            AssertCdmEquivalent(original, parsed);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void ToCdm_MetadataOverrides_UsesOverridesWithoutDiscardingDefaults()
    {
        var encounter = CreateManualEncounterCase();
        var cdm = encounter.ToCdm(new CdmExportOptions
        {
            MessageId = "CDM-UNIT-004",
            ProtectedParticipantMetadata = new CdmParticipantMetadata
            {
                ObjectDesignator = "55001",
                CatalogName = "18SPCS",
                OperatorOrganization = "Protected Ops",
                OperatorEmail = "protected.ops@example.test",
                Maneuverable = CdmManeuverable.No
            },
            SecondaryParticipantMetadata = new CdmParticipantMetadata
            {
                CatalogName = "EXTERNAL-CAT",
                OperatorOrganization = "Secondary Ops"
            }
        });

        var protectedMetadata = cdm.Segments[0].Metadata!;
        var secondaryMetadata = cdm.Segments[1].Metadata!;

        Assert.Equal("55001", protectedMetadata.ObjectDesignator);
        Assert.Equal("18SPCS", protectedMetadata.CatalogName);
        Assert.Equal("Protected Ops", protectedMetadata.OperatorOrganization);
        Assert.Equal("protected.ops@example.test", protectedMetadata.OperatorEmail);
        Assert.Equal(CdmManeuverable.No, protectedMetadata.Maneuverable);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.Name, protectedMetadata.ObjectName);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.CosparId, protectedMetadata.InternationalDesignator);
        Assert.Equal("IO.Astrodynamics", protectedMetadata.EphemerisName);

        Assert.Equal("EXTERNAL-CAT", secondaryMetadata.CatalogName);
        Assert.Equal("Secondary Ops", secondaryMetadata.OperatorOrganization);
        Assert.Equal(encounter.SecondaryObject.Name, secondaryMetadata.ObjectName);
        Assert.Equal(((Spacecraft)encounter.SecondaryObject).CosparId, secondaryMetadata.InternationalDesignator);
        Assert.Equal(CdmReferenceFrame.Gcrf, secondaryMetadata.ReferenceFrame);
    }

    [Fact]
    public void ToCdm_FromAnalyzeWorkflow_ProducesSchemaValidMessage()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(
            -42001,
            "ProtectedWorkflow",
            earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0),
            BuildStateCovariance(
                new[] { 25.0, 36.0, 49.0, 0.01, 0.02, 0.03 }));
        var secondarySpacecraft = CreateSpacecraft(
            -42002,
            "SecondaryWorkflow",
            earth,
            new Vector3(6_800_000.0, 120.0, -40.0),
            new Vector3(0.2, 7_646.0, -0.1),
            BuildStateCovariance(
                new[] { 16.0, 9.0, 4.0, 0.04, 0.05, 0.06 }),
            hardBodyRadius: 3.0);

        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondarySpacecraft,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0)));

        Assert.NotNull(encounter.ProtectedState);
        Assert.NotNull(encounter.SecondaryState);

        var cdm = encounter.ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 20, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-005"
        });

        var schemaValidation = Cdm.ValidateSchemaFromXml(cdm.WriteToString());

        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
        Assert.Equal(encounter.EncounterState.Epoch.ToUTC().DateTime, cdm.RelativeMetadataData.TcaUtc);
        Assert.Equal(encounter.EncounterState.MissDistanceMeters, cdm.RelativeMetadataData.MissDistanceMeters, 9);
        Assert.Equal(encounter.ProtectedAsset.Spacecraft.Name, cdm.Segments[0].Metadata!.ObjectName);
        Assert.Equal(encounter.SecondaryObject.Name, cdm.Segments[1].Metadata!.ObjectName);
    }

    // The pair of ToCdm_FromAnalyzeWorkflow_ProducesSchemaValidMessage, the secondary with a radius of 3 m, with an engine
    // when asked
    private static EncounterCase AnalyzeWorkflowEncounter(int naifId, bool secondaryEngine)
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(naifId, "ProtectedWorkflow", earth, new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0), BuildStateCovariance(new[] { 25.0, 36.0, 49.0, 0.01, 0.02, 0.03 }));
        var secondarySpacecraft = CreateSpacecraft(naifId - 1, "SecondaryWorkflow", earth, new Vector3(6_800_000.0, 120.0, -40.0),
            new Vector3(0.2, 7_646.0, -0.1), BuildStateCovariance(new[] { 16.0, 9.0, 4.0, 0.04, 0.05, 0.06 }),
            hardBodyRadius: 3.0);
        if (secondaryEngine)
        {
            var tank = new FuelTank("tank" + -naifId, "model", "sn1", 20.0, 20.0);
            secondarySpacecraft.AddFuelTank(tank);
            secondarySpacecraft.AddEngine(new Engine("engine" + -naifId, "model", "sn1", 300.0, 10.0, tank));
        }

        return ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedSpacecraft), secondarySpacecraft,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0)));
    }

    [Fact]
    public void ToCdm_FromAnalyzeWorkflow_AreaPcIsTheSecondaryRadius()
    {
        // Arrange: a protected spacecraft of 8 m and a secondary of 3 m, the options unset
        var encounter = AnalyzeWorkflowEncounter(-42011, false);

        // Act
        var cdm = encounter.ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 20, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-683"
        });

        // Assert: the combined radius is the sum of the two, and AREA_PC of object 2 is π r² of the secondary's own radius
        // (the exporter's convention; #683: π·5², the 5 m of the option in its place)
        Assert.Equal(8.0 + 3.0, encounter.CollisionRisk.CombinedHardBodyRadiusMeters);
        Assert.Equal(System.Math.PI * 3.0 * 3.0, cdm.Segments[1].Data!.AdditionalParameters!.AreaPcSquareMeters!.Value, 12);
        var schemaValidation = Cdm.ValidateSchemaFromXml(cdm.WriteToString());
        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
    }

    [Theory]
    [InlineData("engineless", CdmManeuverable.NotApplicable)]
    [InlineData("engine", CdmManeuverable.Yes)]
    [InlineData("override", CdmManeuverable.Yes)]
    public void ToCdm_SecondaryManeuverable_FollowsItsEngines(string secondary, CdmManeuverable expected)
    {
        // Arrange: the secondary spacecraft without an engine, with one, or without one and the flag given
        var encounter = AnalyzeWorkflowEncounter(secondary switch { "engineless" => -42021, "engine" => -42023, _ => -42025 },
            secondary == "engine");
        var options = new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 20, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-683-" + secondary,
            SecondaryParticipantMetadata = secondary == "override"
                ? new CdmParticipantMetadata { Maneuverable = CdmManeuverable.Yes }
                : null
        };

        // Act
        var cdm = encounter.ToCdm(options);
        var xml = cdm.WriteToString();

        // Assert: YES with an engine, N/A ("not available" or "not applicable", CCSDS 508.0-B-1 Cor. 2, 1.4.3.1) without
        // one, the value given winning (#683: YES for any spacecraft); the message valid against the bundled NDM/XML 4.0.0
        // schema, which reads the value back
        Assert.Equal(expected, cdm.Segments[1].Metadata!.Maneuverable);
        var schemaValidation = Cdm.ValidateSchemaFromXml(xml);
        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
        Assert.Equal(expected, Cdm.ReadFromString(xml).Segments[1].Metadata!.Maneuverable);
    }

    [Fact]
    public void ToCdm_ParticipantsWithEngines_OmitIntrackThrust()
    {
        // Arrange: #684's case, an engine on both spacecraft, nothing propagated by the library
        var encounter = CreateManualEncounterCase(withEngines: true);

        // Act
        var cdm = encounter.ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-684"
        });
        var xml = cdm.WriteToString();

        // Assert: INTRACK_THRUST states "whether in-track thrust modeling was used for the OD of the object" (CCSDS 508.0-B-1
        // Cor. 2, Table 3-3, Obligatory No; Annex A2.1.5 item 39, status O), not the capability MANEUVERABLE states: the
        // library ran no OD, so the keyword is omitted (#684: YES from the engines); MANEUVERABLE stays YES for object 2
        Assert.Null(cdm.Segments[0].Metadata!.IntrackThrust);
        Assert.Null(cdm.Segments[1].Metadata!.IntrackThrust);
        Assert.Equal(CdmManeuverable.Yes, cdm.Segments[1].Metadata!.Maneuverable);
        Assert.DoesNotContain(XDocument.Parse(xml).Descendants(), e => e.Name.LocalName == "INTRACK_THRUST");
        var schemaValidation = Cdm.ValidateSchemaFromXml(xml);
        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
        var reread = Cdm.ReadFromString(xml);
        Assert.Null(reread.Segments[0].Metadata!.IntrackThrust);
        Assert.Null(reread.Segments[1].Metadata!.IntrackThrust);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(1, null)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(2, null)]
    public void ToCdm_IntrackThrustOverride_SetsEitherValueAndNullKeepsTheDefault(int participant, bool? value)
    {
        // Arrange: both spacecraft with an engine, the value of one participant given (decision of S. Guillet, 2026-11-13,
        // #684: the caller states the OD's value; a null override keeps the default, omitted without a propagated covariance)
        var encounter = CreateManualEncounterCase(withEngines: true);
        var metadata = new CdmParticipantMetadata { IntrackThrust = value };
        var options = new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-684-OVERRIDE",
            ProtectedParticipantMetadata = participant == 1 ? metadata : null,
            SecondaryParticipantMetadata = participant == 2 ? metadata : null
        };

        // Act
        var cdm = encounter.ToCdm(options);
        var xml = cdm.WriteToString();

        // Assert: YES or NO as given, read back; omitted for null; the other participant omitted
        int given = participant - 1;
        Assert.Equal(value, cdm.Segments[given].Metadata!.IntrackThrust);
        Assert.Null(cdm.Segments[1 - given].Metadata!.IntrackThrust);
        var written = XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "INTRACK_THRUST")
            .Select(e => e.Value).ToArray();
        Assert.Equal(value switch { true => new[] { "YES" }, false => new[] { "NO" }, _ => Array.Empty<string>() }, written);
        var schemaValidation = Cdm.ValidateSchemaFromXml(xml);
        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
        Assert.Equal(value, Cdm.ReadFromString(xml).Segments[given].Metadata!.IntrackThrust);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(1, null)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(2, null)]
    public void ToCdm_SolarRadPressureOverride_SetsEitherValueAndNullKeepsTheDefault(int participant, bool? value)
    {
        // Arrange: the value of one participant given, the caller stating that of its OD; a null override keeps the default,
        // omitted without a propagated covariance
        var encounter = CreateManualEncounterCase();
        var metadata = new CdmParticipantMetadata { SolarRadPressure = value };
        var options = new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-SRP-OVERRIDE",
            ProtectedParticipantMetadata = participant == 1 ? metadata : null,
            SecondaryParticipantMetadata = participant == 2 ? metadata : null
        };

        // Act
        var cdm = encounter.ToCdm(options);
        var xml = cdm.WriteToString();

        // Assert: YES or NO as given, read back; omitted for null; the other participant omitted; the CDM schema-valid (the
        // keyword is optional, minOccurs 0 in ndmxml-4.0.0-cdm-1.0.xsd)
        int given = participant - 1;
        Assert.Equal(value, cdm.Segments[given].Metadata!.SolarRadPressure);
        Assert.Null(cdm.Segments[1 - given].Metadata!.SolarRadPressure);
        var written = XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "SOLAR_RAD_PRESSURE")
            .Select(e => e.Value).ToArray();
        Assert.Equal(value switch { true => new[] { "YES" }, false => new[] { "NO" }, _ => Array.Empty<string>() }, written);
        var schemaValidation = Cdm.ValidateSchemaFromXml(xml);
        Assert.True(schemaValidation.IsValid, FormatValidationIssues(schemaValidation.Issues));
        Assert.True(cdm.Validate().IsValid);
        Assert.Equal(value, Cdm.ReadFromString(xml).Segments[given].Metadata!.SolarRadPressure);
    }

    [Fact]
    public void ToCdm_SameStateInIcrfAndGcrf_ExportsTheSameContent()
    {
        // Frame.ICRF (SPICE J2000) and Frame.GCRF share the ICRF axes, so the GCRF label of the CDM is exact for both.
        var icrfEncounter = CreateManualEncounterCase();
        var gcrfEncounter = new EncounterCase(
            icrfEncounter.ProtectedAsset,
            icrfEncounter.SecondaryObject,
            icrfEncounter.ScreeningWindow,
            icrfEncounter.EncounterState,
            icrfEncounter.CollisionRisk,
            (StateVector)icrfEncounter.ProtectedState!.ToFrame(Frames.Frame.GCRF),
            (StateVector)icrfEncounter.SecondaryState!.ToFrame(Frames.Frame.GCRF));
        Assert.Equal("GCRF", gcrfEncounter.ProtectedState!.Frame.Name);

        var options = new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            MessageId = "CDM-UNIT-ICRF-GCRF"
        };
        var fromIcrf = icrfEncounter.ToCdm(options);
        var fromGcrf = gcrfEncounter.ToCdm(options);

        for (int index = 0; index < 2; index++)
        {
            var icrfSegment = fromIcrf.Segments[index];
            var gcrfSegment = fromGcrf.Segments[index];
            Assert.Equal(CdmReferenceFrame.Gcrf, icrfSegment.Metadata!.ReferenceFrame);
            Assert.Equal(CdmReferenceFrame.Gcrf, gcrfSegment.Metadata!.ReferenceFrame);
            AssertVectorApproximatelyEqual(icrfSegment.Data!.StateVector!.PositionMeters, gcrfSegment.Data!.StateVector!.PositionMeters, 1.0e-6);
            AssertVectorApproximatelyEqual(icrfSegment.Data.StateVector.VelocityMetersPerSecond,
                gcrfSegment.Data.StateVector.VelocityMetersPerSecond, 1.0e-9);

            var icrfCovariance = icrfSegment.Data.CovarianceMatrix!.StateCovarianceRtn;
            var gcrfCovariance = gcrfSegment.Data.CovarianceMatrix!.StateCovarianceRtn;
            for (int row = 0; row < 6; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    Assert.Equal(icrfCovariance.Get(row, column), gcrfCovariance.Get(row, column), 1.0e-6);
                }
            }
        }
    }

    [Fact]
    public void ToCdm_ThrowsWhenParticipantCovarianceIsUnavailable()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(
            -43001,
            "ProtectedNoCovFailure",
            earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0),
            BuildStateCovariance(
                new[] { 25.0, 25.0, 25.0, 0.01, 0.01, 0.01 }));
        var secondarySpacecraft = CreateSpacecraftWithoutCovariance(
            -43002,
            "SecondaryNoCovFailure",
            earth,
            new Vector3(6_800_000.0, 50.0, 0.0),
            new Vector3(0.0, 7_646.0, 0.0),
            hardBodyRadius: 3.0);

        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondarySpacecraft,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0)));

        var exception = Assert.Throws<InvalidOperationException>(() => encounter.ToCdm());
        Assert.Contains("covariance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EncounterCase CreateManualEncounterCase(bool withEngines = false)
    {
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB.AddMinutes(5.0);
        var screeningWindow = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var protectedCovariance = BuildStateCovariance(
            new[] { 25.0, 36.0, 49.0, 0.01, 0.02, 0.03 },
            (1, 0, 1.5),
            (2, 0, -0.5),
            (4, 3, 0.002));
        var secondaryCovariance = BuildStateCovariance(
            new[] { 4.0, 9.0, 16.0, 0.04, 0.05, 0.06 },
            (1, 0, -0.25),
            (2, 1, 0.75),
            (5, 4, -0.003));

        var protectedState = new StateVector(
            new Vector3(7_000_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_500.0, 0.0),
            earth,
            epoch,
            Frames.Frame.ICRF,
            protectedCovariance);
        var secondaryState = new StateVector(
            new Vector3(7_000_120.0, 80.0, -40.0),
            new Vector3(0.5, 7_499.8, 0.1),
            earth,
            epoch,
            Frames.Frame.ICRF,
            secondaryCovariance);

        var protectedSpacecraft = new Spacecraft(
            -41001,
            "ProtectedAlpha",
            120.0,
            150.0,
            new Clock("ProtectedAlpha_CLK", 256),
            protectedState,
            sectionalArea: 2.0,
            dragCoeff: 2.2,
            cosparId: "2026-001A",
            solarRadiationCoeff: 1.2,
            hardBodyRadius: 8.0);
        var secondarySpacecraft = new Spacecraft(
            -41002,
            "SecondaryBravo",
            95.0,
            120.0,
            new Clock("SecondaryBravo_CLK", 256),
            secondaryState,
            sectionalArea: 1.5,
            dragCoeff: 2.1,
            cosparId: "2026-001B",
            solarRadiationCoeff: 1.1,
            hardBodyRadius: 3.0);
        if (withEngines)
        {
            foreach (var spacecraft in new[] { protectedSpacecraft, secondarySpacecraft })
            {
                var tank = new FuelTank("tank" + -spacecraft.NaifId, "model", "sn1", 20.0, 20.0);
                spacecraft.AddFuelTank(tank);
                spacecraft.AddEngine(new Engine("engine" + -spacecraft.NaifId, "model", "sn1", 300.0, 10.0, tank));
            }
        }

        var relativePosition = secondaryState.Position - protectedState.Position;
        var relativeVelocity = secondaryState.Velocity - protectedState.Velocity;
        var relativeState = new RelativeState(
            epoch,
            relativePosition,
            relativeVelocity,
            relativePosition,
            relativeVelocity);

        var combinedCovarianceRtn = AddMatrices(protectedCovariance, secondaryCovariance);
        var encounterState = new EncounterState(
            epoch,
            relativeState,
            relativePosition.Magnitude(),
            combinedCovarianceRtn,
            EncounterQualityFlags.None);

        var collisionRisk = new CollisionRisk(
            combinedHardBodyRadiusMeters: 11.0,
            probabilityOfCollision: 1.23456789e-4,
            projectedCovariance: new Matrix(new[,] { { 45.0, 2.0 }, { 2.0, 55.0 } }),
            radialSigmaMeters: System.Math.Sqrt(combinedCovarianceRtn.Get(0, 0)),
            inTrackSigmaMeters: System.Math.Sqrt(combinedCovarianceRtn.Get(1, 1)),
            crossTrackSigmaMeters: System.Math.Sqrt(combinedCovarianceRtn.Get(2, 2)));

        return new EncounterCase(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondarySpacecraft,
            screeningWindow,
            encounterState,
            collisionRisk,
            protectedState,
            secondaryState);
    }

    private static CelestialBody CreateEarth()
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
    }

    private static Spacecraft CreateSpacecraft(
        int naifId,
        string name,
        CelestialBody earth,
        Vector3 position,
        Vector3 velocity,
        Matrix covariance,
        double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, covariance);
        return new Spacecraft(
            naifId,
            name,
            120.0,
            150.0,
            new Clock($"{name}_CLK", 256),
            orbit,
            sectionalArea: 2.0,
            dragCoeff: 2.2,
            cosparId: $"2026-001{name[0]}",
            solarRadiationCoeff: 1.2,
            hardBodyRadius: hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraftWithoutCovariance(
        int naifId,
        string name,
        CelestialBody earth,
        Vector3 position,
        Vector3 velocity,
        double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        return new Spacecraft(
            naifId,
            name,
            120.0,
            150.0,
            new Clock($"{name}_CLK", 256),
            orbit,
            sectionalArea: 2.0,
            dragCoeff: 2.2,
            cosparId: $"2026-001{name[0]}",
            solarRadiationCoeff: 1.2,
            hardBodyRadius: hardBodyRadius);
    }

    private static Matrix BuildStateCovariance(double[] diagonal, params (int Row, int Column, double Value)[] offDiagonalEntries)
    {
        var covariance = new Matrix(6, 6);
        for (int index = 0; index < diagonal.Length; index++)
        {
            covariance.Set(index, index, diagonal[index]);
        }

        foreach (var entry in offDiagonalEntries)
        {
            covariance.Set(entry.Row, entry.Column, entry.Value);
            covariance.Set(entry.Column, entry.Row, entry.Value);
        }

        return covariance;
    }

    private static Matrix AddMatrices(Matrix left, Matrix right)
    {
        var result = new Matrix(left.Rows, left.Columns);
        for (int row = 0; row < left.Rows; row++)
        {
            for (int column = 0; column < left.Columns; column++)
            {
                result.Set(row, column, left.Get(row, column) + right.Get(row, column));
            }
        }

        return result;
    }

    private static Matrix RotateCovarianceToRtn(StateVector state)
    {
        var rotation = CreateRtnRotation(state);
        var transform = Matrix.CreateBlockDiagonal(rotation, rotation);
        return transform * state.Covariance!.Value * transform.Transpose();
    }

    private static Matrix CreateRtnRotation(StateVector state)
    {
        var radial = state.Position.Normalize();
        var normal = state.Position.Cross(state.Velocity).Normalize();
        var transverse = normal.Cross(radial).Normalize();
        var matrix = new Matrix(3, 3);
        matrix.Set(0, 0, radial.X);
        matrix.Set(0, 1, radial.Y);
        matrix.Set(0, 2, radial.Z);
        matrix.Set(1, 0, transverse.X);
        matrix.Set(1, 1, transverse.Y);
        matrix.Set(1, 2, transverse.Z);
        matrix.Set(2, 0, normal.X);
        matrix.Set(2, 1, normal.Y);
        matrix.Set(2, 2, normal.Z);
        return matrix;
    }

    private static void AssertCdmEquivalent(Cdm expected, Cdm actual)
    {
        Assert.Equal(expected.Header.CreationDateUtc, actual.Header.CreationDateUtc);
        Assert.Equal(expected.Header.Originator, actual.Header.Originator);
        Assert.Equal(expected.Header.MessageFor, actual.Header.MessageFor);
        Assert.Equal(expected.Header.MessageId, actual.Header.MessageId);
        Assert.Equal(expected.RelativeMetadataData.TcaUtc, actual.RelativeMetadataData.TcaUtc);
        Assert.Equal(expected.RelativeMetadataData.MissDistanceMeters, actual.RelativeMetadataData.MissDistanceMeters, 12);
        Assert.NotNull(expected.RelativeMetadataData.RelativeSpeedMetersPerSecond);
        Assert.NotNull(actual.RelativeMetadataData.RelativeSpeedMetersPerSecond);
        Assert.Equal(
            expected.RelativeMetadataData.RelativeSpeedMetersPerSecond!.Value,
            actual.RelativeMetadataData.RelativeSpeedMetersPerSecond!.Value,
            12);
        Assert.NotNull(expected.RelativeMetadataData.CollisionProbability);
        Assert.NotNull(actual.RelativeMetadataData.CollisionProbability);
        Assert.Equal(
            expected.RelativeMetadataData.CollisionProbability!.Value,
            actual.RelativeMetadataData.CollisionProbability!.Value,
            12);
        Assert.Equal(expected.RelativeMetadataData.CollisionProbabilityMethod, actual.RelativeMetadataData.CollisionProbabilityMethod);

        Assert.NotNull(expected.RelativeMetadataData.RelativeStateVector);
        Assert.NotNull(actual.RelativeMetadataData.RelativeStateVector);
        AssertVectorApproximatelyEqual(
            expected.RelativeMetadataData.RelativeStateVector!.RelativePositionRtnMeters,
            actual.RelativeMetadataData.RelativeStateVector!.RelativePositionRtnMeters,
            1.0e-12);
        AssertVectorApproximatelyEqual(
            expected.RelativeMetadataData.RelativeStateVector.RelativeVelocityRtnMetersPerSecond,
            actual.RelativeMetadataData.RelativeStateVector.RelativeVelocityRtnMetersPerSecond,
            1.0e-12);

        Assert.Equal(expected.Segments.Count, actual.Segments.Count);
        for (int index = 0; index < expected.Segments.Count; index++)
        {
            var expectedSegment = expected.Segments[index];
            var actualSegment = actual.Segments[index];

            Assert.Equal(expectedSegment.Metadata!.Object, actualSegment.Metadata!.Object);
            Assert.Equal(expectedSegment.Metadata.ObjectDesignator, actualSegment.Metadata.ObjectDesignator);
            Assert.Equal(expectedSegment.Metadata.CatalogName, actualSegment.Metadata.CatalogName);
            Assert.Equal(expectedSegment.Metadata.ObjectName, actualSegment.Metadata.ObjectName);
            Assert.Equal(expectedSegment.Metadata.InternationalDesignator, actualSegment.Metadata.InternationalDesignator);
            Assert.Equal(expectedSegment.Metadata.ReferenceFrame, actualSegment.Metadata.ReferenceFrame);
            Assert.Equal(expectedSegment.Metadata.EphemerisName, actualSegment.Metadata.EphemerisName);

            AssertVectorApproximatelyEqual(
                expectedSegment.Data!.StateVector!.PositionMeters,
                actualSegment.Data!.StateVector!.PositionMeters,
                1.0e-12);
            AssertVectorApproximatelyEqual(
                expectedSegment.Data.StateVector.VelocityMetersPerSecond,
                actualSegment.Data.StateVector.VelocityMetersPerSecond,
                1.0e-12);
            AssertMatrixApproximatelyEqual(
                expectedSegment.Data.CovarianceMatrix!.StateCovarianceRtn,
                actualSegment.Data.CovarianceMatrix!.StateCovarianceRtn,
                1.0e-12);
        }
    }

    private static void AssertVectorApproximatelyEqual(Vector3 expected, Vector3 actual, double tolerance)
    {
        Assert.True(System.Math.Abs(expected.X - actual.X) <= tolerance,
            $"Vector X mismatch: expected {expected.X:E12}, got {actual.X:E12}");
        Assert.True(System.Math.Abs(expected.Y - actual.Y) <= tolerance,
            $"Vector Y mismatch: expected {expected.Y:E12}, got {actual.Y:E12}");
        Assert.True(System.Math.Abs(expected.Z - actual.Z) <= tolerance,
            $"Vector Z mismatch: expected {expected.Z:E12}, got {actual.Z:E12}");
    }

    private static void AssertMatrixApproximatelyEqual(Matrix expected, Matrix actual, double tolerance)
    {
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Columns, actual.Columns);

        for (int row = 0; row < expected.Rows; row++)
        {
            for (int column = 0; column < expected.Columns; column++)
            {
                Assert.True(System.Math.Abs(expected.Get(row, column) - actual.Get(row, column)) <= tolerance,
                    $"Matrix mismatch at ({row},{column}): expected {expected.Get(row, column):E12}, got {actual.Get(row, column):E12}");
            }
        }
    }

    // The seven lengthTypeUO fields (#613): units optional, m the only unit of the schema and of CCSDS 508.0 table 3-2
    private static readonly (string Name, string Text, double Value)[] OptionalUnitLengths =
    [
        ("MISS_DISTANCE", "715", 715.0),
        ("RELATIVE_POSITION_R", "27.4", 27.4),
        ("RELATIVE_POSITION_T", "-70.2", -70.2),
        ("RELATIVE_POSITION_N", "711.8", 711.8),
        ("SCREEN_VOLUME_X", "200", 200.0),
        ("SCREEN_VOLUME_Y", "1000", 1000.0),
        ("SCREEN_VOLUME_Z", "1000", 1000.0)
    ];

    [Theory]
    [InlineData(null, 1.0)]
    [InlineData("m", 1.0)]
    [InlineData("km", 1000.0)]
    public void ReadFromString_OptionalUnitLengths_AreMetersByDefault(string? units, double scale)
    {
        var document = XDocument.Parse(RemoveNamespaces(CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNITS-613"
        }).WriteToString()));
        var relativeMetadata = document.Descendants("relativeMetadataData").Single();
        foreach (var (name, text, _) in OptionalUnitLengths)
        {
            var element = relativeMetadata.Descendants(name).SingleOrDefault();
            if (element == null)
            {
                element = new XElement(name);
                relativeMetadata.Add(element);
            }

            element.Value = text;
            element.SetAttributeValue("units", units);
        }

        var cdm = Cdm.ReadFromString(document.ToString());

        var data = cdm.RelativeMetadataData;
        var read = new[]
        {
            data.MissDistanceMeters, data.RelativeStateVector!.RelativePositionRtnMeters.X,
            data.RelativeStateVector.RelativePositionRtnMeters.Y, data.RelativeStateVector.RelativePositionRtnMeters.Z,
            data.ScreenVolumeXMeters!.Value, data.ScreenVolumeYMeters!.Value, data.ScreenVolumeZMeters!.Value
        };
        for (int i = 0; i < OptionalUnitLengths.Length; i++)
        {
            // The double of the text, times 1000 for km, exactly: before #613 a length without units was read in km
            Assert.Equal(OptionalUnitLengths[i].Value * scale, read[i]);
        }
    }

    [Fact]
    public void ReadFromString_DayOfYearDates()
    {
        // #490: the TCA and the creation date in the day-of-year form, as CSPICE's str2et reads them
        // (tools/ccsds_epoch_reference: 2026-013 is 2026-01-13, 2014-021 is 2014-01-21)
        var tca = new DateTime(2026, 1, 13, 5, 0, 55, DateTimeKind.Utc).AddTicks(7527680);
        var original = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2014, 1, 21, 17, 42, 47, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-DOY-490"
        });
        var document = XDocument.Parse(RemoveNamespaces(original.WriteToString()));
        document.Descendants("CREATION_DATE").Single().Value = "2014-021T17:42:47.000";
        document.Descendants("TCA").Single().Value = "2026-013T05:00:55.752768Z";

        var cdm = Cdm.ReadFromString(document.ToString());

        Assert.Equal(tca, cdm.RelativeMetadataData.TcaUtc);
        Assert.Equal(DateTimeKind.Utc, cdm.RelativeMetadataData.TcaUtc.Kind);
        Assert.Equal(new DateTime(2014, 1, 21, 17, 42, 47, DateTimeKind.Utc), cdm.Header.CreationDateUtc);

        // The literal DDD was read as 1 January
        document.Descendants("TCA").Single().Value = "2026-DDDT05:00:55.752768Z";
        Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(document.ToString()));
    }

    [Theory]
    [InlineData("0000-01-01T00:00:00")]
    [InlineData("0000-001T00:00:00")]
    [InlineData("\u0662\u0660\u0661\u0660-03-13T22:37:52")]
    [InlineData("\uFF12\uFF10\uFF11\uFF10-03-13T22:37:52")]
    public void ReadFromString_YearZeroOrNonAsciiDigits(string text)
    {
        // The CDM's own parse exception (CcsdsEpochParser returns false), not an exception of DateTime or int.Parse
        var original = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2014, 1, 21, 17, 42, 47, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-EPOCH-0000"
        });
        var document = XDocument.Parse(RemoveNamespaces(original.WriteToString()));
        document.Descendants("CREATION_DATE").Single().Value = text;
        Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(document.ToString()));

        document = XDocument.Parse(RemoveNamespaces(original.WriteToString()));
        document.Descendants("TCA").Single().Value = text;
        Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(document.ToString()));
    }

    private static string FormatValidationIssues(IEnumerable<CdmValidationError> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue =>
            $"{issue.Severity} {issue.Code} {issue.Path}: {issue.Message}"));
    }

    private static string RemoveNamespaces(string xml)
    {
        var document = XDocument.Parse(xml);
        var strippedRoot = StripNamespaces(document.Root!);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), strippedRoot).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement StripNamespaces(XElement element)
    {
        var attributes = element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.None)
            .Select(attribute => new XAttribute(attribute.Name.LocalName, attribute.Value));

        var childNodes = element.Nodes().Select<XNode, XNode?>(node => node switch
        {
            XElement child => StripNamespaces(child),
            XCData cdata => new XCData(cdata.Value),
            XText text => new XText(text.Value),
            XComment comment => new XComment(comment.Value),
            _ => null
        }).Where(node => node != null);

        return new XElement(element.Name.LocalName, attributes, childNodes!);
    }
}
