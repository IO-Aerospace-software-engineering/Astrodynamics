/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <SpacecraftClockKernel.h>
#include <filesystem>
#include <Parameters.h>
#include <TDB.h>
#include <Spacecraft.h>
#include <CelestialBody.h>
#include <InertialFrames.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(SpacecraftClockKernel, BuildGenericKernel)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    auto path = s.GetClock().GetPath();
    ASSERT_TRUE(std::filesystem::exists(path));
    ASSERT_GT(std::filesystem::file_size(filepath), 0.0);
}

TEST(SpacecraftClockKernel, GetCoverage)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    const auto window = s.GetClock().GetCoverageWindow();//-1.3569552000000E+09
    ASSERT_DOUBLE_EQ(-1.356955200E+09, window.GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(2.9380120959999084E+09, window.GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(4.2949672959999089E+09, window.GetLength().GetSeconds().count());
}

TEST(SpacecraftClockKernel, ConvertToTDB)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    IO::Astrodynamics::Time::TDB tdb = s.GetClock().ConvertToTDB("1/0000001000:00000");
    ASSERT_DOUBLE_EQ(-1.356954200E+09, tdb.GetSecondsFromJ2000().count());
}

TEST(SpacecraftClockKernel, ConvertToClock)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    std::string sclk = s.GetClock().ConvertToClockString(IO::Astrodynamics::Time::TDB(-1.356954200E+09s));
    ASSERT_STREQ("1/0000001000:00000", sclk.c_str());
}

TEST(SpacecraftClockKernel, ConvertToEncodedClock)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    double encodedClock = s.GetClock().ConvertToEncodedClock(IO::Astrodynamics::Time::TDB(-1.356954200E+09s)); //T0+1000

    //T+1000*resolution = 65536000
    ASSERT_DOUBLE_EQ(1000.0 * 65536, encodedClock);
}

TEST(SpacecraftClockKernel, GetResolution)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    double res = s.GetClock().GetResolution();

    ASSERT_DOUBLE_EQ(16, res); //16 bits
}

TEST(SpacecraftClockKernel, GetSecondsPerTick)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    double res = s.GetClock().GetSecondsPerTick();

    ASSERT_DOUBLE_EQ(1.52587890625e-05, res); //15.259us
}

TEST(SpacecraftClockKernel, GetTicksPerSeconds)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         1.0, 2.0,
                                                                                                                                                                         3.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         4.0, 5.0,
                                                                                                                                                                         6.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s(-456, "sc456", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
    std::string filepath = s.GetFilesPath() + "/Clocks/" + s.GetName() + ".tsc";

    double res = s.GetClock().GetTicksPerSeconds();

    ASSERT_DOUBLE_EQ(65536, res);
}