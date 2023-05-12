#include<gtest/gtest.h>

#include<TLE.h>
#include<Scenario.h>
#include <Vectors.h>
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(Scenario, Initialize)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(399003, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth,std::string(SitePath));
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);


    IO::SDK::Time::Window<IO::SDK::Time::UTC> window(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", window);
    scenario.AddCelestialBody(*earth);
    scenario.AttachSpacecraft(s);
    scenario.AddSite(ls);

    ASSERT_STREQ("scenariotest", scenario.GetName().c_str());
    ASSERT_EQ(window, scenario.GetWindow());
    ASSERT_EQ(1, scenario.GetCelestialBodies().size());
    ASSERT_EQ(1, scenario.GetSites().size());
    ASSERT_EQ(s, *scenario.GetSpacecraft());
    ASSERT_EQ(*earth, *scenario.GetCelestialBodies().front());

    ASSERT_EQ(&ls, dynamic_cast<const IO::SDK::Sites::LaunchSite *>(scenario.GetSites().front()));

}
