#include <gtest/gtest.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <vector>
#include <CelestialBody.h>
#include <OrbitalPlaneChangingManeuver.h>
#include <TestManeuver.h>
#include <TimeSpan.h>
#include <Propagator.h>
#include <VVIntegrator.h>
#include <memory>

using namespace std::chrono_literals;

TEST(Maneuvers, Initialization)
{

    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    TestManeuver m_first(engines, prop);

    TestManeuver m_second(engines, prop);
}

TEST(Maneuvers, Execute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(300.0s)));

    //Fictive data are enough for this test
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(110.0s),IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(120.0s),IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(130.0s),IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(140.0s),IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(150.0s),IO::SDK::Frames::InertialFrames::GetICRF()));

    s.AddFuelTank("ft1", 1000.0, 800.0);
    s.AddFuelTank("ft2", 1000.0, 700.0);
    s.AddFuelTank("ft3", 1000.0, 200.0);

    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 50.0);
    s.AddEngine("sn2", "eng2", "ft2", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 30.0);
    s.AddEngine("sn3", "eng3", "ft3", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 100.0);

    auto engine1 = s.GetEngine("sn1");
    auto engine2 = s.GetEngine("sn2");
    auto engine3 = s.GetEngine("sn3");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);
    engines.push_back(*engine2);
    engines.push_back(*engine3);

    TestManeuver maneuver(engines, pro);

    maneuver.Handle(IO::SDK::Time::TDB(100.0s));

    IO::SDK::OrbitalParameters::StateVector maneuverPoint(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(130.0s),IO::SDK::Frames::InertialFrames::GetICRF());

    //Check maneuver summary
    auto res = maneuver.TryExecute(maneuverPoint, 2000.0);
    ASSERT_TRUE(res.IsValid());
    ASSERT_FALSE(res.IsExecutedTooEarly());

    //Check maneuver detail
    ASSERT_EQ(IO::SDK::Time::TimeSpan(14.148441346767905s), maneuver.GetThrustDuration());
    ASSERT_EQ(IO::SDK::Math::Vector3D(2000.0, 0.0, 0.0), maneuver.GetDeltaV());
    ASSERT_DOUBLE_EQ(1331.8753077414322, maneuver.GetFuelBurned());
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(122.92577932661605s), IO::SDK::Time::TimeSpan(14.148441346767905s)), *maneuver.GetThrustWindow());

    //Check maneuver actions on propagator
    ASSERT_EQ(5, pro.GetStateVectors().size());
    ASSERT_EQ(IO::SDK::Time::TDB(100.0s), pro.GetStateVectors()[0].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(110.0s), pro.GetStateVectors()[1].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(120.0s), pro.GetStateVectors()[2].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(122.92577932661605s), pro.GetStateVectors()[3].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(122.92577932661605s + 14.148441346767905s), pro.GetStateVectors()[4].GetEpoch());

    //Check spacecraft
    auto totalMass = s.GetMass();
    ASSERT_DOUBLE_EQ(2700.0 - 1331.8753077414322, totalMass);
}
