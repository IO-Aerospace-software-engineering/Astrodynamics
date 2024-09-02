using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters;

public abstract class OrbitalParameters : IEquatable<OrbitalParameters>
{
    public ILocalizable Observer { get; }

    public Time Epoch { get; }

    public Frame Frame { get; }
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
    private KeplerianElements _keplerianElements;
    private Equatorial? _equatorial;
    private double? _perigeeVelocity;
    private double? _apogeeVelocity;
    private double? _ascendingNode;
    protected double? _trueAnomaly;
    private double? _eccentricAnomaly;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="observer"></param>
    /// <param name="epoch"></param>
    /// <param name="frame"></param>
    protected OrbitalParameters(ILocalizable observer, in Time epoch, Frame frame)
    {
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        Epoch = epoch;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
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
    }

    /// <summary>
    /// Get eccentric vector
    /// </summary>
    /// <returns></returns>
    public Vector3 EccentricityVector()
    {
        if (_eccentricVector.HasValue)
        {
            return _eccentricVector!.Value;
        }

        var sv = ToStateVector();
        _eccentricVector = (sv.Velocity.Cross(SpecificAngularMomentum()) / Observer.GM) - sv.Position.Normalize();
        return _eccentricVector.Value;
    }

    /// <summary>
    /// Get eccentricity
    /// </summary>
    /// <returns></returns>
    public double Eccentricity()
    {
        return ToKeplerianElements().E;
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

        _specificOrbitalEnergy = ToStateVector().SpecificOrbitalEnergy();
        return _specificOrbitalEnergy.Value;
    }

    /// <summary>
    /// Get inclination
    /// </summary>
    /// <returns></returns>
    public double Inclination()
    {
        return ToKeplerianElements().I;
    }

    /// <summary>
    /// Get the semi major axis
    /// </summary>
    /// <returns></returns>
    public double SemiMajorAxis()
    {
        return ToKeplerianElements().A;
    }

    /// <summary>
    /// Get vector to ascending node unitless
    /// </summary>
    /// <returns></returns>
    public Vector3 AscendingNodeVector()
    {
        if (_ascendingNodeVector.HasValue)
        {
            return _ascendingNodeVector.Value;
        }

        if (Inclination() == 0.0)
        {
            _ascendingNodeVector = Vector3.VectorX;
            return _ascendingNodeVector!.Value;
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
    /// Get ascending node angle
    /// </summary>
    /// <returns></returns>
    public double AscendingNode()
    {
        if (_ascendingNode.HasValue)
        {
            return _ascendingNode.Value;
        }

        Vector3 n = AscendingNodeVector();

        if (n.Magnitude() == 0.0)
        {
            _ascendingNode = 0.0;
            return _ascendingNode.Value;
        }

        var omega = System.Math.Acos(n.X / n.Magnitude());


        if (n.Y < 0.0)
        {
            omega = 2 * System.Math.PI - omega;
        }

        _ascendingNode = omega;


        return _ascendingNode.Value;
    }

    /// <summary>
    /// Get the argument of periapis
    /// </summary>
    /// <returns></returns>
    public double ArgumentOfPeriapsis()
    {
        return ToKeplerianElements().AOP;
    }

    /// <summary>
    /// Get the true anomaly
    /// </summary>
    /// <returns></returns>
    public double TrueAnomaly()
    {
        if (_trueAnomaly.HasValue)
        {
            return _trueAnomaly.Value;
        }

        _trueAnomaly = ToStateVector().ToKeplerianElements().TrueAnomaly();
        return _trueAnomaly.Value;
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

        double v = TrueAnomaly();
        double e = Eccentricity();

        if (e < 1)
        {
            _eccentricAnomaly ??= 2 * System.Math.Atan((System.Math.Tan(v / 2.0)) / System.Math.Sqrt((1 + e) / (1 - e)));
        }
        else if (e > 1)
        {
            var term1 = (e - 1) / (e + 1);
            var term2 = System.Math.Sqrt(term1);
            var term3 = System.Math.Tan(v * 0.5);
            var term4 = System.Math.Atanh(term2 * term3);
            _eccentricAnomaly = 2 * term4;
        }
        else
        {
            _eccentricAnomaly = System.Math.Tan(v * 0.5);
        }

        return _eccentricAnomaly.Value;
    }

    /// <summary>
    /// Get the mean anomaly
    /// </summary>
    /// <returns></returns>
    public double MeanAnomaly()
    {
        return ToKeplerianElements().M;
    }

    /// <summary>
    /// Compute mean anomaly from true anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    public double MeanAnomaly(double trueAnomaly)
    {
        return TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity(), this.EccentricAnomaly());
    }

    public static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity, double eccentricAnomaly)
    {
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants._2PI;
        }

        if (eccentricity < 1)
        {
            return eccentricAnomaly - eccentricity * System.Math.Sin(eccentricAnomaly);
        }
        else if (eccentricity > 1)
        {
            return eccentricity * System.Math.Sinh(eccentricAnomaly) - eccentricAnomaly;
        }
        else
        {
            return eccentricAnomaly + System.Math.Pow(eccentricAnomaly, 3) / 3.0;
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
    public double MeanMotion()
    {
        if (_meanMotion.HasValue)
        {
            return _meanMotion.Value;
        }

        var e = Eccentricity();

        if (e < 1)
        {
            _meanMotion ??= Constants._2PI / Period().TotalSeconds;
        }
        else if (e > 1)
        {
            var ai = (e - 1) / PerigeeVector().Magnitude();
            _meanMotion = System.Math.Sqrt(Observer.GM * ai) * ai;
        }
        else
        {
            _meanMotion = System.Math.Sqrt(Observer.GM / (PerigeeVector().Magnitude() * 2.0)) / PerigeeVector().Magnitude();
        }

        return _meanMotion.Value;
    }


    /// <summary>
    /// Get the state vector
    /// </summary>
    /// <returns></returns>
    public abstract StateVector ToStateVector();

    public virtual StateVector ToStateVector(Time epoch)
    {
        return AtEpoch(epoch).ToStateVector();
    }

    public virtual StateVector ToStateVector(double trueAnomaly)
    {
        return ToStateVector(EpochAtMeanAnomaly(TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity(), EccentricAnomaly())));
    }

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

            _equinoctial = new EquinoctialElements(p, f, g, h, k, l0, Observer, Epoch, Frame);
        }

        return _equinoctial;
    }

    /// <summary>
    /// Get perigee vector
    /// </summary>
    /// <returns></returns>
    public Vector3 PerigeeVector()
    {
        if (Eccentricity() == 0.0)
        {
            return Vector3.VectorX * SemiMajorAxis();
        }

        _perigeevector ??= EccentricityVector().Normalize() * SemiMajorAxis() * (1.0 - Eccentricity());
        return _perigeevector.Value;
    }

    /// <summary>
    /// Get apogee vector
    /// </summary>
    /// <returns></returns>
    public Vector3 ApogeeVector()
    {
        if (Eccentricity() == 0.0)
        {
            return Vector3.VectorX.Inverse() * SemiMajorAxis();
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
        var sv = this.ToStateVector();
        return API.Instance.Propagate2Bodies(sv, epoch);
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
        _trueLongitude ??= (AscendingNode() + ArgumentOfPeriapsis() + TrueAnomaly(epoch)) % Constants._2PI;
        return _trueLongitude.Value;
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

    public bool IsCircular()
    {
        _isCircular ??= Eccentricity() < 1E-03;
        return _isCircular.Value;
    }

    public bool IsParabolic()
    {
        _isParabolic ??= System.Math.Abs(Eccentricity() - 1.0) < double.Epsilon;
        return _isParabolic.Value;
    }

    public bool IsHyperbolic()
    {
        _isHyperbolic ??= Eccentricity() > 1.0;
        return _isHyperbolic.Value;
    }

    private KeplerianElements ToKeplerianElements(Time epoch)
    {
        return ToStateVector(epoch).ToKeplerianElements();
    }

    public virtual KeplerianElements ToKeplerianElements()
    {
        if (_keplerianElements is not null)
        {
            return _keplerianElements;
        }

        _keplerianElements = ToKeplerianElements(Epoch);
        return _keplerianElements;
    }

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

    public Equatorial ToEquatorial()
    {
        _equatorial ??= new Equatorial(ToStateVector());
        return _equatorial.Value;
    }

    public double PerigeeVelocity()
    {
        _perigeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / PerigeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _perigeeVelocity.Value;
    }

    public double ApogeeVelocity()
    {
        _apogeeVelocity ??= System.Math.Sqrt(Observer.GM * (2 / ApogeeVector().Magnitude() - 1.0 / SemiMajorAxis()));
        return _apogeeVelocity.Value;
    }

    public OrbitalParameters RelativeTo(ILocalizable localizable, Aberration aberration)
    {
        if (localizable.NaifId == this.Observer.NaifId)
        {
            return this;
        }

        var eph = localizable.GetEphemeris(Epoch, Observer, Frame, aberration).ToStateVector();
        var position = (eph.Position - ToStateVector().Position).Inverse();
        var velocity = (eph.Velocity - ToStateVector().Velocity).Inverse();
        return new StateVector(position, velocity, localizable, eph.Epoch, Frame);
    }

    public Planetocentric ToPlanetocentric(Aberration aberration)
    {
        var body = Observer as CelestialBody;
        return ((CelestialBody)Observer).SubObserverPoint(ToFrame(body!.Frame).ToStateVector().Position, Epoch, aberration);
    }

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
}