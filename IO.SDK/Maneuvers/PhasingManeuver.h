#ifndef PHASING_MANEUVER_H
#define PHASING_MANEUVER_H

#include<memory>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <StateOrientation.h>
#include <TimeSpan.h>

namespace IO::SDK::Maneuvers
{
    class PhasingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        const uint m_revolutionsNumber;
        std::unique_ptr<bool> m_isApproachingPerigee{nullptr};
        IO::SDK::OrbitalParameters::OrbitalParameters *m_targetOrbit{nullptr};

        bool IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

        double DeltaHeight(const IO::SDK::OrbitalParameters::OrbitalParameters& orbitalParameters);

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
        PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const uint revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const uint revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };

    /**
     * @brief Compute phasing duration
     * 
     * @param k nimber of revolutions
     * @param n Mean motion
     * @param deltaTrueAnomaly Delta true anomaly
     * @return IO::SDK::Time::TimeSpan 
     */
    IO::SDK::Time::TimeSpan PhasingDuration(const uint k,const double n, const double deltaTrueAnomaly);

    /**
     * @brief Computa semi major axis for phasing
     * 
     * @param gm GM parameter
     * @param phasingDuration Phasing duration
     * @return double 
     */
    double PhasingSemiMajorAxis(const double gm,IO::SDK::Time::TimeSpan phasingDuration);


} // namespace IO::SDK::Maneuvers

#endif