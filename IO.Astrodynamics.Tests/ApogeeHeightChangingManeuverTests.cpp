#include <gtest/gtest.h>
#include <ApogeeHeightChangingManeuver.h>
#include <VVIntegrator.h>
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(ApogeeHeightChangingManeuverTests, CanExecute)
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
    IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver acm(engines, prop, 8000000.0);

    //Initialize CanExecute
    ASSERT_FALSE(acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(80.0s))));

    //Evaluate 3s before
    ASSERT_FALSE(acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(97.0s))));

    //Evaluate 3s after and must execute
    ASSERT_TRUE(acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(103.0s))));

    //Evaluate 10s after
    ASSERT_FALSE(acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(110.0s))));
}

TEST(ApogeeHeightChangingManeuverTests, IncreaseApogeeHeight)
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
    IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver acm(engines, prop, 42164000.0);

    acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(80.0s)));
    auto res = acm.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(100.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(2424.6084264204073, acm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.28046303946422696, acm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(2424.608410199331, acm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, acm.GetDeltaV().GetZ());
}

TEST(ApogeeHeightChangingManeuverTests, DecreaseApogeeHeight)
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
    IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver acm(engines, prop, 6678000.0);

    acm.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(80.0s)));
    auto res = acm.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(IO::Astrodynamics::Time::TDB(100.1s)));

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(1467.2074439917321, acm.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(0.010697828170955959, acm.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(-1467.2074439527321, acm.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(0.0, acm.GetDeltaV().GetZ());
}