/**
 * @file StateVector.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <GravityForce.h>
#include "Helpers/Type.cpp"

IO::SDK::OrbitalParameters::StateVector::StateVector(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Math::Vector3D &position,
                                                     const IO::SDK::Math::Vector3D &velocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame)
        : OrbitalParameters(centerOfMotion, epoch, frame), m_position{position}, m_velocity{velocity}, m_momentum{position.CrossProduct(velocity)}
{
    //We define osculating elements only when velocity is defined
    if (velocity.Magnitude() > 0.0)
    {
        ConstSpiceDouble state[6]{position.GetX(), position.GetY(), position.GetZ(), velocity.GetX(), velocity.GetY(), velocity.GetZ()};
        SpiceDouble elts[SPICE_OSCLTX_NELTS]{};
        oscltx_c(state, epoch.GetSecondsFromJ2000().count(), centerOfMotion->GetMu(), elts);
        std::copy(std::begin(elts), std::end(elts), std::begin(m_osculatingElements));
    }
}


IO::SDK::OrbitalParameters::StateVector::StateVector(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, double state[6], const IO::SDK::Time::TDB &epoch,
                                                     const IO::SDK::Frames::Frames &frame) : StateVector(centerOfMotion, IO::SDK::Math::Vector3D(state[0], state[1], state[2]),
                                                                                                         IO::SDK::Math::Vector3D(state[3], state[4], state[5]), epoch, frame)
{
}

IO::SDK::OrbitalParameters::StateVector::StateVector(const StateVector &v) : OrbitalParameters(v.m_centerOfMotion, v.m_epoch, v.m_frame), m_position{v.m_position},
                                                                             m_velocity{v.m_velocity}, m_momentum{v.m_momentum}
{
    std::copy(std::begin(v.m_osculatingElements), std::end(v.m_osculatingElements), std::begin(m_osculatingElements));
}

IO::SDK::OrbitalParameters::StateVector &IO::SDK::OrbitalParameters::StateVector::operator=(const StateVector &other)
{
    if (this == &other)
        return *this;
    const_cast<IO::SDK::Math::Vector3D &>(m_position) = other.m_position;
    const_cast<IO::SDK::Math::Vector3D &>(m_velocity) = other.m_velocity;
    const_cast<IO::SDK::Math::Vector3D &>(m_momentum) = other.m_momentum;
    const_cast<IO::SDK::Time::TDB &>(m_epoch) = other.m_epoch;
    const_cast<IO::SDK::Frames::Frames &>(m_frame) = other.m_frame;
    m_osculatingElements = other.m_osculatingElements;

    return *this;
}

IO::SDK::Time::TimeSpan IO::SDK::OrbitalParameters::StateVector::GetPeriod() const
{
    return IO::SDK::Time::TimeSpan{std::chrono::duration<double>(m_osculatingElements[10])};
}

double IO::SDK::OrbitalParameters::StateVector::GetEccentricity() const
{
    return m_osculatingElements[1];
}

double IO::SDK::OrbitalParameters::StateVector::GetSemiMajorAxis() const
{
    return m_osculatingElements[9];
}

double IO::SDK::OrbitalParameters::StateVector::GetInclination() const
{
    return m_osculatingElements[2];
}

double IO::SDK::OrbitalParameters::StateVector::GetRightAscendingNodeLongitude() const
{
    return m_osculatingElements[3];
}

double IO::SDK::OrbitalParameters::StateVector::GetPeriapsisArgument() const
{
    return m_osculatingElements[4];
}

double IO::SDK::OrbitalParameters::StateVector::GetMeanAnomaly() const
{
    return m_osculatingElements[5];
}

double IO::SDK::OrbitalParameters::StateVector::GetTrueAnomaly() const
{
    return m_osculatingElements[8];
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::StateVector::GetStateVector(const IO::SDK::Time::TDB &epoch) const
{
    SpiceDouble state[6]{m_position.GetX(), m_position.GetY(), m_position.GetZ(), m_velocity.GetX(), m_velocity.GetY(), m_velocity.GetZ()};
    SpiceDouble stateProp[6]{};

    prop2b_c(m_centerOfMotion->GetMu(), state, (epoch - m_epoch).GetSeconds().count(), stateProp);

    return StateVector{m_centerOfMotion, stateProp, epoch, m_frame};
}

double IO::SDK::OrbitalParameters::StateVector::GetSpecificOrbitalEnergy() const
{
    return (std::pow(m_velocity.Magnitude(), 2.0) / 2) - (m_centerOfMotion->GetMu() / m_position.Magnitude());
}

bool IO::SDK::OrbitalParameters::StateVector::operator==(const IO::SDK::OrbitalParameters::StateVector &other) const
{
    return m_velocity == other.m_velocity && m_position == other.m_position && m_momentum == other.m_momentum && m_epoch == other.m_epoch;
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::StateVector::CheckAndUpdateCenterOfMotion() const
{
    //Current parameters
    IO::SDK::Math::Vector3D position{GetPosition()};
    IO::SDK::Math::Vector3D velocity{GetVelocity()};
    IO::SDK::Math::Vector3D force{IO::SDK::Integrators::Forces::ComputeForce(GetCenterOfMotion()->GetMass(), 1.0, position.Magnitude(), position.Normalize())};

    //New parameters
    std::shared_ptr<IO::SDK::Body::CelestialBody> newMajorBody{};
    IO::SDK::Math::Vector3D newPosition{};
    IO::SDK::Math::Vector3D newVelocity{};
    double greaterForce = force.Magnitude();

    //Each body is under sphere of influence of his major body
    //So Spacecraft is influenced by his center of motion and his parents
    //Eg. Sun->Earth->Moon->Spacecraft
    std::shared_ptr<IO::SDK::Body::Body> currentBody = GetCenterOfMotion();
    while (currentBody->GetOrbitalParametersAtEpoch())
    {
        //Compute vector state
        auto sv = currentBody->ReadEphemeris(m_frame, AberrationsEnum::None, m_epoch, *currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion());
        position = position + sv.GetPosition();
        velocity = velocity + sv.GetVelocity();

        //Compute force
        force = IO::SDK::Integrators::Forces::ComputeForce(currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(), 1.0, position.Magnitude(),
                                                           position.Normalize());

        if (force.Magnitude() > greaterForce)
        {
            newMajorBody = currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion();
            newPosition = position;
            newVelocity = velocity;
            greaterForce = force.Magnitude();
        }

        //Set next parent
        currentBody = currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion();
    }

    //Compute force induced by others satellites with the same center of motion
    for (auto &&sat: GetCenterOfMotion()->GetSatellites())
    {
        if (!IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(sat))
        {
            continue;
        }
        auto sv = sat->ReadEphemeris(m_frame, IO::SDK::AberrationsEnum::None, m_epoch);

        position = GetPosition() - sv.GetPosition();

        force = IO::SDK::Integrators::Forces::ComputeForce(sat->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(), 1.0, position.Magnitude(), position.Normalize());

        //Check if center of motion has changed
        if (force.Magnitude() > greaterForce)
        {
            newMajorBody = std::dynamic_pointer_cast<IO::SDK::Body::CelestialBody>(sat->GetSharedPointer());
            newPosition = position;
            newVelocity = GetVelocity() - sv.GetVelocity();
            greaterForce = force.Magnitude();
        }
    }

    //If the center of motion has changed
    if (newMajorBody)
    {
        return IO::SDK::OrbitalParameters::StateVector{newMajorBody, newPosition, newVelocity, m_epoch, m_frame};
    }

    return *this;
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::StateVector::ToFrame(const IO::SDK::Frames::Frames &frame) const
{
    if (frame == this->m_frame)
    {
        return *this;
    }

    auto mtx = m_frame.ToFrame6x6(frame, m_epoch);
    double v[6];
    v[0] = m_position.GetX();
    v[1] = m_position.GetY();
    v[2] = m_position.GetZ();
    v[3] = m_velocity.GetX();
    v[4] = m_velocity.GetY();
    v[5] = m_velocity.GetZ();

    double convertedMtx[6][6];
    for (size_t i = 0; i < 6; i++)
    {
        for (size_t j = 0; j < 6; j++)
        {
            convertedMtx[i][j] = mtx.GetValue(i, j);
        }
    }

    double nstate[6];
    mxvg_c(convertedMtx, v, 6, 6, nstate);

    return IO::SDK::OrbitalParameters::StateVector{m_centerOfMotion, nstate, m_epoch, frame};
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::StateVector::ToBodyFixedFrame() const
{
    return ToFrame(m_centerOfMotion->GetBodyFixedFrame());
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::StateVector::GetStateVector() const
{
    return *this;
}