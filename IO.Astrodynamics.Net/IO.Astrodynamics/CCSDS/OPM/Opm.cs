// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// Represents a CCSDS Orbit Parameter Message (OPM).
/// </summary>
/// <remarks>
/// OPM is a CCSDS Navigation Data Message format for exchanging osculating (instantaneous) orbital state vectors.
/// Unlike OMM which uses mean elements, OPM contains actual position and velocity at a specific epoch.
/// See CCSDS 502.0-B-3 (ODM Blue Book) for the complete specification.
/// </remarks>
public class Opm
{
    private static readonly OpmReader DefaultReader = new();
    private static readonly OpmValidator DefaultValidator = new();

    /// <summary>
    /// The CCSDS OPM format version.
    /// </summary>
    public const string Version = "3.0";

    /// <summary>
    /// The CCSDS OPM format identifier.
    /// </summary>
    public const string FormatId = "CCSDS_OPM_VERS";

    /// <summary>
    /// Gets the message header.
    /// </summary>
    public CcsdsHeader Header { get; }

    /// <summary>
    /// Gets the message metadata.
    /// </summary>
    public OpmMetadata Metadata { get; }

    /// <summary>
    /// Gets the message data.
    /// </summary>
    public OpmData Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Opm"/> class.
    /// </summary>
    /// <param name="header">The message header.</param>
    /// <param name="metadata">The message metadata.</param>
    /// <param name="data">The message data.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public Opm(CcsdsHeader header, OpmMetadata metadata, OpmData data)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the object name from the metadata.
    /// </summary>
    public string ObjectName => Metadata.ObjectName;

    /// <summary>
    /// Gets the object ID (international designator) from the metadata.
    /// </summary>
    public string ObjectId => Metadata.ObjectId;

    /// <summary>
    /// Gets the epoch of the state vector.
    /// </summary>
    public DateTime Epoch => Data.StateVector.Epoch;

    /// <summary>
    /// Gets a value indicating whether this OPM contains Keplerian elements.
    /// </summary>
    public bool HasKeplerianElements => Data.HasKeplerianElements;

    /// <summary>
    /// Gets a value indicating whether this OPM contains spacecraft parameters.
    /// </summary>
    public bool HasSpacecraftParameters => Data.HasSpacecraftParameters;

    /// <summary>
    /// Gets a value indicating whether this OPM contains a covariance matrix.
    /// </summary>
    public bool HasCovariance => Data.HasCovariance;

    /// <summary>
    /// Gets a value indicating whether this OPM contains maneuver parameters.
    /// </summary>
    public bool HasManeuvers => Data.HasManeuvers;

    /// <summary>
    /// Creates an OPM from a state vector.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="stateVector">The state vector.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new OPM instance.</returns>
    /// <remarks>
    /// REF_FRAME is written as the CCSDS name of the state vector frame (#339): <c>ICRF</c> for <see cref="Frame.ICRF"/>
    /// (the SPICE frame <c>J2000</c>), <c>GCRF</c> for <see cref="Frame.GCRF"/>, the frame's own name otherwise (<c>TEME</c>,
    /// <c>ECLIPJ2000</c>, ...). <c>ICRF</c> and <c>GCRF</c> denote the ICRF axes and are both read back as
    /// <see cref="Frame.ICRF"/>; no frame bias is applied. Before 11.0.0 <c>J2000</c> was written for
    /// <see cref="Frame.ICRF"/>, which is not a CCSDS frame name.
    /// The epoch is written in UTC under <c>TIME_SYSTEM = UTC</c>, whatever the time scale of the state's epoch (#479).
    /// </remarks>
    public static Opm CreateFromStateVector(
        string objectName,
        string objectId,
        StateVector stateVector,
        string originator = null)
    {
        if (stateVector == null)
            throw new ArgumentNullException(nameof(stateVector));

        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OpmMetadata(
            objectName,
            objectId,
            stateVector.Observer.Name,
            CcsdsFrameNames.ToCcsdsName(stateVector.Frame),
            "UTC");

        var opmStateVector = OpmStateVector.FromStateVector(stateVector);

        // Include covariance if present (convert from framework units m² to CCSDS units km²)
        CovarianceMatrix covariance = null;
        if (stateVector.Covariance.HasValue)
        {
            covariance = CovarianceMatrix.FromMatrixWithUnitConversion(stateVector.Covariance.Value);
        }

        var data = new OpmData(opmStateVector, covariance: covariance);

        return new Opm(header, metadata, data);
    }

    /// <summary>
    /// Creates a minimal OPM with only the required fields.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID.</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="stateVector">The state vector.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new minimal OPM instance.</returns>
    public static Opm CreateMinimal(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        OpmStateVector stateVector,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OpmMetadata(
            objectName, objectId, centerName, referenceFrame, timeSystem);

        var data = new OpmData(stateVector);

        return new Opm(header, metadata, data);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"OPM[{ObjectName}, {ObjectId}, {Epoch:O}]";
    }

    /// <summary>
    /// The <c>EPOCH</c> of the state as a <see cref="Time"/> in the <c>TIME_SYSTEM</c> of the metadata (R22 of #668, lot
    /// L7): the clock reading of <see cref="Epoch"/> in UTC, TAI, TDB, TT or GPS, the epoch <see cref="ToStateVector"/>
    /// reads (#485).
    /// </summary>
    /// <returns>The epoch, labelled in the scale of <c>TIME_SYSTEM</c>.</returns>
    /// <exception cref="InvalidOperationException">The <c>TIME_SYSTEM</c> is none of UTC, TAI, TDB, TT and GPS (UT1, TCB,
    /// MET, ...), which a message may hold and <see cref="Time"/> does not.</exception>
    public Time GetEpoch() =>
        CcsdsTimeSystemMapping.ToTime(Data.StateVector.Epoch,
            CcsdsTimeSystemMapping.ToTimeScale(Metadata.TimeSystemEnum, Metadata.TimeSystem));

    #region Framework Integration

    /// <summary>
    /// Converts this OPM to a framework StateVector.
    /// </summary>
    /// <param name="observer">
    /// The centre of the state. If null, the centre <c>CENTER_NAME</c> names: a <see cref="Barycenter"/> for the barycenters
    /// 0 to 9, otherwise a <see cref="CelestialBody"/> in ICRF at the epoch (the Sun, a planet or a moon).
    /// </param>
    /// <returns>A new StateVector instance with the OPM data.</returns>
    /// <exception cref="InvalidOperationException">The <c>TIME_SYSTEM</c> is none of UTC, TAI, TDB, TT and GPS.</exception>
    /// <exception cref="ArgumentException">
    /// No observer is given and <c>CENTER_NAME</c> is not a centre the library identifies (a site, a spacecraft, a body that
    /// only a kernel names): pass the observer. Or the observer's NAIF id differs from the centre <c>CENTER_NAME</c> names.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <c>CENTER_NAME</c> is read trimmed and in any letter case: the SPICE name of a barycenter (<c>SOLAR SYSTEM
    /// BARYCENTER</c>, <c>EARTH BARYCENTER</c>, ...), of the Sun, of a planet or of a moon, the names the writers emit, or
    /// one of NAIF's built-in synonyms of the barycenters (<c>SSB</c>, <c>SOLAR_SYSTEM_BARYCENTER</c>, <c>EMB</c>,
    /// <c>EARTH MOON BARYCENTER</c>, <c>EARTH-MOON BARYCENTER</c>, <c>&lt;PLANET&gt;_BARYCENTER</c>; NAIF IDs Required
    /// Reading). Before 11.0.0 the observer was the Earth whatever <c>CENTER_NAME</c> said (#679). A given observer with a
    /// <c>CENTER_NAME</c> outside these names (a ground station, a spacecraft) is used as given.
    /// </para>
    /// <para>
    /// The epoch is read in the <c>TIME_SYSTEM</c> of the metadata: UTC, TAI, TDB, TT or GPS (#485). Before 11.0.0 it was
    /// read as UTC whatever the time system, 69.19 s late for a TDB file.
    /// </para>
    /// <para>
    /// Frames: CCSDS <c>ICRF</c> and <c>GCRF</c> map exactly onto <see cref="Frame.ICRF"/>, whose axes are the ICRF's.
    /// <c>EME2000</c> also maps onto <see cref="Frame.ICRF"/>: the ~23 mas IAU 2006 frame bias between EME2000 and the
    /// ICRF is ignored (about 0.8 m on a LEO position, 4.7 m on a GEO position).
    /// </para>
    /// <para>
    /// If the OPM contains a covariance matrix with a reference frame that differs from the state vector
    /// reference frame (e.g., covariance in RTN while state vector in ICRF), the covariance is included
    /// as-is without frame transformation. The caller is responsible for frame transformation if needed.
    /// </para>
    /// <para>
    /// Common covariance frame combinations:
    /// <list type="bullet">
    /// <item>State in ICRF, Covariance in ICRF: No transformation needed</item>
    /// <item>State in ICRF, Covariance in RTN/RSW: Requires transformation for proper uncertainty propagation</item>
    /// </list>
    /// </para>
    /// </remarks>
    public StateVector ToStateVector(ILocalizable observer = null)
    {
        // The epoch in the TIME_SYSTEM of the metadata (#485); a time system the framework does not convert throws
        var timeScale = CcsdsTimeSystemMapping.ToTimeScale(Metadata.TimeSystemEnum, Metadata.TimeSystem);

        // The centre CENTER_NAME names (#679): built when no observer is given, checked against the one given
        observer = CcsdsCenterNames.Observer(Metadata.CenterName, observer, CcsdsTimeSystemMapping.ToTime(Data.StateVector.Epoch, timeScale));

        // Get the reference frame from metadata
        var frame = CcsdsFrameNames.ToFrame(Metadata.ReferenceFrameEnum, Metadata.ReferenceFrame);

        // Convert covariance from CCSDS units (km²) to framework units (m²)
        // Note: If covariance is in a different frame than the state vector, the caller
        // must handle frame transformation. Check CovarianceReferenceFrameConsistent property.
        Matrix? covariance = Data.Covariance?.ToMatrixWithUnitConversion();

        return Data.StateVector.ToStateVector(observer, frame, covariance, timeScale);
    }

    /// <summary>
    /// Gets a value indicating whether the covariance reference frame is consistent with the state vector frame.
    /// </summary>
    /// <remarks>
    /// Returns true if no covariance is present, or if the covariance reference frame matches
    /// the metadata reference frame. Returns false if the frames differ, which may require
    /// frame transformation for proper uncertainty propagation.
    /// </remarks>
    public bool CovarianceReferenceFrameConsistent
    {
        get
        {
            if (!HasCovariance || string.IsNullOrEmpty(Data.Covariance.ReferenceFrame))
                return true;

            // Compare covariance frame with metadata frame (case-insensitive)
            return string.Equals(Data.Covariance.ReferenceFrame, Metadata.ReferenceFrame,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Converts this OPM to a framework Spacecraft.
    /// </summary>
    /// <param name="naifId">The NAIF identifier for the spacecraft, in [-2147483, -1].</param>
    /// <param name="maxOperatingMass">The maximum operating mass in kilograms, double.MaxValue by default: at least the
    /// MASS of the OPM, or the constructor throws (decision 77 of #668, decided by S. Guillet, 2026-11-23; before 11.0.0 a
    /// maximum below the mass was raised to it without a word, and the parameter was named
    /// <c>maximumOperatingMass</c>).</param>
    /// <param name="clock">The spacecraft clock, by default a new clock named OBJECT_NAME + "_CLK" at 65536 ticks per
    /// second, as the propagated constructor of <see cref="Spacecraft"/> (required before 11.0.0).</param>
    /// <param name="observer">The centre of the state. If null, the centre <c>CENTER_NAME</c> names, as on <see cref="ToStateVector"/>.</param>
    /// <returns>A new Spacecraft instance with the OPM data.</returns>
    /// <remarks>
    /// OPM provides: OBJECT_NAME (name), OBJECT_ID (cosparId), stateVector, and optionally spacecraftParameters.
    /// The NAIF id is not part of the CCSDS OPM standard; the maximum operating mass and the clock are optional.
    /// If spacecraftParameters is not present in the OPM, default values are used for mass (1.0 kg),
    /// sectionalArea (1.0 m²), dragCoefficient (2.2), and solarRadiationCoefficient (1.0).
    /// The sectional area, which the spacecraft's drag and radiation pressure share, is <c>DRAG_AREA</c> or
    /// <c>SOLAR_RAD_AREA</c>, whichever is given, the value when both are given equal (#805, decision 112 of #668, decided by
    /// S. Guillet, 2026-11-26; before 11.0.0 <c>SOLAR_RAD_AREA</c> was never read).
    /// The reference frame and the epoch's time system are mapped as described on <see cref="ToStateVector"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><c>DRAG_AREA</c> and <c>SOLAR_RAD_AREA</c> are given with different
    /// values, which one sectional area cannot hold (#805): read the state with <see cref="ToStateVector"/> and build the
    /// <see cref="Spacecraft"/> with the area of your choice; checked before anything is built, a clock given left
    /// unattached.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when naifId is not in [-2147483, -1], or, naming it, when
    /// <paramref name="maxOperatingMass"/> is not finite or below the MASS of the OPM.</exception>
    /// <exception cref="ArgumentException">
    /// No observer is given and <c>CENTER_NAME</c> is not a centre the library identifies, or the observer contradicts it
    /// (see <see cref="ToStateVector"/>), or <paramref name="clock"/> is attached to a spacecraft of another NAIF id.
    /// </exception>
    public Spacecraft ToSpacecraft(int naifId, double maxOperatingMass = double.MaxValue, Clock clock = null, ILocalizable observer = null)
    {
        // The one sectional area of a spacecraft, for drag and radiation pressure alike: DRAG_AREA or SOLAR_RAD_AREA, equal
        // when both are given (#805, decision 112 of #668, decided by S. Guillet, 2026-11-26)
        if (Data.DragArea.HasValue && Data.SolarRadiationPressureArea.HasValue &&
            Data.DragArea.Value != Data.SolarRadiationPressureArea.Value)
        {
            throw new InvalidOperationException(
                $"DRAG_AREA {Data.DragArea.Value.ToString("R", CultureInfo.InvariantCulture)} m² and SOLAR_RAD_AREA " +
                $"{Data.SolarRadiationPressureArea.Value.ToString("R", CultureInfo.InvariantCulture)} m² differ, and a " +
                "Spacecraft has one sectional area for drag and radiation pressure: read the state with ToStateVector() and " +
                "build the Spacecraft with the area of your choice.");
        }

        // Get state vector
        var stateVector = ToStateVector(observer);

        // Extract spacecraft parameters from OPM (with defaults if not present)
        double mass = Data.Mass ?? 1.0;
        double sectionalArea = Data.DragArea ?? Data.SolarRadiationPressureArea ?? 1.0;
        double dragCoefficient = Data.DragCoefficient ?? 2.2;
        double solarRadiationCoefficient = Data.SolarRadiationCoefficient ?? 1.0;

        return new Spacecraft(
            naifId,
            ObjectName,
            mass,
            stateVector,
            maxOperatingMass,
            clock,
            sectionalArea,
            dragCoefficient,
            ObjectId,  // COSPAR ID from OPM OBJECT_ID
            solarRadiationCoefficient);
    }

    #endregion

    #region Read and write (R22 of #668)

    // The one verb set of the CCSDS messages over the internal reader and validator
    private static readonly CcsdsMessageIO<Opm, OpmValidationResult> Format = new("OPM",
        DefaultValidator.ValidateSchema, DefaultValidator.ValidateSchemaFromXml, DefaultReader.Read, ContentValidator,
        result => result.IsValid, (message, result) => new OpmValidationException(message, result),
        (message, inner) => new OpmParseException(message, inner));

    // The content check with the validator switches of the read options, the default validator when none is set; an OPM
    // has no TLE parameters to check
    private static Func<Opm, OpmValidationResult> ContentValidator(CcsdsReadOptions options)
    {
        if (options.ValidateTleRequirements == false)
            throw new ArgumentException("An OPM holds no TLE parameters: ValidateTleRequirements cannot be turned off for it.", nameof(options));

        return options.ValidatePhysicalConstraints is null && options.WarnOnMissingOptionalFields is null
            ? DefaultValidator.Validate
            : new OpmValidator
            {
                ValidatePhysicalConstraints = options.ValidatePhysicalConstraints ?? true,
                WarnOnMissingOptionalFields = options.WarnOnMissingOptionalFields ?? true
            }.Validate;
    }

    /// <summary>
    /// Reads the one OPM of a file: a standalone <c>opm</c> or an <c>ndm</c> that holds one (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default (<see cref="CcsdsReadOptions.Default"/>): the NDM/XML 4.0.0
    /// schema in the form of the document, then the content.</param>
    /// <returns>The OPM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OpmParseException">The document cannot be read: no OPM, several (<see cref="LoadAll"/> reads them), or a
    /// keyword whose value the data model refuses, naming the keyword and its value.</exception>
    public static Opm Load(string path, CcsdsReadOptions options = null) => Format.Load(path, options, false)[0];

    /// <summary>
    /// Reads every OPM of a file, in document order, as <see cref="Load"/> reads one: the schema checked once for the
    /// document, the content of each message (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages; a document that holds none throws.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OpmParseException">The document holds no OPM or one that cannot be read.</exception>
    public static IReadOnlyList<Opm> LoadAll(string path, CcsdsReadOptions options = null) => Format.Load(path, options, true);

    /// <summary>
    /// Reads the one OPM of an XML text, as <see cref="Load"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The OPM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OpmParseException">The text is blank or cannot be read, or holds no OPM or several
    /// (<see cref="ParseAll(string, CcsdsReadOptions)"/> reads them).</exception>
    public static Opm Parse(string xml, CcsdsReadOptions options = null) => Format.Parse(xml, options, false)[0];

    /// <summary>
    /// Reads the one OPM of a reader, read to its end, as <see cref="Parse(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The OPM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OpmParseException">The text cannot be read, or holds no OPM or several.</exception>
    public static Opm Parse(TextReader reader, CcsdsReadOptions options = null) => Format.Parse(reader, options, false)[0];

    /// <summary>
    /// Reads every OPM of an XML text, as <see cref="LoadAll"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OpmParseException">The text is blank or cannot be read, or holds no OPM.</exception>
    public static IReadOnlyList<Opm> ParseAll(string xml, CcsdsReadOptions options = null) => Format.Parse(xml, options, true);

    /// <summary>
    /// Reads every OPM of a reader, read to its end, as <see cref="ParseAll(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="OpmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OpmParseException">The text cannot be read, or holds no OPM.</exception>
    public static IReadOnlyList<Opm> ParseAll(TextReader reader, CcsdsReadOptions options = null) => Format.Parse(reader, options, true);

    /// <summary>
    /// <see cref="Load"/> without an exception for a file that does not exist or that the checks or the reader refuse.
    /// </summary>
    /// <param name="path">The path of the file, or null.</param>
    /// <param name="result">The OPM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the file exists and holds one OPM that passes the checks; an error reading an existing file
    /// throws, as <see cref="Load"/> does. Use <see cref="Load"/> for the reason of a refusal.</returns>
    public static bool TryLoad(string path, out Opm result, CcsdsReadOptions options = null) => Format.TryLoad(path, options, out result);

    /// <summary>
    /// <see cref="Parse(string, CcsdsReadOptions)"/> without an exception for a text that the checks or the reader refuse.
    /// </summary>
    /// <param name="xml">The XML text, or null.</param>
    /// <param name="result">The OPM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the text holds one OPM that passes the checks.</returns>
    public static bool TryParse(string xml, out Opm result, CcsdsReadOptions options = null) => Format.TryParse(xml, options, out result);

    /// <summary>
    /// Writes the OPM to a file, in UTF-8, replacing it (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented
    /// (<see cref="CcsdsWriteOptions.Default"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or blank. Or a date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    /// <exception cref="OpmValidationException">The content check fails; nothing is written.</exception>
    public void Save(string path, CcsdsWriteOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("The path is empty or blank.", nameof(path));
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new OpmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToFile(this, path);
    }

    /// <summary>
    /// Writes the OPM to a stream, in UTF-8, as <see cref="Save(string, CcsdsWriteOptions)"/>; the stream stays open.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="OpmValidationException">The content check fails; nothing is written.</exception>
    /// <exception cref="ArgumentException">A date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    public void Save(Stream stream, CcsdsWriteOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new OpmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToStream(this, stream);
    }

    /// <summary>
    /// The OPM as an XML text, as <see cref="Save(string, CcsdsWriteOptions)"/> writes it (R22 of #668).
    /// </summary>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented; before 11.0.0
    /// the text was not validated.</param>
    /// <returns>The XML text.</returns>
    /// <exception cref="OpmValidationException">The content check fails.</exception>
    /// <exception cref="ArgumentException">A date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    public string ToXml(CcsdsWriteOptions options = null)
    {
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        return new OpmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToString(this);
    }

    /// <summary>
    /// Validates the content of this OPM: required values, ranges, consistency.
    /// </summary>
    /// <returns>The errors, warnings and notes.</returns>
    public OpmValidationResult Validate() => DefaultValidator.Validate(this);

    /// <summary>
    /// Whether <see cref="Validate"/> reports no error.
    /// </summary>
    /// <returns>True when the content holds no error.</returns>
    public bool IsValid() => Validate().IsValid;

    #endregion
}

