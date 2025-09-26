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
        /// Convert using Equinoctial elements for better numerical stability with circular/equatorial orbits
        /// Following FixedPointConverter approach
        /// </summary>
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
            // Convert target to Equinoctial elements for numerical stability
            var targetEquinoctial = osculatingElements.ToEquinoctial();

            // Initial guess: use target equinoctial elements directly
            var currentEquinoctial = new EquinoctialElements(
                targetEquinoctial.P, targetEquinoctial.F, targetEquinoctial.G,
                targetEquinoctial.H, targetEquinoctial.K, targetEquinoctial.L0,
                osculatingElements.Observer, osculatingElements.Epoch, osculatingElements.Frame);

            double bestPositionError = double.MaxValue;
            OrbitalParameters bestTle = null;

            // Compute thresholds based on orbit size and speed (meters and m/s)
            var mu = osculatingElements.Observer.GM;
            var aGuess = targetEquinoctial.SemiMajorAxis();
            var vScale = System.Math.Sqrt(mu / aGuess);
            var positionThreshold = epsilon * aGuess; // e.g., 1e-10 * a ~ sub-millimeter for LEO
            var velocityThreshold = epsilon * vScale; // consistent velocity scale

            for (int iter = 0; iter < maxIterations; iter++)
            {
                try
                {
                    // Convert current equinoctial back to Keplerian for TLE creation
                    var testTle = TLE.Create(currentEquinoctial, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);

                    // Propagate TLE and convert back to equinoctial for comparison
                    var propagatedState = testTle.ToStateVector(osculatingElements.Epoch);
                    var propagatedEquinoctial = propagatedState.ToEquinoctial();

                    // Compute position/velocity errors for convergence check
                    var posVec = osculatingElements.Position - propagatedState.Position;
                    var velVec = osculatingElements.Velocity - propagatedState.Velocity;
                    var positionError = posVec.Magnitude();
                    var velocityError = velVec.Magnitude();

                    if (positionError < bestPositionError)
                    {
                        bestPositionError = positionError;
                        bestTle = testTle;
                    }

                    // Check convergence using position AND velocity criteria
                    if (positionError < positionThreshold && velocityError < velocityThreshold)
                    {
                        return testTle;
                    }

                    // Compute residuals in equinoctial space (no singularities!)
                    var residuals = ComputeEquinoctialResiduals(targetEquinoctial, propagatedEquinoctial);

                    // Basic component-wise scaling to account for units and coupling
                    // Scale P residual by semi-major axis to keep steps moderate
                    var pScale = System.Math.Max(1.0, aGuess);
                    var fScale = 1.0; // dimensionless
                    var gScale = 1.0; // dimensionless
                    var hScale = 1.0; // dimensionless
                    var kScale = 1.0; // dimensionless
                    var lScale = 1.0; // radians

                    // Fixed-point correction with adaptive scaling
                    var adaptiveScale = scale * System.Math.Max(0.1, 1.0 - (double)iter / maxIterations);

                    // Apply corrections to equinoctial elements
                    var newP = currentEquinoctial.P - adaptiveScale * (residuals[0] / pScale) * pScale;
                    var newF = currentEquinoctial.F - adaptiveScale * (residuals[1] / fScale) * fScale;
                    var newG = currentEquinoctial.G - adaptiveScale * (residuals[2] / gScale) * gScale;
                    var newH = currentEquinoctial.H - adaptiveScale * (residuals[3] / hScale) * hScale;
                    var newK = currentEquinoctial.K - adaptiveScale * (residuals[4] / kScale) * kScale;
                    var newL0 = SpecialFunctions.NormalizeAngle(currentEquinoctial.L0 - adaptiveScale * (residuals[5] / lScale) * lScale);

                    // Enforce physical constraints via perigee constraint using P,F,G
                    // a = P/(1-e^2), rp = a(1-e) must be >= minRadius + safetyAltitude
                    var e2 = newF * newF + newG * newG;
                    var e = System.Math.Sqrt(System.Math.Max(0.0, System.Math.Min(e2, 0.999999))); // keep e < 1
                    var celestialBody = osculatingElements.Observer as CelestialBody;
                    var minBodyRadius = celestialBody?.EquatorialRadius ?? EQUATORIAL_RADIUS_EARTH;

                    // Recompute a from P and e, then check perigee
                    var denom = System.Math.Max(1e-12, 1.0 - e * e);
                    var aNow = newP / denom;
                    var rp = aNow * (1.0 - e);
                    var rpMin = minBodyRadius + SAFETY_ALTITUDE;
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
                        osculatingElements.Observer, osculatingElements.Epoch, osculatingElements.Frame);
                }
                catch (Exception)
                {
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
        /// Compute residuals between target and propagated Equinoctial elements
        /// </summary>
        private static double[] ComputeEquinoctialResiduals(EquinoctialElements target, EquinoctialElements propagated)
        {
            return
            [
                propagated.P - target.P,
                propagated.F - target.F,
                propagated.G - target.G,
                propagated.H - target.H,
                propagated.K - target.K,
                SpecialFunctions.NormalizeAngleDifference(propagated.L0 - target.L0)
            ];
        }
    }
}