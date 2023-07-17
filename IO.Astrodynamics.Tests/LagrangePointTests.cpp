//
// Created by s.guillet on 07/07/2023.
//
#include <gtest/gtest.h>
#include <LagrangePoint.h>
#include <InertialFrames.h>
#include <StateVector.h>


TEST(LagrangePoint, CreateLagrangePoint)
{
    IO::Astrodynamics::Body::LagrangePoint l1(391);
    ASSERT_STREQ("L1", l1.GetName().c_str());
    ASSERT_DOUBLE_EQ(0.0, l1.GetMu());
    ASSERT_EQ(391, l1.GetId());
}

TEST(LagrangePoint, GetEphemeris)
{
    IO::Astrodynamics::Body::LagrangePoint l1(391);
    IO::Astrodynamics::Time::TDB epoch(std::chrono::duration<double>(0.0));
    auto res = l1.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::Ecliptic(), IO::Astrodynamics::AberrationsEnum::None, epoch);
    ASSERT_DOUBLE_EQ(265316694.670816, res.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(-1448527895.507656, res.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(1706.923545571044, res.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(298.1913805689489, res.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(54.841903612497966, res.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-0.0004202585222601307, res.GetVelocity().GetZ());
    ASSERT_STREQ("", res.GetFrame().ToCharArray());
    ASSERT_EQ(0, res.GetCenterOfMotion()->GetId());

//    Assert.Equal("L1", l1.Name);
//    Assert.Equal(391, l1.NaifId);
//    Assert.Equal(0.0, l1.Mass);
//    Assert.Equal(0.0, l1.GM);
//    Assert.Equal(3, l1.InitialOrbitalParameters.Observer.NaifId);
//    Assert.Equal(new Vector3(265316694.670816,  -1448527895.507656, 1706.923545571044), l1.InitialOrbitalParameters.ToStateVector().Position);
//    Assert.Equal(new Vector3(298.1913805689489, 54.841903612497966, -0.0004202585222601307), l1.InitialOrbitalParameters.ToStateVector().Velocity);
//    Assert.Equal(Frames.Frame.ECLIPTIC_J2000, l1.InitialOrbitalParameters.Frame);
//    Assert.Equal(DateTimeExtension.J2000, l1.InitialOrbitalParameters.Epoch);
}