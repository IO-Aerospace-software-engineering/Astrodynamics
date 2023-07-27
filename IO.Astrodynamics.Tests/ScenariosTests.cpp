/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include<gtest/gtest.h>
#include<memory>
#include<string>

#include<CelestialBody.h>
#include<LaunchSite.h>
#include<OrbitalParameters.h>
#include<Spacecraft.h>
#include<StateVector.h>
#include<TLE.h>
#include<Launch.h>
#include<LaunchWindow.h>
#include<Window.h>
#include<UTC.h>
#include "InertialFrames.h"
#include "TestParameters.h"

TEST(Scenarios, ReachOrbitByDay)
{


    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399003, "S3",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, true, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 18:07:44.336128 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399003, windows[0].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(44.905855362930239, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(42.656671212339546, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.156305077152, windows[0].GetNonInertialInsertionVelocity());
}