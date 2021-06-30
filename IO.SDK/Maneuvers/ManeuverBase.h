/**
 * @file ManeuverBase.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef MANEUVER_BASE_H
#define MANEUVER_BASE_H

#include <memory>
#include <vector>
#include <set>
#include <map>

#include <Window.h>
#include <Vector3D.h>
#include <Engine.h>
#include <TDB.h>
#include <StateVector.h>
#include <ManeuverResult.h>
#include <Propagator.h>
#include <DynamicFuelTank.cpp>
#include <Spacecraft.h>
#include <TimeSpan.h>
#include <TooEarlyManeuverException.h>

namespace IO::SDK::Propagators
{
    class Propagator;
}

namespace IO::SDK::Maneuvers
{
    /**
     * @brief Maneuverbase class used by concrete maneuvers
     * 
     */
    class ManeuverBase
    {
    private:
        /**
         * @brief Get the Remaining Average Fuel Flow
         * 
         * @return double 
         */
        double GetRemainingAvgFuelFlow();

        /**
         * @brief Get the Remaining Avg ISP
         * 
         * @return double 
         */
        double GetRemainingAvgISP();

        /**
         * @brief Get the minimum remaining thrust duration
         * 
         * @return double 
         */
        IO::SDK::Time::TimeSpan GetMinimumRemainingThrustDuration();

        /**
         * @brief Burn fuel form fuels tanks
         * 
         * @param duration 
         * @return Fuel burned quantity in kg
         */
        double Burn(const IO::SDK::Time::TimeSpan& duration);

        /**
         * @brief Check if computed maneuver is valid
         * 
         * @return true 
         * @return false 
         */
        IO::SDK::Maneuvers::ManeuverResult Validate();

        /**
         * @brief Check if computed maneuver can be executed
         * 
         * @return true 
         * @return false 
         */
        bool IsValid();

        /**
         * @brief Execute maneuver and write data in propagator
         * 
         */
        void ExecuteAt(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint);

        /**
         * @brief Spread the duration of thrust
         * 
         */
        void SpreadThrust();

        /**
         * @brief Set the Next Maneuver object
         * 
         * @param maneuver 
         * @return ManeuverBase* 
         */
        virtual IO::SDK::Maneuvers::ManeuverBase &SetNextManeuver(IO::SDK::Maneuvers::ManeuverBase &maneuver);

        const IO::SDK::Time::TimeSpan m_attitudeHoldDuration;
        std::unique_ptr<IO::SDK::Time::Window<IO::SDK::Time::TDB>> m_attitudeWindow{};

    protected:
        std::unique_ptr<IO::SDK::Time::Window<IO::SDK::Time::TDB>> m_thrustWindow{};
        
        std::unique_ptr<IO::SDK::Math::Vector3D> m_deltaV{};
        IO::SDK::Time::TimeSpan m_thrustDuration{};
        double m_fuelBurned{};
        const std::vector<IO::SDK::Body::Spacecraft::Engine> m_engines{};
        std::unique_ptr<IO::SDK::Time::TDB> m_minimumEpoch{};
        ManeuverBase *m_nextManeuver{};
        bool m_isValid{false};
        const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
        IO::SDK::Propagators::Propagator& m_propagator;
        std::vector<ManeuverBase *> m_subManeuvers{};
        std::set<const IO::SDK::Body::Spacecraft::FuelTank *> m_fuelTanks;
        std::map<const IO::SDK::Body::Spacecraft::FuelTank *, IO::SDK::Maneuvers::DynamicFuelTank> m_dynamicFuelTanks;
        

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         */
        ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator& propagator);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         * @param minimumEpoch No maneuver execution before this epoch
         */
        ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator& propagator, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator& propagator, const IO::SDK::Time::TimeSpan& attitudeHoldDuration );

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator& propagator, const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Time::TimeSpan& attitudeHoldDuration );
        virtual ~ManeuverBase() = default;

        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        virtual void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::SDK::OrbitalParameters::StateOrientation 
         */
        virtual IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;

    public:
        /**
         * @brief Handle maneuver by propagator
         * 
         * @param notBeforeEpoch Maneuver must not start before this epoch
         */
        void Handle(const IO::SDK::Time::TDB &notBeforeEpoch);
        

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        virtual bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;

        /**
         * @brief Try to execute maneuver at maneuver point
         * 
         * @param maneuverPoint Maneuver point if it was impulsive
         */
        IO::SDK::Maneuvers::ManeuverResult TryExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint);

        /**
         * @brief Get the maneuver window
         * 
         * @return IO::SDK::Time::Window<IO::SDK::Time::TDB>* 
         */
        IO::SDK::Time::Window<IO::SDK::Time::TDB> *GetThrustWindow() const;

        /**
         * @brief Get the Fuel Burned in kg
         * 
         * @return double 
         */
        double GetFuelBurned() const;

        /**
         * @brief Get the Thrust duration 
         * 
         * @return IO::SDK::Time::TimeSpan 
         */
        IO::SDK::Time::TimeSpan GetThrustDuration() const;

        /**
         * @brief Get the maneuver delta V
         * 
         * @return IO::SDK::Math::Vector3D 
         */
        IO::SDK::Math::Vector3D GetDeltaV() const;
    };
}

#endif