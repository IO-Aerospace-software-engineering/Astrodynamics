/**
 * @file InstrumentPointingToAttitude.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.2
 * @date 2023-02-09
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef INSTRUMENT_ALIGNED_TO_ATTITUDE_H
#define INSTRUMENT_ALIGNED_TO_ATTITUDE_H

#include <vector>
#include <memory>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Propagator.h>
#include <Vector3D.h>
#include <Body.h>
#include <Site.h>

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
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
                                     const IO::SDK::Time::TimeSpan &attitudeHoldDuration, const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Body::Body &targetBody);

        /**
         * Construct a new instrument aligned to object
         * @param engines
         * @param propagator
         * @param attitudeHoldDuration
         * @instrument instrument
         * @param targetSite
         */
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
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
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
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
        InstrumentPointingToAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator,
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