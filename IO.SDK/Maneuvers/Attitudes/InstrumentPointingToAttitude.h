/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INSTRUMENT_ALIGNED_TO_ATTITUDE_H
#define INSTRUMENT_ALIGNED_TO_ATTITUDE_H

#include <ManeuverBase.h>

namespace IO::SDK::Maneuvers::Attitudes
{
    class InstrumentPointingToAttitude final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        const IO::SDK::Body::Body *m_targetBody{nullptr};
        const IO::SDK::Sites::Site *m_targetSite{nullptr};
        const IO::SDK::Instruments::Instrument &m_instrument;

    protected:
        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::SDK::OrbitalParameters::StateOrientation 
         */
        IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

    public:
        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetBody
         */
        InstrumentPointingToAttitude(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator,
                                     const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Body::Body &targetBody);

        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetSite
         */
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator,
                                     const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Instruments::Instrument &instrument,
                                     const IO::SDK::Sites::Site &targetSite);

        /**
         * Construct a new instrument aligned to object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch
         * @instrument instrument
         * @param targetBody 
         */
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator,
                                     const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Instruments::Instrument &instrument,
                                     const IO::SDK::Body::Body &targetBody);

        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param minimumEpoch
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetSite
         */
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator,
                                     const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Instruments::Instrument &instrument,
                                     const IO::SDK::Sites::Site &targetSite);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };
}

#endif