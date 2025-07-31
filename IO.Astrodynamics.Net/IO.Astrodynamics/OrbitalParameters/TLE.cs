// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using One_Sgp4;

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
public class TLE : OrbitalParameters, IEquatable<TLE>
{
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
    /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
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

    public static TLE Create(KeplerianElements kep, string name, int noradId, string cosparId, ushort revolutionsAtEpoch, char classification = 'U', double bstar = 0.0001,
        double nDot = 0.0,
        double nDDot = 0.0, ushort elementSetNumber = 9999)
    {
        if (kep == null) throw new ArgumentNullException(nameof(kep));
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(cosparId)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(cosparId));
        if (cosparId.Length < 6 || cosparId.Length > 8)
        {
            throw new ArgumentException("COSPAR Identifier must be between 6 and 8 characters long.", nameof(cosparId));
        }

        if (elementSetNumber > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(elementSetNumber), "Element set number must be between 0 and 9999.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revolutionsAtEpoch);
        ArgumentOutOfRangeException.ThrowIfNegative(noradId);

        // 1. Convert elements to TLE units
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
        string eStr = kep.E.ToString("0.0000000").Substring(2, 7);

        // 4. nDot, nDDot, BSTAR: TLE scientific notation
        string nDotStr = nDot.ToString(" .00000000;-.00000000").Replace(",", ".").PadLeft(10);
        string nDDotStr = FormatTleExponent(nDDot, 5);
        string bstarStr = FormatTleExponent(bstar, 5);

        // 5. Line 1
        string line1 = $"1 {noradId:00000}{classification} {cosparId,-8} {epochStr} {nDotStr} {nDDotStr} {bstarStr} 0 {elementSetNumber,4}";
        line1 += ComputeTleChecksum(line1);

        // 6. Line 2
        string line2 = $"2 {noradId:00000} {iDeg,8:0.0000} {raanDeg,8:0.0000} {eStr} {argpDeg,8:0.0000} {mDeg,8:0.0000} {meanMotion,11:0.00000000}{revolutionsAtEpoch,5}";
        line2 += ComputeTleChecksum(line2);

        return new TLE(name, line1, line2);

        // Helper for TLE scientific notation (e.g.  34123-4)
        static string FormatTleExponent(double value, int width)
        {
            if (value == 0.0) return ' ' + new string('0', width) + "-0";
            string s = value.ToString("0.0000E+0", System.Globalization.CultureInfo.InvariantCulture);
            s = s.Replace("E+", "").Replace("E", "");
            int expIdx = s.IndexOfAny(['+', '-'], 1);
            string mant = s.Substring(0, expIdx).Replace(".", "").PadLeft(width, '0');
            string exp = s.Substring(expIdx);
            var sign = value < 0 ? '-' : ' ';
            return $"{sign}{mant}{exp}";
        }

        // TLE checksum: sum of all digits + 1 for each '-' (mod 10)
        static int ComputeTleChecksum(string line)
        {
            if (line.Length != 68)
            {
                throw new ArgumentException("TLE line must be exactly 69 characters long.", nameof(line));
            }

            int sum = 0;
            foreach (char c in line.Take(68))
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

    public static Time GetEpoch(string line1, string line2, string line3)
    {
        var tle = ParserTLE.parseTle(line2, line3, line1);
        return new Time(new EpochTime(tle.getEpochYear(), tle.getEpochDay()).toDateTime(), TimeFrame.UTCFrame);
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