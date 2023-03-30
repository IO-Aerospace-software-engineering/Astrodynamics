#include <gtest/gtest.h>
#include <SpacecraftClockKernel.h>
#include <filesystem>
#include <Parameters.h>
#include <TDB.h>
#include <Spacecraft.h>
#include <CelestialBody.h>
#include <InertialFrames.h>

using namespace std::chrono_literals;

TEST(SpacecraftClockKernel, BuildGenericKernel)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	auto path = s.GetClock().GetPath();
	ASSERT_TRUE(std::filesystem::exists(path));
	ASSERT_GT(std::filesystem::file_size(filepath), 0.0);
}

TEST(SpacecraftClockKernel, GetCoverage)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	const auto window = s.GetClock().GetCoverageWindow();
	ASSERT_DOUBLE_EQ(6.62731200E+08, window.GetStartDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(4.9576984959999084E+09, window.GetEndDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(4.2949672959999089E+09, window.GetLength().GetSeconds().count());
}

TEST(SpacecraftClockKernel, ConvertToTDB)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	IO::SDK::Time::TDB tdb = s.GetClock().ConvertToTDB("1/0000001000:00000");
	ASSERT_DOUBLE_EQ(6.62732200E+08, tdb.GetSecondsFromJ2000().count());
}

TEST(SpacecraftClockKernel, ConvertToClock)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	std::string sclk = s.GetClock().ConvertToClockString(IO::SDK::Time::TDB(6.62732200E+08s));
	ASSERT_STREQ("1/0000001000:00000", sclk.c_str());
}

TEST(SpacecraftClockKernel, ConvertToEncodedClock)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	double encodedClock = s.GetClock().ConvertToEncodedClock(IO::SDK::Time::TDB(6.62732200E+08s)); //T+1000

	//T+1000*resolution = 65536000
	ASSERT_DOUBLE_EQ(1000.0 * 65536, encodedClock);
}

TEST(SpacecraftClockKernel, GetResolution)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	double res = s.GetClock().GetResolution();

	ASSERT_DOUBLE_EQ(16, res); //16 bits
}

TEST(SpacecraftClockKernel, GetSecondsPerTick)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	double res = s.GetClock().GetSecondsPerTick();

	ASSERT_DOUBLE_EQ(1.52587890625e-05, res); //15.259us
}

TEST(SpacecraftClockKernel, GetTicksPerSeconds)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, "mission1", std::move(orbitalParams));
	std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

	double res = s.GetClock().GetTicksPerSeconds();

	ASSERT_DOUBLE_EQ(65536, res);
}