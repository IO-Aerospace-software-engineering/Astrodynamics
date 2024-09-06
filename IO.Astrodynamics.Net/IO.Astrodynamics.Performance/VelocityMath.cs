using BenchmarkDotNet.Attributes;
using IO.Astrodynamics.Math;
using MathNet.Numerics.LinearAlgebra;

namespace IO.Astrodynamics.Performance;

public class VelocityMath
{
    private double rand;
    private List<Vector<double>> vectors = new List<Vector<double>>();
    private Vector<double> V1 = null;
    private Vector3 V2;

    public VelocityMath()
    {
        rand = Random.Shared.NextDouble();
        V1 = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(3, i => rand * i);
        V2 = new Vector3(rand * 1, rand * 2, rand * 3);
    }

    // [Benchmark(Description = "MathNet vector")]
    public void VectorMathNet()
    {
        Vector<double> res;
        for (int i = 0; i < 1000000; i++)
        {
            res = V1 * rand * i;
        }
    }

    // [Benchmark(Description = "Math IO Vector", Baseline = true)]
    public void VectorIO()
    {
        Vector3 res;
        for (int i = 0; i < 1000000; i++)
        {
            res = V2 * rand * i;
        }
        
        IO.Astrodynamics.Math.Jacobian j=new IO.Astrodynamics.Math.Jacobian();
    }
}