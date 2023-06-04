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
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(PerigeeHeightChangingManeuverTests, CanExecute)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 0.0), IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};
    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));
    IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 8000000.0);

    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    //Initialize CanExecute
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch - IO::Astrodynamics::Time::TimeSpan(10.0s))));

    //Evaluate 3s before
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch - IO::Astrodynamics::Time::TimeSpan(3.0s))));

    //Evaluate 3s after and must execute
    ASSERT_TRUE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch + IO::Astrodynamics::Time::TimeSpan(3.0s))));

    //Evaluate 10s after
    ASSERT_FALSE(pcm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch + IO::Astrodynamics::Time::TimeSpan(10.0s))));
}

TEST(PerigeeHeightChangingManeuverTests, IncreasePerigeeHeight)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6678000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 7727.0, 0.0), IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};
    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(80.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF()));
    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));
    IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 42164000.0);
    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    auto res = pcm.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch + IO::Astrodynamics::Time::TimeSpan(0.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(2425.7836521643781, pcm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(0.280430410132377074, pcm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(-2425.7836359549324, pcm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, pcm.GetDeltaV().GetZ());
}

TEST(PerigeeHeightChangingManeuverTests, DecreasePerigeeHeight)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(42164000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 3075.035, 0.0), IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};
    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(80.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF()));
    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));
    IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver pcm(engines, prop, 6678000.0);

    auto apogeeEpoch{s.GetOrbitalParametersAtEpoch()->GetEpoch() + s.GetOrbitalParametersAtEpoch()->GetPeriod() / 2.0};

    auto res = pcm.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(apogeeEpoch + IO::Astrodynamics::Time::TimeSpan(0.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(1466.4510337589829, pcm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.010687184635934464, pcm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(1466.45103372004, pcm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, pcm.GetDeltaV().GetZ());
}