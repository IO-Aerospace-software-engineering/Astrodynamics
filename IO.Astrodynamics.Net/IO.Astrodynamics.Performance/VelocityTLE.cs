using BenchmarkDotNet.Attributes;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Performance;

[MemoryDiagnoser]
[SkewnessColumn]
[KurtosisColumn]
[StatisticalTestColumn]
[ShortRunJob]
public class VelocityTLE
{
    private Time _epoch;
    private StateVector _sv;

    public VelocityTLE()
    {
        API.Instance.LoadKernels(new DirectoryInfo("Data"));
        _epoch = new TimeSystem.Time(new DateTime(2024, 1, 1), TimeFrame.UTCFrame);
        _sv = new StateVector(new Vector3(6800000.0, 1000.0, 0.0), new Vector3(100.0, 8000.0, 0.0), CelestialItem.Create(399), _epoch, Frames.Frame.ICRF);
    }

    [Benchmark(Description = "Convert StateVector to TLE")]
    public void ComputeTLE()
    {
        var tle = _sv.ToTLE(new OrbitalParameters.TLE.Configuration(25666, "TestSatellite", "98067A",MaxIterations:20000));
    }
}