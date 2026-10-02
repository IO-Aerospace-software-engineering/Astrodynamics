// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SSA;

namespace IO.Astrodynamics.CCSDS.CDM;

internal static class CdmExporter
{
    private sealed record ResolvedCovariance(Matrix? CovarianceIcrf, bool UsedStaleCovariance);

    public static Cdm Export(EncounterCase encounterCase, CdmExportOptions? options)
    {
        ArgumentNullException.ThrowIfNull(encounterCase);
        options ??= new CdmExportOptions();

        if (encounterCase.ProtectedState == null || encounterCase.SecondaryState == null)
        {
            throw new InvalidOperationException(
                "EncounterCase does not retain participant TCA states. Re-run conjunction assessment with the current version before exporting to CDM.");
        }

        var protectedCovariance = ResolveCovariance(encounterCase.ProtectedAsset.Spacecraft, encounterCase.ProtectedState);
        var secondaryCovariance = ResolveCovariance(encounterCase.SecondaryObject, encounterCase.SecondaryState);

        if (protectedCovariance.CovarianceIcrf == null)
        {
            throw new InvalidOperationException(
                "Protected object covariance is required to export a standards-compliant CDM.");
        }

        if (secondaryCovariance.CovarianceIcrf == null)
        {
            throw new InvalidOperationException(
                "Secondary object covariance is required to export a standards-compliant CDM.");
        }

        var protectedMetadata = MergeMetadata(
            InferMetadata(
                encounterCase,
                CdmObjectRole.Object1,
                encounterCase.ProtectedAsset.Spacecraft,
                encounterCase.ProtectedState,
                protectedCovariance.UsedStaleCovariance),
            options.ProtectedParticipantMetadata);

        var secondaryMetadata = MergeMetadata(
            InferMetadata(
                encounterCase,
                CdmObjectRole.Object2,
                encounterCase.SecondaryObject,
                encounterCase.SecondaryState,
                secondaryCovariance.UsedStaleCovariance),
            options.SecondaryParticipantMetadata);

        var protectedRadius = encounterCase.ProtectedAsset.Spacecraft.HardBodyRadius;
        var secondaryRadius = System.Math.Max(
            encounterCase.CollisionRisk.CombinedHardBodyRadiusMeters - protectedRadius,
            0.0);

        var segments = new[]
        {
            BuildSegment(
                protectedMetadata,
                encounterCase.ProtectedAsset.Spacecraft,
                encounterCase.ProtectedState,
                RotateCovarianceToRtn(protectedCovariance.CovarianceIcrf!.Value, encounterCase.ProtectedState),
                protectedRadius,
                protectedCovariance.UsedStaleCovariance),
            BuildSegment(
                secondaryMetadata,
                encounterCase.SecondaryObject,
                encounterCase.SecondaryState,
                RotateCovarianceToRtn(secondaryCovariance.CovarianceIcrf!.Value, encounterCase.SecondaryState),
                secondaryRadius,
                secondaryCovariance.UsedStaleCovariance)
        };

        var header = new CdmHeader
        {
            CreationDateUtc = EnsureUtc(options.CreationDateUtc ?? DateTime.UtcNow),
            Originator = options.Originator,
            MessageFor = options.MessageFor ?? protectedMetadata.ObjectName,
            MessageId = string.IsNullOrWhiteSpace(options.MessageId)
                ? CreateMessageId(encounterCase)
                : options.MessageId
        };

        return new Cdm(
            header,
            BuildRelativeMetadataData(encounterCase, options),
            segments);
    }

    private static CdmRelativeMetadataData BuildRelativeMetadataData(EncounterCase encounterCase, CdmExportOptions options)
    {
        var comments = new List<string>();
        if (encounterCase.EncounterState.QualityFlags != EncounterQualityFlags.None)
        {
            comments.Add($"Encounter quality flags: {encounterCase.EncounterState.QualityFlags}.");
        }

        return new CdmRelativeMetadataData
        {
            Comments = comments,
            TcaUtc = EnsureUtc(encounterCase.EncounterState.Epoch.ToUTC().DateTime),
            MissDistanceMeters = encounterCase.EncounterState.MissDistanceMeters,
            RelativeSpeedMetersPerSecond = encounterCase.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude(),
            RelativeStateVector = options.IncludeRelativeStateVector
                ? new CdmRelativeStateVector
                {
                    RelativePositionRtnMeters = encounterCase.EncounterState.RelativeState.RelativePositionRtn,
                    RelativeVelocityRtnMetersPerSecond = encounterCase.EncounterState.RelativeState.RelativeVelocityRtn
                }
                : null,
            StartScreenPeriodUtc = options.IncludeScreeningWindow
                ? EnsureUtc(encounterCase.ScreeningWindow.StartDate.ToUTC().DateTime)
                : null,
            StopScreenPeriodUtc = options.IncludeScreeningWindow
                ? EnsureUtc(encounterCase.ScreeningWindow.EndDate.ToUTC().DateTime)
                : null,
            CollisionProbability = encounterCase.CollisionRisk.ProbabilityOfCollision,
            CollisionProbabilityMethod = options.CollisionProbabilityMethod ?? InferCollisionProbabilityMethod(encounterCase)
        };
    }

    private static CdmSegment BuildSegment(
        CdmParticipantMetadata metadata,
        ILocalizable source,
        StateVector state,
        Matrix covarianceRtn,
        double hardBodyRadiusMeters,
        bool usedStaleCovariance)
    {
        var dataComments = usedStaleCovariance
            ? new[] { "Covariance exported from the participant's initial state because no covariance was available at TCA." }
            : Array.Empty<string>();

        return new CdmSegment
        {
            Metadata = metadata,
            Data = new CdmData
            {
                Comments = dataComments,
                AdditionalParameters = BuildAdditionalParameters(source, hardBodyRadiusMeters),
                StateVector = new CdmStateVector
                {
                    PositionMeters = state.Position,
                    VelocityMetersPerSecond = state.Velocity
                },
                CovarianceMatrix = CdmCovarianceMatrix.FromRtnCovariance(covarianceRtn)
            }
        };
    }

    private static CdmAdditionalParameters? BuildAdditionalParameters(ILocalizable source, double hardBodyRadiusMeters)
    {
        double? mass = source.Mass > 0.0 ? source.Mass : null;
        double? areaPc = hardBodyRadiusMeters > 0.0 ? System.Math.PI * hardBodyRadiusMeters * hardBodyRadiusMeters : null;

        if (source is Spacecraft spacecraft)
        {
            var totalMass = spacecraft.GetTotalMass();
            return new CdmAdditionalParameters
            {
                MassKilograms = totalMass > 0.0 ? totalMass : mass,
                AreaPcSquareMeters = areaPc,
                AreaDragSquareMeters = spacecraft.SectionalArea,
                AreaSrpSquareMeters = spacecraft.SectionalArea,
                CdAreaOverMass = totalMass > 0.0 ? spacecraft.DragCoefficient * spacecraft.SectionalArea / totalMass : null,
                CrAreaOverMass = totalMass > 0.0 ? spacecraft.SolarRadiationCoeff * spacecraft.SectionalArea / totalMass : null
            };
        }

        if (mass.HasValue || areaPc.HasValue)
        {
            return new CdmAdditionalParameters
            {
                MassKilograms = mass,
                AreaPcSquareMeters = areaPc
            };
        }

        return null;
    }

    private static CdmParticipantMetadata InferMetadata(
        EncounterCase encounterCase,
        CdmObjectRole role,
        ILocalizable source,
        StateVector state,
        bool usedStaleCovariance)
    {
        var comments = new List<string>();
        if (usedStaleCovariance)
        {
            comments.Add("Participant covariance at TCA was unavailable; initial state covariance was exported instead.");
        }

        if (source.IsSpiceBacked)
        {
            comments.Add("Participant ephemeris is backed by SPICE.");
        }

        var maneuverable = role == CdmObjectRole.Object1
            ? encounterCase.ProtectedAsset.ManeuverConstraints.MaxDeltaVMetersPerSecond > 0.0
                ? CdmManeuverable.Yes
                : CdmManeuverable.No
            : source is Spacecraft
                ? CdmManeuverable.Yes
                : CdmManeuverable.NotApplicable;

        return new CdmParticipantMetadata
        {
            Comments = comments,
            Object = role,
            ObjectDesignator = InferObjectDesignator(source),
            CatalogName = "NAIF",
            ObjectName = source.Name,
            InternationalDesignator = InferInternationalDesignator(source),
            ObjectType = InferObjectType(source),
            EphemerisName = source.IsSpiceBacked ? "SPICE" : "IO.Astrodynamics",
            CovarianceMethod = CdmCovarianceMethod.Calculated,
            Maneuverable = maneuverable,
            OrbitCenter = state.Observer.Name,
            ReferenceFrame = MapReferenceFrame(state.Frame.Name),
            SolarRadPressure = source is Spacecraft spacecraft ? spacecraft.SolarRadiationCoeff > 0.0 : null,
            IntrackThrust = source is Spacecraft craft ? craft.Engines.Count > 0 : null
        };
    }

    private static string InferObjectDesignator(ILocalizable source)
    {
        return System.Math.Abs(source.NaifId).ToString(CultureInfo.InvariantCulture);
    }

    private static string InferInternationalDesignator(ILocalizable source)
    {
        if (source is Spacecraft spacecraft && !string.IsNullOrWhiteSpace(spacecraft.CosparId))
        {
            return spacecraft.CosparId;
        }

        return System.Math.Abs(source.NaifId).ToString(CultureInfo.InvariantCulture);
    }

    private static ObjectType InferObjectType(ILocalizable source)
    {
        return source switch
        {
            Spacecraft => ObjectType.Payload,
            _ => ObjectType.Other
        };
    }

    private static CdmParticipantMetadata MergeMetadata(
        CdmParticipantMetadata defaults,
        CdmParticipantMetadata? overrides)
    {
        if (overrides == null)
        {
            return defaults;
        }

        return defaults with
        {
            Comments = overrides.Comments.Count > 0 ? overrides.Comments : defaults.Comments,
            ObjectDesignator = overrides.ObjectDesignator ?? defaults.ObjectDesignator,
            CatalogName = overrides.CatalogName ?? defaults.CatalogName,
            ObjectName = overrides.ObjectName ?? defaults.ObjectName,
            InternationalDesignator = overrides.InternationalDesignator ?? defaults.InternationalDesignator,
            ObjectType = overrides.ObjectType ?? defaults.ObjectType,
            OperatorContactPosition = overrides.OperatorContactPosition ?? defaults.OperatorContactPosition,
            OperatorOrganization = overrides.OperatorOrganization ?? defaults.OperatorOrganization,
            OperatorPhone = overrides.OperatorPhone ?? defaults.OperatorPhone,
            OperatorEmail = overrides.OperatorEmail ?? defaults.OperatorEmail,
            EphemerisName = overrides.EphemerisName ?? defaults.EphemerisName,
            CovarianceMethod = overrides.CovarianceMethod ?? defaults.CovarianceMethod,
            Maneuverable = overrides.Maneuverable ?? defaults.Maneuverable,
            OrbitCenter = overrides.OrbitCenter ?? defaults.OrbitCenter,
            ReferenceFrame = overrides.ReferenceFrame ?? defaults.ReferenceFrame,
            GravityModel = overrides.GravityModel ?? defaults.GravityModel,
            AtmosphericModel = overrides.AtmosphericModel ?? defaults.AtmosphericModel,
            NBodyPerturbations = overrides.NBodyPerturbations ?? defaults.NBodyPerturbations,
            SolarRadPressure = overrides.SolarRadPressure ?? defaults.SolarRadPressure,
            EarthTides = overrides.EarthTides ?? defaults.EarthTides,
            IntrackThrust = overrides.IntrackThrust ?? defaults.IntrackThrust
        };
    }

    private static string InferCollisionProbabilityMethod(EncounterCase encounterCase)
    {
        return encounterCase.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed)
            ? "FOSTER-2D-MAX-PC"
            : "FOSTER-2D";
    }

    private static string CreateMessageId(EncounterCase encounterCase)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"CDM-{System.Math.Abs(encounterCase.ProtectedAsset.Spacecraft.NaifId)}-{System.Math.Abs(encounterCase.SecondaryObject.NaifId)}-{encounterCase.EncounterState.Epoch.ToUTC().DateTime:yyyyMMddHHmmssfff}");
    }

    private static ResolvedCovariance ResolveCovariance(ILocalizable source, StateVector state)
    {
        if (state.Covariance.HasValue)
        {
            return new ResolvedCovariance(state.Covariance.Value, false);
        }

        if (source.InitialOrbitalParameters is StateVector initialState && initialState.Covariance.HasValue)
        {
            return new ResolvedCovariance(initialState.Covariance.Value, true);
        }

        return new ResolvedCovariance(null, false);
    }

    private static CdmReferenceFrame MapReferenceFrame(string frameName)
    {
        return frameName.ToUpperInvariant() switch
        {
            "J2000" => CdmReferenceFrame.Gcrf,
            "ICRF" => CdmReferenceFrame.Gcrf,
            "GCRF" => CdmReferenceFrame.Gcrf,
            "EME2000" => CdmReferenceFrame.Eme2000,
            "ITRF" => CdmReferenceFrame.Itrf,
            "ITRF93" => CdmReferenceFrame.Itrf,
            _ => throw new InvalidOperationException(
                $"CDM export only supports J2000/ICRF/GCRF/EME2000/ITRF participant states. Encounter state frame '{frameName}' is not supported.")
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static Matrix RotateCovarianceToRtn(Matrix covarianceIcrf, StateVector referenceState)
    {
        return referenceState.RotateCovarianceToRtn(covarianceIcrf);
    }
}
