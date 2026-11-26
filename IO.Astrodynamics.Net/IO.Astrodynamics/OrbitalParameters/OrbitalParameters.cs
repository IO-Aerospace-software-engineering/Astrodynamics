using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.OrbitalParameters.TLE;

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// Represents the base class for orbital parameters and provides methods for calculating various orbital elements.
/// </summary>
public abstract class OrbitalParameters : IEquatable<OrbitalParameters>
{
    /// <summary>
    /// Observer
    /// </summary>
    /// <summary>
    /// Gets the observer associated with the orbital parameters.
    /// </summary>
    public ILocalizable Observer { get; }

    /// <summary>
    /// Gets the epoch time at which the orbital parameters are defined.
    /// </summary>
    public Time Epoch { get; protected set; }

    /// <summary>
    /// Gets the reference frame in which the orbital parameters are defined.
    /// </summary>
    public Frame Frame { get; }

    /// <summary>
    /// Gets the type of orbital elements (osculating or mean).
    /// </summary>
    public OrbitalElementsType ElementsType { get; }

    private static readonly MeanElementsConverter _sharedMeanElementsConverter = new();

    //Data used for caching
    private Vector3? _eccentricVector;
    private Vector3? _specificAngularMomentum;
    private double? _specificOrbitalEnergy;
    private Vector3? _ascendingNodeVector;
    private Vector3? _decendingNodeVector;
    private protected TimeSpan? _period;
    private protected double? _meanMotion;
    private protected StateVector _stateVector;
    private protected EquinoctialElements _equinoctial;
    private Vector3? _perigeevector;
    private Vector3? _apogeeVector;
    private double? _trueLongitude;
    private double? _meanLongitude;
    private bool? _isCircular;
    private bool? _isParabolic;
    private bool? _isHyperbolic;
    private protected KeplerianElements _keplerianElements;
    private Equatorial? _equatorial;
    private double? _perigeeVelocity;
    private double? _apogeeVelocity;
    private protected double? _ascendingNode;
    private protected double? _trueAnomaly;
    private double? _eccentricAnomaly;
    private protected double? _perigeeRadius;
    private protected double? _semiMajorAxis;
    private protected double? _eccentricity;
    private protected double? _inclination;
    private protected double? _periapsisArgument;
    private protected double? _meanAnomaly;


    /// <summary>
    /// Initializes a new instance of the <see cref="OrbitalParameters"/> class.
    /// </summary>
    /// <param name="observer">The observer associated with the orbital parameters.</param>
    /// <param name="epoch">The epoch time at which the orbital parameters are defined.</param>
    /// <param name="frame">The reference frame in which the orbital parameters are defined.</param>
    /// <param name="elementsType">The type of orbital elements (osculating or mean). Defaults to osculating.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the observer or frame is null.
    /// </exception>
    protected OrbitalParameters(ILocalizable observer, in Time epoch, Frame frame,
        OrbitalElementsType elementsType = OrbitalElementsType.Osculating)
    {
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        Epoch = epoch;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        ElementsType = elementsType;
    }


    /// <summary>
    /// Resets the cached values for various orbital parameters.
    /// </summary>
    internal void UpdateEpoch(in Time epoch)
    {
        ResetCache();
        Epoch = epoch;
    }

    protected virtual void ResetCache()
    {
        _eccentricVector = null;
        _specificAngularMomentum = null;
        _specificOrbitalEnergy = null;
        _ascendingNodeVector = null;
        _decendingNodeVector = null;
        _period = null;
        _meanMotion = null;
        _stateVector = null;
        _equinoctial = null;
        _perigeevector = null;
        _apogeeVector = null;
        _trueLongitude = null;
        _meanLongitude = null;
        _isCircular = null;
        _isParabolic = null;
        _isHyperbolic = null;
        _keplerianElements = null;
        _equatorial = null;
        _perigeeVelocity = null;
        _apogeeVelocity = null;
        _ascendingNode = null;
        _trueAnomaly = null;
        _eccentricAnomaly = null;
        _perigeeRadius = null;
        _semiMajorAxis = null;
        _eccentricity = null;
        _inclination = null;
        _periapsisArgument = null;
        _meanAnomaly = null;
    }

    #region Kepler elements

    /// <summary>
    /// Get the semi major axis
    /// </summary>
    /// <returns></returns>
    public abstract double SemiMajorAxis();

    /// <summary>
    /// Get eccentricity
    /// </summary>
    /// <returns></returns>
    public abstract double Eccentricity();

    /// <summary>
    /// Get inclination
    /// </summary>
    /// <returns></returns>
    public abstract double Inclination();

    /// <summary>
    /// Get ascending node angle
    /// </summary>
    /// <returns></returns>
    public abstract double AscendingNode();

    /// <summary>
    /// Get the argument of periapis
    /// </summary>
    /// <returns></returns>
    public abstract double ArgumentOfPeriapsis();

    /// <summary>
    /// Get the mean anomaly
    /// </summary>
    /// <returns></returns>
    public abstract double MeanAnomaly();

    #endregion

    /// <summary>
    /// Get eccentric vector
    /// </summary>
    /// <returns></returns>
    public Vector3 EccentricityVector()
    {
        if (_eccentricVector.HasValue)
        {
            return _eccentricVector.Value;
        }

        var sv = ToStateVector();
        _eccentricVector = (sv.Velocity.Cross(SpecificAngularMomentum()) / Observer.GM) - sv.Position.NormalizeOrZero();
        return _eccentricVector.Value;
    }

    /// <summary>
    /// Get vector to ascending node unitless
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public virtual Vector3 AscendingNodeVector()
    {
        if (_ascendingNodeVector.HasValue)
        {
            return _ascendingNodeVector.Value;
        }

        // Equatorial orbit, prograde or retrograde: the node is undefined and the x axis stands for it
        if (Inclination() == 0.0 || Inclination() == System.Math.PI)
        {
            return Vector3.VectorX;
        }

        var h = SpecificAngularMomentum();
        _ascendingNodeVector = new Vector3(-h.Y, h.X, 0.0);

        return _ascendingNodeVector.Value;
    }

    /// <summary>
    /// Get vector to descending node unitless
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public Vector3 DescendingNodeVector()
    {
        _decendingNodeVector ??= AscendingNodeVector().Inverse();
        return _decendingNodeVector.Value;
    }


    /// <summary>
    /// Get the specific angular momentum
    /// </summary>
    /// <returns></returns>
    public Vector3 SpecificAngularMomentum()
    {
        if (_specificAngularMomentum.HasValue)
        {
            return _specificAngularMomentum!.Value;
        }

        var sv = ToStateVector();
        _specificAngularMomentum = sv.Position.Cross(sv.Velocity);
        return _specificAngularMomentum.Value;
    }

    /// <summary>
    /// Get the specific orbital energy in MJ
    /// </summary>
    /// <returns></returns>
    public double SpecificOrbitalEnergy()
    {
        if (_specificOrbitalEnergy.HasValue)
        {
            return _specificOrbitalEnergy!.Value;
        }

        var sv = ToStateVector();
        _specificOrbitalEnergy = System.Math.Pow(sv.Velocity.Magnitude(), 2.0) / 2.0 - (Observer.GM / sv.Position.Magnitude());
        return _specificOrbitalEnergy.Value;
    }

    /// <summary>
    /// The true anomaly the conic's state and equinoctial elements are computed from: <see cref="TrueAnomaly()"/>, unless a
    /// derived type keeps the angle it was given (<see cref="KeplerianElements.FromTrueAnomaly"/>: ν as given, whose
    /// reduction to [0, 2π) loses up to half an ulp of 2π, about 4.4e-16 rad, which the state near an apsis or an asymptote
    /// amplifies; #668, R23).
    /// </summary>
    private protected virtual double ConicTrueAnomaly() => TrueAnomaly();

    /// <summary>
    /// Get the true anomaly
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the true anomaly is computed from the mean anomaly and the mean anomaly or the eccentricity is not
    /// finite (#453), and when the eccentricity is not finite, which no conic has (#476).
    /// </exception>
    public virtual double TrueAnomaly()
    {
        if (_trueAnomaly.HasValue)
        {
            return _trueAnomaly.Value;
        }

        if (IsElliptical())
        {
            _trueAnomaly = EllipticalTrueAnomaly() % Constants.TwoPi;
        }
        else if (IsParabolic())
        {
            _trueAnomaly = ParabolicTrueAnomaly() % Constants.TwoPi;
        }
        else if (IsHyperbolic())
        {
            _trueAnomaly = HyperbolicTrueAnomaly() % Constants.TwoPi;
        }
        else
        {
            // Only a non-finite eccentricity takes no branch: no conic has it (#476; the message of an empty Nullable before)
            throw new InvalidOperationException(
                $"No conic has the eccentricity e = {Eccentricity():R}: the true anomaly is undefined.");
        }

        if (_trueAnomaly < 0)
        {
            _trueAnomaly += Constants.TwoPi;
        }

        return _trueAnomaly.Value;
    }

    /// <summary>
    /// Computes the true anomaly for a parabolic orbit.
    /// </summary>
    /// <returns>The true anomaly in radians.</returns>
    private double ParabolicTrueAnomaly()
    {
        return 2 * System.Math.Atan(EccentricAnomaly());
    }

    /// <summary>
    /// Computes the true anomaly for a hyperbolic orbit.
    /// </summary>
    /// <returns>The true anomaly in radians.</returns>
    private double HyperbolicTrueAnomaly()
    {
        double e = Eccentricity();
        double H = EccentricAnomaly();
        double sqrtTerm = System.Math.Sqrt((e + 1) / (e - 1));
        double tanNuOver2 = sqrtTerm * System.Math.Tanh(H / 2);
        return 2 * System.Math.Atan(tanNuOver2);
    }

    /// <summary>
    /// Computes the true anomaly for an elliptical orbit.
    /// </summary>
    /// <returns>The true anomaly in radians.</returns>
    private double EllipticalTrueAnomaly()
    {
        double e = Eccentricity();
        double E = EccentricAnomaly();
        double sqrtTerm = System.Math.Sqrt((1 + e) / (1 - e));
        double tanNuOver2 = sqrtTerm * System.Math.Tan(E / 2);
        return 2 * System.Math.Atan(tanNuOver2);
    }

    /// <summary>
    /// Return true anomaly at given epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the mean anomaly at the epoch or the eccentricity is not finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public double TrueAnomaly(Time epoch)
    {
        return AtEpoch(epoch).TrueAnomaly();
    }

    /// <summary>
    /// Get the eccentric anomaly
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// On an ellipse and a hyperbola, the root of Kepler's equation for the mean anomaly, to its rounding floor (#453).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the mean anomaly or the eccentricity is not finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A <see cref="StateVector"/> at the centre (r = 0), whose eccentricity
    /// vector is zero, or a non-finite one has no orbit plane to count from (#687).</exception>
    public double EccentricAnomaly()
    {
        if (_eccentricAnomaly.HasValue)
        {
            return _eccentricAnomaly.Value;
        }

        if (IsElliptical())
        {
            _eccentricAnomaly = EllipticAnomaly();
        }
        else if (IsParabolic())
        {
            _eccentricAnomaly = ParabolicAnomaly();
        }
        else
        {
            _eccentricAnomaly = HyperbolicAnomaly();
        }

        return _eccentricAnomaly.Value;
    }

    /// <summary>
    /// Compute eccentric anomaly from true anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    public double EccentricAnomaly(double trueAnomaly)
    {
        if (IsElliptical())
        {
            return EllipticAnomaly(trueAnomaly);
        }

        if (IsParabolic())
        {
            return ParabolicAnomaly(trueAnomaly);
        }

        return HyperbolicAnomaly(trueAnomaly);
    }

    /// <summary>
    /// Compute parabolic anomaly
    /// </summary>
    /// <returns></returns>
    private double ParabolicAnomaly()
    {
        // Barker's equation M = D + D³/3, solved in closed form: with D = 2 sinh θ, D + D³/3 = (2/3) sinh 3θ, so
        // θ = asinh(3M/2) / 3 (#368). The cubic has a single real root.
        return BarkerAnomaly(MeanAnomaly());
    }

    /// <summary>
    /// Compute hyperbolic anomaly
    /// </summary>
    /// <returns></returns>
    private double HyperbolicAnomaly()
    {
        // Monotone Newton to the rounding floor, bounded (#453); of the sign of M, negative before the periapsis (#368)
        return KeplerEquation.HyperbolicAnomaly(MeanAnomaly(), Eccentricity());
    }

    /// <summary>
    /// Compute eccentric anomaly
    /// </summary>
    /// <returns></returns>
    private double EllipticAnomaly()
    {
        // Monotone Newton to the rounding floor, bounded (#453); the root within e of M, for any M
        return KeplerEquation.EllipticAnomaly(MeanAnomaly(), Eccentricity());
    }

    /// <summary>
    /// Compute eccentric anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    private double EllipticAnomaly(double trueAnomaly)
    {
        var e = Eccentricity();
        return EllipticAnomaly(trueAnomaly, e);
    }

    /// <summary>
    /// Computes the eccentric anomaly for an elliptical orbit given the true anomaly and eccentricity.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly in radians.</param>
    /// <param name="e">The eccentricity of the orbit.</param>
    /// <returns>The eccentric anomaly in radians.</returns>
    internal static double EllipticAnomaly(double trueAnomaly, double e)
    {
        double sqrtTerm = System.Math.Sqrt((1 - e) / (1 + e));
        double tanE2 = System.Math.Tan(trueAnomaly / 2) * sqrtTerm;
        return 2 * System.Math.Atan(tanE2);
    }

    /// <summary>
    /// Compute hyperbolic anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    private double HyperbolicAnomaly(double trueAnomaly)
    {
        var e = Eccentricity();
        double tanNu2 = System.Math.Tan(trueAnomaly / 2);
        double sqrtTerm = System.Math.Sqrt((e - 1) / (e + 1));
        double tanhH2 = sqrtTerm * tanNu2;
        return 2 * System.Math.Atanh(tanhH2);
    }

    /// <summary>
    /// Computes the parabolic anomaly given the true anomaly.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly in radians.</param>
    /// <returns>The parabolic anomaly in radians.</returns>
    private double ParabolicAnomaly(double trueAnomaly)
    {
        // Barker's parabolic anomaly D = tan(ν/2), the inverse of ParabolicTrueAnomaly (#368)
        return System.Math.Tan(trueAnomaly * 0.5);
    }

    /// <summary>
    /// Compute mean anomaly from true anomaly
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly, rad.</param>
    /// <returns>The mean anomaly, rad: Kepler's elliptic or hyperbolic equation of the anomaly of
    /// <see cref="EccentricAnomaly(double)"/>, and Barker's D + D³/3, D = tan(ν/2), in the near-parabolic band
    /// |e − 1| &lt; 1e-6 (<see cref="IsParabolic"/>), whatever the side of e = 1.</returns>
    /// <remarks>Before 11.0.0 the band put Barker's D into Kepler's elliptic or hyperbolic equation, chosen by e alone, so
    /// that only e = 1 exactly gave Barker's mean anomaly: <see cref="ToStateVector(double)"/>, <see cref="TimeToRadius"/>
    /// and the paths of <see cref="EquinoctialElements"/> were up to the size of the orbit off (#478).</remarks>
    public double MeanAnomaly(double trueAnomaly)
    {
        double anomaly = EccentricAnomaly(trueAnomaly);

        // EccentricAnomaly gives Barker's D in the band, so the mean anomaly there is Barker's (#478); at e = 1 exactly the
        // static form below gives the same, bit for bit
        if (IsParabolic())
        {
            return anomaly + System.Math.Pow(anomaly, 3) / 3.0;
        }

        return TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity(), anomaly);
    }

    /// <summary>
    /// Converts the true anomaly to the mean anomaly.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly in radians.</param>
    /// <param name="eccentricity">The eccentricity of the orbit.</param>
    /// <param name="eccentricAnomaly">The eccentric anomaly in radians: elliptic for e &lt; 1, hyperbolic for e &gt; 1, Barker's
    /// D = tan(ν/2) for e = 1. It must match <paramref name="eccentricity"/>, whose value alone chooses the equation; within
    /// 1e-6 of e = 1, where <see cref="EccentricAnomaly(double)"/> gives Barker's D on either side, use
    /// <see cref="MeanAnomaly(double)"/> (#478).</param>
    /// <returns>The mean anomaly in radians: E − e sin E evaluated as (1 − e) E + e (E − sin E) for 0.5 ≤ e &lt; 1, and
    /// e sinh H − H as (e − 1) H + e (sinh H − H) for 1 &lt; e ≤ 2, which do not cancel near the periapsis at e → 1 (#484;
    /// before 11.0.0, up to 1.9e-10 of M off at e = 0.999999); the former forms elsewhere, bit for bit.</returns>
    public static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity, double eccentricAnomaly)
    {
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants.TwoPi;
        }

        // On an open orbit the mean anomaly is not an angle: it grows without bound, negative before the periapsis, so it
        // is not reduced modulo 2π (#368)
        if (eccentricity < 1)
        {
            if (System.Math.Abs(trueAnomaly - Constants.PI) < 1e-09)
            {
                return Constants.PI;
            }

            // Kepler's equation in the forms that do not cancel near the periapsis at e → 1 (#484)
            var res = KeplerEquation.EllipticMeanAnomaly(eccentricAnomaly, eccentricity) % Constants.TwoPi;
            return res < 0 ? res + Constants.TwoPi : res;
        }
        else if (eccentricity > 1)
        {
            return KeplerEquation.HyperbolicMeanAnomaly(eccentricAnomaly, eccentricity);
        }
        else
        {
            // Barker's equation
            return eccentricAnomaly + System.Math.Pow(eccentricAnomaly, 3) / 3.0;
        }
    }

    /// <summary>
    /// Get orbital period
    /// </summary>
    /// <returns>The period of an ellipse, 2π √(a³/μ), to the nearest tick, ties to even (#637; truncated to the tick below
    /// before 11.0.0), or the period given to the elements.</returns>
    /// <exception cref="InvalidOperationException">The semi-major axis is not finite and above 0: a parabola (+Infinity) or
    /// a hyperbola (a &lt; 0) has no period (#663; before 11.0.0, the NaN <see cref="ArgumentException"/> or the
    /// <see cref="OverflowException"/> of <see cref="TimeSpan"/>).</exception>
    /// <exception cref="OverflowException">The period of an ellipse is 922 337 203 685 s or more, about
    /// <see cref="TimeSpan.MaxValue"/> (29 227 years; from 11.0.0 also the last 0.48 s below it, #637): a within 1e-6 of e = 1,
    /// for instance (#455).</exception>
    public TimeSpan Period()
    {
        double a = SemiMajorAxis();

        // Only an ellipse has a period: tested on a, not on e, so that the conic's finite a of the band keeps it (#663);
        // before the cache, so that a period given to the elements of an open orbit (KeplerianElements' period) is not
        // returned either
        ThrowIfOpen("period");

        _period ??= Time.SecondsToSpan(Constants.TwoPi * System.Math.Sqrt((a * a * a) / Observer.GM));
        return _period.Value;
    }

    /// <summary>
    /// Get orbital mean motion
    /// </summary>
    /// <returns>The mean motion of the conic, √(μ/|a|³), rad/s; for a parabola, whose semi-major axis is +Infinity,
    /// Barker's √(μ/(2q³)), q the perigee radius (#368).</returns>
    /// <remarks>Within 1e-6 of e = 1 (<see cref="IsParabolic"/>) the conic of a finite semi-major axis has its own mean
    /// motion, √(μ/|a|³), which this returns since 11.0.0 (#459, option 1, decided by S. Guillet, 2026-11-17; before,
    /// Barker's, 2e9 times larger at |e − 1| = 5e-7). The two-body motion of the band (<see cref="AtEpoch"/>,
    /// <see cref="EpochAtMeanAnomaly"/>, <see cref="TimeToRadius"/>) is the conic's in universal variables, which no mean
    /// motion describes, and at e = 1 Barker's (#753; before 11.0.0, Barker's anomaly at Barker's rate over the whole
    /// band).</remarks>
    public double MeanMotion(double orbitingBodyMass = 0.0)
    {
        if (_meanMotion.HasValue)
        {
            return _meanMotion.Value;
        }

        if (IsElliptical())
        {
            _meanMotion = System.Math.Sqrt(Constants.GravitationalConstant * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(SemiMajorAxis(), 3));
        }
        else if (IsParabolic())
        {
            double a = SemiMajorAxis();

            // A parabola: so that Barker's D + D³/3 = n (t - T), t - T = √(2q³/μ) (D + D³/3), q the periapsis radius
            // (#368). The conic of a finite a in the band, either side of e = 1: √(μ/|a|³) (#459)
            _meanMotion = double.IsPositiveInfinity(a)
                ? BarkerMeanMotion(orbitingBodyMass)
                : System.Math.Sqrt(Constants.GravitationalConstant * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(System.Math.Abs(a), 3));
        }
        else
        {
            _meanMotion = System.Math.Sqrt(Constants.GravitationalConstant * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(-SemiMajorAxis(), 3));
        }

        return _meanMotion.Value;
    }

    /// <summary>
    /// Barker's mean motion √(μ/(2q³)), q the perigee radius: the rate of Barker's mean anomaly D + D³/3 (#368).
    /// </summary>
    private double BarkerMeanMotion(double orbitingBodyMass = 0.0) =>
        System.Math.Sqrt(Constants.GravitationalConstant * (Observer.Mass + orbitingBodyMass) / (2.0 * System.Math.Pow(PerigeeRadius(), 3)));

    /// <summary>
    /// The rate of <see cref="MeanAnomaly()"/> in the two-body motion: Barker's √(μ/(2q³)) at e = 1 exactly (a parabola,
    /// whose mean anomaly is Barker's), <see cref="MeanMotion"/> outside the band |e − 1| &lt; 1e-6, cached mean motion of
    /// mean elements included. Elsewhere in the band the motion is that of the conic in universal variables
    /// (<see cref="MotionAfter"/>, #753), which no rate describes.
    /// </summary>
    private double MeanAnomalyRate() => IsParabolic() ? BarkerMeanMotion() : MeanMotion();

    /// <summary>
    /// Whether the two-body motion takes the universal variables of the conic (#753): within 1e-6 of e = 1 but at e = 1
    /// exactly, where Barker's law is the conic's motion and is kept, bit for bit.
    /// </summary>
    private bool MovesInUniversalVariables() => IsParabolic() && Eccentricity() != 1.0;

    /// <summary>
    /// The two-body motion of the mean anomaly over <paramref name="elapsed"/> seconds of the dynamical time of the
    /// observer: M + n Δt outside the band |e − 1| &lt; 1e-6 and at e = 1 (Barker's rate), bit for bit as before 11.0.0;
    /// elsewhere in the band the exact motion of the conic of e and q, in universal variables from the periapsis
    /// (<see cref="UniversalKepler"/>, #753, decided by S. Guillet, 2026-11-18), its true anomaly at the end with Barker's
    /// mean anomaly D + D³/3 of it, D = tan(ν/2), the anomalies of the band (#459).
    /// </summary>
    /// <remarks>Before 11.0.0 Barker's anomaly moved at Barker's rate √(μ/(2q³)) over the whole band: 0.088 to 0.16 m from
    /// the exact motion of the conic at its edges after 600 s, 0.54 m from the periapsis.</remarks>
    private (double MeanAnomaly, double? TrueAnomaly) MotionAfter(double meanAnomaly, double elapsed)
    {
        if (!MovesInUniversalVariables() || elapsed == 0.0)
        {
            return (meanAnomaly + MeanAnomalyRate() * elapsed, null);
        }

        double e = Eccentricity();
        double q = PerigeeRadius();
        double gm = Observer.GM;
        double time = TimeFromPeriapsis(EccentricAnomaly()) + elapsed;
        double universal = UniversalKepler.Solve(time, q, e, gm);
        var (barker, trueAnomaly) = UniversalKepler.Anomalies(universal, q, e);
        trueAnomaly %= Constants.TwoPi;
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants.TwoPi;
        }

        return (barker + System.Math.Pow(barker, 3) / 3.0, trueAnomaly);
    }

    /// <summary>The time of the conic of the band from its periapsis to Barker's anomaly D, s (#753).</summary>
    private double TimeFromPeriapsis(double barkerAnomaly)
    {
        double e = Eccentricity();
        double q = PerigeeRadius();
        return UniversalKepler.TimeFromPeriapsis(UniversalKepler.FromBarkerAnomaly(barkerAnomaly, q, e), q, e, Observer.GM);
    }

    /// <summary>Barker's anomaly D of Barker's mean anomaly D + D³/3, in closed form (#368).</summary>
    private static double BarkerAnomaly(double meanAnomaly) =>
        2.0 * System.Math.Sinh(System.Math.Asinh(1.5 * meanAnomaly) / 3.0);


    /// <summary>
    /// Get the state vector
    /// </summary>
    /// <returns>The perifocal state of the conic, r = p / (1 + e cos ν) and v = √(μ/p)(−sin ν, e + cos ν), turned by
    /// R3(Ω) R1(i) R3(ω).</returns>
    /// <remarks>Near e = 1 the products are taken where they do not cancel (#472, decided by S. Guillet, 2026-11-18):
    /// p = (a (1 − e))(1 + e) for 0.5 ≤ e ≤ 2 (q (1 + e) in the band |e − 1| &lt; 1e-6), and 1 + e cos ν and e + cos ν in
    /// their half-angle forms (1 − e) + 2e cos²(ν/2) and 2 cos²(ν/2) − (1 − e) for 0.5 ≤ e ≤ 1.2, so that the state is
    /// within about 1e-15 of the conic's near the apoapsis and the asymptote; before 11.0.0, up to 8.1e-11 off (155 m at
    /// the apoapsis of e = 0.999999, q = 7000 km). Below e = 0.5 and above e = 2 the state is unchanged, bit for bit.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to convert mean elements directly to a state vector.
    /// Mean elements require a specific propagator (e.g., SGP4/SDP4) for accurate position calculations.
    /// For TLE data, use <see cref="TLE.Tle.ToOsculating()"/> or <see cref="TLE.Tle.ToOsculating(Time)"/> instead.
    /// Also thrown when the true anomaly is computed from the mean anomaly and the mean anomaly or the eccentricity is
    /// not finite (#453).
    /// </exception>
    public virtual StateVector ToStateVector()
    {
        ThrowIfMeanElements();

        if (_stateVector is null)
        {
            var e = Eccentricity();

            // In the band |e − 1| < 1e-6 the conic's p = q (1 + e), 2q at e = 1 (#459; before, 2q on both sides, which put
            // the periapsis up to 5e-7 of q away and moved a round trip by |r| |e − 1| / (1 + e), metres to 4.2e7 m);
            // outside it a (1 − e)(1 + e) near e = 1 (#472)
            var p = IsParabolic() ? PerigeeRadius() * (1.0 + e) : ConicSemiLatusRectum(SemiMajorAxis(), e);

            var v = ConicTrueAnomaly();
            double cosNu = System.Math.Cos(v);
            double denominator, velocityFactor;
            if (e is >= HalfAngleLowestEccentricity and <= HalfAngleHighestEccentricity)
            {
                // 1 + e cos ν = (1 − e) + 2e cos²(ν/2) and e + cos ν = 2 cos²(ν/2) − (1 − e), 1 − e exact (Sterbenz): near
                // the apoapsis and the asymptote 1 + e cos ν and e + cos ν cancel, the rounding of e cos ν and cos ν, about
                // ε, divided by them; the half-angle forms carry about ε (1 − e) there (#472, choice 2, decided by
                // S. Guillet, 2026-11-18; up to 4.6e-11 of the state at e = 0.999999 before, 8.1e-11 at e = 1.000002).
                // Beyond e = 1.2 they lose near the asymptote (2.6e-12 against 2.5e-13 at e = 1.4), so they stop there
                double halfCos = System.Math.Cos(0.5 * v);
                double onePlusCos = 2.0 * halfCos * halfCos;
                double oneMinusE = 1.0 - e;
                denominator = oneMinusE + e * onePlusCos;
                velocityFactor = onePlusCos - oneMinusE;
            }
            else
            {
                denominator = 1 + e * cosNu;
                velocityFactor = e + cosNu;
            }

            var r0 = p / denominator;
            var x = r0 * cosNu;
            var y = r0 * System.Math.Sin(v);
            var dotX = -System.Math.Sqrt(Observer.GM / p) * System.Math.Sin(v);
            var dotY = System.Math.Sqrt(Observer.GM / p) * velocityFactor;
            Matrix r3 = Matrix.CreateRotationMatrixZ(AscendingNode());
            Matrix r1 = Matrix.CreateRotationMatrixX(Inclination());
            Matrix r3W = Matrix.CreateRotationMatrixZ(ArgumentOfPeriapsis());
            Matrix r = r3 * r1 * r3W;
            double[] pos = [x, y, 0.0];
            double[] vel = [dotX, dotY, 0.0];
            double[] finalPos = r * pos;
            double[] finalV = r * vel;

            _stateVector = new StateVector(new Vector3(finalPos[0], finalPos[1], finalPos[2]), new Vector3(finalV[0], finalV[1], finalV[2]), Observer, Epoch, Frame);
        }

        return _stateVector;
    }

    // The band of the half-angle forms of 1 + e cos ν and e + cos ν in ToStateVector (#472, choice 2)
    private const double HalfAngleLowestEccentricity = 0.5;
    private const double HalfAngleHighestEccentricity = 1.2;

    /// <summary>
    /// The semi-latus rectum a (1 − e²) of the conic of a semi-major axis and an eccentricity outside the near-parabolic
    /// band: (a (1 − e))(1 + e) for 0.5 ≤ e ≤ 2, where 1 − e is exact (Sterbenz's lemma) and nothing cancels, and
    /// a (1 − e·e) elsewhere, the more accurate product there, bit for bit as before 11.0.0 (#472, choice 1, decided by
    /// S. Guillet, 2026-11-18). Near e = 1, 1 − e·e kept the rounding of e·e, about ε, divided by |1 − e²|: 1.1e-11
    /// relative at e = 0.999999, 2.2e-11 at 1.000002; the rewritten product is within 3.2e-16.
    /// </summary>
    internal static double ConicSemiLatusRectum(double semiMajorAxis, double e) =>
        e is >= 0.5 and <= 2.0 ? semiMajorAxis * (1 - e) * (1 + e) : semiMajorAxis * (1 - e * e);

    /// <summary>
    /// The check of <see cref="ToStateVector()"/> and of its overrides: mean elements have no state vector of their
    /// own.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the elements are mean elements.</exception>
    private protected void ThrowIfMeanElements()
    {
        if (ElementsType == OrbitalElementsType.Mean)
        {
            throw new InvalidOperationException(
                "Cannot convert mean elements directly to a state vector. " +
                "Mean elements require a specific propagator (e.g., SGP4/SDP4) for accurate position calculations. " +
                "For TLE data, use Tle.ToOsculating() instead.");
        }
    }

    /// <summary>
    /// Converts the orbital parameters to a state vector at a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch time at which to compute the state vector.</param>
    /// <returns>The state vector at the given epoch.</returns>
    /// <remarks>
    /// The state is the two-body motion of <see cref="AtEpoch"/>, without a covariance. A <see cref="StateVector"/>
    /// returns itself, with its covariance, at the instant of its own epoch (#551).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to convert mean elements directly to a state vector.
    /// Mean elements require a specific propagator (e.g., SGP4/SDP4) for accurate position calculations.
    /// Also thrown when the mean anomaly at the epoch or the eccentricity is not finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane and no two-body motion to another instant (#687).</exception>
    public virtual StateVector ToStateVector(Time epoch)
    {
        if (ElementsType == OrbitalElementsType.Mean)
        {
            throw new InvalidOperationException(
                "Cannot convert mean elements directly to a state vector. " +
                "Mean elements require a specific propagator (e.g., SGP4/SDP4) for accurate position calculations. " +
                "For TLE data, use Tle.ToOsculating(epoch) instead.");
        }

        return AtEpoch(epoch).ToStateVector();
    }

    /// <summary>
    /// Converts the orbital parameters to a state vector at a given true anomaly.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly at which to compute the state vector.</param>
    /// <returns>The state vector at the given true anomaly.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the mean anomaly at that epoch or the eccentricity is not finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A <see cref="StateVector"/> at the centre (r = 0), whose eccentricity
    /// vector is zero, or a non-finite one has no orbit plane to count from (#687).</exception>
    public StateVector ToStateVector(double trueAnomaly)
    {
        return ToStateVector(EpochAtMeanAnomaly(MeanAnomaly(trueAnomaly)));
    }

    /// <summary>
    /// Computes the epoch time at a given mean anomaly: on an ellipse the next passage at that mean anomaly modulo 2π,
    /// at or after <see cref="Epoch"/>, whatever the turn of the given anomaly and of <see cref="MeanAnomaly()"/>; on an
    /// open orbit the single passage, before <see cref="Epoch"/> when the mean anomaly is behind.
    /// </summary>
    /// <param name="meanAnomaly">The mean anomaly at which to compute the epoch time, rad.</param>
    /// <returns>The epoch time at the given mean anomaly, in the frame of <see cref="Epoch"/>.</returns>
    /// <remarks>The time to the mean anomaly is counted in the dynamical time of the observer, as
    /// <see cref="AtEpoch"/> counts it: TT seconds about the Earth, TDB seconds about any other body (#501, #508), so that
    /// <c>AtEpoch(EpochAtMeanAnomaly(m))</c> is at <c>m</c> to the tick of the epoch. About the Earth from a TDB epoch,
    /// the epoch is not <c>Epoch + TimeSpan.FromSeconds(Δt)</c>, which adds TDB seconds. Before 11.0.0 the difference
    /// of the anomalies took 2π at most once: from a <see cref="StateVector"/>, whose mean anomaly is in (−π, π), the
    /// passage came one period late, and at e = 0.999999, where <see cref="KeplerianElements"/> keeps its mean anomaly
    /// unreduced, it could be in the past (#474).</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The passage lies beyond the epochs a <see cref="Time"/> holds.</exception>
    /// <exception cref="InvalidOperationException">A <see cref="StateVector"/> at the centre (r = 0), whose eccentricity
    /// vector is zero, or a non-finite one has no orbit plane to count from (#687).</exception>
    public Time EpochAtMeanAnomaly(double meanAnomaly)
    {
        // In the band but at e = 1, the conic's time between Barker's anomalies of the two mean anomalies (#753)
        if (MovesInUniversalVariables())
        {
            return Propagator.PropagationTimeArgument.EpochAfter(Observer, Epoch,
                TimeFromPeriapsis(BarkerAnomaly(meanAnomaly)) - TimeFromPeriapsis(EccentricAnomaly()));
        }

        var res = meanAnomaly - MeanAnomaly();

        // The next passage on an ellipse, whatever the turn of either anomaly; an open orbit passes once (#474)
        if (IsElliptical())
        {
            res = SpecialFunctions.NormalizeAngle(res);
        }

        return Propagator.PropagationTimeArgument.EpochAfter(Observer, Epoch, res / MeanAnomalyRate());
    }

    /// <summary>
    /// Convert to equinoctial
    /// </summary>
    /// <returns>The equinoctial elements, of semi-latus rectum P = a(1 − e²), evaluated as (a (1 − e))(1 + e) for
    /// 0.5 ≤ e ≤ 2 (#472; before 11.0.0, a (1 − e·e), which lost up to 1.1e-11 relative at e = 0.999999); in the
    /// near-parabolic band |e − 1| &lt; 1e-6 (<see cref="IsParabolic"/>), P = q (1 + e) with q the
    /// <see cref="PerigeeRadius"/>, 2q for a parabola and h²/μ for a <see cref="StateVector"/> (#462; before 11.0.0, NaN at
    /// e = 1 and ±∞ elsewhere in the band, its sign set by rounding).</returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane and no elements (#687).</exception>
    public virtual EquinoctialElements ToEquinoctial()
    {
        if (_equinoctial is null)
        {
            double e = Eccentricity();
            double o = AscendingNode();
            double w = ArgumentOfPeriapsis();
            double i = Inclination();
            double v = ConicTrueAnomaly();

            // In the band |e − 1| < 1e-6 (IsParabolic) a(1 − e²) is ∞ × 0 or ±∞ (#462): the conic's p = q (1 + e), 2q at
            // e = 1 and h²/μ for a state vector, read without the semi-major axis. Outside the band, the p of ToStateVector
            // (#472)
            double p = IsParabolic() ? PerigeeRadius() * (1.0 + e) : ConicSemiLatusRectum(SemiMajorAxis(), e);
            double f = e * System.Math.Cos(o + w);
            double g = e * System.Math.Sin(o + w);
            double h = System.Math.Tan(i * 0.5) * System.Math.Cos(o);
            double k = System.Math.Tan(i * 0.5) * System.Math.Sin(o);
            double l0 = o + w + v;

            _equinoctial = new EquinoctialElements(p, f, g, h, k, l0, Observer, Epoch, Frame, PerigeeRadius(), ElementsType, true);
        }

        return _equinoctial;
    }

    /// <summary>
    /// Get perigee vector: the position of the periapsis relative to the observer, a(1 − e) P̂, with P̂ the direction of
    /// the periapsis (#686); within 1e-6 of e = 1 (<see cref="IsParabolic"/>), q P̂, q the <see cref="PerigeeRadius"/> of the
    /// band (#737: before 11.0.0, NaN for a parabola, a = +Infinity).
    /// </summary>
    /// <returns>The periapsis position, in the frame of the orbital parameters.</returns>
    /// <remarks>
    /// <para>
    /// P̂ is the perifocal axis of the elements, (cos Ω cos ω − sin Ω sin ω cos i, sin Ω cos ω + cos Ω sin ω cos i,
    /// sin ω sin i), the conic at ν = 0 (D. A. Vallado, <i>Fundamentals of Astrodynamics and Applications</i>, 4th ed.,
    /// 2013, algorithm 10 COE2RV; confirmed by S. Guillet, 2026-11-15), defined at every eccentricity: a circular orbit has its
    /// periapsis at ω from the node, where its true anomaly counts from. A <see cref="StateVector"/> takes the part of its
    /// eccentricity vector in its orbit plane, e − (e · ĥ) ĥ, normalized, and its unit node vector when that part is zero
    /// (x̂ for an equatorial orbit).
    /// </para>
    /// <para>
    /// Before 11.0.0, P̂ was the eccentricity vector recomputed from the state, normalized leniently, and x̂ for an
    /// eccentricity of exactly 0: near the rounding level of the eccentricity (about 1e-16) it cancelled to the zero
    /// vector, so <see cref="PerigeeVelocity"/> was infinite, or to a direction off the orbit plane (#686).
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.Tle"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public Vector3 PerigeeVector()
    {
        // In the band |e − 1| < 1e-6 the perigee radius q of the band, which a (1 − e) is not at e = 1 (∞ × 0, NaN) and
        // carries 1e-16/|1 − e| of rounding elsewhere in it (#737); outside the band a (1 − e) P̂, bit for bit
        _perigeevector ??= IsParabolic()
            ? PeriapsisDirection() * PerigeeRadius()
            : PeriapsisDirection() * SemiMajorAxis() * (1.0 - Eccentricity());
        return _perigeevector.Value;
    }

    /// <summary>
    /// The unit vector from the observer to the periapsis: the perifocal axis of the elements, the first column of
    /// R3(−Ω) R1(−i) R3(−ω), defined at every eccentricity, e = 0 included (#686).
    /// </summary>
    /// <exception cref="InvalidOperationException">The elements are mean elements, which have no position, as
    /// <see cref="ToStateVector()"/> and <see cref="EccentricityVector"/> throw (review of #686).</exception>
    internal virtual Vector3 PeriapsisDirection()
    {
        ThrowIfMeanElements();

        double o = AscendingNode();
        double w = ArgumentOfPeriapsis();
        double i = Inclination();
        double cosO = System.Math.Cos(o), sinO = System.Math.Sin(o);
        double cosW = System.Math.Cos(w), sinW = System.Math.Sin(w);
        double cosI = System.Math.Cos(i), sinI = System.Math.Sin(i);
        return new Vector3(cosO * cosW - sinO * sinW * cosI, sinO * cosW + cosO * sinW * cosI, sinW * sinI);
    }

    /// <summary>
    /// Get perigee radius
    /// </summary>
    /// <returns></returns>
    public double PerigeeRadius()
    {
        if (_perigeeRadius.HasValue)
        {
            return _perigeeRadius.Value;
        }

        if (IsParabolic())
        {
            _perigeeRadius = SemiLatusRectum() / (1.0 + Eccentricity());
        }

        _perigeeRadius ??= SemiMajorAxis() * (1.0 - Eccentricity());
        return _perigeeRadius.Value;
    }

    /// <summary>
    /// Get apogee vector: the position of the apoapsis relative to the observer, −a(1 + e) P̂, with P̂ the direction of
    /// the periapsis of <see cref="PerigeeVector"/> (#686).
    /// </summary>
    /// <returns>The apoapsis position, in the frame of the orbital parameters.</returns>
    /// <exception cref="InvalidOperationException">The orbit is not an ellipse: a parabola or a hyperbola, whose
    /// semi-major axis is not finite and above 0, has no apoapsis, as it has no period (#737; before 11.0.0, a point of the
    /// other branch of a hyperbola, ±Infinity for a parabola).</exception>
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.Tle"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public Vector3 ApogeeVector()
    {
        if (!_apogeeVector.HasValue)
        {
            // The perifocal axis first, so that mean elements and a state without an orbit plane report that (#686, #687)
            var direction = PeriapsisDirection();
            ThrowIfOpen("apoapsis");
            _apogeeVector = direction.Inverse() * SemiMajorAxis() * (1.0 + Eccentricity());
        }

        return _apogeeVector.Value;
    }

    /// <summary>
    /// Whether the orbit is an ellipse, whose semi-major axis is finite and above 0: only it has a period and an apoapsis
    /// (#663, #737). Tested on a, not on e, so that the conic's finite a of the band keeps them.
    /// </summary>
    private bool IsClosed()
    {
        double a = SemiMajorAxis();
        return double.IsFinite(a) && a > 0.0;
    }

    /// <exception cref="InvalidOperationException">The orbit is not an ellipse (<see cref="IsClosed"/>).</exception>
    private void ThrowIfOpen(string what)
    {
        if (!IsClosed())
        {
            throw new InvalidOperationException(
                $"An open orbit has no {what}: e = {Eccentricity():R}, a = {SemiMajorAxis():R} m (a {what} needs a finite semi-major axis above 0, an ellipse).");
        }
    }

    /// <summary>
    /// Get orbital parameters at epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <returns></returns>
    /// <remarks>
    /// The two-body motion counts the dynamical time of the observer, as the numerical propagation does: TT seconds
    /// about the Earth, TDB seconds about any other body, whatever the frames of <paramref name="epoch"/> and of
    /// <see cref="Epoch"/>, the leap seconds between two UTC epochs included; none at the same instant
    /// (#501, #508). A <see cref="TimeSpan"/> added to a TDB epoch is a span of TDB, so about the Earth
    /// <c>AtEpoch(Epoch + Period())</c> from a TDB epoch is off the start by the change of TDB − TT over the period
    /// (1.2e-6 s of motion over an hour at most); from a TT epoch it returns there, and from a UTC epoch away from a leap
    /// second: <see cref="Time"/> + <see cref="TimeSpan"/> adds calendar seconds to a UTC time, so across a leap second
    /// <c>Epoch + Period()</c> is a period and one second of TT later.
    /// <para>
    /// Within 1e-6 of e = 1 (<see cref="IsParabolic"/>) but at e = 1 exactly the motion is the conic's of e and q in
    /// universal variables from the periapsis (Stumpff functions, the form of CSPICE's <c>prop2b_c</c>; #753, decided by
    /// S. Guillet, 2026-11-18): the elements at <paramref name="epoch"/> carry the true anomaly of that motion and Barker's
    /// mean anomaly of it, the anomalies of the band. Against the exact conic at 60 digits, within 4.2e-15 of |r| over
    /// |e − 1| from 1e-12 to 9.9e-7, a day included; before 11.0.0, Barker's anomaly at Barker's rate, 0.16 m off after 600 s
    /// at the edges of the band and 3.1 km after a day. At e = 1 Barker's law, and outside the band Kepler's equation, bit
    /// for bit as before.
    /// </para>
    /// <para>
    /// The elements keep their <see cref="ElementsType"/> (#463; before 11.0.0 mean elements came back labelled
    /// osculating, which let <see cref="ToStateVector()"/> take mean numbers for a state). Of mean elements, the mean
    /// anomaly advances at the two-body mean motion only, without the secular rates of the node and of the periapsis of
    /// the theory they belong to (SGP4 for a TLE set): a <see cref="TLE.Tle"/> overrides this method with SGP4.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public virtual OrbitalParameters AtEpoch(Time epoch)
    {
        var ke = this.ToKeplerianElements();
        double elapsed = Propagator.PropagationTimeArgument.ElapsedSeconds(Observer, Epoch, epoch);
        var (meanAnomaly, trueAnomaly) = MotionAfter(ke.MeanAnomaly(), elapsed);

        // An angle on an ellipse only (#368); KeplerianElements reduces it. Mean elements stay mean, so that the guard of
        // ToStateVector holds (#463). In the band the true anomaly of the conic's motion is given with Barker's mean
        // anomaly of it (#753)
        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), meanAnomaly, Observer, epoch, Frame,
            trueAnomaly, null, IsParabolic() ? PerigeeRadius() : _perigeeRadius, ElementsType);
    }

    /// <summary>
    /// Get the true longitude
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the true anomaly is computed from the mean anomaly and the mean anomaly or the eccentricity is not
    /// finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public double TrueLongitude()
    {
        _trueLongitude ??= (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly()) % Constants.TwoPi;
        return _trueLongitude.Value;
    }

    /// <summary>
    /// Get the true longitude at given epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the mean anomaly at the epoch or the eccentricity is not finite (#453).
    /// </exception>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public double TrueLongitude(Time epoch)
    {
        return (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly(epoch)) % Constants.TwoPi;
    }

    /// <summary>
    /// Get the mean longitude
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public double MeanLongitude()
    {
        _meanLongitude ??= (AscendingNode() + ArgumentOfPeriapsis() + MeanAnomaly()) % Constants.TwoPi;
        return _meanLongitude.Value;
    }


    /// <summary>
    /// Determines if the orbit is elliptical.
    /// </summary>
    /// <returns>
    /// True if the eccentricity is between 0 and 1 (exclusive), otherwise false.
    /// </returns>
    public bool IsElliptical()
    {
        return Eccentricity() is <= (1.0 - (1E-06)) and >= 0.0;
    }

    /// <summary>
    /// Determines if the orbit is circular.
    /// </summary>
    /// <returns>True if the eccentricity is less than 0.001, otherwise false.</returns>
    public bool IsCircular()
    {
        _isCircular ??= Eccentricity() < 1E-03;
        return _isCircular.Value;
    }

    /// <summary>
    /// Determines if the orbit is parabolic.
    /// </summary>
    /// <returns>True if the eccentricity is approximately 1.0, otherwise false.</returns>
    public bool IsParabolic()
    {
        _isParabolic ??= System.Math.Abs(Eccentricity() - 1.0) < 1E-06;
        return _isParabolic.Value;
    }

    /// <summary>
    /// Determines if the orbit is hyperbolic.
    /// </summary>
    /// <returns>True if the eccentricity is greater than 1.0, otherwise false.</returns>
    public bool IsHyperbolic()
    {
        _isHyperbolic ??= Eccentricity() > 1.0;
        return _isHyperbolic.Value;
    }

    /// <summary>
    /// Converts the orbital parameters to Keplerian elements at a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch time at which to compute the Keplerian elements.</param>
    /// <returns>The Keplerian elements at the given epoch.</returns>
    /// <remarks>The elapsed time is that of <see cref="AtEpoch"/>: the dynamical time of the observer, TT seconds about
    /// the Earth, TDB seconds about any other body (#501, #508). The elements keep their <see cref="ElementsType"/> (#463);
    /// a <see cref="TLE.Tle"/> moves its osculating set, which stays osculating.</remarks>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane and no elements (#687).</exception>
    public virtual KeplerianElements ToKeplerianElements(Time epoch)
    {
        double ellapsedTime = Propagator.PropagationTimeArgument.ElapsedSeconds(Observer, Epoch, epoch);
        var (m, trueAnomaly) = MotionAfter(MeanAnomaly(), ellapsedTime);

        // An angle on an ellipse only (#368); KeplerianElements reduces it. In the band, the true anomaly of the conic's
        // motion with Barker's mean anomaly of it (#753)
        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), m, Observer, epoch, Frame,
            trueAnomaly, null, PerigeeRadius(), ElementsType);
    }

    /// <summary>
    /// Converts the orbital parameters to Keplerian elements at the current epoch.
    /// </summary>
    /// <returns>The Keplerian elements at the current epoch.</returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane and no elements (#687).</exception>
    public virtual KeplerianElements ToKeplerianElements()
    {
        if (_keplerianElements is not null)
        {
            return _keplerianElements;
        }

        _keplerianElements = ToKeplerianElements(Epoch);
        return _keplerianElements;
    }

    /// <summary>
    /// Converts the orbital parameters to a different reference frame.
    /// </summary>
    /// <param name="frame">The reference frame to which to convert the orbital parameters.</param>
    /// <returns>The orbital parameters in the new reference frame: these parameters themselves when they are in
    /// <paramref name="frame"/> (a <see cref="TLE.Tle"/> in TEME stays the TLE), otherwise their
    /// <see cref="StateVector"/> in the new frame; <see cref="StateVector.ToFrame(Frame)"/> is typed as that state vector
    /// (R20 of #668).</returns>
    /// <remarks>
    /// <para>
    /// The state transforms as r' = R r and v' = R v - (R ω) × r', where R and ω come from
    /// <see cref="Frames.Frame.ToFrame(Frame, Time)"/>: ω is the angular velocity of the target frame relative to
    /// the source frame, expressed in the source frame, so R ω expresses it in the target frame. ω is zero between
    /// two inertial frames and non-zero towards or from a rotating frame (ITRF93, TIRS, a body-fixed frame).
    /// </para>
    /// <para>
    /// If the state carries a covariance matrix, it is transformed with the Jacobian of this transformation,
    /// P' = J · P · J^T with J = [[R, 0], [-[(R ω)×] R, R]], and symmetrized. Towards a rotating frame, the
    /// lower-left block makes the velocity covariance depend on the position covariance: a 100 m position
    /// uncertainty brings about 7 mm/s of velocity uncertainty in an Earth-fixed frame. Between inertial frames,
    /// J reduces to the block-diagonal rotation diag(R, R).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">SPICE knows no frame of the name of <paramref name="frame"/>
    /// (<see cref="ArgumentException.ParamName"/> <c>frame</c>) or of the frame of these parameters (no parameter named,
    /// the frame being no argument of the call): the message names the frame, SPICE's error its inner exception (R25 of
    /// #668, decided by S. Guillet, 2026-11-19; before 11.0.0 an <see cref="InvalidOperationException"/>).</exception>
    /// <exception cref="InvalidOperationException">The data do not give the orientation of a known frame at the epoch
    /// (SPICE's <see cref="Exceptions.SpiceException"/>).</exception>
    public virtual OrbitalParameters ToFrame(Frame frame)
    {
        if (frame == Frame)
        {
            return this;
        }

        StateVector sourceSv = ToStateVector();
        StateOrientation orientation;
        try
        {
            orientation = Frame.ToFrame(frame, Epoch);
        }
        catch (ArgumentException e) when (SpiceApi.IsUnknownFrameErrorOfAnotherParameter(e, frame, nameof(frame)))
        {
            // An unknown name of the target names this call's frame, not Frame.ToFrame's targetFrame (R25 of #668)
            throw SpiceApi.UnknownFrameError(e, frame, nameof(frame));
        }

        // The angular velocity comes in the source frame (SPICE xf2rav_c convention); r' is in the target frame.
        var angularVelocity = orientation.AngularVelocity.Rotate(orientation.Rotation);
        var newPos = sourceSv.Position.Rotate(orientation.Rotation);
        var newVel = sourceSv.Velocity.Rotate(orientation.Rotation) - angularVelocity.Cross(newPos);

        // The covariance goes through the Jacobian of the transformation above, the exact derivative of a map that
        // is linear in (r, v) at a fixed epoch. See docs/reference/covariance-propagation-provenance.md.
        Matrix? transformedCovariance = null;
        if (sourceSv.Covariance.HasValue)
        {
            var jacobian = Matrix.CreateStateTransformationJacobian(orientation.Rotation, angularVelocity);
            transformedCovariance = Matrix.TransformCovarianceWithJacobian(sourceSv.Covariance.Value, jacobian);
        }

        return new StateVector(newPos, newVel, Observer, Epoch, frame, transformedCovariance);
    }

    /// <summary>
    /// Converts the orbital parameters to equatorial coordinates: the right ascension and declination in ICRF, whatever
    /// the frame of the orbital parameters (#680), see <see cref="Equatorial(StateVector)"/>.
    /// </summary>
    /// <returns>The equatorial coordinates of the position relative to the observer, in ICRF.</returns>
    /// <exception cref="InvalidOperationException">The data do not give the orientation of the frame at the epoch.</exception>
    /// <exception cref="ArgumentException">SPICE knows no frame of the name of the frame of these parameters (no parameter
    /// named): the message names the frame, SPICE's error its inner exception (R25 of #668, decided by S. Guillet,
    /// 2026-11-19; before 11.0.0 an <see cref="InvalidOperationException"/>).</exception>
    public Equatorial ToEquatorial()
    {
        _equatorial ??= new Equatorial(ToStateVector());
        return _equatorial.Value;
    }

    /// <summary>
    /// Computes the velocity at perigee, by vis-viva at the radius of <see cref="PerigeeVector"/>: √(2μ/q) for a parabola
    /// (#737; NaN before 11.0.0).
    /// </summary>
    /// <returns>The velocity at perigee.</returns>
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.Tle"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public double PerigeeVelocity()
    {
        _perigeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / PerigeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _perigeeVelocity.Value;
    }

    /// <summary>
    /// Computes the velocity at apogee, by vis-viva at the radius of <see cref="ApogeeVector"/>.
    /// </summary>
    /// <returns>The velocity at apogee.</returns>
    /// <exception cref="InvalidOperationException">The orbit is not an ellipse and has no apoapsis (#737; before 11.0.0, 0
    /// for a parabola and the speed at the other branch of a hyperbola).</exception>
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.Tle"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public double ApogeeVelocity()
    {
        _apogeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / ApogeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _apogeeVelocity.Value;
    }

    /// <summary>
    /// Converts the orbital parameters to be relative to another localizable object: the state minus that object's state
    /// as the current observer sees it.
    /// </summary>
    /// <remarks>
    /// The aberration corrects the new origin, read as a target seen from the current observer
    /// (<c>localizable.GetEphemeris(Epoch, Observer, Frame, aberration)</c>); the state itself, one state and not the
    /// trajectory of its object, is not moved in time. The result is therefore not the object seen from the new origin with
    /// light time: for that, read the object as a target from the new origin (its <c>GetEphemeris</c>; a
    /// <see cref="Surface.Site"/> given by its coordinates is corrected as CSPICE's <c>spkcpt_c</c> corrects it, #489).
    /// When the new origin is the current observer, the state is returned unchanged, whatever the aberration. Kept as it was
    /// before 11.0.0 (#489, decided by S. Guillet, 2026-11-17).
    /// </remarks>
    /// <param name="localizable">The localizable object to which to convert the orbital parameters.</param>
    /// <param name="aberration">The correction of the new origin, seen from the current observer.</param>
    /// <returns>The orbital parameters relative to the specified localizable object: these parameters themselves when it is
    /// their observer, otherwise a <see cref="StateVector"/>; <see cref="StateVector.RelativeTo"/> is typed as that state
    /// vector (R20 of #668).</returns>
    public virtual OrbitalParameters RelativeTo(ILocalizable localizable, Aberration aberration)
    {
        if (localizable.NaifId == this.Observer.NaifId)
        {
            return this;
        }

        var eph = localizable.GetEphemeris(Epoch, Observer, Frame, aberration);
        var position = (ToStateVector().Position - eph.Position);
        var velocity = (ToStateVector().Velocity - eph.Velocity);
        return new StateVector(position, velocity, localizable, eph.Epoch, Frame);
    }

    /// <summary>
    /// The planetocentric coordinates of the position in the body-fixed frame of the observer, a
    /// <see cref="CelestialBody"/>: the position rotated to the body's frame at the epoch (<see cref="ToFrame(Frame)"/>),
    /// then longitude atan2(y, x), latitude asin(z / r) and radius r, CSPICE <c>reclat_c</c>'s but on the degenerate
    /// positions of the remarks.
    /// </summary>
    /// <returns>The planetocentric longitude and latitude (rad) and radius (m) of the position.</returns>
    /// <remarks>Before 11.0.0 <c>ToPlanetocentric(Aberration)</c>, whose correction was not used (R20 of #668). Where it
    /// differs from <c>reclat_c</c>: at the centre of the body the latitude is NaN (asin(0/0)), where <c>reclat_c</c>
    /// gives (0, 0, 0); on the polar axis with a negative-zero x (and y), the longitude is ±π (atan2 of signed zeros),
    /// where <c>reclat_c</c> gives 0 (on the axis with +0, the longitude is 0 and the latitude ±π/2); within about 1e-5
    /// rad of the poles asin(z / r) loses digits (7.6e-12 rad of the geodetic latitude derived from it on the Earth). A
    /// known limitation of 11.0.0, the path of 10.x kept bit for bit.</remarks>
    /// <exception cref="InvalidOperationException">The observer is not a <see cref="CelestialBody"/>, which has no
    /// body-fixed frame (before 11.0.0 an <see cref="InvalidCastException"/>).</exception>
    public Planetocentric ToPlanetocentric()
    {
        var body = ObserverBody(nameof(ToPlanetocentric));
        var position = ToFrame(body.Frame).ToStateVector().Position;
        double radius = position.Magnitude();
        return new Planetocentric(System.Math.Atan2(position.Y, position.X), System.Math.Asin(position.Z / radius), radius);
    }

    /// <summary>
    /// The planetodetic (geodetic) coordinates of the position on the ellipsoid of the observer, a
    /// <see cref="CelestialBody"/>, in its body-fixed frame: <see cref="ToPlanetocentric()"/> then
    /// <see cref="Planetocentric.ToPlanetodetic(CelestialBody)"/>, bit for bit, in one call (R20 of #668).
    /// </summary>
    /// <returns>The geodetic longitude and latitude (rad) and altitude (m) of the position, CSPICE <c>recgeo_c</c>'s on the
    /// body's equatorial radius and flattening but on the degenerate positions of the remarks.</returns>
    /// <remarks>The latitude is that of the planetocentric step, so it differs from <c>recgeo_c</c> where
    /// <see cref="ToPlanetocentric()"/> differs from <c>reclat_c</c>: 7.6e-12 rad 1.75e-5 rad from the poles of the
    /// Earth, 5.6e-16 rad below 1.5 rad; at the centre of the body the latitude and the altitude are NaN, where
    /// <c>recgeo_c</c> gives (0, π/2, −b); on the polar axis with a negative-zero x (and y), the longitude is ±π, where
    /// <c>recgeo_c</c> gives 0.</remarks>
    /// <exception cref="InvalidOperationException">The observer is not a <see cref="CelestialBody"/>, which has no
    /// ellipsoid.</exception>
    public Planetodetic ToPlanetodetic()
    {
        var body = ObserverBody(nameof(ToPlanetodetic));
        return ToPlanetocentric().ToPlanetodetic(body);
    }

    // The observer as the body whose frame and ellipsoid the coordinates take
    private CelestialBody ObserverBody(string conversion)
    {
        return Observer as CelestialBody ?? throw new InvalidOperationException(
            $"{conversion} needs an observer that is a celestial body, with a body-fixed frame and an ellipsoid: the observer of these parameters is {Observer.Name} ({Observer.NaifId}).");
    }

    /// <summary>
    /// Computes the semi-latus rectum of the orbit.
    /// </summary>
    /// <returns>The semi-latus rectum of the orbit.</returns>
    public double SemiLatusRectum()
    {
        var hNorm = SpecificAngularMomentum().Magnitude();
        return hNorm * hNorm / Observer.GM;
    }

    // A radius within this relative distance of an apsis is that apsis: the apsides computed by other expressions
    // (PerigeeVector().Magnitude(), a (1 - e), p / (1 + e)) differ from p / (1 ± e) by a few 1e-16 (#627)
    private const double ApsisRadiusTolerance = 1e-12;

    /// <summary>
    /// Computes the epoch at which the orbit is at a given radius, outbound from the periapsis: on an ellipse
    /// (<see cref="IsElliptical"/>, e ≤ 1 − 1e-6) the next such epoch from <see cref="Epoch"/> (within one period); on an
    /// open orbit its one outbound crossing, an epoch before <see cref="Epoch"/> when the orbit is already past the
    /// radius; on a circular orbit (e = 0), at its radius at every instant, <see cref="Epoch"/> itself.
    /// </summary>
    /// <remarks>Within 1e-6 of e = 1 (<see cref="IsParabolic"/>) the time is the conic's in universal variables, the motion
    /// of the band (#753; Barker's time at Barker's rate before, up to 1.5e-3 s off at |e − 1| = 9e-7 to 2e7 m); before
    /// #478 Kepler's equation took Barker's anomaly there, and the epoch was up to 8 times off, before <see cref="Epoch"/>
    /// even for a radius not yet reached.</remarks>
    /// <param name="radius">The distance from the center of motion, in meters.</param>
    /// <returns>The epoch at the radius, in the frame of <see cref="Epoch"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not finite, is below the periapsis, or
    /// is above the apoapsis of an ellipse: the orbit never reaches it (a radius within 1e-12 relative of an apsis is that
    /// apsis); or the epoch at the radius is outside the epochs a <see cref="Time"/> holds, after the last or before the
    /// first (#627).</exception>
    public Time TimeToRadius(double radius)
    {
        double e = Eccentricity();
        double p = SemiLatusRectum();
        double periapsis = p / (1.0 + e);
        double apoapsis = e < 1.0 ? p / (1.0 - e) : double.PositiveInfinity;
        if (!double.IsFinite(radius) || radius < periapsis * (1.0 - ApsisRadiusTolerance) ||
            radius > apoapsis * (1.0 + ApsisRadiusTolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius,
                $"The orbit never reaches a radius of {radius:R} m: it reaches the radii from its periapsis, {periapsis:R} m, to " +
                (e < 1.0 ? $"its apoapsis, {apoapsis:R} m." : "infinity (exclusive)."));
        }

        // A circular orbit is at its radius at every instant: (p / r - 1) / e is 0 / 0 or ±1 / 0, no true anomaly
        if (e == 0.0)
        {
            return Epoch;
        }

        // The true anomaly at the radius; the clamp only absorbs the rounding at an apsis
        double cosNuRad = System.Math.Clamp((p / radius - 1) / e, -1.0, 1.0);
        double nuRad = System.Math.Acos(cosNuRad);
        double meanAnomaly = MeanAnomaly(nuRad);

        // Far out on an open orbit the mean anomaly overflows (H = +∞, then e sinh H − H = NaN), a crossing after the last
        // epoch of Time; or the epoch passes one end of Time, the first on an open orbit far past the radius
        if (double.IsFinite(meanAnomaly))
        {
            try
            {
                return EpochAtMeanAnomaly(meanAnomaly);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
            }
        }

        bool beforeTheFirst = double.IsFinite(meanAnomaly) && !IsElliptical() && meanAnomaly < MeanAnomaly();
        throw new ArgumentOutOfRangeException(nameof(radius), radius,
            $"The orbit reaches a radius of {radius:R} m outside the epochs a Time holds, " +
            (beforeTheFirst ? "before the first." : "after the last."));
    }

    /// <summary>
    /// Computes the epoch at which the orbit reaches the sphere of influence of its center of motion, outbound from the
    /// periapsis (<see cref="TimeToRadius"/>): on an open orbit already beyond the sphere, an epoch before
    /// <see cref="Epoch"/>.
    /// </summary>
    /// <returns>The epoch at the sphere of influence, in the frame of <see cref="Epoch"/>.</returns>
    /// <exception cref="InvalidOperationException">The observer is not a celestial body, or its sphere of influence is
    /// infinite (the Sun, a body built from custom parameters, a body the kernels do not chain to its barycenter): an
    /// orbit about it never leaves it (#627).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The orbit never reaches the sphere of influence (an ellipse inside
    /// it), or reaches it outside the epochs a <see cref="Time"/> holds (<see cref="TimeToRadius"/>).</exception>
    public Time TimeToSphereOfInfluence()
    {
        var centerOfMotion = Observer as CelestialBody;
        if (centerOfMotion is null)
        {
            throw new InvalidOperationException("The observer must be a celestial body to compute the time to the sphere of influence.");
        }

        if (double.IsPositiveInfinity(centerOfMotion.SphereOfInfluence))
        {
            throw new InvalidOperationException(
                $"{centerOfMotion.Name} has no finite sphere of influence; an orbit about it never leaves it.");
        }

        return TimeToRadius(centerOfMotion.SphereOfInfluence);
    }

    /// <summary>
    /// Converts the current orbital parameters into a TLE (Two-Line Element) representation: a fit of SGP4 mean elements
    /// in TEME, written with the given identifiers and the fields of <paramref name="options"/>, in the order of
    /// <see cref="TLE.Tle.Create"/> (decision 113 of #668, decided by S. Guillet, 2026-11-26: a
    /// <c>TLE.Configuration</c> record before 11.0.0).
    /// </summary>
    /// <param name="name">The name of the object.</param>
    /// <param name="noradId">The NORAD catalog number, 0 to <see cref="NoradCatalogNumber.MaxValue"/> (339999); a value above
    /// 99999 is written in the Alpha-5 form.</param>
    /// <param name="cosparId">The COSPAR international designator in TLE form (YYNNN and one to three capital letters, e.g.
    /// "98067A"), or null or blank for none: columns 10-17 of line 1 are then blank, as <see cref="TLE.Tle.Create"/> writes
    /// them (#604). A non-blank value of another form is not a designator: the fit then throws
    /// <see cref="InvalidOperationException"/>.</param>
    /// <param name="options">The settings of the fit and the other fields of the TLE; null for
    /// <see cref="TleFitOptions.Default"/>.</param>
    /// <returns>
    /// A <see cref="Tle"/> object representing the orbital parameters in TLE format.
    /// </returns>
    /// <remarks>
    /// The mean elements are fitted from the current orbital parameters, rotated to TEME. A null or blank
    /// <paramref name="cosparId"/> writes blank columns 10-17 of line 1, the TLE without an international designator of
    /// <see cref="TLE.Tle.Create"/> (#604).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown before the fit when these parameters are not about the Earth (NAIF id 399): a TLE describes an Earth orbit
    /// (#607; before 11.0.0, the fit took them for an Earth orbit and failed, or wrote a TLE of another orbit).
    /// </exception>
    public TLE.Tle ToTle(string name, int noradId, string cosparId, TleFitOptions options = null)
    {
        options ??= TleFitOptions.Default;
        if (Observer.NaifId != TLE.Tle.EarthNaifId)
        {
            throw new InvalidOperationException(
                $"The orbit is about {Observer.Name} ({Observer.NaifId}): a TLE describes an Earth orbit, so it cannot be converted to a TLE.");
        }

        var meanElements = _sharedMeanElementsConverter.Convert(this, noradId, name, cosparId, options.RevolutionNumberAtEpoch,
            options.BStar, options.MaxIterations);

        return TLE.Tle.Create(meanElements, name, noradId, cosparId, options.RevolutionNumberAtEpoch, options.Classification,
            options.BStar, options.MeanMotionDot, options.MeanMotionDDot, options.ElementSetNumber);
    }

    #region Operators

    /// <summary>
    /// Value equality within one concrete type (#475, decided by S. Guillet, 2026-11-17): each type of the library
    /// overrides this method to compare, for an object of its own type, its values, its observer's NAIF id, its
    /// <see cref="Epoch"/> (an instant, whatever its time scale, #512), its <see cref="Frame"/> and its
    /// <see cref="ElementsType"/>, without computing a state; a type that does not override it has reference equality.
    /// </summary>
    /// <remarks>
    /// Two sets of different types are different, an element set and its own <see cref="ToStateVector()"/> included; to
    /// compare two orbits, compare their states, <c>x.ToStateVector() == y.ToStateVector()</c>, which throws for mean
    /// elements. Before 11.0.0 equality through <see cref="OrbitalParameters"/> compared the states of both operands: it
    /// threw for mean elements (even against null) and for e = NaN, ran SGP4 for a TLE, and found equal two sets whose
    /// states were both NaN.
    /// </remarks>
    public override bool Equals(object obj)
    {
        return ReferenceEquals(this, obj);
    }

    /// <summary>
    /// Whether <paramref name="other"/> is equal to this set: <see cref="Equals(object)"/>, the value equality of the
    /// concrete type (#475). Never throws, and is false for null.
    /// </summary>
    public virtual bool Equals(OrbitalParameters other)
    {
        return Equals((object)other);
    }

    /// <summary>
    /// The hash code of the observer's NAIF id, the epoch (its instant, #512), the frame and the element type, which every
    /// type combines with its own values: equal sets share it (#475; before 11.0.0 it held the identity of the instance).
    /// </summary>
    /// <remarks>
    /// The sets are immutable, but for the work states of an integration, which the integrator updates in place at each
    /// evaluation: the state it hands to <see cref="Propagator.Events.IEventDetector.Evaluate"/>, and the states it hands to
    /// <see cref="Propagator.Forces.ForceBase.Apply(StateVector)"/> of a force added by
    /// <see cref="Propagator.Integrators.Integrator.AddForce"/> (its evaluation state, the stage states of RK7(8)). A
    /// detector or a force that keeps such a state as a key keeps a copy of it, not the instance, whose hash code would
    /// change.
    /// </remarks>
    public override int GetHashCode()
    {
        return HashCode.Combine(Observer.NaifId, Epoch, Frame, ElementsType);
    }

    /// <summary>
    /// Whether this set and <paramref name="other"/> have the same observer (by NAIF id), epoch (an instant, #512), frame
    /// and element type: the part of the equality of every type held by <see cref="OrbitalParameters"/> (#475).
    /// </summary>
    private protected bool HasSameReference(OrbitalParameters other)
    {
        return Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame && ElementsType == other.ElementsType;
    }

    public static bool operator ==(OrbitalParameters left, OrbitalParameters right)
    {
        return EqualityComparer<OrbitalParameters>.Default.Equals(left, right);
    }

    public static bool operator !=(OrbitalParameters left, OrbitalParameters right)
    {
        return !(left == right);
    }

    #endregion
}