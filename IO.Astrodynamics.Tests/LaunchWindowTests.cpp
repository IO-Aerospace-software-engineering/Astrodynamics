/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <memory>

#include <LaunchSite.h>
#include <Constants.h>
#include <SDKException.h>
#include <StateVector.h>
#include <LaunchWindow.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(LaunchWindow, Initialize)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1", IO::Astrodynamics::Coordinates::Planetodetic(81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                           earth,std::string(SitePath));
    IO::Astrodynamics::Maneuvers::LaunchWindow lw(ls, IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::UTC(10.0s), IO::Astrodynamics::Time::UTC(20.0s)), 1.0, 2.0, 3.0, 4.0);
    ASSERT_EQ(399001, lw.GetLaunchSite().GetId());
    ASSERT_EQ(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::UTC(10.0s), IO::Astrodynamics::Time::UTC(20.0s)), lw.GetWindow());
    ASSERT_DOUBLE_EQ(1.0, lw.GetInertialAzimuth());
    ASSERT_DOUBLE_EQ(2.0, lw.GetNonInertialAzimuth());
    ASSERT_DOUBLE_EQ(3.0, lw.GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(4.0, lw.GetNonInertialInsertionVelocity());
}

