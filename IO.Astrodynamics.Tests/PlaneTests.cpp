/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 8/17/23.
//
#include <gtest/gtest.h>
#include <Plane.h>

TEST(Plane, GetAngleFromPlane)
{
    IO::Astrodynamics::Math::Plane plane(IO::Astrodynamics::Math::Vector3D{0.0, 1.0, 0.0}, 0.0);
    IO::Astrodynamics::Math::Plane plane2(IO::Astrodynamics::Math::Vector3D{1.0, 0.0, 0.0}, 0.0);
    auto res = plane.GetAngle(plane2);
    ASSERT_DOUBLE_EQ(1.5707963267948966, res);
}

TEST(Plane, GetAngleFromVector)
{
    IO::Astrodynamics::Math::Vector3D vector(1.0, 0.0, 0.0);
    IO::Astrodynamics::Math::Plane plane(IO::Astrodynamics::Math::Vector3D{0.0, 1.0, 0.0}, 0.0);
    auto res = plane.GetAngle(vector);
    ASSERT_DOUBLE_EQ(0.0, res);
}

TEST(Plane, GetAngleFromVector2)
{
    IO::Astrodynamics::Math::Vector3D vector(1.0, 0.0, 0.0);
    IO::Astrodynamics::Math::Plane plane(IO::Astrodynamics::Math::Vector3D{1.0, 0.0, 0.0}, 0.0);
    auto res = plane.GetAngle(vector);
    ASSERT_DOUBLE_EQ(1.5707963267948966, res);
}

TEST(Plane, GetAngleFromVector3)
{
    IO::Astrodynamics::Math::Vector3D vector(-0.5, 1.0, 0.0);
    IO::Astrodynamics::Math::Plane plane(IO::Astrodynamics::Math::Vector3D{1.0, 0.0, 0.0}, 0.0);
    auto res = plane.GetAngle(vector);
    ASSERT_DOUBLE_EQ(-0.46364760900080609
}
