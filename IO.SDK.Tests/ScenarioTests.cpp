#include<gtest/gtest.h>

#include<TLE.h>
#include<Scenario.h>
#include <Vectors.h>
#include <LaunchParameters.h>

using namespace std::chrono_literals;

TEST(Scenario, Initialize)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(), IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};

    IO::SDK::Time::Window<IO::SDK::Time::UTC> window(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-02T00:00:00"));
    IO::SDK::Scenario scenario("scenariotest", window);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    scenario.AddSite(ls);

    ASSERT_STREQ("scenariotest", scenario.GetName().c_str());
    ASSERT_EQ(window, scenario.GetWindow());
    ASSERT_EQ(1, scenario.GetCelestialBodies().size());
    ASSERT_EQ(1, scenario.GetSites().size());
    ASSERT_EQ(1, scenario.GetSpacecrafts().size());
    ASSERT_EQ(s, *scenario.GetSpacecrafts().front());
    ASSERT_EQ(*earth, *scenario.GetCelestialBodies().front());

    ASSERT_EQ(&ls, dynamic_cast<const IO::SDK::Sites::LaunchSite *>(scenario.GetSites().front()));

}

TEST(Scenario, AddDistanceConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
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
    scenario.AddSite(ls);

    IO::SDK::Constraints::Parameters::DistanceParameters constraint1(*earth, *sun, IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::AberrationsEnum::None, 10.0,
                                                                     IO::SDK::Time::TimeSpan(3600s));
    scenario.AddDistanceConstraint(&constraint1);

    IO::SDK::Constraints::Parameters::DistanceParameters constraint2(*earth, *sun, IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::AberrationsEnum::None, 10.0,
                                                                     IO::SDK::Time::TimeSpan(3600s));
    scenario.AddDistanceConstraint(&constraint2);
    auto distanceConstraint = scenario.GetDistanceConstraints();
    ASSERT_EQ(2, distanceConstraint.size());
    ASSERT_EQ(std::nullopt, distanceConstraint[&constraint1]);
    ASSERT_EQ(std::nullopt, distanceConstraint[&constraint2]);
}

TEST(Scenario, AddOccultationConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
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
    scenario.AddSite(ls);

    IO::SDK::Constraints::Parameters::OccultationParameters constraint1(s, *earth, *sun, IO::SDK::OccultationType::Full(), IO::SDK::AberrationsEnum::None,
                                                                        IO::SDK::Time::TimeSpan(3600s));
    scenario.AddOccultationConstraint(&constraint1);

    IO::SDK::Constraints::Parameters::OccultationParameters constraint2(s, *earth, *sun, IO::SDK::OccultationType::Full(), IO::SDK::AberrationsEnum::None,
                                                                        IO::SDK::Time::TimeSpan(3600s));
    scenario.AddOccultationConstraint(&constraint2);
    auto constraints = scenario.GetOccultationConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(std::nullopt, constraints[&constraint1]);
    ASSERT_EQ(std::nullopt, constraints[&constraint2]);
}

TEST(Scenario, AddByDayConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
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
    scenario.AddSite(ls);

    IO::SDK::Constraints::Parameters::ByDayParameters constraint1(ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddDayConstraint(&constraint1);

    IO::SDK::Constraints::Parameters::ByDayParameters constraint2(ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddDayConstraint(&constraint2);
    auto constraints = scenario.GetByDaysConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(std::nullopt, constraints[&constraint1]);
    ASSERT_EQ(std::nullopt, constraints[&constraint2]);
}

TEST(Scenario, AddByNightConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
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
    scenario.AddSite(ls);

    IO::SDK::Constraints::Parameters::ByNightParameters constraint1(ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddNightConstraint(&constraint1);

    IO::SDK::Constraints::Parameters::ByNightParameters constraint2(ls, IO::SDK::Constants::CivilTwilight);
    scenario.AddNightConstraint(&constraint2);
    auto constraints = scenario.GetByNightConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(std::nullopt, constraints[&constraint1]);
    ASSERT_EQ(std::nullopt, constraints[&constraint2]);
}

TEST(Scenario, AddBodyVisibilityConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(3, "S3",
                                         IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
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
    scenario.AddSite(ls);

    IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters constraint1(ls, *sun, IO::SDK::AberrationsEnum::None);
    scenario.AddBodyVisibilityConstraint(&constraint1);

    IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters constraint2(ls, *sun, IO::SDK::AberrationsEnum::None);
    scenario.AddBodyVisibilityConstraint(&constraint2);
    auto constraints = scenario.GetBodyVisibilityFromSiteConstraints();
    ASSERT_EQ(2, constraints.size());
    ASSERT_EQ(std::nullopt, constraints[&constraint1]);
    ASSERT_EQ(std::nullopt, constraints[&constraint2]);
}

TEST(Scenario, FindDistanceConstraint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);

    auto searchWindow = IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2007 JAN 1").ToUTC(), IO::SDK::Time::TDB("2007 APR 1").ToUTC());
    IO::SDK::Scenario scenario("scenarioDistance", searchWindow);
    scenario.AddCelestialBody(*sun);
    scenario.AddCelestialBody(*earth);
    scenario.AddCelestialBody(*moon);

    auto constraint = IO::SDK::Constraints::Parameters::DistanceParameters(*earth, *moon, IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::AberrationsEnum::None,
                                                                           400000000.0, IO::SDK::Time::TimeSpan(86400s));
    scenario.AddDistanceConstraint(&constraint);
    scenario.Execute();

    auto results = scenario.GetDistanceConstraints();
    auto res = *results[&constraint];

    ASSERT_EQ(4, res.size());
    ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", res[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", res[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", res[3].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", res[3].GetEndDate().ToString().c_str());
}

TEST(Scenario, FindBodyVisibilityConstraint)
{
    IO::SDK::Time::Window<IO::SDK::Time::UTC> window(IO::SDK::Time::UTC("2023-02-19T00:00:00"),
                                                     IO::SDK::Time::UTC("2023-02-20T00:00:00"));
    IO::SDK::Scenario scenario("scenarioDistance", window);
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);

    //Position virtual station on same location as DSS-13 at local noon
    IO::SDK::Sites::Site s{399113, "FK_DSS-13",
                           IO::SDK::Coordinates::Geodetic(-116.7944627147624 * IO::SDK::Constants::DEG_RAD, 35.2471635434595 * IO::SDK::Constants::DEG_RAD, 1070.0), earth};

    scenario.AddCelestialBody(*sun);
    scenario.AddCelestialBody(*earth);
    scenario.AddCelestialBody(*moon);

    scenario.AddSite(s);

    auto constraint = IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters(s, *moon, IO::SDK::AberrationsEnum::None);
    scenario.AddBodyVisibilityConstraint(&constraint);
    scenario.Execute();

    auto constraints = scenario.GetBodyVisibilityFromSiteConstraints();
    auto res = *constraints[&constraint];

    ASSERT_EQ(1, res.size());
    ASSERT_STREQ("2023-02-19 14:33:27.641498 (TDB)", res[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2023-02-20 00:00:00.000000 (UTC)", res[0].GetEndDate().ToString().c_str());
}

TEST(Scenario, FindDayWindowsConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};

    sc.AddCelestialBody(*sun);
    sc.AddCelestialBody(*earth);
    sc.AddSite(s);

    auto constraint = IO::SDK::Constraints::Parameters::ByDayParameters(s, IO::SDK::Constants::OfficialTwilight);
    sc.AddDayConstraint(&constraint);
    sc.Execute();

    auto constraints = sc.GetByDaysConstraints();
    auto windows = *constraints[&constraint];

    ASSERT_EQ(2, windows.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", windows[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", windows[1].GetEndDate().ToTDB().ToString().c_str());
}

TEST(Scenario, FindNightWindowsConstraint)
{
    IO::SDK::Scenario sc("scenario1",
                         IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(), IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    IO::SDK::Sites::Site s{333002, "S2", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};

    sc.AddCelestialBody(*sun);
    sc.AddCelestialBody(*earth);
    sc.AddSite(s);

    auto constraint = IO::SDK::Constraints::Parameters::ByNightParameters(s, IO::SDK::Constants::OfficialTwilight);
    sc.AddNightConstraint(&constraint);
    sc.Execute();

    auto constraints = sc.GetByNightConstraints();
    auto windows = *constraints[&constraint];

    ASSERT_EQ(1, windows.size());
    ASSERT_STREQ("2021-05-17 19:35:42.885022 (TDB)", windows[0].GetStartDate().ToTDB().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", windows[0].GetEndDate().ToString().c_str());
}

TEST(Scenario, FindOccultationConstraint)
{
    IO::SDK::Time::Window<IO::SDK::Time::UTC> searchWindow(IO::SDK::Time::TDB("2001 DEC 13").ToUTC(), IO::SDK::Time::TDB("2001 DEC 15").ToUTC());
    IO::SDK::Scenario scenario("scenario", searchWindow);
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);

    scenario.AddCelestialBody(*sun);
    scenario.AddCelestialBody(*earth);
    scenario.AddCelestialBody(*moon);

    auto constraint = IO::SDK::Constraints::Parameters::OccultationParameters(*earth, *moon, *sun, IO::SDK::OccultationType::Any(), IO::SDK::AberrationsEnum::LT,
                                                                              IO::SDK::Time::TimeSpan(240s));
    scenario.AddOccultationConstraint(&constraint);
    scenario.Execute();

    auto constraints = scenario.GetOccultationConstraints();
    auto results = *constraints[&constraint];

    ASSERT_EQ(1, results.size());
    ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", results[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", results[0].GetEndDate().ToString().c_str());
}

TEST(Scenario, FindInFieldOfViewConstraint)
{
    IO::SDK::Scenario scenario("scenario1",
                               IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").ToUTC(),
                                                                         IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").ToUTC()));
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a);
    IO::SDK::Time::TDB epoch("2021-05-17 12:00:00 TDB");

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::SDK::Math::Vector3D(a, 0.0, 0.0),
                                                                                                                                             IO::SDK::Math::Vector3D(0.0, v, 0.0),
                                                                                                                                             epoch,
                                                                                                                                             IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1749, "SC1749", 1000.0, 3000.0, "MISSFOVTEST", std::move(orbitalParams)};

    s.AddCircularFOVInstrument(799, "CAMERA799", IO::SDK::Tests::Zero, IO::SDK::Tests::VectorX.Reverse(), IO::SDK::Tests::VectorZ, IO::SDK::Constants::PI2);
    scenario.AddCelestialBody(*earth);
    scenario.AddSpacecraft(s);
    auto instrument = s.GetInstrument(799);
    auto constraint = IO::SDK::Constraints::Parameters::InFieldOfViewParameters(*instrument, *earth, IO::SDK::AberrationsEnum::None, IO::SDK::Time::TimeSpan(300s));
    scenario.AddInFieldOfViewConstraint(&constraint);
    scenario.Execute();

    auto constraints = scenario.GetInFieldOfViewConstraints();
    auto results = *constraints[&constraint];

    auto res = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), epoch);
    auto orientation = s.GetOrientation(epoch, IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Time::TDB end("2021-05-18 12:00:00 TDB");
    auto res1 = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), end);
    auto orientation1 = s.GetOrientation(end, IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    auto pos = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch, *earth);
    auto pos1 = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch.Add(IO::SDK::Time::TimeSpan(100s)), *earth);
    auto pos2 = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch.Add(IO::SDK::Time::TimeSpan(200s)), *earth);
    auto pos3 = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch.Add(IO::SDK::Time::TimeSpan(300s)), *earth);
    auto pos4 = s.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch.Add(IO::SDK::Time::TimeSpan(400s)), *earth);


    ASSERT_EQ(17, results.size());
    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", results[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 12:06:23.070312 (TDB)", results[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 12:40:07.189748 (TDB)", results[1].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 13:39:23.588611 (TDB)", results[1].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 14:13:07.708047 (TDB)", results[2].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 15:12:24.106910 (TDB)", results[2].GetEndDate().ToString().c_str());

}

TEST(Scenario, FindLaunchWindowConstraint)
{
    IO::SDK::Scenario scenario("scenario1",
                               IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::TDB("2021-06-02T00:00:00").ToUTC(),
                                                                         IO::SDK::Time::TDB("2021-06-03T00:00:00").ToUTC()));
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
    auto ls = IO::SDK::Sites::LaunchSite(1, "S1",
                                         IO::SDK::Coordinates::Geodetic(-104 * IO::SDK::Constants::DEG_RAD, -41.0 * IO::SDK::Constants::DEG_RAD, 0.0),
                                         earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            ls.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-06-02T00:00:00")));
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    std::string tle[3] = {"ISS (ZARYA)", "1 25544U 98067A   21153.20885672  .00000635  00000-0  19731-4 0  9999",
                          "2 25544  51.6454  56.8104 0003459  55.0598  93.6040 15.48940796286274"};


    IO::SDK::OrbitalParameters::TLE targetOrbit(earth, tle);
    IO::SDK::Constraints::Parameters::LaunchParameters launchParameters(ls, ls, true, targetOrbit);
    scenario.AddSite(ls);
    scenario.AddLaunchConstraint(&launchParameters);
    scenario.Execute();

    auto constraints = scenario.GetLaunchConstraints();
    auto windows = *constraints[&launchParameters];

    //Only one launch window
    ASSERT_EQ(1, windows.size());

    //first launch window
    ASSERT_STREQ("2021-06-02 15:06:01.057589 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    ASSERT_EQ(1, windows[0].GetLaunchSite().GetId());
    ASSERT_DOUBLE_EQ(55.289381850887146, windows[0].GetInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(53.734938879897108, windows[0].GetNonInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    ASSERT_DOUBLE_EQ(7378.9855901408182, windows[0].GetNonInertialInsertionVelocity());
}