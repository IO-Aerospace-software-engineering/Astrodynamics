/**
 * @file Force.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef FORCE_H
#define FORCE_H

#include <Vector3D.h>
#include <Body.h>
#include <StateVector.h>

namespace IO::SDK::Integrators::Forces
{
    class Force
    {
    private:
        /* data */
    public:
        Force();
        virtual IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body,const IO::SDK::OrbitalParameters::StateVector& stateVector) = 0;
    };

}

#endif