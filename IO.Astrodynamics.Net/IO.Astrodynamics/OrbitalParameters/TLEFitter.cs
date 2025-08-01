namespace IO.Astrodynamics.OrbitalParameters;

using System;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Math;

public class TLEFitter
{
    /// <summary>
    /// Fit a TLE from a state vector using the Newton-Raphson method.
    /// This method takes an initial guess of Keplerian elements from the state vector and iteratively refines it to minimize the difference between the state vector derived from the TLE and the target state vector.
    /// The fitting process uses a cost function that measures the difference in position and velocity between the TLE-derived state vector and the target state vector.
    /// The Jacobian of the cost function is computed to guide the Newton-Raphson iterations.
    /// The method returns a TLE object that best fits the provided orbital parameters, NORAD ID, name, and COSPAR ID.
    /// </summary>
    /// <param name="orbitalParams"></param>
    /// <param name="noradId"></param>
    /// <param name="name"></param>
    /// <param name="cosparId"></param>
    /// <param name="revAtEpoch"></param>
    /// <param name="tol"></param>
    /// <param name="maxIter"></param>
    /// <returns></returns>
    internal static TLE FitTleFromStateVector(
        OrbitalParameters orbitalParams,
        ushort noradId,
        string name,
        string cosparId,
        ushort revAtEpoch = 0,
        double tol = 1e-3,
        int maxIter = 15)
    {
        // 1. Initial guess: Keplerian elements from state vector
        var targetState = orbitalParams.ToStateVector();
        var kepInit = targetState.ToKeplerianElements();
        double[] x =
        [
            kepInit.SemiMajorAxis(),
            kepInit.Eccentricity(),
            kepInit.Inclination(),
            kepInit.AscendingNode(),
            kepInit.ArgumentOfPeriapsis(),
            kepInit.MeanAnomaly()
        ];

        // 4. Newton-Raphson vectorial loop
        x = NewtonRaphson.SolveVector(CostFunc, JacobianFunc, x, tol, maxIter);

        // 5. Return fitted TLE
        var kepFinal = new KeplerianElements(
            x[0], x[1], x[2], x[3], x[4], x[5],
            targetState.Observer, targetState.Epoch, targetState.Frame);
        return TLE.Create(kepFinal, name, noradId, cosparId, revAtEpoch);

        // 2. Define cost function (6-dim: 3 for position, 3 for velocity)
        double[] CostFunc(double[] param)
        {
            var kep = new KeplerianElements(param[0], param[1], param[2], param[3], param[4], param[5], targetState.Observer, targetState.Epoch, targetState.Frame);

            var tle = TLE.Create(kep, name, noradId, cosparId, revAtEpoch);
            var sv = tle.ToStateVector(targetState.Epoch);

            return
            [
                sv.Position.X - targetState.Position.X, sv.Position.Y - targetState.Position.Y, sv.Position.Z - targetState.Position.Z,
                0.1 * (sv.Velocity.X - targetState.Velocity.X), 0.1 * (sv.Velocity.Y - targetState.Velocity.Y), 0.1 * (sv.Velocity.Z - targetState.Velocity.Z)
            ];
        }

        // 3. Jacobian function
        double[,] JacobianFunc(double[] z)
        {
            Func<double[], double>[] costFuncArray = Enumerable.Range(0, 6)
                .Select(i => new Func<double[], double>(x => CostFunc(x)[i]))
                .ToArray();
            var jacobian = new Jacobian();
            var jmat = jacobian.Evaluate(costFuncArray, z);
            double[,] js = new double[6, 6];
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                js[i, j] = jmat.Get(i, j);
            return js;
        }
    }
}