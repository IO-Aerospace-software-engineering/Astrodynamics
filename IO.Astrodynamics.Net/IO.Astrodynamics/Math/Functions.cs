using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IO.Astrodynamics.Math
{
    public class SpecialFunctions
    {
        /// <summary>
        /// Computes the value of a hypergeometric series for the given input.
        /// </summary>
        /// <remarks>The method iteratively computes the hypergeometric series until the absolute value of
        /// the next term is less than or equal to the specified tolerance. The series converges for sufficiently small
        /// values of <paramref name="z"/>. For larger values, the caller should ensure that the tolerance is
        /// appropriately set.</remarks>
        /// <param name="z">The input value for the series. Typically represents a parameter in the hypergeometric function.</param>
        /// <param name="tol">The tolerance value that determines the stopping condition for the series computation. Must be positive.</param>
        /// <returns>The computed value of the hypergeometric series, accurate to within the specified tolerance.</returns>
        public static double Hypergeometric(double z, double tol)
        {
            double sum = 1.0;
            double term = 1.0;
            double error = 1.0;
            double nextTerm = 0.0;
            double nextSum = 0.0;
            int iteration = 0;
            while (error > tol)
            {
                nextTerm = term * (3.0 + iteration) * (1.0 + iteration) / ((2.5 + iteration) * (iteration + 1)) * z;
                nextSum = sum + nextTerm;
                error = System.Math.Abs(nextTerm);
                sum = nextSum;
                term = nextTerm;
                iteration++;
            }
            return sum;
        }
    }
}
