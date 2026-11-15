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

    // The known limitation of #680: the signed angle normalised its plane normal leniently, so a zero normal gave the
    // unsigned 0 or π, its sign lost silently; a zero or non-finite normal defines no orientation
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(double.NaN, 0.0, 1.0)]
    [InlineData(0.0, double.PositiveInfinity, 0.0)]
    [InlineData(0.0, 0.0, double.NegativeInfinity)]
    public void SignedAngle_ZeroOrNonFinitePlane_Throws(double x, double y, double z)
    {
        var normal = new Vector3(x, y, z);
        Assert.Equal("plane", Assert.Throws<ArgumentException>(() => Vector3.VectorX.Angle(new Vector3(-1.0, 1e-300, 0.0), normal)).ParamName);
        Assert.Equal("plane", Assert.Throws<ArgumentException>(() => Vector3.VectorX.Angle(Vector3.VectorY, normal)).ParamName);
        Assert.Equal("plane", Assert.Throws<ArgumentException>(() => Vector3.VectorX.Angle(Vector3.VectorY, new Plane(normal))).ParamName);
    }

    // The signed angle about a non-zero normal of any length: atan2((a × b) · n̂, a · b), -π/2 from X to Y about -z,
    // +π/2 about +z (the closed form, exact in doubles)
    [Theory]
    [InlineData(-1.0)]
    [InlineData(-1e-100)]
    [InlineData(-1e100)]
    public void SignedAngle_NonZeroPlane_IsTheAngleAboutItsDirection(double z)
    {
        Assert.Equal(-System.Math.PI / 2.0, Vector3.VectorX.Angle(Vector3.VectorY, new Vector3(0.0, 0.0, z)));
        Assert.Equal(System.Math.PI / 2.0, Vector3.VectorX.Angle(Vector3.VectorY, new Plane(new Vector3(0.0, 0.0, -z))));
        Assert.Equal(System.Math.PI, Vector3.VectorX.Angle(new Vector3(-1.0, 1e-300, 0.0), new Vector3(0.0, 0.0, -z)));
    }

    // Review of #680's known limitation: To normalized its result leniently. A zero or non-finite vector gave the identity
    // (or NaN) silently; a zero or non-finite vector has no direction to rotate from or to
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(double.NaN, 0.0, 1.0)]
    [InlineData(0.0, double.PositiveInfinity, 0.0)]
    [InlineData(0.0, 0.0, double.NegativeInfinity)]
    public void To_ZeroOrNonFiniteVector_Throws(double x, double y, double z)
    {
        var vector = new Vector3(x, y, z);
        Assert.Equal("vector", Assert.Throws<ArgumentException>(() => Vector3.VectorX.To(vector)).ParamName);
        Assert.Throws<InvalidOperationException>(() => vector.To(Vector3.VectorY));
    }

    // The angle between two vectors, atan2(|a × b|, a · b), accurate near 0 and π where acos is not
    private static double AngleBetween(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    // The angle of the rotation from q to the closed form expected (sign free): 2 atan2(|q* p|_v, |(q* p)_w|)
    private static double RotationAngle(in Quaternion q, in Quaternion expected)
    {
        var d = q.Conjugate() * expected;
        return 2.0 * System.Math.Atan2(d.VectorPart.Magnitude(), System.Math.Abs(d.W));
    }

    // The rotation of To depends on the directions only. Closed forms: from x̂ to ŷ, Rz(90°) = (cos 45°, 0, 0, sin 45°); from
    // x̂ to −x̂, a half turn (w = 0) about an axis perpendicular to x̂. Lengths whose squares or products overflow or lose
    // their digits (1e±100 to 1e±200, a subnormal) are scaled first: before, 1e200 gave NaN and 1e-200 the zero quaternion
    // or a non-unit one. The worst angle, MEASURE 4.041e-16 rad, and | |q| − 1 |, 2.220e-16, over these lengths and the
    // collinear opposite pairs below (Linux x64, 2026-11-15), twice the worst, rounded up at the second significant digit,
    // provisional until the three platforms are read from the CI logs
    private const double ToAngleTolerance = 8.1e-16;

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(2.0, 1e-3)]
    [InlineData(1e10, 1.0)]
    [InlineData(1e100, 1e100)]
    [InlineData(1e200, 1e200)]
    [InlineData(1e-100, 1e-100)]
    [InlineData(1e-200, 1e-200)]
    [InlineData(1e200, 1e-200)]
    [InlineData(4.9e-324, 1.0)]
    public void To_VectorsOfAnyLength_IsTheRotationOfTheirDirections(double lengthA, double lengthB)
    {
        var quarter = new Vector3(lengthA, 0.0, 0.0).To(new Vector3(0.0, lengthB, 0.0));
        var expected = new Quaternion(System.Math.Sqrt(0.5), 0.0, 0.0, System.Math.Sqrt(0.5));
        double quarterAngle = RotationAngle(quarter, expected);

        var half = new Vector3(lengthA, 0.0, 0.0).To(new Vector3(-lengthB, 0.0, 0.0));
        double halfAngle = AngleBetween(Vector3.VectorX.Rotate(half), Vector3.VectorX.Inverse());

        double norm = System.Math.Max(System.Math.Abs(quarter.Magnitude() - 1.0), System.Math.Abs(half.Magnitude() - 1.0));
        _output.WriteLine($"MEASURE To lengths {lengthA:E1}, {lengthB:E1}: quarter turn {quarterAngle:E3} rad, half turn {halfAngle:E3} rad, | |q| - 1 | {norm:E3}");
        Assert.True(quarterAngle <= ToAngleTolerance, $"{quarterAngle:E3}");
        Assert.True(halfAngle <= ToAngleTolerance, $"{halfAngle:E3}");
        Assert.Equal(0.0, half.W);
        Assert.True(norm <= ToAngleTolerance, $"{norm:E3}");
    }

    // Review of #680's known limitation: two vectors collinear and opposite in doubles (a × b = 0 exactly, a · b < 0) whose
    // Angle is not exactly π missed the half-turn branch: v = a × b = 0 and w = a · b + |a| |b| was 0, giving the zero
    // quaternion (a vector rotated by it is zero), or a rounding residue, giving the identity. The closed form: a half
    // turn (w = 0) about an axis perpendicular to a, which sends a onto −a. b = −k a, k in [0.1, 10.1), seed 1 (this draw:
    // 29 857 pairs collinear in doubles, of which 7309 more than 1e-6 rad off and 1102 zero quaternions before the fix,
    // MEASURE on a616682, Linux x64, 2026-11-15); only the pairs collinear in doubles are asserted: a pair whose a × b is a
    // rounding residue is a separate defect, measured and not asserted here (16 805 of 70 143 more than 1e-6 rad off,
    // before and after the fix)
    [Fact]
    public void To_OppositeVectorsCollinearInDoubles_IsAHalfTurn()
    {
        var reviewed = new Vector3(0.11847130284573482, 0.3365959929472748, 0.3564648133965511)
            .To(new Vector3(-0.9284643356028833, -2.6379162501937112, -2.7936289901951556));
        Assert.Equal(0.0, reviewed.W);
        Assert.True(AngleBetween(new Vector3(0.11847130284573482, 0.3365959929472748, 0.3564648133965511).Rotate(reviewed),
            new Vector3(-0.9284643356028833, -2.6379162501937112, -2.7936289901951556)) <= ToAngleTolerance);

        var random = new Random(1);
        int collinear = 0;
        int residues = 0;
        int residuesOff = 0;
        double worstAngle = 0.0;
        double worstNorm = 0.0;
        for (int n = 0; n < 100000; n++)
        {
            var a = new Vector3(random.NextDouble(), random.NextDouble(), random.NextDouble());
            double k = 0.1 + 10.0 * random.NextDouble();
            var b = a * -k;
            var q = a.To(b);
            double angle = AngleBetween(a.Rotate(q), b);
            if (a.Cross(b) != Vector3.Zero)
            {
                residues++;
                if (angle > 1e-6) residuesOff++;
                continue;
            }

            collinear++;
            Assert.Equal(0.0, q.W);
            worstAngle = System.Math.Max(worstAngle, angle);
            worstNorm = System.Math.Max(worstNorm, System.Math.Abs(q.Magnitude() - 1.0));
        }

        _output.WriteLine($"MEASURE To opposite collinear pairs: {collinear}, worst angle {worstAngle:E3} rad, worst | |q| - 1 | {worstNorm:E3}");
        _output.WriteLine($"MEASURE To opposite pairs with a cross-product residue (separate defect): {residues}, of which {residuesOff} more than 1e-6 rad off");
        Assert.True(collinear > 0);
        Assert.True(worstAngle <= ToAngleTolerance, $"{worstAngle:E3}");
        Assert.True(worstNorm <= ToAngleTolerance, $"{worstNorm:E3}");
    }
}
