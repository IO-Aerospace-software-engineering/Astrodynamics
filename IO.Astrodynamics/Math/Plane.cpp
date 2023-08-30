/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 8/17/23.
//

#include <algorithm>
#include <Plane.h>
#include <cmath>

IO::Astrodynamics::Math::Plane::Plane(IO::Astrodynamics::Math::Vector3D normal, double distance) : m_normal{normal}, m_distance{distance}
{

}

double IO::Astrodynamics::Math::Plane::GetAngle(const IO::Astrodynamics::Math::Plane &plane) const
{
    return std::acos(m_normal.DotProduct(plane.m_normal) / (m_normal.Magnitude() * plane.m_normal.Magnitude()));
}

double IO::Astrodynamics::Math::Plane::GetAngle(const IO::Astrodynamics::Math::Vector3D &vector3D) const
{
    return std::asin(m_normal.DotProduct(vector3D) / (m_normal.Magnitude() * vector3D.Magnitude()));
}
