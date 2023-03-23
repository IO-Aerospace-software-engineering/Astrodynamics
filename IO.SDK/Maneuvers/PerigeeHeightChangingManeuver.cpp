/**
 * @file PerigeeHeightChangingManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <PerigeeHeightChangingManeuver.h>
#include <InvalidArgumentException.h>
#include <SDKException.h>

IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                 IO::SDK::Propagators::Propagator &propagator, const double targetHeight)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_targetHeight{targetHeight}
{
}

IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                 IO::SDK::Propagators::Propagator &propagator, const double targetHeight,
                                                                                 const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator,
                                                                                                                                                            minimumEpoch),
                                                                                                                           m_targetHeight{targetHeight}
{
}

void IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.GetStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() *
                              ((2.0 / maneuverPoint.GetApogeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetApogeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion().Conjugate()).Normalize() * std::abs(vFinal - vInit));
}

IO::SDK::OrbitalParameters::StateOrientation
IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaH = m_targetHeight - maneuverPoint.GetPerigeeVector().Magnitude();
    auto velocityVector = maneuverPoint.GetStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::SDK::OrbitalParameters::StateOrientation(velocityVector.To(m_spacecraft.Front), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::PerigeeHeightChangingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular() || (orbitalParams.GetMeanAnomaly() >= Constants::PI && orbitalParams.GetMeanAnomaly() < Constants::PI + Parameters::NodeDetectionAccuraccy))
    {
        return true;
    }

    return false;
}
