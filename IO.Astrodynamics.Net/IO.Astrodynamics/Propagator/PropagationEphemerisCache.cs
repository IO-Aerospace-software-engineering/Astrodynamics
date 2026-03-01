using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Pre-computed ephemeris cache for celestial body positions during propagation.
/// Stores positions at a regular time grid and uses 8-point Lagrange interpolation
/// for sub-grid queries. This avoids repeated SPICE calls during integration sub-steps.
/// </summary>
public sealed class PropagationEphemerisCache : IDisposable
{
    /// <summary>
    /// Lightweight value type for cached ephemeris data (48 bytes vs 350+ for StateVector).
    /// </summary>
    public readonly record struct EphemerisPoint(Vector3 Position, Vector3 Velocity, double EpochSeconds);

    private readonly Dictionary<(int naifId, Aberration aberration), EphemerisPoint[]> _cache;
    private readonly double _gridStepSeconds;
    private readonly double _startEpochSeconds;
    private readonly int _gridSize;

    private const int InterpolationOrder = 8;
    private const int BufferPoints = 4;

    /// <summary>
    /// Create a propagation ephemeris cache for the given bodies over the specified time window.
    /// </summary>
    /// <param name="window">Propagation time window (TDB).</param>
    /// <param name="entries">Bodies and aberration modes to cache.</param>
    /// <param name="observer">Observer for all cached ephemeris (central body or SSB).</param>
    /// <param name="gridStep">Time between grid points (default: 60 seconds).</param>
    public PropagationEphemerisCache(
        Window window,
        IEnumerable<(CelestialItem body, Aberration aberration)> entries,
        ILocalizable observer,
        TimeSpan gridStep)
    {
        _gridStepSeconds = gridStep.TotalSeconds;
        if (_gridStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(gridStep), "Grid step must be positive.");

        double windowStart = window.StartDate.TimeSpanFromJ2000().TotalSeconds;
        double windowEnd = window.EndDate.TimeSpanFromJ2000().TotalSeconds;

        // Add buffer points before and after for Lagrange edge effects
        _startEpochSeconds = windowStart - BufferPoints * _gridStepSeconds;
        double endEpochSeconds = windowEnd + BufferPoints * _gridStepSeconds;
        _gridSize = (int)System.Math.Ceiling((endEpochSeconds - _startEpochSeconds) / _gridStepSeconds) + 1;

        _cache = new Dictionary<(int, Aberration), EphemerisPoint[]>();

        // De-duplicate entries
        var seen = new HashSet<(int, Aberration)>();
        foreach (var (body, aberration) in entries)
        {
            var key = (body.NaifId, aberration);
            if (!seen.Add(key))
                continue;

            var points = new EphemerisPoint[_gridSize];
            for (int i = 0; i < _gridSize; i++)
            {
                double epochSec = _startEpochSeconds + i * _gridStepSeconds;
                var epoch = Time.CreateTDB(epochSec);
                var sv = body.GetEphemeris(epoch, observer, Frame.ICRF, aberration).ToStateVector();
                points[i] = new EphemerisPoint(sv.Position, sv.Velocity, epochSec);
            }

            _cache[key] = points;
        }
    }

    /// <summary>
    /// Get interpolated position of a cached body at the given epoch.
    /// </summary>
    public Vector3 GetPosition(int bodyNaifId, Aberration aberration, in Time epoch)
    {
        var points = _cache[(bodyNaifId, aberration)];
        double t = epoch.TimeSpanFromJ2000().TotalSeconds;

        // Compute stencil start index for 8-point Lagrange
        int centerIdx = (int)((t - _startEpochSeconds) / _gridStepSeconds);
        int startIdx = System.Math.Clamp(centerIdx - (InterpolationOrder / 2 - 1), 0, _gridSize - InterpolationOrder);

        // Compute Lagrange basis weights and interpolate position
        double px = 0, py = 0, pz = 0;
        for (int i = 0; i < InterpolationOrder; i++)
        {
            double ti = points[startIdx + i].EpochSeconds;
            double Li = 1.0;
            for (int j = 0; j < InterpolationOrder; j++)
            {
                if (j != i)
                {
                    double tj = points[startIdx + j].EpochSeconds;
                    Li *= (t - tj) / (ti - tj);
                }
            }

            var p = points[startIdx + i];
            px += p.Position.X * Li;
            py += p.Position.Y * Li;
            pz += p.Position.Z * Li;
        }

        return new Vector3(px, py, pz);
    }

    /// <summary>
    /// Get interpolated position and velocity of a cached body at the given epoch.
    /// </summary>
    public (Vector3 position, Vector3 velocity) GetState(int bodyNaifId, Aberration aberration, in Time epoch)
    {
        var points = _cache[(bodyNaifId, aberration)];
        double t = epoch.TimeSpanFromJ2000().TotalSeconds;

        int centerIdx = (int)((t - _startEpochSeconds) / _gridStepSeconds);
        int startIdx = System.Math.Clamp(centerIdx - (InterpolationOrder / 2 - 1), 0, _gridSize - InterpolationOrder);

        double px = 0, py = 0, pz = 0;
        double vx = 0, vy = 0, vz = 0;
        for (int i = 0; i < InterpolationOrder; i++)
        {
            double ti = points[startIdx + i].EpochSeconds;
            double Li = 1.0;
            for (int j = 0; j < InterpolationOrder; j++)
            {
                if (j != i)
                {
                    double tj = points[startIdx + j].EpochSeconds;
                    Li *= (t - tj) / (ti - tj);
                }
            }

            var p = points[startIdx + i];
            px += p.Position.X * Li;
            py += p.Position.Y * Li;
            pz += p.Position.Z * Li;
            vx += p.Velocity.X * Li;
            vy += p.Velocity.Y * Li;
            vz += p.Velocity.Z * Li;
        }

        return (new Vector3(px, py, pz), new Vector3(vx, vy, vz));
    }

    /// <summary>
    /// Check whether this cache contains data for the given body and aberration mode.
    /// </summary>
    public bool Contains(int bodyNaifId, Aberration aberration) => _cache.ContainsKey((bodyNaifId, aberration));

    public void Dispose() => _cache.Clear();
}
