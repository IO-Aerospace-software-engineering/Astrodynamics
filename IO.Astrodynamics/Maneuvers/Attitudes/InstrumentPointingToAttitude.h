/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INSTRUMENT_ALIGNED_TO_ATTITUDE_H
#define INSTRUMENT_ALIGNED_TO_ATTITUDE_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers::Attitudes
{
    class InstrumentPointingToAttitude final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        const IO::Astrodynamics::Body::Body *m_targetBody{nullptr};
        const IO::Astrodynamics::Sites::Site *m_targetSite{nullptr};
        const IO::Astrodynamics::Instruments::Instrument &m_instrument;

    protected:
        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        IO::Astrodynamics::OrbitalParameters::StateOrientation ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

    public:
        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetBody
         */
        InstrumentPointingToAttitude(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                     const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration, const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Body::Body &targetBody);

        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetSite
         */
        InstrumentPointingToAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                     const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration, const IO::Astrodynamics::Instruments::Instrument &instrument,
                                     const IO::Astrodynamics::Sites::Site &targetSite);

        /**
         * Construct a new instrument aligned to object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch
         * @instrument instrument
         * @param targetBody 
         */
        InstrumentPointingToAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                     const IO::Astrodynamics::Time::TDB &minimumEpoch, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration, const IO::Astrodynamics::Instruments::Instrument &instrument,
                                     const IO::Astrodynamics::Body::Body &targetBody);

        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param minimumEpoch
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetSite
         */
        InstrumentPointingToAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                                     const IO::Astrodynamics::Time::TDB &minimumEpoch, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration, const IO::Astrodynamics::Instruments::Instrument &instrument,
                                     const IO::Astrodynamics::Sites::Site &targetSite);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };
}

#endif