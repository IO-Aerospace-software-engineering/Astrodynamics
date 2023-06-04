/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TowardObjectAttitude.h>

IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines,
                                                                          IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TimeSpan &attitudeHoldDuration,
                                                                          const IO::SDK::Body::Body &targetBody) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator,
                                                                                                                                                    attitudeHoldDuration),
                                                                                                                   m_targetBody{targetBody}
{
}

IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines,
                                                                          IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,
                                                                          const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Body::Body &targetBody)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch, attitudeHoldDuration), m_targetBody{targetBody}
{
}

void IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::Compute([[maybe_unused]]const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation
IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    auto targetStateVector = m_targetBody.ReadEphemeris(m_spacecraft.GetOrbitalParametersAtEpoch()->GetFrame(), IO::SDK::AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                        *maneuverPoint.GetCenterOfMotion());
    auto relativeStateVector = targetStateVector.GetPosition() - maneuverPoint.ToStateVector().GetPosition();
    return IO::SDK::OrbitalParameters::StateOrientation{relativeStateVector.Normalize().To(m_spacecraft.Front), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};
}

bool IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::CanExecute([[maybe_unused]]const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}