// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;

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
    /// <exception cref="ArgumentOutOfRangeException">BSTAR, the first or the second derivative of the mean motion is NaN or
    /// ±Infinity, which the OMM reader refuses (#708, #717), or the element set or revolution number is out of its range, the
    /// exception naming <paramref name="elementSetNumber"/> or <paramref name="revolutionNumberAtEpoch"/> (#734).</exception>
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

        // Real numbers, as the reader reads them (#717)
        BStar = CcsdsValues.RequireFinite(bstar, nameof(bstar));
        BTerm = null;
        MeanMotionDot = CcsdsValues.RequireFinite(meanMotionDot, nameof(meanMotionDot));
        MeanMotionDDot = CcsdsValues.RequireFinite(meanMotionDDot, nameof(meanMotionDDot));
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
        // Real numbers, as the reader reads them (#717: NaN and ±Infinity built, Validate() passed them, an infinite BSTAR
        // with an information message only, and the writer wrote NaN, Infinity or -Infinity, which the reader refuses)
        BStar = CcsdsValues.RequireNullOrFinite(bstar, nameof(bstar));
        BTerm = CcsdsValues.RequireNullOrFinite(bterm, nameof(bterm));
        MeanMotionDot = CcsdsValues.RequireFinite(meanMotionDot, nameof(meanMotionDot));
        MeanMotionDDot = CcsdsValues.RequireNullOrFinite(meanMotionDDot, nameof(meanMotionDDot));
        AGom = CcsdsValues.RequireNullOrFinite(agom, nameof(agom));
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
    /// <exception cref="ArgumentOutOfRangeException">A real parameter is NaN or ±Infinity (#717), or the element set or
    /// revolution number is out of its range, the exception naming <c>elementSetNumber</c> or
    /// <c>revolutionNumberAtEpoch</c> (#734).</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">A real parameter is NaN or ±Infinity (#717), or the element set or
    /// revolution number is out of its range, the exception naming <c>elementSetNumber</c> or
    /// <c>revolutionNumberAtEpoch</c> (#734).</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">A real parameter is NaN or ±Infinity (#717), or the element set or
    /// revolution number is out of its range, the exception naming <c>elementSetNumber</c> or
    /// <c>revolutionNumberAtEpoch</c> (#734).</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">A real parameter is NaN or ±Infinity (#717), or the element set or
    /// revolution number is out of its range, the exception naming <c>elementSetNumber</c> or
    /// <c>revolutionNumberAtEpoch</c> (#734).</exception>
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

    // The exception names the parameter of every public entry, revolutionNumberAtEpoch (#734: it named revNumber, this
    // method's own parameter, which a caller reading ParamName could not find)
    private static void ValidateRevolutionNumber(int? revolutionNumberAtEpoch)
    {
        if (revolutionNumberAtEpoch.HasValue && revolutionNumberAtEpoch.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(revolutionNumberAtEpoch), "Revolution number must be non-negative.");
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
