#include <gtest/gtest.h>
#include <vector>
#include <memory>
#include <chrono>

#include <CelestialBody.h>
#include <StateVector.h>
#include <StateOrientation.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <ZenithAttitude.h>
#include <TimeSpan.h>
#include <InertialFrames.h>
#include <TDB.h>

using namespace std::chrono_literals;

TEST(ZenithAttitude, GetOrientation)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth"); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::ZenithAttitude zenith(engines, prop,IO::SDK::Time::TimeSpan(10s));
    zenith.Handle(IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    prop.Propagate();

    auto orientation = s.GetOrientation(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, zenith.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion());
    ASSERT_EQ(IO::SDK::Math::Vector3D(0.99999998288572889, -2.980232227667301e-08, 0.0), newVector);
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:00:00"), s.GetOrientationsCoverageWindow().GetStartDate());
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:00:10"), s.GetOrientationsCoverageWindow().GetEndDate());
    ASSERT_EQ(IO::SDK::Time::TimeSpan(10s).GetSeconds().count(), s.GetOrientationsCoverageWindow().GetLength().GetSeconds().count());
}

TEST(ZenithAttitude, GetOrientationNotBeforeEpoch)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth"); //GEOPHYSICAL PROPERTIES provided by JPL

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6678000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 7727.0, 0.0), IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "maneuverTest", 1000.0, 3000.0, "mt01", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::ZenithAttitude zenith(engines, prop,IO::SDK::Time::TDB("2021-01-01T13:00:10"),IO::SDK::Time::TimeSpan(10s));
    prop.SetStandbyManeuver(&zenith);

    prop.Propagate();

    auto orientation = s.GetOrientation(IO::SDK::Time::TDB("2021-01-01T13:00:10"), IO::SDK::Time::TimeSpan(10s), IO::SDK::Frames::InertialFrames::GetICRF());

    ASSERT_DOUBLE_EQ(0.0, zenith.GetDeltaV().Magnitude());
    ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), orientation.GetFrame());
    auto newVector = s.Front.Rotate(orientation.GetQuaternion());
    ASSERT_EQ(IO::SDK::Math::Vector3D(0.99993304357344959, 0.011570015949534274, 0.0), newVector);
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:00:00"), s.GetOrientationsCoverageWindow().GetStartDate());
    ASSERT_EQ(IO::SDK::Time::TDB("2021-01-01T13:00:20"), s.GetOrientationsCoverageWindow().GetEndDate());
    ASSERT_EQ(IO::SDK::Time::TimeSpan(20s).GetSeconds().count(), s.GetOrientationsCoverageWindow().GetLength().GetSeconds().count());
}