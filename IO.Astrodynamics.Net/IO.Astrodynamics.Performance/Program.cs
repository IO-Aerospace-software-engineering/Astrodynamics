using BenchmarkDotNet.Running;

namespace IO.Astrodynamics.Performance;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        //var scenario=new VelocityScenario();
        //scenario.Propagator();
        // Console.ReadKey();
    }
}