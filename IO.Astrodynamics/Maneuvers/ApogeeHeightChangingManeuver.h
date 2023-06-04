/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef APOGEE_HEIGHT_CHANGING_MANEUVERS_H
#define APOGEE_HEIGHT_CHANGING_MANEUVERS_H

#include <memory>
#include <vector>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <StateVector.h>
#include <StateOrientation.h>

namespace IO::Astrodynamics::Maneuvers
{
    class ApogeeHeightChangingManeuver final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        const double m_targetHeight;

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
        /* data */
    public:
        ApogeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight);
        ApogeeHeightChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double targetHeight, const IO::Astrodynamics::Time::TDB &minimumEpoch);

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