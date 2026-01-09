// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents TLE-specific parameters in an OMM message.
/// </summary>
/// <remarks>
/// These parameters are required when using SGP4/SDP4 propagation theory.
/// Either BSTAR or BTERM must be specified, and either MEAN_MOTION_DDOT or AGOM must be specified.
/// </remarks>
public class TleParameters
{
    /// <summary>
    /// Gets the ephemeris type (usually 0 for SGP4).
    /// </summary>
    public int? EphemerisType { get; }

    /// <summary>
    /// Gets the classification type (U=Unclassified, C=Classified, S=Secret).
    /// </summary>
    public string ClassificationType { get; }

    /// <summary>
    /// Gets the NORAD catalog ID.
    /// </summary>
    public int? NoradCatalogId { get; }

    /// <summary>
    /// Gets the element set number (0-9999).
    /// </summary>
    public int? ElementSetNumber { get; }

    /// <summary>
    /// Gets the revolution number at epoch.
    /// </summary>
    public int? RevolutionNumberAtEpoch { get; }

    /// <summary>
    /// Gets the BSTAR drag term in 1/Earth radii (mutually exclusive with BTerm).
    /// </summary>
    public double? BStar { get; }

    /// <summary>
    /// Gets the ballistic coefficient (B-term) in m²/kg (mutually exclusive with BStar).
    /// </summary>
    public double? BTerm { get; }

    /// <summary>
    /// Gets the first derivative of mean motion in rev/day².
    /// </summary>
    public double MeanMotionDot { get; }

    /// <summary>
    /// Gets the second derivative of mean motion in rev/day³ (mutually exclusive with AGom).
    /// </summary>
    public double? MeanMotionDDot { get; }

    /// <summary>
    /// Gets the radiation pressure coefficient times area/mass (A*Cr/m) in m²/kg (mutually exclusive with MeanMotionDDot).
    /// </summary>
    public double? AGom { get; }

    /// <summary>
    /// Gets optional comments associated with the TLE parameters.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TleParameters"/> class with BSTAR and MEAN_MOTION_DDOT.
    /// </summary>
    /// <param name="bstar">The BSTAR drag term in 1/Earth radii.</param>
    /// <param name="meanMotionDot">The first derivative of mean motion in rev/day².</param>
    /// <param name="meanMotionDDot">The second derivative of mean motion in rev/day³.</param>
    /// <param name="ephemerisType">Optional ephemeris type.</param>
    /// <param name="classificationType">Optional classification type.</param>
    /// <param name="noradCatalogId">Optional NORAD catalog ID.</param>
    /// <param name="elementSetNumber">Optional element set number (0-9999).</param>
    /// <param name="revolutionNumberAtEpoch">Optional revolution number at epoch.</param>
    /// <param name="comments">Optional comments.</param>
    public TleParameters(
        double bstar,
        double meanMotionDot,
        double meanMotionDDot,
        int? ephemerisType = null,
        string classificationType = null,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumberAtEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        ValidateElementSetNumber(elementSetNumber);
        ValidateRevolutionNumber(revolutionNumberAtEpoch);

        BStar = bstar;
        BTerm = null;
        MeanMotionDot = meanMotionDot;
        MeanMotionDDot = meanMotionDDot;
        AGom = null;
        EphemerisType = ephemerisType;
        ClassificationType = classificationType;
        NoradCatalogId = noradCatalogId;
        ElementSetNumber = elementSetNumber;
        RevolutionNumberAtEpoch = revolutionNumberAtEpoch;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Private constructor for factory methods.
    /// </summary>
    private TleParameters(
        double? bstar,
        double? bterm,
        double meanMotionDot,
        double? meanMotionDDot,
        double? agom,
        int? ephemerisType,
        string classificationType,
        int? noradCatalogId,
        int? elementSetNumber,
        int? revolutionNumberAtEpoch,
        IReadOnlyList<string> comments)
    {
        BStar = bstar;
        BTerm = bterm;
        MeanMotionDot = meanMotionDot;
        MeanMotionDDot = meanMotionDDot;
        AGom = agom;
        EphemerisType = ephemerisType;
        ClassificationType = classificationType;
        NoradCatalogId = noradCatalogId;
        ElementSetNumber = elementSetNumber;
        RevolutionNumberAtEpoch = revolutionNumberAtEpoch;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets a value indicating whether BSTAR is used (true) or BTerm is used (false).
    /// </summary>
    public bool UsesBStar => BStar.HasValue;

    /// <summary>
    /// Gets a value indicating whether MEAN_MOTION_DDOT is used (true) or AGOM is used (false).
    /// </summary>
    public bool UsesMeanMotionDDot => MeanMotionDDot.HasValue;

    /// <summary>
    /// Creates TleParameters with BSTAR and MEAN_MOTION_DDOT (standard SGP4).
    /// </summary>
    public static TleParameters CreateWithBStarAndDDot(
        double bstar,
        double meanMotionDot,
        double meanMotionDDot,
        int? ephemerisType = null,
        string classificationType = null,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumberAtEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        ValidateElementSetNumber(elementSetNumber);
        ValidateRevolutionNumber(revolutionNumberAtEpoch);

        return new TleParameters(
            bstar, null, meanMotionDot, meanMotionDDot, null,
            ephemerisType, classificationType, noradCatalogId, elementSetNumber, revolutionNumberAtEpoch, comments);
    }

    /// <summary>
    /// Creates TleParameters with BSTAR and AGOM (SGP4-XP).
    /// </summary>
    public static TleParameters CreateWithBStarAndAGom(
        double bstar,
        double meanMotionDot,
        double agom,
        int? ephemerisType = null,
        string classificationType = null,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumberAtEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        ValidateElementSetNumber(elementSetNumber);
        ValidateRevolutionNumber(revolutionNumberAtEpoch);

        return new TleParameters(
            bstar, null, meanMotionDot, null, agom,
            ephemerisType, classificationType, noradCatalogId, elementSetNumber, revolutionNumberAtEpoch, comments);
    }

    /// <summary>
    /// Creates TleParameters with BTerm and MEAN_MOTION_DDOT.
    /// </summary>
    public static TleParameters CreateWithBTermAndDDot(
        double bterm,
        double meanMotionDot,
        double meanMotionDDot,
        int? ephemerisType = null,
        string classificationType = null,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumberAtEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        ValidateElementSetNumber(elementSetNumber);
        ValidateRevolutionNumber(revolutionNumberAtEpoch);

        return new TleParameters(
            null, bterm, meanMotionDot, meanMotionDDot, null,
            ephemerisType, classificationType, noradCatalogId, elementSetNumber, revolutionNumberAtEpoch, comments);
    }

    /// <summary>
    /// Creates TleParameters with BTerm and AGOM.
    /// </summary>
    public static TleParameters CreateWithBTermAndAGom(
        double bterm,
        double meanMotionDot,
        double agom,
        int? ephemerisType = null,
        string classificationType = null,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumberAtEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        ValidateElementSetNumber(elementSetNumber);
        ValidateRevolutionNumber(revolutionNumberAtEpoch);

        return new TleParameters(
            null, bterm, meanMotionDot, null, agom,
            ephemerisType, classificationType, noradCatalogId, elementSetNumber, revolutionNumberAtEpoch, comments);
    }

    private static void ValidateElementSetNumber(int? elementSetNumber)
    {
        if (elementSetNumber.HasValue && (elementSetNumber.Value < 0 || elementSetNumber.Value > 9999))
            throw new ArgumentOutOfRangeException(nameof(elementSetNumber), "Element set number must be between 0 and 9999.");
    }

    private static void ValidateRevolutionNumber(int? revNumber)
    {
        if (revNumber.HasValue && revNumber.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(revNumber), "Revolution number must be non-negative.");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var dragType = UsesBStar ? $"BSTAR={BStar:E4}" : $"BTerm={BTerm:E4}";
        var ddotType = UsesMeanMotionDDot ? $"n''={MeanMotionDDot:E4}" : $"AGOM={AGom:E4}";
        var norad = NoradCatalogId.HasValue ? $", NORAD={NoradCatalogId}" : "";
        return $"TleParameters[{dragType}, n'={MeanMotionDot:E4}, {ddotType}{norad}]";
    }
}
