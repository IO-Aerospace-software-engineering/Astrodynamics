using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.TimeSystem;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Math
{
    public static class Lagrange
    {
        /// <summary>
        /// Interpolates a value using Lagrange interpolation.
        /// </summary>
        /// <param name="data">An array of tuples containing x and y values.</param>
        /// <param name="idx">The x value at which to interpolate the y value.</param>
        /// <returns>The interpolated y value at the given x value.</returns>
        public static double Interpolate((double x, double y)[] data, double idx)
        {
            int n = data.Length;
            double result = 0; // Initialize result

            for (int i = 0; i < n; i++)
            {
                // Compute individual terms of above formula
                double term = data[i].y;
                for (int j = 0; j < n; j++)
                {
                    if (j != i)
                        term = term * (idx - data[j].x) / (data[i].x - data[j].x);
                }

                // Add current term to result
                result += term;
            }

            return result;
        }

        private static List<StateVector> GetNearestStateVectors(StateVector[] stateVectors, Time targetDate, int k)
        {
            return stateVectors.OrderBy(sv => System.Math.Abs((sv.Epoch - targetDate).TotalSeconds))
                .Take(k)
                .ToList();
        }

        /// <summary>
        /// Interpolates the state vector at a given epoch using Lagrange interpolation.
        /// </summary>
        /// <param name="stateVectors">Array of state vectors to interpolate from.</param>
        /// <param name="date">The date at which to interpolate the state vector.</param>
        /// <param name="k"></param>
        /// <returns>The interpolated state vector at the given epoch.</returns>
        public static StateVector Interpolate(StateVector[] stateVectors, Time date, int k = 8)
        {
            // Obtenir les k points les plus proches
            List<StateVector> nearestStateVectors = GetNearestStateVectors(stateVectors, date, k);
            return InterpolateNearest(nearestStateVectors, date);
        }

        /// <summary>
        /// <see cref="Interpolate(StateVector[], Time, int)"/> over states already in strictly increasing epoch order, without
        /// sorting them: the <paramref name="k"/> nearest are found by a binary search and a merge outwards, in the order the
        /// stable sort by distance gives them (the earlier state first at equal distances), so that the result is bit for bit
        /// that of <see cref="Interpolate(StateVector[], Time, int)"/> on the same states (#522, #540).
        /// </summary>
        /// <param name="sorted">The states, in strictly increasing epoch order; at least one.</param>
        /// <param name="date">The epoch of the interpolation.</param>
        /// <param name="k">The number of states of the polynomial.</param>
        internal static StateVector InterpolateSorted(IReadOnlyList<StateVector> sorted, Time date, int k = 8)
        {
            int count = sorted.Count;
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid].Epoch < date) lo = mid + 1;
                else hi = mid;
            }

            int left = lo - 1, right = lo;
            var nearest = new List<StateVector>(System.Math.Min(k, count));
            while (nearest.Count < k && (left >= 0 || right < count))
            {
                if (right >= count || (left >= 0 && Distance(sorted[left], date) <= Distance(sorted[right], date)))
                {
                    nearest.Add(sorted[left--]);
                }
                else
                {
                    nearest.Add(sorted[right++]);
                }
            }

            return InterpolateNearest(nearest, date);
        }

        // The distance of the former sort, in the same arithmetic
        private static double Distance(StateVector sv, in Time date) => System.Math.Abs((sv.Epoch - date).TotalSeconds);

        private static StateVector InterpolateNearest(List<StateVector> nearestStateVectors, Time date)
        {

            // Convertir les dates en secondes depuis la première date du sous-ensemble
            double[] t = nearestStateVectors.Select(sv => (sv.Epoch - nearestStateVectors[0].Epoch).TotalSeconds).ToArray();
            double tTarget = (date - nearestStateVectors[0].Epoch).TotalSeconds;

            int n = nearestStateVectors.Count;

            // Vérifier si la date cible correspond à l'une des dates dans nearestStateVectors
            for (int i = 0; i < n; i++)
            {
                if (System.Math.Abs(t[i] - tTarget) < 1e-9)
                {
                    return nearestStateVectors[i];
                }
            }

            // Initialiser les coefficients de Lagrange
            double[] L = new double[n];

            // Calcul des coefficients de Lagrange
            for (int i = 0; i < n; i++)
            {
                L[i] = 1.0;
                for (int j = 0; j < n; j++)
                {
                    if (i != j)
                    {
                        double denominator = t[i] - t[j];
                        if (System.Math.Abs(denominator) > 1e-10)
                        {
                            L[i] *= (tTarget - t[j]) / denominator;
                        }
                    }
                }
            }

            // Interpoler la position et la vitesse séparément
            Vector3 interpolatedPosition = new Vector3(0, 0, 0);
            Vector3 interpolatedVelocity = new Vector3(0, 0, 0);

            for (int i = 0; i < n; i++)
            {
                interpolatedPosition += nearestStateVectors[i].Position * L[i];
                interpolatedVelocity += nearestStateVectors[i].Velocity * L[i];
            }

            // Retourner le vecteur état interpolé
            return new StateVector(interpolatedPosition, interpolatedVelocity, nearestStateVectors[0].Observer, date, nearestStateVectors[0].Frame);
        }
    }
}