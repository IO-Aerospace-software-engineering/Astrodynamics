/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TLE_INTEGRATOR_H
#define TLE_INTEGRATOR_H
#include <IntegratorBase.h>
#include <TLE.h>
#include <TimeSpan.h>
#include <StateVector.h>
#include <CelestialItem.h>
#include<Macros.h>

namespace IO::Astrodynamics::Integrators
{
    /**
     * @brief 
     * 
     */
    class TLEIntegrator final : public IO::Astrodynamics::Integrators::IntegratorBase
    {
    private:
        const IO::Astrodynamics::OrbitalParameters::TLE &m_tle;

    public:
        /**
         * @brief Construct a new TLEIntegrator object
         * 
         * @param tle 
         * @param stepDuration 
         */
        TLEIntegrator(const IO::Astrodynamics::OrbitalParameters::TLE &tle, const IO::Astrodynamics::Time::TimeSpan &stepDuration);
        ~TLEIntegrator();

        /**
         * @brief Integrate
         * 
         * @param body 
         * @param stateVector 
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        IO::Astrodynamics::OrbitalParameters::StateVector Integrate(const IO::Astrodynamics::Body::CelestialItem &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif