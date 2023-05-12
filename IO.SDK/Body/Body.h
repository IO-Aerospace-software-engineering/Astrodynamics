/**
 * @file Body.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-22
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef BODY_H
#define BODY_H

#include <vector>

#include <OrbitalParameters.h>
#include <Aberrations.h>
#include <Window.h>
#include "Constraints/RelationalOperator.h"
#include <OccultationType.h>
#include <Planetographic.h>
#include <GeometryFinder.h>

namespace IO::SDK::OrbitalParameters
{
    class OrbitalParameters;

    class StateVector;
}

namespace IO::SDK::Body
{
    /**
     * @brief Body class
     *
     */
    class Body : public std::enable_shared_from_this<IO::SDK::Body::Body>
    {
    private:
    protected:
        std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> m_orbitalParametersAtEpoch{};
        std::vector<IO::SDK::Body::Body *> m_satellites{};
        const int m_id{};
        const std::string m_name{};
        double m_mass{};
        double m_mu{};

    public:
        /**
         * @brief Construct a new Body object
         *
         * @param id
         * @param name
         * @param mass kg
         */
        Body(int id, const std::string &name, double mass);

        /**
         * @brief Construct a new Body object
         *
         * @param id
         * @param name
         * @param mass kg
         * @param orbitalParameters
         */
        Body(int id, const std::string &name, double mass, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);

        /**
         * @brief Construct a new Body object
         *
         * @param id
         * @param name
         * @param mass
         * @param centerOfMotion
         */
        Body(int id, const std::string &name, double mass, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion);

        Body(const Body &body);

        virtual ~Body() = default;

        /**
         * @brief Get the body identifier
         *
         * @return const int
         */
        int GetId() const;

        /**
         * @brief Get the body name
         *
         * @return const std::string
         */
        std::string GetName() const;

        /**
         * @brief Get the Mass
         *
         * @return double
         */
        virtual double GetMass() const;

        /**
         * @brief Get the Mu value
         *
         * @return double
         */
        double GetMu() const;

        /**
         * @brief Get body orbital parameters
         *
         * @return std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters>
         */
        const std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> &GetOrbitalParametersAtEpoch() const;

        /**
         * @brief Get the Satellites
         *
         * @return const std::vector<IO::SDK::Body::Body*>&
         */
        const std::vector<IO::SDK::Body::Body *> &GetSatellites() const;

        /**
         * @brief Get the State Vector relative to its center of motion
         *
         * @param frame
         * @param aberration
         * @param epoch
         * @return IO::SDK::OrbitalParameters::StateVector
         */
        virtual IO::SDK::OrbitalParameters::StateVector
        ReadEphemeris(const IO::SDK::Frames::Frames &frame, IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Get state vector from ephemeris relative to another body
         *
         * @param epoch
         * @return IO::SDK::OrbitalParameters::StateVector
         */
        virtual IO::SDK::OrbitalParameters::StateVector ReadEphemeris(const IO::SDK::Frames::Frames &frame, IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch,
                                                                      const IO::SDK::Body::CelestialBody &relativeTo) const;

        virtual bool operator==(const IO::SDK::Body::Body &rhs) const;

        virtual bool operator!=(const IO::SDK::Body::Body &rhs) const;

        std::shared_ptr<IO::SDK::Body::Body> GetSharedPointer();

        /**
         * @brief Find windows when distance constraint occurs
         *
         * @param targetBody Target body
         * @param observer Observer
         * @param constraint RelationalOperator operator
         * @param aberration Aberration
         * @param value Target value
         * @param searchWindow Time window where constraint is evaluated
         * @param step Step size (should be shorter than the shortest of these intervals. WARNING : A short step size could increase compute time)
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
         */
        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const Body &targetBody, const Body &observer,
                                        const IO::SDK::Constraints::RelationalOperator &constraint, IO::SDK::AberrationsEnum aberration, double value, const IO::SDK::Time::TimeSpan &step);

        /**
         * @brief Find windows when occultation occurs
         *
         * @param searchWindow Time window where constraint is evaluated
         * @param targetBody Target body
         * @param frontBody Front body between target and observer
         * @param occultationType Full-Annular-Partial-Any
         * @param aberration Aberration correction
         * @param stepSize Step size (should be shorter than the shortest of these intervals. WARNING : A short step size could increase compute time)
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
         */
        std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const IO::SDK::Body::Body &targetBody,
                                           const IO::SDK::Body::CelestialBody &frontBody, const IO::SDK::OccultationType &occultationType, IO::SDK::AberrationsEnum aberration,
                                           const IO::SDK::Time::TimeSpan &stepSize) const;

        /**
         * @brief Get the Sub Observer Point observed from this body
         *
         * @param targetBody
         * @param aberration
         * @param epoch
         * @return IO::SDK::Coordinates::Planetographic
         */
        IO::SDK::Coordinates::Planetographic
        GetSubObserverPoint(const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::AberrationsEnum &aberration, const IO::SDK::Time::DateTime &epoch) const;

        /**
         * @brief Get the Sub Solar Point observed from this body
         *
         * @param targetBody
         * @param abberation
         * @param epoch
         * @return IO::SDK::Coordinates::Planetographic
         */
        IO::SDK::Coordinates::Planetographic
        GetSubSolarPoint(const IO::SDK::Body::CelestialBody &targetBody, IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const;
    };
}
#endif
