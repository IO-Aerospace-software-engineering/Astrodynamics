using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// An encounter given by the states of its two objects at TCA (spec.md sections 16.2 and 16.3), analyzed by the public
/// <see cref="ConjunctionAssessment.Analyze(ProtectedSpacecraftProfile, PropagationSolution, ILocalizable, PropagationSolution, Window, ConjunctionAnalysisOptions)"/>
/// on straight-line trajectories over TCA ± 10 s (design of lot 6 of #457, open choice 16): each object a spacecraft
/// whose initial state is its state at TCA, carrying its covariance rotated from its RTN, the combined hard-body radius
/// on object 1 and none on object 2.
/// </summary>
/// <remarks>
/// The 2D Pc assumes straight-line relative motion over the encounter (spec.md section 16.2): the trajectories are that
/// motion, so that the framework's own TCA refinement finds the given TCA and the covariances stay those at TCA (held
/// in RTN at age zero: the trajectories carry no variational equations).
/// </remarks>
internal static class StraightLineEncounter
{
    /// <summary>The half-span of the trajectories about TCA.</summary>
    internal const double HalfSpanSeconds = 10.0;

    /// <summary>
    /// Analyzes the encounter of <paramref name="objects"/> (object 1, then object 2) at <paramref name="tca"/>.
    /// </summary>
    /// <param name="tca">The TCA.</param>
    /// <param name="objects">The states at TCA in ICRF (km, km/s) and the covariances in each object's RTN.</param>
    /// <param name="covarianceScale">The scale of the covariance numbers to m², m²/s and m²/s² (1e6 from km).</param>
    /// <param name="hardBodyRadiusMeters">The combined hard-body radius.</param>
    /// <param name="naifId">The NAIF id of object 1; object 2 takes the next lower.</param>
    /// <param name="absentAsZero">Gives an absent covariance as a zero matrix, so that the framework projects the given
    /// one alone, without Frisbee's maximum.</param>
    public static EncounterCase Analyze(Time tca, IReadOnlyList<EncounterObject> objects, double covarianceScale,
        double hardBodyRadiusMeters, int naifId, bool absentAsZero = false)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, tca);
        var window = new Window(tca.AddSeconds(-HalfSpanSeconds), tca.AddSeconds(HalfSpanSeconds));
        var spacecraft = new Spacecraft[2];
        var trajectories = new PropagationSolution[2];
        for (int k = 0; k < 2; k++)
        {
            var o = objects[k];
            var position = new Vector3(UnitConversion.KmToM(o.PositionKm[0]), UnitConversion.KmToM(o.PositionKm[1]), UnitConversion.KmToM(o.PositionKm[2]));
            var velocity = new Vector3(UnitConversion.KmSToMS(o.VelocityKmS[0]), UnitConversion.KmSToMS(o.VelocityKmS[1]), UnitConversion.KmSToMS(o.VelocityKmS[2]));
            var state = new StateVector(position, velocity, earth, tca, Frame.ICRF);
            var covarianceRtn = Covariance(o.CovarianceRtn, covarianceScale, absentAsZero);
            if (covarianceRtn is { } rtn)
            {
                state = new StateVector(position, velocity, earth, tca, Frame.ICRF, state.RotateCovarianceFromRtn(rtn));
            }

            int id = naifId - k;
            spacecraft[k] = new Spacecraft(id, $"Encounter{-id}", 100.0, 1000.0, new Clock($"EncounterClk{-id}", 256), state,
                hardBodyRadius: k == 0 ? hardBodyRadiusMeters : 0.0);
            trajectories[k] = StraightLine(earth, window.StartDate, position - velocity * HalfSpanSeconds, velocity);
        }

        return ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(spacecraft[0]), trajectories[0], spacecraft[1],
            trajectories[1], window, new ConjunctionAnalysisOptions
            {
                // Longer than the window: one interval, the TCA refined on the trajectories (as the library's tests)
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });
    }

    // The 6x6 RTN covariance of an object (a 3x3 position covariance in its position block), or null
    private static Matrix? Covariance(double[] lower, double scale, bool absentAsZero)
    {
        if (lower == null)
        {
            return absentAsZero ? new Matrix(6, 6) : null;
        }

        var given = LowerTriangle.ToMatrix(lower, scale);
        if (given.Rows == 6)
        {
            return given;
        }

        var covariance = new Matrix(6, 6);
        for (int i = 0; i < given.Rows; i++)
        {
            for (int j = 0; j < given.Columns; j++)
            {
                covariance.Set(i, j, given.Get(i, j));
            }
        }

        return covariance;
    }

    // A straight line at constant velocity from start over the window, one accepted step and its two ends
    private static PropagationSolution StraightLine(CelestialBody earth, Time start, Vector3 startPosition, Vector3 velocity)
    {
        double span = 2.0 * HalfSpanSeconds;
        var endPosition = startPosition + velocity * span;
        var segment = new PropagationSegment(start, 1);
        segment.AddStep(new AcceptedStep(0.0, span, startPosition, velocity, endPosition, velocity, Vector3.Zero, Vector3.Zero));
        var solution = new PropagationSolution();
        solution.AddSegment(segment);
        solution.SetOutputStates(new[]
        {
            new StateVector(startPosition, velocity, earth, start, Frame.ICRF),
            new StateVector(endPosition, velocity, earth, start.AddSeconds(span), Frame.ICRF)
        });
        return solution;
    }
}
