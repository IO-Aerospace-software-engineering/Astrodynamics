/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef MANEUVER_BASE_H
#define MANEUVER_BASE_H

#include <set>

#include <ManeuverResult.h>
#include <Propagator.h>
#include <DynamicFuelTank.h>
#include <optional>

using namespace std::chrono_literals;

namespace IO::Astrodynamics::Propagators
{
    class Propagator;
}

namespace IO::Astrodynamics::Maneuvers
{
    /**
     * @brief ManeuverBase class used by concrete maneuvers
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
        IO::Astrodynamics::Time::TimeSpan GetMinimumRemainingThrustDuration();

        /**
         * @brief Burn fuel form fuels tanks
         * 
         * @param duration 
         * @return Fuel burned quantity in kg
         */
        double Burn(const IO::Astrodynamics::Time::TimeSpan &duration);

        /**
         * @brief Check if computed maneuver is valid
         * 
         * @return true 
         * @return false 
         */
        IO::Astrodynamics::Maneuvers::ManeuverResult Validate();

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
        void ExecuteAt(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint);

        /**
         * @brief Spread the duration of thrust
         * 
         */
        void SpreadThrust();

        const IO::Astrodynamics::Time::TimeSpan m_attitudeHoldDuration;
        std::unique_ptr<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> m_attitudeWindow{};

        std::unique_ptr<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> m_maneuverWindow{};

    protected:
        std::unique_ptr<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> m_thrustWindow{};
        std::unique_ptr<IO::Astrodynamics::Math::Vector3D> m_deltaV{};
        IO::Astrodynamics::Time::TimeSpan m_thrustDuration{0.0s};
        double m_fuelBurned{};
        const std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> m_engines{};
        std::unique_ptr<IO::Astrodynamics::Time::TDB> m_minimumEpoch{};
        ManeuverBase *m_nextManeuver{};
        bool m_isValid{false};
        const IO::Astrodynamics::Body::Spacecraft::Spacecraft &m_spacecraft;
        IO::Astrodynamics::Propagators::Propagator &m_propagator;
        std::vector<ManeuverBase *> m_subManeuvers{};
        std::set<const IO::Astrodynamics::Body::Spacecraft::FuelTank *> m_fuelTanks;
        std::map<const IO::Astrodynamics::Body::Spacecraft::FuelTank *, IO::Astrodynamics::Maneuvers::DynamicFuelTank> m_dynamicFuelTanks;
        IO::Astrodynamics::Time::TimeSpan m_maneuverHoldDuration{0.0s};
        std::optional<Math::Vector3D> m_maneuverPointTarget;
        std::optional<IO::Astrodynamics::Time::TDB> m_maneuverPointUpdate;
        std::optional<bool> m_isInbound;

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         */
        ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines Used by maneuver
         * @param minimumEpoch No maneuver execution before this epoch
         */
        ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
                     const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration);

        /**
         * @brief Construct a new Maneuver Base object
         * 
         * @param engines 
         * @param propagator 
         * @param minimumEpoch 
         * @param attitudeHoldDuration 
         */
        ManeuverBase(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const IO::Astrodynamics::Time::TDB &minimumEpoch,
                     const IO::Astrodynamics::Time::TimeSpan &attitudeHoldDuration);

        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        virtual void Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        virtual IO::Astrodynamics::OrbitalParameters::StateOrientation ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;

        virtual void UpdateManeuverPoint(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint);
        virtual Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) = 0;
    public:
        /**
         * @brief Handle maneuver by propagator
         * 
         * @param notBeforeEpoch Maneuver must not start before this epoch
         */
        void Handle(const IO::Astrodynamics::Time::TDB &notBeforeEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        virtual bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint);

        /**
         * @brief Try to execute maneuver at maneuver point
         * 
         * @param maneuverPoint Maneuver point if it was impulsive
         */
        IO::Astrodynamics::Maneuvers::ManeuverResult TryExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint);

        /**
         * @brief Get the thrust window
         * 
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>*
         */
        [[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *GetThrustWindow() const;

        /**
         * @brief Get the attitude window
         * 
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>*
         */
        [[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *GetAttitudeWindow() const;

        /**
         * @brief Get the maneuver window.
         * Maneuver window start at burn and end when the maneuver is completed. 
         * 
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>*
         */
        [[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> *GetManeuverWindow() const;

        /**
         * @brief Get the Fuel Burned in kg
         * 
         * @return double 
         */
        [[nodiscard]] double GetFuelBurned() const;

        /**
         * @brief Get the Thrust duration 
         * 
         * @return IO::Astrodynamics::Time::TimeSpan
         */
        [[nodiscard]] IO::Astrodynamics::Time::TimeSpan GetThrustDuration() const;

        /**
         * @brief Get the maneuver delta V
         * 
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetDeltaV() const;

        /**
         * @brief Set the Next Maneuver object
         * 
         * @param maneuver 
         * @return IO::Astrodynamics::Maneuvers::ManeuverBase&
         */
        virtual IO::Astrodynamics::Maneuvers::ManeuverBase &SetNextManeuver(IO::Astrodynamics::Maneuvers::ManeuverBase &maneuver);
    };
}

#endif