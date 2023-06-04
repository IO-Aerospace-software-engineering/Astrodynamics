#include <gtest/gtest.h>
#include <InertialFrames.h>
#include <BodyFixedFrames.h>
#include <Matrix.h>

#include <TDB.h>
#include <chrono>

using namespace std::chrono_literals;

TEST(InertialFrames, ToString)
{
	ASSERT_STREQ("J2000", IO::SDK::Frames::InertialFrames::GetICRF().ToCharArray());
	ASSERT_STREQ("ECLIPJ2000", IO::SDK::Frames::InertialFrames::Ecliptic().ToCharArray());
	ASSERT_STREQ("GALACTIC", IO::SDK::Frames::InertialFrames::Galactic().ToCharArray());
}

TEST(InertialFrames, GetName)
{
	ASSERT_STREQ("J2000", IO::SDK::Frames::InertialFrames::GetICRF().GetName().c_str());
	ASSERT_STREQ("ECLIPJ2000", IO::SDK::Frames::InertialFrames::Ecliptic().GetName().c_str());
	ASSERT_STREQ("GALACTIC", IO::SDK::Frames::InertialFrames::Galactic().GetName().c_str());
}

TEST(InertialFrames, Equal)
{
	ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Frames::InertialFrames::GetICRF());
}

TEST(InertialFrames, NotEqual)
{
	ASSERT_NE(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Frames::InertialFrames::Galactic());
}

TEST(InertialFrames, ToFrame6x6)
{
	auto mtx = IO::SDK::Frames::InertialFrames::GetICRF().ToFrame6x6(IO::SDK::Frames::InertialFrames::Ecliptic(), IO::SDK::Time::TDB(0.0s));

	ASSERT_DOUBLE_EQ(1.0, mtx.GetValue(0, 0));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(0, 1));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(0, 2));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(0, 3));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(0, 4));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(0, 5));

	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(1, 0));
	ASSERT_DOUBLE_EQ(0.91748206206918181, mtx.GetValue(1, 1));
	ASSERT_DOUBLE_EQ(0.39777715593191371, mtx.GetValue(1, 2));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(1, 3));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(1, 4));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(1, 5));

	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(2, 0));
	ASSERT_DOUBLE_EQ(-0.39777715593191371, mtx.GetValue(2, 1));
	ASSERT_DOUBLE_EQ(0.91748206206918181, mtx.GetValue(2, 2));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(2, 3));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(2, 4));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(2, 5));

	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(3, 0));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(3, 1));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(3, 2));
	ASSERT_DOUBLE_EQ(1.0, mtx.GetValue(3, 3));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(3, 4));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(3, 5));

	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(4, 0));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(4, 1));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(4, 2));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(4, 3));
	ASSERT_DOUBLE_EQ(0.91748206206918181, mtx.GetValue(4, 4));
	ASSERT_DOUBLE_EQ(0.39777715593191371, mtx.GetValue(4, 5));

	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(5, 0));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(5, 1));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(5, 2));
	ASSERT_DOUBLE_EQ(0.0, mtx.GetValue(5, 3));
	ASSERT_DOUBLE_EQ(-0.39777715593191371, mtx.GetValue(5, 4));
	ASSERT_DOUBLE_EQ(0.91748206206918181, mtx.GetValue(5, 5));
}

TEST(InertialFrames, TransformVector)
{
	IO::SDK::Math::Vector3D vector{1.0, 0.0, 0.0};
	IO::SDK::Frames::BodyFixedFrames earthFrame("IAU_EARTH");
	auto bodyFixedVector = IO::SDK::Frames::InertialFrames::GetICRF().TransformVector(IO::SDK::Frames::BodyFixedFrames("IAU_EARTH"), vector, IO::SDK::Time::TDB(0.0s));

	//Must go back to original vector
	auto ICRFVector = earthFrame.TransformVector(IO::SDK::Frames::InertialFrames::GetICRF(), bodyFixedVector, IO::SDK::Time::TDB(0.0s));

	ASSERT_DOUBLE_EQ(vector.GetX(), ICRFVector.GetX());
	ASSERT_DOUBLE_EQ(vector.GetY(), ICRFVector.GetY());
	ASSERT_DOUBLE_EQ(vector.GetZ(), ICRFVector.GetZ());
}
