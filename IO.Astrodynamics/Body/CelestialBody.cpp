/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <limits>
#include <chrono>

#include <CelestialBody.h>
#include <StateVector.h>
#include <SpiceUsr.h>
#include <Constants.h>
#include <Aberrations.h>
#include <InertialFrames.h>
#include <InvalidArgumentException.h>

using namespace std::chrono_literals;

IO::Astrodynamics::Body::CelestialBody::CelestialBody(const int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion) : IO::Astrodynamics::Body::Body(id, "",
                                                                                                                                                                             ReadGM(id) /
                                                                                                                                                                             IO::Astrodynamics::Constants::G,
                                                                                                                                                                             centerOfMotion),
                                                                                                                                               m_BodyFixedFrame{""}
{
    const_cast<double &>(m_sphereOfInfluence) = IO::Astrodynamics::Body::SphereOfInfluence(m_orbitalParametersAtEpoch->GetSemiMajorAxis(),
                                                                                           m_orbitalParametersAtEpoch->GetCenterOfMotion()->GetMu(), m_mu);
    const_cast<double &>(m_hillSphere) = IO::Astrodynamics::Body::HillSphere(m_orbitalParametersAtEpoch->GetSemiMajorAxis(), m_orbitalParametersAtEpoch->GetEccentricity(),
                                                                             m_orbitalParametersAtEpoch->GetCenterOfMotion()->GetMu(), m_mu);
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    if (!found)
    {
        throw IO::Astrodynamics::Exception::SDKException("Body id" + std::to_string(id) + " can't be found");
    }

    const_cast<std::string &>(m_name) = name;
    if (IsPlanet(id) || IsMoon(id) || IsSun(id))
    {
        std::string frame;
        if (id == 399)
        {
            frame = "ITRF93";
        } else
        {
            frame = "IAU_" + std::string(name);
        }

        const_cast<IO::Astrodynamics::Frames::BodyFixedFrames &>(m_BodyFixedFrame) = IO::Astrodynamics::Frames::BodyFixedFrames(frame);
    }
}

IO::Astrodynamics::Body::CelestialBody::CelestialBody(const int id) : IO::Astrodynamics::Body::Body(id, "", ReadGM(id) / IO::Astrodynamics::Constants::G),
                                                                      m_BodyFixedFrame{""}
{
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    if (!found)
    {
        throw IO::Astrodynamics::Exception::SDKException("Body id" + std::to_string(id) + " can't be found");
    }
    const_cast<std::string &>(m_name) = name;
    if (IsPlanet(id) || IsMoon(id) || IsSun(id))
    {
        std::string frame;
        if (id == 399)
        {
            frame = "ITRF93";
        } else
        {
            frame = "IAU_" + std::string(name);
        }

        const_cast<IO::Astrodynamics::Frames::BodyFixedFrames &>(m_BodyFixedFrame) = IO::Astrodynamics::Frames::BodyFixedFrames(frame);
    }
    const_cast<double &>(m_sphereOfInfluence) = std::numeric_limits<double>::infinity();
    const_cast<double &>(m_hillSphere) = std::numeric_limits<double>::infinity();
}

double IO::Astrodynamics::Body::CelestialBody::GetSphereOfInfluence() const
{
    return m_sphereOfInfluence;
}

double IO::Astrodynamics::Body::CelestialBody::GetHillSphere() const
{
    return m_hillSphere;
}

double IO::Astrodynamics::Body::SphereOfInfluence(double a, double majorMass, double minorMass)
{
    return a * std::pow((minorMass / majorMass), 2.0 / 5.0);
}

double IO::Astrodynamics::Body::HillSphere(double a, double e, double majorMass, double minorMass)
{
    return a * (1 - e) * std::cbrt(minorMass / (3 * majorMass));
}

double IO::Astrodynamics::Body::CelestialBody::ReadGM(int id)
{
    if (id == 0)
    {
        double solarSystemGM{};
        for (int i = 1; i <= 10; ++i)
        {
            solarSystemGM += ReadGM(i);
        }
        return solarSystemGM;
    }
    SpiceInt dim;
    SpiceDouble res[1];
    bodvcd_c(id, "GM", 1, &dim, res);
    return res[0] * 1E+09;
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Body::CelestialBody::GetRelativeStatevector(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const
{
    if (*targetStateVector.GetCenterOfMotion() == *this)
    {
        return targetStateVector;
    }

    auto sv = ReadEphemeris(targetStateVector.GetFrame(), IO::Astrodynamics::AberrationsEnum::None, targetStateVector.GetEpoch(), *targetStateVector.GetCenterOfMotion());

    return IO::Astrodynamics::OrbitalParameters::StateVector{targetStateVector.GetCenterOfMotion(), targetStateVector.GetPosition() - sv.GetPosition(),
                                                             targetStateVector.GetVelocity() - sv.GetVelocity(), targetStateVector.GetEpoch(), targetStateVector.GetFrame()};
}

bool IO::Astrodynamics::Body::CelestialBody::IsInSphereOfInfluence(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const
{
    auto sv = GetRelativeStatevector(targetStateVector);
    return sv.GetPosition().Magnitude() <= m_sphereOfInfluence;
}

bool IO::Astrodynamics::Body::CelestialBody::IsInHillSphere(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const
{
    auto sv = GetRelativeStatevector(targetStateVector);
    return sv.GetPosition().Magnitude() <= m_hillSphere;
}

const IO::Astrodynamics::Frames::BodyFixedFrames &IO::Astrodynamics::Body::CelestialBody::GetBodyFixedFrame() const
{
    return m_BodyFixedFrame;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Body::CelestialBody::GetRadius() const
{
    SpiceInt dim;
    SpiceDouble res[3];
    bodvcd_c(m_id, "RADII", 3, &dim, res);

    return IO::Astrodynamics::Math::Vector3D{res[0], res[1], res[2]};
}

double IO::Astrodynamics::Body::CelestialBody::GetFlattening() const
{
    auto radius = GetRadius();
    return (radius.GetX() - radius.GetZ()) / radius.GetX();
}

double IO::Astrodynamics::Body::CelestialBody::GetAngularVelocity(const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto initialVector = m_BodyFixedFrame.TransformVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Math::Vector3D::VectorX, epoch);
    auto finalVector = m_BodyFixedFrame.TransformVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::Math::Vector3D::VectorX,
                                                        epoch + IO::Astrodynamics::Time::TimeSpan(1000.0s));
    return std::abs(finalVector.GetAngle(initialVector)) / 1000.0;
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Body::CelestialBody::GetSideralRotationPeriod(const IO::Astrodynamics::Time::TDB &epoch) const
{
    return IO::Astrodynamics::Time::TimeSpan{std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI / GetAngularVelocity(epoch))};
}

bool IO::Astrodynamics::Body::CelestialBody::IsBarycenter(int celestialBodyId)
{
    return celestialBodyId >= 0 && celestialBodyId < 10;
}

bool IO::Astrodynamics::Body::CelestialBody::IsSun(int celestialBodyId)
{
    return celestialBodyId == 10;
}

bool IO::Astrodynamics::Body::CelestialBody::IsPlanet(int celestialBodyId)
{
    return celestialBodyId > 100 && celestialBodyId < 1000 && (celestialBodyId % 100) == 99;
}

bool IO::Astrodynamics::Body::CelestialBody::IsAsteroid(int celestialBodyId)
{
    return celestialBodyId > 1000;
}

bool IO::Astrodynamics::Body::CelestialBody::IsMoon(int celestialBodyId)
{
    return celestialBodyId > 100 && celestialBodyId < 1000 && (celestialBodyId % 100) != 99;
}

int IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(int celestialBodyNaifId)
{
    if (IO::Astrodynamics::Body::CelestialBody::IsSun(celestialBodyNaifId) || IO::Astrodynamics::Body::CelestialBody::IsBarycenter(celestialBodyNaifId) ||
        IO::Astrodynamics::Body::CelestialBody::IsAsteroid(celestialBodyNaifId))
    {
        return 0;
    }

    if (IO::Astrodynamics::Body::CelestialBody::IsPlanet(celestialBodyNaifId) || IO::Astrodynamics::Body::CelestialBody::IsMoon(celestialBodyNaifId))
    {
        return (int) (celestialBodyNaifId / 100);
    }

    if (IO::Astrodynamics::Body::CelestialBody::IsLagrangePoint(celestialBodyNaifId))
    {
        if (celestialBodyNaifId == 391 || celestialBodyNaifId == 392)
        {
            return (int) (celestialBodyNaifId / 100);
        }

        return 0;
    }

    throw IO::Astrodynamics::Exception::InvalidArgumentException(std::string("Invalid Naif Id : ") + std::to_string(celestialBodyNaifId));
}

int IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(int celestialBodyNaifId)
{
    if (IO::Astrodynamics::Body::CelestialBody::IsBarycenter(celestialBodyNaifId))
    {
        return 0;
    }
    if (IO::Astrodynamics::Body::CelestialBody::IsSun(celestialBodyNaifId) || IO::Astrodynamics::Body::CelestialBody::IsPlanet(celestialBodyNaifId) ||
        IO::Astrodynamics::Body::CelestialBody::IsAsteroid(celestialBodyNaifId))
    {
        return 10;
    }

    if (IO::Astrodynamics::Body::CelestialBody::IsMoon(celestialBodyNaifId))
    {
        return celestialBodyNaifId - (celestialBodyNaifId % 100) + 99;
    }

    if (IO::Astrodynamics::Body::CelestialBody::IsLagrangePoint(celestialBodyNaifId))
    {
        if (celestialBodyNaifId == 391 || celestialBodyNaifId == 392)
        {
            return (int) (celestialBodyNaifId / 100);
        }

        return 10;
    }

    throw IO::Astrodynamics::Exception::InvalidArgumentException(std::string("Invalid Naif Id : ") + std::to_string(celestialBodyNaifId));
}

bool IO::Astrodynamics::Body::CelestialBody::IsLagrangePoint(int celestialBodyId)
{
    return celestialBodyId == 391 || celestialBodyId == 392 || celestialBodyId == 393 || celestialBodyId == 394;
}

