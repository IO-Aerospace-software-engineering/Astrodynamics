using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters;

public class InitialOrbitDetermination
{
    /// <summary>
    /// Computes the orbital parameters of the observed body.
    /// </summary>
    /// <param name="observation1"></param>
    /// <param name="observation2"></param>
    /// <param name="observation3"></param>
    /// <param name="observer"></param>
    /// <param name="expectedRangeFromObserver"></param>
    /// <returns></returns>
    public static OrbitalParameters CreateFromObservation_Gauss(Equatorial observation1, Equatorial observation2, Equatorial observation3, ILocalizable observer,
        CelestialItem expectedCenterOfMotion, double expectedRangeFromObserver)
    {
        Console.WriteLine($"obs1 declination = {observation1.Declination}");
        Console.WriteLine($"obs1 right ascension = {observation1.RightAscension}");
        Console.WriteLine($"obs2 declination = {observation2.Declination}");
        Console.WriteLine($"obs2 right ascension = {observation2.RightAscension}");
        Console.WriteLine($"obs3 declination = {observation3.Declination}");
        Console.WriteLine($"obs3 right ascension = {observation3.RightAscension}");


        var distanceScale = expectedCenterOfMotion.IsSun ? Constants.AU : 1E03;
        //var secondsPerDay = 86400.0;
        double mu = expectedCenterOfMotion.GM / System.Math.Pow(distanceScale, 3);
        // Step 1: Compute observer positions with improved scaling
        Vector3 R1 = observer
            .GetEphemeris(observation1.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / distanceScale;

        Vector3 R2 = observer
            .GetEphemeris(observation2.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / distanceScale;

        Vector3 R3 = observer
            .GetEphemeris(observation3.Epoch, expectedCenterOfMotion, Frame.ICRF, Aberration.LT)
            .ToStateVector()
            .Position / distanceScale;

        Console.WriteLine($"R1={R1 * distanceScale / 1000.0}");
        Console.WriteLine($"R2={R2 * distanceScale / 1000.0}");
        Console.WriteLine($"R3={R3 * distanceScale / 1000.0}");

        // Improved scaling factor based on expected range
        // double scaleR = expectedRangeFromObserver / distanceScale;


        Console.WriteLine($"obs1 cartesian = {observation1.ToCartesian()}");
        Console.WriteLine($"obs2 cartesian = {observation2.ToCartesian()}");
        Console.WriteLine($"obs3 cartesian = {observation3.ToCartesian()}");
        // Step 2: Convert equatorial coordinates to unit direction vectors
        Vector3 rhoHat1 = observation1.ToDirection();
        Vector3 rhoHat2 = observation2.ToDirection();
        Vector3 rhoHat3 = observation3.ToDirection();

        // Step 3: Time differences with improved precision
        double tau1 = (observation1.Epoch - observation2.Epoch).TotalSeconds;
        double tau3 = (observation3.Epoch - observation2.Epoch).TotalSeconds;
        double tau = tau3 - tau1;

        // Step 4: Compute determinants with improved numerical stability
        Vector3 p1 = rhoHat2.Cross(rhoHat3);
        Vector3 p2 = rhoHat1.Cross(rhoHat3);
        Vector3 p3 = rhoHat1.Cross(rhoHat2);
        Console.WriteLine($"p1={p1}");
        Console.WriteLine($"p2={p2}");
        Console.WriteLine($"p3={p3}");

        double d0 = rhoHat1 * p1;
        double d11 = R1 * p1;
        double d12 = R1 * p2;
        double d13 = R1 * p3;
        double d21 = R2 * p1;
        double d22 = R2 * p2;
        double d23 = R2 * p3;
        double d31 = R3 * p1;
        double d32 = R3 * p2;
        double d33 = R3 * p3;
        Console.WriteLine($"d0={d0}");
        Console.WriteLine($"d11={d11}");
        Console.WriteLine($"d12={d12}");
        Console.WriteLine($"d13={d13}");
        Console.WriteLine($"d21={d21}");
        Console.WriteLine($"d22={d22}");
        Console.WriteLine($"d23={d23}");
        Console.WriteLine($"d31={d31}");
        Console.WriteLine($"d32={d32}");
        Console.WriteLine($"d33={d33}");


        // Step 5: Compute scaled coefficients with improved numerical stability
        double A = (1.0 / d0) * (-d12 * (tau3 / tau) + d22 + d32 * (tau1 / tau));
        double B = (1.0 / (6.0 * d0 * tau)) *
                   (-d12 * tau3 * (tau * tau - tau3 * tau3) + d32 * tau1 * (tau * tau - tau1 * tau1));

        // Step 6: Solve eighth-degree polynomial with improved coefficients
        double E = R2 * rhoHat2;
        Console.WriteLine($"A={A}");
        Console.WriteLine($"B={B}");
        Console.WriteLine($"E={E}");
        
        double polynomialA = -(A * A + 2.0 * A * E + R2.MagnitudeSquared());
        double polynomialB = -2.0 * mu * B * (A + E);
        double polynomialC = -(mu * mu) * (B * B);

        Func<double, double> function = r2 => System.Math.Pow(r2, 8) + polynomialA * System.Math.Pow(r2, 6) +
                                              polynomialB * System.Math.Pow(r2, 3) + polynomialC;

        Func<double, double> derivative = r2 => 8.0 * System.Math.Pow(r2, 7) +
                                                6.0 * polynomialA * System.Math.Pow(r2, 5) +
                                                3.0 * polynomialB * System.Math.Pow(r2, 2);

        // Use improved initial guess based on expected range
        double scaledInitialGuess = expectedRangeFromObserver / distanceScale;
        double root_distance = NewtonRaphson.Solve(function, derivative, scaledInitialGuess, 1E-12, 1000);

        double r23 = System.Math.Pow(root_distance, 3);
        double numerator = 6 * (d31 * (tau1 / tau3) + d21 * (tau / tau3)) * r23 +
                           mu * d31 * (tau * tau - tau1 * tau1) * (tau1 / tau3);

        double denominator = 6 * r23 + mu * (tau * tau - tau3 * tau3);

        double rho1 = (1 / d0) * ((numerator / denominator) - d11);


        // Calculate ρ₃ (rho3)
        double numerator3 = 6 * (d13 * (tau3 / tau1) - d23 * (tau / tau1)) * r23 + mu * d13 * (tau * tau - tau3 * tau3) * (tau3 / tau1);

        double denominator3 = 6 * r23 + mu * (tau * tau - tau1 * tau1);

        double rho3 = (1 / d0) * ((numerator3 / denominator3) - d33);

        // Rescale the root and compute final position and velocity
        //root_distance *= distanceScale;
        // Step 6: Compute rho1 and rho3


        // Calculate ρ₂ (rho2)
        double rho2 = A + ((mu * B) / r23);
        Console.WriteLine($"rho1={rho1}");
        Console.WriteLine($"rho2={rho2}");
        Console.WriteLine($"rho3={rho3}");
        
        // Step 7: Compute position vectors
        Vector3 r1 = rhoHat1 * rho1 + R1;
        Vector3 r2 = rhoHat2 * rho2 + R2;
        Vector3 r3 = rhoHat3 * rho3 + R3;

        // Calculate higher-order terms for f1 and f3
        double r2Cubed = System.Math.Pow(r2.Magnitude(), 3);
        double r2Sixth = System.Math.Pow(r2.Magnitude(), 6);

        double f1 = 1 - (mu * System.Math.Pow(tau1, 2)) / (2 * r2Cubed) +
                    (mu * mu * System.Math.Pow(tau1, 4)) / (24 * r2Sixth);

        double f3 = 1 - (mu * System.Math.Pow(tau3, 2)) / (2 * r2Cubed) +
                    (mu * mu * System.Math.Pow(tau3, 4)) / (24 * r2Sixth);

// Calculate higher-order terms for g1 and g3
        double g1 = tau1 - (mu * System.Math.Pow(tau1, 3)) / (6 * r2Cubed) +
                    (mu * mu * System.Math.Pow(tau1, 5)) / (120 * r2Sixth);

        double g3 = tau3 - (mu * System.Math.Pow(tau3, 3)) / (6 * r2Cubed) +
                    (mu * mu * System.Math.Pow(tau3, 5)) / (120 * r2Sixth);


        // Step 9: Compute velocity vector using full Lagrange formulation
        Vector3 v2 = (r1 * -f3 + r3 * f1) * (1 / (f1 * g3 - f3 * g1));

        // Step 10: Convert position and velocity to Keplerian elements
        return new StateVector(r2 * distanceScale, v2 * distanceScale, expectedCenterOfMotion, observation2.Epoch, Frame.ICRF);
    }
}