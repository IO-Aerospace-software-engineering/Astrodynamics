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
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        _epoch = new TimeSystem.Time(new DateTime(2024, 1, 1), TimeScale.UTC);
        _sv = new StateVector(new Vector3(6800000.0, 1000.0, 0.0), new Vector3(100.0, 8000.0, 0.0), CelestialItem.Create(399), _epoch, Frames.Frame.ICRF);
    }

    // [Benchmark(Description = "Convert StateVector to TLE")]
    public void ComputeTLE()
    {
        var tle = _sv.ToTle("TestSatellite", 25666, "98067A", new IO.Astrodynamics.OrbitalParameters.TLE.TleFitOptions { MaxIterations = 20000 });
    }
}