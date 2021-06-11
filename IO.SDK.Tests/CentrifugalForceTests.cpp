#include <gtest/gtest.h>
#include <CentrifugalForce.h>
#include <Constants.h>
#include <Spacecraft.h>
#include <CelestialBody.h>
#include <chrono>
#include <memory>
#include <InertialFrames.h>

using namespace std::chrono_literals;

TEST(CentrifugalForce, SimpleCases)
{
    IO::SDK::Integrators::Forces::CentrifugalForce cf{};

    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(500.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 200.0, 0.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::ICRF);
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, "missCentrifugal", std::move(orbitalParams));

    auto force = cf.Apply(spc, IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(500.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 200.0, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF));
    auto acc = force / spc.GetMass();
    ASSERT_DOUBLE_EQ(8.157729703823426, (acc / IO::SDK::Constants::g0).Magnitude());

}