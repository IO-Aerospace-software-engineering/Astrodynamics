#include <gtest/gtest.h>
#include <vector>
#include <memory>
#include <chrono>

#include <CelestialBody.h>
#include <StateVector.h>
#include <StateOrientation.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <ProgradeAttitude.h>
#include <TimeSpan.h>
#include <InertialFrames.h>
#include <TDB.h>

using namespace std::chrono_literals;

TEST(ProgradeAttitude, GetOrientation)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::ProgradeAttitude prograde(engines, prop, IO::SDK::Time::TimeSpan(10s));
    prop.SetStandbyManeuver(&prograde);

    prop.Propagate();

    //auto res = nadir.TryExecute(s.GetOrbitalParametersAtEpoch()->GetStateVector(IO::SDK::Time::TDB(100.1s)));
    auto orientation = s.GetOrientation(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, prograde.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion());
    ASSERT_EQ(IO::SDK::Math::Vector3D(0.0, 1.0, 0.0), newVector);
}

TEST(ProgradeAttitude, GetOrientationMinimumEpoch)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::ProgradeAttitude prograde(engines, prop, IO::SDK::Time::TDB("2021-01-01T13:00:10"), IO::SDK::Time::TimeSpan(10s));
    prop.SetStandbyManeuver(&prograde);

    prop.Propagate();

    auto orientation = s.GetOrientation(IO::SDK::Time::TDB("2021-01-01T13:00:10"), IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, prograde.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion());
    ASSERT_EQ(IO::SDK::Math::Vector3D(0.0, 1.0, 0.0), newVector);
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:00:00"), s.GetOrientationsCoverageWindow().GetStartDate());
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:01:00"), s.GetOrientationsCoverageWindow().GetEndDate());
    ASSERT_EQ(IO::SDK::Time::TimeSpan(60s).GetSeconds().count(), s.GetOrientationsCoverageWindow().GetLength().GetSeconds().count());
}