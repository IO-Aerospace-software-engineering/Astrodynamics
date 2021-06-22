#include <NadirAttitude.h>

IO::SDK::Maneuvers::Attitudes::NadirAttitude::NadirAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

void IO::SDK::Maneuvers::Attitudes::NadirAttitude::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation  IO::SDK::Maneuvers::Attitudes::NadirAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(maneuverPoint.GetStateVector().GetPosition().Normalize().Reverse()), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool  IO::SDK::Maneuvers::Attitudes::NadirAttitude::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}