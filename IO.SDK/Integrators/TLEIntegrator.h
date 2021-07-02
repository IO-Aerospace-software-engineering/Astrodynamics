/**
 * @file TLEIntegrator.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-04-06
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef TLE_INTEGRATOR_H
#define TLE_INTEGRATOR_H
#include <IntegratorBase.h>
#include <TLE.h>
#include <TimeSpan.h>
#include <StateVector.h>
#include <Body.h>

namespace IO::SDK::Integrators
{
    class TLEIntegrator final : public IO::SDK::Integrators::IntegratorBase
    {
    private:
        const IO::SDK::OrbitalParameters::TLE& m_tle;

    public:
        TLEIntegrator(const IO::SDK::OrbitalParameters::TLE& tle,const IO::SDK::Time::TimeSpan& stepDuration);
        ~TLEIntegrator();
        IO::SDK::OrbitalParameters::StateVector Integrate(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif