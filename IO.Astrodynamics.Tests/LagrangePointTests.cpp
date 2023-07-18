//
// Created by s.guillet on 07/07/2023.
//
#include <gtest/gtest.h>
#include <LagrangePoint.h>
#include <InertialFrames.h>
#include <StateVector.h>


TEST(LagrangePoint, CreateLagrangePoint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Body::LagrangePoint l1(391, earth);
    ASSERT_STREQ("L1", l1.GetName().c_str());
    ASSERT_DOUBLE_EQ(0.0, l1.GetMu());
    ASSERT_EQ(391, l1.GetId());
}

TEST(LagrangePoint, GetEphemeris)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(3, sun);
    IO::Astrodynamics::Body::LagrangePoint l1(391, earth);
    IO::Astrodynamics::Time::TDB epoch(std::chrono::duration<double>(0.0));
    auto res = l1.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::Ecliptic(), IO::Astrodynamics::AberrationsEnum::None, epoch);
    ASSERT_DOUBLE_EQ(265316694.670816, res.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(-1448527895.507656, res.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(1706.923545571044, res.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(298.1913805689489, res.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(54.841903612497966, res.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-0.0004202585222601307, res.GetVelocity().GetZ());
    ASSERT_STREQ("ECLIPJ2000", res.GetFrame().ToCharArray());
    ASSERT_EQ(3, res.GetCenterOfMotion()->GetId());
}