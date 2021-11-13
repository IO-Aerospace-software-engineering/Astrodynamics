#include <iostream>
#include <fstream>
#include <memory>
#include <chrono>

#include <gtest/gtest.h>
#include <Propagator.h>
#include <Spacecraft.h>
#include <CelestialBody.h>
#include <IntegratorBase.h>
#include <VVIntegrator.h>
#include <TestsConstants.h>
#include <TLEIntegrator.h>

using namespace std::chrono_literals;

TEST(Propagator, Initialization)
{
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));
}

TEST(Propagator, FindNearestLowerValue)
{
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    //Propagator vector states empty
    auto nearestEmpty = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(99.5s));
    ASSERT_FALSE(nearestEmpty);

    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(101.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(102.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(103.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(104.0s), IO::SDK::Frames::InertialFrames::GetICRF()));

    auto nearest = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(103.5s));
    ASSERT_EQ(IO::SDK::Time::TDB(103.0s), nearest->GetEpoch());

    auto nearest1 = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(101.5s));
    ASSERT_EQ(IO::SDK::Time::TDB(101.0s), nearest1->GetEpoch());

    auto nearest2 = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(299.5s));
    ASSERT_EQ(IO::SDK::Time::TDB(104.0s), nearest2->GetEpoch());

    //Invalid value
    auto nearest3 = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(99.5s));
    ASSERT_FALSE(nearest3);
}

TEST(Propagator, EraseDataRange)
{
    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1.0s));
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(IO::SDK::Time::TDB(100.0s), IO::SDK::Time::TDB(200.0s)));

    //Propagator vector states empty
    auto nearestEmpty = pro.FindNearestLowerStateVector(IO::SDK::Time::TDB(99.5s));
    ASSERT_FALSE(nearestEmpty);

    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(101.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(102.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(103.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(104.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(105.0s), IO::SDK::Frames::InertialFrames::GetICRF()));
    pro.AddStateVector(IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(106.0s), IO::SDK::Frames::InertialFrames::GetICRF()));

    pro.EraseDataFromEpochToEnd(IO::SDK::Time::TDB(103.5s));

    ASSERT_EQ(3, pro.GetStateVectors().size());
    ASSERT_EQ(IO::SDK::Time::TDB(101.0s), pro.GetStateVectors()[0].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(103.0s), pro.GetStateVectors()[2].GetEpoch());

    pro.EraseDataFromEpochToEnd(IO::SDK::Time::TDB(103.0s));
    ASSERT_EQ(2, pro.GetStateVectors().size());
    ASSERT_EQ(IO::SDK::Time::TDB(101.0s), pro.GetStateVectors()[0].GetEpoch());
    ASSERT_EQ(IO::SDK::Time::TDB(102.0s), pro.GetStateVectors()[1].GetEpoch());
}

TEST(Propagator, PropagateVVIntegrator)
{
    auto step{IO::SDK::Time::TimeSpan(1.0s)};

    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::SDK::Integrators::VVIntegrator integrator(step, forces);

    IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.679537555216521E+07 Y = 1.327011135216045E+08 Z = 5.752533467064925E+07
    //  VX=-2.976558008982104E+01 VY=-5.075339952746913E+00 VZ=-2.200929976753953E+00
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.068864826237993E+05 Y = 2.891146390982051E+05 Z = 1.515746884380044E+05
    //  VX=-8.366764389833921E-01 VY=-5.602543663174073E-01 VZ=-1.710459390585548E-01
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 8000.0, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-125, "spc125", 1000.0, 3000.0, "missGravity", std::move(orbitalParams));

    IO::SDK::Propagators::Propagator pro(spc, integrator, IO::SDK::Time::Window(epoch, epoch + step * 100.0));

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif

    pro.Propagate();

#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double, std::milli> ms_double = t2 - t1;
    std::cout << std::to_string(ms_double.count()) << " ms" << std::endl;

    //Check performance
    ASSERT_TRUE(4.0 > ms_double.count());
#endif

    auto sv = pro.GetStateVectors()[1];

    ASSERT_DOUBLE_EQ(6799995.6897156574, sv.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(7999.9982033708893, sv.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(-0.00069076103852024734, sv.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-8.620565236076974, sv.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(7999.9913360235832, sv.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-0.001381498705046451, sv.GetVelocity().GetZ());
    ASSERT_DOUBLE_EQ(662731201.0, sv.GetEpoch().GetSecondsFromJ2000().count());

    //Read ephemeris
    sv = pro.GetStateVectors()[80];
    auto ephemerisSv = spc.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch + step * 80.0, *earth);
    ASSERT_EQ(ephemerisSv.GetEpoch(), sv.GetEpoch());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetX(), sv.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetY(), sv.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetZ(), sv.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetX(), sv.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetY(), sv.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetZ(), sv.GetVelocity().GetZ());
}

TEST(Propagator, PropagatorVsKepler)
{
    auto step{IO::SDK::Time::TimeSpan(1.0s)};
    IO::SDK::Time::TimeSpan duration(6447.0s);

    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::SDK::Integrators::VVIntegrator integrator(step, forces);

    IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    // auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.679537555216521E+07 Y = 1.327011135216045E+08 Z = 5.752533467064925E+07
    //  VX=-2.976558008982104E+01 VY=-5.075339952746913E+00 VZ=-2.200929976753953E+00
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a);

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.068864826237993E+05 Y = 2.891146390982051E+05 Z = 1.515746884380044E+05
    //  VX=-8.366764389833921E-01 VY=-5.602543663174073E-01 VZ=-1.710459390585548E-01
    // auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(a, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, v, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF());
    auto localOrbitalparams = dynamic_cast<IO::SDK::OrbitalParameters::StateVector *>(orbitalParams.get());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, "missGravity", std::move(orbitalParams));

    IO::SDK::Propagators::Propagator pro(spc, integrator, IO::SDK::Time::Window(epoch, epoch + duration));

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif

    pro.Propagate();
#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double, std::milli> ms_double = t2 - t1;
    std::cout << std::to_string(ms_double.count()) << " ms" << std::endl;
    //Check performance
    ASSERT_TRUE(25.0 > ms_double.count());
#endif

    const std::vector<IO::SDK::OrbitalParameters::StateVector> &propagationResults = pro.GetStateVectors();
    auto propagationResult = propagationResults[duration.GetSeconds().count() / step.GetSeconds().count()];
    auto keplerResults = localOrbitalparams->GetStateVector(epoch + duration);

    std::cout << "Delta dX : " << std::abs(keplerResults.GetPosition().GetX() - propagationResult.GetPosition().GetX()) << " m" << std::endl;
    std::cout << "Delta dY : " << std::abs(keplerResults.GetPosition().GetY() - propagationResult.GetPosition().GetY()) << " m" << std::endl;
    std::cout << "Delta dZ : " << std::abs(keplerResults.GetPosition().GetZ() - propagationResult.GetPosition().GetZ()) << " m" << std::endl;
    std::cout << "Delta dTotal : " << std::abs(keplerResults.GetPosition().Magnitude() - propagationResult.GetPosition().Magnitude()) << " m" << std::endl;

    std::cout << "Delta dvX : " << std::abs(keplerResults.GetVelocity().GetX() - propagationResult.GetVelocity().GetX()) << " m/s" << std::endl;
    std::cout << "Delta dvY : " << std::abs(keplerResults.GetVelocity().GetY() - propagationResult.GetVelocity().GetY()) << " m/s" << std::endl;
    std::cout << "Delta dvZ : " << std::abs(keplerResults.GetVelocity().GetZ() - propagationResult.GetVelocity().GetZ()) << " m/s" << std::endl;
    std::cout << "Delta dvTotal : " << std::abs(keplerResults.GetVelocity().Magnitude() - propagationResult.GetVelocity().Magnitude()) << " m/s" << std::endl;

    //Check epoch
    ASSERT_EQ(keplerResults.GetEpoch(), propagationResult.GetEpoch());

    //Check energy
    ASSERT_NEAR(pro.GetStateVectors()[0].GetSpecificOrbitalEnergy(), pro.GetStateVectors()[duration.GetSeconds().count() / step.GetSeconds().count() - 1.0].GetSpecificOrbitalEnergy(), 1E-05);

    ASSERT_NEAR(keplerResults.GetPosition().GetX(), propagationResult.GetPosition().GetX(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);
    ASSERT_NEAR(keplerResults.GetPosition().GetY(), propagationResult.GetPosition().GetY(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);
    ASSERT_NEAR(keplerResults.GetPosition().GetZ(), propagationResult.GetPosition().GetZ(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);

    ASSERT_NEAR(keplerResults.GetVelocity().GetX(), propagationResult.GetVelocity().GetX(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);
    ASSERT_NEAR(keplerResults.GetVelocity().GetY(), propagationResult.GetVelocity().GetY(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);
    ASSERT_NEAR(keplerResults.GetVelocity().GetZ(), propagationResult.GetVelocity().GetZ(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);

    auto orientationCoverage = spc.GetOrientationsCoverageWindow();
    ASSERT_STREQ("2021-01-01 00:00:00.000000 (TDB)", orientationCoverage.GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-01-01 00:00:00.000000 (TDB)", orientationCoverage.GetEndDate().ToString().c_str());

    //Analyse energy
    // std::ofstream myfile("SpecificEnergy.csv", std::ios_base::trunc);
    // myfile << std::fixed;
    // double i{0.0};
    // //Write energy file summary
    // for (auto &&sv : propagationResults)
    // {
    //     myfile << i * step.GetSeconds().count() << ";" << sv.GetSpecificOrbitalEnergy() << "\n";
    //     i++;
    // }
    // myfile.close();
}

TEST(Propagator, PropagatorVsKepler2)
{
    auto step{IO::SDK::Time::TimeSpan(1.0s)};
    IO::SDK::Time::TimeSpan duration(6447.0s);

    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::SDK::Integrators::VVIntegrator integrator(step, forces);

    IO::SDK::Time::TDB startEpoch("2021-Jan-01 00:00:00.0000 TDB");
    IO::SDK::Time::TDB endEpoch("2021-Jan-02 00:00:00.0000 TDB");
    // auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.679537555216521E+07 Y = 1.327011135216045E+08 Z = 5.752533467064925E+07
    //  VX=-2.976558008982104E+01 VY=-5.075339952746913E+00 VZ=-2.200929976753953E+00
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a);

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.068864826237993E+05 Y = 2.891146390982051E+05 Z = 1.515746884380044E+05
    //  VX=-8.366764389833921E-01 VY=-5.602543663174073E-01 VZ=-1.710459390585548E-01
    // auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth, 10000000.0, 0.3, 0.0, 0.0, 0.0, 0.0, startEpoch, IO::SDK::Frames::InertialFrames::GetICRF());
    auto localOrbitalparams = dynamic_cast<IO::SDK::OrbitalParameters::ConicOrbitalElements *>(orbitalParams.get());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, "misskepler2", std::move(orbitalParams));

    IO::SDK::Propagators::Propagator pro(spc, integrator, IO::SDK::Time::Window(startEpoch, endEpoch));

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif

    pro.Propagate();
#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double, std::milli> ms_double = t2 - t1;
    std::cout << std::to_string(ms_double.count()) << " ms" << std::endl;
    //Check performance
    ASSERT_TRUE(25.0 > ms_double.count());
#endif

    const std::vector<IO::SDK::OrbitalParameters::StateVector> &propagationResults = pro.GetStateVectors();
    auto propagationResult = propagationResults.back();
    auto keplerResults = localOrbitalparams->GetStateVector(endEpoch);

    std::cout << "Delta dX : " << std::abs(keplerResults.GetPosition().GetX() - propagationResult.GetPosition().GetX()) << " m" << std::endl;
    std::cout << "Delta dY : " << std::abs(keplerResults.GetPosition().GetY() - propagationResult.GetPosition().GetY()) << " m" << std::endl;
    std::cout << "Delta dZ : " << std::abs(keplerResults.GetPosition().GetZ() - propagationResult.GetPosition().GetZ()) << " m" << std::endl;
    std::cout << "Delta dTotal : " << std::abs(keplerResults.GetPosition().Magnitude() - propagationResult.GetPosition().Magnitude()) << " m" << std::endl;

    std::cout << "Delta dvX : " << std::abs(keplerResults.GetVelocity().GetX() - propagationResult.GetVelocity().GetX()) << " m/s" << std::endl;
    std::cout << "Delta dvY : " << std::abs(keplerResults.GetVelocity().GetY() - propagationResult.GetVelocity().GetY()) << " m/s" << std::endl;
    std::cout << "Delta dvZ : " << std::abs(keplerResults.GetVelocity().GetZ() - propagationResult.GetVelocity().GetZ()) << " m/s" << std::endl;
    std::cout << "Delta dvTotal : " << std::abs(keplerResults.GetVelocity().Magnitude() - propagationResult.GetVelocity().Magnitude()) << " m/s" << std::endl;

    //Check epoch
    ASSERT_EQ(keplerResults.GetEpoch(), propagationResult.GetEpoch());

    //Check energy
    ASSERT_NEAR(-13951014.677293681, propagationResult.GetSpecificOrbitalEnergy(), 1E-05);

    ASSERT_NEAR(keplerResults.GetPosition().GetX(), propagationResult.GetPosition().GetX(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);
    ASSERT_NEAR(keplerResults.GetPosition().GetY(), propagationResult.GetPosition().GetY(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);
    ASSERT_NEAR(keplerResults.GetPosition().GetZ(), propagationResult.GetPosition().GetZ(), IO::SDK::Test::Constants::DISTANCE_ACCURACY);

    ASSERT_NEAR(keplerResults.GetVelocity().GetX(), propagationResult.GetVelocity().GetX(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);
    ASSERT_NEAR(keplerResults.GetVelocity().GetY(), propagationResult.GetVelocity().GetY(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);
    ASSERT_NEAR(keplerResults.GetVelocity().GetZ(), propagationResult.GetVelocity().GetZ(), IO::SDK::Test::Constants::VELOCITY_ACCURACY);

    auto orientationCoverage = spc.GetOrientationsCoverageWindow();
    ASSERT_STREQ("2021-01-01 00:00:00.000000 (TDB)", orientationCoverage.GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-01-01 00:00:00.000000 (TDB)", orientationCoverage.GetEndDate().ToString().c_str());

    ASSERT_DOUBLE_EQ(9999999.5292096715, propagationResult.GetPerigeeVector().Magnitude());
    ASSERT_DOUBLE_EQ(0.30000006120264006, propagationResult.GetEccentricity());
    ASSERT_DOUBLE_EQ(keplerResults.GetInclination(), propagationResult.GetInclination());
    ASSERT_DOUBLE_EQ(keplerResults.GetRightAscendingNodeLongitude(), propagationResult.GetRightAscendingNodeLongitude());
    ASSERT_DOUBLE_EQ(6.283183583000322, propagationResult.GetPeriapsisArgument());

    //Analyse energy
    // std::ofstream myfile("SpecificEnergy.csv", std::ios_base::trunc);
    // myfile << std::fixed;
    // double i{0.0};
    // //Write energy file summary
    // for (auto &&sv : propagationResults)
    // {
    //     myfile << i * step.GetSeconds().count() << ";" << sv.GetSpecificOrbitalEnergy() << "\n";
    //     i++;
    // }
    // myfile.close();
}

TEST(Propagator, PropagateTLEIntegrator)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::string lines[3]{"ISS (ZARYA)", "1 25544U 98067A   21096.43776852  .00000912  00000-0  24825-4 0  9997", "2 25544  51.6463 337.6022 0002945 188.9422 344.4138 15.48860043277477"}; //2021-04-06 10:31:32.385783 TDB
    auto tleIntegrator = std::make_unique<IO::SDK::OrbitalParameters::TLE>(earth, lines);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> tle = std::make_unique<IO::SDK::OrbitalParameters::TLE>(earth, lines);
    IO::SDK::Time::TimeSpan step{60s};
    IO::SDK::Integrators::TLEIntegrator integrator(*tleIntegrator, step);
    IO::SDK::Body::Spacecraft::Spacecraft spc(-233, "issTLE", 1000.0, 3000.0, "MissTLEInteg", std::move(tle));

    IO::SDK::Time::TDB epoch{tleIntegrator->GetEpoch()};

    IO::SDK::Propagators::Propagator pro(spc, integrator, IO::SDK::Time::Window(epoch, epoch + step * 100.0));

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif

    pro.Propagate();

#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double, std::milli> ms_double = t2 - t1;
    std::cout << std::to_string(ms_double.count()) << " ms" << std::endl;
    //Check performance
    ASSERT_TRUE(0.9 > ms_double.count());
#endif

    //Read propagator results
    auto stateVector = pro.GetStateVectors()[1];
    ASSERT_DOUBLE_EQ(-6.2018228792385655E+06, stateVector.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(2.7695757618307383E+06, stateVector.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(2.4894250349276792E+05, stateVector.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-2.1459775555620154E+03, stateVector.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(-4.2501793473000989E+03, stateVector.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-6.003797568963455E+03, stateVector.GetVelocity().GetZ());

    ASSERT_EQ(epoch + step, stateVector.GetEpoch());

    // 2459310.994124835 = A.D. 2021-Apr-06 11:51:32.3858 TDB [del_T=     69.185672 s]
    //  X =-2.056539915554970E+03 Y = 4.698989685801117E+03 Z = 4.451870287080748E+03
    //  VX=-6.921346768046464E+00 VY= 9.156923051627522E-02 VZ=-3.288419444276052E+00
    //Read ephemeris results
    stateVector = pro.GetStateVectors()[80];
    auto ephemerisSv = spc.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch + step * 80.0, *earth);
    ASSERT_EQ(ephemerisSv.GetEpoch(), stateVector.GetEpoch());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetX(), stateVector.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetY(), stateVector.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetPosition().GetZ(), stateVector.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetX(), stateVector.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetY(), stateVector.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(ephemerisSv.GetVelocity().GetZ(), stateVector.GetVelocity().GetZ());

    auto latestAttitude = pro.GetLatestStateOrientation();
    ASSERT_TRUE(latestAttitude);
}

TEST(Propagator, EraseEmptyPropagator)
{
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::string lines[3]{"ISS (ZARYA)", "1 25544U 98067A   21096.43776852  .00000912  00000-0  24825-4 0  9997", "2 25544  51.6463 337.6022 0002945 188.9422 344.4138 15.48860043277477"}; //2021-04-06 10:31:32.385783 TDB
    auto tleIntegrator = std::make_unique<IO::SDK::OrbitalParameters::TLE>(earth, lines);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> tle = std::make_unique<IO::SDK::OrbitalParameters::TLE>(earth, lines);
    IO::SDK::Time::TimeSpan step{60s};
    IO::SDK::Integrators::TLEIntegrator integrator(*tleIntegrator, step);
    IO::SDK::Body::Spacecraft::Spacecraft spc(-233, "issTLE", 1000.0, 3000.0, "MissTLEInteg", std::move(tle));

    IO::SDK::Time::TDB epoch{tleIntegrator->GetEpoch()};

    IO::SDK::Propagators::Propagator pro(spc, integrator, IO::SDK::Time::Window(epoch, epoch + step * 100.0));

    pro.EraseDataFromEpochToEnd(epoch);
}