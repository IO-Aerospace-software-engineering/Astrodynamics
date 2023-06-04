
#include <memory>
#include <gtest/gtest.h>
#include <vector>

#include <PhasingManeuver.h>
#include <Constants.h>
#include <VVIntegrator.h>
#include <Propagator.h>
#include <TimeSpan.h>
#include <Window.h>
#include <Spacecraft.h>
#include <TDB.h>
#include <InertialFrames.h>
#include <ConicOrbitalElements.h>
#include <EquinoctialElements.h>
#include <ApogeeHeightChangingManeuver.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(PhasingManeuver, CanExecute)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6800000.0, 0.5, 0.0, 0.0, 0.0, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 6800000.0, 0.5, 0.0, 0.0, 30.0 * IO::SDK::Constants::DEG_RAD, 0.0, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));

    IO::SDK::Maneuvers::PhasingManeuver maneuver(engines, prop, 3, orbitalParams2);

    //Initialize CanExecute
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(358.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 1° before
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(359.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 1°s after and must execute
    ASSERT_TRUE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(0.001 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate 2° after
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(2.0 * IO::SDK::Constants::DEG_RAD)));

    //Evaluate at apogee
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(179.0 * IO::SDK::Constants::DEG_RAD)));
    ASSERT_FALSE(maneuver.CanExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(181.0 * IO::SDK::Constants::DEG_RAD)));
}

TEST(PhasingManeuver, TryExecuteOnGeostationary)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 345.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));

    IO::SDK::Maneuvers::PhasingManeuver maneuver(engines, prop, 3, orbitalParams2);

    prop.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(-10.0s), IO::SDK::Frames::InertialFrames::GetICRF()));

    auto res = maneuver.TryExecute(s.GetOrbitalParametersAtEpoch()->ToStateVector(0.0001));

    ASSERT_TRUE(res.IsValid());
#ifdef _WIN32
    ASSERT_DOUBLE_EQ(14.03976779378854, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.0014039767733584289, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(14.039767723589703, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(8.5968783017332277e-16, maneuver.GetDeltaV().GetZ());
    ASSERT_DOUBLE_EQ(6.0351723087866187, maneuver.GetFuelBurned());
    ASSERT_DOUBLE_EQ(0.12070344617573237, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(1.3109841010206913s), IO::SDK::Time::TDB(1.4316875471964237s)), *maneuver.GetThrustWindow());

#else

    ASSERT_DOUBLE_EQ(14.03976779378854, maneuver.GetDeltaV().Magnitude());
    ASSERT_DOUBLE_EQ(-0.0014039767733584289, maneuver.GetDeltaV().GetX());
    ASSERT_DOUBLE_EQ(14.039767723589703, maneuver.GetDeltaV().GetY());
    ASSERT_DOUBLE_EQ(8.5968783017332277e-16, maneuver.GetDeltaV().GetZ());
    ASSERT_DOUBLE_EQ(6.0351723087866187, maneuver.GetFuelBurned());
    ASSERT_DOUBLE_EQ(0.12070344617573237, maneuver.GetThrustDuration().GetSeconds().count());
    ASSERT_EQ(IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB(1.3109841010206913s), IO::SDK::Time::TDB(1.4316875471964237s)), *maneuver.GetThrustWindow());

#endif

    ASSERT_DOUBLE_EQ(maneuver.GetThrustWindow()->GetLength().GetSeconds().count(), maneuver.GetThrustDuration().GetSeconds().count());

    //Attitude and thrust window must be the same
    ASSERT_DOUBLE_EQ(maneuver.GetAttitudeWindow()->GetLength().GetSeconds().count(), maneuver.GetThrustWindow()->GetLength().GetSeconds().count());
    ASSERT_DOUBLE_EQ(maneuver.GetAttitudeWindow()->GetStartDate().GetSecondsFromJ2000().count(), maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(maneuver.GetAttitudeWindow()->GetEndDate().GetSecondsFromJ2000().count(), maneuver.GetThrustWindow()->GetEndDate().GetSecondsFromJ2000().count());

    //Check maneuver windows
    ASSERT_DOUBLE_EQ(maneuver.GetManeuverWindow()->GetStartDate().GetSecondsFromJ2000().count(), maneuver.GetThrustWindow()->GetStartDate().GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(235872.83661685922, maneuver.GetManeuverWindow()->GetLength().GetSeconds().count());
}

TEST(PhasingManeuver, CheckOrbitalParameters)
{
    const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);
    IO::SDK::Time::TDB startEpoch("2021-01-01T00:00:00");
    IO::SDK::Time::TDB endEpoch("2021-01-04T01:00:00");

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams1 = std::make_unique<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, startEpoch, IO::SDK::Frames::InertialFrames::GetICRF());
    std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams2 = std::make_shared<IO::SDK::OrbitalParameters::EquinoctialElements>(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 345.0 * IO::SDK::Constants::DEG_RAD, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, startEpoch, IO::SDK::Frames::InertialFrames::GetICRF());

    IO::SDK::Body::Spacecraft::Spacecraft s{-189, "189test", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams1)};

    //Add gravity to forces model
    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};
    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s), forces);
    IO::SDK::Propagators::Propagator prop(s, integrator, IO::SDK::Time::Window(startEpoch, endEpoch));

    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    auto engine1 = s.GetEngine("sn1");

    std::vector<IO::SDK::Body::Spacecraft::Engine*> engines;
    engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine*>(engine1));

    IO::SDK::Maneuvers::PhasingManeuver phasingManeuver(engines, prop, 3, orbitalParams2);
    IO::SDK::Maneuvers::ApogeeHeightChangingManeuver finalManeuver(engines, prop, orbitalParams2->GetApogeeVector().Magnitude());
    phasingManeuver.SetNextManeuver(finalManeuver);

    prop.SetStandbyManeuver(&phasingManeuver);
    prop.Propagate();

    ASSERT_STREQ("2021-01-01 00:01:09.123576 (TDB)", phasingManeuver.GetManeuverWindow()->GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-01-03 17:32:21.960193 (TDB)", phasingManeuver.GetManeuverWindow()->GetEndDate().ToString().c_str());

    auto sv = prop.GetStateVectors().back();
    ASSERT_NEAR(42164000.0, sv.GetPerigeeVector().Magnitude(), 6);
    ASSERT_NEAR(0.0, sv.GetEccentricity(), 1E-06);
    ASSERT_DOUBLE_EQ(0.0, sv.GetInclination() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(0.0, sv.GetRightAscendingNodeLongitude() * IO::SDK::Constants::RAD_DEG);

    auto sv2= orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate());
    ASSERT_NEAR(279.02559168459368, sv.ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG, 1E-06);
    ASSERT_DOUBLE_EQ(80.991621690861436, sv.ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetMeanAnomaly() * IO::SDK::Constants::RAD_DEG);

    ASSERT_DOUBLE_EQ(90.017226571823784, orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(270.0, orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetMeanAnomaly() * IO::SDK::Constants::RAD_DEG);

    double longitude1 = sv.GetPeriapsisArgument() + sv.ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetMeanAnomaly();
    if (longitude1 > IO::SDK::Constants::_2PI)
    {
        longitude1 -= IO::SDK::Constants::_2PI;
    }

    double longitude2 = orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetPeriapsisArgument() +
                        orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetMeanAnomaly();
    if (longitude2 > IO::SDK::Constants::_2PI)
    {
        longitude2 -= IO::SDK::Constants::_2PI;
    }

    //At the end of maneuver two bodies are exactly at the same place w1+m1 = w2+m2 (all others parameters are equal)
    ASSERT_NEAR(longitude1, longitude2, 1E-6);

    //Check distance between two objects lower than 2m
    ASSERT_NEAR(0.0, (sv.ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetPosition() -
                      orbitalParams2->ToStateVector(finalManeuver.GetManeuverWindow()->GetEndDate()).GetPosition()).Magnitude(), 2.0);
}
