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
    /// Maximum degrees for the geopotential model.
    /// </summary>
    public double MaxDegrees { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopotentialGravitationalField"/> class.
    /// </summary>
    /// <param name="geopotentialModelFile">The stream reader for the geopotential model file.</param>
    /// <param name="maxDegrees">The maximum degrees for the geopotential model. Default is 70.</param>
    public GeopotentialGravitationalField(StreamReader geopotentialModelFile, ushort maxDegrees = 70)
    {
        MaxDegrees = maxDegrees;
        _geopotentialModelReader = new GeopotentialModelReader(geopotentialModelFile);
    }

    /// <summary>
    /// Computes the gravitational acceleration for a given state vector.
    /// </summary>
    /// <param name="stateVector">The state vector for which to compute the gravitational acceleration.</param>
    /// <returns>The gravitational acceleration as a <see cref="Vector3"/>.</returns>
    public override Vector3 ComputeGravitationalAcceleration(StateVector stateVector)
    {
        var observer = (CelestialBody)stateVector.Observer;
        var svFixedFrame = stateVector.ToFrame(observer.Frame).ToStateVector();
        var position = svFixedFrame.Position;
        double r = position.Magnitude();

        // Theta angle in body fixed frame
        double theta = System.Math.Abs(System.Math.Asin(position.Z / r) - Constants.PI2);

        // Get gravitational acceleration
        var gravitationalAcceleration = -base.ComputeGravitationalAcceleration(svFixedFrame).Magnitude();

        // Compute spherical harmonics
        double eqr = observer.EquatorialRadius / r;
        double omegaN = 0.0;
        double eqrn;

        for (ushort n = 2; n <= MaxDegrees; n++)
        {
            eqrn = System.Math.Pow(eqr, n);

            for (ushort m = 0; m <= n; m++)
            {
                var coefficients = GetCoefficients(n, m);
                double longitude_m = m * System.Math.Atan2(position.Y, position.X);

                omegaN += eqrn * LegendreFunctions.NormalizedAssociatedLegendre(n, m, theta) * (coefficients.C * System.Math.Cos(longitude_m));
                if (m != 0)
                {
                    omegaN += eqrn * LegendreFunctions.NormalizedAssociatedLegendre(n, m, theta) * (coefficients.S * System.Math.Sin(longitude_m));
                }
            }
        }

        // Cumulate gravity and spherical harmonics
        var localAcceleration = gravitationalAcceleration * (1 + omegaN);

        // Return acceleration in original frame
        return (position * localAcceleration / r).Rotate(svFixedFrame.Frame.ToFrame(stateVector.Frame, stateVector.Epoch).Rotation);
    }

    /// <summary>
    /// Gets the coefficients for the given degree and order.
    /// </summary>
    /// <param name="n">The degree.</param>
    /// <param name="m">The order.</param>
    /// <returns>The <see cref="GeopotentialCoefficient"/> for the given degree and order.</returns>
    GeopotentialCoefficient GetCoefficients(ushort n, ushort m)
    {
        return _geopotentialModelReader.ReadCoefficient(n, m);
    }
}