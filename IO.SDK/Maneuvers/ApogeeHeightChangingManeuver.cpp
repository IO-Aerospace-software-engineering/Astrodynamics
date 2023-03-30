/**
 * @file ApogeeHeightChangingManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <ApogeeHeightChangingManeuver.h>
#include <InvalidArgumentException.h>
#include <SDKException.h>
#include <cmath>

IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_targetHeight{targetHeight}
{
}

IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch), m_targetHeight{targetHeight}
{
}

void IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.GetStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() * ((2.0 / maneuverPoint.GetPerigeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetPerigeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(maneuverPoint.GetStateVector().GetVelocity().Normalize() * (vFinal - vInit));
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

    return IO::SDK::OrbitalParameters::StateOrientation(velocityVector.To(m_spacecraft.Front), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular() || orbitalParams.GetMeanAnomaly()<=Parameters::NodeDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

// bool IO::SDK::Maneuvers::ApogeeHeightChangingManeuver::IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
// {
//     //Angle between perigee vector and spacecraft velocity
//     double dp = stateVector.GetPerigeeVector().DotProduct(stateVector.GetVelocity());

//     //if < 90° we're in inbound sector
//     if (dp > 0.0)
//     {
//         return true;
//     }

//     return false;
// }
