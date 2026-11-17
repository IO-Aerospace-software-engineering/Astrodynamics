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
    protected TimeSpan? _period;
    protected double? _meanMotion;
    protected StateVector _stateVector;
    protected EquinoctialElements _equinoctial;
    private Vector3? _perigeevector;
    private Vector3? _apogeeVector;
    private double? _trueLongitude;
    private double? _meanLongitude;
    private bool? _isCircular;
    private bool? _isParabolic;
    private bool? _isHyperbolic;
    protected KeplerianElements _keplerianElements;
    private Equatorial? _equatorial;
    private double? _perigeeVelocity;
    private double? _apogeeVelocity;
    protected double? _ascendingNode;
    protected double? _trueAnomaly;
    private double? _eccentricAnomaly;
    protected double? _perigeeRadius;
    protected double? _semiMajorAxis;
    protected double? _eccentricity;
    protected double? _inclination;
    protected double? _periapsisArgument;
    protected double? _meanAnomaly;


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
            _trueAnomaly = EllipticalTrueAnomaly() % Constants._2PI;
        }
        else if (IsParabolic())
        {
            _trueAnomaly = ParabolicTrueAnomaly() % Constants._2PI;
        }
        else if (IsHyperbolic())
        {
            _trueAnomaly = HyperbolicTrueAnomaly() % Constants._2PI;
        }
        else
        {
            // Only a non-finite eccentricity takes no branch: no conic has it (#476; the message of an empty Nullable before)
            throw new InvalidOperationException(
                $"No conic has the eccentricity e = {Eccentricity():R}: the true anomaly is undefined.");
        }

        if (_trueAnomaly < 0)
        {
            _trueAnomaly += Constants._2PI;
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
        return 2.0 * System.Math.Sinh(System.Math.Asinh(1.5 * MeanAnomaly()) / 3.0);
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
    /// <returns>The mean anomaly in radians.</returns>
    public static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity, double eccentricAnomaly)
    {
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants._2PI;
        }

        // On an open orbit the mean anomaly is not an angle: it grows without bound, negative before the periapsis, so it
        // is not reduced modulo 2π (#368)
        if (eccentricity < 1)
        {
            if (System.Math.Abs(trueAnomaly - Constants.PI) < 1e-09)
            {
                return Constants.PI;
            }

            var res = (eccentricAnomaly - eccentricity * System.Math.Sin(eccentricAnomaly)) % Constants._2PI;
            return res < 0 ? res + Constants._2PI : res;
        }
        else if (eccentricity > 1)
        {
            return eccentricity * System.Math.Sinh(eccentricAnomaly) - eccentricAnomaly;
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
    /// <returns>The period of an ellipse, 2π √(a³/μ), truncated to the tick of <see cref="TimeSpan"/>.</returns>
    /// <exception cref="InvalidOperationException">The semi-major axis is not finite and above 0: a parabola (+Infinity) or
    /// a hyperbola (a &lt; 0) has no period (#663; before 11.0.0, the NaN <see cref="ArgumentException"/> or the
    /// <see cref="OverflowException"/> of <see cref="TimeSpan"/>).</exception>
    /// <exception cref="OverflowException">The period of an ellipse exceeds <see cref="TimeSpan.MaxValue"/>, 29 227 years:
    /// a within 1e-6 of e = 1, for instance (#455).</exception>
    public TimeSpan Period()
    {
        double a = SemiMajorAxis();

        // Only an ellipse has a period: tested on a, not on e, so that the conic's finite a of the band keeps it (#663);
        // before the cache, so that a period given to the elements of an open orbit (KeplerianElements' period) is not
        // returned either
        if (!(double.IsFinite(a) && a > 0.0))
        {
            throw new InvalidOperationException(
                $"An open orbit has no period: e = {Eccentricity():R}, a = {a:R} m (a period needs a finite semi-major axis above 0, an ellipse).");
        }

        _period ??= TimeSpan.FromSeconds(Constants._2PI * System.Math.Sqrt((a * a * a) / Observer.GM));
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
    /// <see cref="EpochAtMeanAnomaly"/>, <see cref="TimeToRadius"/>) still advances Barker's anomaly at Barker's
    /// rate.</remarks>
    public double MeanMotion(double orbitingBodyMass = 0.0)
    {
        if (_meanMotion.HasValue)
        {
            return _meanMotion.Value;
        }

        if (IsElliptical())
        {
            _meanMotion = System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(SemiMajorAxis(), 3));
        }
        else if (IsParabolic())
        {
            double a = SemiMajorAxis();

            // A parabola: so that Barker's D + D³/3 = n (t - T), t - T = √(2q³/μ) (D + D³/3), q the periapsis radius
            // (#368). The conic of a finite a in the band, either side of e = 1: √(μ/|a|³) (#459)
            _meanMotion = double.IsPositiveInfinity(a)
                ? BarkerMeanMotion(orbitingBodyMass)
                : System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(System.Math.Abs(a), 3));
        }
        else
        {
            _meanMotion = System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(-SemiMajorAxis(), 3));
        }

        return _meanMotion.Value;
    }

    /// <summary>
    /// Barker's mean motion √(μ/(2q³)), q the perigee radius: the rate of Barker's mean anomaly D + D³/3 (#368).
    /// </summary>
    private double BarkerMeanMotion(double orbitingBodyMass = 0.0) =>
        System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / (2.0 * System.Math.Pow(PerigeeRadius(), 3)));

    /// <summary>
    /// The rate of <see cref="MeanAnomaly()"/> in the two-body motion: Barker's √(μ/(2q³)) within 1e-6 of e = 1, where the
    /// mean anomaly is Barker's on both sides of e = 1 (#459, option 1: Barker's anomaly kept for the motion, well
    /// conditioned there), <see cref="MeanMotion"/> elsewhere, cached mean motion of mean elements included.
    /// </summary>
    private double MeanAnomalyRate() => IsParabolic() ? BarkerMeanMotion() : MeanMotion();


    /// <summary>
    /// Get the state vector
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to convert mean elements directly to a state vector.
    /// Mean elements require a specific propagator (e.g., SGP4/SDP4) for accurate position calculations.
    /// For TLE data, use <see cref="TLE.TLE.ToOsculating()"/> or <see cref="TLE.TLE.ToOsculating(Time)"/> instead.
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
            // the periapsis up to 5e-7 of q away and moved a round trip by |r| |e − 1| / (1 + e), metres to 4.2e7 m)
            var p = IsParabolic() ? PerigeeRadius() * (1.0 + e) : SemiMajorAxis() * (1 - e * e);

            var v = TrueAnomaly();
            var r0 = p / (1 + e * System.Math.Cos(v));
            var x = r0 * System.Math.Cos(v);
            var y = r0 * System.Math.Sin(v);
            var dotX = -System.Math.Sqrt(Observer.GM / p) * System.Math.Sin(v);
            var dotY = System.Math.Sqrt(Observer.GM / p) * (e + System.Math.Cos(v));
            Matrix r3 = Matrix.CreateRotationMatrixZ(AscendingNode());
            Matrix r1 = Matrix.CreateRotationMatrixX(Inclination());
            Matrix r3W = Matrix.CreateRotationMatrixZ(ArgumentOfPeriapsis());
            Matrix r = r3 * r1 * r3W;
            double[] pos = [x, y, 0.0];
            double[] vel = [dotX, dotY, 0.0];
            double[] finalPos = pos * r;
            double[] finalV = vel * r;

            _stateVector = new StateVector(new Vector3(finalPos[0], finalPos[1], finalPos[2]), new Vector3(finalV[0], finalV[1], finalV[2]), Observer, Epoch, Frame);
        }

        return _stateVector;
    }

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
                "For TLE data, use TLE.ToOsculating() instead.");
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
                "For TLE data, use TLE.ToOsculating(epoch) instead.");
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
    /// at or after <see cref="Epoch"/>, whatever the turn of the given anomaly and of <see cref="MeanAnomaly"/>; on an
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
    /// <returns>The equinoctial elements, of semi-latus rectum P = a(1 − e²); in the near-parabolic band |e − 1| &lt; 1e-6
    /// (<see cref="IsParabolic"/>), P = q (1 + e) with q the <see cref="PerigeeRadius"/>, 2q for a parabola and h²/μ for a
    /// <see cref="StateVector"/> (#462; before 11.0.0, NaN at e = 1 and ±∞ elsewhere in the band, its sign set by
    /// rounding).</returns>
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
            double v = TrueAnomaly();

            // In the band |e − 1| < 1e-6 (IsParabolic) a(1 − e²) is ∞ × 0 or ±∞ (#462): the conic's p = q (1 + e), 2q at
            // e = 1 and h²/μ for a state vector, read without the semi-major axis. Outside the band, a(1 − e²) bit for bit
            double p = IsParabolic() ? PerigeeRadius() * (1.0 + e) : SemiMajorAxis() * (1 - e * e);
            double f = e * System.Math.Cos(o + w);
            double g = e * System.Math.Sin(o + w);
            double h = System.Math.Tan(i * 0.5) * System.Math.Cos(o);
            double k = System.Math.Tan(i * 0.5) * System.Math.Sin(o);
            double l0 = o + w + v;

            _equinoctial = new EquinoctialElements(p, f, g, h, k, l0, Observer, Epoch, Frame, PerigeeRadius(), ElementsType);
        }

        return _equinoctial;
    }

    /// <summary>
    /// Get perigee vector: the position of the periapsis relative to the observer, a(1 − e) P̂, with P̂ the direction of
    /// the periapsis (#686).
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
    /// throws (a <see cref="TLE.TLE"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public Vector3 PerigeeVector()
    {
        _perigeevector ??= PeriapsisDirection() * SemiMajorAxis() * (1.0 - Eccentricity());
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
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.TLE"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public Vector3 ApogeeVector()
    {
        _apogeeVector ??= PeriapsisDirection().Inverse() * SemiMajorAxis() * (1.0 + Eccentricity());
        return _apogeeVector.Value;
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
    /// The elements keep their <see cref="ElementsType"/> (#463; before 11.0.0 mean elements came back labelled
    /// osculating, which let <see cref="ToStateVector()"/> take mean numbers for a state). Of mean elements, the mean
    /// anomaly advances at the two-body mean motion only, without the secular rates of the node and of the periapsis of
    /// the theory they belong to (SGP4 for a TLE set): a <see cref="TLE.TLE"/> overrides this method with SGP4.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public virtual OrbitalParameters AtEpoch(Time epoch)
    {
        var ke = this.ToKeplerianElements();
        double elapsed = Propagator.PropagationTimeArgument.ElapsedSeconds(Observer, Epoch, epoch);
        var meanAnomaly = ke.MeanAnomaly() + elapsed * MeanAnomalyRate();

        // An angle on an ellipse only (#368); KeplerianElements reduces it. Mean elements stay mean, so that the guard of
        // ToStateVector holds (#463)
        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), meanAnomaly, Observer, epoch, Frame,
            perigeeRadius: IsParabolic() ? PerigeeRadius() : _perigeeRadius, elementsType: ElementsType);
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
        _trueLongitude ??= (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly()) % Constants._2PI;
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
        return (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly(epoch)) % Constants._2PI;
    }

    /// <summary>
    /// Get the mean longitude
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane (#687).</exception>
    public double MeanLongitude()
    {
        _meanLongitude ??= (AscendingNode() + ArgumentOfPeriapsis() + MeanAnomaly()) % Constants._2PI;
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
    /// a <see cref="TLE.TLE"/> moves its osculating set, which stays osculating.</remarks>
    /// <exception cref="InvalidOperationException">A rectilinear or non-finite <see cref="StateVector"/> (r × v = 0) has no
    /// orbit plane and no elements (#687).</exception>
    public virtual KeplerianElements ToKeplerianElements(Time epoch)
    {
        double ellapsedTime = Propagator.PropagationTimeArgument.ElapsedSeconds(Observer, Epoch, epoch);
        double m = MeanAnomaly() + MeanAnomalyRate() * ellapsedTime;

        // An angle on an ellipse only (#368); KeplerianElements reduces it
        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), m, Observer, epoch, Frame,
            perigeeRadius: PerigeeRadius(), elementsType: ElementsType);
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
    /// <returns>The orbital parameters in the new reference frame.</returns>
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
    public OrbitalParameters ToFrame(Frame frame)
    {
        if (frame == Frame)
        {
            return this;
        }

        StateVector sourceSv = ToStateVector();
        var orientation = Frame.ToFrame(frame, Epoch);
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
    public Equatorial ToEquatorial()
    {
        _equatorial ??= new Equatorial(ToStateVector());
        return _equatorial.Value;
    }

    /// <summary>
    /// Computes the velocity at perigee, by vis-viva at the radius of <see cref="PerigeeVector"/>.
    /// </summary>
    /// <returns>The velocity at perigee.</returns>
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.TLE"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
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
    /// <exception cref="InvalidOperationException">Mean elements, which have no position, as <see cref="ToStateVector()"/>
    /// throws (a <see cref="TLE.TLE"/> takes its osculating elements); or a <see cref="StateVector"/> at the centre
    /// (r = 0) or non-finite, which has no orbit plane (#686, #687).</exception>
    public double ApogeeVelocity()
    {
        _apogeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / ApogeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _apogeeVelocity.Value;
    }

    /// <summary>
    /// Converts the orbital parameters to be relative to another localizable object.
    /// </summary>
    /// <param name="localizable">The localizable object to which to convert the orbital parameters.</param>
    /// <param name="aberration">The aberration to consider in the conversion.</param>
    /// <returns>The orbital parameters relative to the specified localizable object.</returns>
    public OrbitalParameters RelativeTo(ILocalizable localizable, Aberration aberration)
    {
        if (localizable.NaifId == this.Observer.NaifId)
        {
            return this;
        }

        var eph = localizable.GetEphemeris(Epoch, Observer, Frame, aberration).ToStateVector();
        var position = (ToStateVector().Position - eph.Position);
        var velocity = (ToStateVector().Velocity - eph.Velocity);
        return new StateVector(position, velocity, localizable, eph.Epoch, Frame);
    }

    /// <summary>
    /// Converts the orbital parameters to planetocentric coordinates.
    /// </summary>
    /// <param name="aberration">The aberration to consider in the conversion.</param>
    /// <returns>The planetocentric coordinates.</returns>
    public Planetocentric ToPlanetocentric(Aberration aberration)
    {
        var body = Observer as CelestialBody;
        return ((CelestialBody)Observer).SubObserverPoint(ToFrame(body!.Frame).ToStateVector().Position, Epoch, aberration);
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
    /// <remarks>Within 1e-6 of e = 1 (<see cref="IsParabolic"/>) the time is Barker's, the motion of the band; before 11.0.0
    /// Kepler's equation took Barker's anomaly there, and the epoch was up to 8 times off, before <see cref="Epoch"/> even for
    /// a radius not yet reached (#478).</remarks>
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
    /// Converts the current orbital parameters into a TLE (Two-Line Element) representation
    /// using the provided configuration.
    /// </summary>
    /// <param name="config">
    /// A <see cref="Astrodynamics.OrbitalParameters.TLE.Configuration"/> object containing the necessary parameters for TLE generation,
    /// such as NORAD ID, satellite name, COSPAR ID, and other optional settings.
    /// </param>
    /// <returns>
    /// A <see cref="TLE"/> object representing the orbital parameters in TLE format.
    /// </returns>
    /// <remarks>
    /// This method uses the <see cref="MeanElementsConverter"/> to compute the mean orbital elements
    /// from the current orbital parameters. The resulting TLE includes metadata such as the satellite's
    /// classification, drag term, and epoch information.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="config"/> parameter is null.
    /// </exception>
    public TLE.TLE ToTLE(TLE.Configuration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var meanElements = _sharedMeanElementsConverter.Convert(this, config.NoradId,
            config.Name, config.CosparId, config.RevolutionsAtEpoch,
            config.BstarDragTerm, config.MaxIterations);

        return TLE.TLE.Create(meanElements, config.Name, config.NoradId,
            config.CosparId, config.RevolutionsAtEpoch, config.Classification,
            config.BstarDragTerm, config.FirstDerivativeMeanMotion,
            config.SecondDerivativeMeanMotion, config.ElementSetNumber);
    }

    #region Operators

    public override bool Equals(object obj)
    {
        return Equals(obj as OrbitalParameters);
    }

    public virtual bool Equals(OrbitalParameters other)
    {
        return other is not null &&
            base.Equals(other) || ToStateVector() == other?.ToStateVector();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Observer, Epoch, Frame);
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