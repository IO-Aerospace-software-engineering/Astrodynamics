// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
/// <remarks>
/// <para>
/// The SGP4/SDP4 model is CSPICE's <c>evsgp4_c</c>, Vallado's 2006 code (Vallado, Crawford, Hujsak and Kelso,
/// AIAA 2006-6753), in the AFSPC mode, with the WGS-72 "old" constants (Vallado's <c>wgs72old</c>): KE = 7.43669161e-2
/// exactly (not derived from GM and the radius), R = 6378.135 km, J2 = 1.082616e-3, J3 = −2.53881e-6,
/// J4 = −1.65597e-6, and the density parameters q0 = 120 km and s0 = 78 km. No other mode or constants are offered.
/// The states are in <see cref="Frame.TEME"/>.
/// </para>
/// <para>
/// The time argument of SGP4, tsince, counts the TT seconds from the TLE epoch (UTC) to the requested instant: away
/// from a leap second, the UTC Julian-date difference of Vallado's driver (#599). A failed SGP4 (a decayed or suborbital
/// orbit, an eccentricity out of range) throws <see cref="InvalidOperationException"/> (#454).
/// </para>
/// </remarks>
public class TLE : OrbitalParameters, IEquatable<TLE>, IParsable<TLE>
{
    private const double MAX_ECCENTRICITY = 0.9999999;
    private const int MAX_ELEMENT_SET_NUMBER = 9999;

    /// <summary>
    /// The element set number written when none is given, by <see cref="Create"/>, <see cref="Configuration"/> (so
    /// <see cref="OrbitalParameters.ToTLE"/>) and <c>Omm.ToTle</c> for an OMM without <c>ELEMENT_SET_NO</c>: 999, the
    /// value CelesTrak and Space-Track publish (#606, decided by S. Guillet, 2026-11-17; before 11.0.0,
    /// <see cref="Create"/> and <see cref="Configuration"/> wrote 9999). The field holds 0 to 9999.
    /// </summary>
    public const int DefaultElementSetNumber = 999;
    private const int MAX_REVOLUTIONS_AT_EPOCH = 99999;
    // The centre of every TLE: SGP4 propagates Earth orbits only (#607)
    internal const int EarthNaifId = 399;
    private KeplerianElements _meanKeplerianElements;

    // The TLE epoch as CSPICE's getelm_c reads it (ET, TDB seconds from J2000), read once from the native layer: SGP4
    // runs at this epoch plus the TT seconds from Epoch, so that tsince is the TT seconds from the TLE epoch (#599)
    private readonly Lazy<double> _sgp4Epoch;

    /// <summary>
    /// Gets the first line of the TLE.
    /// </summary>
    public string Line1 { get; }

    /// <summary>
    /// Gets the second line of the TLE.
    /// </summary>
    public string Line2 { get; }

    /// <summary>
    /// Gets the name of the TLE.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the NORAD catalog number decoded from line 1.
    /// </summary>
    /// <remarks>
    /// Alpha-5 identifiers (above 99999) are decoded to their numeric value, so <c>T5544</c> yields 275544.
    /// See <see cref="NoradCatalogNumber"/>.
    /// </remarks>
    public int NoradCatalogId { get; }

    /// <summary>
    /// Gets the ballistic coefficient (drag term) in units of 1/earth radii.
    /// This value is used to model atmospheric drag effects on the satellite's orbit.
    /// It is typically a small value, indicating the amount of drag experienced by the satellite.
    /// For example, a value of 0.0001 means that the satellite experiences a drag force equivalent to 0.0001 times the gravitational force at the Earth's surface.
    /// </summary>
    public double BallisticCoefficient { get; }

    /// <summary>
    /// Gets the value of the first-derivative field of line 1 (columns 34-43), ṅ/2: half the first time derivative of
    /// the mean motion, in revolutions per day squared, as the TLE format and OMM's <c>MEAN_MOTION_DOT</c> hold it.
    /// </summary>
    /// <remarks>
    /// SGP4 does not read it; it is kept for the text of the TLE and its OMM. Before 11.0.0 this documentation called it
    /// ṅ (#605).
    /// </remarks>
    public double FirstDerivationMeanMotion { get; }

    /// <summary>
    /// Gets the value of the second-derivative field of line 1 (columns 45-52), n̈/6: a sixth of the second time
    /// derivative of the mean motion, in revolutions per day cubed, as the TLE format and OMM's <c>MEAN_MOTION_DDOT</c>
    /// hold it.
    /// </summary>
    /// <remarks>
    /// SGP4 does not read it; it is kept for the text of the TLE and its OMM. Before 11.0.0 this documentation called it
    /// n̈ (#605).
    /// </remarks>
    public double SecondDerivativeMeanMotion { get; }

    /// <summary>
    /// Gets the mean semi-major axis of the orbit in meters: a = ∛(GM/n²), n the <see cref="MeanMeanMotion"/> and GM the
    /// kernel GM of the Earth (<c>BODY399_GM</c>).
    /// </summary>
    /// <remarks>
    /// Read at the first use, from the data provider set at that time, and kept once read: a TLE built before the kernels
    /// are loaded throws <see cref="InvalidOperationException"/> naming <c>BODY399_GM</c> here, keeps nothing, and gives the
    /// value once they are loaded (R21 of #668: before 11.0.0 the constructor read the Earth and kept a = ∛(0) without
    /// kernels).
    /// </remarks>
    /// <exception cref="InvalidOperationException">The data provider gives no GM of the Earth.</exception>
    public double MeanSemiMajorAxis => _meanSemiMajorAxis.Value;

    // PublicationOnly: a failed read (no kernel loaded yet) is not cached, as the epoch of SGP4
    private readonly Lazy<double> _meanSemiMajorAxis;

    // The mean motion field of line 2 (columns 53-63), in revolutions per day, as written
    private readonly double _meanMotionRevolutionsPerDay;

    /// <summary>
    /// Gets the mean motion of the TLE in radians per second: the field of line 2 (columns 53-63, revolutions per day) times
    /// 2π / 86400, read without kernels; mean, like <see cref="MeanInclination"/>.
    /// </summary>
    /// <remarks>
    /// Decision 111 of #668, decided by S. Guillet, 2026-11-26: it replaces the method <c>MeanMotion()</c> of 10.x,
    /// √(GM/a³) with a = ∛(GM/n²), which needed the kernels and was 1 ulp off the field on about half the sets. The
    /// inherited <see cref="OrbitalParameters.MeanMotion(double)"/> is the mean motion of the osculating SGP4 set at the
    /// epoch, 0.14 % off this one for the ISS.
    /// </remarks>
    public double MeanMeanMotion { get; }

    /// <summary>
    /// Gets the mean eccentricity of the orbit.
    /// This value represents the shape of the orbit, where 0 is a circular orbit and
    /// values close to 1 indicate highly elliptical orbits.
    /// It is a dimensionless quantity that describes how much the orbit deviates from being circular.
    /// For example, a value of 0.1 means that the orbit is slightly elliptical,
    /// while a value of 0.9 indicates a highly elliptical orbit.
    /// This parameter is crucial for understanding the orbital dynamics and behavior of the satellite,
    /// including its speed at different points in the orbit and the variation in altitude.
    /// </summary>
    public double MeanEccentricity { get; }

    /// <summary>
    /// Gets the mean inclination of the orbit in radians.
    /// </summary>
    public double MeanInclination { get; }

    /// <summary>
    /// Gets the mean right ascension of the ascending node in radians.
    /// This value represents the angle from the vernal equinox to the ascending node of the orbit,
    /// which is the point where the satellite crosses the equatorial plane from south to north.
    /// It is a crucial parameter for understanding the orientation of the orbit in space.
    /// For example, a value of 0 radians means that the ascending node is aligned with the vernal equinox,
    /// while a value of π/2 radians means that the ascending node is 90 degrees away from the vernal equinox.
    /// This parameter is essential for predicting the satellite's position and velocity at any point in its orbit,
    /// as well as for understanding the orbital dynamics and behavior of the satellite.
    /// </summary>
    public double MeanAscendingNode { get; }

    /// <summary>
    /// Gets the mean argument of periapsis in radians.
    /// This value represents the angle from the ascending node to the periapsis of the orbit,
    /// which is the point in the orbit closest to the Earth.
    /// It is a crucial parameter for understanding the orientation of the orbit in space.
    /// For example, a value of 0 radians means that the periapsis is aligned with the ascending node,
    /// while a value of π/2 radians means that the periapsis is 90 degrees away from the ascending node.
    /// This parameter is essential for predicting the satellite's position and velocity at any point in its orbit,
    /// as well as for understanding the orbital dynamics and behavior of the satellite.
    /// </summary>
    public double MeanArgumentOfPeriapsis { get; }

    /// <summary>
    /// Gets the mean mean anomaly in radians.
    /// This value represents the angle from the periapsis to the current position of the satellite
    /// in its orbit, measured in the direction of the satellite's motion.
    /// It is a crucial parameter for understanding the satellite's position in its orbit.
    /// For example, a value of 0 radians means that the satellite is at the periapsis,
    /// while a value of π radians means that the satellite is at the apoapsis (the point in the orbit farthest from the Earth).
    /// This parameter is essential for predicting the satellite's position and velocity at any point in its orbit,
    /// as well as for understanding the orbital dynamics and behavior of the satellite.
    /// </summary>
    /// <remarks>
    /// The mean anomaly is a key parameter in orbital mechanics, as it allows for the calculation
    /// of the satellite's position at any given time. It is used in conjunction with other
    /// orbital elements to determine the satellite's trajectory and predict its future positions.
    /// The mean anomaly is particularly useful for calculating the satellite's position in elliptical orbits,
    /// where the position varies significantly over time.
    /// </remarks>
    /// <seealso cref="MeanMeanAnomaly"/>   
    public double MeanMeanAnomaly { get; }

    /// <summary>
    /// Initializes a new instance of the TLE class.
    /// </summary>
    /// <param name="name">The name of the object (the title line of a three-line set); null or blank for the catalog field
    /// of line 1 (columns 3-7, as written: <c>25544</c>, <c>T5544</c>).</param>
    /// <param name="line1">the first line</param>
    /// <param name="line2">the second line</param>
    /// <exception cref="ArgumentException">Thrown when a line is null or empty, or does not follow the format, naming
    /// <c>line1</c> or <c>line2</c> with a <see cref="TleFormatException"/> (its line and column) as inner exception: the
    /// length (69 characters), the line number, the catalog number (both lines one object), the checksum, and every field
    /// (R21 of #668: the classification U, C or S, the international designator blank or YYNNNPPP, the epoch YYDDD.DDDDDDDD
    /// with a day of its year, the derivatives and B*, the ephemeris type, the element set and revolution numbers, the
    /// angles, the eccentricity and the mean motion; before 11.0.0 a wrong checksum or two catalog numbers threw
    /// <see cref="InvalidOperationException"/>, a malformed field a raw <see cref="FormatException"/>, and a day of year
    /// out of range rolled into the next year silently). No physical range is checked: SGP4 reports a set it cannot
    /// evaluate (#454).</exception>
    /// <remarks>
    /// The constructor reads no kernel (R21 of #668): the text is checked first (#675), and the Earth of the TLE reads its
    /// data from the data provider at the first member that needs them (<see cref="MeanSemiMajorAxis"/>, SGP4), so that a
    /// valid TLE gives its fields, <see cref="MeanMeanMotion"/> and <see cref="ToOmm"/> without kernels. Before 11.0.0 it
    /// built the Earth from the kernels, and a TLE built before them kept a GM of 0.
    /// </remarks>
    public TLE(string name, string line1, string line2) : base(EarthAt(ValidatedEpoch(line1, line2)), ExtractEpochFromTLE(line1),
        Frame.TEME, OrbitalElementsType.Mean)
    {
        Line1 = line1;
        Line2 = line2;
        // Without a name, the catalog field as written (R21 of #668)
        Name = string.IsNullOrWhiteSpace(name) ? line1.Substring(2, NoradCatalogNumber.FieldLength).Trim() : name;
        NoradCatalogId = NoradCatalogNumber.Parse(line1.Substring(2, NoradCatalogNumber.FieldLength));
        Classification = (Classification)line1[7];
        var designator = line1.Substring(9, 8).Trim();
        CosparId = designator.Length == 0 ? null : designator;
        ElementSetNumber = ParseCount(line1.Substring(64, 4));
        RevolutionNumberAtEpoch = ParseCount(line2.Substring(63, 5));

        // Parse line 2 for orbital elements - FIXED: Use InvariantCulture
        _meanMotionRevolutionsPerDay = double.Parse(line2.Substring(52, 11), CultureInfo.InvariantCulture);
        double n = _meanMotionRevolutionsPerDay * Constants.TwoPi / 86400.0; // Convert rev/day to rad/s
        MeanMeanMotion = n;
        // The GM of the Earth of the TLE, read at the first use (R21 of #668)
        _meanSemiMajorAxis = new Lazy<double>(() => System.Math.Cbrt(((CelestialBody)Observer).GM / (n * n)), LazyThreadSafetyMode.PublicationOnly);

        // Cache mean motion to preserve precision for TLE recreation
        MeanEccentricity = double.Parse("0." + line2.Substring(26, 7), CultureInfo.InvariantCulture);
        MeanInclination = double.Parse(line2.Substring(8, 8), CultureInfo.InvariantCulture) * Constants.Deg2Rad;
        MeanAscendingNode = double.Parse(line2.Substring(17, 8), CultureInfo.InvariantCulture) * Constants.Deg2Rad;
        MeanArgumentOfPeriapsis = double.Parse(line2.Substring(34, 8), CultureInfo.InvariantCulture) * Constants.Deg2Rad;
        MeanMeanAnomaly = double.Parse(line2.Substring(43, 8), CultureInfo.InvariantCulture) * Constants.Deg2Rad;

        // Parse line 1 for additional parameters
        FirstDerivationMeanMotion = ParseTleDouble(line1.Substring(33, 10));
        SecondDerivativeMeanMotion = ParseTleExponent(line1.Substring(44, 8));
        BallisticCoefficient = ParseTleExponent(line1.Substring(53, 8));

        // PublicationOnly: a failed read (no kernel loaded yet) is not cached, so that the TLE reads its epoch once the kernels
        // are loaded (#711: before, the epoch of a failed read was kept for the life of the TLE). The read takes the SpiceAPI
        // lock, so a read repeated by a race gives the same value
        _sgp4Epoch = new Lazy<double>(() => SpiceAPI.Instance.ReadTleEpoch(Name, Line1, Line2), LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>
    /// Gets the security classification of line 1 (column 8).
    /// </summary>
    public Classification Classification { get; }

    /// <summary>
    /// Gets the international designator of line 1 (columns 10-17) as written, trimmed (<c>98067A</c>), or null when the
    /// columns are blank; the argument <c>cosparId</c> of <see cref="Create"/>.
    /// </summary>
    public string CosparId { get; }

    /// <summary>
    /// Gets the element set number of line 1 (columns 65-68), 0 when the columns are blank.
    /// </summary>
    public int ElementSetNumber { get; }

    /// <summary>
    /// Gets the revolution number at epoch of line 2 (columns 64-68), 0 when the columns are blank.
    /// </summary>
    public int RevolutionNumberAtEpoch { get; }

    // A count field of digits right-aligned in its columns, 0 when blank (checked by ValidateText)
    private static int ParseCount(string field)
    {
        var digits = field.Trim();
        return digits.Length == 0 ? 0 : int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The epoch of line 1, read once the text has passed every check of <see cref="ValidateText"/>, a failure thrown as
    /// <see cref="ArgumentException"/> naming its line with the <see cref="TleFormatException"/> as inner exception.
    /// Evaluated as the first argument of the base constructor, so that no check waits for a kernel read (#675).
    /// </summary>
    private static Time ValidatedEpoch(string line1, string line2)
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));

        try
        {
            ValidateText(line1, line2);
        }
        catch (TleFormatException e)
        {
            throw new ArgumentException(e.Message, e.Line == 1 ? nameof(line1) : nameof(line2), e);
        }

        return ExtractEpochFromTLE(line1);
    }

    /// <summary>
    /// The Earth of a TLE, in an inertial frame: nothing on the TLE path needs its orientation, which ITRF93 would read
    /// from the Earth's binary PCK at the epoch (#597). Its data (GM, radii, frame, orbit) are read from the data provider
    /// at the first member that needs them, retried on failure (R21 of #668: before 11.0.0 the constructor read them).
    /// </summary>
    private static CelestialBody EarthAt(Time epoch) => CelestialBody.CreateDeferred(PlanetsAndMoons.EARTH, Frame.ECLIPTIC_J2000, epoch);

    /// <summary>
    /// Extracts the epoch time from TLE line 1.
    /// </summary>
    /// <param name="line1">The first line of the TLE.</param>
    /// <returns>The epoch time, a UTC <see cref="Time"/>.</returns>
    /// <remarks>
    /// The day and its fraction are read in decimal and the fraction rounded to the 100 ns tick, so that the 8 decimals
    /// of a day, whole multiples of 864 µs, give the exact instant. Before 11.0.0 the field went through a
    /// <see cref="double"/> and was truncated to the tick, 100 ns early on 43 % of the epochs (#603).
    /// </remarks>
    internal static Time ExtractEpochFromTLE(string line1)
    {
        if (string.IsNullOrEmpty(line1) || line1.Length < 32)
            throw new ArgumentException("Invalid TLE line 1 format.", nameof(line1));

        // Extract epoch from positions 18-32 (0-based indexing): YYDDD.DDDDDDDD
        string epochStr = line1.Substring(18, 14);

        // Parse year (YY format) - FIXED: Use InvariantCulture
        int year = int.Parse(epochStr.Substring(0, 2), CultureInfo.InvariantCulture);
        // Convert 2-digit year to 4-digit (pivot at 57: 57-99 = 1957-1999, 00-56 = 2000-2056)
        if (year < 57)
            year += 2000;
        else
            year += 1900;

        // Parse day of year (DDD.DDDDDDDD) in decimal: the day, then its fraction rounded to the tick (#603)
        decimal dayOfYear = decimal.Parse(epochStr.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture);
        decimal day = decimal.Floor(dayOfYear);
        long fractionTicks = (long)decimal.Round((dayOfYear - day) * TimeSpan.TicksPerDay, MidpointRounding.AwayFromZero);

        // Create DateTime from year and day of year
        var yearStart = new DateTime(year, 1, 1);
        var epochDateTime = yearStart.AddDays((double)(day - 1m)).AddTicks(fractionTicks);

        return new Time(epochDateTime, TimeScale.UTC);
    }

    /// <summary>
    /// Parses a TLE double value (handles spaces and formatting).
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>The parsed double value.</returns>
    private static double ParseTleDouble(string value)
    {
        value = value.Trim();
        if (string.IsNullOrEmpty(value) || value == "0" || value.All(c => c == '0' || c == ' '))
            return 0.0;

        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a TLE scientific notation value (e.g., " 34123-4" = 0.34123e-4).
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>The parsed double value.</returns>
    private static double ParseTleExponent(string value)
    {
        if (string.IsNullOrEmpty(value) || value.All(c => c == '0' || c == ' ' || c == '-' || c == '+'))
            return 0.0;

        // TLE format: [space/sign][5 digits][sign][1 digit]
        // Example: " 34123-4" means 0.34123e-4
        char sign = value[0] == '-' ? '-' : '+';
        string mantissaStr = value.Substring(1, 5);
        string exponentStr = value.Substring(6, 2);

        double mantissa = double.Parse("0." + mantissaStr, CultureInfo.InvariantCulture);
        int exponent = int.Parse(exponentStr, CultureInfo.InvariantCulture);

        double result = mantissa * System.Math.Pow(10, exponent);
        return sign == '-' ? -result : result;
    }

    /// <summary>
    /// Creates a new TLE from orbital parameters and metadata.
    /// </summary>
    /// <param name="meanElements">The orbital parameters to convert to TLE format.</param>
    /// <param name="name">The name/title of the satellite or object.</param>
    /// <param name="noradId">
    /// The NORAD catalog number, between 0 and <see cref="NoradCatalogNumber.MaxValue"/> (339999).
    /// Values above 99999 are encoded with the Alpha-5 convention.
    /// </param>
    /// <param name="cosparId">The COSPAR international designator (6-8 characters), or null or blank for none: columns 10-17
    /// of line 1 are then blank, as a TLE without a designator holds them (#604; before 11.0.0 it threw).</param>
    /// <param name="revolutionsAtEpoch">The revolution number at epoch (0 to 99999).</param>
    /// <param name="classification">The security classification of the object (default: Unclassified).</param>
    /// <param name="bstar">The ballistic coefficient (drag term) in units of 1/earth radii (default: 0.0001).</param>
    /// <param name="nDot">The value of the first-derivative field, ṅ/2 (half the first derivative of the mean motion), in
    /// revolutions/day² (default: 0.0), as OMM's <c>MEAN_MOTION_DOT</c> (#605).</param>
    /// <param name="nDDot">The value of the second-derivative field, n̈/6 (a sixth of the second derivative of the mean
    /// motion), in revolutions/day³ (default: 0.0), as OMM's <c>MEAN_MOTION_DDOT</c> (#605).</param>
    /// <param name="elementSetNumber">The element set number, 0 to 9999 (default: <see cref="DefaultElementSetNumber"/>, 999,
    /// #606).</param>
    /// <returns>A new TLE instance constructed from the provided parameters.</returns>
    /// <remarks>
    /// Each field is the nearest value its columns represent: the epoch the UTC of the elements' epoch, whatever its
    /// time scale (#479), to the nearest 1e-8 day with the carry into the next day and year (#600); e in [0, 0.9999999]
    /// (#602); B* and n̈/6 as a five-digit mantissa and a one-digit exponent, zero below 5e-11 (#601); a value that rounds
    /// to zero, −0.0 included, without a sign (#480).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when meanElements or name is null.</exception>
    /// <exception cref="ArgumentException">Thrown when meanElements are not mean elements, are not in <see cref="Frame.TEME"/>
    /// or are not about the Earth (NAIF id 399): a TLE holds SGP4 mean elements of an Earth orbit in TEME (#607; before
    /// 11.0.0, elements in another frame or about another centre were written as if they were, their angles taken as
    /// TEME's and their mean motion from their centre's GM); or when cosparId is neither null nor blank and not between 6-8
    /// characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when noradId, revolutionsAtEpoch or elementSetNumber falls outside the range its TLE field can represent.
    /// </exception>
    public static TLE Create(OrbitalParameters meanElements, string name, int noradId, string cosparId, int revolutionsAtEpoch,
        Classification classification = Classification.Unclassified, double bstar = 0.0001, double nDot = 0.0, double nDDot = 0.0, int elementSetNumber = DefaultElementSetNumber)
    {
        if (meanElements == null) throw new ArgumentNullException(nameof(meanElements));
        if (meanElements.ElementsType != OrbitalElementsType.Mean)
            throw new ArgumentException("Orbital parameters must be mean elements. Use KeplerianElements.FromOMM() or ensure ElementsType is Mean.", nameof(meanElements));

        // A TLE holds SGP4 mean elements of an Earth orbit in TEME (#607)
        if (meanElements.Frame != Frame.TEME)
            throw new ArgumentException(
                $"The mean elements are in {meanElements.Frame.Name}: a TLE holds SGP4 mean elements of an Earth orbit in TEME.",
                nameof(meanElements));
        if (meanElements.Observer.NaifId != EarthNaifId)
            throw new ArgumentException(
                $"The mean elements are about {meanElements.Observer.Name} ({meanElements.Observer.NaifId}): a TLE holds SGP4 mean " +
                "elements of an Earth orbit in TEME.", nameof(meanElements));

        return CreateInternal(meanElements, name, noradId, cosparId, revolutionsAtEpoch, classification, bstar, nDot, nDDot, elementSetNumber);
    }

    /// <summary>
    /// Internal method for creating TLE from orbital parameters during the fitting process.
    /// This method does not validate ElementsType, allowing iterative convergence from osculating to mean elements.
    /// </summary>
    internal static TLE CreateInternal(OrbitalParameters orbitalParams, string name, int noradId, string cosparId, int revolutionsAtEpoch,
        Classification classification = Classification.Unclassified, double bstar = 0.0001, double nDot = 0.0, double nDDot = 0.0, int elementSetNumber = DefaultElementSetNumber)
    {
        if (orbitalParams == null) throw new ArgumentNullException(nameof(orbitalParams));
        if (name == null) throw new ArgumentNullException(nameof(name));
        // A null or blank designator is written as blank columns 10-17, which a TLE may hold (#604)
        if (string.IsNullOrWhiteSpace(cosparId))
        {
            cosparId = string.Empty;
        }
        else if (cosparId.Length is < 6 or > 8)
        {
            throw new ArgumentException("COSPAR Identifier must be between 6 and 8 characters long, or null or blank for no designator.",
                nameof(cosparId));
        }

        if (!NoradCatalogNumber.IsValid(noradId))
        {
            throw new ArgumentOutOfRangeException(nameof(noradId),
                $"NORAD catalog number must be between 0 and {NoradCatalogNumber.MaxValue} " +
                $"(values above {NoradCatalogNumber.MaxNumericValue} use the Alpha-5 convention).");
        }

        if (revolutionsAtEpoch is < 0 or > MAX_REVOLUTIONS_AT_EPOCH)
        {
            throw new ArgumentOutOfRangeException(nameof(revolutionsAtEpoch),
                $"Revolution number at epoch must be between 0 and {MAX_REVOLUTIONS_AT_EPOCH} to fit the 5-character TLE field.");
        }

        if (elementSetNumber is < 0 or > MAX_ELEMENT_SET_NUMBER)
        {
            throw new ArgumentOutOfRangeException(nameof(elementSetNumber), $"Element set number must be between 0 and {MAX_ELEMENT_SET_NUMBER}.");
        }

        // Convert elements to TLE units
        var kep = orbitalParams is TLE tle ? tle.ToMeanKeplerianElements() : orbitalParams.ToKeplerianElements();

        // Convert and normalize angles - single pass, no redundant calculations
        double iDeg = System.Math.Clamp(kep.I * Constants.Rad2Deg, 0.0, 180.0);
        if (double.IsNaN(iDeg)) iDeg = 0.0;

        double raanDeg = NormalizeAngle(kep.RAAN * Constants.Rad2Deg);
        double argpDeg = NormalizeAngle(kep.AOP * Constants.Rad2Deg);
        double mDeg = NormalizeAngle(kep.M * Constants.Rad2Deg);

        // For mean elements, use cached mean motion to preserve precision from OMM
        // MeanMotion() returns cached value (rad/s) if available, otherwise computes from semi-major axis
        double meanMotion = kep.MeanMotion() * 86400.0 / Constants.TwoPi;

        // Format epoch: YYDDD.DDDDDDDD, in UTC whatever the time scale of the elements' epoch (#479)
        string epochStr = FormatTleEpoch(kep.Epoch.ToUTC().DateTime);

        // Format eccentricity: 7 digits, no decimal point; e = 0 is written 0000000 (#602)
        double clampedE = System.Math.Clamp(kep.E, 0.0, MAX_ECCENTRICITY);
        string eStr = clampedE.ToString("0.0000000", CultureInfo.InvariantCulture).Substring(2, 7);

        // Format derivatives and drag term. A value that rounds to zero is written without a sign (#480)
        string nDotStr = nDot.ToString(" .00000000;-.00000000; .00000000", CultureInfo.InvariantCulture).PadLeft(10);
        string nDDotStr = FormatTleExponent(nDDot, 5);
        string bstarStr = FormatTleExponent(bstar, 5);

        // Build line 1 - use single ToString call
        var line1Builder = new StringBuilder(69);
        line1Builder.Append("1 ")
            .Append(NoradCatalogNumber.Format(noradId))
            .Append((char)classification)
            .Append(' ')
            .Append(cosparId.PadRight(8))
            .Append(' ')
            .Append(epochStr)
            .Append(' ')
            .Append(nDotStr)
            .Append(' ')
            .Append(nDDotStr)
            .Append(' ')
            .Append(bstarStr)
            .Append(" 0 ")
            .Append(elementSetNumber.ToString().PadLeft(4));

        // Compute checksum on the builder's content without creating intermediate string
        int checksum1 = ComputeTleChecksumFromBuilder(line1Builder);
        line1Builder.Append(checksum1);

        // Build line 2
        var line2Builder = new StringBuilder(69);
        line2Builder.Append("2 ")
            .Append(NoradCatalogNumber.Format(noradId))
            .Append(' ')
            .Append(iDeg.ToString(AngleFormat, CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(raanDeg.ToString(AngleFormat, CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(eStr)
            .Append(' ')
            .Append(argpDeg.ToString(AngleFormat, CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(mDeg.ToString(AngleFormat, CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(meanMotion.ToString("0.00000000", CultureInfo.InvariantCulture).PadLeft(11))
            .Append(revolutionsAtEpoch.ToString().PadLeft(5));

        int checksum2 = ComputeTleChecksumFromBuilder(line2Builder);
        line2Builder.Append(checksum2);

        return new TLE(name, line1Builder.ToString(), line2Builder.ToString());
    }

    // The four angles of line 2: a value that rounds to zero, −0.0 included, is written 0.0000 without a sign (#480)
    private const string AngleFormat = "0.0000;-0.0000;0.0000";

    // One unit of the 8th decimal of a day: 864 µs, 8640 ticks
    private const long EpochLatticeTicks = TimeSpan.TicksPerDay / 100_000_000L;

    /// <summary>
    /// The epoch field of line 1, YYDDD.DDDDDDDD, of a UTC calendar instant: the instant rounded to the nearest 1e-8 day
    /// (a tie up), then split into the day of the year and its fraction, a fraction that rounds to one day carried into
    /// the next day and year (#600).
    /// </summary>
    internal static string FormatTleEpoch(DateTime utc)
    {
        long units = (utc.TimeOfDay.Ticks + EpochLatticeTicks / 2) / EpochLatticeTicks;
        var date = utc.Date;
        if (units == 100_000_000L)
        {
            date = date.AddDays(1.0);
            units = 0;
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"{date.Year % 100:00}{date.DayOfYear:000}.{units:00000000}");
    }

    /// <summary>
    /// Normalizes an angle to [0, 360) range with single-pass validation
    /// </summary>
    private static double NormalizeAngle(double angleDeg)
    {
        if (double.IsNaN(angleDeg) || double.IsInfinity(angleDeg))
            return 0.0;

        // Normalize to [0, 360)
        angleDeg = angleDeg % 360.0;
        if (angleDeg < 0.0)
            angleDeg += 360.0;

        // Edge case: very close to 360
        if (angleDeg >= 360.0 - 1e-10)
            return 0.0;

        // Check if formatting to 4 decimals would produce 360.0
        // More efficient than Math.Round: direct comparison
        if (angleDeg >= 359.99995)
            return 0.0;

        return angleDeg;
    }

    /// <summary>
    /// Computes TLE checksum directly from StringBuilder without intermediate string allocation
    /// </summary>
    private static int ComputeTleChecksumFromBuilder(StringBuilder builder)
    {
        int sum = 0;
        int length = builder.Length;

        for (int i = 0; i < length; i++)
        {
            char c = builder[i];
            if (char.IsDigit(c))
                sum += c - '0';
            else if (c == '-')
                sum += 1;
        }

        return sum % 10;
    }

    // Helper for TLE scientific notation (e.g.  34123-4)
    // TLE format: [sign][5-digit mantissa][signed exponent]
    // The mantissa represents 0.XXXXX (value between 0.1 and 1)
    // Example: " 10270-3" means +0.10270 × 10^-3 = 0.00010270
    // The value is written as the nearest representable one, the exponent one digit from -9 to +9 (#601): below
    // 5e-11 in magnitude it is zero, " 00000-0", and from 5e-11 to 1e-10 it is 0.10000e-9; from 0.999995e9 the field
    // cannot hold it and the call throws.
    static string FormatTleExponent(double value, int width)
    {
        const double smallestHalf = 5e-11;
        var absValue = System.Math.Abs(value);
        if (!(absValue >= smallestHalf))
        {
            if (double.IsNaN(value))
                throw new ArgumentOutOfRangeException(nameof(value), "A TLE exponent field cannot hold NaN.");
            return ' ' + new string('0', width) + "-0";
        }

        var sign = value < 0 ? '-' : ' ';

        var scientificNotation = absValue.ToString("0.0000E+0", CultureInfo.InvariantCulture);
        var parts = scientificNotation.Split('E');
        var mantissa = parts[0].Replace(".", "").PadLeft(width, '0');

        // TLE format uses 0.XXXXX mantissa (0.1 to 1), C# uses X.XXXX (1 to 10)
        // So we must increment the exponent by 1 to compensate
        int exponentValue = int.Parse(parts[1], CultureInfo.InvariantCulture) + 1;
        if (exponentValue < -9)
        {
            // [5e-11, 0.999995e-10): the nearest representable value is 0.10000e-9
            mantissa = "1" + new string('0', width - 1);
            exponentValue = -9;
        }
        else if (exponentValue > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "A TLE exponent field holds at most 0.99999e9 in magnitude (a one-digit exponent).");
        }

        string exponent = exponentValue >= 0 ? $"+{exponentValue}" : exponentValue.ToString(CultureInfo.InvariantCulture);

        return $"{sign}{mantissa}{exponent}";
    }

    /// <summary>
    /// Checks the text of the two lines of a set against the format, in this order: the lengths and line numbers, the
    /// catalog numbers and their agreement, the checksums, then every field of line 1 and of line 2 (R21 of #668).
    /// </summary>
    /// <exception cref="TleFormatException">The line (1 or 2) and the first column of the field at fault.</exception>
    internal static void ValidateText(string line1, string line2)
    {
        CheckLength(line1, 1);
        CheckLength(line2, 2);
        if (line1[0] != '1')
            throw new TleFormatException($"TLE line 1 must start with '1', got '{line1[0]}': {line1}", 1, 1);
        if (line2[0] != '2')
            throw new TleFormatException($"TLE line 2 must start with '2', got '{line2[0]}': {line2}", 2, 1);

        // The NORAD catalog number field (columns 3-7), which both lines must carry identically
        var noradField1 = line1.Substring(2, NoradCatalogNumber.FieldLength);
        var noradField2 = line2.Substring(2, NoradCatalogNumber.FieldLength);
        if (!NoradCatalogNumber.TryParse(noradField1, out int noradId1))
            throw new TleFormatException($"Invalid NORAD catalog number '{noradField1}' in TLE line 1: {line1}", 1, 3);
        if (!NoradCatalogNumber.TryParse(noradField2, out int noradId2))
            throw new TleFormatException($"Invalid NORAD catalog number '{noradField2}' in TLE line 2: {line2}", 2, 3);
        if (noradId1 != noradId2)
        {
            throw new TleFormatException(
                $"NORAD catalog numbers differ between TLE lines: {noradId1} in line 1, {noradId2} in line 2. " +
                "The two lines do not belong to the same object.", 2, 3);
        }

        CheckChecksum(line1, 1);
        CheckChecksum(line2, 2);

        // Line 1
        CheckField(line1, 1, 8, 1, ClassificationField, "classification (U, C or S)");
        CheckField(line1, 1, 10, 8, DesignatorField, "international designator (YYNNNPPP or blank)");
        CheckField(line1, 1, 19, 14, EpochField, "epoch (YYDDD.DDDDDDDD)");
        int year = int.Parse(line1.AsSpan(18, 2), NumberStyles.None, CultureInfo.InvariantCulture);
        year += year < 57 ? 2000 : 1900;
        int day = int.Parse(line1.AsSpan(20, 3), NumberStyles.None, CultureInfo.InvariantCulture);
        if (day < 1 || day > (DateTime.IsLeapYear(year) ? 366 : 365))
            throw new TleFormatException($"Invalid day of year {day} of {year} in the epoch of TLE line 1, columns 19-32: {line1}", 1, 19);
        CheckField(line1, 1, 34, 10, DecimalFieldOrBlank, "first derivative of the mean motion");
        CheckField(line1, 1, 45, 8, ExponentField, "second derivative of the mean motion");
        CheckField(line1, 1, 54, 8, ExponentField, "B*");
        CheckField(line1, 1, 63, 1, DigitOrBlank, "ephemeris type");
        CheckField(line1, 1, 65, 4, CountField, "element set number");

        // Line 2
        CheckField(line2, 2, 9, 8, DecimalField, "inclination");
        CheckField(line2, 2, 18, 8, DecimalField, "right ascension of the ascending node");
        CheckField(line2, 2, 27, 7, EccentricityField, "eccentricity (seven digits, no decimal point)");
        CheckField(line2, 2, 35, 8, DecimalField, "argument of perigee");
        CheckField(line2, 2, 44, 8, DecimalField, "mean anomaly");
        CheckField(line2, 2, 53, 11, DecimalField, "mean motion");
        CheckField(line2, 2, 64, 5, CountField, "revolution number at epoch");
    }

    // The syntax of the fields (ASCII digits only): a decimal number with an optional sign and blanks around it, or blank
    // for the first derivative; the exponent fields [sign]ddddd[sign]d or blank; counts right-aligned digits or blank
    private static readonly Regex ClassificationField = new("^[UCS]$", RegexOptions.CultureInvariant);
    private static readonly Regex DesignatorField = new("^( {8}|[0-9]{5}[A-Z]{1,3} *)$", RegexOptions.CultureInvariant);
    private static readonly Regex EpochField = new(@"^[0-9]{5}\.[0-9]{8}$", RegexOptions.CultureInvariant);
    private static readonly Regex DecimalField = new(@"^ *[+-]?[0-9]*\.[0-9]+ *$", RegexOptions.CultureInvariant);
    private static readonly Regex DecimalFieldOrBlank = new(@"^( *[+-]?[0-9]*\.[0-9]+ *| *)$", RegexOptions.CultureInvariant);
    private static readonly Regex ExponentField = new("^([ +-][0-9]{5}[+-][0-9]| *)$", RegexOptions.CultureInvariant);
    private static readonly Regex DigitOrBlank = new("^[0-9 ]$", RegexOptions.CultureInvariant);
    private static readonly Regex CountField = new("^ *[0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex EccentricityField = new("^[0-9]{7}$", RegexOptions.CultureInvariant);

    private static void CheckLength(string line, int number)
    {
        if (line.Length != 69)
        {
            throw new TleFormatException($"TLE line {number} must be exactly 69 characters long, got {line.Length}: {line}", number,
                System.Math.Min(line.Length, 69) + 1);
        }
    }

    private static void CheckField(string line, int number, int column, int width, Regex syntax, string field)
    {
        var text = line.Substring(column - 1, width);
        if (!syntax.IsMatch(text))
        {
            throw new TleFormatException(
                $"Invalid {field} '{text}' in TLE line {number}, columns {column}-{column + width - 1}: {line}", number, column);
        }
    }

    private static void CheckChecksum(string line, int number)
    {
        if (line[68] is < '0' or > '9')
            throw new TleFormatException($"Invalid checksum character in line {number}: '{line[68]}' in {line}", number, 69);

        int computedChecksum = ComputeTleChecksum(line.Substring(0, 68));
        int actualChecksum = line[68] - '0';
        if (computedChecksum != actualChecksum)
        {
            throw new TleFormatException(
                $"Checksum validation failed for TLE line {number}. Expected: {computedChecksum}, Actual: {actualChecksum}, Line: {line}",
                number, 69);
        }
    }

    /// <summary>
    /// Computes the TLE checksum for a line (without the checksum digit).
    /// The checksum is the sum of all digits plus 1 for each minus sign, modulo 10.
    /// </summary>
    /// <param name="line">The TLE line without the checksum digit (68 characters).</param>
    /// <returns>The computed checksum (0-9).</returns>
    private static int ComputeTleChecksum(string line)
    {
        int sum = 0;
        foreach (char c in line)
        {
            if (c is >= '0' and <= '9')
                sum += c - '0';
            else if (c == '-')
                sum += 1;
            // All other characters (letters, spaces, etc.) contribute 0 to the sum
        }

        return sum % 10;
    }

    /// <summary>
    /// Converts the TLE to orbital parameters at a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch time.</param>
    /// <returns>The orbital parameters at the given epoch.</returns>
    public override OrbitalParameters AtEpoch(Time epoch)
    {
        return ToOsculating(epoch);
    }

    /// <summary>
    /// Converts the TLE mean elements to osculating orbital parameters at the TLE epoch
    /// using the SGP4/SDP4 propagator.
    /// </summary>
    /// <returns>
    /// A <see cref="StateVector"/> representing the osculating (instantaneous) orbital state
    /// at the TLE epoch. This can be further converted to <see cref="KeplerianElements"/> 
    /// or <see cref="EquinoctialElements"/> using the standard conversion methods.
    /// </returns>
    /// <remarks>
    /// This is the correct way to obtain physical position and velocity from TLE data.
    /// The SGP4/SDP4 propagator properly accounts for perturbations that are implicit
    /// in the mean elements representation.
    /// </remarks>
    public StateVector ToOsculating()
    {
        return ToOsculating(Epoch);
    }

    /// <summary>
    /// Converts the TLE mean elements to osculating orbital parameters at a given epoch
    /// using the SGP4/SDP4 propagator.
    /// </summary>
    /// <param name="epoch">The epoch time at which to compute the osculating state.</param>
    /// <returns>
    /// A <see cref="StateVector"/> representing the osculating (instantaneous) orbital state
    /// at the specified epoch. This can be further converted to <see cref="KeplerianElements"/> 
    /// or <see cref="EquinoctialElements"/> using the standard conversion methods.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the correct way to obtain physical position and velocity from TLE data.
    /// The SGP4/SDP4 propagator properly accounts for perturbations that are implicit
    /// in the mean elements representation.
    /// </para>
    /// <para>
    /// SGP4 is CSPICE's <c>evsgp4_c</c> (Vallado's 2006 code, AFSPC mode, WGS-72 old, KE = 7.43669161e-2; see
    /// <see cref="TLE"/>), at tsince = the TT seconds from <see cref="OrbitalParameters.Epoch"/> to
    /// <paramref name="epoch"/> over 60, whatever the time scale of <paramref name="epoch"/> (#599): the epoch is taken
    /// as CSPICE's <c>getelm_c</c> reads it, so that tsince is exact. Before 11.0.0 tsince counted the TDB seconds
    /// between the TDB of <paramref name="epoch"/> and that epoch, up to 0.36 m off a day after the epoch. The state is
    /// in <see cref="Frame.TEME"/>, labelled <paramref name="epoch"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">SGP4 failed at this epoch (#454).</exception>
    public StateVector ToOsculating(Time epoch)
    {
        return SpiceAPI.Instance.ConvertTleToStateVector(Name, Line1, Line2, _sgp4Epoch.Value, Epoch, epoch).ToStateVector();
    }

    /// <summary>
    /// Converts the TLE to a state vector at a given date using the SGP4/SDP4 propagator.
    /// </summary>
    /// <param name="date">The epoch time at which to compute the state vector.</param>
    /// <returns>The state vector at the given epoch.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="ToOsculating(Time)"/> for clearer intent.</strong>
    /// </para>
    /// This method overrides the base class to use the SGP4/SDP4 propagator,
    /// which is the correct way to convert TLE mean elements to osculating state vectors.
    /// </remarks>
    public override StateVector ToStateVector(Time date)
    {
        return ToOsculating(date);
    }

    /// <summary>
    /// Converts the TLE to a state vector at the TLE epoch using the SGP4/SDP4 propagator.
    /// </summary>
    /// <returns>The state vector at the TLE epoch.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="ToOsculating()"/> for clearer intent.</strong>
    /// </para>
    /// This method overrides the base class to use the SGP4/SDP4 propagator,
    /// which is the correct way to convert TLE mean elements to osculating state vectors.
    /// </remarks>
    public override StateVector ToStateVector()
    {
        if (_stateVector == null)
        {
            _stateVector = ToOsculating(Epoch);
        }

        return _stateVector;
    }

    /// <summary>
    /// Converts the TLE to osculating Keplerian elements.
    /// </summary>
    /// <returns>
    /// Osculating <see cref="KeplerianElements"/> derived from the SGP4-propagated state vector.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Consider using <see cref="ToOsculating()"/>.ToKeplerianElements() for clearer intent,
    /// or <see cref="ToMeanKeplerianElements()"/> if you need mean elements.</strong>
    /// </para>
    /// This returns osculating (instantaneous) elements, not the mean elements stored in the TLE.
    /// </remarks>
    public override KeplerianElements ToKeplerianElements()
    {
        if (_keplerianElements == null)
        {
            _keplerianElements = ToOsculating().ToKeplerianElements();
        }

        return _keplerianElements;
    }

    /// <summary>
    /// The osculating Keplerian elements of <see cref="ToKeplerianElements()"/>, SGP4's state at the epoch of the TLE,
    /// moved by the two-body motion to <paramref name="epoch"/>: osculating, though the TLE is mean (#463).
    /// </summary>
    /// <param name="epoch">The epoch of the elements.</param>
    /// <returns>Osculating <see cref="KeplerianElements"/> at <paramref name="epoch"/>. For SGP4's state at that epoch, use
    /// <see cref="ToOsculating(Time)"/>.</returns>
    public override KeplerianElements ToKeplerianElements(Time epoch)
    {
        return ToKeplerianElements().ToKeplerianElements(epoch);
    }

    /// <summary>
    /// Converts the TLE to osculating Equinoctial elements.
    /// </summary>
    /// <returns>
    /// Osculating <see cref="EquinoctialElements"/> derived from the SGP4-propagated state vector.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Consider using <see cref="ToOsculating()"/>.ToEquinoctial() for clearer intent.</strong>
    /// </para>
    /// This returns osculating (instantaneous) elements, not mean elements.
    /// </remarks>
    public override EquinoctialElements ToEquinoctial()
    {
        if (_equinoctial == null)
        {
            _equinoctial = ToOsculating().ToEquinoctial();
        }

        return _equinoctial;
    }

    /// <summary>
    /// Converts the TLE to mean Keplerian elements.
    /// </summary>
    /// <remarks>
    /// These are the mean elements as stored in the TLE, suitable for:
    /// <list type="bullet">
    /// <item>Creating new TLEs via <see cref="Create"/></item>
    /// <item>Comparing TLEs</item>
    /// <item>Long-term orbit analysis</item>
    /// </list>
    /// <para>
    /// Do NOT use these for position/velocity calculations - use <see cref="ToOsculating()"/> instead.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A <see cref="KeplerianElements"/> object with <see cref="OrbitalElementsType.Mean"/> 
    /// representing the mean orbital elements from the TLE.
    /// </returns>
    public KeplerianElements ToMeanKeplerianElements()
    {
        if (_meanKeplerianElements == null)
        {
            _meanKeplerianElements = new KeplerianElements(MeanSemiMajorAxis, MeanEccentricity, MeanInclination,
                MeanAscendingNode, MeanArgumentOfPeriapsis, MeanMeanAnomaly, Observer, Epoch, Frame,
                null, null, null, OrbitalElementsType.Mean);
        }

        return _meanKeplerianElements;
    }

    public override double SemiMajorAxis()
    {
        return ToKeplerianElements().A;
    }

    /// <summary>
    /// The direction of the periapsis of the osculating elements at the TLE epoch (SGP4/SDP4), as the semi-major axis,
    /// eccentricity and angles of the TLE are, so that <see cref="OrbitalParameters.PerigeeVector"/> and
    /// <see cref="OrbitalParameters.ApogeeVector"/> are the apsides of its osculating orbit (review of #686).
    /// </summary>
    internal override Vector3 PeriapsisDirection()
    {
        return ToKeplerianElements().PeriapsisDirection();
    }

    public override double Eccentricity()
    {
        return ToKeplerianElements().E;
    }

    public override double Inclination()
    {
        return ToKeplerianElements().I;
    }

    public override double AscendingNode()
    {
        return ToKeplerianElements().RAAN;
    }

    public override double ArgumentOfPeriapsis()
    {
        return ToKeplerianElements().AOP;
    }

    public override double MeanAnomaly()
    {
        return ToKeplerianElements().M;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Name: {Name}");
        sb.AppendLine($"Line 1: {Line1}");
        sb.AppendLine($"Line 2: {Line2}");
        sb.AppendLine($"Epoch: {Epoch}");
        // The semi-major axis needs the GM of the Earth: printed only when the data provider gives it
        if (TryGetMeanSemiMajorAxis(out double meanSemiMajorAxis))
        {
            sb.AppendLine($"Mean Semi-Major Axis: {meanSemiMajorAxis} m");
        }

        sb.AppendLine($"Mean Mean Motion: {MeanMeanMotion} rad/s");
        sb.AppendLine($"Mean Eccentricity: {MeanEccentricity}");
        sb.AppendLine($"Mean Inclination: {MeanInclination} rad");
        sb.AppendLine($"Mean Ascending Node: {MeanAscendingNode} rad");
        sb.AppendLine($"Mean Argument of Periapsis: {MeanArgumentOfPeriapsis} rad");
        sb.AppendLine($"Mean Mean Anomaly: {MeanMeanAnomaly} rad");
        return sb.ToString();
    }

    private bool TryGetMeanSemiMajorAxis(out double value)
    {
        try
        {
            value = MeanSemiMajorAxis;
            return true;
        }
        catch (InvalidOperationException)
        {
            value = double.NaN;
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified TLE is equal to the current TLE.
    /// </summary>
    /// <param name="other">The TLE to compare with the current TLE.</param>
    /// <returns>true if the specified TLE is equal to the current TLE; otherwise, false.</returns>
    public bool Equals(TLE other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        // The lines hold the elements and the epoch, the constructor sets the observer (the Earth) and the frame (TEME):
        // no state is computed (#475; before 11.0.0 the base comparison ran SGP4 on both)
        return GetType() == other.GetType() && Line1 == other.Line1 && Line2 == other.Line2 && Name == other.Name;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current TLE.
    /// </summary>
    /// <param name="obj">The object to compare with the current TLE.</param>
    /// <returns>true if the specified object is equal to the current TLE; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((TLE)obj);
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current TLE.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Line1, Line2, Name);
    }

    /// <summary>
    /// Determines whether two TLE instances are equal.
    /// </summary>
    /// <param name="left">The first TLE to compare.</param>
    /// <param name="right">The second TLE to compare.</param>
    /// <returns>true if the two TLE instances are equal; otherwise, false.</returns>
    public static bool operator ==(TLE left, TLE right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Determines whether two TLE instances are not equal.
    /// </summary>
    /// <param name="left">The first TLE to compare.</param>
    /// <param name="right">The second TLE to compare.</param>
    /// <returns>true if the two TLE instances are not equal; otherwise, false.</returns>
    public static bool operator !=(TLE left, TLE right)
    {
        return !Equals(left, right);
    }

    #region Text

    /// <summary>
    /// Reads the one TLE of a text: two lines, or a name line and two lines (R21 of #668).
    /// </summary>
    /// <param name="text">The text: CR/LF or LF, trailing blanks and blank lines ignored; a name line is trimmed, the
    /// <c>0 </c> Space-Track writes before it removed, and a set without one is named by its catalog field.</param>
    /// <returns>The TLE the constructor builds from the lines and the name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="TleFormatException">The text holds no set, more than one (<see cref="ParseAll(string)"/> reads
    /// them), or a set that does not follow the format, at its line of the text and its column.</exception>
    public static TLE Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Single(ReadSets(new StringReader(text)));
    }

    /// <summary>
    /// Reads the one TLE of a reader, as <see cref="Parse(string)"/>.
    /// </summary>
    /// <param name="reader">The reader, read to its end.</param>
    /// <returns>The TLE.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="TleFormatException">The text holds no set, more than one, or a set that does not follow the
    /// format.</exception>
    public static TLE Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Single(ReadSets(reader));
    }

    /// <summary>
    /// <see cref="Parse(string)"/>, <paramref name="provider"/> unused (<see cref="IParsable{TSelf}"/>).
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: the format is culture invariant.</param>
    /// <returns>The TLE.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="TleFormatException">The text is not one TLE.</exception>
    public static TLE Parse(string s, IFormatProvider provider) => Parse(s);

    /// <summary>
    /// Reads every TLE of a text, in its order: two- and three-line sets, mixed, as <see cref="Parse(string)"/> reads one.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sets, none for a blank text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="TleFormatException">The first set that does not follow the format, at its line of the text and its
    /// column; no partial list.</exception>
    public static IReadOnlyList<TLE> ParseAll(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ParseAll(new StringReader(text));
    }

    /// <summary>
    /// Reads every TLE of a reader, as <see cref="ParseAll(string)"/>.
    /// </summary>
    /// <param name="reader">The reader, read to its end.</param>
    /// <returns>The sets, in their order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="TleFormatException">The first set that does not follow the format.</exception>
    public static IReadOnlyList<TLE> ParseAll(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadSets(reader).Select(set => set.Tle).ToArray();
    }

    /// <summary>
    /// Reads the one TLE of a file, as <see cref="Parse(string)"/>.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The TLE.</returns>
    /// <exception cref="TleFormatException">The file holds no set, more than one (<see cref="LoadAll"/> reads them), or a
    /// set that does not follow the format, at its line of the file.</exception>
    /// <exception cref="IOException">The file cannot be read, as <see cref="File.OpenText"/> throws.</exception>
    public static TLE Load(string path)
    {
        using var reader = File.OpenText(path);
        return Parse(reader);
    }

    /// <summary>
    /// Reads every TLE of a file, in its order, as <see cref="ParseAll(string)"/>: a catalog of CelesTrak or Space-Track,
    /// two- or three-line.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The sets.</returns>
    /// <exception cref="TleFormatException">The first set that does not follow the format, at its line of the file and its
    /// column; no partial list (R21 of #668).</exception>
    /// <exception cref="IOException">The file cannot be read, as <see cref="File.OpenText"/> throws.</exception>
    public static IReadOnlyList<TLE> LoadAll(string path)
    {
        using var reader = File.OpenText(path);
        return ParseAll(reader);
    }

    /// <summary>
    /// <see cref="Parse(string)"/> without an exception for a text that is not one TLE.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="result">The TLE, or null.</param>
    /// <returns>Whether the text is one TLE.</returns>
    public static bool TryParse([NotNullWhen(true)] string text, out TLE result)
    {
        result = null;
        if (text == null) return false;
        try
        {
            result = Parse(text);
            return true;
        }
        catch (TleFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// <see cref="TryParse(string, out TLE)"/>, <paramref name="provider"/> unused (<see cref="IParsable{TSelf}"/>).
    /// </summary>
    /// <param name="s">The text, or null.</param>
    /// <param name="provider">Unused: the format is culture invariant.</param>
    /// <param name="result">The TLE, or null.</param>
    /// <returns>Whether the text is one TLE.</returns>
    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, out TLE result) => TryParse(s, out result);

    /// <summary>
    /// <see cref="Load"/> without an exception for a file that does not exist or is not one TLE.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="result">The TLE, or null.</param>
    /// <returns>Whether the file exists and holds one TLE; an error reading an existing file throws, as
    /// <see cref="File.ReadAllText(string)"/> does.</returns>
    public static bool TryLoad(string path, out TLE result)
    {
        result = null;
        return File.Exists(path) && TryParse(File.ReadAllText(path), out result);
    }

    // The one set of a text: none or several throw at the line of the text where the second starts
    private static TLE Single(List<(TLE Tle, int Line)> sets)
    {
        if (sets.Count == 0)
            throw new TleFormatException("The text holds no TLE.", 1, 1);
        if (sets.Count > 1)
        {
            throw new TleFormatException(
                $"The text holds {sets.Count} TLEs, the second at line {sets[1].Line}: ParseAll or LoadAll reads them all.", sets[1].Line, 1);
        }

        return sets[0].Tle;
    }

    // The sets of a text with the line each starts at (IC-21 of #668's lot L7): the lines trimmed at their end (CR, blanks)
    // and the blank ones skipped; a line that starts with '1' followed by one that starts with '2' opens a set without a
    // name, any other line is the name of the set of the next two (Space-Track's leading "0 " removed, trimmed); a set the
    // constructor refuses throws its TleFormatException at the line of the text
    private static List<(TLE Tle, int Line)> ReadSets(TextReader reader)
    {
        var lines = new List<(string Text, int Number)>();
        int number = 0;
        for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
        {
            number++;
            var text = line.TrimEnd();
            if (text.Length > 0) lines.Add((text, number));
        }

        var sets = new List<(TLE Tle, int Line)>();
        int i = 0;
        while (i < lines.Count)
        {
            int start = lines[i].Number;
            string name = null;
            if (!(lines[i].Text.StartsWith('1') && i + 1 < lines.Count && lines[i + 1].Text.StartsWith('2')))
            {
                var title = lines[i].Text;
                name = (title.StartsWith("0 ", StringComparison.Ordinal) ? title.Substring(2) : title).Trim();
                i++;
            }

            if (i + 1 >= lines.Count)
            {
                throw new TleFormatException($"The TLE that starts at line {start} ends before its two lines.", lines[^1].Number + 1, 1);
            }

            var (line1, number1) = lines[i];
            var (line2, number2) = lines[i + 1];
            try
            {
                sets.Add((new TLE(name, line1, line2), start));
            }
            catch (ArgumentException e) when (e.InnerException is TleFormatException format)
            {
                throw new TleFormatException(format.Message, format.Line == 1 ? number1 : number2, format.Column);
            }

            i += 2;
        }

        return sets;
    }

    #endregion

    #region OMM Conversion

    /// <summary>
    /// Converts this TLE to a CCSDS Orbit Mean-elements Message (OMM).
    /// </summary>
    /// <param name="originator">Optional originator for the OMM header. Defaults to "IO.Astrodynamics".</param>
    /// <returns>A new OMM instance representing this TLE.</returns>
    /// <remarks>
    /// <para>
    /// The resulting OMM is configured for SGP4 propagation with TEME reference frame and UTC time system.
    /// </para>
    /// <para>
    /// The international designator (COSPAR ID) is extracted from TLE line 1. Blank columns 10-17 (a TLE without a
    /// designator) give <c>OBJECT_ID = UNKNOWN</c>, which <see cref="CCSDS.OMM.Omm.ToTle"/> reads back as blank columns
    /// (#604, decided by S. Guillet, 2026-11-18; before 11.0.0 it threw <see cref="ArgumentException"/>).
    /// </para>
    /// <para>
    /// <c>MEAN_MOTION</c> is the field of line 2 as written, in revolutions per day, and the conversion reads no kernel
    /// (decision 111 of #668, decided by S. Guillet, 2026-11-26; before 11.0.0 it was √(GM/a³)·86400/2π, 1 ulp off the field
    /// on about half the sets, e.g. 15.957445309999999 for 15.95744531).
    /// </para>
    /// </remarks>
    public CCSDS.OMM.Omm ToOmm(string originator = null)
    {
        // Create header
        var header = originator != null
            ? CCSDS.Common.CcsdsHeader.Create(originator)
            : CCSDS.Common.CcsdsHeader.CreateDefault();

        // Extract COSPAR ID (international designator) from line 1 positions 9-16
        // TLE format: "98067A" -> OMM format: "1998-067A"
        // Blank columns: OBJECT_ID UNKNOWN, the element being mandatory in the ODM (#604)
        var objectId = CosparId == null ? CCSDS.OMM.OmmMetadata.UnknownObjectId : ConvertCosparIdToObjectId(CosparId);

        // Create metadata for SGP4/TEME configuration
        var metadata = CCSDS.OMM.OmmMetadata.CreateForSgp4(Name, objectId);

        // Create mean elements: angles from radians to degrees, the mean motion the field's own value in rev/day (decision
        // 111 of #668, decided by S. Guillet, 2026-11-26)
        var meanElements = CCSDS.OMM.MeanElements.CreateWithMeanMotion(
            Epoch.DateTime,
            _meanMotionRevolutionsPerDay,
            MeanEccentricity,
            MeanInclination * Constants.Rad2Deg,
            MeanAscendingNode * Constants.Rad2Deg,
            MeanArgumentOfPeriapsis * Constants.Rad2Deg,
            MeanMeanAnomaly * Constants.Rad2Deg);

        // NORAD catalog ID, decoded from Alpha-5 when applicable (validated at construction)
        int? noradCatId = NoradCatalogId;

        // The element set number and the revolution number, omitted when their columns are blank
        int? elementSetNo = string.IsNullOrWhiteSpace(Line1.Substring(64, 4)) ? null : ElementSetNumber;
        int? revAtEpoch = string.IsNullOrWhiteSpace(Line2.Substring(63, 5)) ? null : RevolutionNumberAtEpoch;
        var classChar = ((char)Classification).ToString();

        // Create TLE parameters with BSTAR and MEAN_MOTION_DDOT (standard SGP4)
        var tleParams = CCSDS.OMM.TleParameters.CreateWithBStarAndDDot(
            BallisticCoefficient,
            FirstDerivationMeanMotion,
            SecondDerivativeMeanMotion,
            ephemerisType: 0,
            classificationType: classChar,
            noradCatalogId: noradCatId,
            elementSetNumber: elementSetNo,
            revolutionNumberAtEpoch: revAtEpoch);

        // Create data section
        var data = CCSDS.OMM.OmmData.CreateForTle(meanElements, tleParams);

        return new CCSDS.OMM.Omm(header, metadata, data);
    }

    /// <summary>
    /// Converts a TLE COSPAR ID to OMM Object ID format.
    /// </summary>
    /// <param name="cosparId">The TLE COSPAR ID (e.g., "98067A").</param>
    /// <returns>The OMM Object ID (e.g., "1998-067A").</returns>
    /// <remarks>
    /// TLE format: YYNNNP (e.g., "98067A")
    /// OMM format: YYYY-NNNP (e.g., "1998-067A")
    /// - YY: Last two digits of launch year (converted to YYYY using 57 pivot)
    /// - NNN: Sequential launch number
    /// - P: Piece identifier (A, B, C, etc.)
    /// </remarks>
    private static string ConvertCosparIdToObjectId(string cosparId)
    {
        if (string.IsNullOrWhiteSpace(cosparId) || cosparId.Length < 6)
            return cosparId;

        // Check if it's already in OMM format (contains hyphen or 9+ chars)
        if (cosparId.Contains('-') || cosparId.Length >= 9)
            return cosparId;

        // Parse year (first 2 chars)
        if (!int.TryParse(cosparId.Substring(0, 2), CultureInfo.InvariantCulture, out var year2))
            return cosparId;

        // Convert 2-digit year to 4-digit (pivot at 57: 57-99 = 1957-1999, 00-56 = 2000-2056)
        var year4 = year2 < 57 ? 2000 + year2 : 1900 + year2;

        // Extract launch number (next 3 chars) and piece (remaining)
        var launchNumber = cosparId.Substring(2, 3);
        var piece = cosparId.Length > 5 ? cosparId.Substring(5) : "";

        return $"{year4}-{launchNumber}{piece}";
    }

    #endregion
}