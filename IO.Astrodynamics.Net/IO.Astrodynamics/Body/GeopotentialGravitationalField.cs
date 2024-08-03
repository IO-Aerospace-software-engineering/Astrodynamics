// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body;

public class GeopotentialGravitationalField : GravitationalField
{
    private readonly GeopotentialModelReader _geopotentialModelReader;
    public double MaxDegrees { get; }

    public GeopotentialGravitationalField(StreamReader geopotentialModelFile, ushort maxDegrees = 70)
    {
        MaxDegrees = maxDegrees;
        _geopotentialModelReader = new GeopotentialModelReader(geopotentialModelFile);
    }

    public override Vector3 ComputeGravitationalAcceleration(StateVector stateVector)
    {
        var observer = (CelestialBody)stateVector.Observer;
        var svFixedFrame = stateVector.ToFrame(observer.Frame).ToStateVector();
        var position = svFixedFrame.Position;
        double r = position.Magnitude();

        //Theta angle in body fixed frame
        double theta = System.Math.Abs(System.Math.Asin(position.Z / r) - Constants.PI2);

        //Get gravitational acceleration
        var gravitationalAcceleration = -base.ComputeGravitationalAcceleration(svFixedFrame).Magnitude();

        //Compute spherical harmonics
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

        //Cumulate gravity and spherical harmonics
        var localAcceleration = gravitationalAcceleration * (1 + omegaN);

        //Return acceleration in original frame
        return (position * localAcceleration / r).Rotate(svFixedFrame.Frame.ToFrame(stateVector.Frame, stateVector.Epoch).Rotation);
    }

    GeopotentialCoefficient GetCoefficients(ushort n, ushort m)
    {
        return _geopotentialModelReader.ReadCoefficient(n, m);
    }
}