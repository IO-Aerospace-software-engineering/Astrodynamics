/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Type.h>
#include "StateVector.h"

IO::Astrodynamics::OrbitalParameters::StateVector::StateVector(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const IO::Astrodynamics::Math::Vector3D &position,
                                                     const IO::Astrodynamics::Math::Vector3D &velocity, const IO::Astrodynamics::Time::TDB &epoch, const IO::Astrodynamics::Frames::Frames &frame)
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


IO::Astrodynamics::OrbitalParameters::StateVector::StateVector(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, double state[6], const IO::Astrodynamics::Time::TDB &epoch,
                                                     const IO::Astrodynamics::Frames::Frames &frame) : StateVector(centerOfMotion, IO::Astrodynamics::Math::Vector3D(state[0], state[1], state[2]),
                                                                                                         IO::Astrodynamics::Math::Vector3D(state[3], state[4], state[5]), epoch, frame)
{
}

IO::Astrodynamics::OrbitalParameters::StateVector::StateVector(const StateVector &v) : OrbitalParameters(v.m_centerOfMotion, v.m_epoch, v.m_frame), m_position{v.m_position},
                                                                             m_velocity{v.m_velocity}, m_momentum{v.m_momentum}
{
    std::copy(std::begin(v.m_osculatingElements), std::end(v.m_osculatingElements), std::begin(m_osculatingElements));
}

IO::Astrodynamics::OrbitalParameters::StateVector &IO::Astrodynamics::OrbitalParameters::StateVector::operator=(const StateVector &other)
{
    if (this == &other)
        return *this;
    const_cast<IO::Astrodynamics::Math::Vector3D &>(m_position) = other.m_position;
    const_cast<IO::Astrodynamics::Math::Vector3D &>(m_velocity) = other.m_velocity;
    const_cast<IO::Astrodynamics::Math::Vector3D &>(m_momentum) = other.m_momentum;
    const_cast<IO::Astrodynamics::Time::TDB &>(m_epoch) = other.m_epoch;
    const_cast<IO::Astrodynamics::Frames::Frames &>(m_frame) = other.m_frame;
    m_osculatingElements = other.m_osculatingElements;

    return *this;
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::OrbitalParameters::StateVector::GetPeriod() const
{
    return IO::Astrodynamics::Time::TimeSpan{std::chrono::duration<double>(m_osculatingElements[10])};
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetEccentricity() const
{
    return m_osculatingElements[1];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetSemiMajorAxis() const
{
    return m_osculatingElements[9];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetInclination() const
{
    return m_osculatingElements[2];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetRightAscendingNodeLongitude() const
{
    return m_osculatingElements[3];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetPeriapsisArgument() const
{
    return m_osculatingElements[4];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetMeanAnomaly() const
{
    return m_osculatingElements[5];
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetTrueAnomaly() const
{
    return m_osculatingElements[8];
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::StateVector::ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble state[6]{m_position.GetX(), m_position.GetY(), m_position.GetZ(), m_velocity.GetX(), m_velocity.GetY(), m_velocity.GetZ()};
    SpiceDouble stateProp[6]{};

    prop2b_c(m_centerOfMotion->GetMu(), state, (epoch - m_epoch).GetSeconds().count(), stateProp);

    return StateVector{m_centerOfMotion, stateProp, epoch, m_frame};
}

double IO::Astrodynamics::OrbitalParameters::StateVector::GetSpecificOrbitalEnergy() const
{
    return (std::pow(m_velocity.Magnitude(), 2.0) / 2) - (m_centerOfMotion->GetMu() / m_position.Magnitude());
}

bool IO::Astrodynamics::OrbitalParameters::StateVector::operator==(const IO::Astrodynamics::OrbitalParameters::StateVector &other) const
{
    return m_velocity == other.m_velocity && m_position == other.m_position && m_momentum == other.m_momentum && m_epoch == other.m_epoch;
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::StateVector::ToFrame(const IO::Astrodynamics::Frames::Frames &frame) const
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

    return IO::Astrodynamics::OrbitalParameters::StateVector{m_centerOfMotion, nstate, m_epoch, frame};
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::StateVector::ToBodyFixedFrame() const
{
    return ToFrame(m_centerOfMotion->GetBodyFixedFrame());
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::StateVector::ToStateVector() const
{
    return *this;
}