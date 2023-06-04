#include <gtest/gtest.h>
#include <Quaternion.h>
#include <Constants.h>

TEST(Quaternion, Initialization)
{
	IO::Astrodynamics::Math::Quaternion q(1.0, 2.0, 3.0, 4.0);
	ASSERT_DOUBLE_EQ(1.0, q.GetQ0());
	ASSERT_DOUBLE_EQ(2.0, q.GetQ1());
	ASSERT_DOUBLE_EQ(3.0, q.GetQ2());
	ASSERT_DOUBLE_EQ(4.0, q.GetQ3());

	IO::Astrodynamics::Math::Quaternion qx({1.0, 0.0, 0.0}, IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	ASSERT_DOUBLE_EQ(0.93969262078590843, qx.GetQ0());
	ASSERT_DOUBLE_EQ(0.34202014332566871, qx.GetQ1());
	ASSERT_DOUBLE_EQ(0.0, qx.GetQ2());
	ASSERT_DOUBLE_EQ(0.0, qx.GetQ3());

	IO::Astrodynamics::Math::Quaternion qy({0.0, 1.0, 0.0}, IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	ASSERT_DOUBLE_EQ(0.93969262078590843, qy.GetQ0());
	ASSERT_DOUBLE_EQ(0.0, qy.GetQ1());
	ASSERT_DOUBLE_EQ(0.34202014332566871, qy.GetQ2());
	ASSERT_DOUBLE_EQ(0.0, qy.GetQ3());

	IO::Astrodynamics::Math::Quaternion qz({0.0, 0.0, 1.0}, IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	ASSERT_DOUBLE_EQ(0.93969262078590843, qz.GetQ0());
	ASSERT_DOUBLE_EQ(0.0, qz.GetQ1());
	ASSERT_DOUBLE_EQ(0.0, qz.GetQ2());
	ASSERT_DOUBLE_EQ(0.34202014332566871, qz.GetQ3());

	IO::Astrodynamics::Math::Quaternion qall(IO::Astrodynamics::Math::Vector3D(1.0, 1.0, 1.0).Normalize(), IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	ASSERT_DOUBLE_EQ(0.93969262078590843, qall.GetQ0());
	ASSERT_DOUBLE_EQ(0.19746542181734925, qall.GetQ1());
	ASSERT_DOUBLE_EQ(0.19746542181734925, qall.GetQ2());
	ASSERT_DOUBLE_EQ(0.19746542181734925, qall.GetQ3());
}

TEST(Quaternion, Multiply)
{
	IO::Astrodynamics::Math::Quaternion qx({1.0, 0.0, 0.0}, IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	IO::Astrodynamics::Math::Quaternion qy({0.0, 1.0, 0.0}, IO::Astrodynamics::Constants::DEG_RAD * 40.0);

	auto qres = qx * qy;
	ASSERT_DOUBLE_EQ(0.88302222155948906, qres.GetQ0());
	ASSERT_DOUBLE_EQ(0.32139380484326968, qres.GetQ1());
	ASSERT_DOUBLE_EQ(0.32139380484326968, qres.GetQ2());
	ASSERT_DOUBLE_EQ(0.11697777844051097, qres.GetQ3());

	qres = qx.Multiply(qy);
	ASSERT_DOUBLE_EQ(0.88302222155948906, qres.GetQ0());
	ASSERT_DOUBLE_EQ(0.32139380484326968, qres.GetQ1());
	ASSERT_DOUBLE_EQ(0.32139380484326968, qres.GetQ2());
	ASSERT_DOUBLE_EQ(0.11697777844051097, qres.GetQ3());
}

TEST(Quaternion, GetMatrix)
{
	IO::Astrodynamics::Math::Quaternion qx(IO::Astrodynamics::Math::Vector3D(1.0, 0.0, 1.0).Normalize(), IO::Astrodynamics::Constants::DEG_RAD * 40.0);

	auto mtx = qx.GetMatrix();
	ASSERT_DOUBLE_EQ(0.88302222155948906, mtx.GetValue(0, 0));
	ASSERT_DOUBLE_EQ(-0.45451947767204359, mtx.GetValue(0, 1));
	ASSERT_DOUBLE_EQ(0.11697777844051094, mtx.GetValue(0, 2));
	ASSERT_DOUBLE_EQ(0.45451947767204359, mtx.GetValue(1, 0));
	ASSERT_DOUBLE_EQ(0.76604444311897812, mtx.GetValue(1, 1));
	ASSERT_DOUBLE_EQ(-0.45451947767204359, mtx.GetValue(1, 2));
	ASSERT_DOUBLE_EQ(0.11697777844051094, mtx.GetValue(2, 0));
	ASSERT_DOUBLE_EQ(0.45451947767204359, mtx.GetValue(2, 1));
	ASSERT_DOUBLE_EQ(0.88302222155948906, mtx.GetValue(2, 2));
}

TEST(Quaternion, Magnitude)
{
	IO::Astrodynamics::Math::Quaternion qx(IO::Astrodynamics::Constants::DEG_RAD * 40.0, 2.0, 2.0, 2.0);

	auto res = qx.Magnitude();
	ASSERT_DOUBLE_EQ(3.5337498315045921, res);
}

TEST(Quaternion, Normalize)
{
	IO::Astrodynamics::Math::Quaternion qx(IO::Astrodynamics::Constants::DEG_RAD * 40.0, 2.0, 2.0, 2.0);

	auto res = qx.Normalize();
	ASSERT_DOUBLE_EQ(0.19756115573707231, res.GetQ0());
	ASSERT_DOUBLE_EQ(0.56597102097305074, res.GetQ1());
	ASSERT_DOUBLE_EQ(0.56597102097305074, res.GetQ2());
	ASSERT_DOUBLE_EQ(0.56597102097305074, res.GetQ3());
}

TEST(Quaternion, Conjugate)
{
	IO::Astrodynamics::Math::Quaternion qx(IO::Astrodynamics::Constants::DEG_RAD * 40.0, 2.0, 2.0, 2.0);

	auto res = qx.Conjugate();
	ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::DEG_RAD * 40.0, res.GetQ0());
	ASSERT_DOUBLE_EQ(-2.0, res.GetQ1());
	ASSERT_DOUBLE_EQ(-2.0, res.GetQ2());
	ASSERT_DOUBLE_EQ(-2.0, res.GetQ3());
}

TEST(Quaternion, Assignment)
{
	IO::Astrodynamics::Math::Quaternion q(1.0, 2.0, 3.0, 4.0);
	IO::Astrodynamics::Math::Quaternion q2(9.0, 7.0, 43.0, 1.0);
	q2 = q;
	ASSERT_DOUBLE_EQ(1.0, q2.GetQ0());
	ASSERT_DOUBLE_EQ(2.0, q2.GetQ1());
	ASSERT_DOUBLE_EQ(3.0, q2.GetQ2());
	ASSERT_DOUBLE_EQ(4.0, q2.GetQ3());
}