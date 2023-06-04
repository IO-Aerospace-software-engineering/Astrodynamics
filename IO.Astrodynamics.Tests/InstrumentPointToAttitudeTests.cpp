#include <gtest/gtest.h>
#include <VVIntegrator.h>
#include <InstrumentPointingToAttitude.h>
#include <InertialFrames.h>
#include <Vectors.h>
#include "Constants.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(InstrumentPointingToAttitude, GetOrientation)
{

    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::Astrodynamics::Math::Vector3D(-IO::Astrodynamics::Constants::PI2, 0.0, 0.0), IO::Astrodynamics::Tests::VectorZ, IO::Astrodynamics::Tests::VectorY,
                               IO::Astrodynamics::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::Astrodynamics::Time::TimeSpan(10s), *instrument, *moon);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64548856398372612, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67028530475051784, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36614494944179204, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingToSiteAttitude, GetOrientation)
{

    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);
    //    long 1.1159563818495755
    //    lat 0.0020551285296693113
    IO::Astrodynamics::Sites::Site site(399001, "targetedSite", IO::Astrodynamics::Coordinates::Geodetic(1.1159563818495755, 0.0020551285296693113, 0.0), earth,std::string(SitePath));

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::Astrodynamics::Math::Vector3D(-IO::Astrodynamics::Constants::PI2, 0.0, 0.0), IO::Astrodynamics::Tests::VectorZ, IO::Astrodynamics::Tests::VectorY,
                               IO::Astrodynamics::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::Astrodynamics::Time::TimeSpan(10s), *instrument, site);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.99999999985376886, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(-0.000015435253205632487, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(-0.0000073630780085578658, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingToSiteAttitude2, GetOrientation)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);
    //    long 1.1159563818495755
    //    lat 0.0020551285296693113
    IO::Astrodynamics::Sites::Site site(399001, "targetedSite", IO::Astrodynamics::Coordinates::Geodetic(1.1159563818495755, 0.0020551285296693113, 0.0), moon,std::string(SitePath));

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::Astrodynamics::Math::Vector3D(-IO::Astrodynamics::Constants::PI2, 0.0, 0.0), IO::Astrodynamics::Tests::VectorZ, IO::Astrodynamics::Tests::VectorY,
                               IO::Astrodynamics::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::Astrodynamics::Time::TimeSpan(10s), *instrument, site);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64230280628076275, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67264626995439025, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36741543320919756, pointingVector.GetZ(), 1E-09);

}

TEST(InstrumentPointingTotAttitude, GetOrientationNotBeforeEpoch)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(6678000.0,
                                                                                                                                                                      0.0, 0.0),
                                                                                                                                              IO::Astrodynamics::Math::Vector3D(0.0, 7727.0,
                                                                                                                                                                      0.0),
                                                                                                                                              IO::Astrodynamics::Time::TDB(
                                                                                                                                                      "2021-01-01T13:00:00"),
                                                                                                                                              IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-544, "instPointing", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::Astrodynamics::Integrators::VVIntegrator integrator(IO::Astrodynamics::Time::TimeSpan(1.0s));

    IO::Astrodynamics::Propagators::Propagator prop(s, integrator, IO::Astrodynamics::Time::Window(IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TDB("2021-01-01T13:01:00")));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddCircularFOVInstrument(550, "inst550", IO::Astrodynamics::Math::Vector3D(-IO::Astrodynamics::Constants::PI2, 0.0, 0.0), IO::Astrodynamics::Tests::VectorZ, IO::Astrodynamics::Tests::VectorY,
                               IO::Astrodynamics::Constants::PI2);
    auto instrument = s.GetInstrument(550);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine*>(engine1));

    IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude pointingManeuver(engines, prop, IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"), IO::Astrodynamics::Time::TimeSpan(10s),
                                                                                 *instrument, *moon);
    prop.SetStandbyManeuver(&pointingManeuver);

    prop.Propagate();

    ASSERT_DOUBLE_EQ(0.0, pointingManeuver.GetDeltaV().Magnitude());
    auto pointingVector = instrument->GetBoresight(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Time::TDB("2021-01-01T13:00:00"));

    ASSERT_NEAR(-0.64548856398372612, pointingVector.GetX(), 1E-09);
    ASSERT_NEAR(0.67028530475051784, pointingVector.GetY(), 1E-09);
    ASSERT_NEAR(0.36614494944179204, pointingVector.GetZ(), 1E-09);
}