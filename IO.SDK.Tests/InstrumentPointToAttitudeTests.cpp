#include <gtest/gtest.h>
#include <vector>
#include <memory>
#include <chrono>

#include <CelestialBody.h>
#include <StateVector.h>
#include <StateOrientation.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <InstrumentPointingToAttitude.h>
#include <TimeSpan.h>
#include <InertialFrames.h>
#include <TDB.h>
#include <Vectors.h>
#include <Scenario.h>

using namespace std::chrono_literals;

TEST(InstrumentPointingToAttitude, GetOrientation)
{

    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::SDK::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::SDK::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::SDK::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, "mt22", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::SDK::Math::Vector3D(-IO::SDK::Constants::PI2, 0.0, 0.0), IO::SDK::Tests::VectorZ, IO::SDK::Tests::VectorY,
                               IO::SDK::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::SDK::Time::TimeSpan(10s), *instrument, *moon);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64548856398372612, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67028530475051784, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36614494944179204, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingToSiteAttitude, GetOrientation)
{

    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);
    //    long 1.1159563818495755
    //    lat 0.0020551285296693113
    IO::SDK::Sites::Site site(13001, "targetedSite", IO::SDK::Coordinates::Geodetic(1.1159563818495755, 0.0020551285296693113, 0.0), earth);

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::SDK::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::SDK::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::SDK::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, "mt22", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::SDK::Math::Vector3D(-IO::SDK::Constants::PI2, 0.0, 0.0), IO::SDK::Tests::VectorZ, IO::SDK::Tests::VectorY,
                               IO::SDK::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::SDK::Time::TimeSpan(10s), *instrument, site);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    auto point = s.GetSubObserverPoint(*earth, IO::SDK::AberrationsEnum::LT, IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.99999999985376886, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(-0.000015435253205632487, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(-0.0000073630780085578658, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingToSiteAttitude2, GetOrientation)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);
    //    long 1.1159563818495755
    //    lat 0.0020551285296693113
    IO::SDK::Sites::Site site(13001, "targetedSite", IO::SDK::Coordinates::Geodetic(1.1159563818495755, 0.0020551285296693113, 0.0), moon);

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::SDK::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::SDK::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::SDK::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, "mt22", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::SDK::Math::Vector3D(-IO::SDK::Constants::PI2, 0.0, 0.0), IO::SDK::Tests::VectorZ, IO::SDK::Tests::VectorY,
                               IO::SDK::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::SDK::Time::TimeSpan(10s), *instrument, site);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    auto point = s.GetSubObserverPoint(*earth, IO::SDK::AberrationsEnum::LT, IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64230280628076275, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67264626995439025, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36741543320919756, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingTotAttitude, GetOrientationNotBeforeEpoch)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::SDK::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::SDK::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::SDK::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, "mt22", std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));

    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::SDK::Math::Vector3D(-IO::SDK::Constants::PI2, 0.0, 0.0), IO::SDK::Tests::VectorZ, IO::SDK::Tests::VectorY,
                               IO::SDK::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::SDK::Time::TDB("2021-01-01T13:00:00"), IO::SDK::Time::TimeSpan(10s),
                                                                                 *instrument, *moon);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64548856398372612, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67028530475051784, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36614494944179204, pointingVector.GetZ(), 1E-09);
}