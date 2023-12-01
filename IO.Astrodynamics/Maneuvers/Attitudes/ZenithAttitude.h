/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ZENITH_ATTITUDE_H
#define ZENITH_ATTITUDE_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers::Attitudes
{
    class ZenithAttitude final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        /* data */

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
        Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters& orbitalParameters) override;
    public:
        /**
         * @brief Construct a new Zenith Attitude object
         * 
         * @param engines 
         * @param propagator 
         */
        ZenithAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration);

        /**
         * @brief Construct a new Zenith Attitude object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         */
        ZenithAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch, const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration);

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