using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

public class Frame : IEquatable<Frame>
{
    private readonly IDataProvider _dataProvider;
    public string Name { get; }
    public int? Id { get; }

    protected ConcurrentDictionary<Time, StateOrientation> _stateOrientationsToICRF = new();
    protected ImmutableSortedDictionary<Time, StateOrientation> StateOrientationsToICRF => _stateOrientationsToICRF.ToImmutableSortedDictionary();

    /// <summary>
    /// Optional pre-computed orientation cache for avoiding SPICE calls during propagation.
    /// Set by the propagator before integration begins; cleared after propagation completes.
    /// </summary>
    internal PropagationFrameOrientationCache OrientationCache { get; set; }

    /// <summary>
    /// International Celestial Reference Frame (ICRF) at epoch J2000.
    /// </summary>
    public static readonly Frame ICRF = new Frame("J2000");

    /// <summary>
    /// Ecliptic coordinate system at epoch B1950.
    /// </summary>
    public static readonly Frame ECLIPTIC_B1950 = new Frame("ECLIPB1950");

    /// <summary>
    /// Ecliptic coordinate system at epoch J2000.
    /// </summary>
    public static readonly Frame ECLIPTIC_J2000 = new Frame("ECLIPJ2000");

    /// <summary>
    /// Galactic coordinate system.
    /// </summary>
    public static readonly Frame GALACTIC_SYSTEM2 = new Frame("GALACTIC");

    /// <summary>
    /// Equatorial coordinate system at epoch B1950.
    /// </summary>
    public static readonly Frame B1950 = new Frame("B1950");

    /// <summary>
    /// Fourth Fundamental Catalog (FK4) coordinate system.
    /// </summary>
    public static readonly Frame FK4 = new Frame("FK4");

    /// <summary>
    /// True Equator Mean Equinox (TEME) coordinate system.
    /// </summary>
    public static readonly Frame TEME = new Frame("TEME");

    public Frame(string name, int? id = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Frame must have a name");
        }

        _dataProvider = Configuration.Instance.DataProvider;

        Name = name;
        Id = id;
    }

    /// <summary>
    /// Returns the orientation that maps vectors expressed in <c>this</c> frame into ICRF at the given epoch.
    /// </summary>
    /// <remarks>
    /// If <c>so = frame.GetStateOrientationToICRF(epoch)</c>, then
    /// <c>vectorIcrf = vectorInFrame.Rotate(so.Rotation)</c>.
    /// The returned <see cref="StateOrientation.ReferenceFrame"/> is the source frame (<c>this</c>),
    /// not the destination frame.
    /// </remarks>
    /// <param name="date">Epoch of the transform.</param>
    /// <returns>A state orientation whose rotation is <c>this → ICRF</c>.</returns>
    public virtual StateOrientation GetStateOrientationToICRF(Time date)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(date);
        return _stateOrientationsToICRF.GetOrAdd(date, dt => _dataProvider.FrameTransformationToICRF(dt, this));
    }

    public bool AddStateOrientationToICRF(StateOrientation stateOrientation)
    {
        return _stateOrientationsToICRF.TryAdd(stateOrientation.Epoch, stateOrientation);
    }

    public StateOrientation GetLatestStateOrientationToICRF()
    {
        return _stateOrientationsToICRF.Values.OrderBy(x => x.Epoch).LastOrDefault();
    }

    public IEnumerable<StateOrientation> GetStateOrientationsToICRF()
    {
        return _stateOrientationsToICRF.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
    }

    /// <summary>
    /// Builds the transform that maps vectors from <c>this</c> frame into <paramref name="targetFrame"/>.
    /// </summary>
    /// <remarks>
    /// The returned rotation follows the same convention as <see cref="GetStateOrientationToICRF(Time)"/>:
    /// <c>vectorInTarget = vectorInThis.Rotate(result.Rotation)</c>.
    /// </remarks>
    public StateOrientation ToFrame(Frame targetFrame, Time epoch)
    {
        if (targetFrame == this)
        {
            return new StateOrientation(Quaternion.Zero, Vector3.Zero, epoch, targetFrame);
        }

        var sourceToICRF = GetStateOrientationToICRF(epoch);
        var targetToICRF = targetFrame.GetStateOrientationToICRF(epoch);

        var rotation = targetToICRF.Rotation.Conjugate() * sourceToICRF.Rotation;

        var angularVelocity = targetToICRF.AngularVelocity.Inverse().Rotate(rotation.Conjugate()) + sourceToICRF.AngularVelocity;

        return new StateOrientation(rotation, angularVelocity, epoch, this);
    }

    public void ClearStateOrientations()
    {
        _stateOrientationsToICRF.Clear();
    }

    #region Operators

    public override string ToString()
    {
        return Name;
    }

    public bool Equals(Frame other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Frame)obj);
    }

    public override int GetHashCode()
    {
        return (Name != null ? Name.GetHashCode() : 0);
    }

    public static bool operator ==(Frame left, Frame right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Frame left, Frame right)
    {
        return !Equals(left, right);
    }

    #endregion
}
