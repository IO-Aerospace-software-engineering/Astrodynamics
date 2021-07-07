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

using namespace std::chrono_literals;

TEST(ApsidalAlignmentManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2.get());

    //Initialize
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(150.0 * IO::SDK::Constants::DEG_RAD)));

    //Can't execute, too early
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(155.0 * IO::SDK::Constants::DEG_RAD)));

    //Must execute at 156.41°
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(157.0 * IO::SDK::Constants::DEG_RAD)));

    //Can't execute because point p is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(158.0 * IO::SDK::Constants::DEG_RAD)));

    //Before Q point
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(341.0 * IO::SDK::Constants::DEG_RAD)));

    //Must execute at 341.77°
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(342.0 * IO::SDK::Constants::DEG_RAD)));

    //Can't execute because pont q is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(345.0 * IO::SDK::Constants::DEG_RAD)));
}

TEST(ApsidalAlignmentManeuver, ExecuteP)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2.get());

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(155.0 * IO::SDK::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(156.0 * IO::SDK::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(10.0s), IO::SDK::Frames::InertialFrames::GetICRF()));

    //try execute at P
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(157.0 * IO::SDK::Constants::DEG_RAD));

    //Can't execute, too early
    ASSERT_TRUE(res.IsValid());

    //Theta 30°
    ASSERT_DOUBLE_EQ(30.0 * IO::SDK::Constants::DEG_RAD, maneuver.GetTheta());

    ASSERT_DOUBLE_EQ(1457.3597220350186, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1344.8977433563978, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(561.37992422677098, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, maneuver.GetDeltaV().GetZ());

    ASSERT_DOUBLE_EQ(6990.2572903422742, maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(7000.94482521596, maneuver.GetThrustWindow()->GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(10.68753487368553, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_DOUBLE_EQ(534.37674368427656, maneuver.GetFuelBurned());
}

TEST(ApsidalAlignmentManeuver, ExecuteQ)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.333333, 0.0, 0.0, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 9000000.0, 0.5, 0.0, 0.0, 30.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::ApsidalAlignmentManeuver maneuver(engines, prop, orbitalParams2.get());

    //Initialize
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(340.0 * IO::SDK::Constants::DEG_RAD));
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(341.0 * IO::SDK::Constants::DEG_RAD));

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(10.0s), IO::SDK::Frames::InertialFrames::GetICRF()));

    //try execute at Q
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(342.0 * IO::SDK::Constants::DEG_RAD));

    //Can't execute, too early
    ASSERT_TRUE(res.IsValid());

    //Theta 30°
    ASSERT_NEAR(30.0 * IO::SDK::Constants::DEG_RAD, maneuver.GetTheta(),1E-12);

    ASSERT_DOUBLE_EQ(1456.8673054332951, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-1369.0374802364965, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(498.19546700885803, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, maneuver.GetDeltaV().GetZ());

    ASSERT_DOUBLE_EQ(17843.16033783407, maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(17853.844824920649, maneuver.GetThrustWindow()->GetEndDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(10.684487086579903, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_DOUBLE_EQ(534.2243543289951, maneuver.GetFuelBurned());
}
