/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <Quaternion.h>
#include <Constants.h>
#include <Planes.h>

TEST(Vector, Initialization)
{
    IO::Astrodynamics::Math::Vector3D vector(1.0, 2.0, 3.0);
    ASSERT_DOUBLE_EQ(1.0, vector.GetX());
    ASSERT_DOUBLE_EQ(2.0, vector.GetY());
    ASSERT_DOUBLE_EQ(3.0, vector.GetZ());
}

TEST(Vector, Magnitude)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    ASSERT_DOUBLE_EQ(5.3851648071345037, vector.Magnitude());
}

TEST(Vector, Add)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    IO::Astrodynamics::Math::Vector3D vector2(1.0, 2.0, 3.0);
    auto res = vector + vector2;

    ASSERT_DOUBLE_EQ(3.0, res.GetX());
    ASSERT_DOUBLE_EQ(5.0, res.GetY());
    ASSERT_DOUBLE_EQ(7.0, res.GetZ());
}

TEST(Vector, Substract)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 33.0, 4.0);
    IO::Astrodynamics::Math::Vector3D vector2(10.0, 2.0, -3.0);
    auto res = vector - vector2;

    ASSERT_DOUBLE_EQ(-8.0, res.GetX());
    ASSERT_DOUBLE_EQ(31.0, res.GetY());
    ASSERT_DOUBLE_EQ(7.0, res.GetZ());
}

TEST(Vector, Multiply)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 33.0, -4.0);

    auto res = vector * 10;

    ASSERT_DOUBLE_EQ(20.0, res.GetX());
    ASSERT_DOUBLE_EQ(330.0, res.GetY());
    ASSERT_DOUBLE_EQ(-40.0, res.GetZ());
}

TEST(Vector, Divide)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 34.0, 4.0);
    auto res = vector / -2;

    ASSERT_DOUBLE_EQ(-1, res.GetX());
    ASSERT_DOUBLE_EQ(-17.0, res.GetY());
    ASSERT_DOUBLE_EQ(-2.0, res.GetZ());
}

TEST(Vector, CrossProduct)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    IO::Astrodynamics::Math::Vector3D vector2(5.0, 6.0, 7.0);
    auto res = vector.CrossProduct(vector2);

    ASSERT_DOUBLE_EQ(-3.0, res.GetX());
    ASSERT_DOUBLE_EQ(6.0, res.GetY());
    ASSERT_DOUBLE_EQ(-3.0, res.GetZ());
}

TEST(Vector, DotProduct)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    IO::Astrodynamics::Math::Vector3D vector2(5.0, 6.0, 7.0);
    auto res = vector.DotProduct(vector2);

    ASSERT_DOUBLE_EQ(56.0, res);
}

TEST(Vector, Normalize)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    auto res = vector.Normalize();

    ASSERT_DOUBLE_EQ(0.37139067635410372, res.GetX());
    ASSERT_DOUBLE_EQ(0.55708601453115558, res.GetY());
    ASSERT_DOUBLE_EQ(0.74278135270820744, res.GetZ());
    ASSERT_DOUBLE_EQ(1.0, res.Magnitude());

    IO::Astrodynamics::Math::Vector3D vectorZero(0.0, 0.0, 0.0);
    ASSERT_EQ(vectorZero.Normalize(), IO::Astrodynamics::Math::Vector3D(0, 0, 0));
}

TEST(Vector, GetAngle)
{
    IO::Astrodynamics::Math::Vector3D vector(2.0, 3.0, 4.0);
    IO::Astrodynamics::Math::Vector3D vector2(5.0, 6.0, 7.0);
    auto res = vector.GetAngle(vector2);

    ASSERT_DOUBLE_EQ(0.13047716072476959, res);

    IO::Astrodynamics::Math::Vector3D vector3(0.0, 3.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector4(-1.0, 0.0, 0.0);
    res = vector3.GetAngle(vector4);
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI2, res);

    IO::Astrodynamics::Math::Vector3D vector5(0.0, 3.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector6(1.0, 0.0, 0.0);
    res = vector5.GetAngle(vector6);
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI2, res);
}

TEST(Vector, GetAngle2)
{
    IO::Astrodynamics::Math::Vector3D vector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector2(0.0, 1.0, 0.0);

    ASSERT_DOUBLE_EQ(0.0, vector.GetAngle(vector2, IO::Astrodynamics::Tests::PlaneZ));
}

TEST(Vector, GetAngle3)
{
    IO::Astrodynamics::Math::Vector3D vector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector2(1.0, 1.0, 0.0);

    ASSERT_NEAR(-0.78539816339744828, vector.GetAngle(vector2, IO::Astrodynamics::Tests::PlaneZ), 1E-06);
}

TEST(Vector, GetAngle4)
{
    IO::Astrodynamics::Math::Vector3D vector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector2(0.0, -1.0, 0.0);

    ASSERT_NEAR(IO::Astrodynamics::Constants::PI, vector.GetAngle(vector2, IO::Astrodynamics::Tests::PlaneZ),1E-06);
}

TEST(Vector, GetAngle5)
{
    IO::Astrodynamics::Math::Vector3D vector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector2(-1.0, -1.0, 0.0);

    ASSERT_NEAR(2.3561944901923448, vector.GetAngle(vector2, IO::Astrodynamics::Tests::PlaneZ),1E-06);
}

TEST(Vector, GetAngle6)
{
    IO::Astrodynamics::Math::Vector3D vector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector2(-1.0, 0.0, 0.0);

    ASSERT_NEAR(IO::Astrodynamics::Constants::PI2, vector.GetAngle(vector2),1E-06);
}

TEST(Vector, Rotate)
{
    IO::Astrodynamics::Math::Vector3D vector(1.0, 0.0, 0.0);
    IO::Astrodynamics::Math::Quaternion q(IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 1.0), IO::Astrodynamics::Constants::PI2);
    auto res = vector.Rotate(q);

    ASSERT_NEAR(0.0, res.GetX(), 1E-07);
    ASSERT_NEAR(1.0, res.GetY(), 1E-07);
    ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

    IO::Astrodynamics::Math::Quaternion q1(IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 1.0), -IO::Astrodynamics::Constants::PI2);
    res = vector.Rotate(q1);

    ASSERT_NEAR(0.0, res.GetX(), 1E-07);
    ASSERT_NEAR(-1.0, res.GetY(), 1E-07);
    ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

    IO::Astrodynamics::Math::Quaternion q2(IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 1.0), IO::Astrodynamics::Constants::PI);
    res = vector.Rotate(q2);

    ASSERT_NEAR(-1.0, res.GetX(), 1E-07);
    ASSERT_NEAR(0.0, res.GetY(), 1E-07);
    ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

    IO::Astrodynamics::Math::Quaternion q3(IO::Astrodynamics::Math::Vector3D(0.0, 1.0, 1.0).Normalize(), IO::Astrodynamics::Constants::PI2);
    res = vector.Rotate(q3);

    ASSERT_NEAR(0.0, res.GetX(), 1E-07);
    ASSERT_NEAR(0.70710676908493031, res.GetY(), 1E-07);
    ASSERT_NEAR(-0.70710678118654746, res.GetZ(), 1E-09);

    IO::Astrodynamics::Math::Quaternion q4(IO::Astrodynamics::Math::Vector3D(1.0, 1.0, 1.0).Normalize(), IO::Astrodynamics::Constants::PI2);
    res = vector.Rotate(q4);

    ASSERT_NEAR(0.33333330353101093, res.GetX(), 1E-07);
    ASSERT_NEAR(0.91068359264203003, res.GetY(), 1E-07);
    ASSERT_NEAR(-0.2440169358562925, res.GetZ(), 1E-09);
}

TEST(Vector, To)
{
    IO::Astrodynamics::Math::Vector3D refVector(0.0, 0.0, 1.0);
    IO::Astrodynamics::Math::Vector3D vector(1.0, 0.0, 0.0);

    auto q = vector.To(refVector);

    auto vRes = vector.Rotate(q.Normalize());

    ASSERT_NEAR(0.0, vRes.GetX(), 1e-07);
    ASSERT_NEAR(0.0, vRes.GetY(), 1e-07);
    ASSERT_NEAR(1.0, vRes.GetZ(), 1e-07);
}

TEST(Vector, To2)
{
    IO::Astrodynamics::Math::Vector3D refVector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector(0.0, -0.2, 0.7);

    auto q = refVector.To(vector);

    ASSERT_NEAR(0.52801098892805176, q.GetQ0(), 1e-07);
    ASSERT_NEAR(0.69999999999999996, q.GetQ1(), 1e-07);
    ASSERT_NEAR(0.0, q.GetQ2(), 1e-07);
    ASSERT_NEAR(0.0, q.GetQ3(), 1e-07);
}

TEST(Vector, To3)
{
    IO::Astrodynamics::Math::Vector3D refVector(0.0, 1.0, 0.0);
    IO::Astrodynamics::Math::Vector3D vector(0.0, 10.0, 0.0);

    auto q = refVector.To(vector).Normalize();

    ASSERT_NEAR(1.0, q.GetQ0(), 1e-07);
    ASSERT_NEAR(0.0, q.GetQ1(), 1e-07);
    ASSERT_NEAR(0.0, q.GetQ2(), 1e-07);
    ASSERT_NEAR(0.0, q.GetQ3(), 1e-07);
}

TEST(Vector, Reverse)
{
    IO::Astrodynamics::Math::Vector3D refVector(1.0, 1.0, 1.0);

    auto vRes = refVector.Reverse();

    ASSERT_DOUBLE_EQ(-1.0, vRes.GetX());
    ASSERT_DOUBLE_EQ(-1.0, vRes.GetY());
    ASSERT_DOUBLE_EQ(-1.0, vRes.GetZ());
}
