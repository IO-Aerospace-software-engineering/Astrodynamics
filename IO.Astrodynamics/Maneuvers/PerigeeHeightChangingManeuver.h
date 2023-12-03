/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef PERIGEE_HEIGHT_CHANGING_MANEUVERS_H
#define PERIGEE_HEIGHT_CHANGING_MANEUVERS_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers
{
    class PerigeeHeightChangingManeuver final: public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        double m_targetHeight;

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
         * @brief Construct a new Perigee Height Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetHeight 
         */
        PerigeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight);

        /**
         * @brief Construct a new Perigee Height Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetHeight 
         * @param minimumEpoch 
         */
        PerigeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight, const IO::Astrodynamics::Time::TDB &minimumEpoch);

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