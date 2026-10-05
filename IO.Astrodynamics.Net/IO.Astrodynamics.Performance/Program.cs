using BenchmarkDotNet.Running;

namespace IO.Astrodynamics.Performance;

class Program
{
    static void Main(string[] args)
    {
        // Without arguments every benchmark runs; pass BenchmarkDotNet options (e.g. --filter *VVBenchmarks*) to select.
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Length > 0 ? args : ["--filter", "*"]);
        //var scenario=new VelocityScenario();
        //scenario.Propagator();
        // Console.ReadKey();
    }
}