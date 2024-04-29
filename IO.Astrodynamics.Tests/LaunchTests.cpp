/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <memory>

#include <LaunchSite.h>
#include <Constants.h>
#include <SDKException.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include <Spacecraft.h>
#include <ConicOrbitalElements.h>
#include <Launch.h>
#include <TLE.h>
#include "TestParameters.h"

TEST(Launch, InertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(44.914856362726589, launch.GetInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, InertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(135.0851436372734, launch.GetInertialDescendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, InertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137.0, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(7696.9997304533663, launch.GetInertialInsertionVelocity());
}

TEST(Launch, NonInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(42.675642756467454, launch.GetNonInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, NonInertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(137.32435724353255, launch.GetNonInertialDescendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, NonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(7413.8488305949077, launch.GetNonInertialInsertionVelocity());
}

TEST(Launch, RetrogradeNonInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 110.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(334.35221985722086, launch.GetNonInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, RetrogradeInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 110.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(337.09819280713128, launch.GetInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG);
}

TEST(Launch, RetrogradeNonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 140.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(8056.0460649659399, launch.GetNonInertialInsertionVelocity());
}

TEST(Launch, RetrogradeInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 140.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
                                                                                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_DOUBLE_EQ(7696.9997304533663, launch.GetInertialInsertionVelocity());
}

TEST(Launch, GetLaunchWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 02:47:25.898437 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(135.09414463706975, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(137.34332878766045, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.156305077152, windows[0].GetNonInertialInsertionVelocity());

    //Second launch window
    ASSERT_STREQ("2021-06-02 18:08:23.626972 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[1].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(44.905855362930239, windows[1].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(42.656671212339546, windows[1].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[1].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.156305077152, windows[1].GetNonInertialInsertionVelocity());
}

TEST(Launch, GetLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399003, "S3",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
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

TEST(Launch, GetSouthLaunchSiteLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                         IO::Astrodynamics::Coordinates::Planetodetic(-104 * IO::Astrodynamics::Constants::DEG_RAD, -41.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
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
    ASSERT_STREQ("2021-06-02 15:05:45.288456 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(55.28875223636738, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(53.734282694226756, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9874559875288, windows[0].GetNonInertialInsertionVelocity());
}

TEST(Launch, GetSouthLaunchSiteLaunchWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1", IO::Astrodynamics::Coordinates::Planetodetic(-104.0 * IO::Astrodynamics::Constants::DEG_RAD, -41.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 08:52:54.169921 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(124.71124776363263, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(126.26571730577325, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9874559875288, windows[0].GetNonInertialInsertionVelocity());

    //Second launch window
    ASSERT_STREQ("2021-06-02 15:04:20.562635 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[1].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(55.28875223636738, windows[1].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(53.734282694226756, windows[1].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[1].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9874559875288, windows[1].GetNonInertialInsertionVelocity());
}
