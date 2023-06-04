/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<RetrogradeAttitude.h>

IO::Astrodynamics::Maneuvers::Attitudes::RetrogradeAttitude::RetrogradeAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator,const IO::Astrodynamics::Time::TimeSpan& attitudeHoldDuration) : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator,attitudeHoldDuration)
{
}

IO::Astrodynamics::Maneuvers::Attitudes::RetrogradeAttitude::RetrogradeAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,const IO::Astrodynamics::Time::TimeSpan& attitudeHoldDuration) : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch,attitudeHoldDuration)
{
}

void IO::Astrodynamics::Maneuvers::Attitudes::RetrogradeAttitude::Compute([[maybe_unused]] const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>();
}

IO::Astrodynamics::OrbitalParameters::StateOrientation  IO::Astrodynamics::Maneuvers::Attitudes::RetrogradeAttitude::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::Astrodynamics::OrbitalParameters::StateOrientation{maneuverPoint.ToStateVector().GetVelocity().Normalize().Reverse().To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

bool  IO::Astrodynamics::Maneuvers::Attitudes::RetrogradeAttitude::CanExecute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}