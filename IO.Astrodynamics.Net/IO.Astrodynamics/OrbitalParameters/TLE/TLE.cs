// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
public class TLE : OrbitalParameters, IEquatable<TLE>
{
    private const double MAX_ECCENTRICITY = 0.9999999;
    private const int MAX_ELEMENT_SET_NUMBER = 9999;
    private KeplerianElements _meanKeplerianElements;

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
    /// Gets the ballistic coefficient (drag term) in units of 1/earth radii.
    /// This value is used to model atmospheric drag effects on the satellite's orbit.
    /// It is typically a small value, indicating the amount of drag experienced by the satellite.
    /// For example, a value of 0.0001 means that the satellite experiences a drag force equivalent to 0.0001 times the gravitational force at the Earth's surface.
    /// </summary>
    public double BallisticCoefficient { get; }

    /// <summary>
    /// Gets the first derivative of the mean motion (in revolutions per day squared).
    /// This value represents the rate of change of the mean motion of the satellite.
    /// It is often used to account for perturbations in the satellite's orbit, such as
    /// atmospheric drag or gravitational perturbations from other celestial bodies.
    /// For example, a value of 0.000001 means that the mean motion increases by 0.000001 revolutions per day squared for each day that passes.
    /// This parameter is crucial for accurate orbit predictions and is derived from tracking data.
    /// It is typically a small value, indicating that the mean motion changes gradually over time due to various perturbative effects.
    ///  </summary>
    public double FirstDerivationMeanMotion { get; }

    /// <summary>
    /// Gets the second derivative of the mean motion (in revolutions per day cubed).
    /// This value is often used to account for atmospheric drag effects on the satellite's orbit.
    /// It is typically a very small value, indicating the rate of change of the first derivative
    /// of the mean motion.
    /// For example, a value of 0.000001 means that the first derivative of the mean motion
    /// changes by 0.000001 revolutions per day squared for each day that passes.
    /// This parameter is crucial for accurate long-term orbit predictions, especially for low Earth orbit satellites.
    /// It is often derived from tracking data and is used in conjunction with the first derivative of the mean motion
    /// to refine the satellite's orbital model.
    /// </summary>
    public double SecondDerivativeMeanMotion { get; }

    /// <summary>
    /// Gets the mean semi-major axis of the orbit in meters.
    /// This value represents the average distance from the center of the Earth to the satellite's orbit.
    /// It is a key parameter in determining the size of the orbit and is calculated based on
    /// the mean motion and gravitational parameters of the Earth.
    /// For example, a value of 7000 kilometers means that the average distance from the
    /// center of the Earth to the satellite's orbit is 7000 kilometers.
    /// This parameter is crucial for understanding the satellite's orbital characteristics,
    /// including its altitude and orbital period.
    /// </summary>
    public double MeanSemiMajorAxis { get; }

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
    /// <param name="name">Line 1 - TLE Name</param>
    /// <param name="line1">the first line</param>
    /// <param name="line2">the second line</param>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when checksum validation fails.</exception>
    public TLE(string name, string line1, string line2) : base(new CelestialBody(399, new Frame("ITRF93"), ExtractEpochFromTLE(line1)), ExtractEpochFromTLE(line1),
        new Frame("TEME"), OrbitalElementsType.Mean)
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));

        // Validate TLE format and checksums
        ValidateTleFormat(line1, line2);

        Line1 = line1;
        Line2 = line2;
        Name = name;

        // Extract orbital parameters from TLE lines
        var epoch = ExtractEpochFromTLE(line1);
        var earth = new CelestialBody(399, new Frame("ITRF93"), epoch);

        // Parse line 2 for orbital elements - FIXED: Use InvariantCulture
        var meanMotionRevPerDay = double.Parse(line2.Substring(52, 11), CultureInfo.InvariantCulture);
        double n = meanMotionRevPerDay * Constants._2PI / 86400.0; // Convert rev/day to rad/s
        MeanSemiMajorAxis = System.Math.Cbrt(earth.GM / (n * n));

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
    }

    /// <summary>
    /// Extracts the epoch time from TLE line 1.
    /// </summary>
    /// <param name="line1">The first line of the TLE.</param>
    /// <returns>The epoch time.</returns>
    private static Time ExtractEpochFromTLE(string line1)
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

        // Parse day of year (DDD.DDDDDDDD)
        double dayOfYear = double.Parse(epochStr.Substring(2), CultureInfo.InvariantCulture);

        // Create DateTime from year and day of year
        var yearStart = new DateTime(year, 1, 1);
        var epochDateTime = yearStart.AddDays(dayOfYear - 1.0);

        return new Time(epochDateTime, TimeFrame.UTCFrame);
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
    /// <param name="noradId">The NORAD catalog number (5 digits).</param>
    /// <param name="cosparId">The COSPAR international designator (6-8 characters).</param>
    /// <param name="revolutionsAtEpoch">The revolution number at epoch.</param>
    /// <param name="classification">The security classification of the object (default: Unclassified).</param>
    /// <param name="bstar">The ballistic coefficient (drag term) in units of 1/earth radii (default: 0.0001).</param>
    /// <param name="nDot">The first derivative of mean motion in revolutions/day² (default: 0.0).</param>
    /// <param name="nDDot">The second derivative of mean motion in revolutions/day³ (default: 0.0).</param>
    /// <param name="elementSetNumber">The element set number (default: 9999).</param>
    /// <returns>A new TLE instance constructed from the provided parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when orbitalParams or name is null.</exception>
    /// <exception cref="ArgumentException">Thrown when cosparId is null, whitespace, or not between 6-8 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when elementSetNumber exceeds the maximum allowed value.</exception>
    public static TLE Create(OrbitalParameters meanElements, string name, ushort noradId, string cosparId, ushort revolutionsAtEpoch,
        Classification classification = Classification.Unclassified, double bstar = 0.0001, double nDot = 0.0, double nDDot = 0.0, ushort elementSetNumber = MAX_ELEMENT_SET_NUMBER)
    {
        if (meanElements == null) throw new ArgumentNullException(nameof(meanElements));
        if (meanElements.ElementsType != OrbitalElementsType.Mean)
            throw new ArgumentException("Orbital parameters must be mean elements. Use KeplerianElements.FromOMM() or ensure ElementsType is Mean.", nameof(meanElements));

        return CreateInternal(meanElements, name, noradId, cosparId, revolutionsAtEpoch, classification, bstar, nDot, nDDot, elementSetNumber);
    }

    /// <summary>
    /// Internal method for creating TLE from orbital parameters during the fitting process.
    /// This method does not validate ElementsType, allowing iterative convergence from osculating to mean elements.
    /// </summary>
    internal static TLE CreateInternal(OrbitalParameters orbitalParams, string name, ushort noradId, string cosparId, ushort revolutionsAtEpoch,
        Classification classification = Classification.Unclassified, double bstar = 0.0001, double nDot = 0.0, double nDDot = 0.0, ushort elementSetNumber = MAX_ELEMENT_SET_NUMBER)
    {
        if (orbitalParams == null) throw new ArgumentNullException(nameof(orbitalParams));
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(cosparId)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(cosparId));
        if (cosparId.Length is < 6 or > 8)
        {
            throw new ArgumentException("COSPAR Identifier must be between 6 and 8 characters long.", nameof(cosparId));
        }

        if (elementSetNumber > MAX_ELEMENT_SET_NUMBER)
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
        double meanMotion = kep.MeanMotion() * 86400.0 / Constants._2PI;

        // Format epoch: YYDDD.DDDDDDDD - optimized with Span
        var epochDt = kep.Epoch.DateTime;
        int year = epochDt.Year % 100;
        int dayOfYear = epochDt.DayOfYear;
        double fracDay = epochDt.TimeOfDay.TotalSeconds / 86400.0;

        // Pre-allocate for epoch string: YY + DDD + . + 8 decimals = 14 chars
        Span<char> epochSpan = stackalloc char[14];
        year.TryFormat(epochSpan, out int pos1, "00", CultureInfo.InvariantCulture);
        dayOfYear.TryFormat(epochSpan.Slice(2), out int pos2, "000", CultureInfo.InvariantCulture);

        // Format fractional day directly into span
        string fracDayStr = fracDay.ToString("0.00000000", CultureInfo.InvariantCulture);
        fracDayStr.AsSpan(1, 9).CopyTo(epochSpan.Slice(5)); // Copy ".DDDDDDDD"
        string epochStr = new string(epochSpan);

        // Format eccentricity: 7 digits, no decimal point
        double clampedE = System.Math.Clamp(kep.E, 1e-7, MAX_ECCENTRICITY);
        string eStr = clampedE.ToString("0.0000000", CultureInfo.InvariantCulture).Substring(2, 7);

        // Format derivatives and drag term
        string nDotStr = nDot.ToString(" .00000000;-.00000000", CultureInfo.InvariantCulture).PadLeft(10);
        string nDDotStr = FormatTleExponent(nDDot, 5);
        string bstarStr = FormatTleExponent(bstar, 5);

        // Build line 1 - use single ToString call
        var line1Builder = new StringBuilder(69);
        line1Builder.Append("1 ")
            .Append(noradId.ToString("00000"))
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
            .Append(noradId.ToString("00000"))
            .Append(' ')
            .Append(iDeg.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(raanDeg.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(eStr)
            .Append(' ')
            .Append(argpDeg.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(mDeg.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(8))
            .Append(' ')
            .Append(meanMotion.ToString("0.00000000", CultureInfo.InvariantCulture).PadLeft(11))
            .Append(revolutionsAtEpoch.ToString().PadLeft(5));

        int checksum2 = ComputeTleChecksumFromBuilder(line2Builder);
        line2Builder.Append(checksum2);

        return new TLE(name, line1Builder.ToString(), line2Builder.ToString());
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
    static string FormatTleExponent(double value, int width)
    {
        if (System.Math.Abs(value) < 1e-15)
            return ' ' + new string('0', width) + "-0";

        var sign = value < 0 ? '-' : ' ';
        var absValue = System.Math.Abs(value);

        var scientificNotation = absValue.ToString("0.0000E+0", CultureInfo.InvariantCulture);
        var parts = scientificNotation.Split('E');
        var mantissa = parts[0].Replace(".", "").PadLeft(width, '0');

        // TLE format uses 0.XXXXX mantissa (0.1 to 1), C# uses X.XXXX (1 to 10)
        // So we must increment the exponent by 1 to compensate
        int exponentValue = int.Parse(parts[1], CultureInfo.InvariantCulture) + 1;
        string exponent = exponentValue >= 0 ? $"+{exponentValue}" : exponentValue.ToString();

        return $"{sign}{mantissa}{exponent}";
    }

    /// <summary>
    /// Validates the format and checksums of the TLE lines.
    /// </summary>
    /// <param name="line1">The first line of the TLE.</param>
    /// <param name="line2">The second line of the TLE.</param>
    /// <exception cref="InvalidOperationException">Thrown when checksum validation fails.</exception>
    /// <exception cref="ArgumentException">Thrown when TLE format is invalid.</exception>
    private static void ValidateTleFormat(string line1, string line2)
    {
        // Validate line length
        if (line1.Length != 69)
            throw new ArgumentException($"TLE line 1 must be exactly 69 characters long, got {line1.Length}: {line1}");
        if (line2.Length != 69)
            throw new ArgumentException($"TLE line 2 must be exactly 69 characters long, got {line2.Length}: {line2}");

        // Validate line numbers
        if (line1[0] != '1')
            throw new ArgumentException($"TLE line 1 must start with '1', got '{line1[0]}': {line1}");
        if (line2[0] != '2')
            throw new ArgumentException($"TLE line 2 must start with '2', got '{line2[0]}': {line2}");

        // Validate checksums
        ValidateLineChecksum(line1, 1);
        ValidateLineChecksum(line2, 2);
    }

    /// <summary>
    /// Validates the checksum of a single TLE line.
    /// </summary>
    /// <param name="line">The TLE line to validate.</param>
    /// <param name="lineNumber">The line number (1 or 2) for error reporting.</param>
    /// <exception cref="InvalidOperationException">Thrown when checksum validation fails.</exception>
    private static void ValidateLineChecksum(string line, int lineNumber)
    {
        // Extract the first 68 characters (excluding the checksum)
        string lineWithoutChecksum = line.Substring(0, 68);

        // Compute the expected checksum
        int computedChecksum = ComputeTleChecksum(lineWithoutChecksum);

        // Get the actual checksum from the last character
        if (!char.IsDigit(line[68]))
            throw new InvalidOperationException($"Invalid checksum character in line {lineNumber}: '{line[68]}' in {line}");

        int actualChecksum = line[68] - '0';

        // Compare checksums
        if (computedChecksum != actualChecksum)
        {
            throw new InvalidOperationException(
                $"Checksum validation failed for TLE line {lineNumber}. " +
                $"Expected: {computedChecksum}, Actual: {actualChecksum}, Line: {line}");
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
            if (char.IsDigit(c))
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
    /// This is the correct way to obtain physical position and velocity from TLE data.
    /// The SGP4/SDP4 propagator properly accounts for perturbations that are implicit
    /// in the mean elements representation.
    /// </remarks>
    public StateVector ToOsculating(Time epoch)
    {
        return SpiceAPI.Instance.ConvertTleToStateVector(Name, Line1, Line2, epoch).ToStateVector();
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
        sb.AppendLine($"Mean Semi-Major Axis: {MeanSemiMajorAxis} m");
        sb.AppendLine($"Mean Eccentricity: {MeanEccentricity}");
        sb.AppendLine($"Mean Inclination: {MeanInclination} rad");
        sb.AppendLine($"Mean Ascending Node: {MeanAscendingNode} rad");
        sb.AppendLine($"Mean Argument of Periapsis: {MeanArgumentOfPeriapsis} rad");
        sb.AppendLine($"Mean Mean Anomaly: {MeanMeanAnomaly} rad");
        return sb.ToString();
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
        return base.Equals(other) && Line1 == other.Line1 && Line2 == other.Line2 && Name == other.Name;
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
    /// The international designator (COSPAR ID) is extracted from TLE line 1.
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
        var tleCosparId = Line1.Substring(9, 8).Trim();
        var objectId = ConvertCosparIdToObjectId(tleCosparId);

        // Create metadata for SGP4/TEME configuration
        var metadata = CCSDS.OMM.OmmMetadata.CreateForSgp4(Name, objectId);

        // Create mean elements
        // Convert angles from radians to degrees, mean motion from rad/s to rev/day
        var meanMotionRevPerDay = MeanMotion() * 86400.0 / Constants._2PI;

        var meanElements = CCSDS.OMM.MeanElements.CreateWithMeanMotion(
            Epoch.DateTime,
            meanMotionRevPerDay,
            MeanEccentricity,
            MeanInclination * Constants.Rad2Deg,
            MeanAscendingNode * Constants.Rad2Deg,
            MeanArgumentOfPeriapsis * Constants.Rad2Deg,
            MeanMeanAnomaly * Constants.Rad2Deg);

        // Extract NORAD catalog ID from line 1 positions 2-6
        var noradCatIdStr = Line1.Substring(2, 5).Trim();
        int? noradCatId = int.TryParse(noradCatIdStr, CultureInfo.InvariantCulture, out var id) ? id : null;

        // Extract element set number from line 1 positions 64-67
        var elementSetNoStr = Line1.Substring(64, 4).Trim();
        int? elementSetNo = int.TryParse(elementSetNoStr, CultureInfo.InvariantCulture, out var esn) ? esn : null;

        // Extract revolution number at epoch from line 2 positions 63-67
        var revAtEpochStr = Line2.Substring(63, 5).Trim();
        int? revAtEpoch = int.TryParse(revAtEpochStr, CultureInfo.InvariantCulture, out var rev) ? rev : null;

        // Extract classification from line 1 position 7
        var classChar = Line1[7].ToString().ToUpperInvariant();

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
    /// Gets the mean motion in radians per second.
    /// </summary>
    /// <returns>Mean motion in rad/s.</returns>
    /// <remarks>
    /// This is computed from the mean semi-major axis using Kepler's third law:
    /// n = sqrt(GM/a³)
    /// </remarks>
    public double MeanMotion()
    {
        return System.Math.Sqrt(Observer.GM / (MeanSemiMajorAxis * MeanSemiMajorAxis * MeanSemiMajorAxis));
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