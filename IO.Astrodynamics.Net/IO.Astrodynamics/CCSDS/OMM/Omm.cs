// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents a CCSDS Orbit Mean-elements Message (OMM).
/// </summary>
/// <remarks>
/// OMM is a CCSDS Navigation Data Message format for exchanging mean orbital elements.
/// The framework supports SGP4 mean element theory (which includes SDP4 for deep space).
/// See CCSDS 502.0-B-3 (ODM Blue Book) for the complete specification.
/// </remarks>
public class Omm
{
    private static readonly OmmReader DefaultReader = new();
    private static readonly OmmValidator DefaultValidator = new();

    /// <summary>
    /// Largest revolution number at epoch representable in the 5-character TLE field.
    /// </summary>
    private const int MAX_TLE_REVOLUTION_NUMBER = 99999;

    // The mean motion field of a TLE's line 2, columns 53-63: NN.NNNNNNNN rev/day
    private const string TLE_MEAN_MOTION_FORMAT = "0.00000000";
    private const int TLE_MEAN_MOTION_FIELD_LENGTH = 11;
    /// <summary>
    /// The CCSDS OMM format version.
    /// </summary>
    public const string Version = "3.0";

    /// <summary>
    /// The CCSDS OMM format identifier.
    /// </summary>
    public const string FormatId = "CCSDS_OMM_VERS";

    /// <summary>
    /// Gets the message header.
    /// </summary>
    public CcsdsHeader Header { get; }

    /// <summary>
    /// Gets the message metadata.
    /// </summary>
    public OmmMetadata Metadata { get; }

    /// <summary>
    /// Gets the message data.
    /// </summary>
    public OmmData Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Omm"/> class.
    /// </summary>
    /// <param name="header">The message header.</param>
    /// <param name="metadata">The message metadata.</param>
    /// <param name="data">The message data.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public Omm(CcsdsHeader header, OmmMetadata metadata, OmmData data)
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
    /// Gets the epoch of the orbital elements.
    /// </summary>
    public DateTime Epoch => Data.MeanElements.Epoch;

    /// <summary>
    /// Gets a value indicating whether this OMM can become a TLE: it holds TLE parameters, its <c>MEAN_ELEMENT_THEORY</c> is
    /// SGP4, its <c>REF_FRAME</c> TEME and its <c>CENTER_NAME</c> the Earth.
    /// </summary>
    /// <remarks>
    /// A TLE holds SGP4 mean elements of an Earth orbit in TEME, and the library runs the classic SGP4 only (CSPICE
    /// <c>evsgp4_c</c>), so SGP4-XP, DSST, another frame or another centre is not compatible. The centre, the frame and
    /// the theory are compared trimmed and in any letter case (<c>EARTH</c>, <c>earth</c>, <c> TEME </c>). Before 11.0.0
    /// only the TLE parameters were checked (#679).
    /// </remarks>
    public bool IsTleCompatible => Data.HasTleParameters && TleMetadataFaults().Count == 0;

    /// <summary>
    /// The metadata keywords that keep this OMM from being a TLE, each with its value, in metadata order.
    /// </summary>
    private List<string> TleMetadataFaults()
    {
        var faults = new List<string>();
        if (!CcsdsCenterNames.TryGetNaifId(Metadata.CenterName, out var centerId) || centerId != 399)
            faults.Add($"CENTER_NAME '{Metadata.CenterName}' is not EARTH");
        // REF_FRAME and MEAN_ELEMENT_THEORY are xsd:string, read untrimmed: trimmed here, as CENTER_NAME
        if (!IsKeyword(Metadata.ReferenceFrame, "TEME"))
            faults.Add($"REF_FRAME '{Metadata.ReferenceFrame}' is not TEME");
        if (!IsKeyword(Metadata.MeanElementTheory, "SGP4"))
            faults.Add($"MEAN_ELEMENT_THEORY '{Metadata.MeanElementTheory}' is not SGP4");
        return faults;
    }

    // The value is the keyword value, trimmed and in any letter case
    private static bool IsKeyword(string value, string expected)
    {
        return value.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a value indicating whether this OMM contains a covariance matrix.
    /// </summary>
    public bool HasCovariance => Data.HasCovarianceMatrix;

    /// <summary>
    /// Creates an OMM message suitable for TLE/SGP4 propagation.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="epoch">The epoch of the elements.</param>
    /// <param name="meanMotion">Mean motion in rev/day.</param>
    /// <param name="eccentricity">Eccentricity.</param>
    /// <param name="inclination">Inclination in degrees.</param>
    /// <param name="raan">Right ascension of ascending node in degrees.</param>
    /// <param name="argOfPericenter">Argument of pericenter in degrees.</param>
    /// <param name="meanAnomaly">Mean anomaly in degrees.</param>
    /// <param name="bstar">BSTAR drag term in 1/Earth radii.</param>
    /// <param name="meanMotionDot">First derivative of mean motion in rev/day².</param>
    /// <param name="meanMotionDDot">Second derivative of mean motion in rev/day³.</param>
    /// <param name="noradCatalogId">Optional NORAD catalog ID.</param>
    /// <param name="elementSetNumber">Optional element set number.</param>
    /// <param name="revolutionNumber">Optional revolution number at epoch.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new OMM instance configured for TLE/SGP4.</returns>
    public static Omm CreateForTle(
        string objectName,
        string objectId,
        DateTime epoch,
        double meanMotion,
        double eccentricity,
        double inclination,
        double raan,
        double argOfPericenter,
        double meanAnomaly,
        double bstar,
        double meanMotionDot,
        double meanMotionDDot,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumber = null,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = OmmMetadata.CreateForSgp4(objectName, objectId);

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch, meanMotion, eccentricity, inclination, raan, argOfPericenter, meanAnomaly);

        var tleParams = TleParameters.CreateWithBStarAndDDot(
            bstar, meanMotionDot, meanMotionDDot,
            noradCatalogId: noradCatalogId,
            elementSetNumber: elementSetNumber,
            revolutionNumberAtEpoch: revolutionNumber,
            classificationType: "U");

        var data = OmmData.CreateForTle(meanElements, tleParams);

        return new Omm(header, metadata, data);
    }

    /// <summary>
    /// Creates a minimal OMM message with only the required fields.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID.</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="meanElementTheory">The mean element theory.</param>
    /// <param name="meanElements">The mean orbital elements.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new minimal OMM instance.</returns>
    public static Omm CreateMinimal(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        string meanElementTheory,
        MeanElements meanElements,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OmmMetadata(
            objectName, objectId, centerName, referenceFrame, timeSystem, meanElementTheory);

        var data = OmmData.CreateMinimal(meanElements);

        return new Omm(header, metadata, data);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var tleMarker = IsTleCompatible ? " (TLE)" : "";
        return $"OMM[{ObjectName}, {ObjectId}, {Epoch:O}{tleMarker}]";
    }

    #region Framework Integration

    /// <summary>
    /// Converts this OMM to a TLE (Two-Line Element) set.
    /// </summary>
    /// <returns>A new TLE instance with the same orbital elements.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the OMM does not contain TLE parameters (NORAD catalog ID required), when its
    /// <c>MEAN_ELEMENT_THEORY</c> is not SGP4, its <c>REF_FRAME</c> not TEME or its <c>CENTER_NAME</c> not the Earth (the
    /// message names each keyword at fault with its value; see <see cref="IsTleCompatible"/>), or when required mean motion
    /// is not available, or when a value cannot be represented in its TLE field (the NORAD catalog ID above 339999, the
    /// revolution number above 99999, <c>MEAN_MOTION</c> above the 99.99999999 rev/day of its 11 characters, #729).
    /// </exception>
    /// <remarks>
    /// <para>
    /// This conversion requires TLE parameters to be present in the OMM,
    /// including the NORAD catalog ID which is mandatory for TLE format.
    /// </para>
    /// <para>
    /// The object ID (international designator) is used as the COSPAR ID in the TLE. <c>OBJECT_ID = UNKNOWN</c> (trimmed,
    /// in any letter case), which <see cref="OrbitalParameters.TLE.Tle.ToOmm"/> writes for a TLE without a designator, gives
    /// blank columns 10-17 (#604, decided by S. Guillet, 2026-11-18; before 11.0.0 the TLE could not be built).
    /// </para>
    /// <para>
    /// The OMM must be an SGP4 OMM in TEME about the Earth (<see cref="IsTleCompatible"/>): the TLE is in TEME, the frame
    /// of SGP4/SDP4 mean elements, about the Earth, and no frame or centre conversion is involved. Before 11.0.0 any OMM with
    /// TLE parameters gave an Earth TEME TLE, whatever its <c>CENTER_NAME</c>, <c>REF_FRAME</c> or <c>MEAN_ELEMENT_THEORY</c>
    /// said (#679).
    /// </para>
    /// <para>
    /// <c>EPOCH</c> is read in <c>TIME_SYSTEM</c> (UTC, TAI, TDB, TT or GPS) and written as its UTC, the time scale of a
    /// TLE epoch (#485); another time system throws <see cref="InvalidOperationException"/>. Before 11.0.0 it was read as
    /// UTC whatever the time system.
    /// </para>
    /// </remarks>
    public OrbitalParameters.TLE.Tle ToTle()
    {
        // Validate TLE compatibility
        if (!Data.HasTleParameters)
            throw new InvalidOperationException("OMM does not contain TLE parameters. Cannot convert to TLE.");

        // A TLE holds SGP4 mean elements of an Earth orbit in TEME (#679)
        var faults = TleMetadataFaults();
        if (faults.Count > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join("; ", faults)}: a TLE holds SGP4 mean elements of an Earth orbit in TEME. Cannot convert to TLE.");
        }

        var tleParams = Data.TleParameters;

        if (!tleParams.NoradCatalogId.HasValue)
            throw new InvalidOperationException("NORAD catalog ID is required to create a TLE.");

        var meanElements = Data.MeanElements;

        if (!meanElements.MeanMotion.HasValue)
            throw new InvalidOperationException("Mean motion is required to create a TLE. OMM uses semi-major axis instead.");

        // The mean motion field of line 2 (columns 53-63, NN.NNNNNNNN) holds at most 99.99999999 rev/day. Beyond it the
        // value cannot be represented (#729: above about 2.86e307 rev/day its conversion to rad/s and back overflowed and the
        // TLE was written with the text Infinity)
        var meanMotionText = meanElements.MeanMotion.Value.ToString(TLE_MEAN_MOTION_FORMAT, CultureInfo.InvariantCulture);
        if (meanMotionText.Length > TLE_MEAN_MOTION_FIELD_LENGTH)
        {
            throw new InvalidOperationException(
                $"MEAN_MOTION {meanElements.MeanMotion.Value.ToString("R", CultureInfo.InvariantCulture)} rev/day cannot be " +
                $"represented in a TLE: the {TLE_MEAN_MOTION_FIELD_LENGTH}-character field holds at most 99.99999999 rev/day.");
        }

        // Validate BSTAR is present (required for SGP4)
        if (!tleParams.BStar.HasValue)
            throw new InvalidOperationException("BSTAR drag term is required for TLE. OMM uses BTerm instead.");

        // Create mean Keplerian elements from OMM data, at the epoch read in TIME_SYSTEM (#485), as UTC, the time scale of a
        // TLE epoch
        var timeScale = Common.Enums.CcsdsTimeSystemMapping.ToTimeScale(Metadata.TimeSystemEnum, Metadata.TimeSystem);
        var epoch = Common.Enums.CcsdsTimeSystemMapping.ToTime(meanElements.Epoch, timeScale).ToUTC();
        // The Earth in an inertial frame, as the TLE builds it (#597): ITRF93 would read the Earth's binary PCK at the epoch
        var earth = new Body.CelestialBody(399, Frames.Frame.ECLIPTIC_J2000, epoch);

        var keplerianElements = OrbitalParameters.KeplerianElements.FromOMM(
            meanElements.MeanMotion.Value,
            meanElements.Eccentricity,
            meanElements.Inclination,
            meanElements.RightAscensionOfAscendingNode,
            meanElements.ArgumentOfPericenter,
            meanElements.MeanAnomaly,
            earth,
            epoch,
            Frames.Frame.TEME);

        // Extract TLE parameters. No narrowing cast here: the OMM fields are wider than the TLE
        // ones, so out-of-range values are reported rather than silently wrapped.
        var noradId = tleParams.NoradCatalogId.Value;
        if (!OrbitalParameters.TLE.NoradCatalogNumber.IsValid(noradId))
        {
            throw new InvalidOperationException(
                $"NORAD catalog ID {noradId} cannot be represented in a TLE: the 5-character field holds at most " +
                $"{OrbitalParameters.TLE.NoradCatalogNumber.MaxValue} (Alpha-5 encoded). Keep this object in OMM form.");
        }

        // Convert OMM Object ID (e.g., "1998-067A") to TLE COSPAR format (e.g., "98067A")
        var cosparId = ConvertObjectIdToCosparId(ObjectId);
        var revolutionsAtEpoch = tleParams.RevolutionNumberAtEpoch ?? 0;
        if (revolutionsAtEpoch > MAX_TLE_REVOLUTION_NUMBER)
        {
            throw new InvalidOperationException(
                $"Revolution number at epoch {revolutionsAtEpoch} cannot be represented in a TLE: " +
                $"the 5-character field holds at most {MAX_TLE_REVOLUTION_NUMBER}.");
        }

        // Parse classification
        var classification = OrbitalParameters.TLE.TleClassification.Unclassified;
        if (!string.IsNullOrEmpty(tleParams.ClassificationType))
        {
            classification = tleParams.ClassificationType.ToUpperInvariant() switch
            {
                "C" => OrbitalParameters.TLE.TleClassification.Classified,
                "S" => OrbitalParameters.TLE.TleClassification.Secret,
                _ => OrbitalParameters.TLE.TleClassification.Unclassified
            };
        }

        var bstar = tleParams.BStar.Value;
        var nDot = tleParams.MeanMotionDot;
        var nDDot = tleParams.MeanMotionDDot ?? 0.0;
        // One default with Tle.Create and the TLE Configuration (#606)
        var elementSetNumber = tleParams.ElementSetNumber ?? OrbitalParameters.TLE.Tle.DefaultElementSetNumber;

        return OrbitalParameters.TLE.Tle.Create(
            keplerianElements,
            ObjectName,
            noradId,
            cosparId,
            revolutionsAtEpoch,
            classification,
            bstar,
            nDot,
            nDDot,
            elementSetNumber);
    }

    /// <summary>
    /// Converts an OMM Object ID (international designator) to TLE COSPAR format.
    /// </summary>
    /// <param name="objectId">The OMM Object ID (e.g., "1998-067A").</param>
    /// <returns>The TLE COSPAR ID (e.g., "98067A"); null, the blank designator, for <c>UNKNOWN</c> (trimmed, in any letter
    /// case) or no value (#604).</returns>
    /// <remarks>
    /// OMM format: YYYY-NNNP (e.g., "1998-067A")
    /// TLE format: YYNNNP (e.g., "98067A")
    /// - YY: Last two digits of launch year
    /// - NNN: Sequential launch number
    /// - P: Piece identifier (A, B, C, etc.)
    /// </remarks>
    internal static string ConvertObjectIdToCosparId(string objectId)
    {
        // UNKNOWN (trimmed, any case), or no value, is the blank designator of a TLE (#604); OmmMetadata refuses an empty
        // OBJECT_ID (#708), so only UNKNOWN reaches here from an OMM
        if (string.IsNullOrWhiteSpace(objectId) ||
            string.Equals(objectId.Trim(), OmmMetadata.UnknownObjectId, StringComparison.OrdinalIgnoreCase))
            return null;

        // Remove hyphen if present
        var cleanId = objectId.Replace("-", "");

        // If in full year format (e.g., "1998067A" = 8 chars with 4-digit year), remove century prefix
        // This must be checked before the 6-8 char check to handle "YYYY-NNNP" format
        if (cleanId.Length >= 7 && char.IsDigit(cleanId[0]) && char.IsDigit(cleanId[1]) &&
            char.IsDigit(cleanId[2]) && char.IsDigit(cleanId[3]))
        {
            // Check if first 4 chars form a valid year (1957-2056)
            if (int.TryParse(cleanId.Substring(0, 4), out var year4) && year4 >= 1957 && year4 <= 2099)
            {
                // Remove first two digits (century)
                return cleanId.Substring(2);
            }
        }

        // If already in TLE format (6-7 chars starting with 2-digit year), return as-is
        if (cleanId.Length >= 6 && cleanId.Length <= 7)
            return cleanId;

        // Return original if we can't parse it
        return cleanId;
    }

    #endregion

    #region Epoch and mean elements (R22 of #668)

    /// <summary>
    /// The <c>EPOCH</c> of the mean elements as a <see cref="Time"/> in the <c>TIME_SYSTEM</c> of the metadata (R22 of
    /// #668, lot L7): the clock reading of <see cref="Epoch"/> in UTC, TAI, TDB, TT or GPS, the epoch <see cref="ToTle"/>
    /// and <see cref="ToMeanElements"/> read (#485).
    /// </summary>
    /// <returns>The epoch, labelled in the scale of <c>TIME_SYSTEM</c>.</returns>
    /// <exception cref="InvalidOperationException">The <c>TIME_SYSTEM</c> is none of UTC, TAI, TDB, TT and GPS (UT1, TCB,
    /// MET, ...), which a message may hold and <see cref="Time"/> does not.</exception>
    public Time GetEpoch() =>
        Common.Enums.CcsdsTimeSystemMapping.ToTime(Data.MeanElements.Epoch,
            Common.Enums.CcsdsTimeSystemMapping.ToTimeScale(Metadata.TimeSystemEnum, Metadata.TimeSystem));

    /// <summary>
    /// The mean elements of the message in SI units (m, rad), mean Keplerian elements (R22 of #668, lot L7): about the
    /// centre <c>CENTER_NAME</c> names, in the frame of <c>REF_FRAME</c>, at <see cref="GetEpoch"/>, as
    /// <see cref="OPM.Opm.ToStateVector"/> reads an OPM.
    /// </summary>
    /// <param name="observer">The centre of the elements. If null, the centre <c>CENTER_NAME</c> names: a
    /// <see cref="Barycenter"/> for the barycenters 0 to 9, otherwise a <see cref="CelestialBody"/> in ICRF at the epoch
    /// (the Sun, a planet or a moon); a given observer that contradicts <c>CENTER_NAME</c> throws.</param>
    /// <returns>Mean <see cref="OrbitalParameters.KeplerianElements"/>: from <c>MEAN_MOTION</c>, the mean motion of the message
    /// kept as given (the elements of <c>KeplerianElements.FromOMM</c>, public before 11.0.0, bit for bit), the semi-major
    /// axis ∛(GM/n²) of the observer's GM; from <c>SEMI_MAJOR_AXIS</c>, that axis in m.</returns>
    /// <remarks>
    /// The GM is the observer's, from the kernels, as <c>FromOMM</c> took it; a <c>GM</c> keyword of the message is not read.
    /// <c>REF_FRAME</c> <c>ICRF</c>, <c>GCRF</c> and <c>EME2000</c> map onto <see cref="Frame.ICRF"/>, <c>TEME</c> onto
    /// <see cref="Frame.TEME"/>. Mean elements give no state: an SGP4 message gives its state through <see cref="ToTle"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The <c>TIME_SYSTEM</c> is none of UTC, TAI, TDB, TT and GPS.</exception>
    /// <exception cref="ArgumentException">No observer is given and <c>CENTER_NAME</c> is not a centre the library
    /// identifies, or the observer contradicts it (parameter <c>observer</c>).</exception>
    public OrbitalParameters.KeplerianElements ToMeanElements(ILocalizable observer = null)
    {
        var epoch = GetEpoch();
        observer = CcsdsCenterNames.Observer(Metadata.CenterName, observer, epoch);
        var frame = CcsdsFrameNames.ToFrame(Metadata.ReferenceFrameEnum, Metadata.ReferenceFrame);
        var m = Data.MeanElements;
        if (m.MeanMotion.HasValue)
        {
            return OrbitalParameters.KeplerianElements.FromOMM(m.MeanMotion.Value, m.Eccentricity, m.Inclination,
                m.RightAscensionOfAscendingNode, m.ArgumentOfPericenter, m.MeanAnomaly, observer, epoch, frame);
        }

        return new OrbitalParameters.KeplerianElements(m.SemiMajorAxis!.Value * 1000.0, m.Eccentricity,
            m.Inclination * Constants.Deg2Rad, m.RightAscensionOfAscendingNode * Constants.Deg2Rad,
            m.ArgumentOfPericenter * Constants.Deg2Rad, m.MeanAnomaly * Constants.Deg2Rad, observer, epoch, frame,
            OrbitalParameters.OrbitalElementsType.Mean);
    }

    #endregion

    #region Read and write (R22 of #668)

    // The one verb set of the CCSDS messages over the internal reader and validator
    private static readonly CcsdsMessageIO<Omm, OmmValidationResult> Format = new("OMM",
        DefaultValidator.ValidateSchema, DefaultValidator.ValidateSchemaFromXml, DefaultReader.Read, ContentValidator,
        result => result.IsValid, (message, result) => new OmmValidationException(message, result),
        (message, inner) => new OmmParseException(message, inner));

    // The content check with the validator switches of the read options, the default validator when none is set
    private static Func<Omm, OmmValidationResult> ContentValidator(CcsdsReadOptions options) =>
        options.ValidatePhysicalConstraints is null && options.ValidateTleRequirements is null && options.WarnOnMissingOptionalFields is null
            ? DefaultValidator.Validate
            : new OmmValidator
            {
                ValidatePhysicalConstraints = options.ValidatePhysicalConstraints ?? true,
                ValidateTleRequirements = options.ValidateTleRequirements ?? true,
                WarnOnMissingOptionalFields = options.WarnOnMissingOptionalFields ?? true
            }.Validate;

    /// <summary>
    /// Reads the one OMM of a file: a standalone <c>omm</c> or an <c>ndm</c> that holds one (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default (<see cref="CcsdsReadOptions.Default"/>): the NDM/XML 4.0.0
    /// schema in the form of the document, then the content.</param>
    /// <returns>The OMM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OmmParseException">The document cannot be read: no OMM, several (<see cref="LoadAll"/> reads them), or a
    /// keyword whose value the data model refuses, naming the keyword and its value.</exception>
    public static Omm Load(string path, CcsdsReadOptions options = null) => Format.Load(path, options, false)[0];

    /// <summary>
    /// Reads every OMM of a file, in document order, as <see cref="Load"/> reads one: the schema checked once for the
    /// document, the content of each message (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages; a document that holds none throws.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OmmParseException">The document holds no OMM or one that cannot be read.</exception>
    public static IReadOnlyList<Omm> LoadAll(string path, CcsdsReadOptions options = null) => Format.Load(path, options, true);

    /// <summary>
    /// Reads the one OMM of an XML text, as <see cref="Load"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The OMM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OmmParseException">The text is blank or cannot be read, or holds no OMM or several
    /// (<see cref="ParseAll(string, CcsdsReadOptions)"/> reads them).</exception>
    public static Omm Parse(string xml, CcsdsReadOptions options = null) => Format.Parse(xml, options, false)[0];

    /// <summary>
    /// Reads the one OMM of a reader, read to its end, as <see cref="Parse(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The OMM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="OmmParseException">The text cannot be read, or holds no OMM or several.</exception>
    public static Omm Parse(TextReader reader, CcsdsReadOptions options = null) => Format.Parse(reader, options, false)[0];

    /// <summary>
    /// Reads every OMM of an XML text, as <see cref="LoadAll"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OmmParseException">The text is blank or cannot be read, or holds no OMM.</exception>
    public static IReadOnlyList<Omm> ParseAll(string xml, CcsdsReadOptions options = null) => Format.Parse(xml, options, true);

    /// <summary>
    /// Reads every OMM of a reader, read to its end, as <see cref="ParseAll(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="OmmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="OmmParseException">The text cannot be read, or holds no OMM.</exception>
    public static IReadOnlyList<Omm> ParseAll(TextReader reader, CcsdsReadOptions options = null) => Format.Parse(reader, options, true);

    /// <summary>
    /// <see cref="Load"/> without an exception for a file that does not exist or that the checks or the reader refuse.
    /// </summary>
    /// <param name="path">The path of the file, or null.</param>
    /// <param name="result">The OMM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the file exists and holds one OMM that passes the checks; an error reading an existing file
    /// throws, as <see cref="Load"/> does. Use <see cref="Load"/> for the reason of a refusal.</returns>
    public static bool TryLoad(string path, out Omm result, CcsdsReadOptions options = null) => Format.TryLoad(path, options, out result);

    /// <summary>
    /// <see cref="Parse(string, CcsdsReadOptions)"/> without an exception for a text that the checks or the reader refuse.
    /// </summary>
    /// <param name="xml">The XML text, or null.</param>
    /// <param name="result">The OMM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the text holds one OMM that passes the checks.</returns>
    public static bool TryParse(string xml, out Omm result, CcsdsReadOptions options = null) => Format.TryParse(xml, options, out result);

    /// <summary>
    /// Writes the OMM to a file, in UTF-8, replacing it (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented
    /// (<see cref="CcsdsWriteOptions.Default"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or blank. Or a date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    /// <exception cref="OmmValidationException">The content check fails; nothing is written.</exception>
    public void Save(string path, CcsdsWriteOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("The path is empty or blank.", nameof(path));
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new OmmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToFile(this, path);
    }

    /// <summary>
    /// Writes the OMM to a stream, in UTF-8, as <see cref="Save(string, CcsdsWriteOptions)"/>; the stream stays open.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="OmmValidationException">The content check fails; nothing is written.</exception>
    /// <exception cref="ArgumentException">A date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    public void Save(Stream stream, CcsdsWriteOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new OmmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToStream(this, stream);
    }

    /// <summary>
    /// The OMM as an XML text, as <see cref="Save(string, CcsdsWriteOptions)"/> writes it (R22 of #668).
    /// </summary>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented; before 11.0.0
    /// the text was not validated.</param>
    /// <returns>The XML text.</returns>
    /// <exception cref="OmmValidationException">The content check fails.</exception>
    /// <exception cref="ArgumentException">A date of the message of kind <see cref="DateTimeKind.Local"/> under a <c>TIME_SYSTEM</c> other than UTC (#617).</exception>
    public string ToXml(CcsdsWriteOptions options = null)
    {
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        return new OmmWriter { WrapInNdmContainer = options.WrapInNdmContainer, IndentOutput = options.Indent }.WriteToString(this);
    }

    /// <summary>
    /// Validates the content of this OMM: required values, ranges, consistency.
    /// </summary>
    /// <returns>The errors, warnings and notes.</returns>
    public OmmValidationResult Validate() => DefaultValidator.Validate(this);

    /// <summary>
    /// Whether <see cref="Validate"/> reports no error.
    /// </summary>
    /// <returns>True when the content holds no error.</returns>
    public bool IsValid() => Validate().IsValid;

    #endregion
}

