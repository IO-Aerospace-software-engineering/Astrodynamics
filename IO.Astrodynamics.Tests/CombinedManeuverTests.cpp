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
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333,
                                                                                                                                                       10.0 *
                                                                                                                                                       IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                       0.0, 0.0, 0.0,
                                                                                                                                                       IO::Astrodynamics::Time::TDB(0.0s),
                                                                                                                                                       IO::Astrodynamics::Frames::InertialFrames::GetICRF());

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
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(180.0 * IO::Astrodynamics::Constants::DEG_RAD)));
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(182.0 * IO::Astrodynamics::Constants::DEG_RAD)));
}

TEST(CombinedManeuver, TryExecuteWithPeregeeHigherThanApogee)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6678000.0,
                                                                                                                                                       0.726546824, 28.5 *
                                                                                                                                                                    IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                       0.0, 0.0, 0.0,
                                                                                                                                                       IO::Astrodynamics::Time::TDB(0.0s),
                                                                                                                                                       IO::Astrodynamics::Frames::InertialFrames::GetICRF());

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
    ASSERT_DOUBLE_EQ(1830.2350336445459, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1.0262043727361105, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(-1661.679969759336, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(767.18896198071627, maneuver.GetDeltaV().GetZ());
}

TEST(CombinedManeuver, TryExecuteWithPeregeeLowerThanApogee)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 6678000.0, 0.7266,
                                                                                                                                                       28.5 *
                                                                                                                                                       IO::Astrodynamics::Constants::DEG_RAD,
                                                                                                                                                       0.0, 0.0, 0.0,
                                                                                                                                                       IO::Astrodynamics::Time::TDB(0.0s),
                                                                                                                                                       IO::Astrodynamics::Frames::InertialFrames::GetICRF());

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
    ASSERT_DOUBLE_EQ(1829.9651453364793, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1.0261885721047042, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(-1661.4570370296583, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(767.02796180322059, maneuver.GetDeltaV().GetZ());
}