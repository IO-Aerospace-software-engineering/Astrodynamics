/**
 * @file TLEIntegrator.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <TLEIntegrator.h>


IO::SDK::Integrators::TLEIntegrator::TLEIntegrator(const IO::SDK::OrbitalParameters::TLE &tle, const IO::SDK::Time::TimeSpan &stepDuration) : IO::SDK::Integrators::IntegratorBase(stepDuration), m_tle{tle}
{
}

IO::SDK::Integrators::TLEIntegrator::~TLEIntegrator()
{
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Integrators::TLEIntegrator::Integrate(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector)
{
    return m_tle.GetStateVector(stateVector.GetEpoch() + this->m_stepDuration);
}