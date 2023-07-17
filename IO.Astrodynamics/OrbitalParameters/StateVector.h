/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef STATE_VECTOR_H
#define STATE_VECTOR_H

#include<array>

#include <CelestialBody.h>

namespace IO::Astrodynamics::OrbitalParameters
{
    /**
     * @brief State vector class
     *
     */
    class StateVector final : public IO::Astrodynamics::OrbitalParameters::OrbitalParameters
    {
    private:
        const IO::Astrodynamics::Math::Vector3D m_position{};
        const IO::Astrodynamics::Math::Vector3D m_velocity{};
        const IO::Astrodynamics::Math::Vector3D m_momentum{};
        std::array<SpiceDouble, SPICE_OSCLTX_NELTS> m_osculatingElements{std::numeric_limits<double>::quiet_NaN()};

    public:
        /**
         * @brief Construct a new State Vector object
         *
         * @param centerOfMotion
         * @param position
         * @param velocity
         * @param epoch
         * @param frame
         */
        StateVector(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const IO::Astrodynamics::Math::Vector3D &position, const IO::Astrodynamics::Math::Vector3D &velocity,
                    const IO::Astrodynamics::Time::TDB &epoch, const IO::Astrodynamics::Frames::Frames &frame);

        // /**
        //  * @brief Construct a new State Vector object without
        //  *
        //  * @param centerOfMotion
        //  * @param position
        //  * @param epoch
        //  */
        // StateVector(const std::shared_ptr<IO::Astrodynamics::CelestialItem::CelestialBody> &centerOfMotion, const IO::Astrodynamics::Math::Vector3D& position, const IO::Astrodynamics::Time::TDB &epoch);

        /**
         * @brief Construct a new State Vector object
         *
         * @param centerOfMotion
         * @param spiceState
         * @param epoch
         * @param frame
         */
        StateVector(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, double spiceState[6], const IO::Astrodynamics::Time::TDB &epoch,
                    const IO::Astrodynamics::Frames::Frames &frame);

        ~StateVector() override = default;

        StateVector(const StateVector &v);

        StateVector &operator=(const StateVector &other);


        /**
         * @brief Get the Position
         *
         * @return const IO::Astrodynamics::Math::Vector3D&
         */
        [[nodiscard]] const IO::Astrodynamics::Math::Vector3D &GetPosition() const
        {
            return m_position;
        }

        /**
         * @brief Get the Velocity
         *
         * @return const IO::Astrodynamics::Math::Vector3D&
         */
        [[nodiscard]] const IO::Astrodynamics::Math::Vector3D &GetVelocity() const
        {
            return m_velocity;
        }

        /**
         * @brief Get the Specific Angular Momentum
         *
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetSpecificAngularMomentum() const override
        {
            return m_momentum;
        }

        /**
         * @brief Get the Period
         *
         * @return IO::Astrodynamics::Time::TimeSpan
         */
        [[nodiscard]] IO::Astrodynamics::Time::TimeSpan GetPeriod() const override;

        /**
         * @brief Get the State Vector
         *
         * @param epoch
         * @return StateVector
         */
        [[nodiscard]] StateVector ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const override;

        /**
         * @brief Get the Semi Major Axis
         *
         * @return double
         */
        [[nodiscard]] double GetSemiMajorAxis() const override;

        /**
         * @brief Get the Eccentricity
         *
         * @return double
         */
        [[nodiscard]] double GetEccentricity() const override;

        /**
         * @brief Get the Inclination
         *
         * @return double
         */
        [[nodiscard]] double GetInclination() const override;

        /**
         * @brief Get the Right Ascending Node Longitude
         *
         * @return double
         */
        [[nodiscard]] double GetRightAscendingNodeLongitude() const override;

        /**
         * @brief Get the Periapsis Argument
         *
         * @return double
         */
        [[nodiscard]] double GetPeriapsisArgument() const override;

        /**
         * @brief Get the Mean Anomaly
         *
         * @return double
         */
        [[nodiscard]] double GetMeanAnomaly() const override;

        /**
         * @brief Get the True Anomaly
         *
         * @return double
         */
        [[nodiscard]] double GetTrueAnomaly() const override;

        /**
         * @brief Get the Specific Orbital Energy
         *
         * @return double
         */
        [[nodiscard]] double GetSpecificOrbitalEnergy() const override;

        /**
         * @brief Compare two state vectors
         *
         * @param other
         * @return true
         * @return false
         */
        bool operator==(const StateVector &other) const;

        /**
         * @brief Check and update if another body becomes the center of motion. If condition occurs the state vector is updated and returned
         * 
         * @param stateVector 
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector CheckAndUpdateCenterOfMotion() const;

        using IO::Astrodynamics::OrbitalParameters::OrbitalParameters::ToStateVector;

        /**
         * @brief Get state vector
         *
         * @return StateVector
         */
        [[nodiscard]] StateVector ToStateVector() const override;

        /**
         * @brief Get this state vector relative to another frame
         *
         * @param frame
         * @return StateVector
         */
        [[nodiscard]] StateVector ToFrame(const IO::Astrodynamics::Frames::Frames &frame) const;

        /**
         * @brief Convert state vector to body fixed frame
         *
         * @return StateVector
         */
        [[nodiscard]] StateVector ToBodyFixedFrame() const;


    };
}
#endif // !STATE_VECTOR_H
