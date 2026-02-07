// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body;

public class GeopotentialGravitationalField : GravitationalField
{
    /// <summary>
    /// Reader for the geopotential model.
    /// </summary>
    private readonly GeopotentialModelReader _geopotentialModelReader;

    /// <summary>
    /// Maximum degree for the geopotential model.
    /// </summary>
    public int MaxDegree { get; }

    // Pre-allocated buffers for performance
    private readonly double[][] _P;
    private readonly double[][] _dP;
    private readonly double[] _cosMLambda;
    private readonly double[] _sinMLambda;

    // 2D indexed coefficients for O(1) access: _coefficients[n][m]
    private readonly GeopotentialCoefficient[][] _coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopotentialGravitationalField"/> class.
    /// </summary>
    /// <param name="geopotentialModelFile">The stream reader for the geopotential model file.</param>
    /// <param name="maxDegrees">The maximum degrees for the geopotential model. Default is 70.</param>
    public GeopotentialGravitationalField(StreamReader geopotentialModelFile, ushort maxDegrees = 70)
    {
        MaxDegree = maxDegrees;
        _geopotentialModelReader = new GeopotentialModelReader(geopotentialModelFile);

        // Allocate Legendre buffers
        (_P, _dP) = LegendreFunctions.AllocateLegendreTable(MaxDegree);

        // Allocate trig buffers
        _cosMLambda = new double[MaxDegree + 1];
        _sinMLambda = new double[MaxDegree + 1];

        // Build 2D coefficient array for O(1) indexed access
        _coefficients = new GeopotentialCoefficient[MaxDegree + 1][];
        for (int n = 0; n <= MaxDegree; n++)
        {
            _coefficients[n] = new GeopotentialCoefficient[n + 1];
            for (int m = 0; m <= n; m++)
            {
                try
                {
                    _coefficients[n][m] = _geopotentialModelReader.ReadCoefficient((ushort)n, (ushort)m);
                }
                catch
                {
                    // Coefficient not available in model — use zeros
                    _coefficients[n][m] = new GeopotentialCoefficient((ushort)n, (ushort)m, 0.0, 0.0, 0.0, 0.0);
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatible property for code that references MaxDegrees as double.
    /// </summary>
    public double MaxDegrees => MaxDegree;

    /// <summary>
    /// Computes the full 3D gravitational acceleration including geopotential harmonics
    /// using the Montenbruck &amp; Gill spherical coordinate gradient formulation.
    /// </summary>
    /// <param name="stateVector">The state vector for which to compute the gravitational acceleration.</param>
    /// <returns>The gravitational acceleration as a <see cref="Vector3"/>.</returns>
    public override Vector3 ComputeGravitationalAcceleration(StateVector stateVector)
    {
        var observer = (CelestialBody)stateVector.Observer;
        var svFixedFrame = stateVector.ToFrame(observer.Frame).ToStateVector();
        var position = svFixedFrame.Position;

        double x = position.X;
        double y = position.Y;
        double z = position.Z;
        double r = position.Magnitude();

        // Geocentric latitude φ and longitude λ (fixes Bug 4: no Math.Abs)
        double rhoXY = System.Math.Sqrt(x * x + y * y);
        double sinPhi = z / r;
        double cosPhi = rhoXY / r;
        double lambda = System.Math.Atan2(y, x);

        double GM = observer.GM;
        double Re = observer.EquatorialRadius;

        // Compute Legendre table with derivatives
        LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(MaxDegree, sinPhi, cosPhi, _P, _dP);

        // Pre-compute cos(m·λ) and sin(m·λ) via trig recurrence
        double cosLambda = System.Math.Cos(lambda);
        double sinLambda = System.Math.Sin(lambda);
        _cosMLambda[0] = 1.0;
        _sinMLambda[0] = 0.0;
        if (MaxDegree >= 1)
        {
            _cosMLambda[1] = cosLambda;
            _sinMLambda[1] = sinLambda;
        }

        for (int m = 2; m <= MaxDegree; m++)
        {
            _cosMLambda[m] = 2.0 * cosLambda * _cosMLambda[m - 1] - _cosMLambda[m - 2];
            _sinMLambda[m] = 2.0 * cosLambda * _sinMLambda[m - 1] - _sinMLambda[m - 2];
        }

        // Sum the three gradient components:
        //   dV/dr   (radial) — includes (n+1) factor
        //   dV/dφ   (latitude)
        //   dV/dλ   (longitude)
        double dVdr = 0.0;
        double dVdPhi = 0.0;
        double dVdLambda = 0.0;

        double reOverR = Re / r;
        double reOverRn = reOverR; // Will be (Re/r)^n for n starting at 1; we init for n=2 below

        for (int n = 2; n <= MaxDegree; n++)
        {
            reOverRn = System.Math.Pow(reOverR, n);

            for (int m = 0; m <= n; m++)
            {
                var coeff = _coefficients[n][m];
                double Cnm = coeff.C;
                double Snm = coeff.S;

                double cosmL = _cosMLambda[m];
                double sinmL = _sinMLambda[m];

                double CcosSplussin = Cnm * cosmL + Snm * sinmL;
                double ScosmCsin = Snm * cosmL - Cnm * sinmL;

                double Pnm = _P[n][m];
                double dPnm = _dP[n][m];

                // Radial: -(n+1) * (Re/r)^n * P̄_nm * (C̄·cos(mλ) + S̄·sin(mλ))
                dVdr += (n + 1) * reOverRn * Pnm * CcosSplussin;

                // Latitude: (Re/r)^n * dP̄_nm/dφ * (C̄·cos(mλ) + S̄·sin(mλ))
                dVdPhi += reOverRn * dPnm * CcosSplussin;

                // Longitude: m * (Re/r)^n * P̄_nm * (S̄·cos(mλ) - C̄·sin(mλ))
                dVdLambda += m * reOverRn * Pnm * ScosmCsin;
            }
        }

        // Convert to acceleration in spherical coordinates:
        //   a_r = -(GM/r²) * [1 + dVdr_sum]  (central body + harmonics)
        //   a_φ = (GM/r²) * dVdPhi_sum
        //   a_λ = (GM/r²) * dVdLambda_sum / cosφ
        double GMoverR2 = GM / (r * r);
        double ar = -GMoverR2 * (1.0 + dVdr);
        double aPhi = GMoverR2 * dVdPhi;

        const double cosPhiEpsilon = 1.0e-15;
        double aLambda = System.Math.Abs(cosPhi) > cosPhiEpsilon
            ? GMoverR2 * dVdLambda / cosPhi
            : 0.0;

        // Transform spherical (r, φ, λ) → body-fixed Cartesian (x, y, z)
        double cosL = cosLambda;
        double sinL = sinLambda;

        double ax = ar * cosPhi * cosL - aPhi * sinPhi * cosL - aLambda * sinL;
        double ay = ar * cosPhi * sinL - aPhi * sinPhi * sinL + aLambda * cosL;
        double az = ar * sinPhi + aPhi * cosPhi;

        var bodyFixedAcceleration = new Vector3(ax, ay, az);

        // Rotate from body-fixed to original inertial frame
        return bodyFixedAcceleration.Rotate(svFixedFrame.Frame.ToFrame(stateVector.Frame, stateVector.Epoch).Rotation);
    }
}
