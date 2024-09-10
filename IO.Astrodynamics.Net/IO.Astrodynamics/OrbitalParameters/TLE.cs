// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// Represents a Two-Line Element (TLE) set of orbital parameters.
/// </summary>
public class TLE : KeplerianElements, IEquatable<TLE>
{
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
    /// Creates a new instance of the TLE class.
    /// </summary>
    /// <param name="line1">The first line of the TLE.</param>
    /// <param name="line2">The second line of the TLE.</param>
    /// <param name="line3">The third line of the TLE.</param>
    /// <returns>A new instance of the TLE class.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    public static TLE Create(string line1, string line2, string line3)
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(line3)) throw new ArgumentException("Value cannot be null or empty.", nameof(line3));
        return API.Instance.CreateTLE(line1, line2, line3);
    }

    /// <summary>
    /// Initializes a new instance of the TLE class.
    /// </summary>
    /// <param name="line1">The first line of the TLE.</param>
    /// <param name="line2">The second line of the TLE.</param>
    /// <param name="line3">The third line of the TLE.</param>
    /// <param name="balisticCoefficient">The ballistic coefficient.</param>
    /// <param name="dragTerm">The drag term.</param>
    /// <param name="secondDerivativeMeanMotion">The second derivative of the mean motion.</param>
    /// <param name="a">The semi-major axis.</param>
    /// <param name="e">The eccentricity.</param>
    /// <param name="i">The inclination.</param>
    /// <param name="o">The right ascension of the ascending node.</param>
    /// <param name="w">The argument of perigee.</param>
    /// <param name="m">The mean anomaly.</param>
    /// <param name="epoch">The epoch time.</param>
    /// <param name="frame">The reference frame.</param>
    /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the lines are null or empty.</exception>
    internal TLE(string line1, string line2, string line3, double balisticCoefficient, double dragTerm, double secondDerivativeMeanMotion, double a, double e, double i, double o,
        double w, double m, Time epoch, Frame frame) : base(a, e, i, o, w, m, new CelestialBody(PlanetsAndMoons.EARTH), epoch, frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(line3)) throw new ArgumentException("Value cannot be null or empty.", nameof(line3));
        Line1 = line1;
        Line2 = line2;
        Line3 = line3;
        BalisticCoefficient = balisticCoefficient;
        DragTerm = dragTerm;
        SecondDerivativeMeanMotion = secondDerivativeMeanMotion;
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
    /// Converts the TLE to a state vector at a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch time.</param>
    /// <returns>The state vector at the given epoch.</returns>
    public override StateVector ToStateVector(Time epoch)
    {
        return API.Instance.ConvertTleToStateVector(Line1, Line2, Line3, epoch).ToStateVector();
    }
    
    public override StateVector ToStateVector()
    {
        return API.Instance.ConvertTleToStateVector(Line1, Line2, Line3, Epoch).ToStateVector();
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