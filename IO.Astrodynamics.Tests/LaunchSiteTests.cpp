/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <LaunchSite.h>
#include <Constants.h>
#include <SDKException.h>
#include <StateVector.h>
#include <AzimuthRange.h>
#include "TestParameters.h"

TEST(LaunchSite, AddAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::LaunchSite ls{399001, "S1", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto az = IO::Astrodynamics::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    auto az2 = IO::Astrodynamics::Coordinates::AzimuthRange(1.5, 3.0);
    ASSERT_THROW(ls.AddAzimuthLaunchRange(az2), IO::Astrodynamics::Exception::SDKException);
}

TEST(LaunchSite, ClearAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::LaunchSite ls{399001, "S1", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto az = IO::Astrodynamics::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    ASSERT_TRUE(ls.IsAzimuthLaunchAllowed(1.5));

    ls.ClearAzimuthLaunchRanges();
    ASSERT_FALSE(ls.IsAzimuthLaunchAllowed(1.5));
}

TEST(LaunchSite, IsAzimuthLaunchAllowed)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::LaunchSite ls{399001, "S1", IO::Astrodynamics::Coordinates::Planetodetic(0.0, 45.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath)};
    auto az = IO::Astrodynamics::Coordinates::AzimuthRange(1.0, 2.0);
    ls.AddAzimuthLaunchRange(az);

    ASSERT_TRUE(ls.IsAzimuthLaunchAllowed(1.5));
    ASSERT_FALSE(ls.IsAzimuthLaunchAllowed(2.5));
}