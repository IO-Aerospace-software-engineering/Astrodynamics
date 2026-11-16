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

    // #680: a vector of magnitude 0 has no direction; Normalize returned the vector itself, silently. A vector whose
    // components are all tiny but not zero has a direction (third round of #668: it threw, its unscaled magnitude 0)
    // The worst | |u| - 1 |, 2.220e-16 (one ulp of 1), and |u x v| / |v|, 2.107e-16, the same on Linux x64, Windows x64 and
    // macOS arm64 (CI run 43595573884, 2026-11-14); the bounds are twice the worst, rounded up at the second significant digit
    private const double NormTolerance = 4.5e-16;

    private const double CrossTolerance = 4.3e-16;

    [Fact]
    public void Normalize_OfZero_Throws_AndOfANonZeroIsAParallelUnitVector()
    {
        Assert.Throws<InvalidOperationException>(() => Vector3.Zero.Normalize());
        Assert.Throws<InvalidOperationException>(() => new Vector3(-0.0, 0.0, -0.0).Normalize());

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
            // The former code, bit for bit: the vector over its unscaled magnitude
            double m = System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            Assert.Equal(new Vector3(v.X / m, v.Y / m, v.Z / m), u);
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
    [InlineData(-1e-170)]
    [InlineData(-4.9e-324)]
    [InlineData(-1e200)]
    [InlineData(-double.MaxValue)]
    public void SignedAngle_NonZeroPlane_IsTheAngleAboutItsDirection(double z)
    {
        Assert.Equal(-System.Math.PI / 2.0, Vector3.VectorX.Angle(Vector3.VectorY, new Vector3(0.0, 0.0, z)));
        Assert.Equal(System.Math.PI / 2.0, Vector3.VectorX.Angle(Vector3.VectorY, new Plane(new Vector3(0.0, 0.0, -z))));
        Assert.Equal(System.Math.PI, Vector3.VectorX.Angle(new Vector3(-1.0, 1e-300, 0.0), new Vector3(0.0, 0.0, -z)));
    }

    // The signed angle took atan2((a × b) · n̂, a · b) on the vectors unscaled: for huge vectors the products overflowed
    // (π/4 instead of atan 2 from (1e200, 0, 0) to (1e200, 2e200, 0), NaN for most pairs), for tiny ones they underflowed
    // (0). The closed form: from (1, 0, 0) 2^ka to (1, 2, 0) 2^kb the angle is atan 2 about +z, −atan 2 about −z, at
    // any scales; the products of the vectors scaled by powers of two are exact, so within one ulp of Math.Atan(2)
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 1000)]
    [InlineData(1022, 1021)]
    [InlineData(-1000, -1000)]
    [InlineData(-1070, -1070)]
    [InlineData(1000, -1000)]
    [InlineData(-1074, 1021)]
    public void SignedAngle_ExtremeVectors_IsTheAngleOfTheirDirections(int ka, int kb)
    {
        var a = new Vector3(System.Math.ScaleB(1.0, ka), 0.0, 0.0);
        var b = new Vector3(System.Math.ScaleB(1.0, kb), System.Math.ScaleB(2.0, kb), 0.0);
        double expected = System.Math.Atan(2.0);
        double ulp = System.Math.BitIncrement(expected) - expected;
        Assert.True(System.Math.Abs(a.Angle(b, Vector3.VectorZ) - expected) <= ulp, $"{a.Angle(b, Vector3.VectorZ):R}");
        Assert.True(System.Math.Abs(a.Angle(b, new Plane(new Vector3(0.0, 0.0, -1e-200))) + expected) <= ulp, $"{a.Angle(b, Plane.Z):R}");
        Assert.True(System.Math.Abs(b.Angle(a, new Vector3(0.0, 0.0, 1e200)) + expected) <= ulp, $"{b.Angle(a, Vector3.VectorZ):R}");
    }

    // Random directions in the plane z = 0, a = (cos α, sin α, 0) 2^ea and b = (cos β, sin β, 0) 2^eb, ea and eb over
    // 2^-960 to 2^1022 (every component a normal double), about n = ±z of random length: the closed form β − α (resp.
    // α − β) reduced to (−π, π]. The worst difference, MEASURE 8.882e-16 rad on each of Linux x64, Windows x64 and macOS
    // arm64 (CI run 43745883709, 2026-11-15; NaN before the fix), twice the worst, rounded up at the second significant
    // digit; the vectors with a NaN component give NaN and a zero vector 0, as the unsigned Angle does
    private const double SignedAngleSweepTolerance = 1.8e-15;

    [Fact]
    public void SignedAngle_LogUniformScales_IsTheDifferenceOfTheDirectionAngles()
    {
        var random = new Random(668);
        double worst = 0.0;
        for (int i = 0; i < 10000; i++)
        {
            double alpha = (random.NextDouble() * 2.0 - 1.0) * System.Math.PI;
            double beta = (random.NextDouble() * 2.0 - 1.0) * System.Math.PI;
            int ea = random.Next(-960, 1023);
            int eb = random.Next(-960, 1023);
            var a = new Vector3(System.Math.ScaleB(System.Math.Cos(alpha), ea), System.Math.ScaleB(System.Math.Sin(alpha), ea), 0.0);
            var b = new Vector3(System.Math.ScaleB(System.Math.Cos(beta), eb), System.Math.ScaleB(System.Math.Sin(beta), eb), 0.0);
            double normal = System.Math.Pow(10.0, random.NextDouble() * 600.0 - 300.0);
            double up = a.Angle(b, new Vector3(0.0, 0.0, normal));
            double down = a.Angle(b, new Plane(new Vector3(0.0, 0.0, -normal)));
            worst = System.Math.Max(worst, System.Math.Abs(System.Math.IEEERemainder(up - (beta - alpha), 2.0 * System.Math.PI)));
            worst = System.Math.Max(worst, System.Math.Abs(System.Math.IEEERemainder(down - (alpha - beta), 2.0 * System.Math.PI)));
        }

        _output.WriteLine($"MEASURE signed Angle over 2^-960 to 2^1022 against beta - alpha: {worst:E3}");
        Assert.True(worst <= SignedAngleSweepTolerance, $"{worst:E3}");

        Assert.True(double.IsNaN(new Vector3(double.NaN, 0.0, 0.0).Angle(Vector3.VectorY, Vector3.VectorZ)));
        Assert.True(double.IsNaN(Vector3.VectorX.Angle(new Vector3(0.0, double.PositiveInfinity, 0.0), Vector3.VectorZ)));
        Assert.Equal(0.0, Vector3.Zero.Angle(new Vector3(1e200, 1e200, 0.0), Vector3.VectorZ));
        Assert.Equal(0.0, new Vector3(1e-200, 1e-200, 0.0).Angle(Vector3.Zero, Vector3.VectorZ));
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
    // collinear opposite pairs below, the same on Linux x64, Windows x64 and macOS arm64 (CI run 43745883709, 2026-11-15),
    // twice the worst, rounded up at the second significant digit
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

    // Third round of #668: the magnitude was not scaled, so a component above about 1.34e154 squared to +Infinity and
    // the unit vector was zero, components below about 1.5e-162 squared to 0 and Normalize threw "magnitude of 0", and
    // between them the subnormal sum of squares lost digits (5.5e-6 off at 1e-160). The closed form: (2, 3, 6) has
    // magnitude 7, so (2, 3, 6) 2^k normalizes to (2/7, 3/7, 6/7), each correctly rounded, at any k (the scaling by a power
    // of two is exact)
    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    [InlineData(1000)]
    [InlineData(1021)]
    [InlineData(-600)]
    [InlineData(-1000)]
    [InlineData(-1070)]
    public void Normalize_ExtremeFiniteVectors_AreExactUnitVectors(int k)
    {
        var u = new Vector3(System.Math.ScaleB(2.0, k), System.Math.ScaleB(-3.0, k), System.Math.ScaleB(6.0, k)).Normalize();
        Assert.Equal(new Vector3(2.0 / 7.0, -3.0 / 7.0, 6.0 / 7.0), u);
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(-double.MaxValue)]
    [InlineData(1.4e154)]
    [InlineData(1e300)]
    [InlineData(1e-170)]
    [InlineData(4.9e-324)]
    [InlineData(-4.9e-324)]
    public void Normalize_OneNonZeroComponent_IsItsAxis(double x)
    {
        Assert.Equal(new Vector3(System.Math.Sign(x), 0.0, 0.0), new Vector3(x, 0.0, 0.0).Normalize());
        Assert.Equal(new Vector3(0.0, 0.0, System.Math.Sign(x)), new Vector3(0.0, 0.0, x).Normalize());
    }

    // (3, 4, 0) times a scale is (0.6, 0.8, 0) to the rounding of the scaled components (relative 1.1e-16 each); MEASURE
    // 1.110e-16 at 1e200, 0 at 1e-160 and 1e-200, the same on Linux x64, Windows x64 and macOS arm64 (CI run 43745883709,
    // 2026-11-15), twice the worst rounded up at the second significant digit
    private const double PythagoreanTolerance = 2.3e-16;

    [Theory]
    [InlineData(1e-200)]
    [InlineData(1e-160)]
    [InlineData(1e200)]
    public void Normalize_PythagoreanTriple_IsThreeFifthsFourFifths(double scale)
    {
        var u = new Vector3(3.0 * scale, 4.0 * scale, 0.0).Normalize();
        double worst = System.Math.Max(System.Math.Abs(u.X - 0.6), System.Math.Abs(u.Y - 0.8));
        _output.WriteLine($"MEASURE Normalize (3, 4, 0) x {scale:E1} against (0.6, 0.8, 0): {worst:E3}");
        Assert.True(worst <= PythagoreanTolerance, $"{worst:E3}");
        Assert.Equal(0.0, u.Z);
    }

    // A vector with a NaN or infinite component has no direction: Normalize returned NaN components silently
    [Theory]
    [InlineData(double.NaN, 0.0, 1.0)]
    [InlineData(double.PositiveInfinity, 0.0, 0.0)]
    [InlineData(0.0, double.NegativeInfinity, 0.0)]
    [InlineData(1.0, 1.0, double.NaN)]
    public void Normalize_NonFinite_Throws(double x, double y, double z)
    {
        Assert.Throws<InvalidOperationException>(() => new Vector3(x, y, z).Normalize());
    }

    // The unit vector over the whole double range, magnitudes 2^-1075 to 2^1023: the direction d of each draw is
    // scaled by 2^e (exact, but for the bits a subnormal result loses), and the exact direction of the vector is that
    // vector scaled back by 2^-e (exact: a multiplication of a finite double by a power of two that does not overflow).
    // The invariants of the closed form: | |u| - 1 | and |u x d'| / |d'|; the magnitude, relative, where it is a normal
    // double, against |v| = 2^e |d'| with |d'| computed in double-double (products split by FusedMultiplyAdd, sums by
    // TwoSum, the square root corrected by one Newton step: relative error about 1e-32), independent of the code under
    // test. The worst, MEASURE | |u| - 1 | 2.220e-16, |u x d'| / |d'| 1.221e-16, magnitude 1.940e-16, the same on Linux
    // x64, Windows x64 and macOS arm64 (CI run 43745883709, 2026-11-15; 2.125e-16 reported against an 80-digit decimal
    // computation during the triage); twice the worst rounded up at the second significant digit
    private const double SweepNormTolerance = 4.5e-16;

    private const double SweepCrossTolerance = 2.5e-16;

    private const double SweepMagnitudeTolerance = 3.9e-16;

    [Fact]
    public void Normalize_LogUniformOverTheWholeDoubleRange()
    {
        var random = new Random(668);
        double worstNorm = 0.0;
        double worstCross = 0.0;
        double worstMagnitude = 0.0;
        int drawn = 0;
        for (int i = 0; i < 10000; i++)
        {
            var direction = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0);
            if (direction.Magnitude() < 0.1) continue;
            int e = random.Next(-1075, 1024);
            var v = new Vector3(System.Math.ScaleB(direction.X, e), System.Math.ScaleB(direction.Y, e), System.Math.ScaleB(direction.Z, e));
            if (v == Vector3.Zero) continue;
            var exact = new Vector3(System.Math.ScaleB(v.X, -e), System.Math.ScaleB(v.Y, -e), System.Math.ScaleB(v.Z, -e));
            drawn++;

            var u = v.Normalize();

            worstNorm = System.Math.Max(worstNorm, System.Math.Abs(u.Magnitude() - 1.0));
            worstCross = System.Math.Max(worstCross, u.Cross(exact).Magnitude() / exact.Magnitude());
            double magnitude = v.Magnitude();
            if (magnitude >= 2.2250738585072014e-308 && double.IsFinite(magnitude))
            {
                var (hi, lo) = DoubleDoubleMagnitude(exact);
                // ScaleB(magnitude, -e) is exact for a normal magnitude; the error is taken against hi + lo
                worstMagnitude = System.Math.Max(worstMagnitude, System.Math.Abs((System.Math.ScaleB(magnitude, -e) - hi - lo) / hi));
            }
        }

        _output.WriteLine($"MEASURE Normalize over 2^-1075 to 2^1023 ({drawn} vectors) | |u| - 1 |: {worstNorm:E3}");
        _output.WriteLine($"MEASURE Normalize over 2^-1075 to 2^1023 |u x d'| / |d'|: {worstCross:E3}");
        _output.WriteLine($"MEASURE Magnitude over 2^-1075 to 2^1023, relative: {worstMagnitude:E3}");
        Assert.True(worstNorm <= SweepNormTolerance, $"{worstNorm:E3}");
        Assert.True(worstCross <= SweepCrossTolerance, $"{worstCross:E3}");
        Assert.True(worstMagnitude <= SweepMagnitudeTolerance, $"{worstMagnitude:E3}");
    }

    // √(x² + y² + z²) in double-double: each square exact as hi + lo (FusedMultiplyAdd), the sums by TwoSum, then
    // r = √s_hi corrected by one Newton step on the remainder s − r² (exact through FusedMultiplyAdd); relative error
    // about 1e-32, far below the 1.1e-16 it measures
    private static (double hi, double lo) DoubleDoubleMagnitude(in Vector3 v)
    {
        static (double s, double e) TwoSum(double a, double b)
        {
            double s = a + b;
            double bb = s - a;
            return (s, (a - (s - bb)) + (b - bb));
        }

        static (double hi, double lo) Add(double ahi, double alo, double bhi, double blo)
        {
            var (s, e) = TwoSum(ahi, bhi);
            e += alo + blo;
            return TwoSum(s, e);
        }

        double x2 = v.X * v.X, y2 = v.Y * v.Y, z2 = v.Z * v.Z;
        var (shi, slo) = Add(x2, System.Math.FusedMultiplyAdd(v.X, v.X, -x2), y2, System.Math.FusedMultiplyAdd(v.Y, v.Y, -y2));
        (shi, slo) = Add(shi, slo, z2, System.Math.FusedMultiplyAdd(v.Z, v.Z, -z2));
        double r = System.Math.Sqrt(shi);
        double remainder = System.Math.FusedMultiplyAdd(-r, r, shi) + slo;
        return TwoSum(r, remainder / (2.0 * r));
    }

    // The magnitude was not scaled: +Infinity for a component above about 1.34e154, 0 below about 1.5e-162. Closed forms:
    // |(3, 4, 0)| = 5 times any power of two, exactly; |(x, 0, 0)| = |x| exactly
    [Fact]
    public void Magnitude_ExtremeFiniteVectors()
    {
        Assert.Equal(System.Math.ScaleB(5.0, 1000), new Vector3(System.Math.ScaleB(3.0, 1000), System.Math.ScaleB(4.0, 1000), 0.0).Magnitude());
        Assert.Equal(5.0 * double.Epsilon, new Vector3(3.0 * double.Epsilon, 4.0 * double.Epsilon, 0.0).Magnitude());
        Assert.Equal(System.Math.ScaleB(5.0, -1000), new Vector3(0.0, System.Math.ScaleB(-3.0, -1000), System.Math.ScaleB(4.0, -1000)).Magnitude());
        Assert.Equal(1e-170, new Vector3(1e-170, 0.0, 0.0).Magnitude());
        Assert.Equal(1e200, new Vector3(0.0, -1e200, 0.0).Magnitude());
        Assert.Equal(double.MaxValue, new Vector3(0.0, 0.0, double.MaxValue).Magnitude());
        // The true magnitude, 2.54e308, is above double.MaxValue
        Assert.Equal(double.PositiveInfinity, new Vector3(double.MaxValue, double.MaxValue, 0.0).Magnitude());
        Assert.Equal(0.0, Vector3.Zero.Magnitude());
        Assert.True(double.IsNaN(new Vector3(double.NaN, 0.0, 0.0).Magnitude()));
        Assert.Equal(double.PositiveInfinity, new Vector3(double.NegativeInfinity, 0.0, 0.0).Magnitude());

        // Ordinary vectors, bit for bit the unscaled magnitude
        var random = new Random(668);
        for (int i = 0; i < 10000; i++)
        {
            double magnitude = System.Math.Pow(10.0, random.NextDouble() * 300.0 - 150.0);
            var v = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0) * magnitude;
            Assert.Equal(System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z), v.Magnitude());
        }
    }

    // The angle of two vectors divided their dot product by the product of their unscaled magnitudes: 0 for tiny vectors
    // (both magnitudes 0), NaN for huge ones (Infinity over Infinity). The closed form: π/4 between (1, 0, 0) and
    // (1, 1, 0) at any scale, within one ulp of π/4 (the rounding of 1/√2 and of acos)
    [Theory]
    [InlineData(-1000)]
    [InlineData(-1070)]
    [InlineData(1000)]
    [InlineData(1022)]
    [InlineData(0)]
    public void Angle_ExtremeVectors(int k)
    {
        double s = System.Math.ScaleB(1.0, k);
        double angle = new Vector3(s, 0.0, 0.0).Angle(new Vector3(s, s, 0.0));
        double ulp = System.Math.BitIncrement(System.Math.PI / 4.0) - System.Math.PI / 4.0;
        Assert.True(System.Math.Abs(angle - System.Math.PI / 4.0) <= ulp, $"{angle:R}");
        // Mixed scales: a huge and a tiny vector
        double mixed = new Vector3(System.Math.ScaleB(1.0, 1000), 0.0, 0.0).Angle(new Vector3(0.0, System.Math.ScaleB(1.0, -1000), 0.0));
        Assert.Equal(System.Math.PI / 2.0, mixed);
        // The zero vector keeps its convention, 0
        Assert.Equal(0.0, Vector3.Zero.Angle(new Vector3(s, s, 0.0)));
        Assert.Equal(0.0, new Vector3(s, s, 0.0).Angle(Vector3.Zero));
    }
}
