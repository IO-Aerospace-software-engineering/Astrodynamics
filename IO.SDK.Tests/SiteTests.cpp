#include <gtest/gtest.h>
#include <Site.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include <Scenario.h>

using namespace std::chrono_literals;
TEST(Site, GetRADDec)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto radec = s.GetRADec(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-07 12:00:00 UTC"));
    ASSERT_DOUBLE_EQ(44.394214788670517, radec.GetRA() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(16.869593460563181, radec.GetDec() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(1.5096111046738699E+11, radec.GetRange());
}

TEST(Site, Illumination)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto illumination = s.GetIllumination(IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"));
    ASSERT_DOUBLE_EQ(25.566693646305286, illumination.GetIncidence() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(25.566693646305286, illumination.GetEmission() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(0.0, illumination.GetPhaseAngle() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(151295106772.82697, illumination.GetObserverToSurfacePoint().Magnitude());
    ASSERT_EQ(IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"), illumination.GetEpoch());
}

TEST(Site, IsDay)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto isDay = s.IsDay(IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_TRUE(isDay);
    isDay = s.IsDay(IO::SDK::Time::TDB("2021-05-17 00:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_FALSE(isDay);
}

TEST(Site, IsNight)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto isNight = s.IsNight(IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_FALSE(isNight);
    isNight = s.IsNight(IO::SDK::Time::TDB("2021-05-17 00:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_TRUE(isNight);
}

TEST(Site, FindDayWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto windows = s.FindDayWindows(
            IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()),
            IO::SDK::Constants::OfficialTwilight);

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}

TEST(Site, FindNightWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};

    auto windows = s.FindNightWindows(
            IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()),
            IO::SDK::Constants::OfficialTwilight);

    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2021-05-17 19:35:42.885022 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[0].GetEndDate().ToString().c_str());
}

TEST(Site, GetStateVector)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto sv = s.GetStateVector(*sun, IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB"));
    ASSERT_DOUBLE_EQ(81351867346.038025, sv.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(117072193426.44914, sv.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(50747426654.325386, sv.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-24376.494389633765, sv.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(14622.489770348653, sv.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(6410.5574225323635, sv.GetVelocity().GetZ());
}

TEST(Site, ConvertToLocalFrame)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);

    //Position virtual station on same location as DSS-13
    IO::SDK::Sites::Site s{12945, "FAKE_DSS-13",
                           IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 107.0), earth};
    auto sv = s.GetStateVector(*sun, IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB"));
    auto frm = sv.ToFrame(IO::SDK::Frames::Frames("DSS-13_TOPO"));
    ASSERT_DOUBLE_EQ(151331784302.33798, frm.GetPosition().Magnitude());
    ASSERT_DOUBLE_EQ(10363092.453499723, frm.GetVelocity().Magnitude());
    ASSERT_DOUBLE_EQ(77897211260.624298, frm.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(-127863172360.84084, frm.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(-22007784344.838501, frm.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-5361336.2941664271, frm.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(-4574026.9013716206, frm.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(7597896.8304502163, frm.GetVelocity().GetZ());
}

TEST(Site, GetHorizontalCoordinates)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto marsBarycenter = std::make_shared<IO::SDK::Body::CelestialBody>(4, "mars_barycenter", sun);

    //Position virtual station on same location as DSS-13 at local noon
    IO::SDK::Sites::Site s{12945, "FAKE_DSS-13",
                           IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 107.0), earth};
    auto hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(151392145852.4516, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(179.02968336889137, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(74.901674192124005, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //SunRise
    hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 12:38:00 UTC"));
    ASSERT_DOUBLE_EQ(151390110170.21564, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(64.234704330466599, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-1.1357039304856107, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //SunSet
    hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-21 02:48:00 UTC"));
    ASSERT_DOUBLE_EQ(151406879587.91769, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(295.54578894121431, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-0.66459882179102292, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //Mars
    hor = s.GetHorizontalCoordinates(*marsBarycenter, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(3.2514455949044592E+11, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(90.42089340125473, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(44.922024959556737, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);
}

TEST(Site, FindWindowsOnIlluminationConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto windows = s.FindWindowsOnIlluminationConstraint(
            IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()), *sun,
            IO::SDK::IlluminationAngle::Incidence(), IO::SDK::Constraints::Constraint::LowerThan(), IO::SDK::Constants::PI2 - IO::SDK::Constants::OfficialTwilight);

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}

TEST(Site, WriteEphemeris)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{399103,  "S103", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    IO::SDK::Time::TDB startDate("2021-05-17 12:00:00 TDB");
    IO::SDK::Time::TDB endDate("2021-05-17 12:11:00 TDB");
    s.BuildAndWriteEphemeris(IO::SDK::Time::Window<IO::SDK::Time::UTC>(startDate.ToUTC(),endDate.ToUTC()));

    auto windows = s.GetEphemerisCoverageWindow();

    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows.GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 12:11:00.000000 (TDB)", windows.GetEndDate().ToString().c_str());
}

TEST(Site, ReadEphemeris)
{

    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{399102, "S102", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};

    IO::SDK::Time::TDB startDate("2021-05-17 12:00:00 TDB");
    IO::SDK::Time::TDB endDate("2021-05-17 12:11:00 TDB");
    s.BuildAndWriteEphemeris(IO::SDK::Time::Window<IO::SDK::Time::UTC>(startDate.ToUTC(),endDate.ToUTC()));

    auto startEphemeris = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, startDate, *earth);

    ASSERT_DOUBLE_EQ(2340230.0440586265, startEphemeris.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(3584783.2014813963, startEphemeris.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(4712041.2347625419, startEphemeris.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-261.40764269985209, startEphemeris.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(169.93791555082862, startEphemeris.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(0.54401778415063673, startEphemeris.GetVelocity().GetZ());
}

TEST(Site, FindBodyVisibilityWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);

    //Position virtual station on same location as DSS-13 at local noon
    IO::SDK::Sites::Site s{399113, "FK_DSS-13",
                           IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 1070.0), earth};

    IO::SDK::Time::TDB startDate("2023-02-18 00:00:00 TDB");
    IO::SDK::Time::TDB endDate("2023-02-20 02:00:00 TDB");
    s.BuildAndWriteEphemeris(IO::SDK::Time::Window<IO::SDK::Time::UTC>(startDate.ToUTC(),endDate.ToUTC()));

//    auto res = s.GetStateVector(*moon, IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2023-02-19 00:00:00 TDB"));

    auto windows = s.FindBodyVisibilityWindows(*moon, IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2023-02-19 00:00:00 TDB").ToUTC(),
                                                                                                IO::SDK::Time::TDB("2023-02-20 00:00:00 TDB").ToUTC()),
                                               IO::SDK::AberrationsEnum::None);
    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2023-02-19 14:33:27.641498 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2023-02-19 23:58:50.814787 (UTC)", windows[0].GetEndDate().ToString().c_str());
}
