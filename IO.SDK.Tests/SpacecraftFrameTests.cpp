#include <gtest/gtest.h>
#include <SpacecraftFrameFile.h>
#include <Spacecraft.h>
#include <filesystem>
#include <DataPoolMonitoring.h>
#include <CelestialBody.h>
#include <memory>
#include <InertialFrames.h>

using namespace std::chrono_literals;

TEST(SpacecraftFrameFile, Initialization)
{
	std::string filepath = std::string(IO::SDK::Parameters::SpacecraftPath) + "/SC17_MIS1SCN1/Frames/SC17.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	ASSERT_TRUE(std::filesystem::exists(filepath));
	ASSERT_GT(std::filesystem::file_size(filepath), 0.0);

	auto id = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_SC17_SPACECRAFT", 1);
	ASSERT_EQ(-17000, id[0]);

	auto name = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("FRAME_-17000_NAME", 1);
	ASSERT_STREQ("SC17_SPACECRAFT", name[0].c_str());

	auto classFrame = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17000_CLASS", 1);
	ASSERT_EQ(3, classFrame[0]);

	auto classId = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17000_CLASS_ID", 1);
	ASSERT_EQ(-17000, classId[0]);

	auto center = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17000_CENTER", 1);
	ASSERT_EQ(-17, center[0]);

	auto clock = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("CK_-17000_SCLK", 1);
	ASSERT_EQ(-17, clock[0]);

	auto ephemeris = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("CK_-17000_SPK", 1);
	ASSERT_EQ(-17, ephemeris[0]);

	auto frameName = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("OBJECT_-17_FRAME", 1);
	ASSERT_STREQ("SC17_SPACECRAFT", frameName[0].c_str());
}