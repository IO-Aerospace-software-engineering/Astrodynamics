#include <gtest/gtest.h>
#include <memory>

#include <ApsidalAlignmentManeuver.h>
#include <OrbitalParameters.h>
#include <CelestialBody.h>
#include <ConicOrbitalElements.h>
#include <StateVector.h>
#include <StateOrientation.h>
#include <InertialFrames.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <Engine.h>
#include <TDB.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(ApsidalAlignmentManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-105, "szptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2);

    //Initialize
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(150.0 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Can't execute, too early
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(155.0 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Must execute at 156.41° tolerance = 0.1°
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(156.5 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Can't execute because point p is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(158.0 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Before Q point
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(341.0 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Must execute at 341.77° tolerance = 0.1°
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(341.8 * IO::Astrodynamics::Constants::DEG_RAD)));

    //Can't execute because pont q is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(345.0 * IO::Astrodynamics::Constants::DEG_RAD)));
}

TEST(ApsidalAlignmentManeuver, ExecuteQ)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-109, "sqtest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    std::vector<IO::Astrodynamics::Integrators::Forces::Force *> forces{};
    IO::Astrodynamics::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s), forces);
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2);

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(340.0 * IO::Astrodynamics::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(341.0 * IO::Astrodynamics::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(10.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF()));

    //try execute at Q
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(341.77 * IO::Astrodynamics::Constants::DEG_RAD));

    //Can't execute, too early
    ASSERT_TRUE(res.IsValid());

    //Theta 30°
    ASSERT_NEAR(30.0 * IO::Astrodynamics::Constants::DEG_RAD, maneuver.GetTheta(), 1E-12);

    ASSERT_DOUBLE_EQ(1456.6489286382466, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1368.8299669788796, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(498.12711510572353, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, maneuver.GetDeltaV().GetZ());

    ASSERT_DOUBLE_EQ(17837.515578464092, maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(17848.198713809928, maneuver.GetThrustWindow()->GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(10.683135345837465, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_DOUBLE_EQ(534.15676729187328, maneuver.GetFuelBurned());
}

TEST(ApsidalAlignmentManeuver, ExecuteP)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-107, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2);

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(155.0 * IO::Astrodynamics::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(156.0 * IO::Astrodynamics::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(10.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF()));

    //try execute at P
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(156.5 * IO::Astrodynamics::Constants::DEG_RAD));

    //Can't execute, too early
    ASSERT_TRUE(res.IsValid());

    //Theta 30°
    ASSERT_DOUBLE_EQ(0.52359877559829782, maneuver.GetTheta());

    ASSERT_DOUBLE_EQ(1465.6234133089795, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1352.4744547815126, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(564.68118332640915, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, maneuver.GetDeltaV().GetZ());

    ASSERT_DOUBLE_EQ(6946.0140230624074, maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(6956.7526549159793, maneuver.GetThrustWindow()->GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(10.738631853571592, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_DOUBLE_EQ(536.93159267857959, maneuver.GetFuelBurned());
}

TEST(ApsidalAlignmentManeuver, CheckOrbitalParams)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);

    IO::Astrodynamics::Time::TDB startEpoch("2021-06-02T00:00:00");
    IO::Astrodynamics::Time::TDB endEpoch("2021-06-03T00:00:00");

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, startEpoch, IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0, startEpoch, IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    std::vector<IO::Astrodynamics::Integrators::Forces::Force *> forces{};

    IO::Astrodynamics::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-111, "aptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s), forces);

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(startEpoch, endEpoch));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2);

    prop.SetStandbyManeuver(&maneuver);

    prop.Propagate();

    auto propagationResult = prop.GetStateVectors().back();

    ASSERT_DOUBLE_EQ(8999398.6360428147, propagationResult.GetPerigeeVector().Magnitude());
    ASSERT_DOUBLE_EQ(0.50004260870488881, propagationResult.GetEccentricity());
    ASSERT_DOUBLE_EQ(0.0, propagationResult.GetInclination());
    ASSERT_DOUBLE_EQ(0.0, propagationResult.GetRightAscendingNodeLongitude());
    ASSERT_DOUBLE_EQ(0.52362753017983721, propagationResult.GetPeriapsisArgument());
}