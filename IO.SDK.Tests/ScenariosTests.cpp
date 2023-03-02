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

TEST(Scenarios, ReachOrbitByDay)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));

    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::TLE>(earth, tle);
    IO::SDK::Maneuvers::Launch launch(ls, ls, true, *targetOrbit);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));

    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 18:08:00.980377 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(3, windows[0].GetLaunchSite()->GetId());
    ASSERT_DOUBLE_EQ(44.906290078823638, windows[0].GetInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(42.657119977138009, windows[0].GetNonInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7382.1537527826185, windows[0].GetNonInertialInsertionVelocity());
}