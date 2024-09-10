/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <memory>

#include <LaunchSite.h>
#include <Constants.h>
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
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth,
                                                   std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(44.914856362426271, launch.GetInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG,
                1E-09);
}

TEST(Launch, InertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(135.08514363757374, launch.GetInertialDescendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG,
                1E-09);
}

TEST(Launch, InertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137.0, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
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
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(42.675642756161572,
                launch.GetNonInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
}

TEST(Launch, NonInertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(137.32435724383842,
                launch.GetNonInertialDescendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
}

TEST(Launch, NonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 51.6494 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(7413.8488305971614, launch.GetNonInertialInsertionVelocity(), 1E-09);
}

TEST(Launch, RetrogradeNonInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 110.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(334.35221985695699,
                launch.GetNonInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG, 1E09);
}

TEST(Launch, RetrogradeInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 110.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(337.09819280685457,
                launch.GetInertialAscendingAzimuthLaunch() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
}

TEST(Launch, RetrogradeNonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 140.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
        IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00"),
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    ASSERT_NEAR(8056.0460649662473, launch.GetNonInertialInsertionVelocity(), 1E-09);
}

TEST(Launch, RetrogradeInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2013-10-14T10:18:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(
        earth, 6728137, 0.0, 140.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, 0.0, 0.0,
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
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {
        "ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
        "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"
    };

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(
        IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 02:46:56.894531 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_NEAR(135.21712769705897, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(137.47092364212625, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714715, windows[0].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7382.8026792258042, windows[0].GetNonInertialInsertionVelocity(), 1E-09);

    //Second launch window
    ASSERT_STREQ("2021-06-02 18:07:09.016727 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[1].GetLaunchSite().GetId());
    ASSERT_NEAR(44.78287230294103, windows[1].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(42.529076357873755, windows[1].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714752, windows[1].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7382.8026792258042, windows[1].GetNonInertialInsertionVelocity(), 1E-09);
}

TEST(Launch, GetLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399003, "S3",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -81.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {
        "ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
        "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"
    };

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, true, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(
        IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 18:06:27.698902 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399003, windows[0].GetLaunchSite().GetId());
    ASSERT_NEAR(44.78287230294103, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(42.529076357873755, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714715, windows[0].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7382.8026792258042, windows[0].GetNonInertialInsertionVelocity(), 1E-09);
}

TEST(Launch, GetSouthLaunchSiteLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -104 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       -41.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {
        "ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
        "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"
    };

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, true, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(
        IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 15:05:37.043783 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_NEAR(55.110840288769204, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(53.549545534168743, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714715, windows[0].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7379.6345491745487, windows[0].GetNonInertialInsertionVelocity(), 1E-09);
}

TEST(Launch, GetSouthLaunchSiteLaunchWindows)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto ls = IO::Astrodynamics::Sites::LaunchSite(399001, "S1",
                                                   IO::Astrodynamics::Coordinates::Planetodetic(
                                                       -104.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                       -41.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                   earth, std::string(SitePath));
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(
        ls.GetStateVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                          IO::Astrodynamics::Time::TDB("2021-06-02T00:00:00")));
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{
        -1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)
    };
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {
        "ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
        "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"
    };

    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::TLE>(earth, tle);
    IO::Astrodynamics::Maneuvers::Launch launch(ls, ls, false, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(
        IO::Astrodynamics::Time::UTC("2021-06-02T00:00:00"), IO::Astrodynamics::Time::UTC("2021-06-03T00:00:00")));

    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 08:51:16.611328 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[0].GetLaunchSite().GetId());
    ASSERT_NEAR(124.8891597112308, windows[0].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(126.45045446583126, windows[0].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714715, windows[0].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7379.6345491745487, windows[0].GetNonInertialInsertionVelocity(), 1E-09);

    //Second launch window
    ASSERT_STREQ("2021-06-02 15:04:13.716152 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(399001, windows[1].GetLaunchSite().GetId());
    ASSERT_NEAR(55.110840288769204, windows[1].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(53.549545534168743, windows[1].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG, 1E-09);
    ASSERT_NEAR(7665.2355903714715, windows[1].GetInertialInsertionVelocity(), 1E-09);
    ASSERT_NEAR(7379.6345491745487, windows[1].GetNonInertialInsertionVelocity(), 1E-09);
}
