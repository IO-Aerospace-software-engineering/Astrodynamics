#include <PhasingManeuver.h>
#include <InvalidArgumentException.h>
#include <chrono>
#include <cmath>

IO::SDK::Maneuvers::PhasingManeuver::PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator *propagator, const uint revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit) : m_revolutionsNumber{revolutionNumber}, m_targetOrbit{targetOrbit}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
    if (!targetOrbit)
    {
        throw IO::SDK::Exception::InvalidArgumentException("A target orbit must be defined.");
    }

    m_targetOrbit = targetOrbit;
}

void IO::SDK::Maneuvers::PhasingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double targetHeight = DeltaHeight(maneuverPoint) + maneuverPoint.GetApogeeVector().Magnitude();
    double vInit = maneuverPoint.GetStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() * ((2.0 / maneuverPoint.GetPerigeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetPerigeeVector().Magnitude() + targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion()).Normalize() * std::abs(vFinal - vInit));
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::PhasingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{

    double deltaH = DeltaHeight(maneuverPoint);

    auto velocityVector = maneuverPoint.GetStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(velocityVector), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::PhasingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular())
    {
        return true;
    }

    bool isApproachingPerigee = IsApproachingPerigee(orbitalParams.GetStateVector());

    //If approaching is not initialized
    if (!m_isApproachingPerigee)
    {
        //is initialized
        m_isApproachingPerigee = std::make_unique<bool>(isApproachingPerigee);
        return false;
    }

    //If perigee approaching is changing, we passed maneuver point
    if (isApproachingPerigee != *m_isApproachingPerigee)
    {
        //Set new value
        *m_isApproachingPerigee = isApproachingPerigee;

        //If maneuver point is passed we return true
        return !*m_isApproachingPerigee;
    }

    return false;
}

bool IO::SDK::Maneuvers::PhasingManeuver::IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    //Angle between perigee vector and spacecraft velocity
    double dp = stateVector.GetPerigeeVector().DotProduct(stateVector.GetVelocity());

    //if < 90° we're in inbound sector
    if (dp > 0.0)
    {
        return true;
    }

    return false;
}

IO::SDK::Time::TimeSpan IO::SDK::Maneuvers::PhasingDuration(const uint k, const double n, const double deltaTrueAnomaly)
{
    return IO::SDK::Time::TimeSpan(std::chrono::duration<double>((2.0 * k * IO::SDK::Constants::PI + deltaTrueAnomaly) / (k * n)));
}

double IO::SDK::Maneuvers::PhasingSemiMajorAxis(const double gm, IO::SDK::Time::TimeSpan phasingDuration)
{
    return std::cbrt((gm * phasingDuration.GetSeconds().count() * phasingDuration.GetSeconds().count()) / (4 * IO::SDK::Constants::PI * IO::SDK::Constants::PI));
}

double IO::SDK::Maneuvers::PhasingManeuver::DeltaHeight(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    double deltaTrueAnomaly = orbitalParameters.GetStateVector().GetPosition().GetAngle(m_targetOrbit->GetStateVector(orbitalParameters.GetEpoch()).GetPosition());
    if (deltaTrueAnomaly < 0.0)
    {
        deltaTrueAnomaly += IO::SDK::Constants::_2PI;
    }
    return (PhasingSemiMajorAxis(orbitalParameters.GetCenterOfMotion()->GetMu(), PhasingDuration(m_revolutionsNumber, orbitalParameters.GetMeanMotion(), deltaTrueAnomaly)) - orbitalParameters.GetSemiMajorAxis()) * 2.0;
}