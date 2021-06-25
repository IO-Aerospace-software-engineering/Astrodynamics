#include <TowardObjectAttitude.h>

IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TimeSpan& attitudeHoldDuration,const IO::SDK::Body::Body &targetBody) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator,attitudeHoldDuration), m_targetBody{targetBody}
{
}

IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::TowardObjectAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,const IO::SDK::Time::TimeSpan& attitudeHoldDuration, const IO::SDK::Body::Body &targetBody) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch,attitudeHoldDuration), m_targetBody{targetBody}
{
}

void IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    auto targetStateVector = m_targetBody.ReadEphemeris(m_spacecraft.GetOrbitalParametersAtEpoch()->GetFrame(), IO::SDK::AberrationsEnum::LTS, maneuverPoint.GetEpoch());
    auto relativeStatevector = targetStateVector.GetPosition() - maneuverPoint.GetStateVector().GetPosition();
    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(relativeStatevector.Normalize()), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::Attitudes::TowardObjectAttitude::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}