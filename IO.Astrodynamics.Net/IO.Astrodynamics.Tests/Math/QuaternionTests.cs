using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Math
{
    public class QuaternionTests
    {
        private readonly ITestOutputHelper _output;

        public QuaternionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CreateByAxisAngle()
        {
            Quaternion q = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.HalfPi);
            Assert.Equal(0.7071067811865476, q.W);
            Assert.Equal(0.0, q.VectorPart.X);
            Assert.Equal(0.0, q.VectorPart.Y);
            Assert.Equal(0.7071067811865475, q.VectorPart.Z, 12);
        }

        [Fact]
        public void ToEulerAngle()
        {
            Quaternion q = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.HalfPi);
            var res = q.ToEuler();
            Assert.Equal(0.0, res.X);
            Assert.Equal(0.0, res.Y);
            Assert.Equal(Astrodynamics.Constants.HalfPi, res.Z, 12);
        }

        [Fact]
        public void Create()
        {
            Quaternion q = new Quaternion(1.0, 2.0, 3.0, 4.0);
            Assert.Equal(1.0, q.W);
            Assert.Equal(2.0, q.VectorPart.X);
            Assert.Equal(3.0, q.VectorPart.Y);
            Assert.Equal(4.0, q.VectorPart.Z);
        }

        [Fact]
        public void Create2()
        {
            Quaternion q = new Quaternion();
            Assert.Equal(1.0, q.W);
            Assert.Equal(0.0, q.VectorPart.X);
            Assert.Equal(0.0, q.VectorPart.Y);
            Assert.Equal(0.0, q.VectorPart.Z);
        }

        [Fact]
        public void Create3()
        {
            Quaternion q = default(Quaternion);
            Assert.Equal(0.0, q.W);
            Assert.Equal(0.0, q.VectorPart.X);
            Assert.Equal(0.0, q.VectorPart.Y);
            Assert.Equal(0.0, q.VectorPart.Z);
        }

        [Fact]
        public void CreateByScalarVector()
        {
            Quaternion q = new Quaternion(1.0, new Vector3(2.0, 3.0, 4.0));
            Assert.Equal(1.0, q.W);
            Assert.Equal(2.0, q.VectorPart.X);
            Assert.Equal(3.0, q.VectorPart.Y);
            Assert.Equal(4.0, q.VectorPart.Z);
        }

        [Fact]
        public void Multiply()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 40.0 * IO.Astrodynamics.Constants.Deg2Rad);
            Quaternion q2 = new Quaternion(Vector3.VectorY, 40.0 * IO.Astrodynamics.Constants.Deg2Rad);
            Quaternion q3 = q1 * q2;
            Assert.Equal(0.8830222215594891, q3.W);
            Assert.Equal(0.3213938048432697, q3.VectorPart.X);
            Assert.Equal(0.3213938048432697, q3.VectorPart.Y);
            Assert.Equal(0.11697777844051097, q3.VectorPart.Z);
        }

        [Fact]
        public void Multiply2()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX.Inverse(), 40.0 * IO.Astrodynamics.Constants.Deg2Rad);
            Quaternion q2 = new Quaternion(Vector3.VectorY, 40.0 * IO.Astrodynamics.Constants.Deg2Rad);
            Quaternion q3 = q1 * q2;
            Assert.Equal(0.8830222215594891, q3.W);
            Assert.Equal(-0.3213938048432697, q3.VectorPart.X);
            Assert.Equal(0.3213938048432697, q3.VectorPart.Y);
            Assert.Equal(-0.11697777844051097, q3.VectorPart.Z);
        }

        [Fact]
        public void Multiply3()
        {
            Vector3 instrumentVector = Spacecraft.Front;

            Quaternion spacecraftOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.HalfPi);
            Quaternion instrumentOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.HalfPi);

            Quaternion globalOrientation = spacecraftOrientation * instrumentOrientation;

            Vector3 instrumentDirectionRelativeToWorld = instrumentVector.Rotate(globalOrientation);

            Assert.Equal(0.0, instrumentDirectionRelativeToWorld.X, 12);
            Assert.Equal(Vector3.VectorY.Inverse().Y, instrumentDirectionRelativeToWorld.Y, 12);
            Assert.Equal(0.0, instrumentDirectionRelativeToWorld.Z, 12);
        }

        [Fact]
        public void Multiply4()
        {
            Vector3 instrumentVector = Spacecraft.Front;

            Quaternion spacecraftOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.HalfPi);
            Quaternion instrumentOrientation = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.HalfPi * 0.5);

            Quaternion globalOrientation = spacecraftOrientation * instrumentOrientation;

            Vector3 instrumentDirectionRelativeToWorld = instrumentVector.Rotate(globalOrientation);

            Assert.Equal(-0.70710678118654757, instrumentDirectionRelativeToWorld.X, 12);
            Assert.Equal(0.0, instrumentDirectionRelativeToWorld.Y, 12);
            Assert.Equal(0.70710678118654757, instrumentDirectionRelativeToWorld.Z, 12);
        }

        [Fact]
        public void Multiply5()
        {
            Quaternion q = new Quaternion(1.0, 2.0, 3.0, 4.0);
            var res = q * 2.0;
            Assert.Equal(1.0 * 2.0, res.W);
            Assert.Equal(2.0 * 2.0, res.VectorPart.X);
            Assert.Equal(3.0 * 2.0, res.VectorPart.Y);
            Assert.Equal(4.0 * 2.0, res.VectorPart.Z);
        }

        [Fact]
        public void Multiply6()
        {
            Quaternion q = new Quaternion(1.0, 2.0, 3.0, 4.0);
            var res = 2.0 * q;
            Assert.Equal(1.0 * 2.0, res.W);
            Assert.Equal(2.0 * 2.0, res.VectorPart.X);
            Assert.Equal(3.0 * 2.0, res.VectorPart.Y);
            Assert.Equal(4.0 * 2.0, res.VectorPart.Z);
        }

        [Fact]
        public void Divide()
        {
            Quaternion q = new Quaternion(1.0, 2.0, 3.0, 4.0);
            var res = q / 0.5;
            Assert.Equal(1.0 * 2.0, res.W);
            Assert.Equal(2.0 * 2.0, res.VectorPart.X);
            Assert.Equal(3.0 * 2.0, res.VectorPart.Y);
            Assert.Equal(4.0 * 2.0, res.VectorPart.Z);
        }

        [Fact]
        public void Magnitude()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 40.0 * IO.Astrodynamics.Constants.Deg2Rad);
            Assert.Equal(1.0, q1.Magnitude());
        }

        [Fact]
        public void Normalize()
        {
            Quaternion q1 = new Quaternion(40.0 * IO.Astrodynamics.Constants.Deg2Rad, new Vector3(2.0, 2.0, 2.0));
            Assert.Equal(3.533749831504592, q1.Magnitude());
            Quaternion q2 = q1.Normalize();
            Assert.Equal(0.19756115573707231, q2.W);
            Assert.Equal(0.56597102097305074, q2.VectorPart.X);
            Assert.Equal(0.56597102097305074, q2.VectorPart.Y);
            Assert.Equal(0.56597102097305074, q2.VectorPart.Z);
            Assert.Equal(1.0, q2.Magnitude());
        }

        [Fact]
        public void Conjugate()
        {
            Quaternion q1 = new Quaternion(40.0 * IO.Astrodynamics.Constants.Deg2Rad, new Vector3(-2.0, 2.0, -2.0));
            Quaternion q2 = q1.Conjugate();
            Assert.Equal(40.0 * IO.Astrodynamics.Constants.Deg2Rad, q2.W);
            Assert.Equal(2.0, q2.VectorPart.X);
            Assert.Equal(-2.0, q2.VectorPart.Y);
            Assert.Equal(2.0, q2.VectorPart.Z);
        }

        [Fact]
        public void Identity_IsTheIdentityRotation_AndCannotBeAssigned()
        {
            // #571: the identity is (1, 0, 0, 0), the default quaternion, and a vector rotated by it is the same vector, bit
            // for bit (q v q* with w = 1 and a zero vector part is exact); the field is init-only, so that no caller can
            // change it (Quaternion.Zero, its former name, was assignable)
            Assert.Equal(new Quaternion(1.0, 0.0, 0.0, 0.0), Quaternion.Identity);
            Assert.Equal(new Quaternion(), Quaternion.Identity);
            var v = new Vector3(1.0, -2.0, 3.0);
            Assert.Equal(v, v.Rotate(Quaternion.Identity));

            var field = typeof(Quaternion).GetField(nameof(Quaternion.Identity));
            Assert.NotNull(field);
            Assert.True(field.IsStatic);
            Assert.True(field.IsInitOnly);
            Assert.Null(typeof(Quaternion).GetField("Zero"));
        }

        [Fact]
        public void LERP()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 0.0);
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.HalfPi);
            Quaternion q3 = q1.Lerp(q2, 0.5);
            Assert.Equal(0.92387953251128674, q3.W, 12);
            Assert.Equal(0.38268343236508978, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }
        
        [Fact]
        public void SLERP()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 0.0);
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.HalfPi);
            Quaternion q3 = q1.Slerp(q2, 0.5);
            Assert.Equal(0.92387953251128674, q3.W, 12);
            Assert.Equal(0.38268343236508978, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }

        [Fact]
        public void LERP2()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 0.0);
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.TwoPi - IO.Astrodynamics.Constants.HalfPi);
            Quaternion q3 = q1.Lerp(q2, 0.5); //Expected 135° on X
            Assert.Equal(0.38268343236500002, q3.W, 12);
            Assert.Equal(0.92387953251099997, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }
        
        [Fact]
        public void SLERP2()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 0.0);
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.TwoPi - IO.Astrodynamics.Constants.HalfPi);
            Quaternion q3 = q1.Slerp(q2, 0.5); //Expected -45° on X
            Assert.Equal(0.92387953251128674, q3.W, 12);
            Assert.Equal(-0.38268343236508984, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }

        // The worst rotation of X against the matrix, 5.551e-16, the same on Linux x64, Windows x64 and macOS arm64
        // (CI run 43595573884, 2026-11-14); twice the worst, rounded up at the second significant digit
        private const double RotationTolerance = 1.2e-15;

        // The worst | |q| - 1 |, 2.220e-16 (one ulp of 1), the same on the three platforms (CI run 43595573884, 2026-11-14);
        // twice the worst, rounded up at the second significant digit (4.4e-16 before, below twice the worst)
        private const double NormTolerance = 4.5e-16;

        // #680: a quaternion of magnitude 0 is no rotation; Normalize returned it, and a vector rotated by it was zero
        [Fact]
        public void Normalize_OfZero_Throws_AndRotateCannotBeFedAZeroAttitude()
        {
            Assert.Throws<InvalidOperationException>(() => new Quaternion(0.0, 0.0, 0.0, 0.0).Normalize());
            Assert.Throws<InvalidOperationException>(() => Vector3.VectorX.Rotate(new Quaternion(0.0, 0.0, 0.0, 0.0).Normalize()));
            Assert.Throws<InvalidOperationException>(() => new Quaternion(-0.0, 0.0, -0.0, 0.0).Normalize());

            // A non-zero quaternion: a unit quaternion, which rotates VectorX like the rotation matrix of the same quaternion,
            // whose first column is (1 - 2(y² + z²), 2(xy + wz), 2(xz - wy)) (Shuster 1993, the active rotation q p q*)
            var random = new Random(680);
            double worstNorm = 0.0;
            double worstRotation = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                var q = new Quaternion(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0,
                    random.NextDouble() * 2.0 - 1.0) * System.Math.Pow(10.0, random.NextDouble() * 20.0 - 10.0);
                if (q.Magnitude() == 0.0) continue;

                var u = q.Normalize();

                double w = u.W, x = u.VectorPart.X, y = u.VectorPart.Y, z = u.VectorPart.Z;
                var expected = new Vector3(1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y + w * z), 2.0 * (x * z - w * y));
                worstNorm = System.Math.Max(worstNorm, System.Math.Abs(u.Magnitude() - 1.0));
                worstRotation = System.Math.Max(worstRotation, (Vector3.VectorX.Rotate(u) - expected).Magnitude());
            }

            _output.WriteLine($"MEASURE #680 Quaternion.Normalize | |q| - 1 |: {worstNorm:E3}");
            _output.WriteLine($"MEASURE #680 Quaternion.Normalize rotation of X against the matrix: {worstRotation:E3}");
            Assert.True(worstNorm <= NormTolerance, $"{worstNorm:E3}");
            Assert.True(worstRotation <= RotationTolerance, $"{worstRotation:E3}");
        }
    
        // #688: the axis of an axis-angle quaternion is a direction; a zero or non-finite one is no rotation
        [Theory]
        [InlineData(0.0, 0.0, 0.0, 90.0)]
        [InlineData(0.0, 0.0, 0.0, 180.0)]
        [InlineData(0.0, 0.0, 0.0, 0.0)]
        [InlineData(double.NaN, 0.0, 1.0, 90.0)]
        [InlineData(0.0, double.PositiveInfinity, 0.0, 90.0)]
        [InlineData(0.0, 0.0, double.NegativeInfinity, 90.0)]
        public void AxisAngle_ZeroOrNonFiniteAxis_ThrowsNamingTheAxis(double x, double y, double z, double angleDegrees)
        {
            var exception = Assert.Throws<ArgumentException>(() => new Quaternion(new Vector3(x, y, z), angleDegrees * IO.Astrodynamics.Constants.Deg2Rad));
            Assert.Equal("axis", exception.ParamName);
        }

        // #688, the table of the issue: an axis along z of any length rotates X by 90° about z to Y, as the unit axis does
        // (the closed form (cos 90°, sin 90°, 0) of the rotation; (0, 0, 2) gave (-1.5, 2, 0), a non-unit quaternion); 1e200
        // and 1e-200, whose squares overflow or underflow, are scaled by their largest component first
        [Theory]
        [InlineData(1.0)]
        [InlineData(2.0)]
        [InlineData(1e-3)]
        [InlineData(1e10)]
        [InlineData(1e200)]
        [InlineData(1e-200)]
        public void AxisAngle_AxisAlongZOfAnyLength_RotatesXToY(double length)
        {
            var q = new Quaternion(new Vector3(0.0, 0.0, length), IO.Astrodynamics.Constants.HalfPi);
            var rotated = Vector3.VectorX.Rotate(q);
            Assert.True(System.Math.Abs(q.Magnitude() - 1.0) <= AxisAngleNormTolerance, $"{q.Magnitude()}");
            Assert.True((rotated - Vector3.VectorY).Magnitude() <= AxisAngleRotationAngleTolerance, $"{rotated}");
        }

        // #688: the worst angle between the rotated vector and Rodrigues' formula over 10 000 random axes of 1e-10 to 1e10,
        // MEASURE 6.350e-16 rad against Rodrigues on each of Linux x64, Windows x64 and macOS arm64 and 6.163e-16 against
        // SPICE vrotv (Validation category, Linux only; CI run 43745883709, 2026-11-15); twice the worst, rounded up at the
        // second significant digit
        private const double AxisAngleRotationAngleTolerance = 1.3e-15;

        // #688: the worst | |v'| / |v_Rodrigues| - 1 |, MEASURE 9.992e-16 against Rodrigues on each of the three platforms and
        // 8.882e-16 against vrotv (Validation category, Linux only; CI run 43745883709, 2026-11-15); as above
        private const double AxisAngleLengthTolerance = 2.0e-15;

        // #688: the worst | |q| - 1 |, MEASURE 3.331e-16 on each of the three platforms (CI run 43745883709, 2026-11-15); as
        // above
        private const double AxisAngleNormTolerance = 6.7e-16;

        // #688: the worst element of the rotation matrix of the quaternion against SPICE axisar, MEASURE 8.882e-16
        // (Validation category, Linux only; CI run 43745883709, 2026-11-15); as above
        private const double AxisAngleMatrixTolerance = 1.8e-15;

        private static (Vector3 Axis, double Angle, Vector3 V) RandomAxisAngle(Random random)
        {
            Vector3 direction;
            do
            {
                direction = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0);
            } while (direction.Magnitude() < 1e-3);

            var axis = direction * System.Math.Pow(10.0, random.NextDouble() * 20.0 - 10.0);
            double angle = (random.NextDouble() * 2.0 - 1.0) * IO.Astrodynamics.Constants.TwoPi;
            var v = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0) * 7.0e6;
            return (axis, angle, v);
        }

        private static double AngleBetween(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a.Dot(b));

        // #688: new Quaternion(axis, angle) is the rotation by angle about the direction of axis, whatever its length:
        // Rodrigues' formula v' = v cos a + (k × v) sin a + k (k · v)(1 - cos a), k = axis / |axis|, in plain doubles
        [Fact]
        public void AxisAngle_AnyNonZeroAxis_IsTheRotationOfRodrigues()
        {
            var random = new Random(688);
            double worstAngle = 0.0, worstLength = 0.0, worstNorm = 0.0;
            for (int i = 0; i < 10000; i++)
            {
                var (axis, angle, v) = RandomAxisAngle(random);
                var q = new Quaternion(axis, angle);

                double m = System.Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
                var k = new Vector3(axis.X / m, axis.Y / m, axis.Z / m);
                var expected = v * System.Math.Cos(angle) + k.Cross(v) * System.Math.Sin(angle) + k * (k.Dot(v) * (1.0 - System.Math.Cos(angle)));
                var rotated = v.Rotate(q);

                worstAngle = System.Math.Max(worstAngle, AngleBetween(rotated, expected));
                worstLength = System.Math.Max(worstLength, System.Math.Abs(rotated.Magnitude() / expected.Magnitude() - 1.0));
                worstNorm = System.Math.Max(worstNorm, System.Math.Abs(q.Magnitude() - 1.0));
            }

            _output.WriteLine($"MEASURE #688 axis-angle rotation angle against Rodrigues: {worstAngle:E3} rad");
            _output.WriteLine($"MEASURE #688 axis-angle | |v'| / |v_Rodrigues| - 1 |: {worstLength:E3}");
            _output.WriteLine($"MEASURE #688 axis-angle | |q| - 1 |: {worstNorm:E3}");
            Assert.True(worstAngle <= AxisAngleRotationAngleTolerance, $"{worstAngle:E3}");
            Assert.True(worstLength <= AxisAngleLengthTolerance, $"{worstLength:E3}");
            Assert.True(worstNorm <= AxisAngleNormTolerance, $"{worstNorm:E3}");
        }

        // #688: the same axes against SPICE: the rotated vector against vrotv, the rotation matrix of the quaternion against
        // axisar (CSPICE N0067, which take any non-zero axis), through CspiceDirect (Linux, Validation)
        [Fact]
        [Trait("Category", "Validation")]
        public void AxisAngle_AnyNonZeroAxis_IsTheRotationOfSpice()
        {
            IO.Astrodynamics.Tests.TestKernels.EnsureSolarSystem();
            var random = new Random(6880);
            double worstAngle = 0.0, worstLength = 0.0, worstMatrix = 0.0;
            for (int i = 0; i < 10000; i++)
            {
                var (axis, angle, v) = RandomAxisAngle(random);
                var q = new Quaternion(axis, angle);
                double[] axisArray = [axis.X, axis.Y, axis.Z];

                var spiceV = IO.Astrodynamics.Tests.Body.CspiceDirect.Vrotv([v.X, v.Y, v.Z], axisArray, angle);
                var expected = new Vector3(spiceV[0], spiceV[1], spiceV[2]);
                var rotated = v.Rotate(q);
                worstAngle = System.Math.Max(worstAngle, AngleBetween(rotated, expected));
                worstLength = System.Math.Max(worstLength, System.Math.Abs(rotated.Magnitude() / expected.Magnitude() - 1.0));

                var spiceM = IO.Astrodynamics.Tests.Body.CspiceDirect.Axisar(axisArray, angle);
                var matrix = Matrix.FromQuaternion(q);
                for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    worstMatrix = System.Math.Max(worstMatrix, System.Math.Abs(matrix.Get(r, c) - spiceM[3 * r + c]));
            }

            _output.WriteLine($"MEASURE #688 axis-angle rotation angle against SPICE vrotv: {worstAngle:E3} rad");
            _output.WriteLine($"MEASURE #688 axis-angle | |v'| / |v_vrotv| - 1 |: {worstLength:E3}");
            _output.WriteLine($"MEASURE #688 axis-angle matrix against SPICE axisar: {worstMatrix:E3}");
            Assert.True(worstAngle <= AxisAngleRotationAngleTolerance, $"{worstAngle:E3}");
            Assert.True(worstLength <= AxisAngleLengthTolerance, $"{worstLength:E3}");
            Assert.True(worstMatrix <= AxisAngleMatrixTolerance, $"{worstMatrix:E3}");
        }

        // The known limitation of #680: Lerp normalised its result leniently, so a zero or non-finite input, or an
        // interpolation through zero (q + t (q2 - q) = 0, as from q to -q at t = 0.5), returned the zero quaternion
        [Fact]
        public void Lerp_ZeroInputOrAntipodalAtT_Throws()
        {
            var q = new Quaternion(Vector3.VectorZ, 1.0);
            var minusQ = q * -1.0;
            var zero = new Quaternion(0.0, 0.0, 0.0, 0.0);
            var nan = new Quaternion(double.NaN, 0.0, 0.0, 0.0);

            Assert.Equal("q2", Assert.Throws<ArgumentException>(() => q.Lerp(minusQ, 0.5)).ParamName);
            Assert.Equal("q2", Assert.Throws<ArgumentException>(() => q.Lerp(zero, 1.0)).ParamName);
            Assert.Equal("q2", Assert.Throws<ArgumentException>(() => q.Lerp(zero, 0.0)).ParamName);
            Assert.Equal("q2", Assert.Throws<ArgumentException>(() => q.Lerp(nan, 0.5)).ParamName);
            Assert.Throws<InvalidOperationException>(() => zero.Lerp(q, 0.5));
            Assert.Throws<InvalidOperationException>(() => nan.Lerp(q, 0.5));

            // Elsewhere on the same path, q + t (q2 - q) = (1 - 2t) q, whose unit quaternion is q for t < 0.5 and -q beyond
            var before = q.Lerp(minusQ, 0.25);
            var after = q.Lerp(minusQ, 0.75);
            Assert.True(System.Math.Abs(before.W - q.W) <= 1e-16 && (before.VectorPart - q.VectorPart).Magnitude() <= 1e-16, $"{before}");
            Assert.True(System.Math.Abs(after.W + q.W) <= 1e-16 && (after.VectorPart + q.VectorPart).Magnitude() <= 1e-16, $"{after}");
        }

        // The worst component of SLERP(2 I, 3 Rx(90°), 0.5) against the closed form Rx(45°) = (cos(π/8), sin(π/8), 0, 0),
        // MEASURE 0 and | |s| - 1 | 0 on each of Linux x64, Windows x64 and macOS arm64 (CI run 43745883709, 2026-11-15);
        // twice the worst (one ulp of 1 when the measure is exact)
        private const double SlerpTolerance = 2.3e-16;

        // The known limitation of #680: SLERP never normalised its inputs, so a non-unit input gave a non-unit result (the
        // SLERP of the quaternions, not of their rotations) and a zero input a quaternion of magnitude 0.707. It is the
        // SLERP of the unit quaternions (Shoemake 1985): the rotation by t times the angle about the fixed axis
        [Fact]
        public void Slerp_NonUnitInputs_AreTheSlerpOfTheirRotations()
        {
            var from = Quaternion.Identity * 2.0;
            var to = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.HalfPi) * 3.0;
            var s = from.Slerp(to, 0.5);
            double w = System.Math.Cos(System.Math.PI / 8.0), x = System.Math.Sin(System.Math.PI / 8.0);
            double worst = System.Math.Max(System.Math.Max(System.Math.Abs(s.W - w), System.Math.Abs(s.VectorPart.X - x)),
                System.Math.Max(System.Math.Abs(s.VectorPart.Y), System.Math.Abs(s.VectorPart.Z)));
            _output.WriteLine($"MEASURE SLERP(2 I, 3 Rx(90°), 0.5) against Rx(45°): {worst:E3}, | |s| - 1 |: {System.Math.Abs(s.Magnitude() - 1.0):E3}");
            Assert.True(worst <= SlerpTolerance, $"{worst:E3}");
            Assert.True(System.Math.Abs(s.Magnitude() - 1.0) <= SlerpTolerance, $"{s.Magnitude()}");

            var zero = new Quaternion(0.0, 0.0, 0.0, 0.0);
            Assert.Equal("q", Assert.Throws<ArgumentException>(() => Quaternion.Identity.Slerp(zero, 0.5)).ParamName);
            Assert.Equal("q", Assert.Throws<ArgumentException>(() => Quaternion.Identity.Slerp(new Quaternion(1.0, double.NaN, 0.0, 0.0), 0.5)).ParamName);
            Assert.Equal("q", Assert.Throws<ArgumentException>(() => Quaternion.Identity.Slerp(new Quaternion(double.PositiveInfinity, 0.0, 0.0, 0.0), 0.5)).ParamName);
            Assert.Throws<InvalidOperationException>(() => zero.Slerp(Quaternion.Identity, 0.5));
            Assert.Throws<InvalidOperationException>(() => new Quaternion(double.NaN, 0.0, 0.0, 0.0).Slerp(Quaternion.Identity, 0.5));
        }

        // Third round of #668: the magnitude was not scaled, so a quaternion with a component above about 1.34e154
        // normalized to the zero quaternion, one with every component below about 1.5e-162 threw "magnitude of 0", and a NaN
        // or infinite component gave NaN components silently. The closed form: (1, 2, 2, 4) has magnitude 5, so (1, 2, 2, 4)
        // 2^k normalizes to (0.2, 0.4, 0.4, 0.8), each correctly rounded, at any k (the scaling by a power of two is exact)
        [Theory]
        [InlineData(0)]
        [InlineData(600)]
        [InlineData(1000)]
        [InlineData(-600)]
        [InlineData(-1000)]
        [InlineData(-1070)]
        public void Normalize_ExtremeFiniteQuaternions_AreExactUnitQuaternions(int k)
        {
            var u = new Quaternion(System.Math.ScaleB(1.0, k), System.Math.ScaleB(-2.0, k), System.Math.ScaleB(2.0, k), System.Math.ScaleB(4.0, k)).Normalize();
            Assert.Equal(new Quaternion(1.0 / 5.0, -2.0 / 5.0, 2.0 / 5.0, 4.0 / 5.0), u);
        }

        [Theory]
        [InlineData(1e200)]
        [InlineData(1e-170)]
        [InlineData(double.MaxValue)]
        [InlineData(4.9e-324)]
        public void Normalize_ExtremeEqualComponents_IsTheirUnitQuaternion(double c)
        {
            var u = new Quaternion(c, c, 0.0, 0.0).Normalize();
            double ulp = System.Math.BitIncrement(System.Math.Sqrt(0.5)) - System.Math.Sqrt(0.5);
            Assert.True(System.Math.Abs(u.W - System.Math.Sqrt(0.5)) <= ulp, $"{u}");
            Assert.Equal(u.W, u.VectorPart.X);
            Assert.Equal(0.0, u.VectorPart.Y);
            Assert.Equal(0.0, u.VectorPart.Z);
        }

        [Theory]
        [InlineData(double.NaN, 0.0, 0.0, 0.0)]
        [InlineData(double.PositiveInfinity, 0.0, 0.0, 0.0)]
        [InlineData(1.0, 0.0, double.NegativeInfinity, 0.0)]
        [InlineData(1.0, 0.0, 0.0, double.NaN)]
        public void Normalize_NonFinite_Throws(double w, double x, double y, double z)
        {
            Assert.Throws<InvalidOperationException>(() => new Quaternion(w, x, y, z).Normalize());
        }

        // The magnitude was not scaled: +Infinity above about 1.34e154, 0 below about 1.5e-162. Closed form: |(1, 2, 2, 4)| = 5
        // times any power of two, exactly; ordinary quaternions bit for bit the unscaled magnitude
        [Fact]
        public void Magnitude_ExtremeFiniteQuaternions()
        {
            Assert.Equal(System.Math.ScaleB(5.0, 1000), new Quaternion(System.Math.ScaleB(1.0, 1000), System.Math.ScaleB(2.0, 1000), System.Math.ScaleB(2.0, 1000), System.Math.ScaleB(4.0, 1000)).Magnitude());
            Assert.Equal(System.Math.ScaleB(5.0, -1060), new Quaternion(System.Math.ScaleB(1.0, -1060), System.Math.ScaleB(2.0, -1060), System.Math.ScaleB(2.0, -1060), System.Math.ScaleB(4.0, -1060)).Magnitude());
            Assert.Equal(1e-170, new Quaternion(0.0, 0.0, 0.0, -1e-170).Magnitude());
            Assert.Equal(double.PositiveInfinity, new Quaternion(double.MaxValue, double.MaxValue, 0.0, 0.0).Magnitude());
            Assert.Equal(0.0, new Quaternion(0.0, 0.0, 0.0, 0.0).Magnitude());
            Assert.True(double.IsNaN(new Quaternion(double.NaN, 0.0, 0.0, 0.0).Magnitude()));

            var random = new Random(668);
            for (int i = 0; i < 10000; i++)
            {
                var q = new Quaternion(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0,
                    random.NextDouble() * 2.0 - 1.0) * System.Math.Pow(10.0, random.NextDouble() * 300.0 - 150.0);
                double w = q.W, x = q.VectorPart.X, y = q.VectorPart.Y, z = q.VectorPart.Z;
                Assert.Equal(System.Math.Sqrt(w * w + x * x + y * y + z * z), q.Magnitude());
            }
        }

        // The worst element of FromQuaternion((1e200, 1e200, 0, 0)) against the closed form of Rx(90°), and of the SLERP
        // of the identity times 2^1000 and Rx(90°) times 2^-1000 at 0.5 against Rx(45°) = (cos π/8, sin π/8, 0, 0): MEASURE
        // 2.220e-16 and 0 on Linux x64 and macOS arm64, 2.220e-16 and 5.551e-17 on Windows x64 (CI run 43745883709,
        // 2026-11-15); twice the worst, rounded up at the second significant digit
        private const double ExtremeRotationTolerance = 4.5e-16;

        // Third round of #668: FromQuaternion, SLERP and Lerp required a magnitude that was neither 0 nor infinite, unscaled,
        // so a finite non-zero quaternion outside about 1e-154 to 1e154 threw; Lerp's chord could overflow too
        [Fact]
        public void FromQuaternion_Lerp_Slerp_AcceptFiniteExtremeQuaternions()
        {
            var m = Matrix.FromQuaternion(new Quaternion(1e200, 1e200, 0.0, 0.0));
            double[,] rx90 = { { 1.0, 0.0, 0.0 }, { 0.0, 0.0, -1.0 }, { 0.0, 1.0, 0.0 } };
            double worstMatrix = 0.0;
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                worstMatrix = System.Math.Max(worstMatrix, System.Math.Abs(m.Get(i, j) - rx90[i, j]));

            var from = Quaternion.Identity * System.Math.ScaleB(1.0, 1000);
            var to = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.HalfPi) * System.Math.ScaleB(1.0, -1000);
            var s = from.Slerp(to, 0.5);
            double cw = System.Math.Cos(System.Math.PI / 8.0), cx = System.Math.Sin(System.Math.PI / 8.0);
            double worstSlerp = System.Math.Max(System.Math.Max(System.Math.Abs(s.W - cw), System.Math.Abs(s.VectorPart.X - cx)),
                System.Math.Max(System.Math.Abs(s.VectorPart.Y), System.Math.Abs(s.VectorPart.Z)));

            _output.WriteLine($"MEASURE FromQuaternion((1e200, 1e200, 0, 0)) against Rx(90°): {worstMatrix:E3}");
            _output.WriteLine($"MEASURE SLERP(2^1000 I, 2^-1000 Rx(90°), 0.5) against Rx(45°): {worstSlerp:E3}");
            Assert.True(worstMatrix <= ExtremeRotationTolerance, $"{worstMatrix:E3}");
            Assert.True(worstSlerp <= ExtremeRotationTolerance, $"{worstSlerp:E3}");

            // The chord from (1e308, 0, 0, 0) to (-1e308, 1e308, 0, 0) overflows (-2e308); its midpoint is (0, 0.5e308, 0, 0),
            // whose unit quaternion is (0, 1, 0, 0) exactly
            Assert.Equal(new Quaternion(0.0, 1.0, 0.0, 0.0), new Quaternion(1e308, 0.0, 0.0, 0.0).Lerp(new Quaternion(-1e308, 1e308, 0.0, 0.0), 0.5));
            // Tiny quaternions: the chord from 2^-1060 (1, 0, 0, 0) to 2^-1060 (0, 1, 0, 0) at 0.5 is (1, 1, 0, 0) 2^-1061
            var tiny = new Quaternion(System.Math.ScaleB(1.0, -1060), 0.0, 0.0, 0.0).Lerp(new Quaternion(0.0, System.Math.ScaleB(1.0, -1060), 0.0, 0.0), 0.5);
            Assert.Equal(tiny.W, tiny.VectorPart.X);
            Assert.True(System.Math.Abs(tiny.W - System.Math.Sqrt(0.5)) <= ExtremeRotationTolerance, $"{tiny}");
        }

        // Lerp computed the chord q + t (q2 − q) of tiny quaternions unscaled, so its products rounded into the subnormal
        // range: from s (1, 0, 0, 0) to s (0, 1, 0, 0) at 4.9e-324 any t gave (1, 0, 0, 0), at 1e-320 the components were
        // off by up to 6.8e-4. The closed form: (1 − t, t, 0, 0) / √((1 − t)² + t²) at any scale s, over 1000 draws of t
        // per scale. The worst component, MEASURE 3.331e-16 (1e-160; 0 to 2.220e-16 at the other scales; the same on Linux
        // x64, Windows x64 and macOS arm64, CI run 43745883709, 2026-11-15; before the fix 0.707 at 4.9e-324, 3.99e-4 at
        // 1e-320, 3.99e-14 at 1e-310); twice the worst, rounded up at the second significant digit
        [Theory]
        [InlineData(4.9e-324)]
        [InlineData(1e-320)]
        [InlineData(1e-310)]
        [InlineData(2.2250738585072014e-308)]
        [InlineData(1e-300)]
        [InlineData(1e-160)]
        public void Lerp_TinyQuaternions_InterpolateAsTheirDirections(double s)
        {
            var q = new Quaternion(s, 0.0, 0.0, 0.0);
            var q2 = new Quaternion(0.0, s, 0.0, 0.0);
            var random = new Random(668);
            double worst = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                double t = i == 0 ? 0.3 : i == 1 ? 1.0 / 3.0 : random.NextDouble();
                double norm = System.Math.Sqrt((1.0 - t) * (1.0 - t) + t * t);
                var expected = new Quaternion((1.0 - t) / norm, t / norm, 0.0, 0.0);
                var lerp = q.Lerp(q2, t);
                worst = System.Math.Max(worst, System.Math.Max(System.Math.Abs(lerp.W - expected.W), System.Math.Abs(lerp.VectorPart.X - expected.VectorPart.X)));
                Assert.Equal(0.0, lerp.VectorPart.Y);
                Assert.Equal(0.0, lerp.VectorPart.Z);
            }

            _output.WriteLine($"MEASURE Lerp of s (1, 0, 0, 0) to s (0, 1, 0, 0) at s = {s:E3}, worst component: {worst:E3}");
            Assert.True(worst <= TinyLerpTolerance, $"{worst:E3}");
        }

        private const double TinyLerpTolerance = 6.7e-16;
    }
}
