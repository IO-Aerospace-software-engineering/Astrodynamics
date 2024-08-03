using System;

namespace IO.Astrodynamics.Physics;

public class Tsiolkovski
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
        return isp * Constants.g0 * System.Math.Log(initialMass / finalMass);
    }

    /// <summary>
    /// Compute DeltaT
    /// </summary>
    /// <param name="isp"></param>
    /// <param name="initialMass"></param>
    /// <param name="fuelFlow"></param>
    /// <param name="deltaV"></param>
    /// <returns></returns>
    public static TimeSpan DeltaT(double isp, double initialMass, double fuelFlow, double deltaV)
    {
        return TimeSpan.FromSeconds(initialMass / fuelFlow * (1 - System.Math.Exp(-deltaV / (isp * Constants.g0))));
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
        return initialMass * (1 - System.Math.Exp(-deltaV / (isp * Constants.g0)));
    }
}