using System;

namespace IO.Astrodynamics.Physics;

public class Tsiolkovsky
{
    /// <summary>
    /// Compute DeltaV
    /// </summary>
    /// <param name="isp"></param>
    /// <param name="initialMass"></param>
    /// <param name="finalMass"></param>
    /// <returns></returns>
    public static double DeltaV(double isp, double initialMass, double finalMass)
    {
        return isp * Constants.StandardGravity * System.Math.Log(initialMass / finalMass);
    }

    /// <summary>
    /// Compute DeltaT
    /// </summary>
    /// <param name="isp"></param>
    /// <param name="initialMass"></param>
    /// <param name="fuelFlow"></param>
    /// <param name="deltaV"></param>
    /// <returns>The burn duration m0 / q (1 − exp(−Δv / (Isp g0))), to the nearest tick (100 ns), ties to even (#637,
    /// decided by S. Guillet, 2026-11-17; truncated to the tick below before 11.0.0).</returns>
    public static TimeSpan DeltaT(double isp, double initialMass, double fuelFlow, double deltaV)
    {
        return TimeSystem.Time.SecondsToSpan(initialMass / fuelFlow * (1 - System.Math.Exp(-deltaV / (isp * Constants.StandardGravity))));
    }

    /// <summary>
    /// Compute DeltaM
    /// </summary>
    /// <param name="isp"></param>
    /// <param name="initialMass"></param>
    /// <param name="deltaV"></param>
    /// <returns></returns>
    public static double DeltaM(double isp, double initialMass, double deltaV)
    {
        return initialMass * (1 - System.Math.Exp(-deltaV / (isp * Constants.StandardGravity)));
    }
}