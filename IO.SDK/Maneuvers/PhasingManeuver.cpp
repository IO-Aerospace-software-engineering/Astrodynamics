/**
 * @file PhasingManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <PhasingManeuver.h>
#include <ConicOrbitalElements.h>
#include <Parameters.h>

IO::SDK::Maneuvers::PhasingManeuver::PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
                                                     const unsigned int revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_revolutionsNumber{revolutionNumber}, m_targetOrbit{targetOrbit}
{
    if (!targetOrbit)
    {
        throw IO::SDK::Exception::InvalidArgumentException("A target orbit must be defined.");
    }

    m_targetOrbit = targetOrbit;
}

IO::SDK::Maneuvers::PhasingManeuver::PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
                                                     const unsigned revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit,
                                                     const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch),
                                                                                               m_revolutionsNumber{revolutionNumber}, m_targetOrbit{targetOrbit}
{
    if (!targetOrbit)
    {
        throw IO::SDK::Exception::InvalidArgumentException("A target orbit must be defined.");
    }

    m_targetOrbit = targetOrbit;
}

void IO::SDK::Maneuvers::PhasingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaTrueAnomaly = m_targetOrbit->GetTrueLongitude(maneuverPoint.GetEpoch()) - maneuverPoint.GetTrueLongitude();
    double e = maneuverPoint.GetEccentricity();

    double E = 2 * std::atan((std::sqrt((1 - e) / (1 + e))) * std::tan(deltaTrueAnomaly / 2.0));
    double T1 = maneuverPoint.GetPeriod().GetSeconds().count();
    double t = T1 / IO::SDK::Constants::_2PI * (E - e * std::sin(E));

    double T2 = T1 - t / m_revolutionsNumber;

    double u = maneuverPoint.GetCenterOfMotion()->GetMu();

    double a2 = std::pow((std::sqrt(u) * T2 / IO::SDK::Constants::_2PI), 2.0 / 3.0);

    double rp = maneuverPoint.GetPerigeeVector().Magnitude();
    double ra = 2 * a2 - rp;

    double h2 = std::sqrt(2 * u) * std::sqrt(ra * rp / (ra + rp));

    double dv = h2 / rp - m_targetOrbit->GetSpecificAngularMomentum().Magnitude() / rp;

    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion()).Normalize() * dv);

    m_maneuverHoldDuration = Time::TimeSpan(std::chrono::duration<double>(T2 * m_revolutionsNumber * 0.9));//Hold maneuver for 90% of maneuver total time
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::PhasingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{

    auto velocityVector = maneuverPoint.GetStateVector().GetVelocity().Normalize();

    return IO::SDK::OrbitalParameters::StateOrientation{m_spacecraft.Front.To(velocityVector), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};
}

bool IO::SDK::Maneuvers::PhasingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular() || orbitalParams.GetMeanAnomaly() <= Parameters::NodeDetectionAccuraccy)
    {
        return true;
    }

    return false;
}
