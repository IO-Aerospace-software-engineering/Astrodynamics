/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 8/17/23.
//

#ifndef IO_PLANE_H
#define IO_PLANE_H

#include <Vector3D.h>

namespace IO::Astrodynamics::Math
{
    class Plane
    {
    private:
        const IO::Astrodynamics::Math::Vector3D m_normal;
        const double m_distance;

    public:
        Plane(IO::Astrodynamics::Math::Vector3D normal, double distance);

        [[nodiscard]] inline IO::Astrodynamics::Math::Vector3D GetNormal() const
        { return m_normal; }

        [[nodiscard]] inline double GetDistance() const
        { return m_distance; }

        [[nodiscard]] double GetAngle(const IO::Astrodynamics::Math::Plane& plane) const;
        [[nodiscard]] double GetAngle(const IO::Astrodynamics::Math::Vector3D& vector3D) const;
    };

}

#endif //IO_PLANE_H
