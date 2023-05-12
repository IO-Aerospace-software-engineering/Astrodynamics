/**
 * @file Force.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
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

namespace IO::SDK::Integrators::Forces {
    /**
     * @brief Force
     * 
     */
    class Force {
    private:
        /* data */
    public:
        /**
         * @brief Construct a new Force object
         * 
         */
        Force();

        /**
         * @brief Apply force to body
         * 
         * @param body 
         * @param stateVector 
         * @return IO::SDK::Math::Vector3D 
         */
        virtual IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) = 0;

        virtual ~Force() = default;
    };

}

#endif