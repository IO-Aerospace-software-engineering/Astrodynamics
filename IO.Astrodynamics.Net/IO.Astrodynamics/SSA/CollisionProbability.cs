// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Ssa;

/// <summary>
/// The 2D probability of collision of two objects given by their states at the time of closest approach, the methods
/// of the conjunction analysis, one method per Pc method (R19 of #668, decided by S. Guillet, 2026-11-19).
/// </summary>
/// <remarks>
/// <para>
/// Each state carries the covariance of its object at TCA (6×6, in its frame). The two states are at one instant, in one
/// frame and about one observer; a pair in an inertial frame other than ICRF is converted to ICRF (<see cref="StateVector.ToFrame"/>,
/// which turns the covariance). The probability is the analysis's: the in-plane miss, the projection of the combined
/// position covariance on the encounter plane, its eigenvalues raised to CARA's clip (1e-4 R)², and the integral of the
/// Gaussian over the disc of the combined hard-body radius R (CARA's Pc2D, within 2.0e-14 of the exact Pc of
/// <c>tools/collision_probability</c>), so <see cref="Ccsds.Cdm.ToEncounter"/> of the same states gives the same Pc
/// bit for bit (decision 110 of #668, decided by S. Guillet, 2026-11-26). See the conjunction assessment reference,
/// "Collision probability".
/// </para>
/// </remarks>
public static class CollisionProbability
{
    /// <summary>
    /// The probability of collision of Foster and Estes (1992) in CARA's Pc2D form, both covariances given.
    /// </summary>
    /// <param name="object1">The state of object 1 at TCA, with its covariance.</param>
    /// <param name="object2">The state of object 2 at TCA, with its covariance.</param>
    /// <param name="combinedHardBodyRadiusMeters">The radius of the sphere that bounds both objects, in m, finite and
    /// above 0.</param>
    /// <returns>The probability; null when a state has no covariance, or when the projected covariance is not positive
    /// (<see cref="FrisbeeMax"/> for one covariance).</returns>
    /// <exception cref="ArgumentNullException">A state is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite or not above 0.</exception>
    /// <exception cref="ArgumentException">The states are at two instants, in two frames or about two observers (named
    /// <paramref name="object2"/>).</exception>
    public static double? Foster2D(StateVector object1, StateVector object2, double combinedHardBodyRadiusMeters)
    {
        (object1, object2) = Checked(object1, object2, combinedHardBodyRadiusMeters);
        if (object1.Covariance is null || object2.Covariance is null)
        {
            return null;
        }

        return ConjunctionAssessment.CollisionRiskAt(object1, object2, combinedHardBodyRadiusMeters).ProbabilityOfCollision;
    }

    /// <summary>
    /// The maximum probability of collision of Frisbee when one object alone has a covariance: the probability of the
    /// covariance of the other object that maximizes it (the analysis's <see cref="EncounterQualityFlags.SingleCovarianceMaximumPcUsed"/>).
    /// </summary>
    /// <param name="object1">The state of object 1 at TCA, with or without its covariance.</param>
    /// <param name="object2">The state of object 2 at TCA, with or without its covariance.</param>
    /// <param name="combinedHardBodyRadiusMeters">The radius of the sphere that bounds both objects, in m, finite and
    /// above 0.</param>
    /// <returns>The maximum probability; null when neither state has a covariance, or when the projected covariance is
    /// not positive.</returns>
    /// <exception cref="ArgumentNullException">A state is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite or not above 0.</exception>
    /// <exception cref="ArgumentException">Both states have a covariance (use <see cref="Foster2D"/>), or the states are
    /// at two instants, in two frames or about two observers (named <paramref name="object2"/>).</exception>
    public static double? FrisbeeMax(StateVector object1, StateVector object2, double combinedHardBodyRadiusMeters)
    {
        (object1, object2) = Checked(object1, object2, combinedHardBodyRadiusMeters);
        if (object1.Covariance is not null && object2.Covariance is not null)
        {
            throw new ArgumentException(
                "Both states have a covariance: the maximum probability applies when one covariance alone is known; use Foster2D.",
                nameof(object2));
        }

        if (object1.Covariance is null && object2.Covariance is null)
        {
            return null;
        }

        return ConjunctionAssessment.CollisionRiskAt(object1, object2, combinedHardBodyRadiusMeters).ProbabilityOfCollision;
    }

    // The rules of the arguments, the pair in ICRF
    private static (StateVector, StateVector) Checked(StateVector object1, StateVector object2, double combinedHardBodyRadiusMeters)
    {
        ArgumentNullException.ThrowIfNull(object1);
        ArgumentNullException.ThrowIfNull(object2);
        if (!double.IsFinite(combinedHardBodyRadiusMeters) || combinedHardBodyRadiusMeters <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(combinedHardBodyRadiusMeters), combinedHardBodyRadiusMeters,
                "The combined hard-body radius must be finite and above 0.");
        }

        if (object1.Epoch != object2.Epoch || !object1.Frame.Equals(object2.Frame) || object1.Observer.NaifId != object2.Observer.NaifId)
        {
            throw new ArgumentException(
                $"The two states must be at one instant, in one frame and about one observer: {object1.Epoch}, {object1.Frame.Name}, " +
                $"{object1.Observer.Name} against {object2.Epoch}, {object2.Frame.Name}, {object2.Observer.Name}.", nameof(object2));
        }

        return object1.Frame.Equals(Frame.ICRF) ? (object1, object2) : (object1.ToFrame(Frame.ICRF), object2.ToFrame(Frame.ICRF));
    }
}
