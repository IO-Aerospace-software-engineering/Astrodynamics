// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SSA;

namespace IO.Astrodynamics.CCSDS.CDM;

internal static class CdmExporter
{
    public static Cdm Export(EncounterCase encounterCase, CdmExportOptions? options)
    {
        ArgumentNullException.ThrowIfNull(encounterCase);
        options ??= new CdmExportOptions();

        if (encounterCase.ProtectedState == null || encounterCase.SecondaryState == null)
        {
            throw new InvalidOperationException(
                "EncounterCase does not retain participant TCA states. Re-run conjunction assessment with the current version before exporting to CDM.");
        }

        // The covariance the analysis used, with its source; for a case built by hand, the same resolution as the analysis
        // without propagation: covariance at TCA, otherwise the initial-state covariance held fixed in RTN.
        var protectedCovariance = encounterCase.ProtectedCovariance ??
                                  EncounterCovariance.Resolve(encounterCase.ProtectedAsset.Spacecraft, encounterCase.ProtectedState);
        var secondaryCovariance = encounterCase.SecondaryCovariance ??
                                  EncounterCovariance.Resolve(encounterCase.SecondaryObject, encounterCase.SecondaryState);

        if (protectedCovariance.CovarianceInertial == null)
        {
            throw new InvalidOperationException(
                "Protected object covariance is required to export a standards-compliant CDM.");
        }

        if (secondaryCovariance.CovarianceInertial == null)
        {
            throw new InvalidOperationException(
                "Secondary object covariance is required to export a standards-compliant CDM.");
        }

        if (encounterCase.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed) && !options.AllowStaleCovariance)
        {
            throw new InvalidOperationException(
                $"The encounter used a stale covariance (protected: {FormatAge(protectedCovariance.Age)}, secondary: " +
                $"{FormatAge(secondaryCovariance.Age)} from TCA, held fixed in RTN, not propagated). " +
                "Set CdmExportOptions.AllowStaleCovariance to export it anyway.");
        }

        var protectedMetadata = MergeMetadata(
            InferMetadata(
                encounterCase,
                CdmObjectRole.Object1,
                encounterCase.ProtectedAsset.Spacecraft,
                encounterCase.ProtectedState,
                protectedCovariance),
            options.ProtectedParticipantMetadata);

        var secondaryMetadata = MergeMetadata(
            InferMetadata(
                encounterCase,
                CdmObjectRole.Object2,
                encounterCase.SecondaryObject,
                encounterCase.SecondaryState,
                secondaryCovariance),
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
                RotateCovarianceToRtn(protectedCovariance.CovarianceInertial!.Value, encounterCase.ProtectedState),
                protectedRadius,
                protectedCovariance),
            BuildSegment(
                secondaryMetadata,
                encounterCase.SecondaryObject,
                encounterCase.SecondaryState,
                RotateCovarianceToRtn(secondaryCovariance.CovarianceInertial!.Value, encounterCase.SecondaryState),
                secondaryRadius,
                secondaryCovariance)
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
        EncounterCovariance covariance)
    {
        var dataComments = new List<string>();
        if (covariance.FromInitialState)
        {
            dataComments.Add($"Covariance taken from the participant's initial state, {FormatAge(covariance.Age)} from TCA, " +
                             "held fixed in the RTN frame (not propagated).");
        }

        var covarianceMatrix = CdmCovarianceMatrix.FromRtnCovariance(covarianceRtn);
        if (covariance.Source == CovarianceSource.Propagated && covariance.Solution != null)
        {
            dataComments.AddRange(PropagatedCovarianceComments(covariance));
            covarianceMatrix = WithConsiderParameters(covarianceMatrix, source, state, covariance.Solution);
        }

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
                CovarianceMatrix = covarianceMatrix
            }
        };
    }

    // The source of a propagated covariance, its process noise and its non-linearity index, in the comments of the data
    private static IEnumerable<string> PropagatedCovarianceComments(EncounterCovariance covariance)
    {
        var options = covariance.Solution!.CovariancePropagation;
        yield return "Covariance propagated to TCA by the variational equations of the RK7(8) propagation of the " +
                     "participant, from the covariance of its initial state (IO.Astrodynamics).";
        yield return options?.ProcessNoise is { } noise
            ? string.Create(CultureInfo.InvariantCulture,
                $"Process noise applied: RTN white acceleration noise q_R = {noise.Radial:E3}, q_T = {noise.Transverse:E3}, " +
                $"q_N = {noise.Normal:E3} m**2/s**3.")
            : "No process noise applied: linear propagation of the initial covariance only, which is often optimistic.";
        if (options?.ConsiderParameters is { } consider)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Consider parameters: Cd sigma {FormatSigma(consider.DragCoefficientSigma)}, Cr sigma " +
                $"{FormatSigma(consider.ReflectivityCoefficientSigma)}.");
        }

        if (covariance.NonlinearityIndex is { } index)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Non-linearity index of the propagation (Junkins and Singla, 2004): {index:E3}.");
        }
    }

    private static string FormatSigma(double? sigma) =>
        sigma.HasValue ? sigma.Value.ToString("G6", CultureInfo.InvariantCulture) : "none";

    /// <summary>
    /// The rows CDRG and CSRP of the CDM covariance from the extended covariance at TCA (C4; CCSDS 508.0-B-1, §3.5 and
    /// annex: the parameters are Cd·A/m and Cr·A/m). The cross terms cov(x, C) of the state and of a coefficient are
    /// rotated to RTN as the state block, a pure rotation (CDM convention), and scaled by A/m; the variance of a
    /// coefficient by (A/m)². A is the sectional area of the spacecraft, m its total mass at TCA. Cd and Cr are
    /// independent consider parameters, so CSRP_DRG is zero when both are.
    /// </summary>
    private static CdmCovarianceMatrix WithConsiderParameters(CdmCovarianceMatrix matrix, ILocalizable source,
        StateVector encounterState, PropagationSolution solution)
    {
        var tca = encounterState.Epoch;
        var parameters = solution.ExtendedCovarianceParameters;
        if (parameters.Count == 0 || source is not Spacecraft spacecraft || solution.ContextAt(tca) is not { } context ||
            !(context.TotalMass > 0.0))
        {
            return matrix;
        }

        // The cross terms in the frame of the propagation, rotated to the RTN frame of the state block: that of the
        // encounter state when it is in the same frame, otherwise that of the propagated state (the RTN axes do not depend
        // on the inertial frame)
        var extended = solution.GetExtendedCovariance(tca);
        var state = encounterState.Frame == solution.Frame ? encounterState : solution.GetStateVector(tca);
        double areaOverMass = spacecraft.SectionalArea / context.TotalMass;

        // Row 6 + c: cov(x, C_c), rotated to RTN, times A/m; the variances and covariances of the parameters times (A/m)²
        (double[] Row, double[] Parameters) RowOf(int c)
        {
            int k = 6 + c;
            var position = state.ToRtn(new Vector3(extended.Get(k, 0), extended.Get(k, 1), extended.Get(k, 2)));
            var velocity = state.ToRtn(new Vector3(extended.Get(k, 3), extended.Get(k, 4), extended.Get(k, 5)));
            var row = new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z }
                .Select(value => value * areaOverMass).ToArray();
            var block = Enumerable.Range(0, parameters.Count)
                .Select(j => extended.Get(k, 6 + j) * areaOverMass * areaOverMass).ToArray();
            return (row, block);
        }

        int drag = IndexOf(parameters, ForceParameter.DragCoefficient);
        int reflectivity = IndexOf(parameters, ForceParameter.ReflectivityCoefficient);
        if (drag >= 0)
        {
            var (row, block) = RowOf(drag);
            matrix = matrix with
            {
                CdrgR = row[0], CdrgT = row[1], CdrgN = row[2], CdrgRdot = row[3], CdrgTdot = row[4], CdrgNdot = row[5],
                CdrgDrg = block[drag]
            };
        }

        if (reflectivity >= 0)
        {
            var (row, block) = RowOf(reflectivity);
            matrix = matrix with
            {
                CsrpR = row[0], CsrpT = row[1], CsrpN = row[2], CsrpRdot = row[3], CsrpTdot = row[4], CsrpNdot = row[5],
                CsrpDrg = drag >= 0 ? block[drag] : null,
                CsrpSrp = block[reflectivity]
            };
        }

        return matrix;
    }

    private static int IndexOf(IReadOnlyList<ForceParameter> parameters, ForceParameter parameter)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i] == parameter)
                return i;
        }

        return -1;
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
        EncounterCovariance covariance)
    {
        var comments = new List<string>();
        if (covariance.FromInitialState)
        {
            comments.Add($"Participant covariance at TCA was unavailable; the initial state covariance, {FormatAge(covariance.Age)} " +
                         "from TCA, was exported instead.");
        }

        if (source.IsSpiceBacked)
        {
            comments.Add("Participant ephemeris is backed by SPICE.");
        }

        // The force model the propagation of a propagated covariance actually used (E5)
        var forceModel = covariance.Source == CovarianceSource.Propagated && covariance.Solution?.Dynamics is { } dynamics
            ? CdmForceModel.From(dynamics)
            : null;

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
            GravityModel = forceModel?.GravityModel,
            AtmosphericModel = forceModel?.AtmosphericModel,
            NBodyPerturbations = forceModel?.NBodyPerturbations,
            SolarRadPressure = forceModel?.SolarRadiationPressure ??
                               (source is Spacecraft spacecraft ? spacecraft.SolarRadiationCoeff > 0.0 : null),
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

    private static string FormatAge(TimeSpan? age)
    {
        return age.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{age.Value.TotalSeconds:F1} s")
            : "no covariance";
    }

    /// <summary>
    /// Maps the frame of a participant state onto a CDM REF_FRAME. SPICE J2000 (Frame.ICRF) and GCRF share the ICRF
    /// axes, so both are labelled GCRF exactly; no frame bias is involved. A frame named EME2000 keeps its label.
    /// </summary>
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
