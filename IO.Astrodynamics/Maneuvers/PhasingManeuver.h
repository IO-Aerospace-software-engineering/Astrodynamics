/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef PHASING_MANEUVER_H
#define PHASING_MANEUVER_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers
{
    class PhasingManeuver final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        const unsigned int m_revolutionsNumber;
        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> m_targetOrbit;

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
        PhasingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, unsigned revolutionNumber,
                        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit);

        PhasingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, unsigned revolutionNumber,
                        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };

} // namespace IO::Astrodynamics::Maneuvers

#endif