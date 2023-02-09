#include <InstrumentAlignedToAttitude.h>
#include <TDB.h>


IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::InstrumentAlignedToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                        IO::SDK::Propagators::Propagator &propagator,
                                                                                        const IO::SDK::Time::TimeSpan &attitudeHoldDuration,
                                                                                        const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Body::Body &targetBody)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, attitudeHoldDuration), m_instrument{instrument}, m_targetBody{&targetBody}
{

}

IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::InstrumentAlignedToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                        IO::SDK::Propagators::Propagator &propagator,
                                                                                        const IO::SDK::Time::TimeSpan &attitudeHoldDuration,
                                                                                        const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Sites::Site &targetSite)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, attitudeHoldDuration), m_instrument{instrument}, m_targetSite{&targetSite}
{

}

IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::InstrumentAlignedToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                        IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,
                                                                                        const IO::SDK::Time::TimeSpan &attitudeHoldDuration,
                                                                                        const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Body::Body &targetBody)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch, attitudeHoldDuration), m_instrument{instrument}, m_targetBody{&targetBody}
{

}

IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::InstrumentAlignedToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines,
                                                                                        IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,
                                                                                        const IO::SDK::Time::TimeSpan &attitudeHoldDuration,
                                                                                        const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Sites::Site &targetSite)
        : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch, attitudeHoldDuration), m_instrument{instrument}, m_targetSite{&targetSite}
{

}

bool IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    return true;
}

void IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>();
}

IO::SDK::OrbitalParameters::StateOrientation
IO::SDK::Maneuvers::Attitudes::InstrumentAlignedToAttitude::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::SDK::Math::Vector3D targetPosition;
    IO::SDK::Math::Vector3D spacecraftPosition = maneuverPoint.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetPosition();
    if (m_targetBody)
    {
        targetPosition = m_targetBody->ReadEphemeris(maneuverPoint.GetFrame(), AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                     *maneuverPoint.GetCenterOfMotion()).GetPosition();
    } else if (m_targetSite)
    {
        targetPosition = m_targetSite->GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), maneuverPoint.GetEpoch()).GetPosition();
        if (maneuverPoint.GetCenterOfMotion() != m_targetSite->GetBody())
        {
            targetPosition = targetPosition + m_targetSite->GetBody()->ReadEphemeris(maneuverPoint.GetFrame(), AberrationsEnum::LTS, maneuverPoint.GetEpoch(),
                                                                                     *maneuverPoint.GetCenterOfMotion()).GetPosition();
        }
    }

    auto targetPoint = targetPosition - spacecraftPosition;

    auto q = m_instrument.GetBoresightInSpacecraftFrame().Normalize().To(targetPoint.Normalize());

    return IO::SDK::OrbitalParameters::StateOrientation(q, IO::SDK::Math::Vector3D::Zero, maneuverPoint.GetEpoch(),
                                                        maneuverPoint.GetFrame());

}
