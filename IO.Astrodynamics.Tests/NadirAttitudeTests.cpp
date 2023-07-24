/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <vector>
#include <memory>
#include <chrono>

#include <CelestialBody.h>
#include <StateVector.h>
#include <StateOrientation.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <NadirAttitude.h>
#include <TimeSpan.h>
#include <InertialFrames.h>
#include <TDB.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(NadirAttitude, GetOrientation) {
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::NadirAttitude nadir(engines, prop, IO::Astrodynamics::Time::TimeSpan(10s));
    prop.SetStandbyManeuver(&nadir);

    prop.Propagate();

    auto orientation = s.GetOrientation(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TimeSpan(10s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, nadir.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion().Conjugate());
    ASSERT_EQ(IO::Astrodynamics::Math::Vector3D(-1.0, 2.2204460492503131e-16, 0.0), newVector);
    ASSERT_EQ(IO::Astrodynamics::Time::TimeSpan(60s), s.GetOrientationsCoverageWindow().GetLength());
    auto str=s.GetOrientationsCoverageWindow().GetStartDate().GetSecondsFromJ2000();
    auto str2=IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00").GetSecondsFromJ2000();
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00").GetSecondsFromJ2000().count(), s.GetOrientationsCoverageWindow().GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00").GetSecondsFromJ2000().count(), s.GetOrientationsCoverageWindow().GetEndDate().GetSecondsFromJ2000().count());

}

TEST(NadirAttitude, GetOrientationMinimumEpoch) {
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::NadirAttitude nadir(engines, prop, IO::Astrodynamics::Time::TDB("2021-01-01T13:00:10"), IO::Astrodynamics::Time::TimeSpan(10s));
    prop.SetStandbyManeuver(&nadir);

    prop.Propagate();

    auto orientation = s.GetOrientation(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:10"), IO::Astrodynamics::Time::TimeSpan(10s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, nadir.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion().Conjugate());
    ASSERT_NEAR(-0.99993306467241017, newVector.GetX(), 1E-12);
    ASSERT_NEAR(-0.011570055092428977, newVector.GetY(), 1E-12);
    ASSERT_NEAR(0.0, newVector.GetZ(), 1E-12);
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00").GetSecondsFromJ2000().count(), s.GetOrientationsCoverageWindow().GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00").GetSecondsFromJ2000().count(), s.GetOrientationsCoverageWindow().GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_EQ(IO::Astrodynamics::Time::TimeSpan(60s).GetSeconds().count(), s.GetOrientationsCoverageWindow().GetLength().GetSeconds().count());


}