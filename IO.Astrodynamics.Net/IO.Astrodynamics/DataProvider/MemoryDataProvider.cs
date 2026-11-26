using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.DataProvider;

/// <summary>
/// A data provider that reads states, orientations and body information stored in memory.
/// </summary>
/// <remarks>
/// Its reads follow the contract of the SPICE provider (#680): a target without stored states or read outside them, and a
/// frame read outside its stored orientations, throw <see cref="InvalidOperationException"/>, the type SPICE throws for the
/// same situations and the type the library's handlers of missing data catch, so a body whose barycentre's data the
/// provider lacks builds without an orbit and with an infinite sphere of influence, as on SPICE; a frame neither added nor
/// known to the provider (ICRF, GCRF, ECLIPJ2000, TEME, CIRS and TIRS, #559, #656) throws <see cref="ArgumentException"/>,
/// from <see cref="FrameTransformationToICRF"/> and, since #559, from <see cref="GetEphemeris"/> (a body-fixed frame such as
/// IAU_EARTH or ITRF93 included, unless its orientations were added). A NAIF id without body information throws <see cref="ArgumentException"/> naming
/// <c>naifId</c>, as the SPICE provider for an id SPICE does not name or that is not a celestial body's (#672), and so do
/// <c>new CelestialBody(id)</c> and <see cref="CelestialItem.Create"/> of that id (#680, decided by S. Guillet,
/// 2026-11-14: a bad argument gives <see cref="ArgumentException"/>), except <see cref="CelestialItem.Create"/> of a
/// Lagrange point (391 to 395), which reads the point's state, not its information: a missing state throws
/// <see cref="InvalidOperationException"/>, as on SPICE. A body whose barycentre's information the provider
/// lacks still builds without an orbit, as on SPICE, which names every barycentre. The static objects of
/// <see cref="SolarSystemObjects.Barycenters"/>, <see cref="SolarSystemObjects.PlanetsAndMoons"/> and
/// <see cref="SolarSystemObjects.Stars"/> read the provider at the first read of a member that needs its data: a provider
/// set before their first read and lacking their data makes each such read throw its exception, until the data are added
/// (before 11.0.0 their type initializers threw a <see cref="TypeInitializationException"/> kept for the process).
/// </remarks>
public class MemoryDataProvider : IDataProvider
{
    private Dictionary<string, SortedDictionary<Time, StateOrientation>> _stateOrientationsToICRF = new Dictionary<string, SortedDictionary<Time, StateOrientation>>();
    private Dictionary<int, SortedDictionary<Time, StateVector>> _stateVectors = new Dictionary<int, SortedDictionary<Time, StateVector>>();
    private Dictionary<int, CelestialBodyInfo> _celestialBodies = new Dictionary<int, CelestialBodyInfo>();

    /// <exception cref="ArgumentException">No orientation of <paramref name="source"/> was ever added, and the provider does not
    /// know it: neither ICRF, GCRF, ECLIPJ2000 (#559), nor TEME, CIRS or TIRS (#656), by name in any case for the last
    /// three.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="epoch"/> is outside the stored orientations of
    /// <paramref name="source"/> (#680; <see cref="ArgumentException"/> before 11.0.0).</exception>
    /// <remarks>
    /// A frame named TEME, CIRS or TIRS, in any case, is <see cref="Frame.TEME"/>, <see cref="Frame.CIRS"/> or
    /// <see cref="Frame.TIRS"/>, the managed frames, which need no stored orientation (#451, the routing of #562, #656); its
    /// orientation is labelled with <paramref name="source"/>. An orientation added under one of those names is not read
    /// (#656; before 11.0.0 the stored orientations of a frame named CIRS or TIRS were read), and none can be since #744. Any other frame is read from
    /// the orientations added under its name; a frame without any is known by its definition when the library has it (#559):
    /// <see cref="Frame.ICRF"/> and <see cref="Frame.GCRF"/>, the identity, which no orientation can be added under (#744),
    /// and <see cref="Frame.ECLIPTIC_J2000"/>, SPICE's fixed rotation of the J2000 obliquity (84381.448″ about x, the default
    /// frame of the bodies), so that a body built on this provider reads its orbit in it without stored orientations.
    /// </remarks>
    public StateOrientation FrameTransformationToICRF(in Time epoch, Frame source)
    {
        if (SpiceAPI.ManagedOrientationToIcrf(epoch, source) is { } managed)
        {
            return managed;
        }

        if (!_stateOrientationsToICRF.TryGetValue(source.Name, out var stateOrientations))
        {
            return DefinedOrientationToIcrf(epoch, source) ?? throw new ArgumentException(SpiceAPI.WithIcrfHint(source.Name,
                $"Frame not found ({source}): no orientation was added to this MemoryDataProvider under the name '{source.Name}', and it does not define it."));
        }

        if (stateOrientations.TryGetValue(epoch, out var stateOrientation))
        {
            return stateOrientation;
        }

        // A frame is added with its first entry (AddStateOrientationToICRF): its entries are never empty
        if (epoch < stateOrientations.Keys.First() || epoch > stateOrientations.Keys.Last())
        {
            throw new InvalidOperationException($"No state orientation found for the frame ({source}) at this epoch {epoch}");
        }

        // The entries around the epoch, interpolated by the exact SLERP of StateOrientation.Interpolate; the result is not
        // stored, so that a read depends on the entries only, not on the reads before it (#548)
        var dates = stateOrientations.Keys.ToList();
        int next = ~dates.BinarySearch(epoch);
        return stateOrientations[dates[next - 1]].Interpolate(stateOrientations[dates[next]], epoch);
    }

    /// <summary>
    /// SPICE's obliquity of the ecliptic at J2000, the angle of its built-in frame ECLIPJ2000 from J2000 about x: 84381.448″
    /// (CSPICE <c>chgirf.c</c>, the IAU 1976 value), in radians.
    /// </summary>
    internal static readonly double EclipticJ2000Obliquity = 84381.448 / 3600.0 * Constants.Deg2Rad;

    // The orientation of a frame the provider knows by definition, labelled with the frame given, or null (#559)
    private static StateOrientation DefinedOrientationToIcrf(in Time date, Frame frame)
    {
        if (HasIcrfAxes(frame))
        {
            return new StateOrientation(Quaternion.Identity, Vector3.Zero, date, frame);
        }

        if (frame == Frame.ECLIPTIC_J2000)
        {
            // ECLIPJ2000 -> J2000 is the rotation by +obliquity about x (J2000 -> ECLIPJ2000 is SPICE's ROTATE(obliquity, 1))
            return new StateOrientation(new Quaternion(Vector3.VectorX, EclipticJ2000Obliquity), Vector3.Zero, date, frame);
        }

        return null;
    }

    // ICRF and GCRF share their axes (#543): a frame of either name, in any case, needs no rotation
    private static bool HasIcrfAxes(Frame frame) => frame == Frame.ICRF || frame == Frame.GCRF;

    // The orientation of a frame to ICRF for a state of this provider: the identity for the ICRF axes (the stored states are
    // ICRF; nothing can be stored under their names, #744), the provider's own orientation otherwise
    private StateOrientation OrientationToIcrfForStates(in Time date, Frame frame) =>
        HasIcrfAxes(frame) ? null : FrameTransformationToICRF(date, frame);

    // The stored state of the target about the solar system barycentre, in ICRF: a state stored in another frame is rotated
    // to ICRF by the orientation of its frame at the date, r' = R r, v' = R v - (R ω) × r' (OrbitalParameters.ToFrame)
    private StateVector GetEphemerisFromICRF(in Time date, ILocalizable target)
    {
        if (target.NaifId == 0)
        {
            return new StateVector(Vector3.Zero, Vector3.Zero, new Barycenter(0, date), date, Frame.ICRF);
        }

        // A known item without data: the type SPICE throws (#680)
        if (!_stateVectors.TryGetValue(target.NaifId, out var stateVectors) || stateVectors.Count == 0)
        {
            throw new InvalidOperationException($"No state vectors found for this target ({target.NaifId}) at this date {date}");
        }

        if (date < stateVectors.Keys.First() || date > stateVectors.Keys.Last())
        {
            throw new InvalidOperationException($"No state vector found for this target {target.NaifId} at this date {date}");
        }

        // The stored states only: an interpolated read is not stored, so that it does not become a node of the reads after
        // it (#548)
        var state = stateVectors.TryGetValue(date, out var stateVector) ? stateVector : Lagrange.Interpolate(stateVectors.Values.ToArray(), date);
        var orientation = OrientationToIcrfForStates(date, state.Frame);
        if (orientation == null)
        {
            return state;
        }

        var position = state.Position.Rotate(orientation.Rotation);
        var velocity = state.Velocity.Rotate(orientation.Rotation) - orientation.AngularVelocity.Rotate(orientation.Rotation).Cross(position);
        return new StateVector(position, velocity, state.Observer, date, Frame.ICRF);
    }

    /// <summary>
    /// The geometric state of <paramref name="target"/> relative to <paramref name="observer"/> in <paramref name="frame"/>,
    /// from the states stored about the solar system barycentre.
    /// </summary>
    /// <remarks>
    /// The stored states of the two items are taken in ICRF (a state stored in another frame rotated by the provider's
    /// orientation of that frame), subtracted, then rotated into <paramref name="frame"/> by the provider's orientation of it
    /// (<see cref="FrameTransformationToICRF"/>), as <see cref="OrbitalParameters.OrbitalParameters.ToFrame"/> does: r' = R r,
    /// v' = R v + ω × r', ω the angular velocity of ICRF relative to the frame in its axes. A frame of ICRF's axes (ICRF,
    /// GCRF) is not rotated, so a read in ICRF is the stored numbers subtracted, as before (#559). Before 11.0.0 the
    /// difference was returned in ICRF whatever the frame, labelled with the frame asked: an ECLIPJ2000 read was 5.76e10 m
    /// off in z for the Earth-Moon barycentre.
    /// The provider holds geometric states only: it computes no light time and no stellar aberration (#559, decided by
    /// S. Guillet, 2026-11-17), so any <paramref name="aberration"/> but <see cref="Aberration.None"/> throws; before 11.0.0
    /// the geometric state was returned.
    /// </remarks>
    /// <exception cref="NotSupportedException"><paramref name="aberration"/> is not <see cref="Aberration.None"/> (#559).</exception>
    /// <exception cref="InvalidOperationException">The target or the observer has no stored state, or none around
    /// <paramref name="epoch"/> (#680; <see cref="ArgumentException"/> before 11.0.0); or <paramref name="frame"/>, or the
    /// frame of a stored state, is read outside its stored orientations.</exception>
    /// <exception cref="ArgumentException"><paramref name="frame"/>, or the frame of a stored state, is neither stored nor
    /// known to the provider (<see cref="FrameTransformationToICRF"/>).</exception>
    public StateVector GetEphemeris(in Time epoch, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        if (aberration != Aberration.None)
        {
            throw new NotSupportedException(
                $"The memory data provider holds geometric states only: the aberration {aberration} cannot apply. Read with Aberration.None.");
        }

        if (target.NaifId == observer.NaifId)
        {
            return new StateVector(Vector3.Zero, Vector3.Zero, observer, epoch, frame);
        }

        var targetFromSSB = GetEphemerisFromICRF(epoch, target);
        var observerFromSSB = GetEphemerisFromICRF(epoch, observer);
        var position = targetFromSSB.Position - observerFromSSB.Position;
        var velocity = targetFromSSB.Velocity - observerFromSSB.Velocity;

        var orientation = OrientationToIcrfForStates(epoch, frame);
        if (orientation != null)
        {
            var toFrame = orientation.Rotation.Conjugate();
            position = position.Rotate(toFrame);
            velocity = velocity.Rotate(toFrame) + orientation.AngularVelocity.Cross(position);
        }

        return new StateVector(position, velocity, observer, epoch, frame);
    }

    /// <exception cref="ArgumentException">No information was added for <paramref name="naifId"/>, the exception's
    /// <see cref="ArgumentException.ParamName"/> <c>naifId</c>: the type and the parameter <see cref="SpiceDataProvider"/>
    /// throws for an identifier SPICE does not name (#672). #680, decided by S. Guillet, 2026-11-14: a bad argument gives
    /// <see cref="ArgumentException"/>; <see cref="System.Collections.Generic.KeyNotFoundException"/> before 11.0.0.</exception>
    public CelestialBodyInfo GetCelestialBodyInfo(int naifId)
    {
        if (!_celestialBodies.TryGetValue(naifId, out var info))
        {
            throw new ArgumentException(
                $"No celestial body information for the NAIF id {naifId}: add it with {nameof(AddCelestialBodyInfo)}.",
                nameof(naifId));
        }

        return info;
    }

    /// <summary>
    /// Store the information of celestial bodies, each under its <see cref="CelestialBodyInfo.NaifId"/>, replacing the
    /// information stored under that identifier.
    /// </summary>
    /// <param name="celestialBodyInfos">The records (<see cref="CelestialBodyInfo"/> since 11.0.0, the interop struct
    /// <c>IO.Astrodynamics.DTO.CelestialBody</c> before).</param>
    /// <exception cref="ArgumentNullException">The array or one of its records is null; nothing is stored.</exception>
    public void AddCelestialBodyInfo(params CelestialBodyInfo[] celestialBodyInfos)
    {
        ArgumentNullException.ThrowIfNull(celestialBodyInfos);
        if (Array.IndexOf(celestialBodyInfos, null) >= 0)
        {
            throw new ArgumentNullException(nameof(celestialBodyInfos), "A record of the array is null.");
        }

        foreach (var body in celestialBodyInfos)
        {
            _celestialBodies[body.NaifId] = body;
        }
    }

    /// <summary>
    /// Store the orientation of <paramref name="frame"/> to ICRF at <paramref name="epoch"/>, read by
    /// <see cref="FrameTransformationToICRF"/> and by the ephemeris reads in that frame.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="frame"/> is a frame the provider defines and never reads from
    /// stored entries, by name in any case: ICRF (J2000) and GCRF, the identity (the stored states are ICRF, #559; GCRF
    /// shares its axes, #543), and the managed TEME, CIRS and TIRS (#656) (#744, decided by S. Guillet, 2026-11-18; before
    /// 11.0.0 an orientation stored under ICRF or GCRF was returned by <see cref="FrameTransformationToICRF"/> while the
    /// ephemeris reads took the identity, and one stored under TEME, CIRS or TIRS was never read). Nothing is stored.</exception>
    public void AddStateOrientationToICRF(Frame frame, Time epoch, StateOrientation stateOrientation)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (HasIcrfAxes(frame) || SpiceAPI.ManagedFrame(frame) != null)
        {
            throw new ArgumentException(
                $"The provider defines the frame {frame.Name} and never reads its stored orientations: ICRF and GCRF are the identity, TEME, CIRS and TIRS the managed frames.",
                nameof(frame));
        }

        if (!_stateOrientationsToICRF.ContainsKey(frame.Name))
        {
            _stateOrientationsToICRF[frame.Name] = new SortedDictionary<Time, StateOrientation>();
        }

        _stateOrientationsToICRF[frame.Name][epoch] = stateOrientation;
    }

    public void AddStateVector(int naifId, Time epoch, StateVector stateVector)
    {
        if (!_stateVectors.ContainsKey(naifId))
        {
            _stateVectors[naifId] = new SortedDictionary<Time, StateVector>();
        }

        _stateVectors[naifId][epoch] = stateVector;
    }
}