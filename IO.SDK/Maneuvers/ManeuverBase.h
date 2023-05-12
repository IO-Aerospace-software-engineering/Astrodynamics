/**
 * @file ManeuverBase.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef MANEUVER_BASE_H
#define MANEUVER_BASE_H

#include <set>
#include <map>

#include <ManeuverResult.h>
#include <Propagator.h>
#include <DynamicFuelTank.h>

using namespace std::chrono_literals;

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
        double Burn(const IO::SDK::Time::TimeSpan &duration);

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
        [[nodiscard]] bool IsValid() const;

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

        const IO::SDK::Time::TimeSpan m_attitudeHoldDuration;
        std::unique_ptr<IO::SDK::Time::Window<IO::SDK::Time::TDB>> m_attitudeWindow{};

        std::unique_ptr<IO::SDK::Time::Window<IO::SDK::Time::TDB>> m_maneuverWindow{};

    protected:
        std::unique_ptr<IO::SDK::Time::Window<IO::SDK::Time::TDB>> m_thrustWindow{};
        std::unique_ptr<IO::SDK::Math::Vector3D> m_deltaV{};
        IO::SDK::Time::TimeSpan m_thrustDuration{0.0s};
        double m_fuelBurned{};
        const std::vector<IO::SDK::Body::Spacecraft::Engine*> m_engines{};
        std::unique_ptr<IO::SDK::Time::TDB> m_minimumEpoch{};
        ManeuverBase *m_nextManeuver{};
        bool m_isValid{false};
        const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
        IO::SDK::Propagators::Propagator &m_propagator;
        std::vector<ManeuverBase *> m_subManeuvers{};
        std::set<const IO::SDK::Body::Spacecraft::FuelTank *> m_fuelTanks;
        std::map<const IO::SDK::Body::Spacecraft::FuelTank *, IO::SDK::Maneuvers::DynamicFuelTank> m_dynamicFuelTanks;
        IO::SDK::Time::TimeSpan m_maneuverHoldDuration{0.0s};

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         */
        ManeuverBase(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         * @param minimumEpoch No maneuver execution before this epoch
         */
        ManeuverBase(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator,
                     const IO::SDK::Time::TimeSpan &attitudeHoldDuration);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const IO::SDK::Time::TDB &minimumEpoch,
                     const IO::SDK::Time::TimeSpan &attitudeHoldDuration);

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
         * @brief Get the thrust window
         * 
         * @return IO::SDK::Time::Window<IO::SDK::Time::TDB>* 
         */
        [[nodiscard]] IO::SDK::Time::Window<IO::SDK::Time::TDB> *GetThrustWindow() const;

        /**
         * @brief Get the attitude window
         * 
         * @return IO::SDK::Time::Window<IO::SDK::Time::TDB>* 
         */
        [[nodiscard]] IO::SDK::Time::Window<IO::SDK::Time::TDB> *GetAttitudeWindow() const;

        /**
         * @brief Get the maneuver window.
         * Maneuver window start at burn and end when the maneuver is completed. 
         * 
         * @return IO::SDK::Time::Window<IO::SDK::Time::TDB>* 
         */
        [[nodiscard]] IO::SDK::Time::Window<IO::SDK::Time::TDB> *GetManeuverWindow() const;

        /**
         * @brief Get the Fuel Burned in kg
         * 
         * @return double 
         */
        [[nodiscard]] double GetFuelBurned() const;

        /**
         * @brief Get the Thrust duration 
         * 
         * @return IO::SDK::Time::TimeSpan 
         */
        [[nodiscard]] IO::SDK::Time::TimeSpan GetThrustDuration() const;

        /**
         * @brief Get the maneuver delta V
         * 
         * @return IO::SDK::Math::Vector3D 
         */
        [[nodiscard]] IO::SDK::Math::Vector3D GetDeltaV() const;

        /**
         * @brief Set the Next Maneuver object
         * 
         * @param maneuver 
         * @return IO::SDK::Maneuvers::ManeuverBase& 
         */
        virtual IO::SDK::Maneuvers::ManeuverBase &SetNextManeuver(IO::SDK::Maneuvers::ManeuverBase &maneuver);
    };
}

#endif