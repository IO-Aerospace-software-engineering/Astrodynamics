/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <ApogeeHeightChangingManeuver.h>
#include <cmath>
#include <utility>
#include <Parameters.h>

IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const double targetHeight) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator), m_targetHeight{targetHeight}
{
}

IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::ApogeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const double targetHeight, const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch), m_targetHeight{targetHeight}
{
}

void IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.ToStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() * ((2.0 / maneuverPoint.GetPerigeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetPerigeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(maneuverPoint.ToStateVector().GetVelocity().Normalize() * (vFinal - vInit));
}

IO::Astrodynamics::OrbitalParameters::StateOrientation IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaH = m_targetHeight - maneuverPoint.GetApogeeVector().Magnitude();
    auto velocityVector = maneuverPoint.ToStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{velocityVector.To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

bool IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular() || orbitalParams.GetMeanAnomaly()<=Parameters::NodeDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

// bool IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver::IsApproachingPerigee(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const
// {
//     //Angle between perigee vector and Spacecraft velocity
//     double dp = stateVector.GetPerigeeVector().DotProduct(stateVector.GetVelocity());

//     //if < 90° we're in inbound sector
//     if (dp > 0.0)
//     {
//         return true;
//     }

//     return false;
// }