// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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
        new Frame("TEME"))
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

        // Parse line 2 for orbital elements
        var meanMotion = double.Parse(line2.Substring(52, 11));
        double n = Constants._2PI / (86400.0 / meanMotion);
        MeanSemiMajorAxis = System.Math.Cbrt(earth.GM / (n * n));
        MeanEccentricity = double.Parse("0." + line2.Substring(26, 7));
        MeanInclination = double.Parse(line2.Substring(8, 8)) * Constants.Deg2Rad;
        MeanAscendingNode = double.Parse(line2.Substring(17, 8)) * Constants.Deg2Rad;
        MeanArgumentOfPeriapsis = double.Parse(line2.Substring(34, 8)) * Constants.Deg2Rad;
        MeanMeanAnomaly = double.Parse(line2.Substring(43, 8)) * Constants.Deg2Rad;

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

        // Parse year (YY format)
        int year = int.Parse(epochStr.Substring(0, 2));
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
        int exponent = int.Parse(exponentStr);

        double result = mantissa * System.Math.Pow(10, exponent);
        return sign == '-' ? -result : result;
    }

    /// <summary>
    /// Creates a new TLE from orbital parameters and metadata.
    /// </summary>
    /// <param name="orbitalParams">The orbital parameters to convert to TLE format.</param>
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
    public static TLE Create(OrbitalParameters orbitalParams, string name, ushort noradId, string cosparId, ushort revolutionsAtEpoch,
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

        // 1. Convert elements to TLE units
        var kep = orbitalParams is TLE tle ? tle.ToMeanKeplerianElements() : orbitalParams.ToKeplerianElements();
        double iDeg = kep.I * Constants.Rad2Deg;
        double raanDeg = kep.RAAN * Constants.Rad2Deg;
        double argpDeg = kep.AOP * Constants.Rad2Deg;
        double mDeg = kep.M * Constants.Rad2Deg;
        double meanMotion = kep.MeanMotion() * 86400.0 / Constants._2PI;

        // 2. Epoch: YYDDD.DDDDDDDD
        var epochDt = kep.Epoch.DateTime;
        int year = epochDt.Year % 100;
        int dayOfYear = epochDt.DayOfYear;
        double fracDay = (epochDt.TimeOfDay.TotalSeconds / 86400.0);
        string epochStr = $"{year:00}{dayOfYear:000}{fracDay:.00000000}".PadRight(14);

        // 3. Eccentricity: 7 digits, no decimal
        string eStr = System.Math.Min(MAX_ECCENTRICITY, System.Math.Max(0.0, kep.E))
            .ToString("0.0000000").Substring(2, 7);

        // 4. nDot, nDDot, BSTAR: TLE scientific notation
        string nDotStr = nDot.ToString(" .00000000;-.00000000").Replace(",", ".").PadLeft(10);
        string nDDotStr = FormatTleExponent(nDDot, 5);
        string bstarStr = FormatTleExponent(bstar, 5);

        // Pre-sized StringBuilder to avoid reallocations
        var line1Builder = new StringBuilder(69);
        var line2Builder = new StringBuilder(69);

        // Construction Line 1 sans allocations intermédiaires
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

        // Calculer checksum sur la string actuelle
        string line1Temp = line1Builder.ToString();
        line1Builder.Append(ComputeTleChecksum(line1Temp));

        // Construction Line 2
        line2Builder.Append("2 ")
            .Append(noradId.ToString("00000"))
            .Append(' ')
            .Append(iDeg.ToString("0.0000").PadLeft(8))
            .Append(' ')
            .Append(raanDeg.ToString("0.0000").PadLeft(8))
            .Append(' ')
            .Append(eStr)
            .Append(' ')
            .Append(argpDeg.ToString("0.0000").PadLeft(8))
            .Append(' ')
            .Append(mDeg.ToString("0.0000").PadLeft(8))
            .Append(' ')
            .Append(meanMotion.ToString("0.00000000").PadLeft(11))
            .Append(revolutionsAtEpoch.ToString().PadLeft(5));

        string line2Temp = line2Builder.ToString();
        line2Builder.Append(ComputeTleChecksum(line2Temp));

        return new TLE(name, line1Builder.ToString(), line2Builder.ToString());
    }

    // Helper for TLE scientific notation (e.g.  34123-4)
    static string FormatTleExponent(double value, int width)
    {
        if (System.Math.Abs(value) < 1e-15)
            return ' ' + new string('0', width) + "-0";

        var scientificNotation = value.ToString("0.0000E+0", CultureInfo.InvariantCulture);
        var parts = scientificNotation.Split('E');
        var mantissa = parts[0].Replace(".", "").PadLeft(width, '0');
        var exponent = parts[1];
        var sign = value < 0 ? '-' : ' ';

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
        return ToStateVector(epoch);
    }

    /// <summary>
    /// Converts the TLE to a state vector at a given date.
    /// </summary>
    /// <param name="date"></param>
    /// <returns>The state vector at the given epoch.</returns>
    public override StateVector ToStateVector(Time date)
    {
        return API.Instance.ConvertTleToStateVector(Name, Line1, Line2, date).ToStateVector();
    }

    /// <summary>
    /// Converts the TLE to Keplerian elements.
    /// This method computes the Keplerian elements from the state vector derived from the TLE.
    /// If the Keplerian elements have already been computed, it returns the cached value.
    /// This is useful for performance optimization, as the conversion can be computationally expensive.
    /// The Keplerian elements include parameters such as semi-major axis, eccentricity, inclination,
    /// right ascension of the ascending node, argument of periapsis, and mean anomaly.
    /// These parameters describe the orbit of the satellite in a standard form that is widely used in
    /// astrodynamics and orbital mechanics.
    /// The method first checks if the Keplerian elements have already been computed and cached.
    /// If they have, it returns the cached value to avoid redundant calculations.
    /// If the Keplerian elements have not been computed yet, it converts the TLE to a state vector
    /// using the `ToStateVector` method, and then converts that state vector to Keplerian elements.
    /// This ensures that the TLE is accurately represented in the standard Keplerian format,
    /// which is essential for further orbital analysis and calculations.
    /// <remarks>
    /// This method is particularly useful when working with TLE data, as it allows for easy
    /// conversion to a more usable form for orbital mechanics calculations.
    /// The Keplerian elements provide a clear and concise representation of the orbit,
    /// making it easier to perform various analyses such as predicting the satellite's position,
    /// calculating orbital maneuvers, and understanding the dynamics of the orbit.
    /// </remarks>
    /// <seealso cref="ToStateVector(IO.Astrodynamics.TimeSystem.Time)"/>
    /// <seealso cref="KeplerianElements"/>
    /// <seealso cref="OrbitalParameters.ToKeplerianElements(IO.Astrodynamics.TimeSystem.Time)"/>
    /// <seealso cref="Frame.ICRF"/>
    /// <seealso cref="StateVector.ToFrame(Frame)"/>
    /// <seealso cref="StateVector.ToStateVector()"/>
    /// <remarks>
    /// This method is part of the TLE class, which represents a Two-Line Element set of orbital parameters.
    /// TLEs are commonly used in satellite tracking and orbital mechanics to describe the orbits of satellites.
    /// The TLE format consists of two lines of text, each containing specific orbital parameters
    /// and metadata about the satellite.
    /// The TLE class provides methods to convert these parameters into various formats,
    /// including state vectors and Keplerian elements, which are essential for performing
    /// orbital calculations and analyses.
    /// </remarks>
    /// <seealso cref="OrbitalParameters"/>
    /// <seealso cref="TLE"/>
    /// <seealso cref="API"/>
    /// <seealso cref="API.Instance"/>
    /// <seealso cref="Frame"/>
    /// <seealso cref="Time"/>
    /// <seealso cref="StateVector"/>
    /// <seealso cref="CelestialBody"/>
    /// <seealso cref="Constants"/>
    /// <seealso cref="Constants.Rad2Deg"/>
    /// <seealso cref="Constants.Deg2Rad"/>
    /// <seealso cref="Constants._2PI"/>
    /// </summary>
    /// <returns></returns>
    public override KeplerianElements ToKeplerianElements()
    {
        if (_keplerianElements == null)
        {
            // Convert TLE to Keplerian elements using the state vector
            _keplerianElements = ToStateVector().ToKeplerianElements();
        }

        return _keplerianElements;
    }

    /// <summary>
    /// Converts the TLE to Equinoctial elements.
    /// This method computes the Equinoctial elements from the state vector derived from the TLE.
    /// If the Equinoctial elements have already been computed, it returns the cached value.
    /// This is useful for performance optimization, as the conversion can be computationally expensive.
    /// The Equinoctial elements provide an alternative representation of the orbit that is often more
    /// convenient for certain types of orbital maneuvers and analyses.
    /// The method first checks if the Equinoctial elements have already been computed and cached.
    /// If they have, it returns the cached value to avoid redundant calculations.
    /// If the Equinoctial elements have not been computed yet, it converts the TLE to a state vector
    /// using the `ToStateVector` method, and then converts that state vector to Equinoctial elements.
    /// This ensures that the TLE is accurately represented in the Equinoctial format,
    /// which is essential for certain types of orbital analyses and calculations.
    /// <remarks>
    /// This method is particularly useful when working with TLE data, as it allows for easy
    /// conversion to a more usable form for orbital mechanics calculations.
    /// The Equinoctial elements provide a clear and concise representation of the orbit,
    /// making it easier to perform various analyses such as predicting the satellite's position,
    /// calculating orbital maneuvers, and understanding the dynamics of the orbit.
    /// </remarks>
    /// </summary>
    public override EquinoctialElements ToEquinoctial()
    {
        if (_equinoctial == null)
        {
            _equinoctial = ToStateVector().ToEquinoctial();
        }

        return _equinoctial;
    }

    /// <summary>
    /// Converts the TLE to mean Keplerian elements.
    /// This method computes the mean Keplerian elements from the TLE parameters.
    /// If the mean Keplerian elements have already been computed, it returns the cached value.
    /// This is useful for performance optimization, as the conversion can be computationally expensive.
    /// The mean Keplerian elements provide a standard representation of the orbit that is often used
    /// for long-term predictions and analyses.
    /// The method first checks if the mean Keplerian elements have already been computed and cached.
    /// If they have, it returns the cached value to avoid redundant calculations.
    /// </summary>
    /// <returns></returns>
    public KeplerianElements ToMeanKeplerianElements()
    {
        if (_meanKeplerianElements == null)
        {
            _meanKeplerianElements = new KeplerianElements(MeanSemiMajorAxis, MeanEccentricity, MeanInclination,
                MeanAscendingNode, MeanArgumentOfPeriapsis, MeanMeanAnomaly, Observer, Epoch, Frame);
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

    public override StateVector ToStateVector()
    {
        if (_stateVector == null)
        {
            _stateVector = ToStateVector(Epoch);
        }

        return _stateVector;
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
}