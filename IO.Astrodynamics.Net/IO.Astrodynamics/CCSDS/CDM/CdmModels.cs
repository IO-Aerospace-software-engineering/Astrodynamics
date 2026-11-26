// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Ccsds;

public sealed record CdmHeader
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime CreationDateUtc { get; init; }

    /// <summary>
    /// <see cref="CreationDateUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7).
    /// </summary>
    public Time CreationDate => CcsdsDates.ToUtcTime(CreationDateUtc);

    public string Originator { get; init; } = string.Empty;

    public string? MessageFor { get; init; }

    public string MessageId { get; init; } = string.Empty;
}

/// <summary>
/// The relative position and velocity of object 2 with respect to object 1 in the RTN frame of object 1, in m and m/s.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): the init accessor of each vector throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for a component NaN or ±Infinity, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// </remarks>
public sealed record CdmRelativeStateVector
{
    private readonly Vector3 _relativePositionRtnMeters = Vector3.Zero;

    public Vector3 RelativePositionRtnMeters
    {
        get => _relativePositionRtnMeters;
        init => _relativePositionRtnMeters = CcsdsValues.RequireFinite(value, nameof(RelativePositionRtnMeters));
    }

    private readonly Vector3 _relativeVelocityRtnMetersPerSecond = Vector3.Zero;

    public Vector3 RelativeVelocityRtnMetersPerSecond
    {
        get => _relativeVelocityRtnMetersPerSecond;
        init => _relativeVelocityRtnMetersPerSecond = CcsdsValues.RequireFinite(value, nameof(RelativeVelocityRtnMetersPerSecond));
    }
}

/// <summary>
/// The relative metadata and data of a CDM: the TCA, the miss distance, the relative speed and state, the screening period and volume, and the probability of collision.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): an init accessor throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for NaN or ±Infinity, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// The one range of these values that <see cref="Cdm.Validate"/> checks is the miss distance's, 0 or more.
/// </remarks>
public sealed record CdmRelativeMetadataData
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime TcaUtc { get; init; }

    /// <summary>
    /// <see cref="TcaUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7).
    /// </summary>
    public Time Tca => CcsdsDates.ToUtcTime(TcaUtc);

    private readonly double _missDistanceMeters;

    public double MissDistanceMeters
    {
        get => _missDistanceMeters;
        init => _missDistanceMeters = CcsdsValues.RequireFinite(value, nameof(MissDistanceMeters));
    }

    private readonly double? _relativeSpeedMetersPerSecond;

    public double? RelativeSpeedMetersPerSecond
    {
        get => _relativeSpeedMetersPerSecond;
        init => _relativeSpeedMetersPerSecond = CcsdsValues.RequireNullOrFinite(value, nameof(RelativeSpeedMetersPerSecond));
    }

    public CdmRelativeStateVector? RelativeStateVector { get; init; }

    public DateTime? StartScreenPeriodUtc { get; init; }

    /// <summary>
    /// <see cref="StartScreenPeriodUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? StartScreenPeriod => CcsdsDates.ToUtcTime(StartScreenPeriodUtc);

    public DateTime? StopScreenPeriodUtc { get; init; }

    /// <summary>
    /// <see cref="StopScreenPeriodUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? StopScreenPeriod => CcsdsDates.ToUtcTime(StopScreenPeriodUtc);

    public CdmScreenVolumeFrame? ScreenVolumeFrame { get; init; }

    public CdmScreenVolumeShape? ScreenVolumeShape { get; init; }

    private readonly double? _screenVolumeXMeters;

    public double? ScreenVolumeXMeters
    {
        get => _screenVolumeXMeters;
        init => _screenVolumeXMeters = CcsdsValues.RequireNullOrFinite(value, nameof(ScreenVolumeXMeters));
    }

    private readonly double? _screenVolumeYMeters;

    public double? ScreenVolumeYMeters
    {
        get => _screenVolumeYMeters;
        init => _screenVolumeYMeters = CcsdsValues.RequireNullOrFinite(value, nameof(ScreenVolumeYMeters));
    }

    private readonly double? _screenVolumeZMeters;

    public double? ScreenVolumeZMeters
    {
        get => _screenVolumeZMeters;
        init => _screenVolumeZMeters = CcsdsValues.RequireNullOrFinite(value, nameof(ScreenVolumeZMeters));
    }

    public DateTime? ScreenEntryTimeUtc { get; init; }

    /// <summary>
    /// <see cref="ScreenEntryTimeUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? ScreenEntryTime => CcsdsDates.ToUtcTime(ScreenEntryTimeUtc);

    public DateTime? ScreenExitTimeUtc { get; init; }

    /// <summary>
    /// <see cref="ScreenExitTimeUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? ScreenExitTime => CcsdsDates.ToUtcTime(ScreenExitTimeUtc);

    private readonly double? _collisionProbability;

    public double? CollisionProbability
    {
        get => _collisionProbability;
        init => _collisionProbability = CcsdsValues.RequireNullOrFinite(value, nameof(CollisionProbability));
    }

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

    /// <summary>
    /// <c>SOLAR_RAD_PRESSURE</c>: whether solar radiation pressure perturbations were used for the orbit determination of the
    /// object (CCSDS 508.0-B-1 Cor. 2, Table 3-3, optional), not whether the object has a reflectivity coefficient.
    /// </summary>
    /// <remarks>
    /// <see cref="IO.Astrodynamics.Ssa.EncounterCase.ToCdm"/> infers it from the forces of the propagation for a participant
    /// whose covariance the library propagated to TCA, as it fills the other force-model keywords; otherwise it infers
    /// nothing and the keyword is omitted, the library running no orbit determination. Set it in
    /// <see cref="CdmExportOptions.ProtectedParticipantMetadata"/> or <see cref="CdmExportOptions.SecondaryParticipantMetadata"/>
    /// to state the value of an OD; <c>null</c> keeps the inferred value (<c>true</c> for a spacecraft whose reflectivity
    /// coefficient is above 0 before).
    /// </remarks>
    public bool? SolarRadPressure { get; init; }

    public bool? EarthTides { get; init; }

    /// <summary>
    /// <c>INTRACK_THRUST</c>: whether in-track thrust modeling was used for the orbit determination of the object (CCSDS
    /// 508.0-B-1 Cor. 2, Table 3-3, optional), not whether the object can maneuver, which <see cref="Maneuverable"/> states.
    /// </summary>
    /// <remarks>
    /// <see cref="IO.Astrodynamics.Ssa.EncounterCase.ToCdm"/> infers <c>false</c> (<c>NO</c>) for a participant whose
    /// covariance the library propagated to TCA, whose force model holds no thrust, as it fills the other force-model
    /// keywords; otherwise it infers nothing and the keyword is omitted, the library running no orbit determination. Set
    /// it in <see cref="CdmExportOptions.ProtectedParticipantMetadata"/> or
    /// <see cref="CdmExportOptions.SecondaryParticipantMetadata"/> to state the value of an OD; <c>null</c> keeps the
    /// inferred value (#684: <c>true</c> for a spacecraft with an engine before).
    /// </remarks>
    public bool? IntrackThrust { get; init; }
}

/// <summary>
/// The orbit determination parameters of a CDM segment.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): an init accessor throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for NaN or ±Infinity, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// </remarks>
public sealed record CdmOdParameters
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    public DateTime? TimeLastObservationStartUtc { get; init; }

    /// <summary>
    /// <see cref="TimeLastObservationStartUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? TimeLastObservationStart => CcsdsDates.ToUtcTime(TimeLastObservationStartUtc);

    public DateTime? TimeLastObservationEndUtc { get; init; }

    /// <summary>
    /// <see cref="TimeLastObservationEndUtc"/> as a UTC <see cref="Time"/>, an unspecified kind taken as UTC as the writer takes it (#617;
    /// R19 of #668, lot L7); null without it.
    /// </summary>
    public Time? TimeLastObservationEnd => CcsdsDates.ToUtcTime(TimeLastObservationEndUtc);

    private readonly double? _recommendedOdSpanDays;

    public double? RecommendedOdSpanDays
    {
        get => _recommendedOdSpanDays;
        init => _recommendedOdSpanDays = CcsdsValues.RequireNullOrFinite(value, nameof(RecommendedOdSpanDays));
    }

    private readonly double? _actualOdSpanDays;

    public double? ActualOdSpanDays
    {
        get => _actualOdSpanDays;
        init => _actualOdSpanDays = CcsdsValues.RequireNullOrFinite(value, nameof(ActualOdSpanDays));
    }

    public int? ObservationsAvailable { get; init; }

    public int? ObservationsUsed { get; init; }

    public int? TracksAvailable { get; init; }

    public int? TracksUsed { get; init; }

    private readonly double? _residualsAcceptedPercent;

    public double? ResidualsAcceptedPercent
    {
        get => _residualsAcceptedPercent;
        init => _residualsAcceptedPercent = CcsdsValues.RequireNullOrFinite(value, nameof(ResidualsAcceptedPercent));
    }

    private readonly double? _weightedRms;

    public double? WeightedRms
    {
        get => _weightedRms;
        init => _weightedRms = CcsdsValues.RequireNullOrFinite(value, nameof(WeightedRms));
    }
}

/// <summary>
/// The additional parameters of a CDM segment: areas, mass, ballistic and reflectivity terms, thrust acceleration, SEDR.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): an init accessor throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for NaN or ±Infinity, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// </remarks>
public sealed record CdmAdditionalParameters
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    private readonly double? _areaPcSquareMeters;

    public double? AreaPcSquareMeters
    {
        get => _areaPcSquareMeters;
        init => _areaPcSquareMeters = CcsdsValues.RequireNullOrFinite(value, nameof(AreaPcSquareMeters));
    }

    private readonly double? _areaDragSquareMeters;

    public double? AreaDragSquareMeters
    {
        get => _areaDragSquareMeters;
        init => _areaDragSquareMeters = CcsdsValues.RequireNullOrFinite(value, nameof(AreaDragSquareMeters));
    }

    private readonly double? _areaSrpSquareMeters;

    public double? AreaSrpSquareMeters
    {
        get => _areaSrpSquareMeters;
        init => _areaSrpSquareMeters = CcsdsValues.RequireNullOrFinite(value, nameof(AreaSrpSquareMeters));
    }

    private readonly double? _massKilograms;

    public double? MassKilograms
    {
        get => _massKilograms;
        init => _massKilograms = CcsdsValues.RequireNullOrFinite(value, nameof(MassKilograms));
    }

    private readonly double? _cdAreaOverMass;

    public double? CdAreaOverMass
    {
        get => _cdAreaOverMass;
        init => _cdAreaOverMass = CcsdsValues.RequireNullOrFinite(value, nameof(CdAreaOverMass));
    }

    private readonly double? _crAreaOverMass;

    public double? CrAreaOverMass
    {
        get => _crAreaOverMass;
        init => _crAreaOverMass = CcsdsValues.RequireNullOrFinite(value, nameof(CrAreaOverMass));
    }

    private readonly double? _thrustAccelerationMetersPerSecondSquared;

    public double? ThrustAccelerationMetersPerSecondSquared
    {
        get => _thrustAccelerationMetersPerSecondSquared;
        init => _thrustAccelerationMetersPerSecondSquared = CcsdsValues.RequireNullOrFinite(value, nameof(ThrustAccelerationMetersPerSecondSquared));
    }

    private readonly double? _sedrWattsPerKilogram;

    public double? SedrWattsPerKilogram
    {
        get => _sedrWattsPerKilogram;
        init => _sedrWattsPerKilogram = CcsdsValues.RequireNullOrFinite(value, nameof(SedrWattsPerKilogram));
    }
}

/// <summary>
/// The state of the object of a CDM segment at TCA, in m and m/s.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): the init accessor of each vector throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for a component NaN or ±Infinity, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// </remarks>
public sealed record CdmStateVector
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    private readonly Vector3 _positionMeters = Vector3.Zero;

    public Vector3 PositionMeters
    {
        get => _positionMeters;
        init => _positionMeters = CcsdsValues.RequireFinite(value, nameof(PositionMeters));
    }

    private readonly Vector3 _velocityMetersPerSecond = Vector3.Zero;

    public Vector3 VelocityMetersPerSecond
    {
        get => _velocityMetersPerSecond;
        init => _velocityMetersPerSecond = CcsdsValues.RequireFinite(value, nameof(VelocityMetersPerSecond));
    }
}

/// <summary>
/// The covariance of the object of a CDM segment at TCA in its RTN frame (m², m²/s, m²/s²), with the optional drag, SRP and thrust rows.
/// </summary>
/// <remarks>
/// Every real value is a real number, as the CDM reader reads it (#708): an init accessor throws
/// <see cref="ArgumentOutOfRangeException"/> naming its property for NaN or ±Infinity, a term of
/// <see cref="StateCovarianceRtn"/> included, which the writer would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c> and the reader refuses (#717).
/// The record holds a copy of the matrix it is given and returns a copy of the one it holds, so a term set afterwards on
/// either matrix does not reach it (review of #717). The init accessor refuses a matrix that is not 6×6 (#730), so the
/// record always holds the 21 terms <c>CR_R</c> ... <c>CNDOT_NDOT</c> that the writer writes.
/// </remarks>
public sealed record CdmCovarianceMatrix
{
    public IReadOnlyList<string> Comments { get; init; } = Array.Empty<string>();

    private readonly Matrix _stateCovarianceRtn = new Matrix(6, 6);

    /// <summary>
    /// The 6×6 state covariance in the RTN frame of the object (m², m²/s, m²/s²), of which the writer writes the lower
    /// triangle (<c>CR_R</c> ... <c>CNDOT_NDOT</c>).
    /// </summary>
    /// <remarks>
    /// The record holds its own copy of the matrix: the init accessor checks the terms of a copy of the matrix it is given
    /// and the getter returns a copy of the one it holds (review of #717, when a public <c>Matrix.Set</c> wrote the array
    /// every copy of the struct shares; <see cref="Matrix"/> is immutable since 11.0.0).
    /// </remarks>
    /// <exception cref="ArgumentException">The matrix given is not 6×6, <c>default(Matrix)</c> included (#730: it was
    /// accepted, and <see cref="CdmWriter"/> then threw <see cref="IndexOutOfRangeException"/> or
    /// <see cref="NullReferenceException"/>); checked before its terms.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A term of the matrix given is NaN or ±Infinity.</exception>
    public Matrix StateCovarianceRtn
    {
        get => CcsdsValues.Copy(_stateCovarianceRtn);
        init => _stateCovarianceRtn =
            CcsdsValues.RequireFinite(CcsdsValues.Copy(RequireSixBySix(value)), nameof(StateCovarianceRtn));
    }

    // The size of the state covariance: 6×6, the matrix whose lower triangle CCSDS 508.0-B-1 table 3-5 lists (#730)
    private static Matrix RequireSixBySix(Matrix value)
    {
        if (value.Rows != 6 || value.Columns != 6)
            throw new ArgumentException(
                $"{nameof(StateCovarianceRtn)} must be a 6x6 matrix; the matrix given is {value.Rows}x{value.Columns}.",
                nameof(StateCovarianceRtn));
        return value;
    }

    private readonly double? _cdrgR;

    public double? CdrgR
    {
        get => _cdrgR;
        init => _cdrgR = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgR));
    }

    private readonly double? _cdrgT;

    public double? CdrgT
    {
        get => _cdrgT;
        init => _cdrgT = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgT));
    }

    private readonly double? _cdrgN;

    public double? CdrgN
    {
        get => _cdrgN;
        init => _cdrgN = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgN));
    }

    private readonly double? _cdrgRdot;

    public double? CdrgRdot
    {
        get => _cdrgRdot;
        init => _cdrgRdot = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgRdot));
    }

    private readonly double? _cdrgTdot;

    public double? CdrgTdot
    {
        get => _cdrgTdot;
        init => _cdrgTdot = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgTdot));
    }

    private readonly double? _cdrgNdot;

    public double? CdrgNdot
    {
        get => _cdrgNdot;
        init => _cdrgNdot = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgNdot));
    }

    private readonly double? _cdrgDrg;

    public double? CdrgDrg
    {
        get => _cdrgDrg;
        init => _cdrgDrg = CcsdsValues.RequireNullOrFinite(value, nameof(CdrgDrg));
    }

    private readonly double? _csrpR;

    public double? CsrpR
    {
        get => _csrpR;
        init => _csrpR = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpR));
    }

    private readonly double? _csrpT;

    public double? CsrpT
    {
        get => _csrpT;
        init => _csrpT = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpT));
    }

    private readonly double? _csrpN;

    public double? CsrpN
    {
        get => _csrpN;
        init => _csrpN = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpN));
    }

    private readonly double? _csrpRdot;

    public double? CsrpRdot
    {
        get => _csrpRdot;
        init => _csrpRdot = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpRdot));
    }

    private readonly double? _csrpTdot;

    public double? CsrpTdot
    {
        get => _csrpTdot;
        init => _csrpTdot = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpTdot));
    }

    private readonly double? _csrpNdot;

    public double? CsrpNdot
    {
        get => _csrpNdot;
        init => _csrpNdot = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpNdot));
    }

    private readonly double? _csrpDrg;

    public double? CsrpDrg
    {
        get => _csrpDrg;
        init => _csrpDrg = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpDrg));
    }

    private readonly double? _csrpSrp;

    public double? CsrpSrp
    {
        get => _csrpSrp;
        init => _csrpSrp = CcsdsValues.RequireNullOrFinite(value, nameof(CsrpSrp));
    }

    private readonly double? _cthrR;

    public double? CthrR
    {
        get => _cthrR;
        init => _cthrR = CcsdsValues.RequireNullOrFinite(value, nameof(CthrR));
    }

    private readonly double? _cthrT;

    public double? CthrT
    {
        get => _cthrT;
        init => _cthrT = CcsdsValues.RequireNullOrFinite(value, nameof(CthrT));
    }

    private readonly double? _cthrN;

    public double? CthrN
    {
        get => _cthrN;
        init => _cthrN = CcsdsValues.RequireNullOrFinite(value, nameof(CthrN));
    }

    private readonly double? _cthrRdot;

    public double? CthrRdot
    {
        get => _cthrRdot;
        init => _cthrRdot = CcsdsValues.RequireNullOrFinite(value, nameof(CthrRdot));
    }

    private readonly double? _cthrTdot;

    public double? CthrTdot
    {
        get => _cthrTdot;
        init => _cthrTdot = CcsdsValues.RequireNullOrFinite(value, nameof(CthrTdot));
    }

    private readonly double? _cthrNdot;

    public double? CthrNdot
    {
        get => _cthrNdot;
        init => _cthrNdot = CcsdsValues.RequireNullOrFinite(value, nameof(CthrNdot));
    }

    private readonly double? _cthrDrg;

    public double? CthrDrg
    {
        get => _cthrDrg;
        init => _cthrDrg = CcsdsValues.RequireNullOrFinite(value, nameof(CthrDrg));
    }

    private readonly double? _cthrSrp;

    public double? CthrSrp
    {
        get => _cthrSrp;
        init => _cthrSrp = CcsdsValues.RequireNullOrFinite(value, nameof(CthrSrp));
    }

    private readonly double? _cthrThr;

    public double? CthrThr
    {
        get => _cthrThr;
        init => _cthrThr = CcsdsValues.RequireNullOrFinite(value, nameof(CthrThr));
    }

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
    /// <summary>The options of the default export, as <c>new()</c> (D1 of #668, lot L7).</summary>
    public static CdmExportOptions Default { get; } = new();

    public DateTime? CreationDateUtc { get; init; }

    public string Originator { get; init; } = "IO.Astrodynamics";

    public string? MessageFor { get; init; }

    public string? MessageId { get; init; }

    public CdmParticipantMetadata? ProtectedParticipantMetadata { get; init; }

    public CdmParticipantMetadata? SecondaryParticipantMetadata { get; init; }

    public bool IncludeRelativeStateVector { get; init; } = true;

    public bool IncludeScreeningWindow { get; init; } = true;

    /// <summary>
    /// The <c>COLLISION_PROBABILITY_METHOD</c> written, as given; null for the method the analysis computes, Foster and Estes
    /// (1992), <c>FOSTER-1992</c> in the SANA registry <c>cdm_cpm</c> (CCSDS 508.0-B-1 Cor. 2, annex E; <c>FOSTER-2D</c>,
    /// which the registry does not hold, before #618).
    /// </summary>
    public string? CollisionProbabilityMethod { get; init; }

    /// <summary>
    /// Allows the export of an encounter flagged <see cref="IO.Astrodynamics.Ssa.EncounterQualityFlags.StaleCovarianceUsed"/>, whose
    /// covariance was taken from a participant's initial state further from the TCA than
    /// <see cref="IO.Astrodynamics.Ssa.ConjunctionAnalysisOptions.StaleCovarianceThreshold"/>. Such an export is refused by default.
    /// The age of each exported covariance is written in the CDM comments either way.
    /// </summary>
    public bool AllowStaleCovariance { get; init; }
}
