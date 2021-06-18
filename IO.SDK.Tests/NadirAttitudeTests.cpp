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

using namespace std::chrono_literals;

TEST(NadirAttitude, GetOrientation)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth"); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    //Add fictive data because it executed outside propagator
    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(80.0s), IO::SDK::Frames::InertialFrames::ICRF));
    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);
    IO::SDK::Maneuvers::Attitudes::NadirAttitude nadir(engines, prop);


    auto res = nadir.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(IO::SDK::Time::TDB(100.1s)));
    auto orientation=s.GetOrientation(IO::SDK::Time::TDB(100.1s),IO::SDK::Time::TimeSpan(1s),IO::SDK::Frames::InertialFrames::ICRF);

    ASSERT_TRUE(res.IsValid());
    ASSERT_DOUBLE_EQ(0.0, nadir.GetDeltaV().Magnitude());
}