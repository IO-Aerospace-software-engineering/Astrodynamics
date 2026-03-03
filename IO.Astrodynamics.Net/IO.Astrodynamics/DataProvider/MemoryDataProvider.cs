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

        if (!stateOrientations.TryGetValue(date, out var stateOrientation))
        {
            if (stateOrientations.Count == 0)
            {
                throw new ArgumentException($"No state orientation found for this frame({source})");
            }

            if (date < stateOrientations.Keys.First() || date > stateOrientations.Keys.Last())
            {
                throw new ArgumentException($"No state orientation found for the frame ({source}) at this date {date}");
            }

            var dates = stateOrientations.Keys.Order().ToList();
            int index = dates.BinarySearch(date);

            Time? previousEntry = null;
            Time? nextEntry = null;


            index = ~index;

            if (index > 0)
            {
                previousEntry = dates[index - 1];
            }

            if (index < dates.Count)
            {
                nextEntry = dates[index];
            }

            var previousSo = stateOrientations[previousEntry!.Value];
            var nextSo = stateOrientations[nextEntry!.Value];
            stateOrientation = previousSo.Interpolate(nextSo, date);
            stateOrientations[date] = stateOrientation;
        }

        return stateOrientation;
    }

    public OrbitalParameters.OrbitalParameters GetEphemerisFromICRF(in Time date, ILocalizable target, Frame frame, Aberration aberration)
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

        if(!stateVectors.TryGetValue(date, out var stateVector))
        {
            stateVector = Lagrange.Interpolate(stateVectors.Values.ToArray(), date);
            stateVectors[date] = stateVector;
        }

        return stateVector;
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