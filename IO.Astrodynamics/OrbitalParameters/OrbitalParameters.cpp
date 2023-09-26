/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Constants.h>
#include <StateVector.h>
#include <InertialFrames.h>
#include <Parameters.h>

#include <utility>
#include "OrbitalParameters.h"
#include "Plane.h"


IO::Astrodynamics::OrbitalParameters::OrbitalParameters::OrbitalParameters(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion,
                                                                           IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame) : m_centerOfMotion{
        centerOfMotion}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetCenterOfMotion() const
{
    return m_centerOfMotion;
}

bool IO::Astrodynamics::OrbitalParameters::OrbitalParameters::IsElliptical() const
{
    return GetEccentricity() < 1;
}

bool IO::Astrodynamics::OrbitalParameters::OrbitalParameters::IsParabolic() const
{
    return GetEccentricity() == 1;
}

bool IO::Astrodynamics::OrbitalParameters::OrbitalParameters::IsHyperbolic() const
{
    return GetEccentricity() > 1;
}

bool IO::Astrodynamics::OrbitalParameters::OrbitalParameters::IsCircular() const
{
    return GetEccentricity() < IO::Astrodynamics::Parameters::CircularEccentricityAccuraccy;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetMeanMotion() const
{
    if (this->IsHyperbolic())
    {
        return std::numeric_limits<double>::infinity();
    }

    return IO::Astrodynamics::Constants::_2PI / GetPeriod().GetSeconds().count();
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTimeToMeanAnomaly(double meanAnomalyTarget) const
{
    double delta{meanAnomalyTarget - GetMeanAnomaly()};
    while (delta < 0.0)
    {
        delta += IO::Astrodynamics::Constants::_2PI;
    }
    return IO::Astrodynamics::Time::TDB{
            std::chrono::duration<double>(m_epoch.GetSecondsFromJ2000().count() + std::fmod(delta, IO::Astrodynamics::Constants::_2PI) / GetMeanMotion())};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTimeToTrueAnomaly(double trueAnomalyTarget) const
{
    if (trueAnomalyTarget < 0.0)
    {
        trueAnomalyTarget += IO::Astrodynamics::Constants::_2PI;
    }
    double M = ConvertTrueAnomalyToMeanAnomaly(trueAnomalyTarget, GetEccentricity());

    return GetTimeToMeanAnomaly(M);
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ConvertTrueAnomalyToMeanAnomaly(double trueAnomaly, double eccentricity)
{
    //X = cos E
    double X = (eccentricity + std::cos(trueAnomaly)) / (1 + eccentricity * std::cos(trueAnomaly));
    double E = acos(X);
    double M = E - eccentricity * sin(E);

    if (trueAnomaly > Constants::PI)
    {
        M = Constants::_2PI - M;
    }
    return M;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetEccentricAnomaly(const IO::Astrodynamics::Time::TDB &epoch) const
{
    double M{this->GetMeanAnomaly(epoch)};
    double tmpE{M};
    double E{};

    while (std::abs(tmpE - E) > IO::Astrodynamics::Constants::ECCENTRIC_ANOMALY_ACCURACY)
    {
        E = tmpE;
        tmpE = M + GetEccentricity() * std::sin(E);
    }
    return E;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetMeanAnomaly(const IO::Astrodynamics::Time::TDB &epoch) const
{
    double M{GetMeanAnomaly() + GetMeanMotion() * (epoch - m_epoch).GetSeconds().count()};
    while (M < 0.0)
    {
        M += IO::Astrodynamics::Constants::_2PI;
    }
    return std::fmod(M, IO::Astrodynamics::Constants::_2PI);
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTrueAnomaly(const IO::Astrodynamics::Time::TDB &epoch) const
{
    double E{this->GetEccentricAnomaly(epoch)};
    double v = fmod(atan2(sqrt(1 - pow(GetEccentricity(), 2)) * sin(E), cos(E) - GetEccentricity()), IO::Astrodynamics::Constants::_2PI);
    while (v < 0.0)
    {
        v += IO::Astrodynamics::Constants::_2PI;
    }
    return std::fmod(v, IO::Astrodynamics::Constants::_2PI);
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTrueAnomaly() const
{
    return GetTrueAnomaly(m_epoch);
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ToStateVector() const
{
    return ToStateVector(m_epoch);
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetEpoch() const
{
    return m_epoch;
}

const IO::Astrodynamics::Frames::Frames &IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetFrame() const
{
    return m_frame;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetEccentricityVector() const
{
    auto sv = ToStateVector();
    return (sv.GetVelocity().CrossProduct(GetSpecificAngularMomentum()) / m_centerOfMotion->GetMu()) - (sv.GetPosition() / sv.GetPosition().Magnitude());
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetPerigeeVector() const
{
    return GetEccentricityVector().Normalize() * (GetSemiMajorAxis() * (1.0 - GetEccentricity()));
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetApogeeVector() const
{
    return GetEccentricityVector().Normalize().Reverse() * (GetSemiMajorAxis() * (1.0 + GetEccentricity()));
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ToStateVector(double trueAnomaly) const
{
    return ToStateVector(GetTimeToTrueAnomaly(trueAnomaly));
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetAscendingNodeVector() const
{
    //Compute asending node vector relative to body fixed
    auto v = IO::Astrodynamics::Math::Vector3D::VectorZ.CrossProduct(m_frame.TransformVector(m_centerOfMotion->GetBodyFixedFrame(), GetSpecificAngularMomentum(), m_epoch));

    //Transform ascending node vector to original orbital parameter frame
    return m_centerOfMotion->GetBodyFixedFrame().TransformVector(m_frame, v, m_epoch).Normalize();
}

IO::Astrodynamics::Coordinates::Equatorial IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ToEquatorialCoordinates() const
{
    auto sv = ToStateVector();
    if (sv.GetFrame() != IO::Astrodynamics::Frames::InertialFrames::ICRF())
    {
        sv = sv.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF());
    }

    ConstSpiceDouble rectan[3]{sv.GetPosition().GetX(), sv.GetPosition().GetY(), sv.GetPosition().GetZ()};
    double r, ra, dec;
    recrad_c(rectan, &r, &ra, &dec);

    return IO::Astrodynamics::Coordinates::Equatorial{ra, dec, r};
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetVelocityAtPerigee() const
{
    return ToStateVector(0.0).GetVelocity().Magnitude();
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetVelocityAtApogee() const
{
    return ToStateVector(IO::Astrodynamics::Constants::PI).GetVelocity().Magnitude();
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTrueLongitude() const
{
    double res = this->GetRightAscendingNodeLongitude() + this->GetPeriapsisArgument() + this->GetTrueAnomaly();
    while (res > IO::Astrodynamics::Constants::_2PI)
    {
        res -= IO::Astrodynamics::Constants::_2PI;
    }

    return res;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetMeanLongitude() const
{
    double res = this->GetRightAscendingNodeLongitude() + this->GetPeriapsisArgument() + this->GetMeanAnomaly();
    while (res > IO::Astrodynamics::Constants::_2PI)
    {
        res -= IO::Astrodynamics::Constants::_2PI;
    }

    return res;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetTrueLongitude(const IO::Astrodynamics::Time::TDB &epoch) const
{
    double res = this->GetRightAscendingNodeLongitude() + this->GetPeriapsisArgument() + this->GetTrueAnomaly(epoch);

    while (res > IO::Astrodynamics::Constants::_2PI)
    {
        res -= IO::Astrodynamics::Constants::_2PI;
    }

    return res;
}

double IO::Astrodynamics::OrbitalParameters::OrbitalParameters::GetMeanLongitude(const IO::Astrodynamics::Time::TDB &epoch) const
{
    double res = this->GetRightAscendingNodeLongitude() + this->GetPeriapsisArgument() + this->GetMeanAnomaly(epoch);

    while (res > IO::Astrodynamics::Constants::_2PI)
    {
        res -= IO::Astrodynamics::Constants::_2PI;
    }

    return res;
}

std::shared_ptr<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>
IO::Astrodynamics::OrbitalParameters::OrbitalParameters::CreateEarthHelioSynchronousOrbit(double semiMajorAxis, double eccentricity,
                                                                                          IO::Astrodynamics::Time::TDB &epochAtDescendingNode)
{
    //generate involved bodies
    auto sun = std::make_shared<Body::CelestialBody>(10);
    auto earth = std::make_shared<Body::CelestialBody>(399, sun);
    auto moon=std::make_shared<Body::CelestialBody>(301,earth);

    //Compute perigee radius
    double p = semiMajorAxis * (1 - eccentricity);

    //Get celestial body equatorial radius
    double eqRadius = earth->GetRadius().GetX();

    //Check if orbit doesn't hit celestial body
    if (p < eqRadius)
    {
        throw IO::Astrodynamics::Exception::SDKException("Invalid parameters, orbit perigee is lower than body radius");
    }

    //Compute inclination parameter
    double a72 = std::pow(semiMajorAxis, 3.5);
    double e2 = eccentricity * eccentricity;
    double e22 = (1 - e2) * (1 - e2);
    double sqrtGM = std::sqrt(earth->GetMu());
    double re2 = eqRadius * eqRadius;
    double i = std::acos((2 * a72 * e22 * earth->GetOrbitalParametersAtEpoch()->GetMeanMotion()) / (3 * sqrtGM * -earth->GetJ2() * re2));

    //Compute longitude of ascending node to orient orbit toward the sun
    IO::Astrodynamics::Math::Vector3D sunVector = earth->ReadEphemeris(Frames::InertialFrames::ICRF(), AberrationsEnum::LT, epochAtDescendingNode, *sun).GetPosition().Reverse();
    IO::Astrodynamics::Math::Plane sunPlane{IO::Astrodynamics::Math::Vector3D::VectorZ.CrossProduct(sunVector), 0.0};
    double raanLongitude = sunPlane.GetAngle(IO::Astrodynamics::Math::Vector3D::VectorY);

    if (sunVector.GetY() > 0.0)
    {
        raanLongitude *= -1.0;
    }

    //Make raan in range 0.0->2PI
    if (raanLongitude < 0.0)
    {
        raanLongitude += Constants::_2PI;
    }

    //Compute mean anomaly at ascending node
    double m = IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ConvertTrueAnomalyToMeanAnomaly(Constants::PI2 + Constants::_2PI, eccentricity);
    return std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth, p, eccentricity, i, raanLongitude, Constants::PI + Constants::PI2, m,
                                                                                        epochAtDescendingNode,
                                                                                        IO::Astrodynamics::Frames::InertialFrames::ICRF());
}

std::shared_ptr<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>
IO::Astrodynamics::OrbitalParameters::OrbitalParameters::CreateEarthPhasedHelioSynchronousOrbit(double eccentricity, IO::Astrodynamics::Time::TDB &epochAtDescendingNode,
                                                                                                int nbOrbitByDay)
{
    auto sun = std::make_shared<Body::CelestialBody>(10);
    Body::CelestialBody earth{399, sun};
    IO::Astrodynamics::Time::TimeSpan trueSolarDay{earth.GetTrueSolarDay(epochAtDescendingNode)};
    double T = trueSolarDay.GetSeconds().count() / nbOrbitByDay;
    double a = std::cbrt(((T * T) * earth.GetMu()) / (4 * Constants::PI * Constants::PI));
    return CreateEarthHelioSynchronousOrbit(a, eccentricity, epochAtDescendingNode);
}
