#include <PerigeeHeightChangingManeuver.h>
#include <InvalidArgumentException.h>
#include <SDKException.h>

IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight) : m_targetHeight{targetHeight}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight, const IO::SDK::Time::TDB &minimumEpoch) : m_targetHeight{targetHeight}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator,minimumEpoch)
{
}


void IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.GetStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() * ((2.0 / maneuverPoint.GetApogeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetApogeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion()).Normalize() * std::abs(vFinal - vInit));
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaH = m_targetHeight - maneuverPoint.GetPerigeeVector().Magnitude();
    auto velocityVector = maneuverPoint.GetStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(velocityVector), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular())
    {
        return true;
    }

    bool isApproachingApogee = IsApproachingApogee(orbitalParams.GetStateVector());

    //If approaching is not initialized
    if (!m_isApproachingApogee)
    {
        //is initialized
        m_isApproachingApogee = std::make_unique<bool>(isApproachingApogee);
        return false;
    }

    //If apogee approaching is changing, we passed maneuver point
    if (isApproachingApogee != *m_isApproachingApogee)
    {
        //Set new value
        *m_isApproachingApogee = isApproachingApogee;

        //If maneuver point is passed we return true
        return !*m_isApproachingApogee;
    }

    return false;
}

bool IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::IsApproachingApogee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    //Angle between apogee vector and spacecraft velocity
    double dp = stateVector.GetApogeeVector().DotProduct(stateVector.GetVelocity());

    //if < 90° we're in inbound sector
    if (dp > 0.0)
    {
        return true;
    }

    return false;
}