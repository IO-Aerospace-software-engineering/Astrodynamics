#include<gtest/gtest.h>
#include<StateOrientation.h>
#include<chrono>
#include<Constants.h>
#include<InertialFrames.h>

using namespace std::chrono_literals;

TEST(StateOrientation, InitializationByValues)
{
	IO::Astrodynamics::Time::TDB tdb("2020-01-01T12:00:00");
	IO::Astrodynamics::OrbitalParameters::StateOrientation so(1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, tdb, IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(1.0, so.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(2.0, so.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(3.0, so.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(4.0, so.GetQuaternion().GetQ3());
	ASSERT_DOUBLE_EQ(5.0, so.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(6.0, so.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(7.0, so.GetAngularVelocity().GetZ());
	ASSERT_EQ(IO::Astrodynamics::Time::TDB("2020-01-01T12:00:00"), so.GetEpoch());
}

TEST(StateOrientation, InitializationFromAngles)
{
	IO::Astrodynamics::Time::TDB tdb("2020-01-01T12:00:00");
	IO::Astrodynamics::OrbitalParameters::StateOrientation so(IO::Astrodynamics::Math::Vector3D(1.0, 1.0, 1.0).Normalize(), IO::Astrodynamics::Constants::DEG_RAD * 40.0, IO::Astrodynamics::Math::Vector3D(5.0, 6.0, 7.0), tdb,
                                                              IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(0.93969262078590843, so.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ3());
	ASSERT_DOUBLE_EQ(5.0, so.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(6.0, so.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(7.0, so.GetAngularVelocity().GetZ());
	ASSERT_EQ(IO::Astrodynamics::Time::TDB("2020-01-01T12:00:00"), so.GetEpoch());
}

TEST(StateOrientation, InitializationFromQuaternion)
{
	IO::Astrodynamics::Time::TDB tdb("2020-01-01T12:00:00");
	IO::Astrodynamics::Math::Quaternion q(IO::Astrodynamics::Math::Vector3D(1.0, 1.0, 1.0).Normalize(), IO::Astrodynamics::Constants::DEG_RAD * 40.0);
	IO::Astrodynamics::Math::Vector3D v(5.0, 6.0, 7.0);

	IO::Astrodynamics::OrbitalParameters::StateOrientation so(q, v, tdb, IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(0.93969262078590843, so.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.19746542181734925, so.GetQuaternion().GetQ3());
	ASSERT_DOUBLE_EQ(5.0, so.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(6.0, so.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(7.0, so.GetAngularVelocity().GetZ());
	ASSERT_EQ(IO::Astrodynamics::Time::TDB("2020-01-01T12:00:00"), so.GetEpoch());
}