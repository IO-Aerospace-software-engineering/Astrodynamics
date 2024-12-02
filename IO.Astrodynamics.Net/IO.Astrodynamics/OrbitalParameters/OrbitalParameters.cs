using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

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

    //Data used for caching
    private Vector3? _eccentricVector;
    private Vector3? _specificAngularMomentum;
    private double? _specificOrbitalEnergy;
    private Vector3? _ascendingNodeVector;
    private Vector3? _decendingNodeVector;
    protected TimeSpan? _period;
    private double? _meanMotion;
    protected StateVector _stateVector;
    private EquinoctialElements _equinoctial;
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
    /// <exception cref="ArgumentNullException">
    /// Thrown when the observer or frame is null.
    /// </exception>
    protected OrbitalParameters(ILocalizable observer, in Time epoch, Frame frame)
    {
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        Epoch = epoch;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }


    /// <summary>
    /// Resets the cached values for various orbital parameters.
    /// </summary>
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
        _eccentricVector = (sv.Velocity.Cross(SpecificAngularMomentum()) / Observer.GM) - sv.Position.Normalize();
        return _eccentricVector.Value;
    }

    /// <summary>
    /// Get vector to ascending node unitless
    /// </summary>
    /// <returns></returns>
    public virtual Vector3 AscendingNodeVector()
    {
        if (_ascendingNodeVector.HasValue)
        {
            return _ascendingNodeVector.Value;
        }

        if (Inclination() == 0.0)
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
    public double TrueAnomaly(Time epoch)
    {
        return AtEpoch(epoch).TrueAnomaly();
    }

    /// <summary>
    /// Get the eccentric anomaly
    /// </summary>
    /// <returns></returns>
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
        double M = MeanAnomaly();

        //Barker angle
        double B = M;
        double deltaB;

        do
        {
            deltaB = (M - (0.5 * B + (1.0 / 6.0) * System.Math.Pow(B, 3))) / (0.5 + 0.5 * System.Math.Pow(B, 2));
            B += deltaB;
        } while (System.Math.Abs(deltaB) > 1e-06);

        return B;
    }

    /// <summary>
    /// Compute hyperbolic anomaly
    /// </summary>
    /// <returns></returns>
    private double HyperbolicAnomaly()
    {
        double M = MeanAnomaly();
        double e = Eccentricity();
        double H = System.Math.Log(2 * M / e + 1.8); // Estimation initiale
        double deltaH;

        do
        {
            // Calcul de la nouvelle valeur de H
            deltaH = (e * System.Math.Sinh(H) - H - M) / (e * System.Math.Cosh(H) - 1);
            H -= deltaH;
        } while (System.Math.Abs(deltaH) > 1e-06);

        return H;
    }

    /// <summary>
    /// Compute eccentric anomaly
    /// </summary>
    /// <returns></returns>
    private double EllipticAnomaly()
    {
        double M = MeanAnomaly();
        double E = M;
        double e = Eccentricity();
        double deltaE;

        do
        {
            // Calcul de la nouvelle valeur d'E
            deltaE = (E - e * System.Math.Sin(E) - M) / (1 - e * System.Math.Cos(E));
            E -= deltaE;
        } while (System.Math.Abs(deltaE) > 1e-06);

        return E;
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
        return (trueAnomaly * 0.5) - System.Math.Sinh(trueAnomaly * 0.5);
    }

    /// <summary>
    /// Compute mean anomaly from true anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    public double MeanAnomaly(double trueAnomaly)
    {
        return TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity(), this.EccentricAnomaly(trueAnomaly));
    }

    /// <summary>
    /// Converts the true anomaly to the mean anomaly.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly in radians.</param>
    /// <param name="eccentricity">The eccentricity of the orbit.</param>
    /// <param name="eccentricAnomaly">The eccentric anomaly in radians.</param>
    /// <returns>The mean anomaly in radians.</returns>
    public static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity, double eccentricAnomaly)
    {
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants._2PI;
        }

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
            var res = (eccentricity * System.Math.Sinh(eccentricAnomaly) - eccentricAnomaly) % Constants._2PI;
            return res < 0 ? res + Constants._2PI : res;
        }
        else
        {
            var res = (eccentricAnomaly + System.Math.Pow(eccentricAnomaly, 3) / 3.0) % Constants._2PI;
            return res < 0 ? res + Constants._2PI : res;
        }
    }

    /// <summary>
    /// Get orbital period
    /// </summary>
    /// <returns></returns>
    public TimeSpan Period()
    {
        if (!_period.HasValue)
        {
            double a = SemiMajorAxis();
            double T = Constants._2PI * System.Math.Sqrt((a * a * a) / Observer.GM);
            _period = TimeSpan.FromSeconds(T);
        }

        return _period.Value;
    }

    /// <summary>
    /// Get orbital mean motion
    /// </summary>
    /// <returns></returns>
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
            _meanMotion = 2 * System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(PerigeeRadius(), 3));
        }
        else
        {
            _meanMotion = System.Math.Sqrt(Constants.G * (Observer.Mass + orbitingBodyMass) / System.Math.Pow(-SemiMajorAxis(), 3));
        }

        return _meanMotion.Value;
    }


    /// <summary>
    /// Get the state vector
    /// </summary>
    /// <returns></returns>
    public virtual StateVector ToStateVector()
    {
        if (_stateVector is null)
        {
            var e = Eccentricity();
            var p = SemiMajorAxis() * (1 - e * e);
            if (IsParabolic())
            {
                p = 2 * PerigeeRadius();
            }

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
    /// Converts the orbital parameters to a state vector at a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch time at which to compute the state vector.</param>
    /// <returns>The state vector at the given epoch.</returns>
    public virtual StateVector ToStateVector(Time epoch)
    {
        return AtEpoch(epoch).ToStateVector();
    }

    /// <summary>
    /// Converts the orbital parameters to a state vector at a given true anomaly.
    /// </summary>
    /// <param name="trueAnomaly">The true anomaly at which to compute the state vector.</param>
    /// <returns>The state vector at the given true anomaly.</returns>
    public StateVector ToStateVector(double trueAnomaly)
    {
        return ToStateVector(EpochAtMeanAnomaly(TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity(), EccentricAnomaly(trueAnomaly))));
    }

    /// <summary>
    /// Computes the epoch time at a given mean anomaly.
    /// </summary>
    /// <param name="meanAnomaly">The mean anomaly at which to compute the epoch time.</param>
    /// <returns>The epoch time at the given mean anomaly.</returns>
    public Time EpochAtMeanAnomaly(double meanAnomaly)
    {
        var res = meanAnomaly - MeanAnomaly();
        if (res < 0.0)
        {
            res += Constants._2PI;
        }

        return Epoch + TimeSpan.FromSeconds(res / MeanMotion());
    }

    /// <summary>
    /// Convert to equinoctial
    /// </summary>
    /// <returns></returns>
    public virtual EquinoctialElements ToEquinoctial()
    {
        if (_equinoctial is null)
        {
            double e = Eccentricity();
            double o = AscendingNode();
            double w = ArgumentOfPeriapsis();
            double i = Inclination();
            double v = TrueAnomaly();

            double p = SemiMajorAxis() * (1 - e * e);
            double f = e * System.Math.Cos(o + w);
            double g = e * System.Math.Sin(o + w);
            double h = System.Math.Tan(i * 0.5) * System.Math.Cos(o);
            double k = System.Math.Tan(i * 0.5) * System.Math.Sin(o);
            double l0 = o + w + v;

            _equinoctial = new EquinoctialElements(p, f, g, h, k, l0, Observer, Epoch, Frame, PerigeeRadius());
        }

        return _equinoctial;
    }

    /// <summary>
    /// Get perigee vector
    /// </summary>
    /// <returns></returns>
    public Vector3 PerigeeVector()
    {
        if (_perigeevector.HasValue)
        {
            return _perigeevector.Value;
        }

        if (Eccentricity() == 0.0)
        {
            _perigeevector = Vector3.VectorX * SemiMajorAxis();
        }

        _perigeevector ??= EccentricityVector().Normalize() * SemiMajorAxis() * (1.0 - Eccentricity());
        return _perigeevector.Value;
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
    /// Get apogee vector
    /// </summary>
    /// <returns></returns>
    public Vector3 ApogeeVector()
    {
        if (_apogeeVector.HasValue)
        {
            return _apogeeVector.Value;
        }

        if (Eccentricity() == 0.0)
        {
            _apogeeVector = Vector3.VectorX.Inverse() * SemiMajorAxis();
        }

        _apogeeVector ??= EccentricityVector().Normalize().Inverse() * SemiMajorAxis() * (1.0 + Eccentricity());
        return _apogeeVector.Value;
    }

    /// <summary>
    /// Get orbital parameters at epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <returns></returns>
    public virtual OrbitalParameters AtEpoch(Time epoch)
    {
        var ke = this.ToKeplerianElements();
        var deltaT = epoch - Epoch;
        var meanAnomaly = ke.MeanAnomaly() + deltaT.TotalSeconds * MeanMotion();
        if (meanAnomaly < 0.0)
        {
            meanAnomaly += Constants._2PI;
        }

        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), meanAnomaly % Constants._2PI, Observer, epoch, Frame,
            perigeeRadius: _perigeeRadius);
    }

    /// <summary>
    /// Get the true longitude
    /// </summary>
    /// <returns></returns>
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
    public double TrueLongitude(Time epoch)
    {
        return (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly(epoch)) % Constants._2PI;
    }

    /// <summary>
    /// Get the mean longitude
    /// </summary>
    /// <returns></returns>
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
    public KeplerianElements ToKeplerianElements(Time epoch)
    {
        double ellapsedTime = (epoch - Epoch).TotalSeconds;
        double m = MeanAnomaly() + MeanMotion() * ellapsedTime;
        while (m < 0.0)
        {
            m += Constants._2PI;
        }

        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), m % Constants._2PI, Observer, epoch, Frame,
            perigeeRadius: PerigeeRadius());
    }

    /// <summary>
    /// Converts the orbital parameters to Keplerian elements at the current epoch.
    /// </summary>
    /// <returns>The Keplerian elements at the current epoch.</returns>
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
    public OrbitalParameters ToFrame(Frame frame)
    {
        if (frame == Frame)
        {
            return this;
        }

        StateVector icrfSv = ToStateVector();
        var orientation = Frame.ToFrame(frame, Epoch);
        var newPos = icrfSv.Position.Rotate(orientation.Rotation);
        var newVel = icrfSv.Velocity.Rotate(orientation.Rotation) - orientation.AngularVelocity.Cross(newPos);
        return new StateVector(newPos, newVel, Observer, Epoch, frame);
    }

    /// <summary>
    /// Converts the orbital parameters to equatorial coordinates.
    /// </summary>
    /// <returns>The equatorial coordinates.</returns>
    public Equatorial ToEquatorial()
    {
        _equatorial ??= new Equatorial(ToStateVector());
        return _equatorial.Value;
    }

    /// <summary>
    /// Computes the velocity at perigee.
    /// </summary>
    /// <returns>The velocity at perigee.</returns>
    public double PerigeeVelocity()
    {
        _perigeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / PerigeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _perigeeVelocity.Value;
    }

    /// <summary>
    /// Computes the velocity at apogee.
    /// </summary>
    /// <returns>The velocity at apogee.</returns>
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

    /// <summary>
    /// Computes the orbital parameters of the observed body.
    /// </summary>
    /// <param name="observation1"></param>
    /// <param name="observation2"></param>
    /// <param name="observation3"></param>
    /// <param name="observer"></param>
    /// <param name="expectedCenterOfMotion"></param>
    /// <returns></returns>
    public static OrbitalParameters CreateFromObservation_Gauss(Equatorial observation1, Equatorial observation2, Equatorial observation3, ILocalizable observer,
        CelestialItem expectedCenterOfMotion)
    {
        // Step 1: Compute observer positions dynamically using ephemeris
        Vector3 R1 = observer
            .GetEphemeris(observation1.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / 1000.0;

        Vector3 R2 = observer
            .GetEphemeris(observation2.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / 1000.0;

        Vector3 R3 = observer
            .GetEphemeris(observation3.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / 1000.0;

        // Gravitational parameter of the expected central body
        double mu = expectedCenterOfMotion.GM / 1E09;

        // Step 2: Convert equatorial coordinates to unit direction vectors
        Vector3 rhoHat1 = observation1.ToCartesian().Normalize();
        Vector3 rhoHat2 = observation2.ToCartesian().Normalize();
        Vector3 rhoHat3 = observation3.ToCartesian().Normalize();

        // Step 3: Extract observation times
        Time t1 = observation1.Epoch;
        Time t2 = observation2.Epoch;
        Time t3 = observation3.Epoch;

        // Step 4: Compute scalar coefficients A, B, E
        double tau1 = (t1 - t2).TotalSeconds;
        double tau3 = (t3 - t2).TotalSeconds;
        double tau = tau3 - tau1;

        Vector3 p1 = rhoHat2.Cross(rhoHat3);
        Vector3 p2 = rhoHat1.Cross(rhoHat3);
        Vector3 p3 = rhoHat1.Cross(rhoHat2);

        double d0 = rhoHat1 * p1;
        double d11 = R1 * p1;
        double d12 = R1 * p2;
        double d13 = R1 * p3;
        double d21 = R2 * p1;
        double d22 = R2 * p2;
        double d23 = R2 * p3;
        double d31 = R3 * p1;
        double d32 = R3 * p2;
        double d33 = R3 * p3;

        double A = (1.0 / d0) * (-d12 * (tau3 / tau) + d22 + d32 * (tau1 / tau));
        double B = (1.0 / (6.0 * d0)) *
                   (d12 * ((System.Math.Pow(tau3, 2) - System.Math.Pow(tau, 2)) * (tau3 / tau)) +
                    d32 * ((System.Math.Pow(tau, 2) - System.Math.Pow(tau1, 2)) * (tau1 / tau)));

        double E = R2 * rhoHat2;

        // Step 5: Solve the polynomial for rho2
        double R2Squared = R2 * R2;
        double polynomialA = -(A * A + 2.0 * A * E + R2Squared);
        double polynomialB = -2.0 * mu * B * (A + E);
        double polynomialC = -(mu * mu) * (B * B);

        Func<double, double> function = r2 => System.Math.Pow(r2, 8) + polynomialA * System.Math.Pow(r2, 6) + polynomialB * System.Math.Pow(r2, 3) + polynomialC;

        // Define the derivative f'(r2)
        Func<double, double> derivative = r2 => 8.0 * System.Math.Pow(r2, 7.0) + 6.0 * polynomialA * System.Math.Pow(r2, 5.0) + 3.0 * polynomialB * System.Math.Pow(r2, 2.0);

        // Initial guess for r2
        double root_distance = NewtonRaphson.Solve(function, derivative, 40000.0, 1E-12, 1000);


        // Step 6: Compute rho1 and rho3
        double rho1 = (1 / d0) * (
            (6 * (d31 * tau1 / tau3 + d21 * tau / tau3) * System.Math.Pow(root_distance, 3) +
             mu * d31 * (tau * tau - tau1 * tau1) * tau1 / tau3) /
            (6 * System.Math.Pow(root_distance, 3) + mu * (tau * tau - tau3 * tau3)) -
            d11
        );

        // Calculate ρ₃ (rho3)
        double rho3 = (1 / d0) * (
            (6 * (d13 * tau3 / tau1 - d23 * tau / tau1) * System.Math.Pow(root_distance, 3) +
             mu * d13 * (tau * tau - tau3 * tau3) * tau3 / tau1) /
            (6 * System.Math.Pow(root_distance, 3) + mu * (tau * tau - tau1 * tau1)) -
            d33
        );

        // Calculate ρ₂ (rho2)
        double rho2 = A + (mu * B) / System.Math.Pow(root_distance, 3);


        // Step 7: Compute position vectors
        Vector3 r1 = rhoHat1 * rho1 + R1;
        Vector3 r2 = rhoHat2 * rho2 + R2;
        Vector3 r3 = rhoHat3 * rho3 + R3;

        // Step 8: Compute Lagrange coefficients
        double f1 = 1 - (mu * tau1 * tau1) / (2 * System.Math.Pow(r2.Magnitude(), 3));
        double f3 = 1 - (mu * tau3 * tau3) / (2 * System.Math.Pow(r2.Magnitude(), 3));
        double g1 = tau1 - (System.Math.Pow(tau1, 3) * mu) / (6 * System.Math.Pow(r2.Magnitude(), 3));
        double g3 = tau3 - (System.Math.Pow(tau3, 3) * mu) / (6 * System.Math.Pow(r2.Magnitude(), 3));

        // Step 9: Compute velocity vector using full Lagrange formulation
        Vector3 v2 = (r1 * f3 - r3 * f1) / (f3 * g1 - f1 * g3);

        // Step 10: Convert position and velocity to Keplerian elements
        var stateVector = new StateVector(r2 * 1000.0, v2 * 1000.0, expectedCenterOfMotion, t2, Frame.ICRF);
        return stateVector.ToKeplerianElements();
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