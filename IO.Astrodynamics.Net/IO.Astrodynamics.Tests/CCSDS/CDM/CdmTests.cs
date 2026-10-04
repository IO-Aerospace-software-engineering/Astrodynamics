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
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public CdmTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
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
        Assert.True(protectedSegment.Metadata.SolarRadPressure);
        Assert.False(protectedSegment.Metadata.IntrackThrust);

        Assert.Equal("41002", secondarySegment.Metadata.ObjectDesignator);
        Assert.Equal("NAIF", secondarySegment.Metadata.CatalogName);
        Assert.Equal(encounter.SecondaryObject.Name, secondarySegment.Metadata.ObjectName);
        Assert.Equal(((Spacecraft)encounter.SecondaryObject).CosparId, secondarySegment.Metadata.InternationalDesignator);
        Assert.Equal(ObjectType.Payload, secondarySegment.Metadata.ObjectType);
        Assert.Equal("IO.Astrodynamics", secondarySegment.Metadata.EphemerisName);
        Assert.Equal(CdmCovarianceMethod.Calculated, secondarySegment.Metadata.CovarianceMethod);
        Assert.Equal(CdmManeuverable.Yes, secondarySegment.Metadata.Maneuverable);
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

    private static EncounterCase CreateManualEncounterCase()
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
