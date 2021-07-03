/**
 * @file GravityForce.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef GRAVITY_FORCE_H
#define GRAVITY_FORCE_H

#include <Force.h>
#include <CelestialBody.h>
#include <StateVector.h>

namespace IO::SDK::Integrators::Forces
{
    /**
     * @brief Gravity force
     * 
     */
    class GravityForce : public IO::SDK::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        /**
         * @brief 
         * 
         */
        GravityForce();
        ~GravityForce();

        /**
         * @brief Apply force
         * 
         * @param body 
         * @param stateVector 
         * @return IO::SDK::Math::Vector3D 
         */
        IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };
    /**
     * @brief 
     * 
     * @param m1 
     * @param m2 
     * @param distance 
     * @param u12 
     * @return IO::SDK::Math::Vector3D 
     */
    IO::SDK::Math::Vector3D ComputeForce(const double m1, const double m2, const double distance, const IO::SDK::Math::Vector3D &u12);

}

#endif