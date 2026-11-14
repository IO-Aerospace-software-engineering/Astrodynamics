using System;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Math;

public class VectorTests
{
    private readonly ITestOutputHelper _output;

    public VectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Createvector()
    {
        Vector3 m = new Vector3(1, 2, 3);
        Assert.Equal(1, m.X);
        Assert.Equal(2, m.Y);
        Assert.Equal(3, m.Z);
    }

    [Fact]
    public void Magnitude()
    {
        Vector3 m = new Vector3(2, 3, 4);
        Assert.Equal(5.385164807134504, m.Magnitude());
    }

    [Fact]
    public void Multiply()
    {
        Vector3 m = new Vector3(2, 3, 4);
        m *= 2.0;
        Assert.Equal(new Vector3(4, 6, 8), m);
    }

    [Fact]
    public void Add()
    {
        Vector3 m1 = new Vector3(4, 6, 8);
        Vector3 m2 = new Vector3(1, 2, 3);
        m1 += m2;
        Assert.Equal(new Vector3(5, 8, 11), m1);
    }

    [Fact]
    public void Subtract()
    {
        Vector3 m1 = new Vector3(4, 6, 8);
        Vector3 m2 = new Vector3(1, 2, 3);
        m1 -= m2;
        Assert.Equal(new Vector3(3, 4, 5), m1);
    }

    [Fact]
    public void Dot()
    {
        Vector3 m1 = new Vector3(2, 3, 4);
        Vector3 m2 = new Vector3(5, 6, 7);
        double res = m1 * m2;
        Assert.Equal(56, res);
    }

    [Fact]
    public void Cross()
    {
        Vector3 m1 = new Vector3(1, 0, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        Vector3 v = m1.Cross(m2);
        Assert.Equal(new Vector3(0, 0, 1), v);
    }

    [Fact]
    public void Normalize()
    {
        Vector3 m2 = new Vector3(3, 4, 5);
        Vector3 v = m2.Normalize();
        Assert.Equal(1.0, v.Magnitude(), 15);
    }

    [Fact]
    public void Angle()
    {
        Vector3 m1 = new Vector3(1, 0, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m1.Angle(m2);
        Assert.Equal(System.Math.PI / 2.0, angle);
    }

    [Fact]
    public void Angle2()
    {
        Vector3 m1 = new Vector3(1, 0, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m2.Angle(m1, Plane.Z);
        Assert.Equal(-System.Math.PI / 2.0, angle);
    }

    [Fact]
    public void Angle3()
    {
        Vector3 m1 = new Vector3(1, -1, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m2.Angle(m1, new Plane(new Vector3(0.0, 0.0, 10.0), 0.0));
        Assert.Equal(-2.3561944901923448, angle, 6);
    }

    [Fact]
    public void Angle4()
    {
        Vector3 m1 = new Vector3(0, -1, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m2.Angle(m1, Plane.Z);
        Assert.Equal(3.1415929999999999, angle, 6);
    }

    [Fact]
    public void Angle5()
    {
        Vector3 m1 = new Vector3(-1, -1, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m2.Angle(m1, Plane.Z);
        Assert.Equal(2.3561939999999999, angle, 6);
    }

    [Fact]
    public void Angle6()
    {
        Vector3 m1 = new Vector3(-1, 0, 0);
        Vector3 m2 = new Vector3(0, 1, 0);
        double angle = m2.Angle(m1);
        Assert.Equal(1.5707960000000001, angle, 6);
    }

    [Fact]
    public void To()
    {
        Vector3 m1 = new Vector3(10, 0, 0);
        Vector3 m2 = new Vector3(0, 10, 0);
        var q = m1.To(m2);
        Assert.Equal(0.7071067811865475, q.W);
        Assert.Equal(0.0, q.VectorPart.X);
        Assert.Equal(0.0, q.VectorPart.Y);
        Assert.Equal(0.7071067811865475, q.VectorPart.Z);
    }

    [Fact]
    public void To2()
    {
        Vector3 m1 = new Vector3(10, 0, 0);
        Vector3 m2 = new Vector3(-10, 0, 0);
        var q = m1.To(m2);
        Assert.Equal(0.0, q.W);
        Assert.Equal(0.0, q.VectorPart.X);
        Assert.Equal(1.0, q.VectorPart.Y);
        Assert.Equal(0.0, q.VectorPart.Z);
    }

    [Fact]
    public void To3()
    {
        Vector3 m1 = new Vector3(0, 1, 0);
        Vector3 m2 = new Vector3(0, -0.2, 0.7);
        var q = m1.To(m2);
        Assert.Equal(0.60219551314445285, q.W);
        Assert.Equal(0.79834864811602768, q.VectorPart.X);
        Assert.Equal(0.0, q.VectorPart.Y);
        Assert.Equal(0.0, q.VectorPart.Z);
    }
    
    [Fact]
    public void To4()
    {
        Vector3 m1 = new Vector3(-10, 0, 0);
        Vector3 m2 = new Vector3(10, 0, 0);
        var q = m1.To(m2);
        Assert.Equal(0.0, q.W);
        Assert.Equal(0.0, q.VectorPart.X);
        Assert.Equal(-1.0, q.VectorPart.Y);
        Assert.Equal(0.0, q.VectorPart.Z);
    }

    [Fact]
    public void Rotate()
    {
        Vector3 m1 = new Vector3(10, 0, 0);
        Quaternion q = new Quaternion(0.7071067811865475, 0.0, 0.0, 0.7071067811865475);
        var m2 = m1.Rotate(q);
        Assert.Equal(0.0, m2.X);
        Assert.Equal(10.0, m2.Y, 12);
        Assert.Equal(0.0, m2.Z);
    }

    [Fact]
    public void VectorToString()
    {
        Vector3 m1 = new Vector3(10, 20, 30);
        Assert.Equal("X : 10 Y : 20 Z: 30", m1.ToString());
    }

    [Fact]
    public void LinearInterpolation()
    {
        Vector3 m1 = new Vector3(10, 20, 30);
        Vector3 m2 = new Vector3(20, 40, 60);
        var m3 = m1.LinearInterpolation(m2, 0.5);
        Assert.Equal(15.0, m3.X);
        Assert.Equal(30.0, m3.Y);
        Assert.Equal(45.0, m3.Z);
    }
    
    [Fact]
    public void LinearInterpolation2()
    {
        Vector3 m1 = new Vector3(10, 20, 30);
        Vector3 m2 = new Vector3(20, 40, 60);
        var m3 = m1.LinearInterpolation(m2, 0.75);
        Assert.Equal(17.5, m3.X);
        Assert.Equal(35.0, m3.Y);
        Assert.Equal(52.5, m3.Z);
    }

    // #680: a vector of magnitude 0 has no direction. The magnitude is not scaled, so a vector whose squared components all
    // underflow (below about 1.5e-162) has magnitude 0 too; Normalize returned the vector itself, silently
    // The worst | |u| - 1 |, 2.220e-16 (one ulp of 1), and |u x v| / |v|, 2.107e-16, the same on Linux x64, Windows x64 and
    // macOS arm64 (CI run 43595573884, 2026-11-14); the bounds are twice the worst, rounded up at the second significant digit
    private const double NormTolerance = 4.5e-16;

    private const double CrossTolerance = 4.3e-16;

    [Fact]
    public void Normalize_OfZero_Throws_AndOfANonZeroIsAParallelUnitVector()
    {
        Assert.Throws<InvalidOperationException>(() => Vector3.Zero.Normalize());
        Assert.Throws<InvalidOperationException>(() => new Vector3(1e-170, 0.0, 0.0).Normalize());
        Assert.Throws<InvalidOperationException>(() => new Vector3(-0.0, 0.0, -1e-163).Normalize());

        // The closed form: the unit vector of v has norm 1 and is parallel to v, checked through its norm and the cross
        // product, not through the quotient the code computes; magnitudes from 1e-150 to 1e150, log-uniform
        var random = new Random(680);
        double worstNorm = 0.0;
        double worstCross = 0.0;
        for (int i = 0; i < 10000; i++)
        {
            double magnitude = System.Math.Pow(10.0, random.NextDouble() * 300.0 - 150.0);
            var direction = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0);
            if (direction.Magnitude() < 0.1) continue;
            var v = direction * magnitude;

            var u = v.Normalize();

            worstNorm = System.Math.Max(worstNorm, System.Math.Abs(u.Magnitude() - 1.0));
            // The direction the vector was scaled from, parallel to it within the rounding of the scaling
            worstCross = System.Math.Max(worstCross, u.Cross(direction).Magnitude() / direction.Magnitude());
            // The former code, bit for bit: the vector over its magnitude
            Assert.Equal(new Vector3(v.X / v.Magnitude(), v.Y / v.Magnitude(), v.Z / v.Magnitude()), u);
        }

        _output.WriteLine($"MEASURE #680 Normalize | |u| - 1 |: {worstNorm:E3}");
        _output.WriteLine($"MEASURE #680 Normalize |u x v| / |v|: {worstCross:E3}");
        Assert.True(worstNorm <= NormTolerance, $"{worstNorm:E3}");
        Assert.True(worstCross <= CrossTolerance, $"{worstCross:E3}");
    }
}
