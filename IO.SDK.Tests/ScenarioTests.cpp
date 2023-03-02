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
#include<Scenario.h>
#include<Window.h>
#include<UTC.h>

using namespace std::chrono_literals;

TEST(Scenario, Initialize)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> window(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", window);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    ASSERT_STREQ("scenariotest", scenario.GetName().c_str());
    ASSERT_EQ(window, scenario.GetWindow());
    ASSERT_EQ(1, scenario.GetCelestialBodies().size());
    ASSERT_EQ(1, scenario.GetSites().size());
    ASSERT_EQ(1, scenario.GetSpacecrafts().size());
    ASSERT_EQ(s, *scenario.GetSpacecrafts().front());
    ASSERT_EQ(*earth, *scenario.GetCelestialBodies().front());

    ASSERT_EQ(ls.get(), dynamic_cast<const IO::SDK::Sites::LaunchSite *>(scenario.GetSites().front()));

}

TEST(Scenario, AddDistanceConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> windowUTC(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Time::Window<IO::SDK::Time::TDB> windowTDB(IO::SDK::Time::TDB("2021-06-02T00:00:00"), IO::SDK::Time::TDB("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", windowUTC);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    IO::SDK::Constraints::Parameters::DistanceParameters constraint1(windowTDB, *earth, *sun, IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::AberrationsEnum::None, 10.0,
                                                                     IO::SDK::Time::TimeSpan(3600s));
    scenario.AddDistanceConstraint(constraint1);

    IO::SDK::Constraints::Parameters::DistanceParameters constraint2(windowTDB, *earth, *sun, IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::AberrationsEnum::None, 10.0,
                                                                     IO::SDK::Time::TimeSpan(3600s));
    scenario.AddDistanceConstraint(constraint2);
    auto distanceConstraint = scenario.GetDistanceConstraints();
    ASSERT_EQ(2, distanceConstraint.size());
    ASSERT_EQ(0, constraint1.Order);
    ASSERT_EQ(std::nullopt, distanceConstraint[constraint1]);
    ASSERT_EQ(1, constraint2.Order);
    ASSERT_EQ(std::nullopt, distanceConstraint[constraint2]);
}

TEST(Scenario, AddOccultationConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> windowUTC(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Time::Window<IO::SDK::Time::TDB> windowTDB(IO::SDK::Time::TDB("2021-06-02T00:00:00"), IO::SDK::Time::TDB("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", windowUTC);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    IO::SDK::Constraints::Parameters::OccultationParameters constraint1(windowTDB, s, *earth, *sun, IO::SDK::OccultationType::Full(), IO::SDK::AberrationsEnum::None,
                                                                        IO::SDK::Time::TimeSpan(3600s));
    scenario.AddOccultationConstraint(constraint1);

    IO::SDK::Constraints::Parameters::OccultationParameters constraint2(windowTDB, s, *earth, *sun, IO::SDK::OccultationType::Full(), IO::SDK::AberrationsEnum::None,
                                                                        IO::SDK::Time::TimeSpan(3600s));
    scenario.AddOccultationConstraint(constraint2);
    auto constraints = scenario.GetOccultationConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(0, constraint1.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint1]);
    ASSERT_EQ(1, constraint2.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint2]);
}

TEST(Scenario, AddByDayConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> windowUTC(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Time::Window<IO::SDK::Time::TDB> windowTDB(IO::SDK::Time::TDB("2021-06-02T00:00:00"), IO::SDK::Time::TDB("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", windowUTC);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    IO::SDK::Constraints::Parameters::ByDayParameters constraint1(windowUTC, *ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddDayConstraint(constraint1);

    IO::SDK::Constraints::Parameters::ByDayParameters constraint2(windowUTC, *ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddDayConstraint(constraint2);
    auto constraints = scenario.GetByDaysConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(0, constraint1.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint1]);
    ASSERT_EQ(1, constraint2.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint2]);
}

TEST(Scenario, AddByNightConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> windowUTC(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Time::Window<IO::SDK::Time::TDB> windowTDB(IO::SDK::Time::TDB("2021-06-02T00:00:00"), IO::SDK::Time::TDB("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", windowUTC);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    IO::SDK::Constraints::Parameters::ByNightParameters constraint1(windowUTC, *ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddNightConstraint(constraint1);

    IO::SDK::Constraints::Parameters::ByNightParameters constraint2(windowUTC, *ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddNightConstraint(constraint2);
    auto constraints = scenario.GetByNightConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(0, constraint1.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint1]);
    ASSERT_EQ(1, constraint2.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint2]);
}

TEST(Scenario, AddBodyVisibilityConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, sc, "S3",
                                                           IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                                           earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> windowUTC(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Time::Window<IO::SDK::Time::TDB> windowTDB(IO::SDK::Time::TDB("2021-06-02T00:00:00"), IO::SDK::Time::TDB("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", windowUTC);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(*ls);

    IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters constraint1(windowUTC, *ls, *sun, IO::SDK::AberrationsEnum::None);
    scenario.AddBodyVisibilityConstraint(constraint1);

    IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters constraint2(windowUTC, *ls, *sun, IO::SDK::AberrationsEnum::None);
    scenario.AddBodyVisibilityConstraint(constraint2);
    auto constraints = scenario.GetBodyVisibilityFromSiteConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(0, constraint1.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint1]);
    ASSERT_EQ(1, constraint2.Order);
    ASSERT_EQ(std::nullopt, constraints[constraint2]);
}