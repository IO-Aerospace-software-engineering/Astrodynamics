/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef PROGRADE_ATTITUDE_H
#define PROGRADE_ATTITUDE_H

#include <ManeuverBase.h>

namespace IO::SDK::Maneuvers::Attitudes
{
    class ProgradeAttitude final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        /* data */

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
         * @brief Construct a new Prograde Attitude object
         * 
         * @param engines 
         * @param propagator 
         */
        ProgradeAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines, IO::SDK::Propagators::Propagator &propagator,const IO::SDK::Time::TimeSpan& attitudeHoldDuration);

        /**
         * @brief Construct a new Prograde Attitude object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         */
        ProgradeAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*>& engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,const IO::SDK::Time::TimeSpan& attitudeHoldDuration);

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