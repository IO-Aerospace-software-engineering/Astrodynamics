using IO.Astrodynamics.Math;
using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.OrbitalParameters.TLE
{
    /// <summary>
    /// Converts between osculating and mean orbital elements using fixed-point iteration
    /// following the FixedPointConverter approach with equinoctial elements
    /// </summary>
    public class MeanElementsConverter
    {
        const double SAFETY_ALTITUDE = 120000.0; // 120 km minimum altitude for LEO
        const double EQUATORIAL_RADIUS_EARTH = 6378136.6; // meters
        const double POSITION_TOLERANCE = 1E+04; // 10 km position tolerance for best-effort fallback
        const int MAX_NON_IMPROVING_ITERATIONS = 50;  // Increased from 20
        const int MIN_ITERATIONS_BEFORE_EARLY_EXIT = 50;  // Don't exit early before 50 iterations

        /// <summary>
        /// Fit a TLE from a state vector using the upgraded
        /// </summary>
        internal OrbitalParameters Convert(
            OrbitalParameters osculatingElements,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch = 0,
            double bstar = 0.0001,
            int maxIter = 200)
        {
            if (osculatingElements == null) throw new ArgumentNullException(nameof(osculatingElements));

            // Convert to TEME frame (required for TLE)
            var targetState = (StateVector)osculatingElements.ToFrame(Frame.TEME);

            // Fallback to original method if new algorithm fails
            return ConvertUsingEquinoctialElements(targetState, noradId, name, cosparId, revAtEpoch, bstar, 1e-10, maxIter, 1.0);
        }

        /// <summary>
        /// Convert using Equinoctial elements for better numerical stability and convergence 
        /// </summary>
        /// <param name="osculatingElements"></param>
        /// <param name="noradId"></param>
        /// <param name="name"></param>
        /// <param name="cosparId"></param>
        /// <param name="revAtEpoch"></param>
        /// <param name="bstar"></param>
        /// <param name="epsilon"></param>
        /// <param name="maxIterations"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private OrbitalParameters ConvertUsingEquinoctialElements(
            StateVector osculatingElements,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch,
            double bstar,
            double epsilon,
            int maxIterations,
            double scale)
        {
            // Convert target to Equinoctial elements for numerical stability (reuse directly)
            var currentEquinoctial = osculatingElements.ToEquinoctial();
            var targetEquinoctial = currentEquinoctial;

            double bestPositionError = double.MaxValue;
            OrbitalParameters bestTle = null;

            // Cache frequently accessed values outside the loop
            var mu = osculatingElements.Observer.GM;
            var observer = osculatingElements.Observer;
            var epoch = osculatingElements.Epoch;
            var frame = osculatingElements.Frame;
            var targetPosition = osculatingElements.Position;
            var targetVelocity = osculatingElements.Velocity;
            var celestialBody = observer as CelestialBody;
            var minBodyRadius = celestialBody?.EquatorialRadius ?? EQUATORIAL_RADIUS_EARTH;
            var rpMin = minBodyRadius + SAFETY_ALTITUDE;

            // Compute thresholds based on orbit size and speed (meters and m/s)
            var aGuess = targetEquinoctial.SemiMajorAxis();
            var vScale = System.Math.Sqrt(mu / aGuess);
            var positionThreshold = epsilon * aGuess; // e.g., 1e-10 * a ~ sub-millimeter for LEO
            var velocityThreshold = epsilon * vScale; // consistent velocity scale

            // Reusable arrays to avoid allocations
            Span<double> residuals = stackalloc double[6];
            
            // Track non-improving iterations for early exit (but only after some minimum iterations)
            int nonImprovingIterations = 0;
            

            for (int iter = 0; iter < maxIterations; iter++)
            {
                try
                {
                    // Convert current equinoctial back to Keplerian for TLE creation
                    // Use internal method since we're iteratively fitting from osculating elements
                    var testTle = TLE.CreateInternal(currentEquinoctial, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);

                    // Propagate TLE and convert back to equinoctial for comparison
                    var propagatedState = testTle.ToStateVector(epoch);
                    
                    // Compute position/velocity errors for convergence check (before expensive ToEquinoctial)
                    var posVec = targetPosition - propagatedState.Position;
                    var velVec = targetVelocity - propagatedState.Velocity;
                    var positionError = posVec.Magnitude();
                    var velocityError = velVec.Magnitude();

                    // Track best solution
                    if (positionError < bestPositionError)
                    {
                        bestPositionError = positionError;
                        bestTle = testTle;
                        nonImprovingIterations = 0;
                    }
                    else
                    {
                        nonImprovingIterations++;
                        // Early exit only after minimum iterations and if really not improving
                        if (iter >= MIN_ITERATIONS_BEFORE_EARLY_EXIT && nonImprovingIterations >= MAX_NON_IMPROVING_ITERATIONS)
                        {
                            break;
                        }
                    }

                    // Check convergence using position AND velocity criteria
                    if (positionError < positionThreshold && velocityError < velocityThreshold)
                    {
                        return testTle;
                    }

                    // Only compute equinoctial if we need to continue iterating
                    var propagatedEquinoctial = propagatedState.ToEquinoctial();

                    // Compute residuals in equinoctial space (no singularities!)
                    ComputeEquinoctialResidualsInPlace(targetEquinoctial, propagatedEquinoctial, residuals);

                    // Fixed-point correction with adaptive scaling
                    var adaptiveScale = scale * System.Math.Max(0.1, 1.0 - (double)iter / maxIterations);

                    // Apply corrections to equinoctial elements
                    var newP = currentEquinoctial.P - adaptiveScale * residuals[0];
                    var newF = currentEquinoctial.F - adaptiveScale * residuals[1];
                    var newG = currentEquinoctial.G - adaptiveScale * residuals[2];
                    var newH = currentEquinoctial.H - adaptiveScale * residuals[3];
                    var newK = currentEquinoctial.K - adaptiveScale * residuals[4];
                    var newL0 = SpecialFunctions.NormalizeAngle(currentEquinoctial.L0 - adaptiveScale * residuals[5]);

                    // Enforce physical constraints via perigee constraint using P,F,G
                    // a = P/(1-e^2), rp = a(1-e) must be >= minRadius + safetyAltitude
                    var e2 = newF * newF + newG * newG;
                    var e = System.Math.Sqrt(System.Math.Max(0.0, System.Math.Min(e2, 0.999999))); // keep e < 1

                    // Recompute a from P and e, then check perigee
                    var denom = System.Math.Max(1e-12, 1.0 - e * e);
                    var aNow = newP / denom;
                    var rp = aNow * (1.0 - e);
                    
                    if (rp < rpMin)
                    {
                        // Increase a to satisfy perigee constraint while keeping e
                        var aReq = rpMin / (1.0 - e);
                        newP = aReq * (1.0 - e * e);
                        aNow = aReq;
                    }

                    // Update semi-major axis guess for scaling next iteration
                    aGuess = aNow;

                    // Create new equinoctial elements for next iteration
                    currentEquinoctial = new EquinoctialElements(
                        newP, newF, newG, newH, newK, newL0,
                        observer, epoch, frame);
                }
                catch (Exception ex)
                {
                    // Log specific error for debugging
                    System.Diagnostics.Debug.WriteLine($"TLE conversion iteration {iter} failed: {ex.Message}");
                    break;
                }
            }

            // Return best solution found
            if (bestTle != null && bestPositionError < POSITION_TOLERANCE) // Accept up to 10km error for very problematic cases
            {
                return bestTle;
            }

            throw new InvalidOperationException($"TLE conversion using Equinoctial elements failed after {maxIterations} iterations. " +
                                                $"Best position error: {bestPositionError:F2}m");
        }

        /// <summary>
        /// Compute residuals between target and propagated Equinoctial elements in-place
        /// to avoid array allocation overhead
        /// </summary>
        private static void ComputeEquinoctialResidualsInPlace(EquinoctialElements target, EquinoctialElements propagated, Span<double> residuals)
        {
            residuals[0] = propagated.P - target.P;
            residuals[1] = propagated.F - target.F;
            residuals[2] = propagated.G - target.G;
            residuals[3] = propagated.H - target.H;
            residuals[4] = propagated.K - target.K;
            residuals[5] = SpecialFunctions.NormalizeAngleDifference(propagated.L0 - target.L0);
        }
    }
}