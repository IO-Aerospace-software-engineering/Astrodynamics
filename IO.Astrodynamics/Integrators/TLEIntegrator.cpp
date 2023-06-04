/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TLEIntegrator.h>


IO::SDK::Integrators::TLEIntegrator::TLEIntegrator(const IO::SDK::OrbitalParameters::TLE &tle, const IO::SDK::Time::TimeSpan &stepDuration) : IO::SDK::Integrators::IntegratorBase(stepDuration), m_tle{tle}
{
}

IO::SDK::Integrators::TLEIntegrator::~TLEIntegrator()
= default;

IO::SDK::OrbitalParameters::StateVector IO::SDK::Integrators::TLEIntegrator::Integrate([[maybe_unused]]const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector)
{
    return m_tle.ToStateVector(stateVector.GetEpoch() + this->m_stepDuration);
}