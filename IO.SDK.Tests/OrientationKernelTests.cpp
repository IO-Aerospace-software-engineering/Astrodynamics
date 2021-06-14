#include<gtest/gtest.h>
#include<Spacecraft.h>
#include<OrientationKernel.h>
#include<chrono>
#include<InertialFrames.h>
#include<StateOrientation.h>
#include<vector>
#include<Vector3D.h>
#include<Constants.h>
#include<TimeSpan.h>
#include<CelestialBody.h>
#include<memory>

using namespace std::chrono_literals;

TEST(OrientationKernel, WriteData)
{
	const auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s(-150, "Spacecraft150", 500.0,3000.0, "MissionTest",std::move(orbitalParams));

	std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> data;
	std::vector<IO::SDK::OrbitalParameters::StateOrientation> interval;
	auto e = IO::SDK::Time::TDB("2021-01-01T12:00:00");
	auto a = IO::SDK::Math::Vector3D(1.0, 0.0, 0.0);
	auto v = IO::SDK::Math::Vector3D();
	IO::SDK::Time::TimeSpan tol(5.0s);

	for (size_t i = 0; i < 20; i++)
	{
		e = e + IO::SDK::Time::TimeSpan(10s);
		auto q = IO::SDK::Math::Quaternion(a, i * 10 * IO::SDK::Constants::DEG_RAD);
		IO::SDK::OrbitalParameters::StateOrientation s(q, v, e,IO::SDK::Frames::InertialFrames::ICRF);
		interval.push_back(s);
	}

	data.push_back(interval);

	s.WriteOrientations(data);

	//Read first known orientation 0deg
	auto e0 = IO::SDK::Time::TDB("2021-01-01T12:00:10");

	auto orientation = s.GetOrientation(e0, tol, IO::SDK::Frames::InertialFrames::ICRF);
	ASSERT_DOUBLE_EQ(1.0, orientation.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(0.0, orientation.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.0, orientation.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.0, orientation.GetQuaternion().GetQ3());

	ASSERT_DOUBLE_EQ(0.0, orientation.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(0.0, orientation.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(0.0, orientation.GetAngularVelocity().GetZ());

	ASSERT_EQ(e0, orientation.GetEpoch());

	//Read middle known orientation - 60deg
	auto e1 = IO::SDK::Time::TDB("2021-01-01T12:01:10");

	auto orientation1 = s.GetOrientation(e1, tol, IO::SDK::Frames::InertialFrames::ICRF);
	ASSERT_DOUBLE_EQ(0.86602540378443882, orientation1.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(0.49999999999999972, orientation1.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.0, orientation1.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.0, orientation1.GetQuaternion().GetQ3());

	ASSERT_DOUBLE_EQ(0.0, orientation1.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(0.0, orientation1.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(0.0, orientation1.GetAngularVelocity().GetZ());

	ASSERT_EQ(e1, orientation1.GetEpoch());

	//Read end known orientation - 200deg
	auto e2 = IO::SDK::Time::TDB("2021-01-01T12:03:20");

	auto orientation2 = s.GetOrientation(e2, tol, IO::SDK::Frames::InertialFrames::ICRF);
	ASSERT_DOUBLE_EQ(0.087155742747658208, orientation2.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(-0.99619469809174555, orientation2.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.0, orientation2.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.0, orientation2.GetQuaternion().GetQ3());

	ASSERT_DOUBLE_EQ(0.0, orientation2.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(0.0, orientation2.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(0.0, orientation2.GetAngularVelocity().GetZ());

	ASSERT_STREQ(e2.ToString().c_str(), orientation2.GetEpoch().ToString().c_str());

	//Read interpolated orientation - 35deg
	auto e3 = IO::SDK::Time::TDB("2021-01-01T12:00:45");

	auto orientation3 = s.GetOrientation(e3, tol, IO::SDK::Frames::InertialFrames::ICRF);
	ASSERT_DOUBLE_EQ(0.95371695074822693, orientation3.GetQuaternion().GetQ0());
	ASSERT_DOUBLE_EQ(0.30070579950427306, orientation3.GetQuaternion().GetQ1());
	ASSERT_DOUBLE_EQ(0.0, orientation3.GetQuaternion().GetQ2());
	ASSERT_DOUBLE_EQ(0.0, orientation3.GetQuaternion().GetQ3());

	ASSERT_DOUBLE_EQ(0.0, orientation3.GetAngularVelocity().GetX());
	ASSERT_DOUBLE_EQ(0.0, orientation3.GetAngularVelocity().GetY());
	ASSERT_DOUBLE_EQ(0.0, orientation3.GetAngularVelocity().GetZ());

	ASSERT_STREQ(e3.ToString().c_str(), orientation3.GetEpoch().ToString().c_str());

}

TEST(OrientationKernel, GetCoverage)
{
	const auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s(-150, "Spacecraft150", 500.0,3000.0, "MissionTest",std::move(orbitalParams));

	std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> data;
	std::vector<IO::SDK::OrbitalParameters::StateOrientation> interval;
	auto e = IO::SDK::Time::TDB("2021-01-01T12:00:00");
	auto a = IO::SDK::Math::Vector3D(1.0, 0.0, 0.0);
	auto v = IO::SDK::Math::Vector3D();

	for (size_t i = 0; i < 20; i++)
	{
		e = e + IO::SDK::Time::TimeSpan(10s);
		auto q = IO::SDK::Math::Quaternion(a, i * 10 * IO::SDK::Constants::DEG_RAD);
		IO::SDK::OrbitalParameters::StateOrientation s(q, v, e,IO::SDK::Frames::InertialFrames::ICRF);
		interval.push_back(s);
	}

	data.push_back(interval);

	s.WriteOrientations(data);

	const auto window = s.GetOrientationsCoverageWindow();
	ASSERT_DOUBLE_EQ(662774479.18394315, window.GetStartDate().GetSecondsFromJ2000().count());//2021-01-01 12:00:10.000000 UTC

	ASSERT_DOUBLE_EQ(662774669.18394315, window.GetEndDate().GetSecondsFromJ2000().count());//2021-01-01 12:03:20.000000 UTC

	ASSERT_DOUBLE_EQ(190.0, window.GetLength().GetSeconds().count());
}

TEST(OrientationKernel, WriteComment)
{
	const auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s(-150, "Spacecraft150", 500.0,3000.0, "MissionTest",std::move(orbitalParams));

	std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> data;
	std::vector<IO::SDK::OrbitalParameters::StateOrientation> interval;
	auto e = IO::SDK::Time::TDB("2021-01-01T12:00:00");
	auto a = IO::SDK::Math::Vector3D(1.0, 0.0, 0.0);
	auto v = IO::SDK::Math::Vector3D();

	for (size_t i = 0; i < 20; i++)
	{
		e = e + IO::SDK::Time::TimeSpan(10s);
		auto q = IO::SDK::Math::Quaternion(a, i * 10 * IO::SDK::Constants::DEG_RAD);
		IO::SDK::OrbitalParameters::StateOrientation s(q, v, e,IO::SDK::Frames::InertialFrames::ICRF);
		interval.push_back(s);
	}

	data.push_back(interval);

	s.WriteOrientations(data);

	s.WriteOrientationKernelComment("Comment Test");
	auto comment = s.ReadOrientationKernelComment();
	ASSERT_STREQ("Comment Test", comment.c_str());
}
