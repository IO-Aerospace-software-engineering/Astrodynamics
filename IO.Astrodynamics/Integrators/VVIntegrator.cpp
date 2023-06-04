/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <VVIntegrator.h>
#include <InvalidArgumentException.h>

IO::Astrodynamics::Integrators::VVIntegrator::VVIntegrator(const IO::Astrodynamics::Time::TimeSpan &stepDuration) : IO::Astrodynamics::Integrators::IntegratorBase(stepDuration) {
    if (stepDuration.GetSeconds().count() <= 0) {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Step duration must be a positive number");
    }
}

IO::Astrodynamics::Integrators::VVIntegrator::VVIntegrator(const IO::Astrodynamics::Time::TimeSpan &stepDuration, std::vector<IO::Astrodynamics::Integrators::Forces::Force *> &forces) : VVIntegrator(
        stepDuration) {
    if (forces.empty()) {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Forces must have one force at least");
    }
    m_forces = forces;
}

IO::Astrodynamics::Integrators::VVIntegrator::~VVIntegrator()
= default;

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::Integrators::VVIntegrator::Integrate(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) {
    //Set initial parameters
    auto position = stateVector.GetPosition();
    auto velocity = stateVector.GetVelocity();
    auto nextEpoch = stateVector.GetEpoch() + m_stepDuration;

    if (!m_acceleration) {
        m_acceleration = ComputeAcceleration(body, stateVector);
    }

    velocity = velocity + (*m_acceleration * m_half_h);

    position = position + velocity * m_h;

    m_acceleration = ComputeAcceleration(body, IO::Astrodynamics::OrbitalParameters::StateVector(stateVector.GetCenterOfMotion(), position, velocity, nextEpoch, stateVector.GetFrame()));

    velocity = velocity + (*m_acceleration * m_half_h);

    //Create new state vector
    auto newSV = IO::Astrodynamics::OrbitalParameters::StateVector(stateVector.GetCenterOfMotion(), position, velocity, nextEpoch, stateVector.GetFrame());

    //Check if center of motion has changed
    if (newSV.GetPosition().Magnitude() > newSV.GetCenterOfMotion()->GetHillSphere())
    {
        return newSV.CheckAndUpdateCenterOfMotion();
    }

    return newSV;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Integrators::VVIntegrator::ComputeAcceleration(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const {
    IO::Astrodynamics::Math::Vector3D forceVector{};
    for (auto force: m_forces) {
        forceVector = forceVector + force->Apply(body, stateVector);
    }

    //Compute acceleration
    return forceVector / body.GetMass();
}