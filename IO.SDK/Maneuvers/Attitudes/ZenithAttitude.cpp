#include<ZenithAttitude.h>

IO::SDK::Maneuvers::Attitudes::ZenithAttitude::ZenithAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

IO::SDK::Maneuvers::Attitudes::ZenithAttitude::ZenithAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch)
{
}

void IO::SDK::Maneuvers::Attitudes::ZenithAttitude::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation  IO::SDK::Maneuvers::Attitudes::ZenithAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(maneuverPoint.GetStateVector().GetPosition().Normalize()), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool  IO::SDK::Maneuvers::Attitudes::ZenithAttitude::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}