using BenchmarkDotNet.Running;

namespace IO.Astrodynamics.Performance;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}