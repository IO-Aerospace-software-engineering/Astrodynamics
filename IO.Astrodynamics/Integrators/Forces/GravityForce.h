/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef GRAVITY_FORCE_H
#define GRAVITY_FORCE_H

#include <Force.h>
#include <CelestialBody.h>
#include <StateVector.h>

namespace IO::Astrodynamics::Integrators::Forces
{
    /**
     * @brief Gravity force
     * 
     */
    class GravityForce : public IO::Astrodynamics::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        /**
         * @brief 
         * 
         */
        GravityForce();
        ~GravityForce() override;

        /**
         * @brief Apply force
         * 
         * @param body 
         * @param stateVector 
         * @return IO::Astrodynamics::Math::Vector3D
         */
        IO::Astrodynamics::Math::Vector3D Apply(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) override;
    };
    /**
     * @brief 
     * 
     * @param m1 
     * @param m2 
     * @param distance 
     * @param u12 
     * @return IO::Astrodynamics::Math::Vector3D
     */
    IO::Astrodynamics::Math::Vector3D ComputeForce(double m1, double m2, double distance, const IO::Astrodynamics::Math::Vector3D &u12);

}

#endif