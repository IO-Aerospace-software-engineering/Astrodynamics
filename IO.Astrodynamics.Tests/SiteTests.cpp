/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <Site.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include "TestParameters.h"

using namespace std::chrono_literals;
TEST(Site, GetRADDec)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto radec = s.GetRADec(*sun, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-07 12:00:00 UTC"));
    ASSERT_DOUBLE_EQ(44.394212434542645, radec.GetRA() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(16.869593416434938, radec.GetDec() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(150961110592.54434, radec.GetRange());
}

TEST(Site, Illumination)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto illumination = s.GetIllumination(IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 UTC"));
    ASSERT_DOUBLE_EQ(25.568976251339219, illumination.GetIncidence() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(25.568976251339219, illumination.GetEmission() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(0.0, illumination.GetPhaseAngle() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(151295106882.38199, illumination.GetObserverToSurfacePoint().Magnitude());
    ASSERT_EQ(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 UTC"), illumination.GetEpoch());
}

TEST(Site, IsDay)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto isDay = s.IsDay(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 UTC"), IO::Astrodynamics::Constants::OfficialTwilight);
    ASSERT_TRUE(isDay);
    isDay = s.IsDay(IO::Astrodynamics::Time::TDB("2021-05-17 00:00:00 UTC"), IO::Astrodynamics::Constants::OfficialTwilight);
    ASSERT_FALSE(isDay);
}

TEST(Site, IsNight)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto isNight = s.IsNight(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 UTC"), IO::Astrodynamics::Constants::OfficialTwilight);
    ASSERT_FALSE(isNight);
    isNight = s.IsNight(IO::Astrodynamics::Time::TDB("2021-05-17 00:00:00 UTC"), IO::Astrodynamics::Constants::OfficialTwilight);
    ASSERT_TRUE(isNight);
}

TEST(Site, FindDayWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto windows = s.FindDayWindows(
            IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()),
            IO::Astrodynamics::Constants::OfficialTwilight);

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:15.723625 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:23.258548 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}

TEST(Site, FindNightWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};

    auto windows = s.FindNightWindows(
            IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()),
            IO::Astrodynamics::Constants::OfficialTwilight);

    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2021-05-17 19:35:24.908834 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:23.258548 (UTC)", windows[0].GetEndDate().ToString().c_str());
}

TEST(Site, GetStateVector)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto sv = s.GetStateVector(*sun, IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB"));
    ASSERT_DOUBLE_EQ(81351872154.581024, sv.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(117072190462.72804, sv.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(50747426612.422821, sv.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-24376.282783934152, sv.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(14622.828661739692, sv.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(6410.5682033023377, sv.GetVelocity().GetZ());
}

TEST(Site, ConvertToLocalFrame)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);

    //Position virtual station on same location as DSS-13
    IO::Astrodynamics::Sites::Site s{399213, "FAKE_DSS-13",
                                     IO::Astrodynamics::Coordinates::Planetodetic(-116.7944627147624 * IO::Astrodynamics::Constants::DEG_RAD, 35.2471635434595 * IO::Astrodynamics::Constants::DEG_RAD, 107.0), earth,
                                     std::string(SitePath)};
    auto sv = s.GetStateVector(*sun, IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB"));
    auto frm = sv.ToFrame(IO::Astrodynamics::Frames::Frames("DSS-13_TOPO"));
    ASSERT_DOUBLE_EQ(151331778648.73926, frm.GetPosition().Magnitude());
    ASSERT_DOUBLE_EQ(10363092.455799565, frm.GetVelocity().Magnitude());
    ASSERT_DOUBLE_EQ(77897211073.824677, frm.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(-127863165782.66985, frm.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(-22007784348.827862, frm.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-5361336.3050836818, frm.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(-4574026.8938565897, frm.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(7597896.8304076176, frm.GetVelocity().GetZ());
}

TEST(Site, GetHorizontalCoordinates)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto marsBarycenter = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(4, sun);

    //Position virtual station on same location as DSS-13 at local noon
    IO::Astrodynamics::Sites::Site s{399013, "FAKE_DSS-13",
                                     IO::Astrodynamics::Coordinates::Planetodetic(-116.7944627147624 * IO::Astrodynamics::Constants::DEG_RAD, 35.2471635434595 * IO::Astrodynamics::Constants::DEG_RAD, 107.0), earth,
                                     std::string(SitePath)};
    auto hor = s.GetHorizontalCoordinates(*sun, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(151392145840.51752, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(179.29648368392296, hor.GetAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(74.902071908623157, hor.GetElevation() * IO::Astrodynamics::Constants::RAD_DEG);

    //SunRise
    hor = s.GetHorizontalCoordinates(*sun, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-20 12:38:00 UTC"));
    ASSERT_DOUBLE_EQ(151390104028.21216, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(64.278334038627449, hor.GetAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-1.0814907937079876, hor.GetElevation() * IO::Astrodynamics::Constants::RAD_DEG);

    //SunSet
    hor = s.GetHorizontalCoordinates(*sun, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-21 02:48:00 UTC"));
    ASSERT_DOUBLE_EQ(151406885786.61737, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(295.58861851368368, hor.GetAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-0.71930879481469068, hor.GetElevation() * IO::Astrodynamics::Constants::RAD_DEG);

    //Mars
    hor = s.GetHorizontalCoordinates(*marsBarycenter, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(325144554599.82349, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(90.462537951785677, hor.GetAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(44.983020083563815, hor.GetElevation() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Site, FindWindowsOnIlluminationConstraint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{333002, "S2", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto windows = s.FindWindowsOnIlluminationConstraint(
            IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()), *sun,
            IO::Astrodynamics::IlluminationAngle::Incidence(), IO::Astrodynamics::Constraints::RelationalOperator::LowerThan(), IO::Astrodynamics::Constants::PI2 - IO::Astrodynamics::Constants::OfficialTwilight);

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:15.723625 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:23.258548 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}

TEST(Site, WriteEphemeris)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{399103, "S103", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth,
                                     std::string(SitePath)};
    IO::Astrodynamics::Time::TDB startDate("2021-05-17 12:00:00 TDB");
    IO::Astrodynamics::Time::TDB endDate("2021-05-17 12:11:00 TDB");
    s.BuildAndWriteEphemeris(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(startDate, endDate));

    auto windows = s.GetEphemerisCoverageWindow();

    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows.GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 12:11:00.000000 (TDB)", windows.GetEndDate().ToString().c_str());
}

TEST(Site, ReadEphemeris)
{

    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{399102, "S102", IO::Astrodynamics::Coordinates::Planetodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth,
                                     std::string(SitePath)};

    IO::Astrodynamics::Time::TDB startDate("2021-05-17 12:00:00 TDB");
    IO::Astrodynamics::Time::TDB endDate("2021-05-17 12:11:00 TDB");
    s.BuildAndWriteEphemeris(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(startDate, endDate));

    auto startEphemeris = s.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, startDate, *earth);

    ASSERT_DOUBLE_EQ(2335472.0057788552, startEphemeris.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(3587825.5088150194, startEphemeris.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(4712086.4261343824, startEphemeris.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-261.62502564594365, startEphemeris.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(169.60274546540077, startEphemeris.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(0.5332811485616662, startEphemeris.GetVelocity().GetZ());
}

TEST(Site, FindBodyVisibilityWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    //Position virtual station on same location as DSS-13 at local noon
    IO::Astrodynamics::Sites::Site s{399113, "FK_DSS-13",
                                     IO::Astrodynamics::Coordinates::Planetodetic(-116.7944627147624 * IO::Astrodynamics::Constants::DEG_RAD, 35.2471635434595 * IO::Astrodynamics::Constants::DEG_RAD, 1070.0), earth,
                                     std::string(SitePath)};

    IO::Astrodynamics::Time::TDB startDate("2023-02-18 00:00:00 TDB");
    IO::Astrodynamics::Time::TDB endDate("2023-02-20 02:00:00 TDB");
    s.BuildAndWriteEphemeris(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(startDate, endDate));

//    auto res = s.ToStateVector(*moon, IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2023-02-19 00:00:00 TDB"));

    auto windows = s.FindBodyVisibilityWindows(*moon, IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::TDB("2023-02-19 00:00:00 TDB").ToUTC(),
                                                                                                IO::Astrodynamics::Time::TDB("2023-02-20 00:00:00 TDB").ToUTC()),
                                               IO::Astrodynamics::AberrationsEnum::None);
    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2023-02-19 14:33:08.921284 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2023-02-19 23:58:50.814787 (UTC)", windows[0].GetEndDate().ToString().c_str());
}
