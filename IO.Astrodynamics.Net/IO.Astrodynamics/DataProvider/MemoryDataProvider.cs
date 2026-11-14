using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;
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
/// provider lacks builds without an orbit and with an infinite sphere of influence, as on SPICE; a frame never added throws
/// <see cref="ArgumentException"/>. A NAIF id without body information throws <see cref="InvalidOperationException"/>
/// too, the type the construction of a body without a name throws on SPICE (<see cref="CelestialItem"/>); the SPICE
/// provider's own read returns a record for an id the kernels do not name and throws <see cref="ArgumentException"/>
/// for an id that is not a celestial body's.
/// </remarks>
public class MemoryDataProvider : IDataProvider
{
    private Dictionary<string, SortedDictionary<Time, StateOrientation>> _stateOrientationsToICRF = new Dictionary<string, SortedDictionary<Time, StateOrientation>>();
    private Dictionary<int, SortedDictionary<Time, StateVector>> _stateVectors = new Dictionary<int, SortedDictionary<Time, StateVector>>();
    private Dictionary<int, CelestialBody> _celestialBodies = new Dictionary<int, CelestialBody>();

    /// <exception cref="ArgumentException">No orientation of <paramref name="source"/> was ever added.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="date"/> is outside the stored orientations of
    /// <paramref name="source"/> (#680; <see cref="ArgumentException"/> before 11.0.0).</exception>
    /// <remarks>
    /// A frame named TEME is <see cref="Frame.TEME"/>, the managed <see cref="TemeFrame"/>, which needs no stored
    /// orientation (#451, the routing of #562); its orientation is labelled with <paramref name="source"/>.
    /// </remarks>
    public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
    {
        if (SpiceAPI.IsTeme(source))
        {
            var teme = Frame.TEME.GetStateOrientationToICRF(date);
            return new StateOrientation(teme.Rotation, teme.AngularVelocity, date, source);
        }

        if (!_stateOrientationsToICRF.TryGetValue(source.Name, out var stateOrientations))
        {
            throw new ArgumentException($"Frame not found ({source})");
        }

        if (stateOrientations.TryGetValue(date, out var stateOrientation))
        {
            return stateOrientation;
        }

        // A frame is added with its first entry (AddStateOrientationToICRF): its entries are never empty
        if (date < stateOrientations.Keys.First() || date > stateOrientations.Keys.Last())
        {
            throw new InvalidOperationException($"No state orientation found for the frame ({source}) at this date {date}");
        }

        // The entries around the date, interpolated by the exact SLERP of StateOrientation.Interpolate; the result is not
        // stored, so that a read depends on the entries only, not on the reads before it (#548)
        var dates = stateOrientations.Keys.ToList();
        int next = ~dates.BinarySearch(date);
        return stateOrientations[dates[next - 1]].Interpolate(stateOrientations[dates[next]], date);
    }

    private OrbitalParameters.OrbitalParameters GetEphemerisFromICRF(in Time date, ILocalizable target, Frame frame, Aberration aberration)
    {
        if (target.NaifId == 0)
        {
            return new StateVector(Vector3.Zero, Vector3.Zero, new Barycenter(0, date), date, frame);
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
        return stateVectors.TryGetValue(date, out var stateVector) ? stateVector : Lagrange.Interpolate(stateVectors.Values.ToArray(), date);
    }

    /// <exception cref="InvalidOperationException">The target or the observer has no stored state, or none around
    /// <paramref name="date"/> (#680; <see cref="ArgumentException"/> before 11.0.0).</exception>
    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        if (target.NaifId == observer.NaifId)
        {
            return new StateVector(Vector3.Zero, Vector3.Zero, observer, date, frame);
        }

        var targetFromSSB = GetEphemerisFromICRF(date, target, frame, aberration).ToStateVector();
        var observerFromSSB = GetEphemerisFromICRF(date, observer, frame, aberration).ToStateVector();
        return new StateVector(targetFromSSB.Position - observerFromSSB.Position,
            targetFromSSB.Velocity - observerFromSSB.Velocity, observer, date, frame);
    }

    /// <exception cref="InvalidOperationException">No information was added for <paramref name="naifId"/> (#680;
    /// <see cref="System.Collections.Generic.KeyNotFoundException"/> before 11.0.0), the type the construction of a body the
    /// kernels do not name throws on SPICE (<see cref="CelestialItem"/>).</exception>
    public CelestialBody GetCelestialBodyInfo(int naifId)
    {
        if (!_celestialBodies.TryGetValue(naifId, out var info))
        {
            throw new InvalidOperationException(
                $"No celestial body information for the NAIF id {naifId}: add it with {nameof(AddCelestialBodyInfo)}.");
        }

        return info;
    }

    public void AddCelestialBodyInfo(params CelestialBody[] celestialBody)
    {
        foreach (var body in celestialBody)
        {
            _celestialBodies[body.Id] = body;
        }
    }

    public void AddStateOrientationToICRF(Frame frame, Time date, StateOrientation stateOrientation)
    {
        if (!_stateOrientationsToICRF.ContainsKey(frame.Name))
        {
            _stateOrientationsToICRF[frame.Name] = new SortedDictionary<Time, StateOrientation>();
        }

        _stateOrientationsToICRF[frame.Name][date] = stateOrientation;
    }

    public void AddStateVector(int naifId, Time date, StateVector stateVector)
    {
        if (!_stateVectors.ContainsKey(naifId))
        {
            _stateVectors[naifId] = new SortedDictionary<Time, StateVector>();
        }

        _stateVectors[naifId][date] = stateVector;
    }
}