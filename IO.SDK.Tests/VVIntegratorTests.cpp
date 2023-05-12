#include <gtest/gtest.h>
#include <VVIntegrator.h>
#include <TimeSpan.h>
#include <chrono>
#include <vector>
#include <iostream>
#include <InertialFrames.h>
#include <Spacecraft.h>
#include <StateOrientation.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(VVIntegrator, IntegrateGravity)
{
    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);

    IO::SDK::Integrators::VVIntegrator integrator(IO::SDK::Time::TimeSpan(1s), forces);
    IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.679537555216521E+07 Y = 1.327011135216045E+08 Z = 5.752533467064925E+07
    //  VX=-2.976558008982104E+01 VY=-5.075339952746913E+00 VZ=-2.200929976753953E+00
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);

    //  2459215.500000000 = A.D. 2021-Jan-01 00:00:00.0000 TDB [del_T=     69.183909 s]
    //  X =-2.068864826237993E+05 Y = 2.891146390982051E+05 Z = 1.515746884380044E+05
    //  VX=-8.366764389833921E-01 VY=-5.602543663174073E-01 VZ=-1.710459390585548E-01
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 8000.0, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif
    auto sv = integrator.Integrate(spc, IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 8000.0, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF()));
#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double, std::milli> ms_double = t2 - t1;
    std::cout << std::to_string(ms_double.count()) << " ms" << std::endl;
    ASSERT_TRUE(0.12 > ms_double.count());
#endif

    ASSERT_DOUBLE_EQ(6799995.6897156574, sv.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(7999.9982033708893, sv.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(-0.00069076103852024734, sv.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-8.620565236076974, sv.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(7999.9913360235832, sv.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-0.001381498705046451, sv.GetVelocity().GetZ());
}