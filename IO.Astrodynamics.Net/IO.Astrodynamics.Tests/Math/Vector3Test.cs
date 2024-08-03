using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class VectorTests
{
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
}