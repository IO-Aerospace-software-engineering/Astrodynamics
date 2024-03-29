/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef CONIC_ORBITAL_ELEMENTS_H
#define CONIC_ORBITAL_ELEMENTS_H

#include<Constants.h>
#include<StateVector.h>

namespace IO::Astrodynamics::OrbitalParameters
{
    // class StateVector;
    /// <summary>
    /// Conic orbital elements
    /// </summary>
    class ConicOrbitalElements final : public IO::Astrodynamics::OrbitalParameters::OrbitalParameters
    {
    private:
        double m_perifocalDistance{};
        double m_eccentricity{};
        double m_inclination{};
        double m_ascendingNodeLongitude{};
        double m_periapsisArgument{};
        double m_meanAnomaly{};
        double m_trueAnomaly{};
        IO::Astrodynamics::Time::TimeSpan m_orbitalPeriod;
        double m_semiMajorAxis{};


    public:

        ConicOrbitalElements(
                const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion,
                double perifocalDistance,
                double eccentricity,
                double inclination,
                double ascendingNodeLongitude,
                double periapsisArgument,
                double meanAnomaly,
                const IO::Astrodynamics::Time::TDB &epoch,
                const IO::Astrodynamics::Frames::Frames &frame);

        /// <summary>
        /// Instanciate conical elements from spice elements
        /// </summary>
        /// <param name="centerOfMotion"></param>
        /// <param name="spiceElements"></param>
        ConicOrbitalElements(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const double spiceElements[SPICE_OSCLTX_NELTS],
                             const IO::Astrodynamics::Frames::Frames &frame);

        explicit ConicOrbitalElements(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector);

        ConicOrbitalElements(const ConicOrbitalElements &conicOrbitalElements) = default;

        ~ConicOrbitalElements() override = default;

        /// <summary>
        /// Get perifocal distance at orbital parameters epoch
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetPerifocalDistance() const;

        /// <summary>
        /// Get eccentticity
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetEccentricity() const override;

        /// <summary>
        /// Get inclination
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetInclination() const override;

        /// <summary>
        /// Get ascending node longitude
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetRightAscendingNodeLongitude() const override;

        /// <summary>
        /// Get periapsis argument
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetPeriapsisArgument() const override;

        //Get non pure virtual function accessible
        using OrbitalParameters::GetMeanAnomaly;

        /// <summary>
        /// Get mean anomaly at epoch
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetMeanAnomaly() const override;

        //Get non pure virtual function accessible
        using OrbitalParameters::GetTrueAnomaly;

        /// <summary>
        /// Get true anomaly at epoch
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetTrueAnomaly() const override;

        /// <summary>
        /// Get the orbital semi major axis
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetSemiMajorAxis() const override;

        /// <summary>
        /// Get the orbital period
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] IO::Astrodynamics::Time::TimeSpan GetPeriod() const override;

        /// <summary>
        /// Get the state vector at epoch
        /// </summary>
        /// <param name="epoch"></param>
        /// <returns></returns>
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const override;

        /// <summary>
        /// Get the specific angular momentum
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetSpecificAngularMomentum() const override;

        /// <summary>
        /// Get specific orbital energy
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetSpecificOrbitalEnergy() const override;

        using IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ToStateVector;
    };
}
#endif

