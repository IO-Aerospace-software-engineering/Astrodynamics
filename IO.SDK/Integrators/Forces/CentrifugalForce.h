/**
 * @file CentrifugalForce.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef CENTRIFUGAL_FORCE_H
#define CENTRIFUGAL_FORCE_H

#include <Force.h>

namespace IO::SDK::Integrators::Forces
{
    /**
     * @brief Centrifugal force
     * 
     */
    class CentrifugalForce : public IO::SDK::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        CentrifugalForce(/* args */);
        ~CentrifugalForce();

        /**
         * @brief Apply force to body
         * 
         * @param body 
         * @param stateVector 
         * @return IO::SDK::Math::Vector3D 
         */
        IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif