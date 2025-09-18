using IO.Astrodynamics.Math;
using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.OrbitalParameters.TLE
{
    public class MeanElementsConverter
    {
        /// <summary>
        /// Default convergence tolerance (similar to OREKIT's EPSILON_DEFAULT)
        /// </summary>
        private const double DefaultEpsilon = 1.0e-10;
        
        /// <summary>
        /// Default maximum iterations (similar to OREKIT's MAX_ITERATIONS_DEFAULT)
        /// </summary>
        private const int DefaultMaxIterations = 100;
        
        /// <summary>
        /// Default scale factor (similar to OREKIT's SCALE_DEFAULT)
        /// </summary>
        private const double DefaultScale = 1.0;

        public MeanElementsConverter()
        {
        }

        /// <summary>
        /// Fit a TLE from a state vector using Fixed Point algorithm with Equinoctial elements
        /// for better handling of circular orbits, following OREKIT's approach.
        /// </summary>
        internal OrbitalParameters Convert(
            OrbitalParameters osculatingElements,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch = 0,
            double bstar = 0.0001,
            double tol = 1,
            int maxIter = 20)
        {
            if (osculatingElements == null) throw new ArgumentNullException(nameof(osculatingElements));
            
            // Convert to TEME frame (required for TLE)
            var targetState = (StateVector)osculatingElements.ToFrame(Frame.TEME);

            // Use OREKIT-like parameters for better convergence
            var epsilon = DefaultEpsilon;
            var maxIterations = System.Math.Max(maxIter, DefaultMaxIterations);
            var scale = DefaultScale;
            
            // Always use Equinoctial elements
            return ConvertUsingEquinoctialElements(targetState, noradId, name, cosparId, revAtEpoch, bstar, epsilon, maxIterations, scale);
        }

        /// <summary>
        /// Convert using Equinoctial elements for better numerical stability with circular/equatorial orbits
        /// Following OREKIT's FixedPointConverter approach
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
            
            // Compute threshold based on orbital period and epsilon (OREKIT approach)
            var n = System.Math.Sqrt(osculatingElements.Observer.GM / System.Math.Pow(targetEquinoctial.SemiMajorAxis(), 3));
            var threshold = epsilon * 2 * System.Math.PI / n; // Period-based threshold
            
            for (int iter = 0; iter < maxIterations; iter++)
            {
                try
                {
                    // Convert current equinoctial back to Keplerian for TLE creation
                    var currentKep = currentEquinoctial.ToKeplerianElements();
                    var testTle = TLE.Create(currentKep, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);
                    
                    // Propagate TLE and convert back to equinoctial for comparison
                    var propagatedState = testTle.ToStateVector(osculatingElements.Epoch);
                    var propagatedEquinoctial = propagatedState.ToEquinoctial();

                    // Compute position error for convergence check
                    var positionError = (osculatingElements.Position - propagatedState.Position).Magnitude();
                    var velocityError = (osculatingElements.Velocity - propagatedState.Velocity).Magnitude();

                    if (positionError < bestPositionError)
                    {
                        bestPositionError = positionError;
                        bestTle = testTle;
                    }

                    // Check convergence using position/velocity criteria
                    if (positionError < threshold && velocityError < epsilon * 1000) // Scale velocity threshold
                    {
                        return testTle;
                    }

                    // Compute residuals in equinoctial space (no singularities!)
                    var residuals = ComputeEquinoctialResiduals(targetEquinoctial, propagatedEquinoctial);

                    // Fixed-point correction with adaptive scaling
                    var adaptiveScale = scale * System.Math.Max(0.1, 1.0 - (double)iter / maxIterations);
                    
                    // Apply corrections to equinoctial elements
                    var newP = currentEquinoctial.P - adaptiveScale * residuals[0];
                    var newF = currentEquinoctial.F - adaptiveScale * residuals[1];
                    var newG = currentEquinoctial.G - adaptiveScale * residuals[2];
                    var newH = currentEquinoctial.H - adaptiveScale * residuals[3];
                    var newK = currentEquinoctial.K - adaptiveScale * residuals[4];
                    var newL0 = SpecialFunctions.NormalizeAngle(currentEquinoctial.L0 - adaptiveScale * residuals[5]);

                    // Apply physical constraints
                    var celestialBody = osculatingElements.Observer as CelestialBody;
                    var minRadius = celestialBody?.EquatorialRadius ?? 6378136.6;
                    newP = System.Math.Max(newP, minRadius + 100000); // Ensure minimum altitude

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
            if (bestTle != null && bestPositionError < 10000.0) // Accept up to 10km error for very problematic cases
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
            return new double[] 
            {
                propagated.P - target.P,
                propagated.F - target.F,
                propagated.G - target.G,
                propagated.H - target.H,
                propagated.K - target.K,
                SpecialFunctions.NormalizeAngleDifference(propagated.L0 - target.L0)
            };
        }
    }
}
