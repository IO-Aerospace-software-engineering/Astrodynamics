/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <NadirAttitude.h>

IO::SDK::Maneuvers::Attitudes::NadirAttitude::NadirAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines, IO::SDK::Propagators::Propagator &propagator,
                                                            const IO::SDK::Time::TimeSpan &attitudeHoldDuration) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator,
                                                                                                                                                    attitudeHoldDuration)
{
}

IO::SDK::Maneuvers::Attitudes::NadirAttitude::NadirAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines, IO::SDK::Propagators::Propagator &propagator,
                                                            const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan &attitudeHoldDuration)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch, attitudeHoldDuration)
{
}

void IO::SDK::Maneuvers::Attitudes::NadirAttitude::Compute([[maybe_unused]] const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::Attitudes::NadirAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::SDK::OrbitalParameters::StateOrientation{maneuverPoint.ToStateVector().GetPosition().Normalize().Reverse().To(m_spacecraft.Front),
                                                        IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

bool IO::SDK::Maneuvers::Attitudes::NadirAttitude::CanExecute([[maybe_unused]]const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}