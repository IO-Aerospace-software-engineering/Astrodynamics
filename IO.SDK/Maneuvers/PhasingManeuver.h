/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef PHASING_MANEUVER_H
#define PHASING_MANEUVER_H

#include <ManeuverBase.h>

namespace IO::SDK::Maneuvers
{
    class PhasingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        const unsigned int m_revolutionsNumber;
        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> m_targetOrbit;

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
        /* data */
    public:
        PhasingManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, unsigned revolutionNumber,
                        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit);

        PhasingManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, unsigned revolutionNumber,
                        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };

} // namespace IO::SDK::Maneuvers

#endif