/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef CELESTIAL_BODY_H
#define CELESTIAL_BODY_H

#include <string>
#include <cmath>

#include <CelestialItem.h>
#include <BodyFixedFrames.h>
#include <TDB.h>
#include <Planetographic.h>
#include <ConicOrbitalElements.h>

namespace IO::Astrodynamics::OrbitalParameters
{
    class ConicOrbitalElements;
}

namespace IO::Astrodynamics::Body
{
    /**
 * @class CelestialBody
 * @brief The CelestialBody class represents a celestial body in astrodynamics calculations.
 * It inherits from IO::Astrodynamics::Body::CelestialItem.
 *
 * A celestial body is characterized by its sphere of influence, hill sphere, body-fixed frame,
 * and certain J values. It provides methods to obtain various properties such as the relative state vector,
 * sphere of influence, hill sphere, body-fixed frame, radius, flattening, angular velocity, and rotation period.
 *
 * It also provides static methods to check if a given celestial body is a sun, planet, asteroid, moon, or
 * Lagrange point. It provides methods to find the center of motion id, barycenter of motion id, and to check if
 * a given celestial body is a barycenter.
 */

    class CelestialBody final : public IO::Astrodynamics::Body::CelestialItem
    {
    private:

        const double m_sphereOfInfluence{};
        const double m_hillSphere{};
        const IO::Astrodynamics::Frames::BodyFixedFrames m_BodyFixedFrame;
        const double m_J2{};
        const double m_J3{};
        const double m_J4{};

        double ReadJValue(const char *valueName) const;

        double ReadJ2() const;

        double ReadJ3() const;

        double ReadJ4() const;


    public:
        /**
         * @class CelestialBody
         * @brief Represents a celestial body in the astrodynamics system.
         *
         * This class represents a celestial body with a unique identifier and a center of motion.
         * It is used to model celestial objects such as planets, moons, and stars.
         */
        CelestialBody(int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion);

        /**
         * @class CelestialBody
         * Represents a celestial body.
         */
        explicit CelestialBody(int id);


        /**
     * \fn float GetSphereOfInfluence() const
     *
     * \brief Returns the sphere of influence for the object.
     *
     * The sphere of influence is a measure of the range within which the object has significant influence or control.
     *
     * \return The sphere of influence as a floating-point value.
     */

        double GetSphereOfInfluence() const;

        /**
         * \brief Calculates the Hill Sphere of an astronomical object.
         *
         * This function calculates the Hill Sphere, which is the region around an astronomical object in which its gravitational
         * influence dominates over that of other nearby objects. The Hill Sphere is defined by the distance from the object's
         * center at which a satellite can maintain a stable orbit around the object.
         *
         * \return The radius of the Hill Sphere as a constant value.
         */

        double GetHillSphere() const;


        static double ReadGM(int id);

        /**
         * @brief Get the Relative Statevector from this celestial body to a given target
         *
         * @param targetStateVector
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        IO::Astrodynamics::OrbitalParameters::StateVector GetRelativeStatevector(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const;

        /**
         * @brief Know if a target is in sphere of influence
         *
         * @param targetStateVector
         * @return true
         * @return false
         */
        bool IsInSphereOfInfluence(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const;

        /**
         * @brief Know if a target is in hill sphere
         *
         * @param targetStateVector
         * @return true
         * @return false
         */
        bool IsInHillSphere(const IO::Astrodynamics::OrbitalParameters::StateVector &targetStateVector) const;

        /**
         * @brief Get the Body Fixed Frame
         *
         * @return const IO::Astrodynamics::Frames::BodyFixedFrames&
         */
        const IO::Astrodynamics::Frames::BodyFixedFrames &GetBodyFixedFrame() const;

        /**
         * @brief Get the Radius
         *
         * @return IO::Astrodynamics::Math::Vector3D
         */
        IO::Astrodynamics::Math::Vector3D GetRadius() const;

        /**
         * @brief Get the Flattening
         *
         * @return double
         */
        double GetFlattening() const;

        /**
         * @brief Get the Angular Velocity
         *
         * @param epoch
         * @return double
         */
        double GetAngularVelocity(const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get the Sideral Rotation Period
         *
         * @param epoch
         * @return IO::Astrodynamics::Time::TimeSpan
         */
        IO::Astrodynamics::Time::TimeSpan GetSideralRotationPeriod(const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Accessor function to get the value of the variable J2.
         *
         * This function returns the value of the variable J2.
         * The returned value will be constant and cannot be modified.
         *
         * @return The value of variable J2.
         */

        inline double GetJ2() const
        { return m_J2; }

        /**
         * @brief GetJ3 Function
         *
         * This function returns the value of the J3 variable.
         *
         * @return The value of the J3 variable.
         */

        inline double GetJ3() const
        { return m_J3; }

        /**
        * @brief Retrieves the value of J4.
        *
        * This function is used to get the value of J4.
        *
        * @return The value of J4.
        */

        inline double GetJ4() const
        { return m_J4; }


        /**
         * @brief Calculates the true solar day for a given epoch time in TDB.
         *
         * The true solar day is the time interval between two consecutive solar noons.
         * It is influenced by factors such as the eccentricity and obliquity of the body orbit.
         *
         * @param epoch The epoch time in TDB (Barycentric Dynamical Time).
         * @return The true solar day as a floating-point number, in the same unit as the epoch time.
         *
         * @see IO::Astrodynamics::Time::TDB
         *
         * @warning The epoch time must be in TDB. Ensure that the time is properly converted to TDB before calling this function.
         *          Failure to do so may result in incorrect results.
         *
         * @note This function is a const member function and does not modify the state of the object it belongs to.
         *       It only performs calculations based on the given epoch time.
         */

        IO::Astrodynamics::Time::TimeSpan GetTrueSolarDay(IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Check if the given celestial body is Sun.
         *
         * This function takes a celestial body identifier as input and
         * determines whether the celestial body corresponds to the Sun.
         *
         * @param celestialBodyId The identifier of the celestial body to check.
         *
         * @return @c true if the celestial body is the Sun, @c false otherwise.
         *
         * @note Celestial body identifiers are assumed to be unique.
         */

        static bool IsSun(int celestialBodyId);

        /**
         * @brief Determines whether a celestial body is a planet based on its ID.
         *
         * This function checks whether a given celestial body is classified as a planet based on its ID.
         * It takes the celestial body ID as an input and returns a boolean value indicating whether it is a planet or not.
         * A celestial body is considered a planet if its ID matches the criteria set for planets.
         *
         * @param celestialBodyId The ID of the celestial body to be checked.
         *
         * @return True if the celestial body is a planet, otherwise false.
         */

        static bool IsPlanet(int celestialBodyId);

        /**
         * @brief Checks if the given celestial body ID corresponds to an asteroid.
         *
         * This function takes a celestial body ID as input and checks if it corresponds to an asteroid.
         * An asteroid is a small rocky object that orbits the sun, typically in the asteroid belt between Mars and Jupiter.
         *
         * @param celestialBodyId The ID of the celestial body to be checked.
         * @return True if the celestial body is an asteroid, false otherwise.
         */

        static bool IsAsteroid(int celestialBodyId);

        /**
         * @brief Check if the given celestial body is a moon.
         *
         * This function checks whether the given celestial body identified by
         * its ID is a moon or not.
         *
         * @param celestialBodyId The ID of the celestial body to be checked.
         *
         * @return True if the celestial body is a moon, False otherwise.
         */

        static bool IsMoon(int celestialBodyId);

        /**
        * @brief Check if a celestial body is a Lagrange point.
        *
        * This function determines whether a celestial body with the given identifier
        * is a Lagrange point or not. Lagrange points are locations in a two-body
        * system where a third, smaller body can remain in a stable and fixed position
        * relative to the two larger bodies. The function takes the celestial body
        * identifier as an input parameter and returns a boolean value indicating
        * whether it is a Lagrange point or not.
        *
        * @param celestialBodyId The identifier of the celestial body to check.
        * @return True if the celestial body is a Lagrange point, false otherwise.
        */

        static bool IsLagrangePoint(int celestialBodyId);

        /**
         * @brief FindCenterOfMotionId - Finds the center of motion ID for a given celestial body.
         *
         * This function takes in a celestial body NAIF ID as a parameter and returns the center of motion ID.
         * The center of motion ID represents the celestial body around which the given celestial body revolves or orbits.
         *
         * @param celestialBodyNaifId The NAIF ID of the celestial body for which the center of motion ID needs to be found.
         *
         * @return The center of motion ID for the given celestial body. If no center of motion ID is found, returns -1.
         *
         * @note The value of NAIF ID should be a valid integer. Otherwise, the behavior of this function is undefined.
         *
         * @see https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/idfnc.html
         *
         * @warning This function depends on external libraries and data sources for determining the center of motion ID.
         *          It is assumed that the required libraries and data sources are properly installed and accessible.
         *          If the required resources are not available, the behavior of this function is undefined.
         */

        static int FindCenterOfMotionId(int celestialBodyNaifId);

        /**
         * @brief Finds the barycenter of motion ID for a given celestial body Naif ID.
         *
         * This function calculates the barycenter of motion ID for a given celestial body
         * Naif ID. The barycenter of motion ID represents the center of motion for a
         * celestial body within a system.
         *
         * @param celestialBodyNaifId The Naif ID of the celestial body.
         * @return The barycenter of motion ID for the given celestial body Naif ID.
         */

        static int FindBarycenterOfMotionId(int celestialBodyNaifId);

        /**
         * @brief Determines if the given celestial body ID corresponds to a barycenter.
         *
         * This function checks if the provided celestial body ID corresponds to a barycenter.
         * A barycenter is the center of mass of two or more celestial bodies that are orbiting around each other.
         *
         * @param celestialBodyId The ID of the celestial body to check.
         * @return True if the given celestial body ID corresponds to a barycenter, false otherwise.
         */

        static bool IsBarycenter(int celestialBodyId);

        /**
         * @brief Compute the geosynchronous orbit based on the given longitude and epoch.
         *
         * This function calculates the state vector of a geosynchronous orbit in body fixed frame of the current celestial body.
         *
         * @param longitude The desired longitude at which the geosynchronous orbit should be established.
         * @param epoch The epoch of the orbit, expressed in TDB (Barycentric Dynamic Time).
         * @return The calculated parameters of the geosynchronous orbit.
         *
         * @note The longitude is assumed to be in radian and the epoch is assumed to be in TDB.
         *
         * @remark The geosynchronous orbit is a circular, geocentric orbit with an inclination of zero and
         * an argument of perigee that aligns the longitude with the position of the satellite. Therefore,
         * all other orbital parameters are computed based on these constraints.
         */

        OrbitalParameters::ConicOrbitalElements ComputeGeosynchronousOrbit(double longitude, const Time::TDB &epoch) const;

        /**
         * @brief Computes the geosynchronous orbit parameters for a given longitude, latitude, and epoch.
         *
         * This function calculates the orbital parameters for a geosynchronous orbit
         * at a specific longitude, latitude, and epoch in time.
         *
         * @param longitude Longitude of the desired position on celestial body.
         * @param latitude Latitude of the desired position on celestial body.
         * @param epoch The epoch at which the orbital parameters should be calculated.
         *
         * @return void
         */

        OrbitalParameters::ConicOrbitalElements ComputeGeosynchronousOrbit(double longitude, double latitude, const Time::TDB &epoch) const;


        /**
         * Calculates the fixed position of a body given its longitude, latitude, and epoch.
         *
         * @param longitude The longitude of the body.
         * @param latitude The latitude of the body.
         * @param epoch The epoch at which to calculate the fixed position.
         * @return The fixed position of the body.
         */

        IO::Astrodynamics::Math::Vector3D GetBodyFixedPosition(double longitude, double latitude, const IO::Astrodynamics::Time::TDB &epoch) const;
    };

    /**
     * @brief Compute sphere of influence radius
     *
     * @param a Semi major axis
     * @param majorMass Major body
     * @param minorMass Minor body
     * @return double
     */
    double SphereOfInfluence(double a, double majorMass, double minorMass);

    /**
     * @brief Compute Hill sphere radius
     *
     * @param a Semi major axis
     * @param majorMass Major body
     * @param minorMass minor body
     * @return double
     */
    double HillSphere(double a, double e, double majorMass, double minorMass);


}
#endif
