using BenchmarkDotNet.Attributes;
using IO.Astrodynamics.Math;
using MathNet.Numerics.LinearAlgebra;

namespace IO.Astrodynamics.Performance;

public class VelocityMath
{
    private readonly double _rand;
    private List<Vector<double>> _vectors = new List<Vector<double>>();
    private readonly Vector<double> _v1;
    private readonly Vector3 _v2;

    public VelocityMath()
    {
        _rand = Random.Shared.NextDouble();
        _v1 = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(3, i => _rand * i);
        _v2 = new Vector3(_rand * 1, _rand * 2, _rand * 3);
    }

    // [Benchmark(Description = "MathNet vector")]
    public void VectorMathNet()
    {
        Vector<double> res;
        for (int i = 0; i < 1000000; i++)
        {
            res = _v1 * _rand * i;
        }
    }

    // [Benchmark(Description = "Math IO Vector", Baseline = true)]
    public void VectorIO()
    {
        Vector3 res;
        for (int i = 0; i < 1000000; i++)
        {
            res = _v2 * _rand * i;
        }
        
        IO.Astrodynamics.Math.Jacobian j=new IO.Astrodynamics.Math.Jacobian();
    }
}