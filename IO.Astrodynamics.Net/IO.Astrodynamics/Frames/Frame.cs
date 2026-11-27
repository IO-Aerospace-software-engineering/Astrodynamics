using System;
using System.Threading;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

public class Frame : IEquatable<Frame>
{
    // The provider of a frame built by name (the provider set at its construction), null for the static SPICE frames of this
    // class, which read the provider of the process at each transform (structuring choice 1 of #668)
    private readonly IDataProvider _dataProvider;
    public string Name { get; }
    public int? Id { get; }

    // The orientation of the last epoch the frame read (LastOrientation), replaced at each read of another epoch or after a
    // change of the kernel pool: a frame no longer keeps one orientation per epoch read, for its life (#541)
    private LastOrientationEntry _lastOrientation;
    private readonly Func<Time, StateOrientation> _readFromProvider;

    private sealed record LastOrientationEntry(Time Date, long KernelPoolGeneration, IDataProvider Provider, StateOrientation Orientation);

    // The orientation cache active on the current thread, and the frame it belongs to (FrameOrientationCacheScope)
    [ThreadStatic] private static Frame _cachedFrame;
    [ThreadStatic] private static PropagationFrameOrientationCache _activeCache;

    /// <summary>
    /// The pre-computed orientation cache that replaces the SPICE calls of this frame on the current thread, or null.
    /// </summary>
    /// <remarks>
    /// Active only on the thread that propagates or evaluates a propagation, for its duration
    /// (<see cref="FrameOrientationCacheScope"/>): the cache never sits on the shared <see cref="Frame"/> instance, so
    /// concurrent propagations and evaluations that share a central body do not see each other's cache.
    /// </remarks>
    internal PropagationFrameOrientationCache OrientationCache =>
        ReferenceEquals(_cachedFrame, this) ? _activeCache : null;

    /// <summary>
    /// Make <paramref name="cache"/> the orientation cache of <paramref name="frame"/> on the current thread until the
    /// returned scope is disposed, which restores the previous one. A null frame or cache leaves the thread unchanged.
    /// </summary>
    internal static FrameOrientationCacheScope UseOrientationCache(Frame frame, PropagationFrameOrientationCache cache)
    {
        if (frame == null || cache == null)
        {
            return default;
        }

        var scope = new FrameOrientationCacheScope(_cachedFrame, _activeCache);
        _cachedFrame = frame;
        _activeCache = cache;
        return scope;
    }

    /// <summary>
    /// The orientation cache of a frame on the current thread, restored when disposed. A value type, so entering and
    /// leaving a scope allocates nothing.
    /// </summary>
    internal readonly struct FrameOrientationCacheScope : IDisposable
    {
        private readonly bool _active;
        private readonly Frame _previousFrame;
        private readonly PropagationFrameOrientationCache _previousCache;

        internal FrameOrientationCacheScope(Frame previousFrame, PropagationFrameOrientationCache previousCache)
        {
            _active = true;
            _previousFrame = previousFrame;
            _previousCache = previousCache;
        }

        public void Dispose()
        {
            if (!_active) return;
            _cachedFrame = _previousFrame;
            _activeCache = _previousCache;
        }
    }

    /// <summary>
    /// International Celestial Reference Frame (ICRF), built on the SPICE frame <c>J2000</c>.
    /// It is the pivot of every frame transformation in the library.
    /// </summary>
    /// <remarks>
    /// SPICE names this frame <c>J2000</c> and treats it as aligned with the ICRF; the DE planetary
    /// ephemerides it carries are ICRF-aligned. The library follows the same convention: the ICRF has
    /// no epoch, and <see cref="GCRF"/> shares its axes. The ~23 mas IAU 2006 frame bias separates the
    /// ICRF from the mean equator and equinox of J2000 (EME2000, the dynamical frame); no frame of the
    /// library models EME2000 separately, so data labelled EME2000 is read in this frame, ignoring the
    /// bias (about 0.8 m on a LEO position, 4.7 m on a GEO position).
    /// </remarks>
    public static readonly Frame ICRF = ProcessProviderFrame("J2000");

    /// <summary>
    /// Ecliptic coordinate system at epoch B1950.
    /// </summary>
    public static readonly Frame ECLIPB1950 = ProcessProviderFrame("ECLIPB1950");

    /// <summary>
    /// Ecliptic coordinate system at epoch J2000.
    /// </summary>
    public static readonly Frame ECLIPJ2000 = ProcessProviderFrame("ECLIPJ2000");

    /// <summary>
    /// Galactic coordinate system.
    /// </summary>
    public static readonly Frame GALACTIC = ProcessProviderFrame("GALACTIC");

    /// <summary>
    /// Equatorial coordinate system at epoch B1950.
    /// </summary>
    public static readonly Frame B1950 = ProcessProviderFrame("B1950");

    /// <summary>
    /// Fourth Fundamental Catalog (FK4) coordinate system.
    /// </summary>
    public static readonly Frame FK4 = ProcessProviderFrame("FK4");

    /// <summary>
    /// True Equator, Mean Equinox (TEME), the frame of the SGP4/SDP4 states of a TLE: the managed
    /// <see cref="TemeFrame"/>, TEME → ICRF = Q(t)ᵀ · Rz(ERA − GMST82) with IAU 2006/2000B and UT1 = UTC, quasi-inertial
    /// (v' = R v). It reads no kernel, so it works at any epoch (#451). A frame built by name, <c>new Frame("TEME")</c>,
    /// is routed to it by the data providers.
    /// </summary>
    public static readonly Frame TEME = new TemeFrame();

    /// <summary>
    /// Geocentric Celestial Reference Frame (GCRF), the ICRS axes centred on the Earth.
    /// It shares the axes of <see cref="ICRF"/>: the rotation between the two is the identity.
    /// It is the celestial end of the IAU CIO-based chain GCRF → <see cref="CIRS"/> → <see cref="TIRS"/>.
    /// </summary>
    public static readonly Frame GCRF = new GcrfFrame();

    /// <summary>
    /// Celestial Intermediate Reference System (CIRS), CIO-based intermediate frame.
    /// </summary>
    public static readonly Frame CIRS = new CirsFrame();

    /// <summary>
    /// Terrestrial Intermediate Reference System (TIRS), obtained from CIRS by Earth rotation.
    /// Uses <see cref="NullEop"/> (UT1 = UTC), so it is off by the actual UT1 - UTC, within 0.9 s: up to about
    /// 13.5 arcseconds of Earth rotation, about 420 m for a point fixed on the equator. Build a
    /// <see cref="TirsFrame"/> directly to supply real Earth orientation parameters.
    /// </summary>
    public static readonly Frame TIRS = new TirsFrame();

    /// <summary>
    /// The International Terrestrial Reference Frame ITRF93, the SPICE name of the Earth's precise body-fixed frame, which
    /// <c>earth_latest_high_prec.bpc</c> orients and <c>earth_assoc_itrf93.tf</c> makes the frame of the Earth: equal to
    /// <c>new Frame("ITRF93")</c> and, with those kernels, to the frame of <c>new CelestialBody(399)</c> (R25 of #668).
    /// </summary>
    /// <remarks>Its orientation needs the Earth's binary PCK: outside its coverage a transform throws
    /// <see cref="InvalidOperationException"/>.</remarks>
    public static readonly Frame ITRF93 = ProcessProviderFrame("ITRF93");

    /// <summary>
    /// A static SPICE frame of this class: it reads the data provider of the process (<see cref="Configuration.DataProvider"/>)
    /// at each transform, where a frame built by name reads the provider set at its construction (structuring choice 1 of
    /// #668, decided by S. Guillet, 2026-11-19; before 11.0.0 the static frames read the provider set when the type was first
    /// used, for the life of the process).
    /// </summary>
    private static Frame ProcessProviderFrame(string name) => new(name, followsProcessProvider: true);

    private Frame(string name, bool followsProcessProvider) : this(name)
    {
        if (followsProcessProvider) _dataProvider = null;
    }

    // The provider this frame reads: its own, or the process's for a static SPICE frame
    private IDataProvider Provider => _dataProvider ?? Configuration.Instance.DataProvider;

    public Frame(string name, int? id = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Frame must have a name");
        }

        _dataProvider = Configuration.Instance.DataProvider;

        Name = name;
        Id = id;
        _readFromProvider = date => ReadFromProvider(Provider, date);
    }

    /// <summary>
    /// The orientation of this frame to ICRF read from <paramref name="provider"/>: an unknown name of the frame, which is no
    /// argument of the public call that reads it, names no parameter (R25 of #668, open choice 1; review of L1: the
    /// provider's <c>source</c> before).
    /// </summary>
    private protected StateOrientation ReadFromProvider(IDataProvider provider, in Time date)
    {
        try
        {
            return provider.FrameTransformationToIcrf(date, this);
        }
        catch (ArgumentException e) when (SpiceApi.IsUnknownFrameErrorOfAnotherParameter(e, this, null))
        {
            throw SpiceApi.UnknownFrameError(e, this, null);
        }
    }

    /// <summary>
    /// Returns the orientation that maps vectors expressed in <c>this</c> frame into ICRF at the given epoch.
    /// </summary>
    /// <remarks>
    /// If <c>so = frame.GetStateOrientationToIcrf(epoch)</c>, then
    /// <c>vectorIcrf = vectorInFrame.Rotate(so.Rotation)</c>.
    /// The returned <see cref="StateOrientation.ReferenceFrame"/> is the source frame (<c>this</c>),
    /// not the destination frame.
    /// <see cref="StateOrientation.AngularVelocity"/> follows the SPICE convention (<c>xf2rav_c</c>): it is the
    /// angular velocity of ICRF relative to <c>this</c> frame, expressed in <c>this</c> frame, in rad/s. For an
    /// Earth-fixed frame it is close to (0, 0, -7.292e-5).
    /// </remarks>
    /// <param name="epoch">Epoch of the transform.</param>
    /// <returns>A state orientation whose rotation is <c>this → ICRF</c>.</returns>
    /// <remarks>
    /// Outside the orientation cache of a propagation, a frame keeps the orientation of the last epoch it read only, while
    /// the kernel pool is unchanged (<c>SpiceApi.LoadKernels</c>, <c>UnloadKernels</c> and
    /// <c>ClearKernels</c> make the next read read again), and so do the frames the library computes
    /// (<see cref="CirsFrame"/>, <see cref="TirsFrame"/>, <see cref="TemeFrame"/>, the frame of a site from coordinates):
    /// at most one orientation per frame, whatever the number of epochs read (#541, decided by S. Guillet, 2026-11-17).
    /// Before 11.0.0 each orientation read was kept, by epoch, for the life of the frame (16.5 MB after 100 000 reads of
    /// ITRF93), and the static frames live for the whole process. An orientation added to a <see cref="MemoryDataProvider"/>
    /// at the epoch of the frame's last read is read once the frame has read another epoch.
    /// </remarks>
    /// <exception cref="ArgumentException">SPICE knows no frame of the name of this frame: the message names the frame,
    /// SPICE's error its inner exception, and no parameter is named (R25 of #668, decided by S. Guillet, 2026-11-19; before
    /// 11.0.0 an <see cref="InvalidOperationException"/>).</exception>
    /// <exception cref="InvalidOperationException">The data do not give the orientation of the frame at the epoch.</exception>
    public virtual StateOrientation GetStateOrientationToIcrf(Time epoch)
    {
        if (OrientationCache != null)
            return OrientationCache.GetOrientation(epoch);
        return LastOrientation(epoch, _readFromProvider);
    }

    /// <summary>
    /// The orientation of this frame to ICRF read from the data provider, not through the orientation cache active on the
    /// current thread: what <see cref="GetStateOrientationToIcrf(Time)"/> of a <see cref="Frame"/> reads when the cache does
    /// not hold the epoch. Used by <see cref="PropagationFrameOrientationCache"/> where its grid cannot give an orientation.
    /// </summary>
    internal StateOrientation ReadStateOrientationToICRF(in Time date)
    {
        return ReadFromProvider(Provider, date);
    }

    /// <summary>
    /// The orientation of this frame at <paramref name="date"/>, kept from its last read when that was at the same epoch and
    /// the same generation of the kernel pool (<see cref="SpiceApi.KernelPoolGeneration"/>), else read by
    /// <paramref name="read"/> and kept in place of the previous one: at most one orientation per frame, whatever the
    /// number of epochs read (#541). The entry is replaced as a whole, so a read on another thread sees either entry, each
    /// consistent.
    /// </summary>
    private protected StateOrientation LastOrientation(in Time date, Func<Time, StateOrientation> read)
    {
        long generation = SpiceApi.KernelPoolGeneration;
        // The provider read: a static frame's follows the process, so an orientation of another provider is read again
        var provider = Provider;
        var last = Volatile.Read(ref _lastOrientation);
        if (last != null && last.KernelPoolGeneration == generation && ReferenceEquals(last.Provider, provider) && last.Date.EqualsExact(date))
        {
            return last.Orientation;
        }

        var orientation = read(date);
        Volatile.Write(ref _lastOrientation, new LastOrientationEntry(date, generation, provider, orientation));
        return orientation;
    }

    /// <summary>
    /// Builds the transform that maps vectors from <c>this</c> frame into <paramref name="targetFrame"/>.
    /// </summary>
    /// <remarks>
    /// The returned rotation follows the same convention as <see cref="GetStateOrientationToIcrf(Time)"/>:
    /// <c>vectorInTarget = vectorInThis.Rotate(result.Rotation)</c>.
    /// The returned angular velocity is that of <paramref name="targetFrame"/> relative to <c>this</c> frame,
    /// expressed in <c>this</c> frame (SPICE <c>xf2rav_c</c> convention), in rad/s. A state transforms as
    /// r' = R r and v' = R v - (R ω) × r', see
    /// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame(Frame)"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">SPICE knows no frame of the name of <paramref name="targetFrame"/>
    /// (<see cref="ArgumentException.ParamName"/> <c>targetFrame</c>) or of this frame (no parameter named, this frame being
    /// no argument of the call): the message names the frame, SPICE's error its inner exception (R25 of #668, decided by
    /// S. Guillet, 2026-11-19; before 11.0.0 an <see cref="InvalidOperationException"/>). A
    /// <see cref="MemoryDataProvider"/> throws its own for a frame it neither holds nor defines.</exception>
    /// <exception cref="InvalidOperationException">The data do not give the orientation of a known frame at the epoch
    /// (SPICE's <see cref="Exceptions.SpiceException"/>).</exception>
    public StateOrientation ToFrame(Frame targetFrame, Time epoch)
    {
        if (targetFrame == this)
        {
            return new StateOrientation(Quaternion.Identity, Vector3.Zero, epoch, targetFrame);
        }

        // An unknown name names targetFrame for the target, and no parameter for this frame, which is none (R25 of #668)
        var sourceToICRF = OrientationToIcrfNaming(this, epoch, null);
        var targetToICRF = OrientationToIcrfNaming(targetFrame, epoch, nameof(targetFrame));

        var rotation = targetToICRF.Rotation.Conjugate() * sourceToICRF.Rotation;

        var angularVelocity = targetToICRF.AngularVelocity.Inverse().Rotate(rotation.Conjugate()) + sourceToICRF.AngularVelocity;

        return new StateOrientation(rotation, angularVelocity, epoch, this);
    }

    // The orientation of frame to ICRF, the error of an unknown name of it naming paramName (R25 of #668, open choice 1)
    private static StateOrientation OrientationToIcrfNaming(Frame frame, in Time epoch, string paramName)
    {
        try
        {
            return frame.GetStateOrientationToIcrf(epoch);
        }
        catch (ArgumentException e) when (SpiceApi.IsUnknownFrameErrorOfAnotherParameter(e, frame, paramName))
        {
            throw SpiceApi.UnknownFrameError(e, frame, paramName);
        }
    }

    /// <summary>
    /// The rotation of <see cref="ToFrame"/>, the same doubles, without allocating in the propagation's case.
    /// </summary>
    /// <remarks>
    /// The same composition as <see cref="ToFrame"/>, <c>targetToICRF.Rotation.Conjugate() * sourceToICRF.Rotation</c>,
    /// with each rotation to ICRF from <see cref="GetRotationToICRF"/>: the orientation cache active on the current thread
    /// or the identity of ICRF allocate nothing, and the other frames go through
    /// <see cref="GetStateOrientationToIcrf(Time)"/>. Used by the forces that work in a body-fixed frame at every stage.
    /// </remarks>
    /// <param name="targetFrame">The frame the vectors are rotated to.</param>
    /// <param name="epoch">Epoch of the transform.</param>
    /// <returns>The rotation q such that <c>vectorInTarget = vectorInThis.Rotate(q)</c>.</returns>
    internal Quaternion RotationTo(Frame targetFrame, in Time epoch)
    {
        if (targetFrame == this)
        {
            return Quaternion.Identity;
        }

        var sourceToICRF = GetRotationToICRF(epoch);
        var targetToICRF = targetFrame.GetRotationToICRF(epoch);
        return targetToICRF.Conjugate() * sourceToICRF;
    }

    /// <summary>
    /// The angular velocity of <see cref="ToFrame"/>, the same doubles, without allocating in the propagation's case.
    /// </summary>
    /// <remarks>
    /// The same composition as <see cref="ToFrame"/>,
    /// <c>targetToICRF.AngularVelocity.Inverse().Rotate(rotation.Conjugate()) + sourceToICRF.AngularVelocity</c>, with
    /// the orientations to ICRF from <see cref="GetRotationAndAngularVelocityToICRF"/>, as <see cref="RotationTo"/>
    /// does for the rotation. Used by atmospheric drag at every stage.
    /// </remarks>
    /// <param name="targetFrame">The target frame.</param>
    /// <param name="epoch">Epoch of the transform.</param>
    /// <returns>The angular velocity of the target frame relative to this one, in the axes of this frame, in rad/s.</returns>
    internal Vector3 AngularVelocityTo(Frame targetFrame, in Time epoch)
    {
        if (targetFrame == this)
        {
            return Vector3.Zero;
        }

        GetRotationAndAngularVelocityToICRF(epoch, out var sourceRotation, out var sourceAngularVelocity);
        targetFrame.GetRotationAndAngularVelocityToICRF(epoch, out var targetRotation, out var targetAngularVelocity);
        var rotation = targetRotation.Conjugate() * sourceRotation;
        return targetAngularVelocity.Inverse().Rotate(rotation.Conjugate()) + sourceAngularVelocity;
    }

    /// <summary>
    /// The rotation and the angular velocity of <see cref="GetStateOrientationToIcrf(Time)"/>, the same doubles,
    /// without allocating when they come from the orientation cache active on the current thread or when this frame is
    /// ICRF (the identity and a zero angular velocity, which SPICE returns exactly), in the order of
    /// <see cref="GetRotationToICRF"/>.
    /// </summary>
    internal void GetRotationAndAngularVelocityToICRF(in Time date, out Quaternion rotation, out Vector3 angularVelocity)
    {
        var cache = OrientationCache;
        if (cache != null && ReadsOrientationCache)
        {
            cache.GetRotationAndAngularVelocity(date, out rotation, out angularVelocity);
            return;
        }

        if (GetType() == typeof(Frame) && Name == ICRF.Name)
        {
            rotation = Quaternion.Identity;
            angularVelocity = Vector3.Zero;
            return;
        }

        var orientation = GetStateOrientationToIcrf(date);
        rotation = orientation.Rotation;
        angularVelocity = orientation.AngularVelocity;
    }

    /// <summary>
    /// The rotation of <see cref="GetStateOrientationToIcrf(Time)"/>, the same doubles, without allocating when it
    /// comes from the orientation cache active on the current thread or when this frame is ICRF.
    /// </summary>
    /// <remarks>
    /// ICRF to ICRF is the identity: SPICE returns exactly (1, 0, 0, 0), bit for bit
    /// (<c>FrameRotationTests.IcrfToIcrf_IsTheIdentityBitForBit</c>), which is <see cref="Quaternion.Identity"/>; reading it
    /// would otherwise store an orientation per epoch. The cache is used only by a frame whose
    /// <see cref="GetStateOrientationToIcrf(Time)"/> reads it (<see cref="ReadsOrientationCache"/>), so that the two
    /// methods agree for every frame.
    /// </remarks>
    internal Quaternion GetRotationToICRF(in Time date)
    {
        // In the order of GetStateOrientationToIcrf: the cache first, then the orientation of the frame
        var cache = OrientationCache;
        if (cache != null && ReadsOrientationCache)
        {
            return cache.GetRotation(date);
        }

        if (GetType() == typeof(Frame) && Name == ICRF.Name)
        {
            return Quaternion.Identity;
        }

        return GetStateOrientationToIcrf(date).Rotation;
    }

    /// <summary>
    /// Whether <see cref="GetStateOrientationToIcrf(Time)"/> reads the orientation cache active on the current thread:
    /// the base frame does; a subclass that overrides it says so.
    /// </summary>
    private protected virtual bool ReadsOrientationCache => GetType() == typeof(Frame);

    #region Operators

    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same frame: a frame is its name, compared ignoring case as SPICE compares
    /// frame names (<c>namfrm_c</c>), whatever the instance or its type (#562). <c>new Frame("TEME")</c> is
    /// <see cref="TEME"/>, <c>new Frame("gcrf")</c> is <see cref="GCRF"/>, <c>new Frame("J2000")</c> is <see cref="ICRF"/>.
    /// </summary>
    /// <remarks>
    /// <c>==</c>, <c>!=</c>, <see cref="Equals(object)"/> and <see cref="GetHashCode"/> are this relation, so that the
    /// operators, the collections and the comparers agree. The name does not carry the Earth orientation parameters of a
    /// <see cref="TirsFrame"/> or a <see cref="TemeFrame"/>: two of them with the same name are one frame, as they were
    /// before 11.0.0. Nor does it carry the definition of a <c>SiteFrame</c> or a <see cref="Body.Spacecraft.SpacecraftFrame"/>: each equals
    /// any frame of its name, a kernel frame included, whatever its axes.
    /// </remarks>
    public bool Equals(Frame other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <see cref="Equals(Frame)"/> for a frame, false for anything else (#562: the type of the frame no longer counts).
    /// Sealed, so that a subclass cannot make it another relation than the operators.
    /// </summary>
    public sealed override bool Equals(object obj)
    {
        return Equals(obj as Frame);
    }

    /// <summary>
    /// The hash of the name ignoring case, the relation of <see cref="Equals(Frame)"/> (#562).
    /// </summary>
    public sealed override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public static bool operator ==(Frame left, Frame right)
    {
        return ReferenceEquals(left, right) || (left is not null && left.Equals(right));
    }

    public static bool operator !=(Frame left, Frame right)
    {
        return !(left == right);
    }

    #endregion
}
