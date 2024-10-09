using System;
using IO.Astrodynamics.DataProvider;

namespace IO.Astrodynamics;

public class Configuration
{
    public static Configuration Instance { get; } = new();
    internal IDataProvider DataProvider { get; private set; }
    
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