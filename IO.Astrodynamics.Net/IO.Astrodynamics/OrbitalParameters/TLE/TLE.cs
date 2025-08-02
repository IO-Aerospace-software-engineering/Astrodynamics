// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using One_Sgp4;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
public class TLE : OrbitalParameters, IEquatable<TLE>
{
    private const int CHECKSUM_LENGTH = 68;
    private const double MAX_ECCENTRICITY = 0.9999999;
    private const int MAX_ELEMENT_SET_NUMBER = 9999;

    private readonly Tle _tleItem;
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
    /// Gets the drag term.
    /// </summary>
    public double DragTerm { get; }

    /// <summary>
    /// Gets the second derivative of the mean motion.
    /// </summary>
    public double SecondDerivativeMeanMotion { get; }

    /// <summary>
    /// Initializes a new instance of the TLE class.
    /// </summary>
    /// <param name="name">Line 1 - TLE Name</param>
    /// <param name="line1">the first line</param>
    /// <param name="line2">the second line</param>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    public TLE(string name, string line1, string line2) : this(ParserTLE.parseTle(line1, line2, name))
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
        Line1 = line1;
        Line2 = line2;
        Name = name;
    }

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

        // TLE checksum: sum of all digits + 1 for each '-' (mod 10)
        static int ComputeTleChecksum(string line)
        {
            if (line.Length != CHECKSUM_LENGTH)
            {
                throw new ArgumentException("TLE line must be exactly 68 characters long.", nameof(line));
            }

            int sum = 0;
            foreach (char c in line.Take(CHECKSUM_LENGTH))
            {
                if (char.IsDigit(c)) sum += c - '0';
                if (c == '-') sum += 1;
            }

            return sum % 10;
        }
    }

    private TLE(Tle tle) : this(tle, new Time(new EpochTime(tle.getEpochYear(), tle.getEpochDay()).toDateTime(), TimeFrame.UTCFrame))
    {
    }

    private TLE(Tle tle, Time epoch) : base(new CelestialBody(399, new Frame("ITRF93"), epoch), epoch, new Frame("TEME"))
    {
        _tleItem = tle;
        Epoch = epoch;
        var earth = new CelestialBody(399, new Frame("ITRF93"), Epoch);
        var revDay = _tleItem.getMeanMotion();
        double n = Constants._2PI / (86400.0 / revDay);
        _a = System.Math.Cbrt(earth.GM / (n * n));
        _e = _tleItem.getEccentriciy();
        _i = _tleItem.getInclination() * Constants.Deg2Rad;
        _o = _tleItem.getRightAscendingNode() * Constants.Deg2Rad;
        _w = _tleItem.getPerigee() * Constants.Deg2Rad;
        _m = _tleItem.getMeanAnomoly() * Constants.Deg2Rad;

        BalisticCoefficient = _tleItem.getFirstMeanMotion();
        DragTerm = _tleItem.getDrag();
        SecondDerivativeMeanMotion = _tleItem.getSecondMeanMotion();
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
        var sgp4 = new Sgp4(_tleItem, Sgp4.wgsConstant.WGS_84);
        EpochTime epochSGP = new EpochTime(date.ToUTC().DateTime);
        sgp4.runSgp4Cal(epochSGP, epochSGP, 0.01);
        List<Sgp4Data> resultDataList = sgp4.getResults();
        var position = resultDataList[0].getPositionData();
        var velocity = resultDataList[0].getVelocityData();
        return new StateVector(new Vector3(position.x, position.y, position.z) * 1000.0, new Vector3(velocity.x, velocity.y, velocity.z) * 1000.0,
            new CelestialBody(399, Frame.ECLIPTIC_J2000, date), date,
            new Frame("TEME")).ToFrame(Frame.ICRF).ToStateVector();
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