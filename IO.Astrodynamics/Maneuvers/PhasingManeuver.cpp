/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <PhasingManeuver.h>
#include <ConicOrbitalElements.h>
#include <Parameters.h>

IO::Astrodynamics::Maneuvers::PhasingManeuver::PhasingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                                     const unsigned int revolutionNumber, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator), m_revolutionsNumber{revolutionNumber}, m_targetOrbit{targetOrbit}
{
    if (!targetOrbit)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("A target orbit must be defined.");
    }

    m_targetOrbit = targetOrbit;
}

IO::Astrodynamics::Maneuvers::PhasingManeuver::PhasingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                                     const unsigned revolutionNumber, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit,
                                                     const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch),
                                                                                               m_revolutionsNumber{revolutionNumber}, m_targetOrbit{targetOrbit}
{
    if (!targetOrbit)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("A target orbit must be defined.");
    }

    m_targetOrbit = targetOrbit;
}

void IO::Astrodynamics::Maneuvers::PhasingManeuver::Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaTrueAnomaly = m_targetOrbit->GetTrueLongitude(maneuverPoint.GetEpoch()) - maneuverPoint.GetTrueLongitude();
    double e = maneuverPoint.GetEccentricity();

    double E = 2 * std::atan((std::sqrt((1 - e) / (1 + e))) * std::tan(deltaTrueAnomaly / 2.0));
    double T1 = maneuverPoint.GetPeriod().GetSeconds().count();
    double t = T1 / IO::Astrodynamics::Constants::_2PI * (E - e * std::sin(E));

    double T2 = T1 - t / m_revolutionsNumber;

    double u = maneuverPoint.GetCenterOfMotion()->GetMu();

    double a2 = std::pow((std::sqrt(u) * T2 / IO::Astrodynamics::Constants::_2PI), 2.0 / 3.0);

    double rp = maneuverPoint.GetPerigeeVector().Magnitude();
    double ra = 2 * a2 - rp;

    double h2 = std::sqrt(2 * u) * std::sqrt(ra * rp / (ra + rp));

    double dv = h2 / rp - m_targetOrbit->GetSpecificAngularMomentum().Magnitude() / rp;

    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion()).Normalize() * dv);

    m_maneuverHoldDuration = Time::TimeSpan(std::chrono::duration<double>(T2 * m_revolutionsNumber * 0.9));//Hold maneuver for 90% of maneuver total time
}

IO::Astrodynamics::OrbitalParameters::StateOrientation IO::Astrodynamics::Maneuvers::PhasingManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{

    auto velocityVector = maneuverPoint.ToStateVector().GetVelocity().Normalize();

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{m_spacecraft.Front.To(velocityVector), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};
}

bool IO::Astrodynamics::Maneuvers::PhasingManeuver::CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular())
    {
        m_maneuverPointTarget = orbitalParams.ToStateVector().GetPosition();
        m_maneuverPointUpdate = orbitalParams.GetEpoch();
        return true;
    }

    return ManeuverBase::CanExecute(orbitalParams);
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::PhasingManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    return orbitalParameters.GetPerigeeVector();
}
