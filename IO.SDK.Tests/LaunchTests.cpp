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
#include <Launch.h>
#include <TLE.h>

TEST(Launch, InertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(44.912872404793241, launch.GetInertialAscendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, InertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(180.0 - 44.912872404793241, launch.GetInertialDescendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, InertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137.0, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(7696.9997304533663, launch.GetInertialInsertionVelocity());
}

TEST(Launch, NonInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(42.673582338286877, launch.GetNonInertialAscendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, NonInertialDescendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(180.0 - 42.673582338286877, launch.GetNonInertialDescendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, NonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 51.6494 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(7413.8589742455188, launch.GetNonInertialInsertionVelocity());
}

TEST(Launch, RetrogradeNonInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 110.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(334.35043076072935, launch.GetNonInertialAscendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, RetrogradeInertialAscendingAzimuth)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 110.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(337.09636518334463, launch.GetInertialAscendingAzimuthLaunch() * IO::SDK::Constants::RAD_DEG);
}

TEST(Launch, RetrogradeNonInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 140.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(8056.0542722844666, launch.GetNonInertialInsertionVelocity());
}

TEST(Launch, RetrogradeInertialInsertionVelocity)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2013-10-14T10:18:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6728137, 0.0, 140.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, 0.0, IO::SDK::Time::TDB("2013-10-14T10:18:00"), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    ASSERT_DOUBLE_EQ(7696.9997304533663, launch.GetInertialInsertionVelocity());
}

TEST(Launch, GetLaunchWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999", "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::TLE>(earth, tle);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));
    
    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 02:47:43.037109 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[0].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(135.09370992117638, windows[0].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(137.34288002286198, windows[0].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.1537527826185, windows[0].GetNonInertialInsertionVelocity());

    //Second launch window
    ASSERT_STREQ("2021-06-02 18:08:40.928101 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[1].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(44.906290078823638, windows[1].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(42.657119977138009, windows[1].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[1].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.1537527826185, windows[1].GetNonInertialInsertionVelocity());
}

TEST(Launch, GetLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, "S3", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999", "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::TLE>(earth, tle);
    IO::SDK::Maneuvers::Launch launch(ls, ls, true, *targetOrbit, s);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));
    
    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 18:08:00.980377 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(3, windows[0].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(44.906290078823638, windows[0].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(42.657119977138009, windows[0].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.1537527826185, windows[0].GetNonInertialInsertionVelocity());    
}

TEST(Launch, GetSouthLaunchSiteLaunchWindowsByDay)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-104 * IO::SDK::Constants::DEG_RAD, -41.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999", "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::TLE>(earth, tle);
    IO::SDK::Maneuvers::Launch launch(ls, ls, true, *targetOrbit, s);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));
    
    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 15:06:01.057589 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[0].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(55.289381850887146, windows[0].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(53.734938879897108, windows[0].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9855901408182, windows[0].GetNonInertialInsertionVelocity());    
}

TEST(Launch, GetSouthLaunchSiteLaunchWindows)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(1, "S1", IO::SDK::Coordinates::Geodetic(-104.0 * IO::SDK::Constants::DEG_RAD, -41.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(ls->GetStateVector(IO::SDK::Frames::InertialFrames::ICRF, IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999", "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::TLE>(earth, tle);
    IO::SDK::Maneuvers::Launch launch(ls, ls, false, *targetOrbit, s);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));
    
    //Two launch windows
    ASSERT_EQ(2, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 08:53:12.626953 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[0].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(124.71061814911286, windows[0].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(126.2650611201029, windows[0].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9855901408182, windows[0].GetNonInertialInsertionVelocity());

    //Second launch window
    ASSERT_STREQ("2021-06-02 15:04:38.698620 (UTC)", windows[1].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[1].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(55.289381850887146, windows[1].GetInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(53.734938879897108, windows[1].GetNonInertialAzimuth()*IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[1].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9855901408182, windows[1].GetNonInertialInsertionVelocity());
}