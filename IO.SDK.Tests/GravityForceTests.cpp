#include <gtest/gtest.h>
#include <GravityForce.h>
#include <Constants.h>
#include <Spacecraft.h>
#include <CelestialBody.h>
#include <chrono>
#include <memory>

using namespace std::chrono_literals;

TEST(GravityForce, ComputeForce)
{
    auto force = IO::SDK::Integrators::Forces::ComputeForce(3.986004418e14 / IO::SDK::Constants::G, 10.0, 7000000, IO::SDK::Math::Vector3D(1, 0, 0));
    ASSERT_EQ(IO::SDK::Math::Vector3D(-81.347028938775509, 0, 0), force);
}

TEST(GravityForce, ApplyToBody)
{
    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(10000000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 1000.0, 0.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, "missGravity", std::move(orbitalParams));

    auto force = gravityForce.Apply(spc, *dynamic_cast<IO::SDK::OrbitalParameters::StateVector *>(spc.GetOrbitalParametersAtEpoch().get()));
    ASSERT_EQ(IO::SDK::Math::Vector3D(-3986.0043543609595, 0, 0), force);
}

TEST(GravityForce, ApplyToBodyWithSatellites)
{
    IO::SDK::Integrators::Forces::GravityForce gravityForce;
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
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, "missGravity", std::move(orbitalParams));

    auto force = gravityForce.Apply(spc, *dynamic_cast<IO::SDK::OrbitalParameters::StateVector *>(spc.GetOrbitalParametersAtEpoch().get()));

    ASSERT_EQ(IO::SDK::Math::Vector3D(-8620.5686852713916, -3.59325822164271, -1.3815220770404948), force);
}