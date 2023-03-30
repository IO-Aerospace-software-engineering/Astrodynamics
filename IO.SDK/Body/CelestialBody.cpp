/**
 * @file CelestialBody.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <limits>
#include <chrono>

#include "CelestialBody.h"
#include <StateVector.h>
#include <SpiceUsr.h>
#include <Constants.h>
#include <Aberrations.h>
#include <InertialFrames.h>
#include <InvalidArgumentException.h>

using namespace std::chrono_literals;

IO::SDK::Body::CelestialBody::CelestialBody(const int id, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion) : IO::SDK::Body::Body(id, "",
                                                                                                                                               ReadGM(id) /
                                                                                                                                               IO::SDK::Constants::G,
                                                                                                                                               centerOfMotion),
                                                                                                                           m_BodyFixedFrame{""}
{
    const_cast<double &>(m_sphereOfInfluence) = IO::SDK::Body::SphereOfInfluence(m_orbitalParametersAtEpoch->GetSemiMajorAxis(),
                                                                                 m_orbitalParametersAtEpoch->GetCenterOfMotion()->GetMu(), m_mu);
    const_cast<double &>(m_hillSphere) = IO::SDK::Body::HillSphere(m_orbitalParametersAtEpoch->GetSemiMajorAxis(), m_orbitalParametersAtEpoch->GetEccentricity(),
                                                                   m_orbitalParametersAtEpoch->GetCenterOfMotion()->GetMu(), m_mu);
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    const_cast<std::string &>(m_name) = name;
    const_cast<IO::SDK::Frames::BodyFixedFrames &>(m_BodyFixedFrame) = IO::SDK::Frames::BodyFixedFrames("IAU_" + std::string(name));
}

IO::SDK::Body::CelestialBody::CelestialBody(const int id) : IO::SDK::Body::Body(id, "", ReadGM(id) / IO::SDK::Constants::G),
                                                            m_BodyFixedFrame{""}
{
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    const_cast<std::string &>(m_name) = name;
    const_cast<IO::SDK::Frames::BodyFixedFrames &>(m_BodyFixedFrame) = IO::SDK::Frames::BodyFixedFrames("IAU_" + std::string(name));
    const_cast<double &>(m_sphereOfInfluence) = std::numeric_limits<double>::infinity();
    const_cast<double &>(m_hillSphere) = std::numeric_limits<double>::infinity();
}

double IO::SDK::Body::CelestialBody::GetSphereOfInfluence() const
{
    return m_sphereOfInfluence;
}

double IO::SDK::Body::CelestialBody::GetHillSphere() const
{
    return m_hillSphere;
}

double IO::SDK::Body::SphereOfInfluence(double a, double majorMass, double minorMass)
{
    return a * std::pow((minorMass / majorMass), 2.0 / 5.0);
}

double IO::SDK::Body::HillSphere(double a, double e, double majorMass, double minorMass)
{
    return a * (1 - e) * std::cbrt(minorMass / (3 * majorMass));
}

double IO::SDK::Body::CelestialBody::ReadGM(int id)
{
    SpiceInt dim;
    SpiceDouble res[1];
    bodvcd_c(id, "GM", 1, &dim, res);
    return res[0] * 1E+09;
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Body::CelestialBody::GetRelativeStatevector(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const
{
    if (*targetStateVector.GetCenterOfMotion() == *this)
    {
        return targetStateVector;
    }

    auto sv = ReadEphemeris(targetStateVector.GetFrame(), IO::SDK::AberrationsEnum::None, targetStateVector.GetEpoch(), *targetStateVector.GetCenterOfMotion());

    return IO::SDK::OrbitalParameters::StateVector(targetStateVector.GetCenterOfMotion(), targetStateVector.GetPosition() - sv.GetPosition(),
                                                   targetStateVector.GetVelocity() - sv.GetVelocity(), targetStateVector.GetEpoch(), targetStateVector.GetFrame());
}

bool IO::SDK::Body::CelestialBody::IsInSphereOfInfluence(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const
{
    auto sv = GetRelativeStatevector(targetStateVector);
    return sv.GetPosition().Magnitude() <= m_sphereOfInfluence;
}

bool IO::SDK::Body::CelestialBody::IsInHillSphere(const IO::SDK::OrbitalParameters::StateVector &targetStateVector) const
{
    auto sv = GetRelativeStatevector(targetStateVector);
    return sv.GetPosition().Magnitude() <= m_hillSphere;
}

const IO::SDK::Frames::BodyFixedFrames &IO::SDK::Body::CelestialBody::GetBodyFixedFrame() const
{
    return m_BodyFixedFrame;
}

IO::SDK::Math::Vector3D IO::SDK::Body::CelestialBody::GetRadius() const
{
    SpiceInt dim;
    SpiceDouble res[3];
    bodvcd_c(m_id, "RADII", 3, &dim, res);

    return IO::SDK::Math::Vector3D(res[0], res[1], res[2]);
}

double IO::SDK::Body::CelestialBody::GetFlattening() const
{
    auto radius = GetRadius();
    return (radius.GetX() - radius.GetZ()) / radius.GetX();
}

double IO::SDK::Body::CelestialBody::GetAngularVelocity(const IO::SDK::Time::TDB &epoch) const
{
    auto initialVector = m_BodyFixedFrame.TransformVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Math::Vector3D::VectorX, epoch);
    auto finalVector = m_BodyFixedFrame.TransformVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Math::Vector3D::VectorX, epoch + IO::SDK::Time::TimeSpan(1000.0s));
    return std::abs(finalVector.GetAngle(initialVector)) / 1000.0;
}

IO::SDK::Time::TimeSpan IO::SDK::Body::CelestialBody::GetSideralRotationPeriod(const IO::SDK::Time::TDB &epoch) const
{
    return IO::SDK::Time::TimeSpan(std::chrono::duration<double>(IO::SDK::Constants::_2PI / GetAngularVelocity(epoch)));
}

bool IO::SDK::Body::CelestialBody::IsSun(int celestialBodyId)
{
    return celestialBodyId == 10;
}

bool IO::SDK::Body::CelestialBody::IsPlanet(int celestialBodyId)
{
    return celestialBodyId > 100 && celestialBodyId < 1000 && (celestialBodyId % 100) == 99;
}

bool IO::SDK::Body::CelestialBody::IsAsteroid(int celestialBodyId)
{
    return celestialBodyId > 1000;
}

bool IO::SDK::Body::CelestialBody::IsMoon(int celestialBodyId)
{
    return celestialBodyId > 100 && celestialBodyId < 1000 && (celestialBodyId % 100) != 99;
}

int IO::SDK::Body::CelestialBody::FindCenterOfMotionId(int celestialBodyNaifId)
{
    if (IO::SDK::Body::CelestialBody::IsSun(celestialBodyNaifId) || IO::SDK::Body::CelestialBody::IsPlanet(celestialBodyNaifId) ||
        IO::SDK::Body::CelestialBody::IsAsteroid(celestialBodyNaifId))
    {
        return 10;
    }

    if (IO::SDK::Body::CelestialBody::IsMoon(celestialBodyNaifId))
    {
        return celestialBodyNaifId - (celestialBodyNaifId % 100) + 99;
    }

    throw IO::SDK::Exception::InvalidArgumentException(std::string("Invalid Naif Id : ") + std::to_string(celestialBodyNaifId));
}

