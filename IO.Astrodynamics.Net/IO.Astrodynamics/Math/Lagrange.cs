using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Math
{
    public static class Lagrange
    {
        public static double Interpolate((double x, double y)[] data, double idx)
        {
            int n = data.Length;
            double result = 0; // Initialize result

            for (int i = 0; i < n; i++)
            {
                // Compute individual terms
                // of above formula
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

        public static StateVector Interpolate(StateVector[] data, DateTime epoch)
        {
            double idx = epoch.SecondsFromJ2000TDB();
            int n = data.Length;
            StateVector result = new StateVector(new Vector3(), new Vector3(), data[0].Observer, epoch, data[0].Frame); // Initialize result

            for (int i = 0; i < n; i++)
            {
                // Compute individual terms
                // of above formula
                Vector3 posTerm = data[i].Position;
                Vector3 velTerm = data[i].Velocity;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    var t = (idx - data[j].Epoch.SecondsFromJ2000TDB()) / (data[i].Epoch.SecondsFromJ2000TDB() - data[j].Epoch.SecondsFromJ2000TDB());
                    posTerm *= t;
                    velTerm *= t;
                }

                // Add current term to result
                result.UpdatePosition(result.Position + posTerm);
                result.UpdateVelocity(result.Velocity + velTerm);
            }

            return result;
        }
    }
}