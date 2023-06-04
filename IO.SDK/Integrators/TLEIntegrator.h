/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TLE_INTEGRATOR_H
#define TLE_INTEGRATOR_H
#include <IntegratorBase.h>
#include <TLE.h>
#include <TimeSpan.h>
#include <StateVector.h>
#include <Body.h>
#include<Macros.h>

namespace IO::SDK::Integrators
{
    /**
     * @brief 
     * 
     */
    class TLEIntegrator final : public IO::SDK::Integrators::IntegratorBase
    {
    private:
        const IO::SDK::OrbitalParameters::TLE &m_tle;

    public:
        /**
         * @brief Construct a new TLEIntegrator object
         * 
         * @param tle 
         * @param stepDuration 
         */
        TLEIntegrator(const IO::SDK::OrbitalParameters::TLE &tle, const IO::SDK::Time::TimeSpan &stepDuration);
        ~TLEIntegrator();

        /**
         * @brief Integrate
         * 
         * @param body 
         * @param stateVector 
         * @return IO::SDK::OrbitalParameters::StateVector 
         */
        IO::SDK::OrbitalParameters::StateVector Integrate(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif