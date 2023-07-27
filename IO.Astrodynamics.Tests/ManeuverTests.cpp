/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

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
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(Maneuvers, Initialization)
{

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    TestManeuver m_first(engines, prop);

    TestManeuver m_second(engines, prop);
}

TEST(Maneuvers, Execute)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));
    IO::Astrodynamics::Propagators::Propagator pro(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Time::TDB(300.0s)));

    //Fictive data are enough for this test
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(110.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(120.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(130.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(140.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));
    pro.AddStateVector(IO::Astrodynamics::OrbitalParameters::StateVector(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(150.0s),
                                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()));

    s.AddFuelTank("ft1", 1000.0, 800.0);
    s.AddFuelTank("ft2", 1000.0, 700.0);
    s.AddFuelTank("ft3", 1000.0, 200.0);

    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 50.0);
    s.AddEngine("sn2", "eng2", "ft2", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 30.0);
    s.AddEngine("sn3", "eng3", "ft3", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 300.0, 100.0);

    auto engine1 = s.GetEngine("sn1");
    auto engine2 = s.GetEngine("sn2");
    auto engine3 = s.GetEngine("sn3");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine2));
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine3));


    TestManeuver maneuver(engines, pro);

    maneuver.Handle(IO::Astrodynamics::Time::TDB(100.0s));

    IO::Astrodynamics::OrbitalParameters::StateVector maneuverPoint(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(130.0s),
                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());

    //Check maneuver summary
    auto res = maneuver.TryExecute(maneuverPoint, 2000.0);
    ASSERT_TRUE(res.IsValid());
    ASSERT_FALSE(res.IsExecutedTooEarly());

    //Check maneuver detail
    ASSERT_EQ(IO::Astrodynamics::Time::TimeSpan(14.148441346767905s), maneuver.GetThrustDuration());
    ASSERT_EQ(IO::Astrodynamics::Math::Vector3D(2000.0, 0.0, 0.0), maneuver.GetDeltaV());
    ASSERT_DOUBLE_EQ(1331.8753077414322, maneuver.GetFuelBurned());
    ASSERT_EQ(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(IO::Astrodynamics::Time::TDB(122.92577932661605s), IO::Astrodynamics::Time::TimeSpan(14.148441346767905s)), *maneuver.GetThrustWindow());

    //Check maneuver actions on propagator
    ASSERT_EQ(13, pro.GetStateVectors().size());
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(100.0s), pro.GetStateVectors()[0].GetEpoch());
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(110.0s), pro.GetStateVectors()[1].GetEpoch());
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(120.0s), pro.GetStateVectors()[2].GetEpoch());
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(122.92577932661605s), pro.GetStateVectors()[3].GetEpoch());//epoch at maneuver start point
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(130s).GetSecondsFromJ2000().count(), pro.GetStateVectors()[4].GetEpoch().GetSecondsFromJ2000().count());//epoch at maneuver point
    ASSERT_EQ(IO::Astrodynamics::Time::TDB(131s).GetSecondsFromJ2000().count(), pro.GetStateVectors()[5].GetEpoch().GetSecondsFromJ2000().count());// epoch 1s after maneuver point

    //Check Spacecraft
    auto totalMass = s.GetMass();
    ASSERT_DOUBLE_EQ(2700.0 - 1331.8753077414322, totalMass);
}
