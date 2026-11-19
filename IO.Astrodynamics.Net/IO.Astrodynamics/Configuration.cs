using System;
using IO.Astrodynamics.DataProvider;

namespace IO.Astrodynamics;

public class Configuration
{
    public static Configuration Instance { get; } = new();
    /// <summary>
    /// The data provider of the process: a <see cref="SpiceDataProvider"/> until <see cref="SetDataProvider"/> sets
    /// another.
    /// </summary>
    /// <remarks>
    /// Which objects read it, and when (structuring choice 1 of #668, decided by S. Guillet, 2026-11-19): an object built
    /// by the caller (a <see cref="Body.CelestialBody"/>, a <see cref="Surface.Site"/>) reads the provider set when it is
    /// built; a static object of <see cref="SolarSystemObjects.PlanetsAndMoons"/>, <see cref="SolarSystemObjects.Stars"/>
    /// and <see cref="SolarSystemObjects.Barycenters"/> keeps the provider of its first read; a static
    /// <see cref="Frames.Frame"/> reads this provider at each transform. See the Data Providers guide. Public since
    /// 11.0.0 (R02 of #668), read only: there is no scoped provider.
    /// </remarks>
    public IDataProvider DataProvider { get; private set; }
    
    private Configuration()
    {
        DataProvider = new SpiceDataProvider();
    }
    
    /// <summary>
    /// Set a new data provider
    /// </summary>
    /// <param name="dataProvider"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void SetDataProvider(IDataProvider dataProvider)
    {
        DataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }
    
}