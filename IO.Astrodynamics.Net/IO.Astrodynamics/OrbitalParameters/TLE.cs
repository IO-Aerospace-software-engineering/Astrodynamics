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
    /// Gets the third line of the TLE.
    /// </summary>
    public string Line3 { get; }

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
    /// <param name="line1">The first line of the TLE.</param>
    /// <param name="line2">The second line of the TLE.</param>
    /// <param name="line3">The third line of the TLE.</param>
    /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    public TLE(string line1, string line2, string line3) : this(ParserTLE.parseTle(line2, line3, line1))
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(line3)) throw new ArgumentException("Value cannot be null or empty.", nameof(line3));
        Line1 = line1;
        Line2 = line2;
        Line3 = line3;
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
        return new StateVector(new Vector3(position.x, position.y, position.z) * 1000.0, new Vector3(velocity.x, velocity.y, velocity.z) * 1000.0, new CelestialBody(399,Frame.ECLIPTIC_J2000, date), date,
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
        return base.Equals(other) && Line1 == other.Line1 && Line2 == other.Line2 && Line3 == other.Line3;
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
        return HashCode.Combine(base.GetHashCode(), Line1, Line2, Line3);
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