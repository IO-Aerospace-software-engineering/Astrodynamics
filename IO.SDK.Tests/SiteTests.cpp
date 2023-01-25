#include <gtest/gtest.h>
#include <Site.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
using namespace std::chrono_literals;
TEST(Site, GetRADDec)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto radec = s.GetRADec(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-07 12:00:00 UTC"));
    ASSERT_DOUBLE_EQ(44.394214788670517, radec.GetRA() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(16.869593460563181, radec.GetDec() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(1.5096111046738699E+11, radec.GetRange());
}

TEST(Site, Illumination)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
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
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto isDay = s.IsDay(IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_TRUE(isDay);
    isDay = s.IsDay(IO::SDK::Time::TDB("2021-05-17 00:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_FALSE(isDay);
}

TEST(Site, IsNight)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto isNight = s.IsNight(IO::SDK::Time::TDB("2021-05-17 12:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_FALSE(isNight);
    isNight = s.IsNight(IO::SDK::Time::TDB("2021-05-17 00:00:00 UTC"), IO::SDK::Constants::OfficialTwilight);
    ASSERT_TRUE(isNight);
}

TEST(Site, FindDayWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto windows = s.FindDayWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()), IO::SDK::Constants::OfficialTwilight);

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
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};

    auto windows = s.FindNightWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()), IO::SDK::Constants::OfficialTwilight);

    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2021-05-17 19:35:42.885022 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[0].GetEndDate().ToString().c_str());
}

TEST(Site, GetStateVector)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
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
    IO::SDK::Sites::Site s{12945, "FAKE_DSS-13", IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 107.0), earth};
    auto sv = s.GetStateVector(*sun, IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB"));
    auto frm = sv.ToFrame(IO::SDK::Frames::Frames("DSS-13_TOPO"));
    ASSERT_DOUBLE_EQ(151331784302.33798, frm.GetPosition().Magnitude());
    ASSERT_DOUBLE_EQ(10363092.453308845, frm.GetVelocity().Magnitude());
    ASSERT_DOUBLE_EQ(77897211194.850403, frm.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(-127863172415.52254, frm.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(-22007784259.951591, frm.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-5361336.2961583128, frm.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(-4574026.8933693748, frm.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(7597896.8336017765, frm.GetVelocity().GetZ());
}

TEST(Site, GetHorizontalCoordinates)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto marsBarycenter = std::make_shared<IO::SDK::Body::CelestialBody>(4, "mars", sun);

    //Position virtual station on same location as DSS-13 at local noon
    IO::SDK::Sites::Site s{12945, "FAKE_DSS-13", IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 107.0), earth};
    auto hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(151392249055.53369, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(179.02966833518911, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(74.90166914480163, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //SunRise
    hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 12:38:00 UTC"));
    ASSERT_DOUBLE_EQ(151390108051.58334, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(64.234701458460961, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-1.135660001562194, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //SunSet
    hor = s.GetHorizontalCoordinates(*sun, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-21 02:48:00 UTC"));
    ASSERT_DOUBLE_EQ(151406878348.0845, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(295.54578652197779, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-0.66454840555255634, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);

    //Mars
    hor = s.GetHorizontalCoordinates(*marsBarycenter, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB("2021-05-20 19:43:00 UTC"));
    ASSERT_DOUBLE_EQ(3.2514463497228497E+11, hor.GetAltitude());
    ASSERT_DOUBLE_EQ(90.420906294082812, hor.GetAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(44.922034555236579, hor.GetElevation() * IO::SDK::Constants::RAD_DEG);
}

TEST(Site, FindWindowsOnIlluminationConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{2, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto windows = s.FindWindowsOnIlluminationConstraint(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()),*sun,IO::SDK::IlluminationAngle::Incidence(),IO::SDK::Constraint::LowerThan(),IO::SDK::Constants::PI2- IO::SDK::Constants::OfficialTwilight);

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}
