#include <gtest/gtest.h>
#include <Spacecraft.h>
#include <Instrument.h>
#include <filesystem>
#include <Vector3D.h>
#include <CircularInstrumentKernel.h>
#include <FOVShapes.h>
#include <SpiceUsr.h>
#include <DataPoolMonitoring.h>
#include <InvalidArgumentException.h>
#include <CelestialBody.h>
#include <InertialFrames.h>
#include <InstrumentFrameFile.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <GravityForce.h>
#include <Quaternion.h>
#include <StateOrientation.h>
#include <SpiceUsr.h>
#include <Builder.h>

using namespace std::chrono_literals;

TEST(Instrument, Initialization)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{4.0, 5.0, 6.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};

	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 1.5);
	const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(200)};

	ASSERT_TRUE(std::filesystem::exists(filepath));
	ASSERT_GT(std::filesystem::file_size(filepath), 0.0);
	ASSERT_STREQ("sc17_Camera200", instrument->GetFrame()->GetName().c_str());
	ASSERT_EQ(&s, &instrument->GetSpacecraft());
}

TEST(Instrument, Frame)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{4.0, 5.0, 6.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};

	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 1.5);

	auto id = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_sc17_Camera200", 1);
	ASSERT_EQ(-17200, id[0]);

	auto name = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("FRAME_-17200_NAME", 1);
	ASSERT_STREQ("sc17_Camera200", name[0].c_str());

	auto classVal = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CLASS", 1);
	ASSERT_EQ(4, classVal[0]);

	auto classid = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CLASS_ID", 1);
	ASSERT_EQ(-17200, classid[0]);

	auto centerid = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CENTER", 1);
	ASSERT_EQ(-17, centerid[0]);

	auto spec = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_-17200_SPEC", 1);
	ASSERT_STREQ("ANGLES", spec[0].c_str());

	auto relative = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_-17200_RELATIVE", 1);
	ASSERT_STREQ("sc17", relative[0].c_str());

	auto frameAngles = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("TKFRAME_-17200_ANGLES", 3);
	ASSERT_DOUBLE_EQ(orientation.GetX() * -1.0, frameAngles[0]);
	ASSERT_DOUBLE_EQ(orientation.GetY() * -1.0, frameAngles[1]);
	ASSERT_DOUBLE_EQ(orientation.GetZ() * -1.0, frameAngles[2]);

	auto axes = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("TKFRAME_-17200_AXES", 3);
	ASSERT_EQ(1, axes[0]);
	ASSERT_EQ(2, axes[1]);
	ASSERT_EQ(3, axes[2]);
}

TEST(Instrument, CircularKernel)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{4.0, 5.0, 6.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 1.5);

	auto classSpec = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_CLASS_SPEC", 1);
	ASSERT_STREQ("ANGLES", classSpec[0].c_str());

	auto shape = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_SHAPE", 1);
	ASSERT_STREQ("CIRCLE", shape[0].c_str());

	auto frame = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_FRAME", 1);
	ASSERT_STREQ("sc17_Camera200", frame[0].c_str());

	auto boresightKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_BORESIGHT", 3);
	ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
	ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
	ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

	auto fovVectorKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_FOV_REF_VECTOR", 3);
	ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
	ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
	ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

	auto angle = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_FOV_REF_ANGLE", 1);
	ASSERT_DOUBLE_EQ(1.5, angle[0]);

	auto units = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_ANGLE_UNITS", 1);
	ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, RectangularKernel)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera300/Frames/Camera300.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{4.0, 5.0, 6.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddRectangularFOVInstrument(300, "Camera300", orientation, boresight, fovvector, 1.5,IO::SDK::Constants::PI2);

	auto classSpec = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_CLASS_SPEC", 1);
	ASSERT_STREQ("ANGLES", classSpec[0].c_str());

	auto shape = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_SHAPE", 1);
	ASSERT_STREQ("RECTANGLE", shape[0].c_str());

	auto frame = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_FRAME", 1);
	ASSERT_STREQ("sc17_Camera300", frame[0].c_str());

	auto boresightKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_BORESIGHT", 3);
	ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
	ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
	ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

	auto fovVectorKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_FOV_REF_VECTOR", 3);
	ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
	ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
	ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

	auto angle = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_FOV_REF_ANGLE", 1);
	ASSERT_DOUBLE_EQ(1.5, angle[0]);

	auto units = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_ANGLE_UNITS", 1);
	ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, EllipticalKernel)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera400/Frames/Camera400.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{4.0, 5.0, 6.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddEllipticalFOVInstrument(400, "Camera400", orientation, boresight, fovvector, 1.5,IO::SDK::Constants::PI2);

	auto classSpec = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_CLASS_SPEC", 1);
	ASSERT_STREQ("ANGLES", classSpec[0].c_str());

	auto shape = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_SHAPE", 1);
	ASSERT_STREQ("ELLIPSE", shape[0].c_str());

	auto frame = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_FRAME", 1);
	ASSERT_STREQ("sc17_Camera400", frame[0].c_str());

	auto boresightKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_BORESIGHT", 3);
	ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
	ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
	ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

	auto fovVectorKernel = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_FOV_REF_VECTOR", 3);
	ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
	ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
	ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

	auto angle = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_FOV_REF_ANGLE", 1);
	ASSERT_DOUBLE_EQ(1.5, angle[0]);

	auto units = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_ANGLE_UNITS", 1);
	ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, Boundaries)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{0.0, 0.0, 1.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD);

	const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(200)};

	auto boundaries = instrument->GetFOVBoundaries();

	ASSERT_DOUBLE_EQ(0.087155281908263951, boundaries[0].GetX());
	ASSERT_DOUBLE_EQ(0.0, boundaries[0].GetY());
	ASSERT_DOUBLE_EQ(0.99619473840986084, boundaries[0].GetZ());
}

TEST(Instrument, Boresight)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD);

	const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(200)};

	auto boresightRes = instrument->GetBoresight();

	ASSERT_DOUBLE_EQ(boresight.GetX(), boresightRes.GetX());
	ASSERT_DOUBLE_EQ(boresight.GetY(), boresightRes.GetY());
	ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightRes.GetZ());
}

TEST(Instrument, FOVShape)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD);

	const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(200)};

	auto shape = instrument->GetFOVShape();

	ASSERT_EQ(IO::SDK::Instruments::FOVShapeEnum::Circular, shape);
}

TEST(Instrument, GetBadId)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};
	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD);

	//Id doesn't exist
	ASSERT_FALSE(s.GetInstrument(1234));
}

TEST(Instrument, CreateBadId)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};

	//Id must be < 1000
	ASSERT_THROW(s.AddCircularFOVInstrument(1200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Instrument, AlreadyExists)
{
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/sc17_mis1Scn1/Instruments/Camera200/Frames/Camera200.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{0.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{1.0, 2.0, 3.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, "mis1Scn1", std::move(orbitalParams)};

	s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD);
	ASSERT_THROW(s.AddCircularFOVInstrument(200, "Camera200", orientation, boresight, fovvector, 5 * IO::SDK::Constants::DEG_RAD), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Instrument, FindWindowFieldOfView)
{
	
	//========== Configure spacecraft===================
	std::string filepath = std::string(IO::SDK::Parameters::KernelsPath) + "/SC179FOV_MISSFOVTEST/Instruments/CAMERAFOV789/Frames/CAMERAFOV789.tf";
	if (std::filesystem::exists(filepath))
	{
		std::filesystem::remove(filepath);
	}

	IO::SDK::Math::Vector3D orientation{1.0, 0.0, 0.0};
	IO::SDK::Math::Vector3D boresight{0.0, 0.0, 1.0};
	IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	double a = 6800000.0;
	auto v = std::sqrt(earth->GetMu() / a);
	IO::SDK::Time::TDB epoch("2021-JUN-10 00:00:00.0000 TDB");

	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(a, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, v, 0.0), epoch, IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-179, "SC179FOV", 1000.0, 3000.0, "MISSFOVTEST", std::move(orbitalParams)};

	s.AddCircularFOVInstrument(789, "CAMERAFOV789", orientation, boresight, fovvector, 1.5);
	const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(789)};

	//==========PROPAGATOR====================
	auto step{IO::SDK::Time::TimeSpan(1.0s)};
	IO::SDK::Time::TimeSpan duration(6447.0s);

	std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

	IO::SDK::Integrators::Forces::GravityForce gravityForce;
	forces.push_back(&gravityForce);
	IO::SDK::Integrators::VVIntegrator integrator(step, forces);

	IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(epoch, epoch + duration));

	pro.Propagate();

	//=======Orientation==========
	std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> orientationData;
	std::vector<IO::SDK::OrbitalParameters::StateOrientation> interval;
	auto epoch_or = IO::SDK::Time::TDB("2021-JUN-10 00:00:00.0000 TDB");
	auto axis_or = IO::SDK::Math::Vector3D(1.0, 0.0, 0.0);
	auto angularVelocity_or = IO::SDK::Math::Vector3D();

	IO::SDK::Time::TimeSpan ts(10s);

	for (size_t i = 0; i < 646; i++)
	{
		auto q = IO::SDK::Math::Quaternion(axis_or, 0.0);
		IO::SDK::OrbitalParameters::StateOrientation s_or(q, angularVelocity_or, epoch_or, IO::SDK::Frames::InertialFrames::ICRF);
		interval.push_back(s_or);
		epoch_or = epoch_or + ts;
	}

	orientationData.push_back(interval);

	s.WriteOrientations(orientationData);

	//Check frame name and id
	auto id = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_SC179FOV_CAMERAFOV789", 1);
	ASSERT_EQ(-179789, id[0]);

	auto name = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("FRAME_-179789_NAME", 1);
	ASSERT_STREQ("SC179FOV_CAMERAFOV789", name[0].c_str());

	//Get windows
	auto results = instrument->FindWindowsWhereInFieldOfView(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB("2021-JUN-10 00:00:00.0000 TDB"), epoch + duration), *earth, IO::SDK::Time::TimeSpan(60s), IO::SDK::AberrationsEnum::None);

	ASSERT_EQ(2, results.size());
	ASSERT_STREQ("2021-06-10 00:01:13.760767 (TDB)", results[0].GetStartDate().ToString().c_str());
	ASSERT_STREQ("2021-06-10 00:45:16.498288 (TDB)", results[0].GetEndDate().ToString().c_str());

	ASSERT_STREQ("2021-06-10 01:34:14.279066 (TDB)", results[1].GetStartDate().ToString().c_str());
	ASSERT_STREQ("2021-06-10 01:47:27.000000 (TDB)", results[1].GetEndDate().ToString().c_str());
}