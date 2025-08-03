// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
public class TLE : OrbitalParameters, IEquatable<TLE>
{
    private const int CHECKSUM_LENGTH = 68;
    private const double MAX_ECCENTRICITY = 0.9999999;
    private const int MAX_ELEMENT_SET_NUMBER = 9999;

    private readonly double _a;
    private readonly double _e;
    private readonly double _i;
    private readonly double _o;
    private readonly double _w;
    private readonly double _m;

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
    /// Gets the ballistic coefficient.
    /// </summary>
    public double BalisticCoefficient { get; }

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
    /// Initializes a new instance of the TLE class.
    /// </summary>
    /// <param name="name">Line 1 - TLE Name</param>
    /// <param name="line1">the first line</param>
    /// <param name="line2">the second line</param>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when checksum validation fails.</exception>
    public TLE(string name, string line1, string line2) : base(new CelestialBody(399, new Frame("ITRF93"), ExtractEpochFromTLE(line1)), ExtractEpochFromTLE(line1), new Frame("TEME"))
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
        _a = System.Math.Cbrt(earth.GM / (n * n));
        _e = double.Parse("0." + line2.Substring(26, 7));
        _i = double.Parse(line2.Substring(8, 8)) * Constants.Deg2Rad;
        _o = double.Parse(line2.Substring(17, 8)) * Constants.Deg2Rad;
        _w = double.Parse(line2.Substring(34, 8)) * Constants.Deg2Rad;
        _m = double.Parse(line2.Substring(43, 8)) * Constants.Deg2Rad;

        // Parse line 1 for additional parameters
        FirstDerivationMeanMotion = ParseTleDouble(line1.Substring(33, 10));
        SecondDerivativeMeanMotion = ParseTleExponent(line1.Substring(44, 8));
        BalisticCoefficient = ParseTleExponent(line1.Substring(53, 8));
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
        var kep = orbitalParams.ToKeplerianElements();
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

        // StringBuilder pré-dimensionné pour éviter les réallocations
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
        // var sgp4 = new Sgp4(_tleItem, Sgp4.wgsConstant.WGS_84);
        // EpochTime epochSGP = new EpochTime(date.ToUTC().DateTime);
        // sgp4.runSgp4Cal(epochSGP, epochSGP, 0.01);
        // List<Sgp4Data> resultDataList = sgp4.getResults();
        // var position = resultDataList[0].getPositionData();
        // var velocity = resultDataList[0].getVelocityData();
        // return new StateVector(new Vector3(position.x, position.y, position.z) * 1000.0, new Vector3(velocity.x, velocity.y, velocity.z) * 1000.0,
        //     new CelestialBody(399, Frame.ECLIPTIC_J2000, date), date,
        //     new Frame("TEME")).ToFrame(Frame.ICRF).ToStateVector();

        return API.Instance.ConvertTleToStateVector(Name, Line1, Line2, date).ToFrame(Frame.ICRF).ToStateVector();
    }

    public override double SemiMajorAxis()
    {
        return _a;
    }

    public override double Eccentricity()
    {
        return _e;
    }

    public override double Inclination()
    {
        return _i;
    }

    public override double AscendingNode()
    {
        return _o;
    }

    public override double ArgumentOfPeriapsis()
    {
        return _w;
    }

    public override double MeanAnomaly()
    {
        return _m;
    }

    public override StateVector ToStateVector()
    {
        return ToStateVector(Epoch);
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