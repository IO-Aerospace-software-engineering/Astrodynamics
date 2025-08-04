using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using System;
using System.Linq;

namespace IO.Astrodynamics.OrbitalParameters.TLE
{
    public class MeanElementsConverter
    {
        /// <summary>
        /// Fit a TLE from a state vector using an iterative approach similar to OREKIT's fixed-point algorithm.
        /// This method converts osculating elements to mean elements that are compatible with SGP4 propagation.
        /// </summary>
        /// <param name="orbitalParams">The osculating orbital parameters to fit</param>
        /// <param name="noradId">NORAD catalog number</param>
        /// <param name="name">Satellite name</param>
        /// <param name="cosparId">International designator</param>
        /// <param name="revAtEpoch">Revolution number at epoch</param>
        /// <param name="bstar">BSTAR drag term</param>
        /// <param name="tol">Convergence tolerance</param>
        /// <param name="maxIter">Maximum iterations</param>
        /// <returns>A TLE that best represents the input orbital parameters</returns>
        internal OrbitalParameters Convert(
            OrbitalParameters orbitalParams,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch = 0,
            double bstar = 0.0001,
            double tol = 1,
            int maxIter = 20)
        {
            if (orbitalParams == null) throw new ArgumentNullException(nameof(orbitalParams));
            // Convert to TEME frame (required for TLE)
            var targetState = (StateVector)orbitalParams.ToFrame(Frame.TEME);
            var targetKep = targetState.ToKeplerianElements();

            // Initial guess: use osculating elements directly
            var currentKep = targetKep;

            // Fixed-point iteration to find mean elements
            for (int iter = 0; iter < maxIter; iter++)
            {
                // Create TLE from current Keplerian elements
                var testTle = TLE.Create(currentKep, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);

                // Propagate the TLE to get osculating elements at the same epoch
                var propagatedState = testTle.ToStateVector(targetState.Epoch);
                var propagatedKep = propagatedState.ToKeplerianElements();

                // Compute residuals (difference between target and propagated)
                var residuals = ComputeResiduals(targetKep, propagatedKep);

                // Check convergence
                if (residuals.Max(System.Math.Abs) < tol)
                {
                    return testTle;
                }

                // Update mean elements by subtracting the residuals
                // mean = osculating - (propagated - osculating)
                var updatedKep = new KeplerianElements(
                    currentKep.A - residuals[0],
                    System.Math.Max(0.0, currentKep.E - residuals[1]), // Ensure eccentricity >= 0
                    currentKep.I - residuals[2],
                    currentKep.RAAN - residuals[3],
                    currentKep.AOP - residuals[4],
                    SpecialFunctions.NormalizeAngle(currentKep.M - residuals[5]),
                    targetState.Observer,
                    targetState.Epoch,
                    targetState.Frame
                );

                currentKep = updatedKep;
            }

            throw new InvalidOperationException($"TLE conversion failed to converge after {maxIter} iterations. Increase tolerance or max iterations if necessary.");
            
        }

        /// <summary>
        /// Compute residuals between target and propagated Keplerian elements
        /// </summary>
        private static double[] ComputeResiduals(KeplerianElements target, KeplerianElements propagated)
        {
            return
            [
                propagated.A - target.A,
                propagated.E - target.E,
                propagated.I - target.I,
                SpecialFunctions.NormalizeAngleDifference(propagated.RAAN - target.RAAN),
                SpecialFunctions.NormalizeAngleDifference(propagated.AOP - target.AOP),
                SpecialFunctions.NormalizeAngleDifference(propagated.M - target.M)
            ];
        }
    }
}