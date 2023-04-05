/**
 * @file ZenithAttitude.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-18
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ZENITH_ATTITUDE_H
#define ZENITH_ATTITUDE_H

#include <vector>
#include <memory>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Propagator.h>
#include <Vector3D.h>

namespace IO::SDK::Maneuvers::Attitudes
{
    class ZenithAttitude final : public IO::SDK::Maneuvers::ManeuverBase
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
         * @brief Construct a new Zenith Attitude object
         * 
         * @param engines 
         * @param propagator 
         */
        ZenithAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TimeSpan &attitudeHoldDuration);

        /**
         * @brief Construct a new Zenith Attitude object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         */
        ZenithAttitude(const std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan &attitudeHoldDuration);

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