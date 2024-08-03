using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class MatirxTests
{
    [Fact]
    public void CreateMatrix()
    {
        Matrix m = new Matrix(2, 3);
        Assert.Equal(2, m.Rows);
        Assert.Equal(3, m.Columns);
    }

    [Fact]
    public void CreateMatrixFromData()
    {
        double[,] data = new double[2, 3];
        Matrix m = new Matrix(data);
        Assert.Equal(2, m.Rows);
        Assert.Equal(3, m.Columns);
    }

    [Fact]
    public void CreateMatrixFromArray()
    {
        double[] data = new double[6] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m = new Matrix(2, 3, data);
        Assert.Equal(1, m.Get(0, 0));
        Assert.Equal(2, m.Get(0, 1));
        Assert.Equal(3, m.Get(0, 2));
        Assert.Equal(4, m.Get(1, 0));
        Assert.Equal(5, m.Get(1, 1));
        Assert.Equal(6, m.Get(1, 2));
    }

    [Fact]
    public void Equality()
    {
        double[] data = new double[6] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m = new Matrix(2, 3, data);

        Matrix m2 = new Matrix(2, 3, data);

        double[] data3 = new double[6] { 2.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m3 = new Matrix(2, 3, data3);
        
        double[] data4 = new double[6] { 2.0, 2.0, 3.0, 4.0, 5.0, 6.0 };
        Matrix m4 = new Matrix(3, 2, data3);
        Assert.Equal(m, m2);
        Assert.NotEqual(m2, m3);
        Assert.True(m==m2);
        Assert.True(m!=m3);
        Assert.True(m.Equals(m2));
        Assert.True(m.Equals((object)m2));
        Assert.False(m.Equals(null));
        Assert.False(m.Equals((object)null));
        Assert.NotEqual(m3, m4);
    }


    [Fact]
    public void MultiplyMatrix()
    {
        Matrix m1 = new Matrix(2, 3, new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 });
        Matrix m2 = new Matrix(3, 2, new[] { 7.0, 8.0, 9.0, 10.0, 11.0, 12.0 });
        Matrix res = m1.Multiply(m2);
        Assert.Equal(2, res.Rows);
        Assert.Equal(2, res.Columns);
        Assert.Equal(58, res.Get(0, 0));
        Assert.Equal(64, res.Get(0, 1));
        Assert.Equal(139, res.Get(1, 0));
        Assert.Equal(154, res.Get(1, 1));

        Assert.Equal(res, m1 * m2);
    }

    [Fact]
    public void MultiplyVector()
    {
        //J2000->Ecliptic
        double[] v = new double[36];
        v[0] = 1.0;
        v[7] = 0.9174820620691818;
        v[8] = 0.3977771559319137;
        v[13] = -0.3977771559319137;
        v[14] = 0.9174820620691818;
        v[21] = 1.0;
        v[28] = 0.9174820620691818;
        v[29] = 0.3977771559319137;
        v[34] = -0.3977771559319137;
        v[35] = 0.9174820620691818;
        Matrix m = new Matrix(6, 6, v);

        //Earth from sun at 0 TDB
        double[] earthSv = { -26499033.67742509, 132757417.33833946, 57556718.47053819, -29.79426007, -5.01805231, -2.17539380 };
        double[] res = m.Multiply(earthSv);
        Assert.Equal(-26499033.67742509, res[0]);
        Assert.Equal(144697296.79254317, res[1]);
        Assert.Equal(-611.1494260579348, res[2]);
        Assert.Equal(-29.79426007, res[3]);
        Assert.Equal(-5.469294939745739, res[4]);
        Assert.Equal(0.0001817867528561834, res[5]);
        Assert.Equal(res, m * earthSv);
        Assert.Equal(res, earthSv * m);
    }

    [Fact]
    public void Inverse()
    {
        //J2000->Ecliptic
        double[] v = new double[36];
        v[0] = 1.0;
        v[7] = 0.9174820620691818;
        v[8] = 0.3977771559319137;
        v[13] = -0.3977771559319137;
        v[14] = 0.9174820620691818;
        v[21] = 1.0;
        v[28] = 0.9174820620691818;
        v[29] = 0.3977771559319137;
        v[34] = -0.3977771559319137;
        v[35] = 0.9174820620691818;
        Matrix m = new Matrix(6, 6, v);

        var res = m.Inverse();
        //Eclip=>J2000
        Assert.Equal(1.0, res.Get(0, 0));
        Assert.Equal(0.0, res.Get(0, 1));
        Assert.Equal(0.9174820620691818, res.Get(1, 1));
        Assert.Equal(-0.3977771559319137, res.Get(1, 2));
        Assert.Equal(0.3977771559319137, res.Get(2, 1));
        Assert.Equal(0.9174820620691818, res.Get(2, 2));
        Assert.Equal(1.0, res.Get(3, 3));
        Assert.Equal(0.9174820620691818, res.Get(4, 4));
        Assert.Equal(-0.3977771559319137, res.Get(4, 5));
        Assert.Equal(0.3977771559319137, res.Get(5, 4));
        Assert.Equal(0.9174820620691818, res.Get(5, 5));
    }

    [Fact]
    public void CreateRotationMatrixX()
    {
        var res = Matrix.CreateRotationMatrixX(Astrodynamics.Constants.PI2);
        Assert.Equal(1.0, res.Get(0, 0));
        Assert.Equal(-1.0, res.Get(1, 2));
        Assert.Equal(1.0, res.Get(2, 1));
    }

    [Fact]
    public void CreateRotationMatrixY()
    {
        var res = Matrix.CreateRotationMatrixY(Astrodynamics.Constants.PI2);
        Assert.Equal(1.0, res.Get(0, 2));
        Assert.Equal(1.0, res.Get(1, 1));
        Assert.Equal(-1.0, res.Get(2, 0));

        var rot = res.Multiply(new double[] { 1.0, 0.0, 0.0 });
    }

    [Fact]
    public void CreateRotationMatrixZ()
    {
        var res = Matrix.CreateRotationMatrixZ(Astrodynamics.Constants.PI2);
        Assert.Equal(-1.0, res.Get(0, 1));
        Assert.Equal(1.0, res.Get(1, 0));
        Assert.Equal(1.0, res.Get(2, 2));
    }
}