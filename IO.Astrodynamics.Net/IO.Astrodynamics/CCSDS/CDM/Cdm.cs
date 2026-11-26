// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class Cdm
{
    private static readonly CdmReader DefaultReader = new();
    private static readonly CdmValidator DefaultValidator = new();

    public const string Version = "1.0";
    public const string FormatId = "CCSDS_CDM_VERS";

    public Cdm(CdmHeader header, CdmRelativeMetadataData relativeMetadataData, IReadOnlyList<CdmSegment> segments)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        RelativeMetadataData = relativeMetadataData ?? throw new ArgumentNullException(nameof(relativeMetadataData));
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));

        if (Segments.Count != 2)
        {
            throw new ArgumentException("CDM must contain exactly two segments.", nameof(segments));
        }
    }

    public CdmHeader Header { get; }

    public CdmRelativeMetadataData RelativeMetadataData { get; }

    public IReadOnlyList<CdmSegment> Segments { get; }

    #region Read and write (R22 of #668)

    // The one verb set of the CCSDS messages over the internal reader and validator
    private static readonly CcsdsMessageIO<Cdm, CdmValidationResult> Format = new("CDM",
        DefaultValidator.ValidateSchema, DefaultValidator.ValidateSchemaFromXml, DefaultReader.Read, ContentValidator,
        result => result.IsValid, (message, result) => new CdmValidationException(message, result),
        (message, inner) => new CdmParseException(message, inner));

    // The content check of a CDM, which has none of the validator switches of the read options
    private static Func<Cdm, CdmValidationResult> ContentValidator(CcsdsReadOptions options)
    {
        if (options.ValidatePhysicalConstraints == false || options.ValidateTleRequirements == false || options.WarnOnMissingOptionalFields == false)
            throw new ArgumentException("A CDM's content check has no physical-constraint, TLE or optional-field switch: none can be turned off for it.", nameof(options));

        return DefaultValidator.Validate;
    }

    /// <summary>
    /// Reads the one CDM of a file: a standalone <c>cdm</c> or an <c>ndm</c> that holds one (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default (<see cref="CcsdsReadOptions.Default"/>): the NDM/XML 4.0.0
    /// schema in the form of the document, then the content.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The document cannot be read: no CDM, several (<see cref="LoadAll"/> reads them), or a
    /// keyword whose value the data model refuses, naming the keyword and its value.</exception>
    public static Cdm Load(string path, CcsdsReadOptions? options = null) => Format.Load(path, options, false)[0];

    /// <summary>
    /// Reads every CDM of a file, in document order, as <see cref="Load"/> reads one: the schema checked once for the
    /// document, the content of each message (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages; a document that holds none throws.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The document holds no CDM or one that cannot be read.</exception>
    public static IReadOnlyList<Cdm> LoadAll(string path, CcsdsReadOptions? options = null) => Format.Load(path, options, true);

    /// <summary>
    /// Reads the one CDM of an XML text, as <see cref="Load"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The text is blank or cannot be read, or holds no CDM or several
    /// (<see cref="ParseAll(string, CcsdsReadOptions)"/> reads them).</exception>
    public static Cdm Parse(string xml, CcsdsReadOptions? options = null) => Format.Parse(xml, options, false)[0];

    /// <summary>
    /// Reads the one CDM of a reader, read to its end, as <see cref="Parse(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The text cannot be read, or holds no CDM or several.</exception>
    public static Cdm Parse(TextReader reader, CcsdsReadOptions? options = null) => Format.Parse(reader, options, false)[0];

    /// <summary>
    /// Reads every CDM of an XML text, as <see cref="LoadAll"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The text is blank or cannot be read, or holds no CDM.</exception>
    public static IReadOnlyList<Cdm> ParseAll(string xml, CcsdsReadOptions? options = null) => Format.Parse(xml, options, true);

    /// <summary>
    /// Reads every CDM of a reader, read to its end, as <see cref="ParseAll(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The text cannot be read, or holds no CDM.</exception>
    public static IReadOnlyList<Cdm> ParseAll(TextReader reader, CcsdsReadOptions? options = null) => Format.Parse(reader, options, true);

    /// <summary>
    /// <see cref="Load"/> without an exception for a file that does not exist or that the checks or the reader refuse.
    /// </summary>
    /// <param name="path">The path of the file, or null.</param>
    /// <param name="result">The CDM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the file exists and holds one CDM that passes the checks; an error reading an existing file
    /// throws, as <see cref="Load"/> does. Use <see cref="Load"/> for the reason of a refusal.</returns>
    public static bool TryLoad(string? path, [NotNullWhen(true)] out Cdm? result, CcsdsReadOptions? options = null) => Format.TryLoad(path, options, out result);

    /// <summary>
    /// <see cref="Parse(string, CcsdsReadOptions)"/> without an exception for a text that the checks or the reader refuse.
    /// </summary>
    /// <param name="xml">The XML text, or null.</param>
    /// <param name="result">The CDM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the text holds one CDM that passes the checks.</returns>
    public static bool TryParse(string? xml, [NotNullWhen(true)] out Cdm? result, CcsdsReadOptions? options = null) => Format.TryParse(xml, options, out result);

    /// <summary>
    /// Writes the CDM to a file, in UTF-8, replacing it (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented
    /// (<see cref="CcsdsWriteOptions.Default"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or blank.</exception>
    /// <exception cref="CdmValidationException">The content check fails; nothing is written.</exception>
    public void Save(string path, CcsdsWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("The path is empty or blank.", nameof(path));
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new CdmWriter { IndentOutput = options.Indent }.WriteToFile(this, path, options.WrapInNdmContainer);
    }

    /// <summary>
    /// Writes the CDM to a stream, in UTF-8, as <see cref="Save(string, CcsdsWriteOptions)"/>; the stream stays open.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="CdmValidationException">The content check fails; nothing is written.</exception>
    public void Save(Stream stream, CcsdsWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new CdmWriter { IndentOutput = options.Indent }.WriteToStream(this, stream, options.WrapInNdmContainer);
    }

    /// <summary>
    /// The CDM as an XML text, as <see cref="Save(string, CcsdsWriteOptions)"/> writes it (R22 of #668).
    /// </summary>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented; before 11.0.0
    /// the text was not validated.</param>
    /// <returns>The XML text.</returns>
    /// <exception cref="CdmValidationException">The content check fails.</exception>
    public string ToXml(CcsdsWriteOptions? options = null)
    {
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        return new CdmWriter { IndentOutput = options.Indent }.WriteToString(this, options.WrapInNdmContainer);
    }

    /// <summary>
    /// Validates the content of this CDM: required values, ranges, consistency.
    /// </summary>
    /// <returns>The errors, warnings and notes.</returns>
    public CdmValidationResult Validate() => DefaultValidator.Validate(this);

    /// <summary>
    /// Whether <see cref="Validate"/> reports no error.
    /// </summary>
    /// <returns>True when the content holds no error.</returns>
    public bool IsValid() => Validate().IsValid;

    #endregion

    #region Ingest

    /// <summary>
    /// The state of an object at TCA, with its covariance turned from its RTN frame (R19 of #668, lot L7).
    /// </summary>
    /// <param name="role">The object: <see cref="CdmObjectRole.Object1"/> (the first segment) or
    /// <see cref="CdmObjectRole.Object2"/> (the second).</param>
    /// <returns>
    /// The state of the segment at <c>TCA</c> (UTC), about the centre <c>ORBIT_CENTER</c> names (the Earth without one):
    /// for <c>REF_FRAME</c> <c>EME2000</c> or <c>GCRF</c>, in <see cref="Frame.ICRF"/>, its covariance the RTN covariance
    /// turned on that state (<see cref="StateVector.RotateCovarianceFromRtn"/>); for <c>ITRF</c>, in
    /// <see cref="Frame.ITRF93"/>, its covariance the RTN covariance turned on the state in ICRF, then carried into
    /// ITRF93 (<see cref="StateVector.ToFrame"/>). Without a covariance, the state alone.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is not a role of the enumeration.</exception>
    /// <exception cref="InvalidOperationException">The segment holds no state vector (a CDM built by hand: the reader
    /// refuses one), names no <c>REF_FRAME</c>, or an <c>ORBIT_CENTER</c> the library does not identify, or an
    /// <c>ITRF</c> state about another centre than the Earth.</exception>
    public StateVector ToStateVector(CdmObjectRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The role is Object1 or Object2.");
        }

        var segment = Segments[(int)role];
        var state = segment.Data?.StateVector ??
                    throw new InvalidOperationException($"The segment of {role} holds no state vector.");
        var epoch = RelativeMetadataData.Tca;
        var observer = OrbitCenter(segment.Metadata?.OrbitCenter, epoch, role);
        var covarianceRtn = segment.Data.CovarianceMatrix?.StateCovarianceRtn;
        switch (segment.Metadata?.ReferenceFrame)
        {
            case CdmReferenceFrame.Eme2000:
            case CdmReferenceFrame.Gcrf:
            {
                var inertial = new StateVector(state.PositionMeters, state.VelocityMetersPerSecond, observer, epoch, Frame.ICRF);
                return covarianceRtn is { } rtn
                    ? new StateVector(state.PositionMeters, state.VelocityMetersPerSecond, observer, epoch, Frame.ICRF,
                        inertial.RotateCovarianceFromRtn(rtn))
                    : inertial;
            }
            case CdmReferenceFrame.Itrf:
            {
                if (observer.NaifId != PlanetsAndMoons.Earth.NaifId)
                {
                    throw new InvalidOperationException(
                        $"The segment of {role} gives an ITRF state about ORBIT_CENTER {segment.Metadata.OrbitCenter}: ITRF is the frame of the Earth.");
                }

                var fixedState = new StateVector(state.PositionMeters, state.VelocityMetersPerSecond, observer, epoch, Frame.ITRF93);
                if (covarianceRtn is not { } rtn)
                {
                    return fixedState;
                }

                var inertial = fixedState.ToFrame(Frame.ICRF);
                var withCovariance = new StateVector(inertial.Position, inertial.Velocity, observer, epoch, Frame.ICRF,
                    inertial.RotateCovarianceFromRtn(rtn));
                return new StateVector(state.PositionMeters, state.VelocityMetersPerSecond, observer, epoch, Frame.ITRF93,
                    withCovariance.ToFrame(Frame.ITRF93).Covariance);
            }
            default:
                throw new InvalidOperationException($"The segment of {role} names no REF_FRAME.");
        }
    }

    /// <summary>
    /// The encounter this CDM describes, at its TCA from the states of its two objects as given (decision 110 of #668,
    /// decided by S. Guillet, 2026-11-26): no search, <see cref="EncounterCase.Search"/>
    /// <see cref="CloseApproachSearch.None"/>.
    /// </summary>
    /// <param name="combinedHardBodyRadiusMeters">The radius of the sphere that bounds both objects, in m, finite and above
    /// 0: a CDM gives none.</param>
    /// <returns>
    /// The encounter at <c>TCA</c> of the states of <see cref="ToStateVector"/> (in ICRF), each covariance given at the
    /// encounter, so its <see cref="EncounterCase.ProbabilityOfCollision"/> is
    /// <see cref="CollisionProbability.Foster2D"/> of the two states bit for bit, or
    /// <see cref="CollisionProbability.FrisbeeMax"/> when one covariance is absent. The objects are
    /// <see cref="Body.Spacecraft.Spacecraft"/> of 100 kg on their states, named by <c>OBJECT_NAME</c> (or the role), with
    /// transient NAIF ids, object 1 bearing the combined radius; their trajectories are the two-body orbits of those
    /// states (<see cref="TrajectorySource.TwoBody"/>), which <see cref="ConjunctionAssessment.EvaluateAvoidance"/>
    /// follows. The screening window is <c>START_SCREEN_PERIOD</c> to <c>STOP_SCREEN_PERIOD</c>, within which an avoidance
    /// places its burns; without them (both optional) it is the TCA alone, so
    /// <see cref="ConjunctionAssessment.EvaluateAvoidance"/> gives no option and lists every lead time in
    /// <see cref="AvoidanceStudy.Skipped"/> as before the window. <see cref="EncounterCase.ToCdm"/> of the encounter is not a re-export of
    /// this CDM: the masses, areas and designators it writes are those of the transient spacecraft.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite or not above 0.</exception>
    /// <exception cref="InvalidOperationException">A segment cannot give its state (<see cref="ToStateVector"/>), or the
    /// two objects are about two centres.</exception>
    public EncounterCase ToEncounter(double combinedHardBodyRadiusMeters)
    {
        if (!double.IsFinite(combinedHardBodyRadiusMeters) || combinedHardBodyRadiusMeters <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(combinedHardBodyRadiusMeters), combinedHardBodyRadiusMeters,
                "The combined hard-body radius must be finite and above 0.");
        }

        var object1 = InIcrf(ToStateVector(CdmObjectRole.Object1));
        var object2 = InIcrf(ToStateVector(CdmObjectRole.Object2));
        if (object1.Observer.NaifId != object2.Observer.NaifId)
        {
            throw new InvalidOperationException(
                $"The two objects are about two centres, {object1.Observer.Name} and {object2.Observer.Name} (ORBIT_CENTER).");
        }

        var relative = RelativeMetadataData;
        var window = relative.StartScreenPeriod is { } start && relative.StopScreenPeriod is { } stop
            ? new Window(start, stop)
            : new Window(relative.Tca, relative.Tca);
        return ConjunctionAssessment.EncounterAt(object1, Segments[0].Metadata?.ObjectName ?? nameof(CdmObjectRole.Object1),
            object2, Segments[1].Metadata?.ObjectName ?? nameof(CdmObjectRole.Object2), combinedHardBodyRadiusMeters, window);
    }

    private static StateVector InIcrf(StateVector state) => state.Frame.Equals(Frame.ICRF) ? state : state.ToFrame(Frame.ICRF);

    // The centre of a segment: the Earth without ORBIT_CENTER, otherwise the centre it names
    private static ILocalizable OrbitCenter(string? orbitCenter, Time epoch, CdmObjectRole role)
    {
        if (orbitCenter == null)
        {
            return new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, epoch);
        }

        if (!CcsdsCenterNames.TryGetNaifId(orbitCenter, out _))
        {
            throw new InvalidOperationException(
                $"The ORBIT_CENTER '{orbitCenter}' of {role} is not a barycenter, the Sun, a planet or a moon the library identifies.");
        }

        return CcsdsCenterNames.Observer(orbitCenter, null!, epoch);
    }

    #endregion
}

