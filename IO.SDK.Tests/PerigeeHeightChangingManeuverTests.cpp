#include <vector>
#include <chrono>

#include <gtest/gtest.h>
#include <PerigeeHeightChangingManeuver.h>
#include <Spacecraft.h>
#include <Engine.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <TimeSpan.h>
#include <TDB.h>
#include <OrbitalParameters.h>
#include <StateVector.h>

using namespace std::chrono_literals;

TEST(PerigeeHeightChangingManeuverTests, CanExecute)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));
    IO::SDK::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 8000000.0);

    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    //Initialize CanExecute
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch - IO::SDK::Time::TimeSpan(10.0s))));

    //Evaluate 3s before
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch - IO::SDK::Time::TimeSpan(3.0s))));

    //Evaluate 3s after and must execute
    ASSERT_TRUE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch + IO::SDK::Time::TimeSpan(3.0s))));

    //Evaluate 10s after
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch + IO::SDK::Time::TimeSpan(10.0s))));
}

TEST(PerigeeHeightChangingManeuverTests, IncreasePerigeeHeight)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(80.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));
    IO::SDK::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 42164000.0);
    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    auto res = pcm.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch + IO::SDK::Time::TimeSpan(0.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(2425.7836521643781, pcm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(0.280430410132377074, pcm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(-2425.7836359549324, pcm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, pcm.GetDeltaV().GetZ());
}

TEST(PerigeeHeightChangingManeuverTests, DecreasePerigeeHeight)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(42164000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 3075.035, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(80.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));
    IO::SDK::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 6678000.0);

    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    auto res = pcm.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(apogeeEpoch + IO::SDK::Time::TimeSpan(0.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(1466.4510337589829, pcm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.010687184635934464, pcm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(1466.45103372004, pcm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, pcm.GetDeltaV().GetZ());
}