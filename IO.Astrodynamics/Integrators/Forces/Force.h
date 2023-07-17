/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef FORCE_H
#define FORCE_H

#include <Vector3D.h>
#include <CelestialItem.h>
#include <StateVector.h>

namespace IO::Astrodynamics::Integrators::Forces {
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
         * @return IO::Astrodynamics::Math::Vector3D
         */
        virtual IO::Astrodynamics::Math::Vector3D Apply(const IO::Astrodynamics::Body::CelestialItem &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) = 0;

        virtual ~Force() = default;
    };

}

#endif