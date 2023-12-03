/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <vector>
#include <chrono>

#include <gtest/gtest.h>
#include <CombinedManeuver.h>
#include <Spacecraft.h>
#include <Engine.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <TimeSpan.h>
#include <TDB.h>
#include <OrbitalParameters.h>
#include <StateVector.h>
#include <ConicOrbitalElements.h>
#include <EquinoctialElements.h>
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(CombinedManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                                                                                                           10000000.0,
                                                                                                                                                                           0.333333,
                                                                                                                                                                           10.0 *
                                                                                                                                                                           IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                                           0.0, 0.0,
                                                                                                                                                                           0.0,
                                                                                                                                                                           IO::Astrodynamics::Time::TDB(
                                                                                                                                                                                   0.0s),
                                                                                                                                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine *>(engine1));

    IO::Astrodynamics::Maneuvers::CombinedManeuver maneuver(engines, prop, 20.0 * IO::Astrodynamics::Constants::DEG_RAD, 12000000.0);

    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(178.0 * IO::Astrodynamics::Constants::DEG_RAD)));
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::Astrodynamics::Constants::DEG_RAD)));
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(180.000001 * IO::Astrodynamics::Constants::DEG_RAD)));
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(182.0 * IO::Astrodynamics::Constants::DEG_RAD)));
}

TEST(CombinedManeuver, TryExecuteWithPeregeeHigherThanApogee)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                                                                                                           6678000.0,
                                                                                                                                                                           0.726546824,
                                                                                                                                                                           28.5 *
                                                                                                                                                                           IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                                           0.0, 0.0,
                                                                                                                                                                           0.0,
                                                                                                                                                                           IO::Astrodynamics::Time::TDB(
                                                                                                                                                                                   0.0s),
                                                                                                                                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 10000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 9000.0, 9000.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine *>(engine1));

    IO::Astrodynamics::Maneuvers::CombinedManeuver maneuver(engines, prop, 0.0, 42164000.0);

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(178.0 * IO::Astrodynamics::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::Astrodynamics::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::Astrodynamics::Constants::DEG_RAD));

    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(180.01 * IO::Astrodynamics::Constants::DEG_RAD));
    ASSERT_TRUE(res.IsValid());
    ASSERT_NEAR(1830.234408755432, maneuver.GetDeltaV().Magnitude(), 1E-06);
    ASSERT_NEAR(1.3018941319074089, maneuver.GetDeltaV().GetX(), 1E-06);
    ASSERT_NEAR(-1661.679088355801, maneuver.GetDeltaV().GetY(), 1E-06);
    ASSERT_NEAR(767.18896198071627, maneuver.GetDeltaV().GetZ(), 1E-06);
}

TEST(CombinedManeuver, TryExecuteWithPeregeeLowerThanApogee)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                                                                                                           6678000.0,
                                                                                                                                                                           0.7266,
                                                                                                                                                                           28.5 *
                                                                                                                                                                           IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                                           0.0, 0.0,
                                                                                                                                                                           0.0,
                                                                                                                                                                           IO::Astrodynamics::Time::TDB(
                                                                                                                                                                                   0.0s),
                                                                                                                                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 10000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 9000.0, 9000.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine *>(engine1));

    IO::Astrodynamics::Maneuvers::CombinedManeuver maneuver(engines, prop, 0.0, 42164000.0);

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(178.0 * IO::Astrodynamics::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::Astrodynamics::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::Astrodynamics::Constants::DEG_RAD));

    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(180.01 * IO::Astrodynamics::Constants::DEG_RAD));
    ASSERT_TRUE(res.IsValid());
    ASSERT_NEAR(1829.9645204299281, maneuver.GetDeltaV().Magnitude(), 1E-06);
    ASSERT_NEAR(1.3015883307426679, maneuver.GetDeltaV().GetX(), 1E-06);
    ASSERT_NEAR(-1661.4561558199939, maneuver.GetDeltaV().GetY(), 1E-06);
    ASSERT_NEAR(767.02796180322059, maneuver.GetDeltaV().GetZ(), 1E-06);
}