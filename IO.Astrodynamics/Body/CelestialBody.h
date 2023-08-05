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
    class CelestialBody final : public IO::Astrodynamics::Body::CelestialItem
    {
    private:

        const double m_sphereOfInfluence{};
        const double m_hillSphere{};
        const IO::Astrodynamics::Frames::BodyFixedFrames m_BodyFixedFrame;
        const double m_J2{};
        const double m_J3{};
        const double m_J4{};

        double ReadJValue(const char * valueName) const;
        double ReadJ2() const;
        double ReadJ3() const;
        double ReadJ4() const;


    public:
        CelestialBody(int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion);

        explicit CelestialBody(int id);

        double GetSphereOfInfluence() const;

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

        inline double GetJ2() const
        { return m_J2; }

        inline double GetJ3() const
        { return m_J3; }

        inline double GetJ4() const
        { return m_J4; }

        IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements CreateHelioSynchronousOrbit(double semiMajorAxis, double eccentricity, IO::Astrodynamics::Time::TDB& crossEquatorAt) const;

        static bool IsSun(int celestialBodyId);

        static bool IsPlanet(int celestialBodyId);

        static bool IsAsteroid(int celestialBodyId);

        static bool IsMoon(int celestialBodyId);

        static bool IsLagrangePoint(int celestialBodyId);

        static int FindCenterOfMotionId(int celestialBodyNaifId);

        static int FindBarycenterOfMotionId(int celestialBodyNaifId);

        static bool IsBarycenter(int celestialBodyId);


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
