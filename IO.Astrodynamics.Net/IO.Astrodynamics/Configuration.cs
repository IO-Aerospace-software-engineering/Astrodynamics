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
    
    public void SetDataProvider(IDataProvider dataProvider)
    {
        DataProvider = dataProvider;
    }
    
}