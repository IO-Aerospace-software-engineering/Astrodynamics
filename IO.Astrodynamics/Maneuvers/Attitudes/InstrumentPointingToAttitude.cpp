/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <InstrumentPointingToAttitude.h>
#include <InertialFrames.h>

IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::InstrumentPointingToAttitude(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                          IO::Astrodynamics::Propagators::Propagator &propagator,
                                                                                          const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration,
                                                                                          const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Body::CelestialItem &targetBody)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, attitudeHoldDuration), m_targetBody{&targetBody},m_instrument{instrument}
{

}

IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::InstrumentPointingToAttitude(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                          IO::Astrodynamics::Propagators::Propagator &propagator,
                                                                                          const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration,
                                                                                          const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Sites::Site &targetSite)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, attitudeHoldDuration), m_targetSite{&targetSite}, m_instrument{instrument}
{

}

IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::InstrumentPointingToAttitude(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                          IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,
                                                                                          const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration,
                                                                                          const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Body::CelestialItem &targetBody)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch, attitudeHoldDuration), m_targetBody{&targetBody}, m_instrument{instrument}
{

}

IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::InstrumentPointingToAttitude(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines,
                                                                                          IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,
                                                                                          const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration,
                                                                                          const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Sites::Site &targetSite)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch, attitudeHoldDuration), m_targetSite{&targetSite}, m_instrument{instrument}
{

}

bool IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::CanExecute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}

void IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::Compute([[maybe_unused]]const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>();
}

IO::Astrodynamics::OrbitalParameters::StateOrientation
IO::Astrodynamics::Maneuvers::Attitudes::InstrumentPointingToAttitude::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::Astrodynamics::Math::Vector3D targetPosition;
    IO::Astrodynamics::Math::Vector3D spacecraftPosition = maneuverPoint.ToStateVector().ToFrame(IO::Astrodynamics::Frames::InertialFrames::GetICRF()).GetPosition();
    if (m_targetBody)
    {
        targetPosition = m_targetBody->ReadEphemeris(maneuverPoint.GetFrame(), AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                     *maneuverPoint.GetCenterOfMotion()).GetPosition();
    } else if (m_targetSite)
    {
        targetPosition = m_targetSite->GetStateVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), maneuverPoint.GetEpoch()).GetPosition();
        if (maneuverPoint.GetCenterOfMotion() != m_targetSite->GetBody())
        {
            targetPosition = targetPosition + m_targetSite->GetBody()->ReadEphemeris(maneuverPoint.GetFrame(), AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                                                     *maneuverPoint.GetCenterOfMotion()).GetPosition();
        }
    }

    auto targetOrientation = (targetPosition - spacecraftPosition).Normalize();

    auto q = targetOrientation.To(m_instrument.GetBoresightInSpacecraftFrame().Normalize());

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{q, IO::Astrodynamics::Math::Vector3D::Zero, maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame()};

}
