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

public class MemoryDataProvider : IDataProvider
{
    private Dictionary<string, SortedDictionary<Time, StateOrientation>> _stateOrientationsToICRF = new Dictionary<string, SortedDictionary<Time, StateOrientation>>();
    private Dictionary<int, SortedDictionary<Time, StateVector>> _stateVectors = new Dictionary<int, SortedDictionary<Time, StateVector>>();
    private Dictionary<int, CelestialBody> _celestialBodies = new Dictionary<int, CelestialBody>();

    public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
    {
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
            throw new ArgumentException($"No state orientation found for the frame ({source}) at this date {date}");
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

        if (!_stateVectors.TryGetValue(target.NaifId, out var stateVectors))
        {
            throw new ArgumentException($"No state vectors found for this target ({target.NaifId})");
        }

        if (stateVectors.Count == 0)
        {
            throw new ArgumentException($"No state vectors found for this target ({target.NaifId})");
        }

        if (date < stateVectors.Keys.First() || date > stateVectors.Keys.Last())
        {
            throw new ArgumentException($"No state vector found for this target {target.NaifId} at this date {date}");
        }

        // The stored states only: an interpolated read is not stored, so that it does not become a node of the reads after
        // it (#548)
        return stateVectors.TryGetValue(date, out var stateVector) ? stateVector : Lagrange.Interpolate(stateVectors.Values.ToArray(), date);
    }

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

    public CelestialBody GetCelestialBodyInfo(int naifId)
    {
        return _celestialBodies[naifId];
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