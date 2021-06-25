#include <ApogeeHeightChangingManeuver.h>
#include <InvalidArgumentException.h>
#include <SDKException.h>
#include <cmath>

IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight) : m_targetHeight{targetHeight}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight, const IO::SDK::Time::TDB &minimumEpoch) : m_targetHeight{targetHeight}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch)
{
}

void IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.GetStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() * ((2.0 / maneuverPoint.GetPerigeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetPerigeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion()).Normalize() * std::abs(vFinal - vInit));
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaH = m_targetHeight - maneuverPoint.GetApogeeVector().Magnitude();
    auto velocityVector = maneuverPoint.GetStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(velocityVector), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
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

bool IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
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
