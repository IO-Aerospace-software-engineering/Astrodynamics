/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <PerigeeHeightChangingManeuver.h>
#include <Constants.h>
#include <Parameters.h>

#include <utility>

IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                 IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator), m_targetHeight{targetHeight}
{
}

IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver::PerigeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                 IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight,
                                                                                 const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator,
                                                                                                                                                            minimumEpoch),
                                                                                                                           m_targetHeight{targetHeight}
{
}

void IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver::Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double vInit = maneuverPoint.ToStateVector().GetVelocity().Magnitude();
    double vFinal = std::sqrt(maneuverPoint.GetCenterOfMotion()->GetMu() *
                              ((2.0 / maneuverPoint.GetApogeeVector().Magnitude()) - (1.0 / ((maneuverPoint.GetApogeeVector().Magnitude() + m_targetHeight) / 2.0))));
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(
            m_spacecraft.Front.Rotate(ComputeOrientation(maneuverPoint).GetQuaternion().Conjugate()).Normalize() * std::abs(vFinal - vInit));
}

IO::Astrodynamics::OrbitalParameters::StateOrientation
IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    double deltaH = m_targetHeight - maneuverPoint.GetPerigeeVector().Magnitude();
    auto velocityVector = maneuverPoint.ToStateVector().GetVelocity().Normalize();

    //Check if it's a retrograde burn
    if (deltaH < 0.0)
    {
        velocityVector = velocityVector.Reverse();
    }

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{velocityVector.To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};
}

bool IO::Astrodynamics::Maneuvers::PerigeeHeightChangingManeuver::CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    if (orbitalParams.IsCircular() || (orbitalParams.GetMeanAnomaly() >= Constants::PI && orbitalParams.GetMeanAnomaly() < Constants::PI + Parameters::NodeDetectionAccuraccy))
    {
        return true;
    }

    return false;
}
