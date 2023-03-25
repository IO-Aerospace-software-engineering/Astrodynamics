#include <gtest/gtest.h>
#include <LaunchSite.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include <AzimuthRange.h>
#include <Scenario.h>
#include <Window.h>
#include <UTC.h>

TEST(LaunchSite, AddAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::LaunchSite ls{1, "S1", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto az = IO::SDK::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    auto az2 = IO::SDK::Coordinates::AzimuthRange(1.5, 3.0);
    ASSERT_THROW(ls.AddAzimuthLaunchRange(az2), IO::SDK::Exception::SDKException);
}

TEST(LaunchSite, ClearAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::LaunchSite ls{1, "S1", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto az = IO::SDK::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    ASSERT_TRUE(ls.IsAzimuthLaunchAllowed(1.5));

    ls.ClearAzimuthLaunchRanges();
    ASSERT_FALSE(ls.IsAzimuthLaunchAllowed(1.5));
}

TEST(LaunchSite, IsAzimuthLaunchAllowed)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::LaunchSite ls{1, "S1", IO::SDK::Coordinates::Geodetic(0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto az = IO::SDK::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    ASSERT_TRUE(ls.IsAzimuthLaunchAllowed(1.5));
    ASSERT_FALSE(ls.IsAzimuthLaunchAllowed(2.5));
}