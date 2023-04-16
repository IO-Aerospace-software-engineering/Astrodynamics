#include <gtest/gtest.h>
#include <memory>

#include <LaunchSite.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include <AzimuthRange.h>
#include <Spacecraft.h>
#include <ConicOrbitalElements.h>
#include <LaunchWindow.h>
#include <Scenario.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(LaunchWindow, Initialize)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(399001, "S1", IO::SDK::Coordinates::Geodetic(81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth,std::string(SitePath));
    IO::SDK::Maneuvers::LaunchWindow lw(ls, IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC(10.0s), IO::SDK::Time::UTC(20.0s)), 1.0, 2.0, 3.0, 4.0);
    ASSERT_EQ(399001, lw.GetLaunchSite().GetId());
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC(10.0s), IO::SDK::Time::UTC(20.0s)), lw.GetWindow());
    ASSERT_DOUBLE_EQ(1.0, lw.GetInertialAzimuth());
    ASSERT_DOUBLE_EQ(2.0, lw.GetNonInertialAzimuth());
    ASSERT_DOUBLE_EQ(3.0, lw.GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(4.0, lw.GetNonInertialInsertionVelocity());
}

