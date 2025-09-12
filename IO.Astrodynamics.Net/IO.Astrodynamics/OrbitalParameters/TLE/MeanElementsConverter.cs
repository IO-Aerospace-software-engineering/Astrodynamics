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
            
            // Use OREKIT's approach: detect circular orbits and use Equinoctial elements
            var isCircular = targetKep.E < 0.01;
            var isEquatorial = System.Math.Abs(targetKep.I) < 0.01 || System.Math.Abs(targetKep.I - System.Math.PI) < 0.01;
            var useEquinoctial = isCircular || isEquatorial;

            // Use OREKIT-like parameters for better convergence
            var epsilon = DefaultEpsilon;
            var maxIterations = System.Math.Max(maxIter, DefaultMaxIterations);
            var scale = DefaultScale;
            
            if (useEquinoctial)
            {
                return ConvertUsingEquinoctialElements(targetState, noradId, name, cosparId, revAtEpoch, bstar, epsilon, maxIterations, scale);
            }
            else
            {
                return ConvertUsingKeplerianElements(targetState, noradId, name, cosparId, revAtEpoch, bstar, epsilon, maxIterations, scale);
            }
        }

        /// <summary>
        /// Convert using Equinoctial elements for better numerical stability with circular/equatorial orbits
        /// Following OREKIT's FixedPointConverter approach
        /// </summary>
        private OrbitalParameters ConvertUsingEquinoctialElements(
            StateVector targetState,
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
            var targetEquinoctial = targetState.ToEquinoctial();
            
            // Initial guess: use target equinoctial elements directly
            var currentEquinoctial = new EquinoctialElements(
                targetEquinoctial.P, targetEquinoctial.F, targetEquinoctial.G,
                targetEquinoctial.H, targetEquinoctial.K, targetEquinoctial.L0,
                targetState.Observer, targetState.Epoch, targetState.Frame);

            double bestPositionError = double.MaxValue;
            OrbitalParameters bestTle = null;
            
            // Compute threshold based on orbital period and epsilon (OREKIT approach)
            var n = System.Math.Sqrt(targetState.Observer.GM / System.Math.Pow(targetEquinoctial.SemiMajorAxis(), 3));
            var threshold = epsilon * 2 * System.Math.PI / n; // Period-based threshold
            
            for (int iter = 0; iter < maxIterations; iter++)
            {
                try
                {
                    // Convert current equinoctial back to Keplerian for TLE creation
                    var currentKep = currentEquinoctial.ToKeplerianElements();
                    var testTle = TLE.Create(currentKep, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);
                    
                    // Propagate TLE and convert back to equinoctial for comparison
                    var propagatedState = testTle.ToStateVector(targetState.Epoch);
                    var propagatedEquinoctial = propagatedState.ToEquinoctial();

                    // Compute position error for convergence check
                    var positionError = (targetState.Position - propagatedState.Position).Magnitude();
                    var velocityError = (targetState.Velocity - propagatedState.Velocity).Magnitude();

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
                    var celestialBody = targetState.Observer as CelestialBody;
                    var minRadius = celestialBody?.EquatorialRadius ?? 6378136.6;
                    newP = System.Math.Max(newP, minRadius + 100000); // Ensure minimum altitude

                    // Create new equinoctial elements for next iteration
                    currentEquinoctial = new EquinoctialElements(
                        newP, newF, newG, newH, newK, newL0,
                        targetState.Observer, targetState.Epoch, targetState.Frame);
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
        /// Convert using traditional Keplerian elements for non-circular orbits
        /// </summary>
        private OrbitalParameters ConvertUsingKeplerianElements(
            StateVector targetState,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch,
            double bstar,
            double epsilon,
            int maxIterations,
            double scale)
        {
            var targetKep = targetState.ToKeplerianElements();
            var currentKep = new KeplerianElements(
                targetKep.A, targetKep.E, targetKep.I, targetKep.RAAN, 
                targetKep.AOP, targetKep.M, targetKep.Observer, 
                targetKep.Epoch, targetKep.Frame);

            double bestPositionError = double.MaxValue;
            OrbitalParameters bestTle = null;
            
            // Compute threshold based on orbital period
            var n = System.Math.Sqrt(targetKep.Observer.GM / (targetKep.A * targetKep.A * targetKep.A));
            var threshold = epsilon * 2 * System.Math.PI / n;
            
            for (int iter = 0; iter < maxIterations; iter++)
            {
                try
                {
                    var testTle = TLE.Create(currentKep, name, noradId, cosparId, revAtEpoch, Classification.Unclassified, bstar);
                    var propagatedState = testTle.ToStateVector(targetState.Epoch);
                    var propagatedKep = propagatedState.ToKeplerianElements();

                    var positionError = (targetState.Position - propagatedState.Position).Magnitude();
                    var velocityError = (targetState.Velocity - propagatedState.Velocity).Magnitude();

                    if (positionError < bestPositionError)
                    {
                        bestPositionError = positionError;
                        bestTle = testTle;
                    }

                    if (positionError < threshold && velocityError < epsilon * 1000)
                    {
                        return testTle;
                    }

                    var residuals = ComputeKeplerianResiduals(targetKep, propagatedKep);
                    var adaptiveScale = scale * System.Math.Max(0.1, 1.0 - (double)iter / maxIterations);
                    
                    var newA = currentKep.A - adaptiveScale * residuals[0];
                    var newE = currentKep.E - adaptiveScale * residuals[1];
                    var newI = currentKep.I - adaptiveScale * residuals[2];
                    var newRaan = SpecialFunctions.NormalizeAngle(currentKep.RAAN - adaptiveScale * residuals[3]);
                    var newAop = SpecialFunctions.NormalizeAngle(currentKep.AOP - adaptiveScale * residuals[4]);
                    var newM = SpecialFunctions.NormalizeAngle(currentKep.M - adaptiveScale * residuals[5]);

                    // Apply constraints
                    var celestialBody = targetState.Observer as CelestialBody;
                    var minRadius = celestialBody?.EquatorialRadius ?? 6378136.6;
                    newA = System.Math.Max(newA, minRadius + 100000);
                    newE = System.Math.Max(0.0, System.Math.Min(0.99, newE));
                    newI = System.Math.Max(0.0, System.Math.Min(System.Math.PI, newI));

                    currentKep = new KeplerianElements(
                        newA, newE, newI, newRaan, newAop, newM,
                        targetState.Observer, targetState.Epoch, targetState.Frame);
                }
                catch (Exception)
                {
                    break;
                }
            }

            if (bestTle != null && bestPositionError < 1000.0)
            {
                return bestTle;
            }

            throw new InvalidOperationException($"TLE conversion using Keplerian elements failed after {maxIterations} iterations. " +
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

        /// <summary>
        /// Compute residuals between target and propagated Keplerian elements
        /// </summary>
        private static double[] ComputeKeplerianResiduals(KeplerianElements target, KeplerianElements propagated)
        {
            return new double[] 
            {
                propagated.A - target.A,
                propagated.E - target.E,
                propagated.I - target.I,
                SpecialFunctions.NormalizeAngleDifference(propagated.RAAN - target.RAAN),
                SpecialFunctions.NormalizeAngleDifference(propagated.AOP - target.AOP),
                SpecialFunctions.NormalizeAngleDifference(propagated.M - target.M)
            };
        }
    }
}
