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
#include <GeometryFinder.h>
#include <Plane.h>

using namespace std::chrono_literals;

IO::Astrodynamics::Body::CelestialBody::CelestialBody(const int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion)
        : IO::Astrodynamics::Body::CelestialItem(id, "",
                                                 ReadGM(id) /
                                                 IO::Astrodynamics::Constants::G,
                                                 centerOfMotion),
          m_BodyFixedFrame{""},
          m_J2{ReadJ2()}, m_J3{ReadJ3()}, m_J4{ReadJ4()}
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
        throw IO::Astrodynamics::Exception::SDKException("CelestialItem id" + std::to_string(id) + " can't be found");
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

IO::Astrodynamics::Body::CelestialBody::CelestialBody(const int id) : IO::Astrodynamics::Body::CelestialItem(id, "", ReadGM(id) / IO::Astrodynamics::Constants::G),
                                                                      m_BodyFixedFrame{""},
                                                                      m_J2{ReadJ2()}, m_J3{ReadJ3()}, m_J4{ReadJ4()}
{
    SpiceBoolean found;
    SpiceChar name[32];
    bodc2n_c(id, 32, name, &found);
    if (!found)
    {
        throw IO::Astrodynamics::Exception::SDKException("CelestialItem id" + std::to_string(id) + " can't be found");
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

    return IO::Astrodynamics::Math::Vector3D{res[0] * 1000.0, res[1] * 1000.0, res[2] * 1000.0};
}

double IO::Astrodynamics::Body::CelestialBody::GetFlattening() const
{
    auto radius = GetRadius();
    return (radius.GetX() - radius.GetZ()) / radius.GetX();
}

double IO::Astrodynamics::Body::CelestialBody::GetAngularVelocity(const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto initialVector = m_BodyFixedFrame.TransformVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Math::Vector3D::VectorX, epoch);
    auto finalVector = m_BodyFixedFrame.TransformVector(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::Math::Vector3D::VectorX,
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

double IO::Astrodynamics::Body::CelestialBody::ReadJ2() const
{
    return ReadJValue("J2");
}

double IO::Astrodynamics::Body::CelestialBody::ReadJ3() const
{
    return ReadJValue("J3");
}

double IO::Astrodynamics::Body::CelestialBody::ReadJ4() const
{
    return ReadJValue("J4");
}

double IO::Astrodynamics::Body::CelestialBody::ReadJValue(const char *valueName) const
{
    if (!bodfnd_c(m_id, valueName))
    {
        return std::numeric_limits<double>::quiet_NaN();
    }
    SpiceInt dim;
    SpiceDouble res[1];
    bodvcd_c(m_id, valueName, 1, &dim, res);
    if (dim == 0)
    {
        return std::numeric_limits<double>::quiet_NaN();
    }
    return res[0];
}


IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Body::CelestialBody::GetTrueSolarDay(IO::Astrodynamics::Time::TDB &epoch) const
{
    IO::Astrodynamics::Body::CelestialBody sun{10};
    auto sideralRotationPeriod = GetSideralRotationPeriod(epoch);
    auto eph0 = this->ReadEphemeris(Frames::InertialFrames::EclipticJ2000(), AberrationsEnum::LT, epoch, sun);
    auto eph1 = this->ReadEphemeris(Frames::InertialFrames::EclipticJ2000(), AberrationsEnum::LT, epoch + sideralRotationPeriod, sun);
    auto angle = eph0.GetPosition().GetAngle(eph1.GetPosition());
    return sideralRotationPeriod + angle / GetAngularVelocity(epoch);

}

IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements IO::Astrodynamics::Body::CelestialBody::ComputeGeosynchronousOrbit(double longitude, const Time::TDB &epoch) const
{
    //Get body fixed position
    Math::Vector3D bfixedPos = GetBodyFixedPosition(longitude, 0.0, epoch);

    //Generate state vector in body fixed frame
    auto celestialBody = std::dynamic_pointer_cast<IO::Astrodynamics::Body::CelestialBody>(GetSharedPointer());
    IO::Astrodynamics::OrbitalParameters::StateVector sv{celestialBody, bfixedPos, IO::Astrodynamics::Math::Vector3D(), epoch, GetBodyFixedFrame()};

    //Convert state vector in ICRF
    auto svICRF = sv.ToFrame(Frames::InertialFrames::ICRF());

    //Build conics elements
    IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conics{celestialBody, bfixedPos.Magnitude(), 0.0, svICRF.GetInclination(), svICRF.GetRightAscendingNodeLongitude(),
                                                                      std::fmod(svICRF.GetPeriapsisArgument() + svICRF.GetMeanAnomaly(), Constants::_2PI), 0.0, epoch,
                                                                      svICRF.GetFrame()};
    return conics;
}

IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements
IO::Astrodynamics::Body::CelestialBody::ComputeGeosynchronousOrbit(double longitude, double latitude, const IO::Astrodynamics::Time::TDB &epoch) const
{
    //Get body fixed position
    Math::Vector3D bfixedPos = GetBodyFixedPosition(longitude, latitude, epoch);
    double r = bfixedPos.Magnitude();

    //Transform position in ICRF
    auto icrfPos = GetBodyFixedFrame().TransformVector(Frames::InertialFrames::ICRF(), bfixedPos, epoch);

    //Transform celestial body rotation axis in ICRF
    auto icrfRotAxis = GetBodyFixedFrame().TransformVector(Frames::InertialFrames::ICRF(), Math::Vector3D::VectorZ, epoch);

    //Compute velocity vector in ICRF
    auto velocity = icrfRotAxis.CrossProduct(icrfPos).Normalize() * std::sqrt(m_mu / r);

    //Compute ICRF state vector
    auto celestialBody = std::dynamic_pointer_cast<IO::Astrodynamics::Body::CelestialBody>(GetSharedPointer());
    IO::Astrodynamics::OrbitalParameters::StateVector svICRF{celestialBody, icrfPos, velocity, epoch, Frames::InertialFrames::ICRF()};

    //Build conic elements
    IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conics{celestialBody, r, 0.0, svICRF.GetInclination(), svICRF.GetRightAscendingNodeLongitude(),
                                                                      std::fmod(svICRF.GetPeriapsisArgument() + svICRF.GetMeanAnomaly(), Constants::_2PI), 0.0, epoch,
                                                                      svICRF.GetFrame()};
    return conics;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Body::CelestialBody::GetBodyFixedPosition(double longitude, double latitude, const IO::Astrodynamics::Time::TDB &epoch) const
{
    //Compute radius
    auto t = GetSideralRotationPeriod(epoch);
    double t2 = t.GetSeconds().count() * t.GetSeconds().count();
    double r = cbrt((m_mu * t2) / (4 * Constants::PI * Constants::PI));

    //Convert to cartesian
    SpiceDouble bodyFixedLocation[3];
    latrec_c(r, longitude, latitude, bodyFixedLocation);

    //return position vector
    return Math::Vector3D{bodyFixedLocation[0], bodyFixedLocation[1], bodyFixedLocation[2]};
}


