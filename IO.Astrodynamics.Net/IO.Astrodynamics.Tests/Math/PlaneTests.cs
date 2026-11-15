using System;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Math;

public class PlaneTests
{
    private readonly ITestOutputHelper _output;

    public PlaneTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetAngle_ReturnsZero_ForIdenticalPlanes()
    {
        var plane1 = new Plane(new Vector3(1, 0, 0));
        var plane2 = new Plane(new Vector3(1, 0, 0));
        var result = plane1.GetAngle(plane2);
        Assert.Equal(0, result, 5);
    }

    [Fact]
    public void GetAngle_ReturnsPiOverTwo_ForPerpendicularPlanes()
    {
        var plane1 = new Plane(new Vector3(1, 0, 0));
        var plane2 = new Plane(new Vector3(0, 1, 0));
        var result = plane1.GetAngle(plane2);
        Assert.Equal(System.Math.PI / 2, result, 5);
    }

    [Fact]
    public void GetAngle_ReturnsPi_ForOppositePlanes()
    {
        var plane1 = new Plane(new Vector3(1, 0, 0));
        var plane2 = new Plane(new Vector3(-1, 0, 0));
        var result = plane1.GetAngle(plane2);
        Assert.Equal(System.Math.PI, result, 5);
    }

    [Fact]
    public void GetAngle_ReturnsCorrectAngle_ForArbitraryPlanes()
    {
        var plane1 = new Plane(new Vector3(1, 1, 0));
        var plane2 = new Plane(new Vector3(1, -1, 0));
        var result = plane1.GetAngle(plane2);
        Assert.Equal(System.Math.PI / 2, result, 5);
    }

    [Fact]
    public void GetAngleWithVector_ReturnsZero_ForParallelVector()
    {
        var plane = new Plane(new Vector3(1, 0, 0));
        var vector = new Vector3(1, 0, 0);
        var result = plane.GetAngle(vector);
        Assert.Equal(Astrodynamics.Constants.PI2, result, 6);
    }

    [Fact]
    public void GetAngleWithVector_ReturnsPiOverTwo_ForPerpendicularVector()
    {
        var plane = new Plane(new Vector3(1, 0, 0));
        var vector = new Vector3(0, 1, 0);
        var result = plane.GetAngle(vector);
        Assert.Equal(0.0, result, 6);
    }

    [Fact]
    public void GetAngleWithVector_ReturnsPi_ForOppositeVector()
    {
        var plane = new Plane(new Vector3(1, 0, 0));
        var vector = new Vector3(-1, 0, 0);
        var result = plane.GetAngle(vector);
        Assert.Equal(-System.Math.PI / 2, result, 6);
    }

    [Fact]
    public void GetAngleWithVector_ReturnsCorrectAngle_ForArbitraryVector()
    {
        var plane = new Plane(new Vector3(1, 1, 0));
        var vector = new Vector3(1, -1, 0);
        var result = plane.GetAngle(vector);
        Assert.Equal(0.0, result, 6);
    }

    [Fact]
    public void Create()
    {
        var plane = new Plane(new Vector3(1, 1, 0), 12.0);
        Assert.Equal(new Vector3(1, 1, 0), plane.Normal);
        Assert.Equal(12.0, plane.Distance);
    }

    [Fact]
    public void GetX()
    {
        var plane = Plane.X;
        Assert.Equal(Vector3.VectorX, plane.Normal);
        Assert.Equal(0.0, plane.Distance);
    }

    [Fact]
    public void GetY()
    {
        var plane = Plane.Y;
        Assert.Equal(Vector3.VectorY, plane.Normal);
        Assert.Equal(0.0, plane.Distance);
    }

    [Fact]
    public void GetZ()
    {
        var plane = Plane.Z;
        Assert.Equal(Vector3.VectorZ, plane.Normal);
        Assert.Equal(0.0, plane.Distance);
    }

    // Third round of #668: GetAngle divided by the product of the magnitudes unchecked, so a zero or non-finite normal
    // (default(Plane) and new Plane() included: a record struct's default has the zero normal) gave NaN silently. A plane
    // whose normal is zero or not finite has no orientation: InvalidOperationException for this plane,
    // ArgumentException (plane) for the argument
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-0.0, 0.0, 0.0)]
    [InlineData(double.NaN, 0.0, 1.0)]
    [InlineData(0.0, double.PositiveInfinity, 0.0)]
    [InlineData(0.0, 0.0, double.NegativeInfinity)]
    public void GetAngle_ZeroOrNonFiniteNormal_Throws(double x, double y, double z)
    {
        var plane = new Plane(new Vector3(x, y, z));
        Assert.Throws<InvalidOperationException>(() => plane.GetAngle(Plane.Z));
        Assert.Throws<InvalidOperationException>(() => plane.GetAngle(Vector3.VectorX));
        Assert.Equal("plane", Assert.Throws<ArgumentException>(() => Plane.Z.GetAngle(plane)).ParamName);
    }

    [Fact]
    public void GetAngle_DefaultPlane_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => default(Plane).GetAngle(Plane.Z));
        Assert.Throws<InvalidOperationException>(() => new Plane().GetAngle(Vector3.VectorX));
        Assert.Equal("plane", Assert.Throws<ArgumentException>(() => Plane.Z.GetAngle(default(Plane))).ParamName);
    }

    // The angle of a zero or non-finite vector to a plane is undefined: it was NaN
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(double.NaN, 0.0, 0.0)]
    [InlineData(double.PositiveInfinity, 0.0, 0.0)]
    public void GetAngle_ZeroOrNonFiniteVector_Throws(double x, double y, double z)
    {
        Assert.Equal("vector", Assert.Throws<ArgumentException>(() => Plane.Z.GetAngle(new Vector3(x, y, z))).ParamName);
    }

    // Parallel and antiparallel normals: the quotient (n1 · n2) / (|n1| |n2|) rounds past ±1 and acos or asin gave NaN, about
    // a quarter of the time (478 904 of 2e6 pairs on the base of the third round of #668). The closed forms: 0 between a and
    // a s, π between a and -a s, π/2 between the plane of normal a and the vector a s, for s > 0; acos and asin are
    // sensitive near ±1 (an error δ of the quotient gives √(2δ), 2.98e-8 rad for one ulp of 1 below it), so the
    // worst, MEASURE 2.980e-08 rad for each of the four (Linux x64, 2026-11-15), is twice the worst rounded up at the second
    // significant digit, provisional until the three platforms are read from the CI logs
    private const double ParallelTolerance = 6.0e-8;

    [Fact]
    public void GetAngle_ParallelNormals_AreZeroOrPi()
    {
        var random = new Random(668);
        double worstSame = 0.0, worstOpposite = 0.0, worstVector = 0.0, worstVectorOpposite = 0.0;
        int pairs = 0;
        for (int i = 0; i < 100000; i++)
        {
            var a = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0);
            if (a.Magnitude() < 0.1) continue;
            double s = 0.1 + 10.0 * random.NextDouble();
            pairs++;

            worstSame = System.Math.Max(worstSame, System.Math.Abs(new Plane(a).GetAngle(new Plane(a * s))));
            worstOpposite = System.Math.Max(worstOpposite, System.Math.Abs(new Plane(a).GetAngle(new Plane(a * -s)) - System.Math.PI));
            worstVector = System.Math.Max(worstVector, System.Math.Abs(new Plane(a).GetAngle(a * s) - System.Math.PI / 2.0));
            worstVectorOpposite = System.Math.Max(worstVectorOpposite, System.Math.Abs(new Plane(a).GetAngle(a * -s) + System.Math.PI / 2.0));
        }

        _output.WriteLine($"MEASURE Plane.GetAngle parallel normals ({pairs} pairs): {worstSame:E3} rad, antiparallel {worstOpposite:E3} rad");
        _output.WriteLine($"MEASURE Plane.GetAngle normal and parallel vector: {worstVector:E3} rad, antiparallel {worstVectorOpposite:E3} rad");
        // NaN fails each comparison
        Assert.True(worstSame <= ParallelTolerance, $"{worstSame:E3}");
        Assert.True(worstOpposite <= ParallelTolerance, $"{worstOpposite:E3}");
        Assert.True(worstVector <= ParallelTolerance, $"{worstVector:E3}");
        Assert.True(worstVectorOpposite <= ParallelTolerance, $"{worstVectorOpposite:E3}");
    }

    // Normals outside about 1e-154 to 1e154: their unscaled magnitudes overflowed (NaN) or lost their digits (1.3e-4 rad off
    // at 1e-160). The closed form: π/4 between the normals (1, 0, 0) and (1, 1, 0) at any scale, within one ulp of π/4;
    // π/2 between Plane.Z and a tiny vector along z
    [Theory]
    [InlineData(1000)]
    [InlineData(1022)]
    [InlineData(-1000)]
    [InlineData(-1070)]
    public void GetAngle_ExtremeNormals(int k)
    {
        double c = System.Math.ScaleB(1.0, k);
        double ulp = System.Math.BitIncrement(System.Math.PI / 4.0) - System.Math.PI / 4.0;
        double angle = new Plane(new Vector3(c, 0.0, 0.0)).GetAngle(new Plane(new Vector3(c, c, 0.0)));
        Assert.True(System.Math.Abs(angle - System.Math.PI / 4.0) <= ulp, $"{angle:R}");
        double toVector = new Plane(new Vector3(c, 0.0, 0.0)).GetAngle(new Vector3(c, c, 0.0));
        Assert.True(System.Math.Abs(toVector - System.Math.PI / 4.0) <= ulp, $"{toVector:R}");
        Assert.Equal(System.Math.PI / 2.0, Plane.Z.GetAngle(new Vector3(0.0, 0.0, c)));
        Assert.Equal(System.Math.PI / 2.0, new Plane(new Vector3(0.0, 0.0, c)).GetAngle(Vector3.VectorZ));
    }
}
