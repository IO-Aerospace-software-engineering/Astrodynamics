/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INTEGRATOR_BASE_H
#define INTEGRATOR_BASE_H

#include <StateVector.h>

namespace IO::Astrodynamics::Integrators
{
    /**
     * @brief Integrator base
     * 
     */
    class IntegratorBase
    {
    private:
    protected:
        const IO::Astrodynamics::Time::TimeSpan m_stepDuration{};
        const double m_h{};
        const double m_half_h{};

    public:
        /**
         * @brief Construct a new Integrator Base
         * 
         * @param deltat Step duration
         */
        explicit IntegratorBase(const IO::Astrodynamics::Time::TimeSpan &stepDuration);

        /**
         * @brief Integrate forces
         * 
         * @param spacecraft Vessel on which integration occurs
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        virtual IO::Astrodynamics::OrbitalParameters::StateVector Integrate(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) = 0;
    };

} // namespace IO::Astrodynamics::Propagators

#endif