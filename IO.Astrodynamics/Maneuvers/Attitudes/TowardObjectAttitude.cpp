/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TowardObjectAttitude.h>

IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines,
                                                                          IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration,
                                                                          const IO::Astrodynamics::Body::CelestialItem &targetBody) : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator,
                                                                                                                                                                                 attitudeHoldDuration),
                                                                                                                                      m_targetBody{targetBody}
{
}

IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines,
                                                                          IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,
                                                                          const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration, const IO::Astrodynamics::Body::CelestialItem &targetBody)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch, attitudeHoldDuration), m_targetBody{targetBody}
{
}

void IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::Compute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>();
}

IO::Astrodynamics::OrbitalParameters::StateOrientation
IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    auto targetStateVector = m_targetBody.ReadEphemeris(m_spacecraft.GetOrbitalParametersAtEpoch()->GetFrame(), IO::Astrodynamics::AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                        *maneuverPoint.GetCenterOfMotion());
    auto relativeStateVector = targetStateVector.GetPosition() - maneuverPoint.ToStateVector().GetPosition();
    return IO::Astrodynamics::OrbitalParameters::StateOrientation{relativeStateVector.Normalize().To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};
}

bool IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::CanExecute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::Attitudes::TowardObjectAttitude::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    return IO::Astrodynamics::Math::Vector3D();
}
