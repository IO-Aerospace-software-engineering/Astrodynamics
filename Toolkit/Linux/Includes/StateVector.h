#ifndef STATE_VECTOR_H
#define STATE_VECTOR_H

/**
 * @file StateVector.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-19
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<array>
#include<memory>
#include<cmath>
#include<limits>

#include <Vector3D.h>
#include <OrbitalParameters.h>
#include <CelestialBody.h>
#include <Body.h>
#include <TimeSpan.h>
#include <TDB.h>
#include <Frames.h>




namespace IO::SDK::OrbitalParameters
{
	/**
	 * @brief State vector class
	 * 
	 */
	class StateVector : public IO::SDK::OrbitalParameters::OrbitalParameters
	{
	private:
		const IO::SDK::Math::Vector3D m_position{};
		const IO::SDK::Math::Vector3D m_velocity{};
		const IO::SDK::Math::Vector3D m_momentum{};
		std::array<SpiceDouble,SPICE_OSCLTX_NELTS> m_osculatingElements{std::numeric_limits<double>::quiet_NaN()};

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
		StateVector(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Math::Vector3D& position, const IO::SDK::Math::Vector3D& velocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		// /**
		//  * @brief Construct a new State Vector object without
		//  * 
		//  * @param centerOfMotion 
		//  * @param position 
		//  * @param epoch 
		//  */
		// StateVector(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Math::Vector3D& position, const IO::SDK::Time::TDB &epoch);

		/**
		 * @brief Construct a new State Vector object
		 * 
		 * @param centerOfMotion 
		 * @param spiceState 
		 * @param epoch 
		 * @param frame 
		 */
		StateVector(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, double spiceState[6], const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		virtual ~StateVector()=default;
		StateVector(const StateVector &v);
		StateVector& operator=(const StateVector& other);


		/**
		 * @brief Get the Position
		 * 
		 * @return const IO::SDK::Math::Vector3D& 
		 */
		const IO::SDK::Math::Vector3D &GetPosition() const
		{
			return m_position;
		}

		/**
		 * @brief Get the Velocity
		 * 
		 * @return const IO::SDK::Math::Vector3D& 
		 */
		const IO::SDK::Math::Vector3D &GetVelocity() const
		{
			return m_velocity;
		}

		/**
		 * @brief Get the Specific Angular Momentum
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		IO::SDK::Math::Vector3D GetSpecificAngularMomentum() const override
		{
			return m_momentum;
		}

		/**
		 * @brief Get the Period
		 * 
		 * @return IO::SDK::Time::TimeSpan 
		 */
		IO::SDK::Time::TimeSpan GetPeriod() const override;

		/**
		 * @brief Get the State Vector
		 * 
		 * @param epoch 
		 * @return StateVector 
		 */
		StateVector GetStateVector(const IO::SDK::Time::TDB &epoch) const override;

		/**
		 * @brief Get the Semi Major Axis
		 * 
		 * @return double 
		 */
		double GetSemiMajorAxis() const override;

		/**
		 * @brief Get the Eccentricity
		 * 
		 * @return double 
		 */
		double GetEccentricity() const override;

		/**
		 * @brief Get the Inclination
		 * 
		 * @return double 
		 */
		double GetInclination() const override;

		/**
		 * @brief Get the Right Ascending Node Longitude
		 * 
		 * @return double 
		 */
		double GetRightAscendingNodeLongitude() const override;

		/**
		 * @brief Get the Periapsis Argument
		 * 
		 * @return double 
		 */
		double GetPeriapsisArgument() const override;

		/**
		 * @brief Get the Mean Anomaly
		 * 
		 * @return double 
		 */
		double GetMeanAnomaly() const override;

		/**
		 * @brief Get the True Anomaly
		 * 
		 * @return double 
		 */
		double GetTrueAnomaly() const override;

		/**
		 * @brief Get the Specific Orbital Energy
		 * 
		 * @return double 
		 */
		double GetSpecificOrbitalEnergy() const override;

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
         * @return IO::SDK::OrbitalParameters::StateVector 
         */
        IO::SDK::OrbitalParameters::StateVector CheckAndUpdateCenterOfMotion() const;

		using IO::SDK::OrbitalParameters::OrbitalParameters::GetStateVector;

		/**
		 * @brief Get state vector
		 * 
		 * @return StateVector 
		 */
		StateVector GetStateVector() const override;

		/**
		 * @brief Get this state vector relative to another frame
		 * 
		 * @param frame 
		 * @return StateVector 
		 */
		StateVector ToFrame(const IO::SDK::Frames::Frames& frame) const;

		/**
		 * @brief Convert state vector to body fixed frame
		 * 
		 * @return StateVector 
		 */
		StateVector ToBodyFixedFrame() const;

		
	};
}
#endif // !STATE_VECTOR_H
