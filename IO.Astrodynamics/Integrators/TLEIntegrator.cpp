/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TLEIntegrator.h>


IO::Astrodynamics::Integrators::TLEIntegrator::TLEIntegrator(const IO::Astrodynamics::OrbitalParameters::TLE &tle, const IO::Astrodynamics::Time::TimeSpan &stepDuration) : IO::Astrodynamics::Integrators::IntegratorBase(stepDuration), m_tle{tle}
{
}

IO::Astrodynamics::Integrators::TLEIntegrator::~TLEIntegrator()
= default;

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::Integrators::TLEIntegrator::Integrate([[maybe_unused]]const IO::Astrodynamics::Body::CelestialItem &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector)
{
    return m_tle.ToStateVector(stateVector.GetEpoch() + this->m_stepDuration);
}