/**
 * @file VVIntegrator.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <VVIntegrator.h>
#include <InvalidArgumentException.h>

IO::SDK::Integrators::VVIntegrator::VVIntegrator(const IO::SDK::Time::TimeSpan &stepDuration) : IO::SDK::Integrators::IntegratorBase(stepDuration) {
    if (stepDuration.GetSeconds().count() <= 0) {
        throw IO::SDK::Exception::InvalidArgumentException("Step duration must be a positive number");
    }
}

IO::SDK::Integrators::VVIntegrator::VVIntegrator(const IO::SDK::Time::TimeSpan &stepDuration, std::vector<IO::SDK::Integrators::Forces::Force *> &forces) : VVIntegrator(
        stepDuration) {
    if (forces.empty()) {
        throw IO::SDK::Exception::InvalidArgumentException("Forces must have one force at least");
    }
    m_forces = forces;
}

IO::SDK::Integrators::VVIntegrator::~VVIntegrator()
= default;

IO::SDK::OrbitalParameters::StateVector IO::SDK::Integrators::VVIntegrator::Integrate(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) {
    //Set initial parameters
    auto position = stateVector.GetPosition();
    auto velocity = stateVector.GetVelocity();
    auto nextEpoch = stateVector.GetEpoch() + m_stepDuration;

    if (!m_acceleration) {
        m_acceleration = ComputeAcceleration(body, stateVector);
    }

    velocity = velocity + (*m_acceleration * m_half_h);

    position = position + velocity * m_h;

    m_acceleration = ComputeAcceleration(body, IO::SDK::OrbitalParameters::StateVector(stateVector.GetCenterOfMotion(), position, velocity, nextEpoch, stateVector.GetFrame()));

    velocity = velocity + (*m_acceleration * m_half_h);

    //Create new state vector
    auto newSV = IO::SDK::OrbitalParameters::StateVector(stateVector.GetCenterOfMotion(), position, velocity, nextEpoch, stateVector.GetFrame());

    //Check if center of motion has changed
    if (newSV.GetPosition().Magnitude() > newSV.GetCenterOfMotion()->GetHillSphere())
    {
        return newSV.CheckAndUpdateCenterOfMotion();
    }

    return newSV;
}

IO::SDK::Math::Vector3D IO::SDK::Integrators::VVIntegrator::ComputeAcceleration(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) const {
    IO::SDK::Math::Vector3D forceVector{};
    for (auto force: m_forces) {
        forceVector = forceVector + force->Apply(body, stateVector);
    }

    //Compute acceleration
    return forceVector / body.GetMass();
}