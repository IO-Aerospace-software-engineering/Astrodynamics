using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class PlaneTests
{
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
}