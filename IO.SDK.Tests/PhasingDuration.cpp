
#include <memory>
#include <gtest/gtest.h>
#include <vector>

#include <PhasingManeuver.h>
#include <Constants.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <TimeSpan.h>
#include <Window.h>
#include <Spacecraft.h>
#include <TDB.h>
#include <InertialFrames.h>
#include <ConicOrbitalElements.h>
#include <EquinoctialElements.h>

using namespace std::chrono_literals;

TEST(PhasingManeuver, PhasingDuration)
{
    auto n = 7.2922E-05;
    auto phasingDuration = IO::SDK::Maneuvers::PhasingDuration(3, n, 15 * IO::SDK::Constants::PI / 180.0);
    ASSERT_DOUBLE_EQ(87359.805954023512, phasingDuration.GetSeconds().count());
}

TEST(PhasingManeuver, SemiMajorAxis)
{
    auto a = IO::SDK::Maneuvers::PhasingSemiMajorAxis(3.986004418E14, IO::SDK::Time::TimeSpan(87359.805954023512s));
    ASSERT_DOUBLE_EQ(42553353.069617197, a);
}

TEST(PhasingManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6800000.0, 0.5, 0.0, 0.0, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6800000.0, 0.5, 0.0, 0.0, 30.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::PhasingManeuver maneuver(engines, prop, 3, orbitalParams2.get());

    //Initialize CanExecute
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(358.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 1° before
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(359.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 1°s after and must execute
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(1.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 2° after
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(2.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate at apogee
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(179.0 * IO::SDK::Constants::DEG_RAD)));
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(181.0 * IO::SDK::Constants::DEG_RAD)));
}

TEST(PhasingManeuver, TryExecute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_unique<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 345.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::ICRF);

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::PhasingManeuver maneuver(engines, prop, 3, orbitalParams2.get());

    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(-10.0s), IO::SDK::Frames::InertialFrames::ICRF));

    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(0.0001));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(14.039767793790816, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.0014039767698487144, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(14.039767723591977, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(8.5968782802424241e-16, maneuver.GetDeltaV().GetZ());
    ASSERT_DOUBLE_EQ(6.0351723087874625, maneuver.GetFuelBurned());
    ASSERT_DOUBLE_EQ(0.12070344617574924, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(1.3109841010206829s),IO::SDK::Time::TDB(1.4316875471964321s)), *maneuver.GetWindow());

}