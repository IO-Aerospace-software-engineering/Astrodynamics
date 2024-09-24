using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

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

        /// <summary>
        /// Interpolates the state vector at a given epoch using Lagrange interpolation.
        /// </summary>
        /// <param name="data">Array of state vectors to interpolate from.</param>
        /// <param name="date">The date at which to interpolate the state vector.</param>
        /// <returns>The interpolated state vector at the given epoch.</returns>
        public static StateVector Interpolate(StateVector[] data, Time date)
        {
            double idx = date.TimeSpanFromJ2000().TotalSeconds;
            int n = data.Length;
            StateVector result = new StateVector(new Vector3(), new Vector3(), data[0].Observer, date, data[0].Frame); // Initialize result

            // Store time in seconds from J2000 for each state
            double[] times = new double[n];
            for (int i = 0; i < n; i++)
            {
                times[i] = data[i].Epoch.TimeSpanFromJ2000().TotalSeconds;
            }

            for (int i = 0; i < n; i++)
            {
                // Position and velocity for term i
                Vector3 posTerm = data[i].Position;
                Vector3 velTerm = data[i].Velocity;

                // Compute coefficients for index i
                for (int j = 0; j < n; j++)
                {
                    if (j != i)
                    {
                        double denominator = times[i] - times[j];
                        if (denominator == 0) continue; // Handle case where denominator is 0
                        double t = (idx - times[j]) / denominator;

                        posTerm *= t;
                        velTerm *= t;
                    }
                }

                // Add terms to result
                result.UpdatePosition(result.Position + posTerm);
                result.UpdateVelocity(result.Velocity + velTerm);
            }

            return result;
        }
    }
}