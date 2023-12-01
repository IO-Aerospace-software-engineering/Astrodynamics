/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TOWARD_OBJECT_ATTITUDE_H
#define TOWARD_OBJECT_ATTITUDE_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers::Attitudes
{
    class TowardObjectAttitude final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        const IO::Astrodynamics::Body::CelestialItem &m_targetBody;

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
         * @brief Construct a new Toward Object Attitude object
         * 
         * @param engines 
         * @param propagator 
         * @param targetBody 
         */
        TowardObjectAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator,const IO::Astrodynamics::Time::TimeSpan& attitudeHoldDuration, const IO::Astrodynamics::Body::CelestialItem &targetBody);

        /**
         * @brief Construct a new Toward Object Attitude object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         * @param targetBody 
         */
        TowardObjectAttitude(const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*>& engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,const IO::Astrodynamics::Time::TimeSpan& attitudeHoldDuration, const IO::Astrodynamics::Body::CelestialItem &targetBody);

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