#include <gtest/gtest.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <vector>
#include <CelestialBody.h>
#include <OrbitalPlaneChangingManeuver.h>
#include <TimeSpan.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <ConicOrbitalElements.h>
#include <TestsConstants.h>

#include <chrono>
#include <memory>

using namespace std::chrono_literals;

TEST(OrbitalPlaneChangingManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.5, 60.0 * IO::SDK::Constants::DEG_RAD, 10.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.5, 45.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto T = s.GetOrbitalParametersAtEpoch()->GetPeriod().GetSeconds().count();

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654);
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654 + IO::SDK::Constants::PI);
    //Initialize
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector()));

    //Can't execute, too early
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN - IO::SDK::Time::TimeSpan(10s))));

    //Must execute at 125.93° == t+6600s
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN + IO::SDK::Time::TimeSpan(10s))));

    //Can't execute because node is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN + IO::SDK::Time::TimeSpan(30s))));

    //==============
    //!!NEXT NODE!!
    //==============

    //Can't execute, too far
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN - IO::SDK::Time::TimeSpan(10s))));

    //Must execute at 125.93°+180° == t+32959
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN + IO::SDK::Time::TimeSpan(10s))));

    //Can't execute because node is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN + IO::SDK::Time::TimeSpan(30s))));

    //==============
    //Test another lap to validate switching between node inbound and outbound
    //==============

    //Can't execute, too early
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN - IO::SDK::Time::TimeSpan(10s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));

    //Must execute at 125.93° == t+6600s+Orbital Period
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN + IO::SDK::Time::TimeSpan(10s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));

    //Can't execute because node is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN + IO::SDK::Time::TimeSpan(30s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));

    //==============
    //!!NEXT NODE!!
    //==============

    //Can't execute, too far
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN - IO::SDK::Time::TimeSpan(10s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));

    //Must execute at 125.93°+180° == t+32959+Orbital Period
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN + IO::SDK::Time::TimeSpan(10s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));

    //Can't execute because node is behind
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN + IO::SDK::Time::TimeSpan(30s) + s.GetOrbitalParametersAtEpoch()->GetPeriod())));
}

TEST(OrbitalPlaneChangingManeuver, IdentifyNode)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 60.0 * IO::SDK::Constants::DEG_RAD, 10.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 45.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197893);
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197893 + IO::SDK::Constants::PI);

    //Initialize
    ASSERT_FALSE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN)));
    ASSERT_TRUE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN)));
}

TEST(OrbitalPlaneChangingManeuver, IdentifyNode2)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 60.0 * IO::SDK::Constants::DEG_RAD, 220.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 130.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(5.4677);
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(5.4677 - IO::SDK::Constants::PI);

    //Initialize
    ASSERT_FALSE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN)));
    ASSERT_TRUE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN)));
}

TEST(OrbitalPlaneChangingManeuver, IdentifyNode3)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 140.0 * IO::SDK::Constants::DEG_RAD, 220.0 * IO::SDK::Constants::DEG_RAD, 70.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.2, 130.0 * IO::SDK::Constants::DEG_RAD, 300.0 * IO::SDK::Constants::DEG_RAD, 205.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(6.24);
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(6.24 - IO::SDK::Constants::PI);

    //Initialize
    ASSERT_FALSE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN)));
    ASSERT_TRUE(maneuver.IsAscendingNode(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN)));
}

TEST(OrbitalPlaneChangingManeuver, ExecuteInsuffisantDeltaV)
{

    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 60.0 * IO::SDK::Constants::DEG_RAD, 10.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654);
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654 + IO::SDK::Constants::PI);

    //To detect if we're passing through the node we need at least two
    //This canExecute will evaluate a first point
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN - IO::SDK::Time::TimeSpan(10.0s)));

    //The "TryExecute" will evaluate a second time "CanExecute", so it will be able to check the passage of the node
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN));

    ASSERT_FALSE(res.IsValid());
    ASSERT_DOUBLE_EQ(3849.8574224042982, maneuver.GetDeltaV().Magnitude());
}

TEST(OrbitalPlaneChangingManeuver, ExecuteDN)
{

    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 60.0 * IO::SDK::Constants::DEG_RAD, 10.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));
    //Add fictive data
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(4260.0s), IO::SDK::Frames::InertialFrames::ICRF));

    s.AddFuelTank("ft1", 2000.0, 1900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654);                          //4282s
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654 + IO::SDK::Constants::PI); //10402s

    //To detect if we're passing through the node we need at least two
    //This canExecute will evaluate a first point
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN - IO::SDK::Time::TimeSpan(10.0s)));

    //The "TryExecute" will evaluate a second time "CanExecute", so it will be able to check the passage of the node
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyDN));

    ASSERT_TRUE(res.IsValid());

    //Check Delta V magnitude
    ASSERT_DOUBLE_EQ(3849.8574224042982, maneuver.GetDeltaV().Magnitude());

    //Check if delta v vector is parallel to moment of momentum and points to the right direction
    auto orientation = maneuver.GetDeltaV().Normalize();
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetX(), orientation.GetX(),1E-07);
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetY(), orientation.GetY(),1E-07);
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetZ(), orientation.GetZ(),1E-07);

    //Check inclination
    ASSERT_DOUBLE_EQ(0.66556812329478388, maneuver.GetRelativeInclination());

    //Check fuel burned
    ASSERT_DOUBLE_EQ(1687.9426869962572, maneuver.GetFuelBurned());

    //Check maneuver window
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(4265.245338621311s), IO::SDK::Time::TDB(4299.0041923612362s)), *maneuver.GetWindow());
}

TEST(OrbitalPlaneChangingManeuver, ExecuteAN)
{

    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 60.0 * IO::SDK::Constants::DEG_RAD, 10.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 11480000.0, 0.0, 45.0 * IO::SDK::Constants::DEG_RAD, 55.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(0.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));
    //Add fictive data
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(4260.0s), IO::SDK::Frames::InertialFrames::ICRF));

    s.AddFuelTank("ft1", 2000.0, 1900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver maneuver(engines, &prop, orbitalParams2.get());

    auto timeToTrueAnomalyDN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654);                          //4282s
    auto timeToTrueAnomalyAN = s.GetOrbitalParametersAtEpoch()->GetTimeToTrueAnomaly(2.197937654 + IO::SDK::Constants::PI); //10402s

    //To detect if we're passing through the node we need at least two
    //This canExecute will evaluate a first point
    maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN - IO::SDK::Time::TimeSpan(10.0s)));

    //The "TryExecute" will evaluate a second time "CanExecute", so it will be able to check the passage of the node
    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(timeToTrueAnomalyAN));

    ASSERT_TRUE(res.IsValid());

    //Check Delta V magnitude
    ASSERT_DOUBLE_EQ(3849.8574224042945, maneuver.GetDeltaV().Magnitude());

    //Check if delta v vector is parallel to moment of momentum and points to the right direction
    auto orientation = maneuver.GetDeltaV().Normalize();
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetX(), -orientation.GetX(),1E-07);
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetY(), -orientation.GetY(),1E-07);
    ASSERT_NEAR(s.GetOrbitalParametersAtEpoch()->GetSpecificAngularMomentum().Normalize().GetZ(), -orientation.GetZ(),1E-07);

    //Check inclination
    ASSERT_DOUBLE_EQ(0.66556812329478388, maneuver.GetRelativeInclination());

    //Check fuel burned
    ASSERT_DOUBLE_EQ(1687.9426869962572, maneuver.GetFuelBurned());

    //Check maneuver window
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(10385.842836252745s), IO::SDK::Time::TDB(10419.601689992669s)), *maneuver.GetWindow());
}