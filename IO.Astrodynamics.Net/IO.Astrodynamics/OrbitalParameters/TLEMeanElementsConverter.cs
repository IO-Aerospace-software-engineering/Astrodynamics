using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using System;
using System.Linq;

namespace IO.Astrodynamics.OrbitalParameters
{
    public class TLEMeanElementsConverter
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
            // Convert to TEME frame (required for TLE)
            var targetState = (StateVector)orbitalParams.ToFrame(Frame.TEME);
            var targetKep = targetState.ToKeplerianElements();

            // Initial guess: use osculating elements directly
            var currentKep = targetKep;

            // Fixed-point iteration to find mean elements
            for (int iter = 0; iter < maxIter; iter++)
            {
                // Create TLE from current Keplerian elements
                var testTle = TLE.Create(currentKep, name, noradId, cosparId, revAtEpoch, 'U', bstar);

                // Propagate the TLE to get osculating elements at the same epoch
                var propagatedState = testTle.ToStateVector(targetState.Epoch).ToFrame(Frame.TEME);
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

            return currentKep;
            
        }

        /// <summary>
        /// Compute residuals between target and propagated Keplerian elements
        /// </summary>
        private static double[] ComputeResiduals(KeplerianElements target, KeplerianElements propagated)
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


        /// <summary>
        /// Alternative fitting method using position/velocity residuals (your original approach, improved)
        /// </summary>
        internal static TLE FitTleFromOrbitalParametersNewton(
            OrbitalParameters orbitalParams,
            ushort noradId,
            string name,
            string cosparId,
            ushort revAtEpoch = 0,
            double bstar = 0.0001,
            double tol = 1e-6,
            int maxIter = 15)
        {
            var targetState = (StateVector)orbitalParams.ToFrame(Frame.TEME);
            var kepInit = targetState.ToKeplerianElements();

            // Better scaling factors
            double[] scales =
            {
                1e-6, // Semi-major axis (Mm)
                1.0, // Eccentricity
                1.0, // Inclination (rad)
                1.0, // RAAN (rad)
                1.0, // Argument of periapsis (rad)
                1.0 // Mean anomaly (rad)
            };

            double[] x =
            {
                kepInit.SemiMajorAxis() * scales[0],
                kepInit.Eccentricity() * scales[1],
                kepInit.Inclination() * scales[2],
                kepInit.AscendingNode() * scales[3],
                kepInit.ArgumentOfPeriapsis() * scales[4],
                kepInit.MeanAnomaly() * scales[5]
            };

            // Newton-Raphson iteration
            x = NewtonRaphson.SolveVector(CostFunc, JacobianFunc, x, tol, maxIter);

            // Create final TLE
            var kepFinal = new KeplerianElements(
                x[0] / scales[0], x[1] / scales[1], x[2] / scales[2],
                x[3] / scales[3], x[4] / scales[4], x[5] / scales[5],
                targetState.Observer, targetState.Epoch, targetState.Frame
            );

            return TLE.Create(kepFinal, name, noradId, cosparId, revAtEpoch, 'U', bstar);

            double[] CostFunc(double[] param)
            {
                var kep = new KeplerianElements(
                    param[0] / scales[0], param[1] / scales[1], param[2] / scales[2],
                    param[3] / scales[3], param[4] / scales[4], param[5] / scales[5],
                    targetState.Observer, targetState.Epoch, targetState.Frame
                );

                var tle = TLE.Create(kep, name, noradId, cosparId, revAtEpoch, 'U', bstar);
                var sv = tle.ToStateVector(targetState.Epoch);

                // Residuals in km and km/s
                double posScale = 1e-3;
                double velScale = 1e-3;

                return new double[]
                {
                    (sv.Position.X - targetState.Position.X) * posScale,
                    (sv.Position.Y - targetState.Position.Y) * posScale,
                    (sv.Position.Z - targetState.Position.Z) * posScale,
                    (sv.Velocity.X - targetState.Velocity.X) * velScale,
                    (sv.Velocity.Y - targetState.Velocity.Y) * velScale,
                    (sv.Velocity.Z - targetState.Velocity.Z) * velScale
                };
            }

            double[,] JacobianFunc(double[] z)
            {
                var costFuncArray = Enumerable.Range(0, 6)
                    .Select(i => new Func<double[], double>(x => CostFunc(x)[i]))
                    .ToArray();
                var jacobian = new Jacobian(5, 2);
                var jmat = jacobian.Evaluate(costFuncArray, z);

                double[,] js = new double[6, 6];
                for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    js[i, j] = jmat.Get(i, j);
                return js;
            }
        }
    }
}