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
        // Foster and Estes (1992), the 2D Gaussian integrated over the hard-body disc: FOSTER-1992 in the SANA registry
        // cdm_cpm (CCSDS 508.0-B-1 Cor. 2, annex E, E1, which gives it as an example) and in the standard's examples
        // (3.6.3, 4.4); FOSTER-2D, written before #618, is not registered
        Assert.Equal("FOSTER-1992", cdm.RelativeMetadataData.CollisionProbabilityMethod);
        Assert.Equal("FOSTER-1992", XDocument.Parse(cdm.WriteToString()).Descendants()
            .Single(e => e.Name.LocalName == "COLLISION_PROBABILITY_METHOD").Value);
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

    // The maximum Pc of a single covariance (Frisbee) has no value in the SANA registry cdm_cpm (ALFANO-MAX-PROBABILITY is
    // Alfano's 2005 maximum, another method), and a CDM carries both covariances: an encounter whose Pc is that maximum is
    // not exported (#618 with #342, decided by S. Guillet, 2026-11-17; before, a case built with the flag and both
    // covariances was written with the unregistered FOSTER-2D-MAX-PC), whatever method the options name
    [Theory]
    [InlineData(null)]
    [InlineData("ALFANO-MAX-PROBABILITY")]
    public void ToCdm_SingleCovarianceMaximumPc_IsRefused(string? method)
    {
        var encounter = CreateManualEncounterCase(qualityFlags: EncounterQualityFlags.SingleCovarianceMaximumPcUsed);

        var exception = Assert.Throws<InvalidOperationException>(() => encounter.ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            CollisionProbabilityMethod = method
        }));
        Assert.Contains("SingleCovarianceMaximumPcUsed", exception.Message);
    }

    // A method the options name is written as given
    [Fact]
    public void ToCdm_CollisionProbabilityMethodOption_IsWrittenAsGiven()
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            CollisionProbabilityMethod = "ALFANO-2005"
        });

        Assert.Equal("ALFANO-2005", Cdm.ReadFromString(cdm.WriteToString()).RelativeMetadataData.CollisionProbabilityMethod);
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

    // The qualified form of CCSDS 508.0-B-1 Cor. 2, 4.3.3 (note 2): the root, <cdm> or the <ndm> that wraps it, in no
    // namespace, with xmlns:ndm="urn:ccsds:schema:ndmxml" declared on it, every element below it in that namespace under
    // the prefix ndm. The reference is the SANA NDM/XML 4.0.0 qualified master as embedded (ndmxml-4.0.0-master-4.0.xsd,
    // no targetNamespace: its global ndm and cdm in no namespace; the cdm of an ndm qualified, ndmxml-4.0.0-ndm-4.0.xsd),
    // compiled here from the embedded files with no synthetic root and run by .NET's validator on the written text alone.
    // Before #615 the writer put the root in the namespace, which that master rejects at the root ("No matching global
    // declaration available for the validation root", lxml 6.1.3 and .NET alike, measured 2026-11-17 on a85098d)
    [Theory]
    [InlineData("string, ndm")]
    [InlineData("string, cdm")]
    [InlineData("stream, ndm")]
    [InlineData("file, cdm")]
    public void WriteTo_RootOutsideTheNamespace_ValidatesAgainstTheSanaQualifiedMaster(string output)
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 5, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-615"
        });
        bool wrapped = output.EndsWith("ndm", StringComparison.Ordinal);
        string xml;
        if (output.StartsWith("string", StringComparison.Ordinal))
        {
            xml = cdm.WriteToString(wrapped);
        }
        else if (output.StartsWith("stream", StringComparison.Ordinal))
        {
            using var stream = new MemoryStream();
            new CdmWriter().WriteToStream(cdm, stream, wrapped);
            stream.Position = 0;
            xml = new StreamReader(stream, System.Text.Encoding.UTF8).ReadToEnd();
        }
        else
        {
            var path = Path.Combine(Path.GetTempPath(), $"cdm_615_{Guid.NewGuid():N}.xml");
            try
            {
                cdm.WriteToFile(path, wrapped);
                xml = File.ReadAllText(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        var document = XDocument.Parse(xml);
        XNamespace ndm = "urn:ccsds:schema:ndmxml";
        var root = document.Root!;
        Assert.Equal(XName.Get(wrapped ? "ndm" : "cdm"), root.Name);
        Assert.Equal(ndm.NamespaceName, root.Attribute(XNamespace.Xmlns + "ndm")?.Value);
        Assert.Equal("https://sanaregistry.org/r/ndmxml_qualified/ndmxml-4.0.0-master-4.0.xsd",
            root.Attribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "noNamespaceSchemaLocation")?.Value);
        Assert.All(root.Descendants(), e => Assert.Equal(ndm, e.Name.Namespace));
        Assert.All(root.Descendants(), e => Assert.Equal("ndm", e.GetPrefixOfNamespace(ndm)));
        if (wrapped)
        {
            Assert.Equal(ndm + "cdm", Assert.Single(root.Elements()).Name);
        }

        Assert.Empty(SanaQualifiedMasterIssues(xml));
        AssertCdmEquivalent(cdm, Cdm.ReadFromString(xml));
        var validation = Cdm.ValidateSchemaFromXml(xml);
        Assert.True(validation.IsValid, FormatValidationIssues(validation.Issues));
        Assert.Empty(validation.Issues);
    }

    // Every date of a CDM is UTC (CCSDS 508.0-B-1 Cor. 2, 6.3.2.6): a DateTime of kind Utc or Unspecified is written as the
    // clock reading given, one of kind Local as its UTC, .NET's meaning of Local (#617; CcsdsLocalTimeTests checks the same
    // in time zones other than UTC, where an Unspecified date was written shifted by the zone's offset)
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void WriteToString_DateOfEachKind_WrittenAsItsUtc(DateTimeKind kind)
    {
        var exported = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 5, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-617"
        });
        var given = new DateTime(2010, 3, 13, 22, 37, 52, 618, kind);
        var expected = (kind == DateTimeKind.Local ? given.ToUniversalTime() : given)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture) + "Z";
        var cdm = new Cdm(exported.Header with { CreationDateUtc = given },
            exported.RelativeMetadataData with
            {
                TcaUtc = given, StartScreenPeriodUtc = given, StopScreenPeriodUtc = given, ScreenEntryTimeUtc = given,
                ScreenExitTimeUtc = given
            },
            exported.Segments.Select(segment => segment with
            {
                Data = segment.Data! with
                {
                    OdParameters = new CdmOdParameters { TimeLastObservationStartUtc = given, TimeLastObservationEndUtc = given }
                }
            }).ToList());

        var document = XDocument.Parse(cdm.WriteToString());

        var dates = new[]
        {
            "CREATION_DATE", "TCA", "START_SCREEN_PERIOD", "STOP_SCREEN_PERIOD", "SCREEN_ENTRY_TIME", "SCREEN_EXIT_TIME",
            "TIME_LASTOB_START", "TIME_LASTOB_END"
        };
        foreach (var keyword in dates)
        {
            Assert.All(document.Descendants().Where(e => e.Name.LocalName == keyword), e => Assert.Equal(expected, e.Value));
        }

        Assert.Equal(dates.Length + 2, document.Descendants().Count(e => dates.Contains(e.Name.LocalName)));
    }

    // The form written before #615, the root in the namespace: still read, and still checked by Cdm.ValidateSchema
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadFromString_RootInTheNamespace_FormWrittenBefore615_IsStillReadAndValidated(bool wrapped)
    {
        var cdm = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 5, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-615-OLD"
        });
        XNamespace ndm = "urn:ccsds:schema:ndmxml";
        var document = XDocument.Parse(cdm.WriteToString(wrapped));
        var root = document.Root!;
        foreach (var element in root.DescendantsAndSelf())
        {
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            element.Name = ndm + element.Name.LocalName;
        }

        root.SetAttributeValue("xmlns", ndm.NamespaceName);
        var old = document.ToString();
        Assert.StartsWith($"<{root.Name.LocalName} ", old);
        Assert.Equal(ndm, XDocument.Parse(old).Root!.Name.Namespace);
        Assert.NotEmpty(SanaQualifiedMasterIssues(old));

        AssertCdmEquivalent(cdm, Cdm.ReadFromString(old));
        var validation = Cdm.ValidateSchemaFromXml(old);
        Assert.True(validation.IsValid, FormatValidationIssues(validation.Issues));
    }

    // The errors and warnings of .NET's validator on the document against the embedded SANA NDM/XML 4.0.0 qualified master,
    // compiled from the embedded schema files, every include resolved to them
    private static List<string> SanaQualifiedMasterIssues(string xml)
    {
        var resolver = new EmbeddedNdmSchemaResolver();
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = resolver };
        using (var master = IO.Astrodynamics.CCSDS.CcsdsSchemaLoader.GetSchemaStream("ndmxml-4.0.0-master-4.0.xsd"))
        using (var reader = System.Xml.XmlReader.Create(master,
                   new System.Xml.XmlReaderSettings { XmlResolver = resolver, DtdProcessing = System.Xml.DtdProcessing.Prohibit },
                   "file:///ndmxml/ndmxml-4.0.0-master-4.0.xsd"))
        {
            schemas.Add(null, reader);
        }

        schemas.Compile();
        var issues = new List<string>();
        var settings = new System.Xml.XmlReaderSettings
        {
            ValidationType = System.Xml.ValidationType.Schema,
            Schemas = schemas,
            ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.ReportValidationWarnings
        };
        settings.ValidationEventHandler += (_, args) => issues.Add($"{args.Severity}: {args.Message}");
        using (var reader = System.Xml.XmlReader.Create(new StringReader(xml), settings))
        {
            while (reader.Read())
            {
            }
        }

        return issues;
    }

    private sealed class EmbeddedNdmSchemaResolver : System.Xml.XmlResolver
    {
        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            return IO.Astrodynamics.CCSDS.CcsdsSchemaLoader.GetSchemaStream(Path.GetFileName(absoluteUri.LocalPath));
        }
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

    // ManualCdmXml in the namespace with every optional enumerated keyword present: the screen volume's frame and shape,
    // and SOLAR_RAD_PRESSURE, EARTH_TIDES and INTRACK_THRUST in both segments
    private static string EnumeratedKeywordCdmXml()
    {
        var cdm = Cdm.ReadFromString(ManualCdmXml(qualified: true));
        var withKeywords = new Cdm(cdm.Header,
            cdm.RelativeMetadataData with
            {
                ScreenVolumeFrame = CdmScreenVolumeFrame.Rtn,
                ScreenVolumeShape = CdmScreenVolumeShape.Ellipsoid,
                ScreenVolumeXMeters = 200.0,
                ScreenVolumeYMeters = 1000.0,
                ScreenVolumeZMeters = 1000.0
            },
            cdm.Segments.Select(segment => segment with
            {
                Metadata = segment.Metadata! with { SolarRadPressure = true, EarthTides = false, IntrackThrust = false }
            }).ToList());
        return withKeywords.WriteToString(wrapInNdmContainer: false);
    }

    // The enumerated keywords of a CDM with a value that is none of theirs: CCSDS 508.0-B-1 table 3-3 and the enumerations
    // of ndmxml-4.0.0-cdm-1.0.xsd (objectType OBJECT1/OBJECT2, referenceFrameType EME2000/GCRF/ITRF, covarianceMethodType
    // CALCULATED/DEFAULT, maneuverableType YES/NO/N/A, screenVolumeFrameType RTN/TVN, screenVolumeShapeType
    // ELLIPSOID/BOX, yesNoType YES/NO)
    [Theory]
    [InlineData("OBJECT", "OBJECT3")]
    [InlineData("REF_FRAME", "TEME")]
    [InlineData("COVARIANCE_METHOD", "GUESSED")]
    [InlineData("MANEUVERABLE", "MAYBE")]
    [InlineData("SCREEN_VOLUME_FRAME", "XYZ")]
    [InlineData("SCREEN_VOLUME_SHAPE", "SPHERE")]
    [InlineData("SOLAR_RAD_PRESSURE", "MAYBE")]
    [InlineData("EARTH_TIDES", "MAYBE")]
    [InlineData("INTRACK_THRUST", "MAYBE")]
    public void ReadFromString_KeywordNotOneOfItsValues_ThrowsCdmParseException(string keyword, string value)
    {
        var baseXml = EnumeratedKeywordCdmXml();
        Assert.True(Cdm.ValidateSchemaFromXml(baseXml).IsValid);
        var xml = NdmDocuments.WithElementValue(baseXml, keyword, value);
        // The schema, the oracle that the value is not one of the keyword's, rejects the document
        Assert.False(Cdm.ValidateSchemaFromXml(xml).IsValid);

        // Before: the FormatException of CdmEnumExtensions, not the reader's documented exception
        void AssertNamesKeyword(CdmParseException ex)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.IsType<FormatException>(ex.InnerException);
        }

        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml)));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(RemoveNamespaces(xml))));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromStream(stream)));
        NdmDocuments.WithTempFile(xml, file => AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromFile(file))));
    }

    // A comma is not in the lexical space of xsd:double (XML Schema Part 2, 3.2.5.1: decimal digits, a period, an exponent,
    // the special values), the type of every real value of CDM/XML (6.4.2.2), and CCSDS 508.0-B-1 writes a value with a
    // period as decimal point and no blank inside it (6.3.2.2 to 6.3.2.4). AllowThousands dropped every comma whatever its
    // place, so a decimal comma read as another number (#717, #619): MISS_DISTANCE 118,1 as 1181 m, 0,715 as 715 m,
    // COLLISION_PROBABILITY 4,835E-05 as 0.04835, CR_R 4,1 as 41 m², X 2,570.097065 km as 2 570 097.065 m
    [Theory]
    [InlineData("MISS_DISTANCE", "118,1")]
    [InlineData("MISS_DISTANCE", "1,1,8")]
    [InlineData("MISS_DISTANCE", "0,715")]
    [InlineData("MISS_DISTANCE", "1,715")]
    [InlineData("MISS_DISTANCE", "7,,1,5")]
    [InlineData("COLLISION_PROBABILITY", "4,835E-05")]
    [InlineData("CR_R", "4,1")]
    [InlineData("X", "2,570.097065")]
    [InlineData("X_DOT", "7,5")]
    [InlineData("MASS", "1,2")]
    public void ReadFromString_CommaInValue_ThrowsCdmParseException(string keyword, string value)
    {
        var xml = NdmDocuments.WithElementValue(ManualCdmXml(), keyword, value);
        var ex = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml));
        Assert.Equal($"Invalid numeric value for {keyword}: '{value}'.", ex.Message);
    }

    [Theory]
    [InlineData("MISS_DISTANCE", " 118.1 ", 118.1)]
    [InlineData("MISS_DISTANCE", "+1.181E2", 118.1)]
    [InlineData("MISS_DISTANCE", "1181e-1", 118.1)]
    [InlineData("COLLISION_PROBABILITY", "4.835E-05", 4.835e-5)]
    [InlineData("COLLISION_PROBABILITY", ".5", 0.5)]
    public void ReadFromString_XsdDoubleLexicalForms_AreRead(string keyword, string value, double expected)
    {
        // The forms of the xsd:double lexical space a writer uses (a sign, no integer digit, an exponent of either case,
        // whitespace the schema collapses) read as before, bit for bit the double nearest the decimal value
        var cdm = Cdm.ReadFromString(NdmDocuments.WithElementValue(ManualCdmXml(), keyword, value));
        var read = keyword == "MISS_DISTANCE" ? cdm.RelativeMetadataData.MissDistanceMeters : cdm.RelativeMetadataData.CollisionProbability!.Value;
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(read));
    }

    // A text that double.TryParse reads as NaN or ±Infinity ("NaN", "Infinity" in any case, an overflow such as "1e400")
    // is not a real number (IEEE 754). CCSDS 508.0-B-1 writes a KVN value in fixed-point or floating-point digits up to about
    // 1.798E+308 (6.3.2.2, 6.3.2.3), which holds neither, but a CDM/XML value follows the xsd:double of XML Schema Part 2
    // (6.4.2.2), whose value space holds NaN and ±INF (CCSDS 508.0-B-1; confirmed by S. Guillet, 2026-11-17): the
    // reader refuses it as a rule of the library, as #708 expects. The rows cover the relative metadata, a state vector
    // and the covariance, in the first segment (the first element of that name in the document)
    [Theory]
    [InlineData("MISS_DISTANCE", "NaN")]
    [InlineData("MISS_DISTANCE", "Infinity")]
    [InlineData("MISS_DISTANCE", "1e400")]
    [InlineData("RELATIVE_SPEED", "-Infinity")]
    [InlineData("COLLISION_PROBABILITY", "1e400")]
    [InlineData("X", "NaN")]
    [InlineData("X_DOT", "nan")]
    [InlineData("CR_R", "Infinity")]
    [InlineData("CT_R", "NaN")]
    public void ReadFromString_NonFiniteValue_ThrowsCdmParseException(string keyword, string value)
    {
        var xml = NdmDocuments.WithElementValue(ManualCdmXml(), keyword, value);

        // Before: the CDM loaded with the keyword NaN or ±Infinity, and CdmValidator reported it valid
        void AssertNamesKeyword(CdmParseException ex)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.Contains("not a finite number", ex.Message);
        }

        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml)));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => new CdmReader().ReadFromString(xml)));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromStream(stream)));
        NdmDocuments.WithTempFile(xml, file => AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromFile(file))));
    }

    // A finite text in km or km/s whose value in m or m/s, the text times 1000, is beyond the range of a double
    // (double.MaxValue = 1.797...e308, so 1e306 km = 1e309 m): IEEE 754 rounds the product to +Infinity (-Infinity for
    // -1e306), which is no real number, so the reader refuses it as it refuses a non-finite text (#708). The rows are the
    // conversions of the reader: a position (positionTypeUR, km), a velocity (km/s) and a relative velocity given in km/s
    // (dvType: m/s in the writer's documents); a lengthTypeUO field in km throws for its units since #619
    // (ReadFromString_OptionalUnitLengthNotInMeters_ThrowsCdmParseException)
    [Theory]
    [InlineData("X", "km", "1e306")]
    [InlineData("Z", "km", "-1e306")]
    [InlineData("X_DOT", "km/s", "1e306")]
    [InlineData("RELATIVE_VELOCITY_T", "km/s", "1e306")]
    public void ReadFromString_FiniteValueOverflowingInSiUnits_ThrowsCdmParseException(string keyword, string units, string value)
    {
        var document = XDocument.Parse(NdmDocuments.WithElementValue(ManualCdmXml(), keyword, value));
        document.Descendants().First(e => e.Name.LocalName == keyword).SetAttributeValue("units", units);
        var xml = document.ToString();
        Assert.False(double.IsFinite(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture) * 1000.0));

        // Before: the CDM loaded with the keyword at ±Infinity in m or m/s, and Cdm.Validate reported it valid
        void AssertNamesKeyword(CdmParseException ex)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.Contains(units, ex.Message);
            Assert.Contains("not a finite number", ex.Message);
        }

        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml)));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => new CdmReader().ReadFromString(xml)));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromStream(stream)));
        NdmDocuments.WithTempFile(xml, file => AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromFile(file))));
    }

    [Fact]
    public void ReadFromString_LargestValueFiniteInSiUnits_ReadsTheProduct()
    {
        // The largest km text whose product by 1000 is finite reads as that product, the conversion unchanged (1e305 km
        // = 1e308 m, below double.MaxValue), as a value in m up to double.MaxValue
        var document = XDocument.Parse(NdmDocuments.WithElementValue(ManualCdmXml(), "X", "1e305"));
        document.Descendants().First(e => e.Name.LocalName == "X").SetAttributeValue("units", "km");
        var cdm = Cdm.ReadFromString(document.ToString());
        Assert.Equal(BitConverter.DoubleToInt64Bits(1e305 * 1000.0), BitConverter.DoubleToInt64Bits(cdm.Segments[0].Data!.StateVector!.PositionMeters.X));

        var inMeters = XDocument.Parse(NdmDocuments.WithElementValue(ManualCdmXml(), "MISS_DISTANCE", "1.7976931348623157E+308"));
        Assert.Equal("m", inMeters.Descendants().First(e => e.Name.LocalName == "MISS_DISTANCE").Attribute("units")!.Value);
        Assert.Equal(double.MaxValue, Cdm.ReadFromString(inMeters.ToString()).RelativeMetadataData.MissDistanceMeters);
    }

    [Fact]
    public void ReadFromString_FiniteExtremeMissDistance_ReadsTheValueOfTheText()
    {
        // Zero, -0 and a value near the least normal double are finite: read as the value of their text (the writer's
        // MISS_DISTANCE is in m)
        var units = XDocument.Parse(ManualCdmXml()).Descendants().First(e => e.Name.LocalName == "MISS_DISTANCE").Attribute("units")!.Value;
        Assert.Equal("m", units);
        foreach (var text in new[] { "0", "-0", "1.0e-300" })
        {
            var cdm = Cdm.ReadFromString(NdmDocuments.WithElementValue(ManualCdmXml(), "MISS_DISTANCE", text));
            double expected = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(cdm.RelativeMetadataData.MissDistanceMeters));
        }
    }

    [Fact]
    public void Validate_NegativeMissDistance_IsInvalid()
    {
        // A CDM built from its records with a negative miss distance: not non-negative, so invalid. A NaN miss distance,
        // reported invalid since #708, no longer builds (#717)
        var read = Cdm.ReadFromString(ManualCdmXml());
        Assert.True(read.Validate().IsValid);
        var cdm = new Cdm(read.Header, read.RelativeMetadataData with { MissDistanceMeters = -1.0 }, read.Segments);
        var result = cdm.Validate();
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Miss distance must be non-negative"));
    }

    // The real values of the CDM records, by record: every double, double?, Vector3 and Matrix property, which the reader
    // fills from an xsd:double it reads as a real number (#708) and the writer writes (#717)
    private static readonly Dictionary<Type, string[]> RealProperties = new()
    {
        [typeof(CdmRelativeMetadataData)] = new[]
        {
            nameof(CdmRelativeMetadataData.MissDistanceMeters), nameof(CdmRelativeMetadataData.RelativeSpeedMetersPerSecond),
            nameof(CdmRelativeMetadataData.ScreenVolumeXMeters), nameof(CdmRelativeMetadataData.ScreenVolumeYMeters),
            nameof(CdmRelativeMetadataData.ScreenVolumeZMeters), nameof(CdmRelativeMetadataData.CollisionProbability)
        },
        [typeof(CdmRelativeStateVector)] = new[]
        {
            nameof(CdmRelativeStateVector.RelativePositionRtnMeters), nameof(CdmRelativeStateVector.RelativeVelocityRtnMetersPerSecond)
        },
        [typeof(CdmOdParameters)] = new[]
        {
            nameof(CdmOdParameters.RecommendedOdSpanDays), nameof(CdmOdParameters.ActualOdSpanDays),
            nameof(CdmOdParameters.ResidualsAcceptedPercent), nameof(CdmOdParameters.WeightedRms)
        },
        [typeof(CdmAdditionalParameters)] = new[]
        {
            nameof(CdmAdditionalParameters.AreaPcSquareMeters), nameof(CdmAdditionalParameters.AreaDragSquareMeters),
            nameof(CdmAdditionalParameters.AreaSrpSquareMeters), nameof(CdmAdditionalParameters.MassKilograms),
            nameof(CdmAdditionalParameters.CdAreaOverMass), nameof(CdmAdditionalParameters.CrAreaOverMass),
            nameof(CdmAdditionalParameters.ThrustAccelerationMetersPerSecondSquared), nameof(CdmAdditionalParameters.SedrWattsPerKilogram)
        },
        [typeof(CdmStateVector)] = new[] { nameof(CdmStateVector.PositionMeters), nameof(CdmStateVector.VelocityMetersPerSecond) },
        [typeof(CdmCovarianceMatrix)] = new[]
        {
            nameof(CdmCovarianceMatrix.StateCovarianceRtn),
            nameof(CdmCovarianceMatrix.CdrgR), nameof(CdmCovarianceMatrix.CdrgT), nameof(CdmCovarianceMatrix.CdrgN),
            nameof(CdmCovarianceMatrix.CdrgRdot), nameof(CdmCovarianceMatrix.CdrgTdot), nameof(CdmCovarianceMatrix.CdrgNdot),
            nameof(CdmCovarianceMatrix.CdrgDrg),
            nameof(CdmCovarianceMatrix.CsrpR), nameof(CdmCovarianceMatrix.CsrpT), nameof(CdmCovarianceMatrix.CsrpN),
            nameof(CdmCovarianceMatrix.CsrpRdot), nameof(CdmCovarianceMatrix.CsrpTdot), nameof(CdmCovarianceMatrix.CsrpNdot),
            nameof(CdmCovarianceMatrix.CsrpDrg), nameof(CdmCovarianceMatrix.CsrpSrp),
            nameof(CdmCovarianceMatrix.CthrR), nameof(CdmCovarianceMatrix.CthrT), nameof(CdmCovarianceMatrix.CthrN),
            nameof(CdmCovarianceMatrix.CthrRdot), nameof(CdmCovarianceMatrix.CthrTdot), nameof(CdmCovarianceMatrix.CthrNdot),
            nameof(CdmCovarianceMatrix.CthrDrg), nameof(CdmCovarianceMatrix.CthrSrp), nameof(CdmCovarianceMatrix.CthrThr)
        }
    };

    private static readonly double[] NonFiniteValues = { double.NaN, double.PositiveInfinity, double.NegativeInfinity };

    [Fact]
    public void RealProperties_ListEveryRealPropertyOfTheRecords()
    {
        // The list of the tests below is complete: a real property added to a record without its check fails here
        foreach (var (type, names) in RealProperties)
        {
            var real = type.GetProperties()
                .Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(double?) ||
                            p.PropertyType == typeof(Vector3) || p.PropertyType == typeof(Matrix))
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), real);
        }
    }

    public static TheoryData<string, string, double> NonFiniteScalarProperties()
    {
        var data = new TheoryData<string, string, double>();
        foreach (var (type, names) in RealProperties)
        foreach (var name in names)
        {
            var propertyType = type.GetProperty(name)!.PropertyType;
            if (propertyType != typeof(double) && propertyType != typeof(double?))
                continue;
            foreach (var value in NonFiniteValues)
                data.Add(type.Name, name, value);
        }

        return data;
    }

    // Sets a property through its init accessor, as an object initializer or a with expression does
    private static void Init(object record, string property, object? value)
    {
        try
        {
            record.GetType().GetProperty(property)!.SetValue(record, value);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    [Theory]
    [MemberData(nameof(NonFiniteScalarProperties))]
    public void Init_NonFiniteScalar_ThrowsArgumentOutOfRangeException(string record, string property, double value)
    {
        // Before, NaN and ±Infinity built, Validate() passed them (but the miss distance, #708) and WriteToString wrote
        // NaN, Infinity or -Infinity, which the reader refuses
        var type = RealProperties.Keys.Single(t => t.Name == record);
        var instance = Activator.CreateInstance(type)!;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Init(instance, property, value));
        Assert.Equal(property, ex.ParamName);

        // A finite value, and null for an optional one, is stored as given
        Init(instance, property, -double.MaxValue);
        Assert.Equal(-double.MaxValue, Convert.ToDouble(type.GetProperty(property)!.GetValue(instance)));
        if (type.GetProperty(property)!.PropertyType == typeof(double?))
        {
            Init(instance, property, null);
            Assert.Null(type.GetProperty(property)!.GetValue(instance));
        }
    }

    public static TheoryData<string, string, int, double> NonFiniteVectorComponents()
    {
        var data = new TheoryData<string, string, int, double>();
        foreach (var (type, names) in RealProperties)
        foreach (var name in names.Where(n => type.GetProperty(n)!.PropertyType == typeof(Vector3)))
        for (int component = 0; component < 3; component++)
        foreach (var value in NonFiniteValues)
            data.Add(type.Name, name, component, value);
        return data;
    }

    [Theory]
    [MemberData(nameof(NonFiniteVectorComponents))]
    public void Init_NonFiniteVectorComponent_ThrowsArgumentOutOfRangeException(string record, string property, int component, double value)
    {
        var type = RealProperties.Keys.Single(t => t.Name == record);
        var instance = Activator.CreateInstance(type)!;
        var c = new[] { 1.5, -2.5, 3.5 };
        c[component] = value;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Init(instance, property, new Vector3(c[0], c[1], c[2])));
        Assert.Equal(property, ex.ParamName);
    }

    public static TheoryData<int, int, double> NonFiniteCovarianceTerms()
    {
        var data = new TheoryData<int, int, double>();
        for (int row = 0; row < 6; row++)
        for (int column = 0; column < 6; column++)
        foreach (var value in NonFiniteValues)
            data.Add(row, column, value);
        return data;
    }

    [Theory]
    [MemberData(nameof(NonFiniteCovarianceTerms))]
    public void Init_NonFiniteCovarianceTerm_ThrowsArgumentOutOfRangeException(int row, int column, double value)
    {
        // The 21 terms of the lower triangle are written (CR_R ... CNDOT_NDOT); a term of the upper triangle is refused too,
        // the record holding no non-finite value
        var matrixTerms = new double[6, 6];
        for (int i = 0; i < 6; i++)
            matrixTerms[i, i] = 1.0;
        matrixTerms[row, column] = value;
        var matrix = new Matrix(matrixTerms);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new CdmCovarianceMatrix { StateCovarianceRtn = matrix });
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);
        ex = Assert.Throws<ArgumentOutOfRangeException>(() => CdmCovarianceMatrix.FromRtnCovariance(matrix));
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);
    }

    public static TheoryData<int, int> NotSixBySixSizes() => new()
    {
        { 3, 3 }, { 6, 5 }, { 5, 6 }, { 7, 7 }, { 6, 1 }, { 1, 6 }, { 0, 0 }
    };

    [Theory]
    [MemberData(nameof(NotSixBySixSizes))]
    public void Init_CovarianceNotSixBySix_ThrowsArgumentExceptionNamingTheProperty(int rows, int columns)
    {
        // #730: the 21 terms CR_R ... CNDOT_NDOT of CCSDS 508.0-B-1 table 3-5 are the lower triangle of a 6×6 matrix; the
        // init accessor accepted any size, and CdmWriter then threw IndexOutOfRangeException (Cdm.Validate reported it only
        // when it ran)
        var matrix = new Matrix(rows, columns);
        var ex = Assert.Throws<ArgumentException>(() => new CdmCovarianceMatrix { StateCovarianceRtn = matrix });
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);
        Assert.Contains("6x6", ex.Message);

        // A with expression on a covariance read from a document, the same refusal
        var read = Cdm.ReadFromString(ManualCdmXml()).Segments[0].Data!.CovarianceMatrix!;
        ex = Assert.Throws<ArgumentException>(() => read with { StateCovarianceRtn = matrix });
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);
    }

    [Fact]
    public void Init_DefaultCovarianceMatrix_ThrowsArgumentExceptionNamingTheProperty()
    {
        // #730: default(Matrix), 0×0 over a null array, was accepted, and CdmWriter then threw NullReferenceException
        var ex = Assert.Throws<ArgumentException>(() => new CdmCovarianceMatrix { StateCovarianceRtn = default });
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);

        // A size error before a term error: a 3×3 matrix with a NaN throws ArgumentException for its size, not the
        // ArgumentOutOfRangeException of a term (Assert.Throws checks the exact type)
        var matrixTerms = new double[3, 3];
        matrixTerms[0, 0] = double.NaN;
        var matrix = new Matrix(matrixTerms);
        ex = Assert.Throws<ArgumentException>(() => new CdmCovarianceMatrix { StateCovarianceRtn = matrix });
        Assert.Equal(nameof(CdmCovarianceMatrix.StateCovarianceRtn), ex.ParamName);

        // The record without an init holds a 6×6 zero matrix, written and read back
        var covariance = new CdmCovarianceMatrix();
        Assert.Equal(6, covariance.StateCovarianceRtn.Rows);
        Assert.Equal(6, covariance.StateCovarianceRtn.Columns);
    }

    [Fact]
    public void With_NonFiniteCollisionProbability_ThrowsAndTheWrittenCdmReadsBack()
    {
        // The reproduction of #717: a with expression on a CDM read back, which built and wrote <COLLISION_PROBABILITY>NaN
        // that the reader refuses; every CDM the records can hold is written and read back
        var read = Cdm.ReadFromString(ManualCdmXml());
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => read.RelativeMetadataData with { CollisionProbability = double.NaN });
        Assert.Equal(nameof(CdmRelativeMetadataData.CollisionProbability), ex.ParamName);

        var extreme = new Cdm(read.Header, read.RelativeMetadataData with { CollisionProbability = double.Epsilon }, read.Segments);
        var back = Cdm.ReadFromString(extreme.WriteToString());
        Assert.Equal(BitConverter.DoubleToInt64Bits(double.Epsilon), BitConverter.DoubleToInt64Bits(back.RelativeMetadataData.CollisionProbability!.Value));
    }

    public static TheoryData<int, int, double> NonFiniteLowerTriangleTerms()
    {
        var data = new TheoryData<int, int, double>();
        foreach (var (row, column) in new[] { (0, 0), (1, 0), (5, 5) })
        foreach (var value in NonFiniteValues)
            data.Add(row, column, value);
        return data;
    }

    [Theory]
    [MemberData(nameof(NonFiniteLowerTriangleTerms))]
    public void Init_MatrixChangedAfterwards_TheRecordKeepsTheTermsItChecked(int row, int column, double value)
    {
        // The review of #717: Matrix wrapped a double[,] with a public Set, so a record that kept the caller's matrix held the
        // NaN or ±Infinity set after the check, passed Validate, wrote it (CR_R, CT_R, CNDOT_NDOT) and its own reader refused
        // it. Matrix is immutable since 11.0.0 (#668, R26); the record still copies, which the internal setter checks
        var matrix = Matrix.Identity(6);
        var byInit = new CdmCovarianceMatrix { StateCovarianceRtn = matrix };
        var byFactory = CdmCovarianceMatrix.FromRtnCovariance(matrix);

        matrix.SetTerm(row, column, value);

        foreach (var covariance in new[] { byInit, byFactory })
        {
            Assert.Equal(row == column ? 1.0 : 0.0, covariance.StateCovarianceRtn.Get(row, column));
            Assert.Equal(Matrix.Identity6x6(), covariance.StateCovarianceRtn);
        }
    }

    [Theory]
    [MemberData(nameof(NonFiniteLowerTriangleTerms))]
    public void StateCovarianceRtn_ReturnedMatrixChanged_TheCdmStillWritesAndReadsBack(int row, int column, double value)
    {
        // A CDM read from a document: the matrix its getter returns is the caller's, so a term set on it reaches neither the
        // record, nor Validate, nor the document written
        var read = Cdm.ReadFromString(ManualCdmXml());
        var covariance = read.Segments[0].Data!.CovarianceMatrix!;
        var expected = covariance.StateCovarianceRtn.Get(row, column);

        covariance.StateCovarianceRtn.SetTerm(row, column, value);

        Assert.Equal(expected, covariance.StateCovarianceRtn.Get(row, column));
        Assert.True(read.Validate().IsValid);
        var back = Cdm.ReadFromString(read.WriteToString());
        Assert.Equal(expected, back.Segments[0].Data!.CovarianceMatrix!.StateCovarianceRtn.Get(row, column));
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
            var tank = new FuelTank("tank" + -naifId, 20.0, 20.0, "model", "sn1");
            secondarySpacecraft.AddFuelTank(tank);
            secondarySpacecraft.AddEngine(new Engine("engine" + -naifId, 300.0, 10.0, tank, "model", "sn1"));
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

    internal static EncounterCase CreateManualEncounterCase(bool withEngines = false,
        EncounterQualityFlags qualityFlags = EncounterQualityFlags.None)
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
            protectedState,
            150.0,
            new Clock("ProtectedAlpha_CLK", 256),
            sectionalArea: 2.0,
            dragCoefficient: 2.2,
            cosparId: "2026-001A",
            solarRadiationCoefficient: 1.2,
            hardBodyRadius: 8.0);
        var secondarySpacecraft = new Spacecraft(
            -41002,
            "SecondaryBravo",
            95.0,
            secondaryState,
            120.0,
            new Clock("SecondaryBravo_CLK", 256),
            sectionalArea: 1.5,
            dragCoefficient: 2.1,
            cosparId: "2026-001B",
            solarRadiationCoefficient: 1.1,
            hardBodyRadius: 3.0);
        if (withEngines)
        {
            foreach (var spacecraft in new[] { protectedSpacecraft, secondarySpacecraft })
            {
                var tank = new FuelTank("tank" + -spacecraft.NaifId, 20.0, 20.0, "model", "sn1");
                spacecraft.AddFuelTank(tank);
                spacecraft.AddEngine(new Engine("engine" + -spacecraft.NaifId, 300.0, 10.0, tank, "model", "sn1"));
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
            qualityFlags);

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
            orbit,
            150.0,
            new Clock($"{name}_CLK", 256),
            sectionalArea: 2.0,
            dragCoefficient: 2.2,
            cosparId: $"2026-001{name[0]}",
            solarRadiationCoefficient: 1.2,
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
            orbit,
            150.0,
            new Clock($"{name}_CLK", 256),
            sectionalArea: 2.0,
            dragCoefficient: 2.2,
            cosparId: $"2026-001{name[0]}",
            solarRadiationCoefficient: 1.2,
            hardBodyRadius: hardBodyRadius);
    }

    private static Matrix BuildStateCovariance(double[] diagonal, params (int Row, int Column, double Value)[] offDiagonalEntries)
    {
        var covarianceTerms = new double[6, 6];
        for (int index = 0; index < diagonal.Length; index++)
        {
            covarianceTerms[index, index] = diagonal[index];
        }

        foreach (var entry in offDiagonalEntries)
        {
            covarianceTerms[entry.Row, entry.Column] = entry.Value;
            covarianceTerms[entry.Column, entry.Row] = entry.Value;
        }
        var covariance = new Matrix(covarianceTerms);

        return covariance;
    }

    private static Matrix AddMatrices(Matrix left, Matrix right)
    {
        var resultTerms = new double[left.Rows, left.Columns];
        for (int row = 0; row < left.Rows; row++)
        {
            for (int column = 0; column < left.Columns; column++)
            {
                resultTerms[row, column] = left.Get(row, column) + right.Get(row, column);
            }
        }
        var result = new Matrix(resultTerms);

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
        var matrixTerms = new double[3, 3];
        matrixTerms[0, 0] = radial.X;
        matrixTerms[0, 1] = radial.Y;
        matrixTerms[0, 2] = radial.Z;
        matrixTerms[1, 0] = transverse.X;
        matrixTerms[1, 1] = transverse.Y;
        matrixTerms[1, 2] = transverse.Z;
        matrixTerms[2, 0] = normal.X;
        matrixTerms[2, 1] = normal.Y;
        matrixTerms[2, 2] = normal.Z;
        var matrix = new Matrix(matrixTerms);
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
    public void ReadFromString_OptionalUnitLengths_AreMetersByDefault(string? units, double scale)
    {
        var cdm = Cdm.ReadFromString(OptionalUnitLengthsDocument(units).ToString());

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

    [Theory]
    [InlineData("MISS_DISTANCE", "km")]
    [InlineData("RELATIVE_POSITION_R", "km")]
    [InlineData("RELATIVE_POSITION_T", "km")]
    [InlineData("RELATIVE_POSITION_N", "km")]
    [InlineData("SCREEN_VOLUME_X", "km")]
    [InlineData("SCREEN_VOLUME_Y", "km")]
    [InlineData("SCREEN_VOLUME_Z", "km")]
    [InlineData("MISS_DISTANCE", "M")]
    [InlineData("MISS_DISTANCE", " m")]
    [InlineData("MISS_DISTANCE", "")]
    public void ReadFromString_OptionalUnitLengthNotInMeters_ThrowsCdmParseException(string keyword, string units)
    {
        // #619, decided by S. Guillet, 2026-11-17: lengthUnits of ndmxml-4.0.0-common-4.0.xsd enumerates m alone (an
        // xsd:string, so neither M nor a blank), as CCSDS 508.0-B-1 table 3-2; km was read and converted before, the one
        // value outside the standard that the reader accepted on these seven fields
        var document = OptionalUnitLengthsDocument("m");
        var schema = new CdmValidator().ValidateSchemaFromXml(document.ToString());
        Assert.True(schema.IsValid, FormatValidationIssues(schema.Errors));
        document.Descendants(keyword).Single().SetAttributeValue("units", units);
        var xml = document.ToString();
        Assert.False(new CdmValidator().ValidateSchemaFromXml(xml).IsValid);

        void AssertNamesKeyword(CdmParseException ex)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{units}'", ex.Message);
        }

        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(xml)));
        AssertNamesKeyword(Assert.Throws<CdmParseException>(() => new CdmReader().ReadFromString(xml)));

        // The other fields of the document read in m, as written
        Assert.Equal(715.0, Cdm.ReadFromString(OptionalUnitLengthsDocument("m").ToString()).RelativeMetadataData.MissDistanceMeters);
    }

    private static XDocument OptionalUnitLengthsDocument(string? units)
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
                // A screening volume the writer did not write, in the order of the schema's sequence
                element = new XElement(name);
                var next = relativeMetadata.Elements().FirstOrDefault(e => e.Name.LocalName is "SCREEN_ENTRY_TIME"
                    or "SCREEN_EXIT_TIME" or "COLLISION_PROBABILITY" or "COLLISION_PROBABILITY_METHOD");
                if (next == null)
                    relativeMetadata.Add(element);
                else
                    next.AddBeforeSelf(element);
            }

            element.Value = text;
            element.SetAttributeValue("units", units);
        }

        return document;
    }

    /// <summary>
    /// Dates outside the two forms of CCSDS 508.0-B-1 §6.3.2.6 (<c>yyyy-mm-ddThh:mm:ss[.d→d][Z]</c>,
    /// <c>yyyy-dddThh:mm:ss[.d→d][Z]</c>), the rows of #619's table; lxml 6.1.3 with the repository's NDM/XML 4.0.0
    /// schema rejects each but <c>1.5</c>, the bare-number branch of <c>epochType</c>, which §6.3.2.6 does not allow.
    /// </summary>
    [Theory]
    [InlineData("13 March 2010 22:37:52.618")]
    [InlineData("03/13/2010 22:37:52.618")]
    [InlineData("2010-03-13 22:37:52.618")]
    [InlineData("2010-3-13T22:37:52.618")]
    [InlineData("Sat, 13 Mar 2010 22:37:52 GMT")]
    [InlineData("2010-03-13")]
    [InlineData("22:37:52.618")]
    [InlineData("1,5")]
    [InlineData("1.5")]
    public void ReadFromString_DateOutsideTheTwoForms_ThrowsCdmParseException(string text)
    {
        // Before #619: the TCA read as the date DateTime.TryParse gave, the day of the run for 22:37:52.618, 5 January of
        // the year of the run for 1,5 and 1.5
        var original = CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2014, 1, 21, 17, 42, 47, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-EPOCH-619"
        });
        foreach (var field in new[] { "TCA", "CREATION_DATE" })
        {
            var document = XDocument.Parse(RemoveNamespaces(original.WriteToString()));
            document.Descendants(field).Single().Value = text;
            var ex = Assert.Throws<CdmParseException>(() => Cdm.ReadFromString(document.ToString()));
            Assert.Contains(field, ex.Message);
            Assert.Contains($"'{text}'", ex.Message);
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
