// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed record CdmHeader
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime CreationDateUtc { get; init; }

    public string Originator { get; init; } = string.Empty;

    public string? MessageFor { get; init; }

    public string MessageId { get; init; } = string.Empty;
}

public sealed record CdmRelativeStateVector
{
    public Vector3 RelativePositionRtnMeters { get; init; } = Vector3.Zero;

    public Vector3 RelativeVelocityRtnMetersPerSecond { get; init; } = Vector3.Zero;
}

public sealed record CdmRelativeMetadataData
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime TcaUtc { get; init; }

    public double MissDistanceMeters { get; init; }

    public double? RelativeSpeedMetersPerSecond { get; init; }

    public CdmRelativeStateVector? RelativeStateVector { get; init; }

    public DateTime? StartScreenPeriodUtc { get; init; }

    public DateTime? StopScreenPeriodUtc { get; init; }

    public CdmScreenVolumeFrame? ScreenVolumeFrame { get; init; }

    public CdmScreenVolumeShape? ScreenVolumeShape { get; init; }

    public double? ScreenVolumeXMeters { get; init; }

    public double? ScreenVolumeYMeters { get; init; }

    public double? ScreenVolumeZMeters { get; init; }

    public DateTime? ScreenEntryTimeUtc { get; init; }

    public DateTime? ScreenExitTimeUtc { get; init; }

    public double? CollisionProbability { get; init; }

    public string? CollisionProbabilityMethod { get; init; }
}

public sealed record CdmParticipantMetadata
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public CdmObjectRole Object { get; init; }

    public string? ObjectDesignator { get; init; }

    public string? CatalogName { get; init; }

    public string? ObjectName { get; init; }

    public string? InternationalDesignator { get; init; }

    public ObjectType? ObjectType { get; init; }

    public string? OperatorContactPosition { get; init; }

    public string? OperatorOrganization { get; init; }

    public string? OperatorPhone { get; init; }

    public string? OperatorEmail { get; init; }

    public string? EphemerisName { get; init; }

    public CdmCovarianceMethod? CovarianceMethod { get; init; }

    public CdmManeuverable? Maneuverable { get; init; }

    public string? OrbitCenter { get; init; }

    public CdmReferenceFrame? ReferenceFrame { get; init; }

    public string? GravityModel { get; init; }

    public string? AtmosphericModel { get; init; }

    public string? NBodyPerturbations { get; init; }

    public bool? SolarRadPressure { get; init; }

    public bool? EarthTides { get; init; }

    public bool? IntrackThrust { get; init; }
}

public sealed record CdmOdParameters
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime? TimeLastObservationStartUtc { get; init; }

    public DateTime? TimeLastObservationEndUtc { get; init; }

    public double? RecommendedOdSpanDays { get; init; }

    public double? ActualOdSpanDays { get; init; }

    public int? ObservationsAvailable { get; init; }

    public int? ObservationsUsed { get; init; }

    public int? TracksAvailable { get; init; }

    public int? TracksUsed { get; init; }

    public double? ResidualsAcceptedPercent { get; init; }

    public double? WeightedRms { get; init; }
}

public sealed record CdmAdditionalParameters
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public double? AreaPcSquareMeters { get; init; }

    public double? AreaDragSquareMeters { get; init; }

    public double? AreaSrpSquareMeters { get; init; }

    public double? MassKilograms { get; init; }

    public double? CdAreaOverMass { get; init; }

    public double? CrAreaOverMass { get; init; }

    public double? ThrustAccelerationMetersPerSecondSquared { get; init; }

    public double? SedrWattsPerKilogram { get; init; }
}

public sealed record CdmStateVector
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public Vector3 PositionMeters { get; init; } = Vector3.Zero;

    public Vector3 VelocityMetersPerSecond { get; init; } = Vector3.Zero;
}

public sealed record CdmCovarianceMatrix
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public Matrix StateCovarianceRtn { get; init; } = new Matrix(6, 6);

    public double? CdrgR { get; init; }

    public double? CdrgT { get; init; }

    public double? CdrgN { get; init; }

    public double? CdrgRdot { get; init; }

    public double? CdrgTdot { get; init; }

    public double? CdrgNdot { get; init; }

    public double? CdrgDrg { get; init; }

    public double? CsrpR { get; init; }

    public double? CsrpT { get; init; }

    public double? CsrpN { get; init; }

    public double? CsrpRdot { get; init; }

    public double? CsrpTdot { get; init; }

    public double? CsrpNdot { get; init; }

    public double? CsrpDrg { get; init; }

    public double? CsrpSrp { get; init; }

    public double? CthrR { get; init; }

    public double? CthrT { get; init; }

    public double? CthrN { get; init; }

    public double? CthrRdot { get; init; }

    public double? CthrTdot { get; init; }

    public double? CthrNdot { get; init; }

    public double? CthrDrg { get; init; }

    public double? CthrSrp { get; init; }

    public double? CthrThr { get; init; }

    public static CdmCovarianceMatrix FromRtnCovariance(Matrix covarianceRtn, IReadOnlyList<string>? comments = null)
    {
        if (covarianceRtn.Rows != 6 || covarianceRtn.Columns != 6)
        {
            throw new ArgumentException("CDM covariance matrix must be 6x6.", nameof(covarianceRtn));
        }

        return new CdmCovarianceMatrix
        {
            Comments = comments ?? Array.Empty<string>(),
            StateCovarianceRtn = covarianceRtn
        };
    }
}

public sealed record CdmData
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public CdmOdParameters? OdParameters { get; init; }

    public CdmAdditionalParameters? AdditionalParameters { get; init; }

    public CdmStateVector? StateVector { get; init; }

    public CdmCovarianceMatrix? CovarianceMatrix { get; init; }
}

public sealed record CdmSegment
{
    public CdmParticipantMetadata? Metadata { get; init; }

    public CdmData? Data { get; init; }
}

public sealed record CdmExportOptions
{
    public DateTime? CreationDateUtc { get; init; }

    public string Originator { get; init; } = "IO.Astrodynamics";

    public string? MessageFor { get; init; }

    public string? MessageId { get; init; }

    public CdmParticipantMetadata? ProtectedParticipantMetadata { get; init; }

    public CdmParticipantMetadata? SecondaryParticipantMetadata { get; init; }

    public bool IncludeRelativeStateVector { get; init; } = true;

    public bool IncludeScreeningWindow { get; init; } = true;

    public string? CollisionProbabilityMethod { get; init; }

    /// <summary>
    /// Allows the export of an encounter flagged <see cref="IO.Astrodynamics.SSA.EncounterQualityFlags.StaleCovarianceUsed"/>, whose
    /// covariance was taken from a participant's initial state further from the TCA than
    /// <see cref="IO.Astrodynamics.SSA.ConjunctionAnalysisOptions.StaleCovarianceThreshold"/>. Such an export is refused by default.
    /// The age of each exported covariance is written in the CDM comments either way.
    /// </summary>
    public bool AllowStaleCovariance { get; init; }
}
