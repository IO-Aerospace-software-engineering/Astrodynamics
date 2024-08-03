using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.OrbitalParameters;

public abstract class OrbitalParameters : IEquatable<OrbitalParameters>
{
    public ILocalizable Observer { get; }

    public DateTime Epoch { get; }

    public Frame Frame { get; }
    protected Vector3? _eccentricVector;
    protected Vector3? _specificAngularMomentum;
    protected double? _specificOrbitalEnergy;
    protected Vector3? _ascendingNodeVector;
    protected Vector3? _decendingNodeVector;
    protected TimeSpan? _period;
    protected double? _meanMotion;
    protected StateVector _stateVector;
    protected EquinoctialElements _equinoctial;
    protected Vector3? _perigeevector;
    protected Vector3? _apogeeVector;
    protected double? _trueLongitude;
    protected double? _meanLongitude;
    protected bool? _isCircular;
    protected bool? _isParabolic;
    protected bool? _isHyperbolic;
    protected KeplerianElements _keplerianElements;
    protected Equatorial? _equatorial;
    protected double? _perigeeVelocity;
    protected double? _apogeeVelocity;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="observer"></param>
    /// <param name="epoch"></param>
    /// <param name="frame"></param>
    protected OrbitalParameters(ILocalizable observer, in DateTime epoch, Frame frame)
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
    }

    /// <summary>
    /// Get eccentric vector
    /// </summary>
    /// <returns></returns>
    public virtual Vector3 EccentricityVector()
    {
        _eccentricVector ??= ToStateVector().EccentricityVector();

        return _eccentricVector.Value;
    }

    /// <summary>
    /// Get eccentricity
    /// </summary>
    /// <returns></returns>
    public abstract double Eccentricity();

    /// <summary>
    /// Get the specific angular momentum
    /// </summary>
    /// <returns></returns>
    public virtual Vector3 SpecificAngularMomentum()
    {
        _specificAngularMomentum ??= ToStateVector().SpecificAngularMomentum();
        return _specificAngularMomentum.Value;
    }

    /// <summary>
    /// Get the specific orbital energy in MJ
    /// </summary>
    /// <returns></returns>
    public virtual double SpecificOrbitalEnergy()
    {
        _specificOrbitalEnergy ??= ToStateVector().SpecificOrbitalEnergy();
        return _specificOrbitalEnergy.Value;
    }

    /// <summary>
    /// Get inclination
    /// </summary>
    /// <returns></returns>
    public abstract double Inclination();

    /// <summary>
    /// Get the semi major axis
    /// </summary>
    /// <returns></returns>
    public abstract double SemiMajorAxis();

    /// <summary>
    /// Get vector to ascending node unitless
    /// </summary>
    /// <returns></returns>
    public virtual Vector3 AscendingNodeVector()
    {
        if (Inclination() == 0.0)
        {
            return Vector3.VectorX;
        }

        _ascendingNodeVector ??= ToStateVector().AscendingNodeVector();
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
    public abstract double AscendingNode();

    /// <summary>
    /// Get the argument of periapis
    /// </summary>
    /// <returns></returns>
    public abstract double ArgumentOfPeriapsis();

    /// <summary>
    /// Get the true anomaly
    /// </summary>
    /// <returns></returns>
    public abstract double TrueAnomaly();

    /// <summary>
    /// Return true anomaly at given epoch
    /// </summary>
    /// <param name="epoch"></param>
    /// <returns></returns>
    public double TrueAnomaly(DateTime epoch)
    {
        return AtEpoch(epoch).TrueAnomaly();
    }

    /// <summary>
    /// Get the eccentric anomaly
    /// </summary>
    /// <returns></returns>
    public abstract double EccentricAnomaly();

    /// <summary>
    /// Get the mean anomaly
    /// </summary>
    /// <returns></returns>
    public abstract double MeanAnomaly();

    /// <summary>
    /// Compute mean anomaly from true anomaly
    /// </summary>
    /// <param name="trueAnomaly"></param>
    /// <returns></returns>
    public double MeanAnomaly(double trueAnomaly)
    {
        return TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity());
    }

    public static double TrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity)
    {
        if (trueAnomaly < 0.0)
        {
            trueAnomaly += Constants._2PI;
        }

        //X = cos E
        double x = (eccentricity + System.Math.Cos(trueAnomaly)) / (1 + eccentricity * System.Math.Cos(trueAnomaly));
        double eccAno = System.Math.Acos(x);
        double M = eccAno - eccentricity * System.Math.Sin(eccAno);

        if (trueAnomaly > Constants.PI)
        {
            M = Constants._2PI - M;
        }

        return M;
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
        _meanMotion ??= Constants._2PI / Period().TotalSeconds;
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
            var v = TrueAnomaly();
            var r0 = p / (1 + e * System.Math.Cos(v));
            var x = r0 * System.Math.Cos(v);
            var y = r0 * System.Math.Sin(v);
            var dotX = -System.Math.Sqrt(Observer.GM / p) * System.Math.Sin(v);
            var dotY = System.Math.Sqrt(Observer.GM / p) * (e + System.Math.Cos(v));
            Matrix R3 = Matrix.CreateRotationMatrixZ(AscendingNode());
            Matrix R1 = Matrix.CreateRotationMatrixX(Inclination());
            Matrix R3w = Matrix.CreateRotationMatrixZ(ArgumentOfPeriapsis());
            Matrix R = R3 * R1 * R3w;
            double[] pos = { x, y, 0.0 };
            double[] vel = { dotX, dotY, 0.0 };
            double[] finalPos = pos * R;
            double[] finalV = vel * R;

            _stateVector = new StateVector(new Vector3(finalPos[0], finalPos[1], finalPos[2]), new Vector3(finalV[0], finalV[1], finalV[2]), Observer, Epoch, Frame);
        }

        return _stateVector;
    }

    public virtual StateVector ToStateVector(DateTime epoch)
    {
        return AtEpoch(epoch).ToStateVector();
    }

    public virtual StateVector ToStateVector(double trueAnomaly)
    {
        return ToStateVector(EpochAtMeanAnomaly(TrueAnomalyToMeanAnomaly(trueAnomaly, Eccentricity())));
    }

    public DateTime EpochAtMeanAnomaly(double meanAnomaly)
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
    public virtual OrbitalParameters AtEpoch(DateTime epoch)
    {
        return ToKeplerianElements(epoch);
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
    public double TrueLongitude(DateTime epoch)
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

    private KeplerianElements ToKeplerianElements(DateTime epoch)
    {
        double ellapsedTime = epoch.SecondsFromJ2000TDB() - Epoch.SecondsFromJ2000TDB();
        double M = MeanAnomaly() + MeanMotion() * ellapsedTime;
        while (M < 0.0)
        {
            M += Constants._2PI;
        }

        return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), M % Constants._2PI, Observer, epoch, Frame);
    }

    public virtual KeplerianElements ToKeplerianElements()
    {
        _keplerianElements ??= ToKeplerianElements(Epoch);
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