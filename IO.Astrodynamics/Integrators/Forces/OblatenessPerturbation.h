/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 9/22/23.
//

#ifndef IO_OBLATENESSPERTURBATION_H
#define IO_OBLATENESSPERTURBATION_H


#include "Force.h"

namespace IO::Astrodynamics::Integrators::Forces
{
    /**
     * @class OblatenessPerturbation
     * @brief This class represents a force due to oblateness perturbations acting on a celestial body.
     *
     * It inherits from the Force class, and overrides the Apply() method to calculate and apply the force.
     */
    class OblatenessPerturbation : public IO::Astrodynamics::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        /**
         * @brief
         *
         */
        OblatenessPerturbation();
        ~OblatenessPerturbation() override;


        /**
         * @brief Apply the celestial item body to the given state vector.
         *
         * This function applies the celestial item to the given state vector, updating the state vector with the effects of the celestial item's gravitational field.
         *
         * @param body The celestial item to be applied.
         * @param stateVector The state vector to be updated.
         *
         * @return void
         */

        IO::Astrodynamics::Math::Vector3D Apply(const IO::Astrodynamics::Body::CelestialItem &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) override;
    };
}


#endif //IO_OBLATENESSPERTURBATION_H