/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef BODY_H
#define BODY_H

#include <OrbitalParameters.h>
#include <Planetographic.h>
#include <GeometryFinder.h>

namespace IO::Astrodynamics::OrbitalParameters
{
    class OrbitalParameters;

    class StateVector;
}

namespace IO::Astrodynamics::Body
{
    /**
     * @brief Body class
     *
     */
    class CelestialItem : public std::enable_shared_from_this<IO::Astrodynamics::Body::CelestialItem>
    {
    private:
    protected:
        std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> m_orbitalParametersAtEpoch{};
        std::vector<IO::Astrodynamics::Body::CelestialItem *> m_satellites{};
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
        CelestialItem(int id, const std::string &name, double mass);

        /**
         * @brief Construct a new Body object
         *
         * @param id
         * @param name
         * @param mass kg
         * @param orbitalParameters
         */
        CelestialItem(int id, const std::string &name, double mass, std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);

        /**
         * @brief Construct a new Body object
         *
         * @param id
         * @param name
         * @param mass
         * @param centerOfMotion
         */
        CelestialItem(int id, const std::string &name, double mass, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion);

        CelestialItem(const CelestialItem &body);

        virtual ~CelestialItem() = default;

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
         * @return std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters>
         */
        const std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> &GetOrbitalParametersAtEpoch() const;

        /**
         * @brief Get the Satellites
         *
         * @return const std::vector<IO::Astrodynamics::Body::Body*>&
         */
        const std::vector<IO::Astrodynamics::Body::CelestialItem *> &GetSatellites() const;

        /**
         * @brief Get the State Vector relative to its center of motion
         *
         * @param frame
         * @param aberration
         * @param epoch
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        virtual IO::Astrodynamics::OrbitalParameters::StateVector
        ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get state vector from ephemeris relative to another body
         *
         * @param epoch
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        virtual IO::Astrodynamics::OrbitalParameters::StateVector ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch,
                                                                      const IO::Astrodynamics::Body::CelestialBody &relativeTo) const;

        virtual bool operator==(const IO::Astrodynamics::Body::CelestialItem &rhs) const;

        virtual bool operator!=(const IO::Astrodynamics::Body::CelestialItem &rhs) const;

        std::shared_ptr<IO::Astrodynamics::Body::CelestialItem> GetSharedPointer();
        std::shared_ptr<IO::Astrodynamics::Body::CelestialItem> GetSharedPointer() const;


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
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
         */
        static std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsOnDistanceConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const CelestialItem &targetBody, const CelestialItem &observer,
                                        const IO::Astrodynamics::Constraints::RelationalOperator &constraint, IO::Astrodynamics::AberrationsEnum aberration, double value, const IO::Astrodynamics::Time::TimeSpan &step);

        /**
         * @brief Find windows when occultation occurs
         *
         * @param searchWindow Time window where constraint is evaluated
         * @param targetBody Target body
         * @param frontBody Front body between target and observer
         * @param occultationType Full-Annular-Partial-Any
         * @param aberration Aberration correction
         * @param stepSize Step size (should be shorter than the shortest of these intervals. WARNING : A short step size could increase compute time)
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
         */
        std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsOnOccultationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Body::CelestialItem &targetBody,
                                           const IO::Astrodynamics::Body::CelestialBody &frontBody, const IO::Astrodynamics::OccultationType &occultationType, IO::Astrodynamics::AberrationsEnum aberration,
                                           const IO::Astrodynamics::Time::TimeSpan &stepSize) const;

        /**
         * @brief Get the Sub Observer Point observed from this body
         *
         * @param targetBody
         * @param aberration
         * @param epoch
         * @return IO::Astrodynamics::Coordinates::Planetographic
         */
        IO::Astrodynamics::Coordinates::Planetographic
        GetSubObserverPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, const IO::Astrodynamics::AberrationsEnum &aberration, const IO::Astrodynamics::Time::DateTime &epoch) const;

        /**
         * @brief Get the Sub Solar Point observed from this body
         *
         * @param targetBody
         * @param abberation
         * @param epoch
         * @return IO::Astrodynamics::Coordinates::Planetographic
         */
        IO::Astrodynamics::Coordinates::Planetographic
        GetSubSolarPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch) const;
    };
}
#endif
