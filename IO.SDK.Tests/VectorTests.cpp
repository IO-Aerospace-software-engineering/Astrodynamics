#include <gtest/gtest.h>
#include <Vector3D.h>
#include <Quaternion.h>
#include <Constants.h>

TEST(Vector, Initialization)
{
	IO::SDK::Math::Vector3D vector(1.0, 2.0, 3.0);
	ASSERT_DOUBLE_EQ(1.0, vector.GetX());
	ASSERT_DOUBLE_EQ(2.0, vector.GetY());
	ASSERT_DOUBLE_EQ(3.0, vector.GetZ());
}

TEST(Vector, Magnitude)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	ASSERT_DOUBLE_EQ(5.3851648071345037, vector.Magnitude());
}

TEST(Vector, Add)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	IO::SDK::Math::Vector3D vector2(1.0, 2.0, 3.0);
	auto res = vector + vector2;

	ASSERT_DOUBLE_EQ(3.0, res.GetX());
	ASSERT_DOUBLE_EQ(5.0, res.GetY());
	ASSERT_DOUBLE_EQ(7.0, res.GetZ());
}

TEST(Vector, Substract)
{
	IO::SDK::Math::Vector3D vector(2.0, 33.0, 4.0);
	IO::SDK::Math::Vector3D vector2(10.0, 2.0, -3.0);
	auto res = vector - vector2;

	ASSERT_DOUBLE_EQ(-8.0, res.GetX());
	ASSERT_DOUBLE_EQ(31.0, res.GetY());
	ASSERT_DOUBLE_EQ(7.0, res.GetZ());
}

TEST(Vector, Multiply)
{
	IO::SDK::Math::Vector3D vector(2.0, 33.0, -4.0);

	auto res = vector * 10;

	ASSERT_DOUBLE_EQ(20.0, res.GetX());
	ASSERT_DOUBLE_EQ(330.0, res.GetY());
	ASSERT_DOUBLE_EQ(-40.0, res.GetZ());
}

TEST(Vector, Divide)
{
	IO::SDK::Math::Vector3D vector(2.0, 34.0, 4.0);
	auto res = vector / -2;

	ASSERT_DOUBLE_EQ(-1, res.GetX());
	ASSERT_DOUBLE_EQ(-17.0, res.GetY());
	ASSERT_DOUBLE_EQ(-2.0, res.GetZ());
}

TEST(Vector, CrossProduct)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	IO::SDK::Math::Vector3D vector2(5.0, 6.0, 7.0);
	auto res = vector.CrossProduct(vector2);

	ASSERT_DOUBLE_EQ(-3.0, res.GetX());
	ASSERT_DOUBLE_EQ(6.0, res.GetY());
	ASSERT_DOUBLE_EQ(-3.0, res.GetZ());
}

TEST(Vector, DotProduct)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	IO::SDK::Math::Vector3D vector2(5.0, 6.0, 7.0);
	auto res = vector.DotProduct(vector2);

	ASSERT_DOUBLE_EQ(56.0, res);
}

TEST(Vector, Normalize)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	auto res = vector.Normalize();

	ASSERT_DOUBLE_EQ(0.37139067635410372, res.GetX());
	ASSERT_DOUBLE_EQ(0.55708601453115558, res.GetY());
	ASSERT_DOUBLE_EQ(0.74278135270820744, res.GetZ());
	ASSERT_DOUBLE_EQ(1.0, res.Magnitude());

	IO::SDK::Math::Vector3D vectorZero(0.0, 0.0, 0.0);
	ASSERT_THROW(vectorZero.Normalize(), std::exception);
}

TEST(Vector, GetAngle)
{
	IO::SDK::Math::Vector3D vector(2.0, 3.0, 4.0);
	IO::SDK::Math::Vector3D vector2(5.0, 6.0, 7.0);
	auto res = vector.GetAngle(vector2);

	ASSERT_DOUBLE_EQ(0.13047716072476959, res);

	IO::SDK::Math::Vector3D vector3(0.0, 3.0, 0.0);
	IO::SDK::Math::Vector3D vector4(-1.0, 0.0, 0.0);
	res = vector3.GetAngle(vector4);
	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI2, res);

	IO::SDK::Math::Vector3D vector5(0.0, 3.0, 0.0);
	IO::SDK::Math::Vector3D vector6(1.0, 0.0, 0.0);
	res = vector5.GetAngle(vector6);
	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI2, res);
}

TEST(Vector, Rotate)
{
	IO::SDK::Math::Vector3D vector(1.0, 0.0, 0.0);
	IO::SDK::Math::Quaternion q(IO::SDK::Math::Vector3D(0.0, 0.0, 1.0), IO::SDK::Constants::PI2);
	auto res = vector.Rotate(q);

	ASSERT_NEAR(0.0, res.GetX(), 1E-07);
	ASSERT_NEAR(1.0, res.GetY(), 1E-07);
	ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

	IO::SDK::Math::Quaternion q1(IO::SDK::Math::Vector3D(0.0, 0.0, 1.0), -IO::SDK::Constants::PI2);
	res = vector.Rotate(q1);

	ASSERT_NEAR(0.0, res.GetX(), 1E-07);
	ASSERT_NEAR(-1.0, res.GetY(), 1E-07);
	ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

	IO::SDK::Math::Quaternion q2(IO::SDK::Math::Vector3D(0.0, 0.0, 1.0), IO::SDK::Constants::PI);
	res = vector.Rotate(q2);

	ASSERT_NEAR(-1.0, res.GetX(), 1E-07);
	ASSERT_NEAR(0.0, res.GetY(), 1E-07);
	ASSERT_NEAR(0.0, res.GetZ(), 1E-09);

	IO::SDK::Math::Quaternion q3(IO::SDK::Math::Vector3D(0.0, 1.0, 1.0).Normalize(), IO::SDK::Constants::PI2);
	res = vector.Rotate(q3);

	ASSERT_NEAR(0.0, res.GetX(), 1E-07);
	ASSERT_NEAR(0.70710676908493031, res.GetY(), 1E-07);
	ASSERT_NEAR(-0.70710676908493031, res.GetZ(), 1E-09);

	IO::SDK::Math::Quaternion q4(IO::SDK::Math::Vector3D(1.0, 1.0, 1.0).Normalize(), IO::SDK::Constants::PI2);
	res = vector.Rotate(q4);

	ASSERT_NEAR(0.33333330353101093, res.GetX(), 1E-07);
	ASSERT_NEAR(0.91068359264203003, res.GetY(), 1E-07);
	ASSERT_NEAR(-0.24401692597536345, res.GetZ(), 1E-09);
}

TEST(Vector, To)
{
	IO::SDK::Math::Vector3D refVector(0.0, 0.0, 1.0);
	IO::SDK::Math::Vector3D vector(1.0, 0.0, 0.0);

	auto q = vector.To(refVector);

	auto vRes = vector.Rotate(q.Normalize());

	ASSERT_NEAR(0.0, vRes.GetX(), 1e-07);
	ASSERT_NEAR(0.0, vRes.GetY(), 1e-07);
	ASSERT_NEAR(1.0, vRes.GetZ(), 1e-07);
}

TEST(Vector, Reverse)
{
	IO::SDK::Math::Vector3D refVector(1.0, 1.0, 1.0);

	auto vRes = refVector.Reverse();

	ASSERT_DOUBLE_EQ(-1.0, vRes.GetX());
	ASSERT_DOUBLE_EQ(-1.0, vRes.GetY());
	ASSERT_DOUBLE_EQ(-1.0, vRes.GetZ());
}
