#include <TLEIntegrator.h>

IO::SDK::Integrators::TLEIntegrator::TLEIntegrator(const IO::SDK::OrbitalParameters::TLE &tle, const IO::SDK::Time::TimeSpan &stepDuration) : IO::SDK::Integrators::IntegratorBase(stepDuration), m_tle{tle}
{
}

IO::SDK::Integrators::TLEIntegrator::~TLEIntegrator()
{
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Integrators::TLEIntegrator::Integrate(const IO::SDK::Body::Body &body __attribute__((unused)), const IO::SDK::OrbitalParameters::StateVector &stateVector)
{
    return m_tle.GetStateVector(stateVector.GetEpoch() + m_stepDuration);
}