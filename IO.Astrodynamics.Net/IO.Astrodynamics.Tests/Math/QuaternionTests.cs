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
            Quaternion q = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2);
            Assert.Equal(0.7071067811865476, q.W);
            Assert.Equal(0.0, q.VectorPart.X);
            Assert.Equal(0.0, q.VectorPart.Y);
            Assert.Equal(0.7071067811865475, q.VectorPart.Z, 12);
        }

        [Fact]
        public void ToEulerAngle()
        {
            Quaternion q = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2);
            var res = q.ToEuler();
            Assert.Equal(0.0, res.X);
            Assert.Equal(0.0, res.Y);
            Assert.Equal(Astrodynamics.Constants.PI2, res.Z, 12);
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

            Quaternion spacecraftOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2);
            Quaternion instrumentOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2);

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

            Quaternion spacecraftOrientation = new Quaternion(Vector3.VectorZ, IO.Astrodynamics.Constants.PI2);
            Quaternion instrumentOrientation = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.PI2 * 0.5);

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
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.PI2);
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
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants.PI2);
            Quaternion q3 = q1.SLERP(q2, 0.5);
            Assert.Equal(0.92387953251128674, q3.W, 12);
            Assert.Equal(0.38268343236508978, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }

        [Fact]
        public void LERP2()
        {
            Quaternion q1 = new Quaternion(Vector3.VectorX, 0.0);
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants._2PI - IO.Astrodynamics.Constants.PI2);
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
            Quaternion q2 = new Quaternion(Vector3.VectorX, IO.Astrodynamics.Constants._2PI - IO.Astrodynamics.Constants.PI2);
            Quaternion q3 = q1.SLERP(q2, 0.5); //Expected -45° on X
            Assert.Equal(0.92387953251128674, q3.W, 12);
            Assert.Equal(-0.38268343236508984, q3.VectorPart.X, 12);
            Assert.Equal(0.0, q3.VectorPart.Y);
            Assert.Equal(0.0, q3.VectorPart.Z);
        }

        // Measured on Linux x64 (2026-11-14): 5.551e-16; twice the worst, provisional until the three platforms are measured
        private const double RotationTolerance = 1.2e-15;

        // #680: a quaternion of magnitude 0 is no rotation; Normalize returned it, and a vector rotated by it was zero
        [Fact]
        public void Normalize_OfZero_Throws_AndRotateCannotBeFedAZeroAttitude()
        {
            Assert.Throws<InvalidOperationException>(() => new Quaternion(0.0, 0.0, 0.0, 0.0).Normalize());
            Assert.Throws<InvalidOperationException>(() => Vector3.VectorX.Rotate(new Quaternion(0.0, 0.0, 0.0, 0.0).Normalize()));
            Assert.Throws<InvalidOperationException>(() => new Quaternion(1e-170, 0.0, -1e-170, 0.0).Normalize());

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
            // |q| - 1 measured 2.220e-16 (one ulp of 1): the bound is twice it
            Assert.True(worstNorm <= 4.4e-16, $"{worstNorm:E3}");
            Assert.True(worstRotation <= RotationTolerance, $"{worstRotation:E3}");
        }
    }
}
