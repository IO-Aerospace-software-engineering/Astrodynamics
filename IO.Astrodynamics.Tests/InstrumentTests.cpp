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
#include "TestParameters.h"
#include "Constants.h"

using namespace std::chrono_literals;

TEST(Instrument, Initialization)
{
    std::string filepath = std::string(SpacecraftPath) + "/sc17/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{4.0, 5.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 1.5);
    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};

    ASSERT_TRUE(std::filesystem::exists(filepath));
    ASSERT_GT(std::filesystem::file_size(filepath), 0.0);
    ASSERT_STREQ("SC17_CAMERA200", instrument->GetFrame()->GetName().c_str());
    ASSERT_EQ(&s, &instrument->GetSpacecraft());
}

TEST(Instrument, Frame)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{4.0, 5.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17200, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 1.5);

    auto id = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_SC17_CAMERA200", 1);
    ASSERT_EQ(-17200, id[0]);

    auto name = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("FRAME_-17200_NAME", 1);
    ASSERT_STREQ("SC17_CAMERA200", name[0].c_str());

    auto classVal = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CLASS", 1);
    ASSERT_EQ(4, classVal[0]);

    auto classid = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CLASS_ID", 1);
    ASSERT_EQ(-17200, classid[0]);

    auto centerid = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_-17200_CENTER", 1);
    ASSERT_EQ(-17200, centerid[0]);

    auto spec = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_-17200_SPEC", 1);
    ASSERT_STREQ("ANGLES", spec[0].c_str());

    auto relative = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_-17200_RELATIVE", 1);
    ASSERT_STREQ("SC17_SPACECRAFT", relative[0].c_str());

    auto frameAngles = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("TKFRAME_-17200_ANGLES", 3);
    ASSERT_DOUBLE_EQ(orientation.GetX() * -1.0, frameAngles[0]);
    ASSERT_DOUBLE_EQ(orientation.GetY() * -1.0, frameAngles[1]);
    ASSERT_DOUBLE_EQ(orientation.GetZ() * -1.0, frameAngles[2]);

    auto axes = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("TKFRAME_-17200_AXES", 3);
    ASSERT_EQ(1, axes[0]);
    ASSERT_EQ(2, axes[1]);
    ASSERT_EQ(3, axes[2]);
}

TEST(Instrument, CircularKernel)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{4.0, 5.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 1.5);

    auto classSpec = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_CLASS_SPEC", 1);
    ASSERT_STREQ("ANGLES", classSpec[0].c_str());

    auto shape = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_SHAPE", 1);
    ASSERT_STREQ("CIRCLE", shape[0].c_str());

    auto frame = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_FRAME", 1);
    ASSERT_STREQ("SC17_CAMERA200", frame[0].c_str());

    auto boresightKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_BORESIGHT", 3);
    ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
    ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
    ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

    auto fovVectorKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_FOV_REF_VECTOR", 3);
    ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
    ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
    ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

    auto angle = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17200_FOV_REF_ANGLE", 1);
    ASSERT_DOUBLE_EQ(1.5 * 0.5, angle[0]);

    auto units = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17200_FOV_ANGLE_UNITS", 1);
    ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, RectangularKernel)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA300/Frames/CAMERA300.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{4.0, 5.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddRectangularFOVInstrument(-17300, "Camera300", orientation, boresight, fovvector, 1.5, IO::Astrodynamics::Constants::PI2);

    auto classSpec = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_CLASS_SPEC", 1);
    ASSERT_STREQ("ANGLES", classSpec[0].c_str());

    auto shape = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_SHAPE", 1);
    ASSERT_STREQ("RECTANGLE", shape[0].c_str());

    auto frame = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_FRAME", 1);
    ASSERT_STREQ("SC17_CAMERA300", frame[0].c_str());

    auto boresightKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_BORESIGHT", 3);
    ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
    ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
    ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

    auto fovVectorKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_FOV_REF_VECTOR", 3);
    ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
    ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
    ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

    auto angle = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17300_FOV_REF_ANGLE", 1);
    ASSERT_DOUBLE_EQ(1.5, angle[0]);

    auto units = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17300_FOV_ANGLE_UNITS", 1);
    ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, EllipticalKernel)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA400/Frames/CAMERA400.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{4.0, 5.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddEllipticalFOVInstrument(-17400, "Camera400", orientation, boresight, fovvector, 1.5, IO::Astrodynamics::Constants::PI2);

    auto classSpec = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_CLASS_SPEC", 1);
    ASSERT_STREQ("ANGLES", classSpec[0].c_str());

    auto shape = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_SHAPE", 1);
    ASSERT_STREQ("ELLIPSE", shape[0].c_str());

    auto frame = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_FRAME", 1);
    ASSERT_STREQ("SC17_CAMERA400", frame[0].c_str());

    auto boresightKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_BORESIGHT", 3);
    ASSERT_DOUBLE_EQ(boresight.GetX(), boresightKernel[0]);
    ASSERT_DOUBLE_EQ(boresight.GetY(), boresightKernel[1]);
    ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightKernel[2]);

    auto fovVectorKernel = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_FOV_REF_VECTOR", 3);
    ASSERT_DOUBLE_EQ(fovvector.GetX(), fovVectorKernel[0]);
    ASSERT_DOUBLE_EQ(fovvector.GetY(), fovVectorKernel[1]);
    ASSERT_DOUBLE_EQ(fovvector.GetZ(), fovVectorKernel[2]);

    auto angle = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("INS-17400_FOV_REF_ANGLE", 1);
    ASSERT_DOUBLE_EQ(1.5, angle[0]);

    auto units = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("INS-17400_FOV_ANGLE_UNITS", 1);
    ASSERT_STREQ("RADIANS", units[0].c_str());
}

TEST(Instrument, Boundaries)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD);

    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};

    auto boundaries = instrument->GetFOVBoundaries();

    ASSERT_DOUBLE_EQ(0.043619156285622802, boundaries[0].GetX());
    ASSERT_DOUBLE_EQ(0.0, boundaries[0].GetY());
    ASSERT_DOUBLE_EQ(0.99904823167098911, boundaries[0].GetZ());
}

TEST(Instrument, Boresight)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD);

    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};

    auto boresightRes = instrument->GetBoresight();

    ASSERT_DOUBLE_EQ(boresight.GetX(), boresightRes.GetX());
    ASSERT_DOUBLE_EQ(boresight.GetY(), boresightRes.GetY());
    ASSERT_DOUBLE_EQ(boresight.GetZ(), boresightRes.GetZ());
}

TEST(Instrument, FOVShape)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD);

    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};

    auto shape = instrument->GetFOVShape();

    ASSERT_EQ(IO::Astrodynamics::Instruments::FOVShapeEnum::Circular, shape);
}

TEST(Instrument, GetBadId)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD);

    //Id doesn't exist
    ASSERT_FALSE(s.GetInstrument(1234));
}

TEST(Instrument, CreateBadId)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};


    ASSERT_THROW(s.AddCircularFOVInstrument(1200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Instrument, AlreadyExists)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{1.0, 2.0, 3.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD);
    ASSERT_THROW(s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 5 * IO::Astrodynamics::Constants::DEG_RAD), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Instrument, FindWindowFieldOfView)
{

    //========== Configure Spacecraft===================
    std::string filepath = std::string(SpacecraftPath) + "/SC179_MISSFOVTEST/Instruments/CAMERA789/Frames/CAMERA789.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{1.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a);
    IO::Astrodynamics::Time::TDB epoch("2021-JUN-10 00:00:00.0000 TDB");

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(a, 0.0, 0.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(0.0, v, 0.0),
                                                                                                                                             epoch,
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-179, "SC179", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-179789, "CAMERA789", orientation, boresight, fovvector, 1.5);
    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-179789)};

    //==========PROPAGATOR====================
    auto step{IO::Astrodynamics::Time::TimeSpan(1.0s)};
    IO::Astrodynamics::Time::TimeSpan duration(6447.0s);

    std::vector<IO::Astrodynamics::Integrators::Forces::Force *> forces{};

    IO::Astrodynamics::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::Astrodynamics::Integrators::VVIntegrator integrator(step, forces);

    IO::Astrodynamics::Propagators::Propagator pro(s, integrator, IO::Astrodynamics::Time::Window(epoch, epoch + duration));

    pro.Propagate();

    //=======Orientation==========
    std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> orientationData;
    std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation> interval;
    auto epoch_or = IO::Astrodynamics::Time::TDB("2021-JUN-10 00:00:00.0000 TDB");
    auto axis_or = IO::Astrodynamics::Math::Vector3D(1.0, 0.0, 0.0);
    auto angularVelocity_or = IO::Astrodynamics::Math::Vector3D();

    IO::Astrodynamics::Time::TimeSpan ts(10s);

    auto q = IO::Astrodynamics::Math::Quaternion(axis_or, 0.0);
    for (size_t i = 0; i < 646; i++)
    {
        IO::Astrodynamics::OrbitalParameters::StateOrientation s_or(q, angularVelocity_or, epoch_or, IO::Astrodynamics::Frames::InertialFrames::GetICRF());
        interval.push_back(s_or);
        epoch_or = epoch_or + ts;
    }

    orientationData.push_back(interval);

    s.WriteOrientations(orientationData);

    //Check frame name and id
    auto id = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_SC179_CAMERA789", 1);
    ASSERT_EQ(-179789, id[0]);

    auto name = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("FRAME_-179789_NAME", 1);
    ASSERT_STREQ("SC179_CAMERA789", name[0].c_str());

    //Get windows
    auto results = instrument->FindWindowsWhereInFieldOfView(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(IO::Astrodynamics::Time::TDB("2021-JUN-10 00:00:00.0000 TDB"), epoch + duration),
                                                             *earth, IO::Astrodynamics::AberrationsEnum::LT, IO::Astrodynamics::Time::TimeSpan(60s));

    ASSERT_EQ(2, results.size());
    ASSERT_STREQ("2021-06-10 00:00:00.000000 (TDB)", results[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-06-10 00:53:32.872199 (TDB)", results[0].GetEndDate().ToString().c_str());

    ASSERT_STREQ("2021-06-10 01:25:58.343786 (TDB)", results[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-06-10 01:47:27.000000 (TDB)", results[1].GetEndDate().ToString().c_str());
}

TEST(Instrument, GetBoresightAtEpoch)
{

    //========== Configure Spacecraft===================
    std::string filepath = std::string(SpacecraftPath) + "/SC179_MISSFOVTEST/Instruments/CAMERA789/Frames/CAMERA789.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{IO::Astrodynamics::Constants::PI2, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a); //Compute velocity for a circular orbit
    IO::Astrodynamics::Time::TDB epoch("2021-JUN-10 00:00:00.0000 TDB");

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(a, 0.0, 0.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(0.0, v, 0.0),
                                                                                                                                             epoch,
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-179, "SC179", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-179789, "CAMERA789", orientation, boresight, fovvector, 1.5);
    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-179789)};

    //==========PROPAGATOR====================
    auto step{IO::Astrodynamics::Time::TimeSpan(1.0s)};
    IO::Astrodynamics::Time::TimeSpan duration(6447.0s);

    std::vector<IO::Astrodynamics::Integrators::Forces::Force *> forces{};

    IO::Astrodynamics::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::Astrodynamics::Integrators::VVIntegrator integrator(step, forces);

    IO::Astrodynamics::Propagators::Propagator pro(s, integrator, IO::Astrodynamics::Time::Window(epoch, epoch + duration));

    pro.Propagate();

    //=======Orientation==========
    std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> orientationData;
    std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation> interval;
    auto epoch_or = IO::Astrodynamics::Time::TDB("2021-JUN-10 00:00:00.0000 TDB");
    auto axis_or = IO::Astrodynamics::Math::Vector3D(1.0, 0.0, 0.0);
    auto angularVelocity_or = IO::Astrodynamics::Math::Vector3D();

    IO::Astrodynamics::Time::TimeSpan ts(10s);

    for (size_t i = 0; i < 646; i++)
    {
        auto q = IO::Astrodynamics::Math::Quaternion(axis_or, 0.0);
        IO::Astrodynamics::OrbitalParameters::StateOrientation s_or(q, angularVelocity_or, epoch_or, IO::Astrodynamics::Frames::InertialFrames::GetICRF());
        interval.push_back(s_or);
        epoch_or = epoch_or + ts;
    }

    orientationData.push_back(interval);

    s.WriteOrientations(orientationData);

    auto results = instrument->GetBoresight(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Time::TDB("2021-JUN-10 00:00:00.0000 TDB"));

    ASSERT_NEAR(0.0, results.GetX(), 1E-07);
    ASSERT_NEAR(-1.0, results.GetY(), 1E-07);
    ASSERT_NEAR(0.0, results.GetZ(), 1E-07);
}

TEST(Instrument, GetBoresightInSpacecraftFrame)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{0.0, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{0.0, 1.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 1.5);
    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};
    ASSERT_EQ(s.Top, instrument->GetBoresightInSpacecraftFrame());
}

TEST(Instrument, GetBoresightInSpacecraftFrame2)
{
    std::string filepath = std::string(SpacecraftPath) + "/SC17_MIS1SCN1/Instruments/CAMERA200/Frames/CAMERA200.tf";
    if (std::filesystem::exists(filepath))
    {
        std::filesystem::remove(filepath);
    }

    IO::Astrodynamics::Math::Vector3D orientation{IO::Astrodynamics::Constants::PI2, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{0.0, 1.0, 6.0};

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-17, "sc17", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(-17200, "Camera200", orientation, boresight, fovvector, 1.5);
    const IO::Astrodynamics::Instruments::Instrument *instrument{s.GetInstrument(-17200)};
    auto vRes{instrument->GetBoresightInSpacecraftFrame()};
    ASSERT_NEAR(s.Back.GetX(), vRes.GetX(), 1E-09);
    ASSERT_NEAR(s.Back.GetY(), vRes.GetY(), 1E-07);
    ASSERT_NEAR(s.Back.GetZ(), vRes.GetZ(), 1E-07);
}